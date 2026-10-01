using System;

namespace FightCore
{
    public enum Phase { Intro, Fight, KO, TimeUp, MatchOver }

    //this is one projectile like a fireball
    public class Projectile
    {
        public bool active;
        public int owner;
        public int x, y, dir;
        public int life, hitsLeft, cooldown, hitstop;
        public int moveIndex;
        public ProjectileDef def;

        public WorldBox Box { get { return def.box.ToWorld(x, y, dir); } }
    }

    //this is the whole fight simulation and it does not know unity exists
    //the engine just calls Tick sixty times a second with both players inputs and then draws what is in here
    public class MatchSim
    {
        public const int FPS = 60;
        //the rules for this match and they never change once the match starts
        public readonly MatchRules rules;
        public int StageHalf { get { return rules.stageHalfWidth; } }
        public bool HasTimer { get { return rules.roundSeconds > 0; } }
        public const int ThrowHoldFrames = 22;

        public readonly FighterSim[] fighters = new FighterSim[2];
        public readonly Projectile[] projectiles = new Projectile[12];
        public readonly SimEvent[] events = new SimEvent[64];
        public int eventCount;

        public int frame;
        public Phase phase;
        public int phaseFrame;
        public int timerFrames;
        public int round = 1;
        public int roundsToWin { get { return rules.roundsToWin; } }
        public int superFreeze;
        public int superFreezeOwner = -1;
        public int matchWinner = -1;
        public int roundWinner = -1;

        public bool training;
        public bool trainingInfiniteMeter { get { return rules.trainingInfiniteMeter; } }
        public bool trainingRefillHealth { get { return rules.trainingRefillHealth; } }

        //this is the damage scaling so combos cannot do crazy amounts of damage
        static readonly int[] Scaling = { 100, 100, 80, 70, 60, 50, 40, 30, 20, 10 };

        readonly WorldBox[] hurtA = new WorldBox[6];

        public MatchSim(FighterDef p1, FighterDef p2, bool training) : this(p1, p2, training, null) { }

        public MatchSim(FighterDef p1, FighterDef p2, bool training, MatchRules rules)
        {
            this.rules = rules != null ? rules.Clone() : new MatchRules();
            this.rules.Sanitize();
            fighters[0] = new FighterSim(0, p1);
            fighters[1] = new FighterSim(1, p2);
            fighters[0].opp = fighters[1];
            fighters[1].opp = fighters[0];
            fighters[0].match = this;
            fighters[1].match = this;
            for (int i = 0; i < projectiles.Length; i++) projectiles[i] = new Projectile();
            this.training = training;
            StartRound();
            if (training)
            {
                phase = Phase.Fight;
                fighters[0].state = FState.Idle;
                fighters[1].state = FState.Idle;
            }
        }

        public bool ControlEnabled { get { return phase == Phase.Fight; } }
        public int TimerSeconds { get { return (timerFrames + FPS - 1) / FPS; } }

        void StartRound()
        {
            fighters[0].ResetForRound(-1500, 1);
            fighters[1].ResetForRound(1500, -1);
            for (int i = 0; i < projectiles.Length; i++) projectiles[i].active = false;
            timerFrames = rules.roundSeconds * FPS;
            phase = Phase.Intro;
            phaseFrame = 0;
            superFreeze = 0;
            roundWinner = -1;
        }

        //this is for training mode so you can put the fighters back wherever you want
        public void ResetPositions(int p1x, int p2x)
        {
            fighters[0].ResetForRound(p1x, p1x < p2x ? 1 : -1);
            fighters[1].ResetForRound(p2x, p1x < p2x ? -1 : 1);
            fighters[0].state = FState.Idle;
            fighters[1].state = FState.Idle;
            for (int i = 0; i < projectiles.Length; i++) projectiles[i].active = false;
            phase = Phase.Fight;
            superFreeze = 0;
        }

        public void Emit(SimEventType t, int player, int x, int y, HitLevel level = HitLevel.Light, int value = 0, int value2 = 0, int move = -1)
        {
            if (eventCount >= events.Length) return;
            events[eventCount++] = new SimEvent { type = t, player = player, x = x, y = y, level = level, value = value, value2 = value2, move = move };
        }

        //this runs exactly one frame of the fight
        public void Tick(FrameInput p1, FrameInput p2)
        {
            eventCount = 0;
            fighters[0].input.Push(p1);
            fighters[1].input.Push(p2);
            frame++;

            //it freezes everything during the super flash but inputs still get buffered
            if (superFreeze > 0)
            {
                superFreeze--;
                return;
            }

            fighters[0].Step();
            fighters[1].Step();
            UpdateThrows();
            UpdateProjectiles();
            ResolvePush();
            UpdateFacing();
            ResolveHits();
            ResolveProjectileHits();
            RoundLogic();
        }

        public void StartSuperFreeze(int owner, int frames)
        {
            superFreeze = frames;
            superFreezeOwner = owner;
            Emit(SimEventType.SuperFlash, owner, fighters[owner].x, fighters[owner].y + 1200, HitLevel.Super);
        }

        void UpdateFacing()
        {
            for (int i = 0; i < 2; i++)
            {
                var f = fighters[i];
                if (f.airborne) continue;
                if (f.IsActionable || f.state == FState.Land || f.state == FState.PreJump || f.state == FState.Intro)
                    f.FaceOpponent();
            }
        }

        //this keeps fighters from walking through each other and keeps them inside the stage
        void ResolvePush()
        {
            var a = fighters[0];
            var b = fighters[1];
            bool skip = a.state == FState.Thrown || b.state == FState.Thrown || a.state == FState.Knockdown || b.state == FState.Knockdown;
            if (!skip)
            {
                var pa = a.GetPushbox();
                var pb = b.GetPushbox();
                if (pa.bottom < pb.top && pb.bottom < pa.top)
                {
                    int half = (a.def.pushWidth + b.def.pushWidth) / 2;
                    int dx = b.x - a.x;
                    int dist = Math.Abs(dx);
                    if (dist < half)
                    {
                        int dir = dx > 0 ? 1 : (dx < 0 ? -1 : (a.facing > 0 ? 1 : -1));
                        int overlap = half - dist;
                        int moveA = overlap / 2, moveB = overlap - moveA;
                        //it pushes a jumping fighter more so crossups feel natural
                        if (a.airborne && !b.airborne) { moveA = overlap; moveB = 0; }
                        else if (b.airborne && !a.airborne) { moveB = overlap; moveA = 0; }
                        a.x -= dir * moveA;
                        b.x += dir * moveB;
                        ClampWall(a, b);
                        ClampWall(b, a);
                        //if one of them is stuck in the corner the other one gets pushed out instead
                        dx = b.x - a.x;
                        dist = Math.Abs(dx);
                        if (dist < half)
                        {
                            int left = WallLimit(a) ;
                            if (Math.Abs(a.x) >= left) b.x = a.x + dir * half; else a.x = b.x - dir * half;
                        }
                    }
                }
            }
            ClampWall(a, b);
            ClampWall(b, a);

            //this stops the fighters from getting farther apart than the camera can show
            int sep = Math.Abs(b.x - a.x);
            if (sep > rules.maxSeparation)
            {
                int excess = sep - rules.maxSeparation;
                bool aAway = (a.x - a.prevX) * (a.x - b.x) > 0;
                bool bAway = (b.x - b.prevX) * (b.x - a.x) > 0;
                int sA = a.x < b.x ? 1 : -1;
                if (aAway && !bAway) a.x += sA * excess;
                else if (bAway && !aAway) b.x -= sA * excess;
                else { a.x += sA * (excess / 2); b.x -= sA * (excess - excess / 2); }
            }
        }

        int WallLimit(FighterSim f) { return StageHalf - f.def.pushWidth / 2; }

        void ClampWall(FighterSim f, FighterSim other)
        {
            int lim = WallLimit(f);
            int over = 0;
            if (f.x > lim) { over = f.x - lim; f.x = lim; }
            else if (f.x < -lim) { over = f.x + lim; f.x = -lim; }
            //this is corner pushback where the attacker gets pushed away when the defender cannot go back any more
            if (over != 0 && f.slide > 0 && f.slideFromMelee && !other.airborne &&
                (f.state == FState.HitStun || f.state == FState.BlockStun))
            {
                other.x -= over;
            }
        }

        public bool HasProjectile(int owner)
        {
            for (int i = 0; i < projectiles.Length; i++)
                if (projectiles[i].active && projectiles[i].owner == owner) return true;
            return false;
        }

        //this is used for proximity guard and the ai
        public bool IsThreatened(FighterSim d)
        {
            var a = d.opp;
            if (a.state == FState.Attack && !a.Move.IsThrow && a.moveFrame < a.Move.startup + a.Move.active &&
                Math.Abs(a.x - d.x) < a.Move.Reach() + 900)
                return true;
            for (int i = 0; i < projectiles.Length; i++)
            {
                var p = projectiles[i];
                if (!p.active || p.owner == d.index) continue;
                int dist = (d.x - p.x) * p.dir;
                if (dist > 0 && dist < 3500) return true;
            }
            return false;
        }

        public void SpawnProjectile(FighterSim o, MoveDef m)
        {
            for (int i = 0; i < projectiles.Length; i++)
            {
                var p = projectiles[i];
                if (p.active) continue;
                p.active = true;
                p.owner = o.index;
                p.def = m.projectile;
                p.dir = o.facing;
                p.x = o.x + o.facing * m.projectile.spawnX;
                p.y = o.y + m.projectile.spawnY;
                p.life = m.projectile.lifetime;
                p.hitsLeft = m.projectile.durability;
                p.cooldown = 0;
                p.hitstop = 0;
                p.moveIndex = m.index;
                Emit(SimEventType.ProjectileSpawn, o.index, p.x, p.y, m.projectile.hit.level, i, 0, m.index);
                return;
            }
        }

        void UpdateProjectiles()
        {
            for (int i = 0; i < projectiles.Length; i++)
            {
                var p = projectiles[i];
                if (!p.active) continue;
                if (p.hitstop > 0) { p.hitstop--; continue; }
                if (p.cooldown > 0) p.cooldown--;
                p.x += p.dir * p.def.speed;
                p.life--;
                if (p.life <= 0 || Math.Abs(p.x) > StageHalf + 1500) KillProjectile(i);
            }
        }

        void KillProjectile(int i)
        {
            var p = projectiles[i];
            p.active = false;
            Emit(SimEventType.ProjectileEnd, p.owner, p.x, p.y + p.def.box.cy, p.def.hit.level, i, 0, p.moveIndex);
        }

        struct StrikeHit
        {
            public bool valid;
            public int group;
            public int cx, cy;
        }

        StrikeHit FindStrike(FighterSim a, FighterSim d)
        {
            var r = new StrikeHit();
            if (a.state != FState.Attack || a.hitstop > 0) return r;
            var m = a.Move;
            if (m.IsThrow || d.IsStrikeInvulnerable) return r;
            int hurtCount = d.GetHurtboxes(hurtA);
            for (int i = 0; i < m.hitboxes.Count; i++)
            {
                var hb = m.hitboxes[i];
                if (!hb.ActiveOn(a.moveFrame)) continue;
                if ((a.hitGroupMask & (1 << hb.hitGroup)) != 0) continue;
                var wb = hb.rect.ToWorld(a.x, a.y, a.facing);
                for (int h = 0; h < hurtCount; h++)
                {
                    if (!wb.Overlaps(hurtA[h])) continue;
                    r.valid = true;
                    r.group = hb.hitGroup;
                    //it puts the spark in the middle of where the boxes overlap
                    r.cx = (Math.Max(wb.left, hurtA[h].left) + Math.Min(wb.right, hurtA[h].right)) / 2;
                    r.cy = (Math.Max(wb.bottom, hurtA[h].bottom) + Math.Min(wb.top, hurtA[h].top)) / 2;
                    return r;
                }
            }
            return r;
        }

        bool CheckThrow(FighterSim a, FighterSim d)
        {
            if (a.state != FState.Attack || a.hitstop > 0) return false;
            var m = a.Move;
            if (!m.IsThrow || !m.IsActiveFrame(a.moveFrame) || a.moveConnected) return false;
            if (!d.IsThrowable) return false;
            return Math.Abs(a.x - d.x) <= m.throwRange;
        }

        //this checks for all the hits this frame at the same time so trades can happen
        void ResolveHits()
        {
            var f0 = fighters[0];
            var f1 = fighters[1];
            //it grabs both moves first because getting hit clears the move and we still need them after
            var m0 = f0.Move;
            var m1 = f1.Move;
            var s0 = FindStrike(f0, f1);
            var s1 = FindStrike(f1, f0);
            bool t0 = CheckThrow(f0, f1);
            bool t1 = CheckThrow(f1, f0);

            if (s0.valid) { f0.hitGroupMask |= 1 << s0.group; hitMoveIndex = m0.index; ApplyHit(f0, f1, m0.HitFor(s0.group), s0.cx, s0.cy, false, m0.isSuper, f0.x); }
            if (s1.valid) { f1.hitGroupMask |= 1 << s1.group; hitMoveIndex = m1.index; ApplyHit(f1, f0, m1.HitFor(s1.group), s1.cx, s1.cy, false, m1.isSuper, f1.x); }

            //attacks beat throws so if you got hit this frame your throw does not happen
            if (s1.valid) t0 = false;
            if (s0.valid) t1 = false;
            if (t0 && t1)
            {
                if (m0.kind == MoveKind.CommandThrow && m1.kind != MoveKind.CommandThrow) StartThrow(f0, f1);
                else if (m1.kind == MoveKind.CommandThrow && m0.kind != MoveKind.CommandThrow) StartThrow(f1, f0);
                else TechThrow(f0, f1);
            }
            else if (t0) StartThrow(f0, f1);
            else if (t1) StartThrow(f1, f0);
        }

        void ResolveProjectileHits()
        {
            //it lets fireballs cancel each other out when they touch
            for (int i = 0; i < projectiles.Length; i++)
            {
                var a = projectiles[i];
                if (!a.active || a.cooldown > 0) continue;
                for (int j = i + 1; j < projectiles.Length; j++)
                {
                    var b = projectiles[j];
                    if (!b.active || b.owner == a.owner || b.cooldown > 0) continue;
                    if (!a.Box.Overlaps(b.Box)) continue;
                    a.hitsLeft--; b.hitsLeft--;
                    a.cooldown = a.def.hitInterval; b.cooldown = b.def.hitInterval;
                    Emit(SimEventType.ProjectileClash, a.owner, (a.x + b.x) / 2, a.y + a.def.box.cy, HitLevel.Special);
                    if (a.hitsLeft <= 0) KillProjectile(i);
                    if (b.hitsLeft <= 0) KillProjectile(j);
                    if (!a.active) break;
                }
            }

            for (int i = 0; i < projectiles.Length; i++)
            {
                var p = projectiles[i];
                if (!p.active || p.cooldown > 0) continue;
                var d = fighters[1 - p.owner];
                if (d.IsStrikeInvulnerable) continue;
                int n = d.GetHurtboxes(hurtA);
                var box = p.Box;
                for (int h = 0; h < n; h++)
                {
                    if (!box.Overlaps(hurtA[h])) continue;
                    var owner = fighters[p.owner];
                    var ownerMove = owner.def.moves[p.moveIndex];
                    hitMoveIndex = p.moveIndex;
                    ApplyHit(owner, d, p.def.hit, box.CenterX, box.CenterY, true, ownerMove.isSuper, p.x - p.dir * 1000);
                    p.hitsLeft--;
                    p.cooldown = p.def.hitInterval;
                    p.hitstop = Math.Max(4, p.def.hit.hitstop - 2);
                    if (p.hitsLeft <= 0) KillProjectile(i);
                    break;
                }
            }
        }

        //this decides if the defender blocks
        //you have to hold away from where the attack is coming from and crouch for lows and stand for overheads
        public bool CanBlock(FighterSim d, GuardType guard, int sourceX)
        {
            if (guard == GuardType.Unblockable || d.airborne || d.dead) return false;
            switch (d.state)
            {
                case FState.Idle: case FState.WalkF: case FState.WalkB: case FState.Crouch: case FState.BlockStun:
                    break;
                default:
                    return false;
            }
            var inp = d.input.Get(0);
            bool attackerOnRight = sourceX > d.x || (sourceX == d.x && d.facing > 0);
            bool holdingBack = attackerOnRight ? inp.Has(Btn.Left) : inp.Has(Btn.Right);
            if (!holdingBack) return false;
            bool crouch = inp.Has(Btn.Down);
            if (guard == GuardType.Low && !crouch) return false;
            if (guard == GuardType.High && crouch) return false;
            return true;
        }

        static int Scale(int hitNumber, bool super)
        {
            int s = Scaling[Math.Min(hitNumber - 1, Scaling.Length - 1)];
            if (super) s = Math.Max(s, 50);
            return s;
        }

        //this remembers which move caused the hit so the unity side can play that move's own sound or spark
        //it is only for visuals and is never part of the fight logic
        int hitMoveIndex = -1;

        void ApplyHit(FighterSim a, FighterSim d, HitData h, int cx, int cy, bool projectile, bool super, int sourceX)
        {
            int awayDir = d.x > sourceX ? 1 : (d.x < sourceX ? -1 : -d.facing);

            if (CanBlock(d, h.guard, sourceX))
            {
                d.moveIndex = -1;
                d.state = FState.BlockStun;
                d.stateFrame = 0;
                d.stun = h.blockstun;
                d.crouchBlocking = d.input.Get(0).Has(Btn.Down);
                d.lastBlockGuard = h.guard;
                d.vx = 0;
                d.slide = h.pushbackBlock / 5;
                d.slideDir = awayDir;
                d.slideFromMelee = !projectile;
                if (h.chip > 0)
                {
                    d.health -= h.chip;
                    if (d.health <= 0 && !(h.chipCanKO && !training)) d.health = 1;
                }
                int stop = Math.Max(4, h.hitstop - 2);
                d.hitstop = stop;
                if (!projectile) { a.hitstop = stop; a.moveConnected = true; a.cancelSinceFrame = a.input.NewestFrame; }
                a.AddMeter(h.meterGain / 2);
                d.AddMeter(h.meterGain / 3);
                Emit(SimEventType.Block, a.index, cx, cy, h.level, d.index, 0, hitMoveIndex);
                if (d.health <= 0) KO(d);
                return;
            }

            bool counter = d.state == FState.Attack && d.Move != null && d.moveFrame < d.Move.startup + d.Move.active;
            bool comboing = d.state == FState.HitStun || d.state == FState.AirHitStun;
            if (!comboing) { d.comboHits = 0; d.comboDamage = 0; d.juggle = 0; }
            d.comboHits++;
            int dmg = h.damage * Scale(d.comboHits, super) / 100;
            if (counter) dmg = dmg * 12 / 10;
            d.health -= dmg;
            d.comboDamage += dmg;
            d.counterHitTaken = counter;
            d.lastHurtFrame = frame;

            bool wasCrouching = d.IsCrouchingPosture;
            bool launch = d.airborne || h.effect != HitEffect.Normal || d.health <= 0;
            d.moveIndex = -1;
            d.stateFrame = 0;
            if (launch)
            {
                if (d.airborne) d.juggle++;
                d.state = FState.AirHitStun;
                d.airborne = true;
                if (d.y <= 0) d.y = 1;
                d.vy = h.launchVY > 0 ? h.launchVY : 150;
                d.vx = awayDir * h.launchVX;
                d.hardKnockdown = h.effect == HitEffect.HardKnockdown;
                d.slide = 0;
            }
            else
            {
                d.state = FState.HitStun;
                d.stun = h.hitstun + (counter ? 2 : 0);
                d.crouchHit = wasCrouching;
                d.vx = 0;
                d.slide = h.pushbackHit / 5;
                d.slideDir = awayDir;
                d.slideFromMelee = !projectile;
            }

            int hitstop = h.hitstop + (counter ? 3 : 0);
            d.hitstop = hitstop;
            if (!projectile)
            {
                a.hitstop = hitstop;
                a.moveConnected = true;
                a.moveHitConfirmed = true;
                a.cancelSinceFrame = a.input.NewestFrame;
            }
            a.AddMeter(h.meterGain);
            d.AddMeter(h.meterGain / 3);
            Emit(counter ? SimEventType.CounterHit : SimEventType.Hit, a.index, cx, cy, h.level, d.index, dmg, hitMoveIndex);

            if (d.health <= 0) KO(d);
        }

        void KO(FighterSim d)
        {
            if (training) { d.health = 1; return; }
            d.health = 0;
            if (d.dead) return;
            d.dead = true;
            if (!d.airborne)
            {
                d.state = FState.AirHitStun;
                d.airborne = true;
                d.y = 1;
                d.vy = 160;
                d.vx = -d.facing * 40;
            }
            d.hardKnockdown = true;
            bool other = fighters[1 - d.index].dead;
            if (phase == Phase.Fight)
            {
                phase = Phase.KO;
                phaseFrame = 0;
            }
            Emit(other ? SimEventType.DoubleKO : SimEventType.KO, d.index, d.x, d.y + 900, HitLevel.Super);
        }

        void StartThrow(FighterSim a, FighterSim d)
        {
            var m = a.Move;
            a.moveConnected = true;
            a.throwMoveIndex = m.index;
            a.state = FState.Throwing; a.stateFrame = 0; a.vx = 0;
            d.state = FState.Thrown; d.stateFrame = 0; d.vx = 0; d.vy = 0; d.moveIndex = -1; d.slide = 0;
            d.throwTechable = m.kind == MoveKind.Throw;
            d.throwMoveIndex = m.index;
            d.x = a.x + a.facing * 650;
            ClampWall(d, a);
            Emit(SimEventType.ThrowStart, a.index, (a.x + d.x) / 2, 1100, HitLevel.Heavy, d.index);
        }

        void TechThrow(FighterSim a, FighterSim b)
        {
            var left = a.x <= b.x ? a : b;
            var right = left == a ? b : a;
            foreach (var f in fighters)
            {
                f.state = FState.TechRecover;
                f.stateFrame = 0;
                f.moveIndex = -1;
                f.vx = 0;
                f.slide = 140;
                f.slideFromMelee = false;
                f.throwInvuln = 10;
            }
            left.slideDir = -1;
            right.slideDir = 1;
            Emit(SimEventType.ThrowTech, a.index, (a.x + b.x) / 2, 1100, HitLevel.Medium);
        }

        //this runs throws that already grabbed and checks for techs
        void UpdateThrows()
        {
            for (int i = 0; i < 2; i++)
            {
                var d = fighters[i];
                if (d.state != FState.Thrown) continue;
                var a = d.opp;
                var m = a.def.moves[d.throwMoveIndex];
                if (d.throwTechable && d.stateFrame <= FighterSim.ThrowTechWindow && d.PressedThrowRecently(Math.Min(d.stateFrame + 1, 6)))
                {
                    TechThrow(a, d);
                    continue;
                }
                if (d.stateFrame < ThrowHoldFrames) continue;

                bool swap = m.throwSwapSides;
                d.x = a.x + (swap ? -a.facing : a.facing) * 700;
                int away = swap ? -a.facing : a.facing;
                int dmg = m.hit.damage;
                d.health -= dmg;
                d.lastHurtFrame = frame;
                d.comboHits = 1;
                d.comboDamage = dmg;
                d.state = FState.AirHitStun;
                d.stateFrame = 0;
                d.airborne = true;
                d.y = 300;
                d.vy = m.hit.launchVY > 0 ? m.hit.launchVY : 120;
                d.vx = away * Math.Max(20, m.hit.launchVX);
                d.hardKnockdown = true;
                d.juggle = rules.juggleLimit;
                ClampWall(d, a);
                a.AddMeter(m.hit.meterGain);

                a.state = FState.Land;
                a.stateFrame = 0;
                a.landRecover = Math.Max(1, m.recovery / 2);
                a.moveIndex = -1;
                Emit(SimEventType.ThrowLand, a.index, d.x, 600, HitLevel.Heavy, d.index, dmg);
                if (d.health <= 0) KO(d);
            }
        }

        void RoundLogic()
        {
            if (training)
            {
                for (int i = 0; i < 2; i++)
                {
                    var f = fighters[i];
                    if (trainingInfiniteMeter) f.meter = FighterSim.MaxMeter;
                    if (trainingRefillHealth && f.IsActionable && frame - f.lastHurtFrame > 60) f.health = f.def.maxHealth;
                }
                return;
            }

            phaseFrame++;
            switch (phase)
            {
                case Phase.Intro:
                    if (phaseFrame == 1) Emit(SimEventType.RoundAnnounce, -1, 0, 0, HitLevel.Light, round);
                    if (phaseFrame >= rules.introFrames)
                    {
                        phase = Phase.Fight;
                        phaseFrame = 0;
                        fighters[0].state = FState.Idle;
                        fighters[1].state = FState.Idle;
                        Emit(SimEventType.Fight, -1, 0, 0);
                    }
                    break;

                case Phase.Fight:
                    //if the timer is turned off the round just goes until someone gets knocked out
                    if (!HasTimer) break;
                    timerFrames--;
                    if (timerFrames <= 0)
                    {
                        timerFrames = 0;
                        phase = Phase.TimeUp;
                        phaseFrame = 0;
                        Emit(SimEventType.TimeUp, -1, 0, 0);
                    }
                    break;

                case Phase.KO:
                case Phase.TimeUp:
                    if (phaseFrame == 90) DecideRoundWinner();
                    if (phaseFrame > 90 && roundWinner >= 0)
                    {
                        var w = fighters[roundWinner];
                        if (w.IsActionable && w.state != FState.Win) { w.state = FState.Win; w.stateFrame = 0; w.vx = 0; }
                    }
                    if (phaseFrame >= rules.roundEndFrames) EndRound();
                    break;

                case Phase.MatchOver:
                    break;
            }
        }

        void DecideRoundWinner()
        {
            var a = fighters[0];
            var b = fighters[1];
            if (a.dead && b.dead) roundWinner = -1;
            else if (b.dead) roundWinner = 0;
            else if (a.dead) roundWinner = 1;
            else
            {
                //on time out the one with more health left by percent wins
                long pa = (long)a.health * 1000 / a.def.maxHealth;
                long pb = (long)b.health * 1000 / b.def.maxHealth;
                roundWinner = pa > pb ? 0 : (pb > pa ? 1 : -1);
            }
            if (roundWinner >= 0)
            {
                fighters[roundWinner].roundWins++;
                Emit(SimEventType.RoundWin, roundWinner, 0, 0, HitLevel.Light, round);
            }
            else Emit(SimEventType.Draw, -1, 0, 0, HitLevel.Light, round);
        }

        void EndRound()
        {
            for (int i = 0; i < 2; i++)
            {
                if (fighters[i].roundWins >= roundsToWin && fighters[1 - i].roundWins < fighters[i].roundWins)
                {
                    phase = Phase.MatchOver;
                    phaseFrame = 0;
                    matchWinner = i;
                    Emit(SimEventType.MatchWin, i, 0, 0);
                    return;
                }
            }
            //it caps the match so draws cannot go on forever
            if (round >= 5)
            {
                phase = Phase.MatchOver;
                phaseFrame = 0;
                matchWinner = fighters[0].roundWins > fighters[1].roundWins ? 0 : (fighters[1].roundWins > fighters[0].roundWins ? 1 : -1);
                Emit(SimEventType.MatchWin, matchWinner, 0, 0);
                return;
            }
            round++;
            StartRound();
        }

        //this makes a number out of the whole game state so you can check two runs are exactly the same
        public uint Checksum()
        {
            uint h = 2166136261u;
            h = Mix(h, frame); h = Mix(h, (int)phase); h = Mix(h, timerFrames); h = Mix(h, superFreeze);
            for (int i = 0; i < 2; i++)
            {
                var f = fighters[i];
                h = Mix(h, f.x); h = Mix(h, f.y); h = Mix(h, f.vx); h = Mix(h, f.vy); h = Mix(h, f.facing);
                h = Mix(h, (int)f.state); h = Mix(h, f.stateFrame); h = Mix(h, f.moveIndex); h = Mix(h, f.moveFrame);
                h = Mix(h, f.health); h = Mix(h, f.meter); h = Mix(h, f.hitstop); h = Mix(h, f.stun); h = Mix(h, f.slide);
            }
            for (int i = 0; i < projectiles.Length; i++)
            {
                var p = projectiles[i];
                if (!p.active) continue;
                h = Mix(h, p.x); h = Mix(h, p.y); h = Mix(h, p.life); h = Mix(h, p.hitsLeft);
            }
            return h;
        }

        static uint Mix(uint h, int v)
        {
            unchecked
            {
                h ^= (uint)v;
                h *= 16777619u;
                return h;
            }
        }
    }
}

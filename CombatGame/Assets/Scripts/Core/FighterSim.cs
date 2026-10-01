using System;

namespace FightCore
{
    //these are all the states a fighter can be in
    //the rules for which state can go to which are all in FighterSim
    public enum FState : byte
    {
        Intro, Idle, WalkF, WalkB, Crouch, PreJump, Air, Land, DashF, DashB,
        Attack, HitStun, AirHitStun, BlockStun, Knockdown, Thrown, Throwing, TechRecover, KO, Win
    }

    //this is one fighter in the simulation
    //everything is ints so it is fully deterministic and the same inputs always give the same result
    public class FighterSim
    {
        public readonly int index;
        public readonly FighterDef def;
        public MatchSim match;
        public FighterSim opp;
        public readonly InputBuffer input = new InputBuffer();

        //position and movement
        public int x, y, vx, vy;
        public int prevX;
        public int facing = 1;
        public bool airborne;
        public int slide, slideDir;
        public bool slideFromMelee;

        //state machine stuff
        public FState state = FState.Intro;
        public int stateFrame;
        public int moveIndex = -1;
        public int moveFrame;
        public int hitstop;
        public int stun;
        public int landRecover;
        public int knockdownLeft;
        public int jumpDir;
        public bool airAttackUsed;
        public bool crouchBlocking;
        public bool crouchHit;
        public bool hardKnockdown;
        public bool dead;

        //attack tracking
        public int hitGroupMask;
        public bool moveConnected;
        public bool moveHitConfirmed;
        public bool projectileSpawned;
        public int consumedPressFrame = -1;
        public int cancelSinceFrame = -1;
        public bool justActionable;
        public int lastStartedMove = -1;

        //getting hit tracking
        public int comboHits, comboDamage, juggle;
        public bool counterHitTaken;
        public int throwInvuln;
        public int lastHurtFrame = -9999;
        public bool throwTechable;
        public int throwMoveIndex = -1;
        public GuardType lastBlockGuard;

        public int health, meter;
        public int roundWins;

        public const int MaxMeter = 300;
        public const int ThrowTechWindow = 8;

        public FighterSim(int index, FighterDef def)
        {
            this.index = index;
            this.def = def;
            def.Build();
            health = def.maxHealth;
        }

        public MoveDef Move { get { return moveIndex >= 0 ? def.moves[moveIndex] : null; } }
        public bool FacingRight { get { return facing > 0; } }

        public bool IsActionable
        {
            get { return state == FState.Idle || state == FState.WalkF || state == FState.WalkB || state == FState.Crouch; }
        }

        public bool IsCrouchingPosture
        {
            get
            {
                if (airborne) return false;
                switch (state)
                {
                    case FState.Crouch: return true;
                    case FState.BlockStun: return crouchBlocking;
                    case FState.HitStun: return crouchHit;
                    case FState.Attack: return Move != null && Move.crouching;
                }
                return false;
            }
        }

        //this is where strikes go right through the fighter
        public bool IsStrikeInvulnerable
        {
            get
            {
                switch (state)
                {
                    case FState.Knockdown:
                    case FState.Thrown:
                    case FState.Throwing:
                    case FState.Intro:
                    case FState.KO:
                    case FState.Win:
                        return true;
                    case FState.AirHitStun:
                        return juggle >= MatchSim.JuggleLimit || dead;
                    case FState.DashB:
                        return stateFrame <= 8;
                    case FState.Attack:
                        return Move.InvulnOn(moveFrame);
                }
                return false;
            }
        }

        public bool IsThrowable
        {
            get
            {
                if (airborne || throwInvuln > 0 || dead) return false;
                switch (state)
                {
                    case FState.Idle: case FState.WalkF: case FState.WalkB: case FState.Crouch:
                    case FState.PreJump: case FState.Land: case FState.DashF: case FState.DashB:
                        return true;
                    case FState.Attack:
                        return !(Move.throwInvuln && Move.InvulnOn(moveFrame));
                }
                return false;
            }
        }

        public void ResetForRound(int startX, int startFacing)
        {
            x = prevX = startX; y = 0; vx = vy = 0;
            facing = startFacing;
            airborne = false;
            slide = 0;
            state = FState.Intro; stateFrame = 0;
            moveIndex = -1; moveFrame = 0;
            hitstop = stun = landRecover = knockdownLeft = 0;
            comboHits = comboDamage = juggle = 0;
            throwInvuln = 0;
            dead = false;
            health = def.maxHealth;
            consumedPressFrame = input.NewestFrame;
        }

        void SetState(FState s)
        {
            if (state != s) { state = s; stateFrame = 0; }
        }

        //this runs one frame of the fighter
        public void Step()
        {
            prevX = x;
            if (hitstop > 0) { hitstop--; return; }
            stateFrame++;
            if (throwInvuln > 0) throwInvuln--;
            bool ctrl = match.ControlEnabled;

            switch (state)
            {
                case FState.Idle:
                case FState.WalkF:
                case FState.WalkB:
                case FState.Crouch:
                    if (ctrl && TryStartMove()) break;
                    if (ctrl) GroundMovement();
                    else { SetState(FState.Idle); vx = 0; }
                    break;

                case FState.PreJump:
                    if (stateFrame >= def.prejump) StartJump();
                    break;

                case FState.Air:
                    if (ctrl && !airAttackUsed) TryStartMove();
                    break;

                case FState.Land:
                    if (stateFrame >= landRecover) BecomeActionable();
                    break;

                case FState.DashF:
                    vx = (stateFrame > def.dashFrames - 4 ? def.dashSpeed / 2 : def.dashSpeed) * facing;
                    if (stateFrame >= def.dashFrames) BecomeActionable();
                    break;

                case FState.DashB:
                    vx = (stateFrame > def.backdashFrames - 4 ? -def.backdashSpeed / 2 : -def.backdashSpeed) * facing;
                    if (stateFrame >= def.backdashFrames) BecomeActionable();
                    break;

                case FState.Attack:
                    AttackStep(ctrl);
                    break;

                case FState.HitStun:
                    stun--;
                    if (stun <= 0) { BecomeActionable(); throwInvuln = 3; }
                    break;

                case FState.BlockStun:
                    //it lets you switch between high and low block while you are stuck blocking
                    crouchBlocking = input.Get(0).Has(Btn.Down);
                    stun--;
                    if (stun <= 0) { BecomeActionable(); throwInvuln = 3; }
                    break;

                case FState.Knockdown:
                    knockdownLeft--;
                    if (knockdownLeft <= 0)
                    {
                        if (dead) { SetState(FState.KO); break; }
                        BecomeActionable();
                        throwInvuln = 6;
                    }
                    break;

                case FState.TechRecover:
                    if (stateFrame >= 16) BecomeActionable();
                    break;
            }

            Physics();
        }

        //this is what happens when a fighter gets control back
        //it faces the opponent and marks the frame so a buffered reversal gets a bit more leniency
        public void BecomeActionable()
        {
            moveIndex = -1;
            vx = 0;
            FaceOpponent();
            bool down = input.Get(0).Has(Btn.Down);
            state = down ? FState.Crouch : FState.Idle;
            stateFrame = 0;
            justActionable = true;
            comboHits = 0;
            juggle = 0;
        }

        public void FaceOpponent()
        {
            if (opp == null) return;
            if (opp.x > x) facing = 1;
            else if (opp.x < x) facing = -1;
        }

        void GroundMovement()
        {
            bool fr = FacingRight;
            int n = input.Get(0).Numpad(fr);

            if (MotionReader.Check(Motion.DashForward, input, 0, fr))
            {
                SetState(FState.DashF); stateFrame = 0;
                match.Emit(SimEventType.Dash, index, x, y);
                return;
            }
            if (MotionReader.Check(Motion.DashBack, input, 0, fr))
            {
                SetState(FState.DashB); stateFrame = 0;
                match.Emit(SimEventType.Dash, index, x, y);
                return;
            }
            if (n >= 7)
            {
                SetState(FState.PreJump);
                jumpDir = n == 7 ? -1 : (n == 9 ? 1 : 0);
                vx = 0;
                return;
            }
            if (n <= 3) { SetState(FState.Crouch); vx = 0; return; }
            if (n == 6) { SetState(FState.WalkF); vx = def.walkForward * facing; return; }
            if (n == 4)
            {
                SetState(FState.WalkB);
                //it stops walking back when something is coming at you so holding back feels like guarding
                vx = match.IsThreatened(this) ? 0 : -def.walkBack * facing;
                return;
            }
            SetState(FState.Idle);
            vx = 0;
        }

        void StartJump()
        {
            airborne = true;
            y = 1;
            vy = def.jumpVY;
            vx = jumpDir * def.jumpVX * facing;
            airAttackUsed = false;
            SetState(FState.Air);
            match.Emit(SimEventType.Jump, index, x, y);
        }

        int CurrentGravity()
        {
            if (state == FState.Attack && Move.gravity > 0) return Move.gravity;
            if (state == FState.AirHitStun) return 12;
            return def.gravity;
        }

        void Physics()
        {
            if (slide > 0)
            {
                x += slideDir * slide;
                slide = slide * 4 / 5;
                if (slide < 3) slide = 0;
            }
            x += vx;
            if (airborne)
            {
                y += vy;
                vy -= CurrentGravity();
                if (y <= 0 && vy < 0)
                {
                    y = 0;
                    OnLand();
                }
            }
        }

        void OnLand()
        {
            airborne = false;
            vy = 0;
            switch (state)
            {
                case FState.Air:
                    vx = 0;
                    landRecover = def.landFrames;
                    SetState(FState.Land);
                    match.Emit(SimEventType.Land, index, x, 0);
                    break;

                case FState.Attack:
                    {
                        var m = Move;
                        bool airNormal = m.dir == DirReq.Air || m.dir == DirReq.AirDown;
                        landRecover = airNormal ? def.landFrames : Math.Max(1, m.landingRecovery);
                        moveIndex = -1;
                        vx = 0;
                        state = FState.Land; stateFrame = 0;
                        match.Emit(SimEventType.Land, index, x, 0);
                        break;
                    }

                case FState.AirHitStun:
                    vx = 0;
                    //it lets you quick rise if you are holding a button when you hit the ground
                    bool quick = !hardKnockdown && input.Get(0).Buttons != Btn.None;
                    knockdownLeft = hardKnockdown ? 44 : (quick ? 22 : 32);
                    SetState(FState.Knockdown);
                    match.Emit(SimEventType.Knockdown, index, x, 0);
                    break;

                case FState.KO:
                    vx = 0;
                    break;

                default:
                    vx = 0;
                    landRecover = def.landFrames;
                    SetState(FState.Land);
                    break;
            }
        }

        void AttackStep(bool ctrl)
        {
            var m = Move;
            //cancels are just moves that are allowed to start in the middle of another move
            if (ctrl && moveFrame >= m.startup && moveFrame <= m.CancelEndFrame && TryStartMove()) return;
            AdvanceMove();
        }

        void AdvanceMove()
        {
            var m = Move;
            moveFrame++;
            for (int i = 0; i < m.velocity.Count; i++)
            {
                var k = m.velocity[i];
                if (k.frame != moveFrame) continue;
                vx = k.vx * facing;
                if (k.airborne)
                {
                    if (!airborne) y = Math.Max(y, 1);
                    airborne = true;
                    vy = k.vy;
                }
                else if (airborne && k.vy != 0) vy = k.vy;
            }

            if (m.spawnsProjectile && !projectileSpawned && moveFrame >= m.projectileFrame)
            {
                projectileSpawned = true;
                match.SpawnProjectile(this, m);
            }

            if (moveFrame > m.TotalFrames)
            {
                if (airborne)
                {
                    bool airNormal = m.dir == DirReq.Air || m.dir == DirReq.AirDown;
                    if (airNormal && m.kind == MoveKind.Normal)
                    {
                        moveIndex = -1;
                        SetState(FState.Air);
                    }
                    else moveFrame = m.TotalFrames;
                }
                else
                {
                    BecomeActionable();
                }
            }
        }

        //this is the big one that reads the buffer and figures out if a move should come out
        //it checks moves in priority order so supers and dragon punches win over simpler stuff
        public bool TryStartMove()
        {
            bool fr = FacingRight;
            int window = justActionable ? 8 : 4;
            justActionable = false;
            int newest = input.NewestFrame;
            int maxAgo = window;
            if (state == FState.Attack && cancelSinceFrame >= 0) maxAgo = Math.Max(maxAgo, newest - cancelSinceFrame);
            maxAgo = Math.Min(maxAgo, newest - consumedPressFrame - 1);
            maxAgo = Math.Min(maxAgo, 40);
            if (maxAgo < 0) return false;

            var order = def.priorityOrder;
            for (int o = 0; o < order.Length; o++)
            {
                var m = def.moves[order[o]];
                if (!Eligible(m)) continue;
                for (int f = 0; f <= maxAgo; f++)
                {
                    if (!PressFor(m, f)) continue;
                    if (!DirOk(m, input.NumpadAt(f, fr))) continue;
                    if (m.motion != Motion.None && !MotionReader.Check(m.motion, input, f, fr)) continue;
                    consumedPressFrame = newest - f;
                    StartMove(m);
                    return true;
                }
            }
            return false;
        }

        //this is the rule list for which moves can start from which state
        public bool Eligible(MoveDef m)
        {
            if (m.meterCost > meter) return false;
            if (m.spawnsProjectile && match.HasProjectile(index)) return false;
            bool airMove = m.dir == DirReq.Air || m.dir == DirReq.AirDown;
            if (airborne != airMove) return false;

            if (state == FState.Attack)
            {
                var cur = Move;
                if (cur == null) return false;
                if (m.requiresPrevIndex >= 0)
                    return m.requiresPrevIndex == cur.index && moveFrame >= cur.startup && moveFrame <= cur.CancelEndFrame;
                if (!moveConnected) return false;
                if (moveFrame < cur.startup || moveFrame > cur.CancelEndFrame) return false;
                if (m.isSuper) return cur.superCancel || (cur.specialCancel && cur.kind == MoveKind.Normal);
                if (m.kind == MoveKind.Special) return cur.specialCancel && cur.kind == MoveKind.Normal;
                if (m.kind == MoveKind.Normal) return Array.IndexOf(cur.chainIndices, m.index) >= 0;
                return false;
            }
            if (m.requiresPrevIndex >= 0) return false;
            if (state == FState.Air) return !airAttackUsed;
            return IsActionable;
        }

        bool PressFor(MoveDef m, int f)
        {
            if (m.kind == MoveKind.Throw)
            {
                if ((input.PressedMaskAt(f) & (Btn.LP | Btn.LK)) == Btn.None) return false;
                var held = input.Get(f);
                if (!held.Has(Btn.LP) || !held.Has(Btn.LK)) return false;
                return PressedWithin(Btn.LP, f, 3) && PressedWithin(Btn.LK, f, 3);
            }
            return (input.PressedMaskAt(f) & m.buttons) != Btn.None;
        }

        bool PressedWithin(Btn b, int from, int window)
        {
            for (int g = from; g <= from + window; g++) if (input.PressedAt(g, b)) return true;
            return false;
        }

        //this is for throw techs where you mash throw while getting thrown
        public bool PressedThrowRecently(int window)
        {
            for (int f = 0; f <= window; f++)
            {
                if ((input.PressedMaskAt(f) & (Btn.LP | Btn.LK)) == Btn.None) continue;
                var held = input.Get(f);
                if (held.Has(Btn.LP) && held.Has(Btn.LK)) return true;
            }
            return false;
        }

        static bool DirOk(MoveDef m, int n)
        {
            switch (m.dir)
            {
                case DirReq.Any: return true;
                case DirReq.Stand: return n >= 4;
                case DirReq.Crouch: return n <= 3;
                case DirReq.Forward: return n == 6;
                case DirReq.Back: return n == 4;
                case DirReq.DownForward: return n == 3;
                case DirReq.DownBack: return n == 1;
                case DirReq.Air: return true;
                case DirReq.AirDown: return n <= 3;
            }
            return false;
        }

        public void StartMove(MoveDef m)
        {
            bool fromNeutral = state != FState.Attack;
            state = FState.Attack;
            stateFrame = 0;
            moveIndex = m.index;
            moveFrame = 0;
            hitGroupMask = 0;
            moveConnected = false;
            moveHitConfirmed = false;
            projectileSpawned = false;
            cancelSinceFrame = -1;
            lastStartedMove = m.index;
            meter = Math.Min(MaxMeter, Math.Max(0, meter - m.meterCost + m.meterOnUse));
            if (!airborne) { vx = 0; if (fromNeutral) FaceOpponent(); }
            else airAttackUsed = true;
            match.Emit(SimEventType.AttackStart, index, x, y, m.hit.level, m.index);
            if (m.isSuper && m.superFreeze > 0) match.StartSuperFreeze(index, m.superFreeze);
            AdvanceMove();
        }

        //this fills in the world hurtboxes and returns how many there are
        public int GetHurtboxes(WorldBox[] outBoxes)
        {
            if (state == FState.Knockdown || state == FState.Thrown || state == FState.Throwing) return 0;
            BoxRect b;
            if (airborne || (state == FState.Attack && Move.useAirHurtbox)) b = def.airHurt;
            else if (IsCrouchingPosture) b = def.crouchHurt;
            else b = def.standHurt;
            int n = 0;
            outBoxes[n++] = b.ToWorld(x, y, facing);
            if (state == FState.Attack)
            {
                var m = Move;
                for (int i = 0; i < m.hurtboxes.Count && n < outBoxes.Length; i++)
                    if (m.hurtboxes[i].ActiveOn(moveFrame)) outBoxes[n++] = m.hurtboxes[i].rect.ToWorld(x, y, facing);
            }
            return n;
        }

        public WorldBox GetPushbox()
        {
            int h = IsCrouchingPosture ? 1200 : 1600;
            return new WorldBox(x - def.pushWidth / 2, x + def.pushWidth / 2, y, y + h);
        }

        public void AddMeter(int amount)
        {
            meter = Math.Min(MaxMeter, Math.Max(0, meter + amount));
        }
    }
}

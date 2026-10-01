using System;
using System.Collections.Generic;

namespace FightCore
{
    public enum AIDifficulty { Easy, Normal, Hard, Nightmare }

    //these are the knobs that make each difficulty feel different
    //chances are out of 1000 so the whole thing stays in ints
    public class AIProfile
    {
        public int reaction, block, antiAir, punish, comboSkill, tech, mistake, aggression, reversal, think, jump;

        public static AIProfile For(AIDifficulty d)
        {
            switch (d)
            {
                case AIDifficulty.Easy:
                    return new AIProfile { reaction = 28, block = 250, antiAir = 120, punish = 100, comboSkill = 0, tech = 50, mistake = 150, aggression = 350, reversal = 50, think = 18, jump = 120 };
                case AIDifficulty.Normal:
                    return new AIProfile { reaction = 18, block = 550, antiAir = 400, punish = 400, comboSkill = 1, tech = 300, mistake = 50, aggression = 500, reversal = 200, think = 12, jump = 90 };
                case AIDifficulty.Hard:
                    return new AIProfile { reaction = 12, block = 800, antiAir = 750, punish = 750, comboSkill = 2, tech = 600, mistake = 15, aggression = 600, reversal = 400, think = 8, jump = 70 };
                default:
                    return new AIProfile { reaction = 8, block = 950, antiAir = 950, punish = 950, comboSkill = 3, tech = 850, mistake = 0, aggression = 650, reversal = 550, think = 5, jump = 50 };
            }
        }
    }

    //this is the cpu opponent
    //it only sees the other player through a delay so it has real reaction time and cannot just read inputs
    //it also has to do the actual motions through the input buffer just like a person would
    public class AIBrain
    {
        struct Obs
        {
            public FState state;
            public int x, y, vx, vy;
            public int moveIndex, moveFrame, landRecover;
            public bool airborne;
        }

        readonly int me;
        public readonly AIProfile profile;
        public AIDifficulty Difficulty { get; private set; }
        DeterministicRandom rng;

        readonly Obs[] hist = new Obs[64];
        int histCount;

        readonly Btn[] plan = new Btn[40];
        int planLen, planPos;

        int holdNumpad = 5;
        Btn holdButtons;
        int holdUntil;
        int nextThink;
        int frameCounter;

        int comboRoute = -1;
        int comboStep;
        bool comboIssued;
        bool decidedTech;
        bool decidedWakeup;
        int oppBlockedCount;
        FState lastOppState;

        FighterDef def;
        readonly List<int> antiAirs = new List<int>();
        readonly List<int> reversals = new List<int>();
        readonly List<int> pokes = new List<int>();
        readonly List<int> lows = new List<int>();
        readonly List<int> overheads = new List<int>();
        readonly List<int> projectilesMoves = new List<int>();
        readonly List<int> approaches = new List<int>();
        readonly List<int> airAttacks = new List<int>();
        readonly List<int> supers = new List<int>();
        int throwMove = -1, cmdGrab = -1;

        public AIBrain(int playerIndex, AIDifficulty difficulty, uint seed)
        {
            me = playerIndex;
            Difficulty = difficulty;
            profile = AIProfile.For(difficulty);
            rng = new DeterministicRandom(seed * 2654435761u + (uint)playerIndex + 1);
        }

        void Learn(FighterDef d)
        {
            if (def == d) return;
            def = d;
            antiAirs.Clear(); reversals.Clear(); pokes.Clear(); lows.Clear(); overheads.Clear();
            projectilesMoves.Clear(); approaches.Clear(); airAttacks.Clear(); supers.Clear();
            for (int i = 0; i < d.moves.Count; i++)
            {
                var m = d.moves[i];
                var t = m.aiTags;
                if ((t & AITag.Super) != 0) { supers.Add(i); continue; }
                if ((t & AITag.AntiAir) != 0) antiAirs.Add(i);
                if ((t & AITag.Reversal) != 0) reversals.Add(i);
                if ((t & AITag.Poke) != 0) pokes.Add(i);
                if ((t & AITag.Low) != 0 && m.kind == MoveKind.Normal) lows.Add(i);
                if ((t & AITag.Overhead) != 0) overheads.Add(i);
                if ((t & AITag.Projectile) != 0) projectilesMoves.Add(i);
                if ((t & AITag.Approach) != 0) approaches.Add(i);
                if ((t & AITag.AirAttack) != 0) airAttacks.Add(i);
                if ((t & AITag.Throw) != 0) throwMove = i;
                if ((t & AITag.CommandThrow) != 0) cmdGrab = i;
            }
        }

        public void Reset()
        {
            planLen = planPos = 0;
            histCount = 0;
            comboRoute = -1;
            holdUntil = 0;
            nextThink = 0;
        }

        //this is called once per frame before the sim ticks and it returns what the cpu is pressing
        public FrameInput Decide(MatchSim sim)
        {
            frameCounter++;
            var self = sim.fighters[me];
            var opp = self.opp;
            Learn(self.def);
            Record(opp);

            if (!sim.ControlEnabled)
            {
                planLen = planPos = 0;
                comboRoute = -1;
                return new FrameInput(Btn.None);
            }

            //if we got hit the plan does not matter any more
            if (self.state == FState.HitStun || self.state == FState.AirHitStun || self.state == FState.Thrown || self.state == FState.BlockStun)
            {
                if (self.state != FState.Thrown || planPos >= planLen) { planLen = planPos = 0; }
                comboRoute = -1;
            }

            if (planPos < planLen) return Out(plan[planPos++], self.FacingRight);

            var o = Delayed(profile.reaction);
            bool fr = self.FacingRight;
            int dist = Math.Abs(o.x - self.x);
            TrackOpponent(opp);

            switch (self.state)
            {
                case FState.Thrown:
                    if (!decidedTech)
                    {
                        decidedTech = true;
                        if (self.throwTechable && rng.Chance(profile.tech))
                        {
                            Plan(5, Btn.None);
                            PlanAdd(5, Btn.LP | Btn.LK);
                            PlanAdd(5, Btn.None);
                        }
                    }
                    return Out(Btn.None, fr);

                case FState.BlockStun:
                    decidedTech = false;
                    return Out(BlockDir(o, self), fr);

                case FState.HitStun:
                case FState.AirHitStun:
                    decidedTech = false;
                    decidedWakeup = false;
                    return Out(FrameInput.FromNumpad(4, fr), fr);

                case FState.Knockdown:
                    decidedTech = false;
                    if (!decidedWakeup && self.knockdownLeft <= 4)
                    {
                        decidedWakeup = true;
                        int rev = PickUsable(reversals, self, 99999);
                        if (rev >= 0 && rng.Chance(profile.reversal)) { PlanMove(self.def.moves[rev], self); return NextPlanned(self); }
                    }
                    return Out(FrameInput.FromNumpad(1, fr), fr);

                case FState.Attack:
                    return ComboLogic(sim, self);

                case FState.Air:
                    return AirLogic(self, opp, o);
            }

            decidedWakeup = false;
            if (!self.IsActionable) return Out(FrameInput.FromNumpad(holdNumpad, fr) | holdButtons, fr);

            if (comboRoute >= 0 && TryContinueCombo(self)) return NextPlanned(self);
            comboRoute = -1;

            //these are the reactions the cpu checks first and they are what difficulty mostly changes
            if (o.airborne && o.y > 300 && dist < 2600 && ((o.x - self.x) * o.vx > 0 || dist < 900))
            {
                if (Roll(profile.antiAir, 200))
                {
                    int aa = PickUsable(antiAirs, self, dist + 400);
                    if (aa >= 0 && dist < 1500) { PlanMove(self.def.moves[aa], self); return NextPlanned(self); }
                }
                if (rng.Chance(profile.block)) { Hold(4, 12); return Out(FrameInput.FromNumpad(4, fr), fr); }
            }

            if (OppInRecovery(o, opp) && Roll(profile.punish, 400))
            {
                if (StartPunish(self, dist)) return NextPlanned(self);
            }

            if (o.state == FState.Attack && o.moveIndex >= 0)
            {
                var om = opp.def.moves[o.moveIndex];
                bool threatening = !om.IsThrow && o.moveFrame < om.startup + om.active && dist < om.Reach() + 600;
                if (threatening && rng.Chance(profile.block))
                {
                    int bd = om.hit.guard == GuardType.High ? 4 : 1;
                    Hold(bd, Math.Max(6, om.startup + om.active - o.moveFrame + 6));
                    return Out(FrameInput.FromNumpad(bd, fr), fr);
                }
            }

            int projDist;
            if (IncomingProjectile(sim, self, out projDist)) return ReactToProjectile(sim, self, projDist, dist);

            if (frameCounter < holdUntil) return Out(FrameInput.FromNumpad(holdNumpad, fr) | holdButtons, fr);
            if (frameCounter < nextThink) return Out(FrameInput.FromNumpad(IdleNumpad(self), fr), fr);
            nextThink = frameCounter + profile.think + rng.Range(0, profile.think);
            return Neutral(sim, self, opp, dist);
        }

        //this is the footsies brain and it plays differently for each kind of fighter
        FrameInput Neutral(MatchSim sim, FighterSim self, FighterSim opp, int dist)
        {
            bool fr = self.FacingRight;
            int want;
            switch (self.def.archetype)
            {
                case Archetype.Grappler: want = 900; break;
                case Archetype.Rushdown: want = 1000; break;
                case Archetype.Zoner: want = 4000; break;
                default: want = 2200; break;
            }

            bool oppDown = opp.state == FState.Knockdown;
            int roll = rng.Range(0, 1000);

            //it spends meter on a super sometimes when the other player is close and not blocking
            if (supers.Count > 0 && self.meter >= 200 && profile.comboSkill >= 2 && roll < 40 && dist < 2200 && !oppDown)
            {
                int s = PickUsable(supers, self, dist + 300);
                if (s >= 0) { PlanMove(self.def.moves[s], self); return NextPlanned(self); }
            }

            //zoners throw fireballs from far away and everyone else throws them sometimes at mid range
            if (projectilesMoves.Count > 0 && dist > 2000 && !sim.HasProjectile(me))
            {
                int chance = self.def.archetype == Archetype.Zoner ? 550 : 250;
                if (rng.Chance(chance))
                {
                    int p = PickUsable(projectilesMoves, self, 99999);
                    if (p >= 0) { PlanMove(self.def.moves[p], self); return NextPlanned(self); }
                }
            }

            if (dist > want + 500)
            {
                if (self.def.archetype == Archetype.Zoner) { Hold(1, 10); return Out(FrameInput.FromNumpad(1, fr), fr); }
                if (rng.Chance(profile.jump) && dist < 3200 && !oppDown)
                {
                    Plan(9, Btn.None, self.def.prejump + 2);
                    return NextPlanned(self);
                }
                if (self.def.archetype == Archetype.Rushdown && rng.Chance(400))
                {
                    Plan(6, Btn.None); PlanAdd(5, Btn.None); PlanAdd(6, Btn.None); PlanAdd(5, Btn.None);
                    return NextPlanned(self);
                }
                if (approaches.Count > 0 && rng.Chance(80))
                {
                    int a = PickUsable(approaches, self, dist + 200);
                    if (a >= 0) { PlanMove(self.def.moves[a], self); return NextPlanned(self); }
                }
                Hold(6, 10 + rng.Range(0, 20));
                return Out(FrameInput.FromNumpad(6, fr), fr);
            }

            if (dist < want - 600 && self.def.archetype == Archetype.Zoner)
            {
                if (rng.Chance(250)) { Plan(4, Btn.None); PlanAdd(5, Btn.None); PlanAdd(4, Btn.None); return NextPlanned(self); }
                Hold(4, 15);
                return Out(FrameInput.FromNumpad(4, fr), fr);
            }

            //this is close range where it mixes up between throws lows overheads and pokes
            if (dist < 1000 && !oppDown)
            {
                bool opponentTurtles = oppBlockedCount >= 2;
                int throwChance = opponentTurtles ? 350 : 150;
                if (cmdGrab >= 0 && self.def.archetype == Archetype.Grappler) throwChance += 250;
                if (rng.Chance(throwChance))
                {
                    int t = cmdGrab >= 0 && rng.Chance(600) ? cmdGrab : throwMove;
                    if (t >= 0 && self.Eligible(self.def.moves[t]) && dist <= self.def.moves[t].throwRange + 100)
                    {
                        oppBlockedCount = 0;
                        PlanMove(self.def.moves[t], self);
                        return NextPlanned(self);
                    }
                }
                if (opponentTurtles && overheads.Count > 0 && rng.Chance(300))
                {
                    oppBlockedCount = 0;
                    PlanMove(self.def.moves[overheads[rng.Range(0, overheads.Count)]], self);
                    return NextPlanned(self);
                }
                if (lows.Count > 0 && rng.Chance(300))
                {
                    int l = PickUsable(lows, self, dist + 100);
                    if (l >= 0) { PlanMove(self.def.moves[l], self); return NextPlanned(self); }
                }
            }

            //it pokes when something reaches and otherwise walks around a bit or just waits and blocks
            if (roll < profile.aggression)
            {
                int p = PickUsable(pokes, self, dist + 50);
                if (p >= 0 && !oppDown) { PlanMove(self.def.moves[p], self); return NextPlanned(self); }
                if (dist > 700) { Hold(6, 8 + rng.Range(0, 12)); return Out(FrameInput.FromNumpad(6, fr), fr); }
            }
            if (rng.Chance(profile.jump / 2) && dist < 2600 && dist > 900 && !oppDown)
            {
                Plan(9, Btn.None, self.def.prejump + 2);
                return NextPlanned(self);
            }
            int wait = IdleNumpad(self);
            if (rng.Chance(300)) wait = 4;
            Hold(wait, 6 + rng.Range(0, 14));
            return Out(FrameInput.FromNumpad(wait, fr), fr);
        }

        //zoners hold down back so they always have charge and everyone else crouch blocks while waiting
        int IdleNumpad(FighterSim self)
        {
            if (self.def.archetype == Archetype.Zoner) return 1;
            return rng.Chance(profile.block) ? 1 : 5;
        }

        FrameInput AirLogic(FighterSim self, FighterSim opp, Obs o)
        {
            bool fr = self.FacingRight;
            if (self.airAttackUsed || airAttacks.Count == 0) return Out(Btn.None, fr);
            int dx = Math.Abs(opp.x - self.x);
            //it presses the jump attack on the way down when it is about to reach
            if (self.vy < 60 && dx < 1100 && self.y < 1600 && self.y > 200)
            {
                int best = -1;
                for (int i = 0; i < airAttacks.Count; i++)
                {
                    var m = self.def.moves[airAttacks[i]];
                    if (m.kind == MoveKind.Special && !rng.Chance(400)) continue;
                    if (best < 0 || m.hit.damage > self.def.moves[best].hit.damage) best = airAttacks[i];
                }
                if (best >= 0) { PlanMove(self.def.moves[best], self); return NextPlanned(self); }
            }
            return Out(Btn.None, fr);
        }

        //this is how the cpu does combos where it confirms the hit and then does the next part of the route
        FrameInput ComboLogic(MatchSim sim, FighterSim self)
        {
            bool fr = self.FacingRight;
            if (self.moveConnected && comboRoute < 0 && profile.comboSkill > 0)
            {
                comboRoute = PickRoute(self);
                comboStep = 1;
                comboIssued = false;
            }
            if (comboRoute >= 0)
            {
                var route = self.def.combos[comboRoute].stepIndices;
                //it notices when the next part started and moves along the route
                if (comboStep < route.Length && self.moveIndex == route[comboStep] && comboIssued)
                {
                    comboStep++;
                    comboIssued = false;
                }
                else if (comboIssued && self.moveIndex != route[comboStep - 1] && self.moveIndex != (comboStep < route.Length ? route[comboStep] : -2))
                {
                    comboRoute = -1;
                }

                if (comboRoute >= 0 && comboStep < route.Length && !comboIssued && self.moveConnected)
                {
                    //harder cpus only keep going if the hit actually landed and easier ones just do the whole string
                    if (!self.moveHitConfirmed && profile.comboSkill >= 2 && !self.def.moves[route[comboStep]].spawnsProjectile)
                    {
                        comboRoute = -1;
                    }
                    else
                    {
                        var next = self.def.moves[route[comboStep]];
                        bool isCancel = next.kind != MoveKind.Normal || Array.IndexOf(self.Move.chainIndices, next.index) >= 0;
                        if (isCancel)
                        {
                            comboIssued = true;
                            PlanMove(next, self);
                            return NextPlanned(self);
                        }
                    }
                }
                if (comboRoute >= 0 && comboStep >= route.Length) comboRoute = -1;
            }
            int hold = self.def.archetype == Archetype.Zoner ? 1 : 5;
            return Out(FrameInput.FromNumpad(hold, fr), fr);
        }

        //this is for links where the next hit is a normal you press after the last one ends
        bool TryContinueCombo(FighterSim self)
        {
            var route = self.def.combos[comboRoute].stepIndices;
            if (comboStep >= route.Length) return false;
            var next = self.def.moves[route[comboStep]];
            if (!self.Eligible(next)) return false;
            comboIssued = true;
            PlanMove(next, self);
            return true;
        }

        int PickRoute(FighterSim self)
        {
            var combos = self.def.combos;
            int best = -1, bestScore = -1;
            for (int i = 0; i < combos.Count; i++)
            {
                var r = combos[i];
                if (r.stepIndices.Length < 2 || r.stepIndices[0] != self.moveIndex) continue;
                if (r.meterNeeded > self.meter) continue;
                if (profile.comboSkill == 1 && r.stepIndices.Length > 2) continue;
                if (profile.comboSkill < 3 && r.meterNeeded > 0 && !rng.Chance(300)) continue;
                int score = r.stepIndices.Length * 10 + (r.meterNeeded > 0 ? 25 : 0) + rng.Range(0, 12);
                if (score > bestScore) { bestScore = score; best = i; }
            }
            return best;
        }

        bool OppInRecovery(Obs o, FighterSim opp)
        {
            if (o.state == FState.Land && o.landRecover >= 8) return true;
            if (o.state != FState.Attack || o.moveIndex < 0) return false;
            var m = opp.def.moves[o.moveIndex];
            return o.moveFrame >= m.startup + m.active && m.TotalFrames - o.moveFrame >= 6;
        }

        bool StartPunish(FighterSim self, int dist)
        {
            //it tries to punish with the best combo that reaches and otherwise just the fastest button
            var combos = self.def.combos;
            int best = -1, bestLen = 0;
            for (int i = 0; i < combos.Count; i++)
            {
                var r = combos[i];
                if (r.stepIndices.Length == 0 || r.meterNeeded > self.meter) continue;
                var first = self.def.moves[r.stepIndices[0]];
                if (first.dir == DirReq.Air || !self.Eligible(first)) continue;
                if (first.Reach() + 150 < dist) continue;
                if (r.stepIndices.Length > bestLen) { bestLen = r.stepIndices.Length; best = i; }
            }
            if (best >= 0)
            {
                PlanMove(self.def.moves[combos[best].stepIndices[0]], self);
                comboRoute = profile.comboSkill > 0 ? best : -1;
                comboStep = 1;
                comboIssued = false;
                return true;
            }
            int p = PickUsable(pokes, self, dist + 50);
            if (p < 0) return false;
            PlanMove(self.def.moves[p], self);
            return true;
        }

        bool IncomingProjectile(MatchSim sim, FighterSim self, out int d)
        {
            d = 99999;
            int delayFrames = profile.reaction;
            for (int i = 0; i < sim.projectiles.Length; i++)
            {
                var p = sim.projectiles[i];
                if (!p.active || p.owner == me) continue;
                //it pretends the fireball is where it was a few frames ago so reaction time still matters
                int seenX = p.x - p.dir * p.def.speed * delayFrames;
                int dd = (self.x - seenX) * p.dir;
                if (dd > 0 && dd < d) d = dd;
            }
            return d < 3000;
        }

        FrameInput ReactToProjectile(MatchSim sim, FighterSim self, int projDist, int oppDist)
        {
            bool fr = self.FacingRight;
            if (projDist > 1200 && projDist < 2200 && rng.Chance(profile.jump * 3) && oppDist < 4500)
            {
                Plan(9, Btn.None, self.def.prejump + 2);
                return NextPlanned(self);
            }
            if (!sim.HasProjectile(me) && projectilesMoves.Count > 0 && projDist > 1800 && rng.Chance(500))
            {
                int p = PickUsable(projectilesMoves, self, 99999);
                if (p >= 0) { PlanMove(self.def.moves[p], self); return NextPlanned(self); }
            }
            Hold(1, 10);
            return Out(FrameInput.FromNumpad(1, fr), fr);
        }

        Btn BlockDir(Obs o, FighterSim self)
        {
            int n = self.lastBlockGuard == GuardType.High ? 4 : 1;
            if (o.moveIndex >= 0 && o.state == FState.Attack)
            {
                var om = self.opp.def.moves[o.moveIndex];
                n = om.hit.guard == GuardType.High ? 4 : 1;
            }
            if (o.airborne) n = 4;
            return FrameInput.FromNumpad(n, self.FacingRight);
        }

        //it picks a random move from the list that the fighter can actually do right now and that reaches
        int PickUsable(List<int> list, FighterSim self, int maxDist)
        {
            if (list.Count == 0) return -1;
            int start = rng.Range(0, list.Count);
            for (int k = 0; k < list.Count; k++)
            {
                var m = self.def.moves[list[(start + k) % list.Count]];
                if (!self.Eligible(m)) continue;
                if (!m.spawnsProjectile && m.Reach() + 150 < maxDist - 50 && maxDist < 99999) continue;
                if (!HasCharge(m, self)) continue;
                return m.index;
            }
            return -1;
        }

        static bool HasCharge(MoveDef m, FighterSim self)
        {
            if (m.motion == Motion.ChargeBackForward) return MotionReader.ChargeHeld(self.input, self.FacingRight, true) >= MotionReader.ChargeFrames;
            if (m.motion == Motion.ChargeDownUp) return MotionReader.ChargeHeld(self.input, self.FacingRight, false) >= MotionReader.ChargeFrames;
            return true;
        }

        //this turns a move into the actual frames of stick and buttons the cpu has to press
        void PlanMove(MoveDef m, FighterSim self)
        {
            planLen = planPos = 0;
            Btn b = PickButton(m);
            int dirNum;
            switch (m.dir)
            {
                case DirReq.Crouch: dirNum = 2; break;
                case DirReq.Forward: dirNum = 6; break;
                case DirReq.Back: dirNum = 4; break;
                case DirReq.DownForward: dirNum = 3; break;
                case DirReq.DownBack: dirNum = 1; break;
                case DirReq.AirDown: dirNum = 2; break;
                default: dirNum = 5; break;
            }
            int[] seq = null;
            switch (m.motion)
            {
                case Motion.QCF: seq = new[] { 2, 3, 6 }; break;
                case Motion.QCB: seq = new[] { 2, 1, 4 }; break;
                case Motion.DP: seq = new[] { 6, 2, 3 }; break;
                case Motion.RDP: seq = new[] { 4, 2, 1 }; break;
                case Motion.HCF: seq = new[] { 4, 1, 2, 3, 6 }; break;
                case Motion.HCB: seq = new[] { 6, 3, 2, 1, 4 }; break;
                case Motion.Super236236: seq = new[] { 2, 3, 6, 2, 3, 6 }; break;
                case Motion.Super214214: seq = new[] { 2, 1, 4, 2, 1, 4 }; break;
                case Motion.ChargeBackForward: seq = new[] { 6 }; break;
                case Motion.ChargeDownUp: seq = new[] { 8 }; break;
            }
            //sometimes the cpu messes up an input on purpose so easier ones feel more human
            bool fumble = profile.mistake > 0 && rng.Chance(profile.mistake);
            if (seq == null)
            {
                PlanAdd(dirNum, Btn.None);
                PlanAdd(dirNum, b);
            }
            else
            {
                int skip = fumble && seq.Length > 1 ? rng.Range(0, seq.Length - 1) : -1;
                for (int i = 0; i < seq.Length; i++)
                {
                    if (i == skip) continue;
                    PlanAdd(seq[i], i == seq.Length - 1 ? b : Btn.None);
                }
            }
            PlanAdd(5, Btn.None);
        }

        Btn PickButton(MoveDef m)
        {
            if (m.kind == MoveKind.Throw) return Btn.LP | Btn.LK;
            Btn mask = m.buttons;
            //it picks the strongest button the move allows so it gets the best version
            Btn[] order = { Btn.HP, Btn.HK, Btn.MP, Btn.MK, Btn.LP, Btn.LK };
            if (rng.Chance(300)) order = new[] { Btn.LP, Btn.LK, Btn.MP, Btn.MK, Btn.HP, Btn.HK };
            for (int i = 0; i < order.Length; i++) if ((mask & order[i]) != Btn.None) return order[i];
            return Btn.LP;
        }

        void Plan(int numpad, Btn b, int frames = 1)
        {
            planLen = planPos = 0;
            for (int i = 0; i < frames; i++) PlanAdd(numpad, b);
        }

        //it stores numpad and buttons together and turns them into real directions when they get sent
        void PlanAdd(int numpad, Btn b)
        {
            if (planLen >= plan.Length) return;
            plan[planLen++] = (Btn)((int)b | (numpad << 11));
        }

        FrameInput NextPlanned(FighterSim self)
        {
            if (planPos < planLen) return Out(plan[planPos++], self.FacingRight);
            return Out(Btn.None, self.FacingRight);
        }

        static FrameInput Out(Btn packed, bool fr)
        {
            int n = ((int)packed >> 11) & 15;
            Btn buttons = packed & Btn.Attacks;
            Btn dirs = packed & Btn.Dirs;
            if (n > 0) dirs = FrameInput.FromNumpad(n, fr);
            return FrameInput.Clean(dirs | buttons);
        }

        void Hold(int numpad, int frames)
        {
            holdNumpad = numpad;
            holdButtons = Btn.None;
            holdUntil = frameCounter + frames;
        }

        bool Roll(int perMille, int minFramesSinceLast)
        {
            return rng.Chance(perMille);
        }

        void TrackOpponent(FighterSim opp)
        {
            if (opp.state == FState.BlockStun && lastOppState != FState.BlockStun) oppBlockedCount++;
            if (opp.state == FState.HitStun) oppBlockedCount = 0;
            lastOppState = opp.state;
        }

        void Record(FighterSim opp)
        {
            var o = new Obs
            {
                state = opp.state, x = opp.x, y = opp.y, vx = opp.vx, vy = opp.vy,
                moveIndex = opp.moveIndex, moveFrame = opp.moveFrame, landRecover = opp.landRecover - opp.stateFrame, airborne = opp.airborne
            };
            hist[histCount % hist.Length] = o;
            histCount++;
        }

        Obs Delayed(int frames)
        {
            frames = Math.Min(frames, Math.Min(histCount - 1, hist.Length - 1));
            int i = (histCount - 1 - frames) % hist.Length;
            if (i < 0) i += hist.Length;
            return hist[i];
        }
    }
}

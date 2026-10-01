using System;
using System.Collections.Generic;

namespace FightCore
{
    public enum DummyStance { Stand, Crouch, Jump, CPU, Playback }
    public enum DummyBlock { None, All, AfterFirstHit, Random }

    //this controls the training dummy
    //it can stand crouch jump block play back a recording or wake up with a reversal
    public class DummyController
    {
        public DummyStance stance = DummyStance.Stand;
        public DummyBlock block = DummyBlock.None;
        public bool wakeupReversal;

        public readonly List<ushort> recording = new List<ushort>();
        public bool isRecording;
        int playIndex;
        int blockTimer;
        int reversalFrames;
        DeterministicRandom rng = new DeterministicRandom(1234);
        int randomBlockUntil;
        bool randomBlockOn;

        public const int MaxRecordFrames = 60 * 10;

        public void StartRecording() { recording.Clear(); isRecording = true; }
        public void StopRecording() { isRecording = false; playIndex = 0; }

        //it records inputs relative to facing so playback still works after the sides switch
        public void Record(FrameInput input, bool facingRight)
        {
            if (!isRecording) return;
            if (recording.Count >= MaxRecordFrames) { StopRecording(); return; }
            recording.Add(facingRight ? input.bits : Mirror(input.bits));
        }

        static ushort Mirror(ushort bits)
        {
            var b = (Btn)bits;
            bool l = (b & Btn.Left) != 0, r = (b & Btn.Right) != 0;
            b &= ~(Btn.Left | Btn.Right);
            if (l) b |= Btn.Right;
            if (r) b |= Btn.Left;
            return (ushort)b;
        }

        public FrameInput Decide(MatchSim sim, int me, AIBrain cpu)
        {
            var self = sim.fighters[me];
            var opp = self.opp;
            bool fr = self.FacingRight;
            Btn outB = Btn.None;

            switch (stance)
            {
                case DummyStance.Crouch: outB = FrameInput.FromNumpad(2, fr); break;
                case DummyStance.Jump: outB = FrameInput.FromNumpad(8, fr); break;
                case DummyStance.CPU: return cpu != null ? cpu.Decide(sim) : new FrameInput(Btn.None);
                case DummyStance.Playback:
                    if (recording.Count > 0 && !isRecording)
                    {
                        ushort bits = recording[playIndex % recording.Count];
                        playIndex++;
                        outB = (Btn)(fr ? bits : Mirror(bits));
                    }
                    break;
            }

            //the dummy cheats a little and looks at what is happening right now so blocking is perfect
            bool wantsBlock = false;
            if (self.state == FState.HitStun || self.state == FState.AirHitStun) blockTimer = 40;
            switch (block)
            {
                case DummyBlock.All: wantsBlock = true; break;
                case DummyBlock.AfterFirstHit: wantsBlock = blockTimer > 0; break;
                case DummyBlock.Random:
                    if (sim.frame >= randomBlockUntil) { randomBlockOn = rng.Chance(500); randomBlockUntil = sim.frame + 20; }
                    wantsBlock = randomBlockOn;
                    break;
            }
            if (blockTimer > 0 && !(self.state == FState.HitStun || self.state == FState.AirHitStun)) blockTimer--;

            if (wantsBlock && (sim.IsThreatened(self) || opp.airborne || self.state == FState.BlockStun))
            {
                bool low = true;
                if (opp.state == FState.Attack && opp.Move.hit.guard == GuardType.High) low = false;
                if (opp.airborne) low = false;
                outB = FrameInput.FromNumpad(low ? 1 : 4, fr);
            }

            //this does a dragon punch type move right as the dummy wakes up
            if (wakeupReversal && self.state == FState.Knockdown && self.knockdownLeft <= 4 && reversalFrames == 0)
            {
                for (int i = 0; i < self.def.moves.Count; i++)
                {
                    var m = self.def.moves[i];
                    if ((m.aiTags & AITag.Reversal) == 0 || m.isSuper || m.motion != Motion.DP) continue;
                    reversalFrames = 4;
                    break;
                }
            }
            if (reversalFrames > 0)
            {
                int[] seq = { 5, 6, 2, 3 };
                int step = 4 - reversalFrames;
                reversalFrames--;
                outB = FrameInput.FromNumpad(seq[step], fr) | (step == 3 ? Btn.HP : Btn.None);
            }
            return FrameInput.Clean(outB);
        }
    }

    //this watches the fight and works out frame data like advantage on hit and block
    //it waits until both fighters can act again and then compares who got there first
    public class FrameDataTracker
    {
        public string moveName = "";
        public int startup, active, recovery, damage;
        public int advantage;
        public bool hasAdvantage;
        public bool lastWasBlock;
        public int comboHits, comboDamage;

        int pendingAttacker = -1;
        int attackerFree = -1, defenderFree = -1;

        public void Update(MatchSim sim, int watchPlayer)
        {
            for (int i = 0; i < sim.eventCount; i++)
            {
                var e = sim.events[i];
                if (e.player != watchPlayer) continue;
                if (e.type == SimEventType.AttackStart)
                {
                    var m = sim.fighters[watchPlayer].def.moves[e.value];
                    moveName = m.displayName;
                    startup = m.startup; active = m.active; recovery = m.recovery; damage = m.hit.damage;
                }
                else if (e.type == SimEventType.Hit || e.type == SimEventType.CounterHit || e.type == SimEventType.Block)
                {
                    pendingAttacker = watchPlayer;
                    attackerFree = defenderFree = -1;
                    lastWasBlock = e.type == SimEventType.Block;
                }
            }
            var def = sim.fighters[1 - watchPlayer];
            if (def.comboHits > 0) { comboHits = def.comboHits; comboDamage = def.comboDamage; }

            if (pendingAttacker < 0) return;
            var a = sim.fighters[pendingAttacker];
            var d = a.opp;
            if (attackerFree < 0 && a.IsActionable) attackerFree = sim.frame;
            if (defenderFree < 0 && d.IsActionable) defenderFree = sim.frame;
            if (attackerFree >= 0 && defenderFree >= 0)
            {
                advantage = defenderFree - attackerFree;
                hasAdvantage = true;
                pendingAttacker = -1;
            }
        }
    }

    //this is everything needed to play a match back exactly since the sim is deterministic
    [Serializable]
    public class ReplayData
    {
        public string p1Fighter;
        public string p2Fighter;
        public List<int> p1Inputs = new List<int>();
        public List<int> p2Inputs = new List<int>();
        public uint finalChecksum;

        public void Add(FrameInput a, FrameInput b)
        {
            p1Inputs.Add(a.bits);
            p2Inputs.Add(b.bits);
        }

        public int Length { get { return Math.Min(p1Inputs.Count, p2Inputs.Count); } }
    }
}

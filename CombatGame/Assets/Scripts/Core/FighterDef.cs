using System;
using System.Collections.Generic;

namespace FightCore
{
    //these are the kinds of fighters and the ai plays each kind a bit differently
    public enum Archetype { AllRounder, Grappler, Rushdown, Zoner }

    //this is a combo the ai knows how to do which is just a list of move ids in order
    [Serializable]
    public class ComboRoute
    {
        public string name = "combo";
        public List<string> steps = new List<string>();
        public int meterNeeded;
        [NonSerialized] public int[] stepIndices = new int[0];

        public ComboRoute() { }
        public ComboRoute(string name, int meter, params string[] steps)
        {
            this.name = name; meterNeeded = meter; this.steps = new List<string>(steps);
        }
    }

    //this is one whole character with their stats and moves
    [Serializable]
    public class FighterDef
    {
        public string id = "fighter";
        public string displayName = "Fighter";
        public Archetype archetype = Archetype.AllRounder;
        public int colorRGB = 0xDDDDDD;
        public int accentRGB = 0x333333;

        public int maxHealth = 10000;
        public int walkForward = 45;
        public int walkBack = 35;
        public int dashFrames = 18;
        public int dashSpeed = 90;
        public int backdashFrames = 22;
        public int backdashSpeed = 75;
        public int prejump = 4;
        public int jumpVY = 250;
        public int jumpVX = 55;
        public int gravity = 14;
        public int landFrames = 3;

        public int pushWidth = 520;
        public BoxRect standHurt = new BoxRect(0, 900, 560, 1800);
        public BoxRect crouchHurt = new BoxRect(0, 600, 640, 1200);
        public BoxRect airHurt = new BoxRect(0, 800, 560, 1300);

        public List<MoveDef> moves = new List<MoveDef>();
        public List<ComboRoute> combos = new List<ComboRoute>();

        [NonSerialized] public int[] priorityOrder = new int[0];
        [NonSerialized] bool built;

        //this links all the string ids to indexes and sorts moves so the important ones get checked first
        //it is kind of like the priority rules so a dragon punch motion wins over a fireball motion
        public void Build()
        {
            if (built) return;
            var lookup = new Dictionary<string, int>();
            for (int i = 0; i < moves.Count; i++)
            {
                moves[i].index = i;
                lookup[moves[i].id] = i;
            }
            for (int i = 0; i < moves.Count; i++)
            {
                var m = moves[i];
                int r;
                m.requiresPrevIndex = !string.IsNullOrEmpty(m.requiresPrev) && lookup.TryGetValue(m.requiresPrev, out r) ? r : -1;
                var ch = new List<int>();
                foreach (var c in m.chains) if (lookup.TryGetValue(c, out r)) ch.Add(r);
                m.chainIndices = ch.ToArray();
            }
            foreach (var combo in combos)
            {
                var st = new List<int>();
                foreach (var s in combo.steps) { int r; if (lookup.TryGetValue(s, out r)) st.Add(r); }
                combo.stepIndices = st.ToArray();
            }
            var order = new List<int>();
            for (int i = 0; i < moves.Count; i++) order.Add(i);
            //it uses a stable sort so moves with the same priority stay in the order you made them
            order.Sort((a, b) =>
            {
                int pa = EffectivePriority(moves[a]), pb = EffectivePriority(moves[b]);
                if (pa != pb) return pb.CompareTo(pa);
                return a.CompareTo(b);
            });
            priorityOrder = order.ToArray();
            built = true;
        }

        public void Rebuild() { built = false; Build(); }

        //this gives supers and longer motions a higher priority automatically so you do not have to think about it
        static int EffectivePriority(MoveDef m)
        {
            int p = m.priority * 100;
            if (m.isSuper) p += 90;
            else if (m.requiresPrevIndex >= 0 || !string.IsNullOrEmpty(m.requiresPrev)) p += 80;
            switch (m.motion)
            {
                case Motion.HCF: case Motion.HCB: p += 60; break;
                case Motion.DP: case Motion.RDP: p += 50; break;
                case Motion.QCF: case Motion.QCB: p += 40; break;
                case Motion.ChargeBackForward: case Motion.ChargeDownUp: p += 45; break;
            }
            if (m.IsThrow) p += 30;
            if (m.kind == MoveKind.Normal && m.dir != DirReq.Stand && m.dir != DirReq.Crouch && m.dir != DirReq.Air) p += 20;
            return p;
        }

        public int FindMove(string moveId)
        {
            for (int i = 0; i < moves.Count; i++) if (moves[i].id == moveId) return i;
            return -1;
        }
    }
}

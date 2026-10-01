namespace FightCore
{
    //these are the special move motions the game knows about
    //they are all in numpad notation so 236 is down then down forward then forward
    public enum Motion
    {
        None,
        QCF,          //236
        QCB,          //214
        DP,           //623
        RDP,          //421
        HCF,          //41236
        HCB,          //63214
        Super236236,
        Super214214,
        ChargeBackForward, //hold 4 then 6
        ChargeDownUp,      //hold 2 then 8
        DashForward,       //66
        DashBack           //44
    }

    //this is what reads motions out of the input buffer
    //it is where the leniency lives so the game forgives kind of sloppy inputs like street fighter does
    public static class MotionReader
    {
        //this is how many frames you get between each direction of a motion
        public static int StepWindow = 10;
        //this is how early the last direction can be before the button press
        public static int FinalWindow = 5;
        //this is how long you have to hold a charge
        public static int ChargeFrames = 40;
        //this is how long you have between taps for a dash
        public static int DashWindow = 12;

        static readonly int[] SeqQCF = { 2, 3, 6 };
        static readonly int[] SeqQCB = { 2, 1, 4 };
        static readonly int[] SeqDP = { 6, 2, 3 };
        static readonly int[] SeqRDP = { 4, 2, 1 };
        static readonly int[] SeqHCF = { 4, 1, 2, 3, 6 };
        static readonly int[] SeqHCB = { 6, 3, 2, 1, 4 };
        static readonly int[] SeqS236 = { 2, 3, 6, 2, 3, 6 };
        static readonly int[] SeqS214 = { 2, 1, 4, 2, 1, 4 };

        //anchor is how many frames ago the button got pressed
        public static bool Check(Motion m, InputBuffer buf, int anchor, bool facingRight)
        {
            switch (m)
            {
                case Motion.None: return true;
                case Motion.QCF: return Sequence(SeqQCF, buf, anchor, facingRight);
                case Motion.QCB: return Sequence(SeqQCB, buf, anchor, facingRight);
                case Motion.DP: return Sequence(SeqDP, buf, anchor, facingRight);
                case Motion.RDP: return Sequence(SeqRDP, buf, anchor, facingRight);
                case Motion.HCF: return Sequence(SeqHCF, buf, anchor, facingRight);
                case Motion.HCB: return Sequence(SeqHCB, buf, anchor, facingRight);
                case Motion.Super236236: return Sequence(SeqS236, buf, anchor, facingRight);
                case Motion.Super214214: return Sequence(SeqS214, buf, anchor, facingRight);
                case Motion.ChargeBackForward: return Charge(buf, anchor, facingRight, true);
                case Motion.ChargeDownUp: return Charge(buf, anchor, facingRight, false);
                case Motion.DashForward: return Dash(buf, anchor, facingRight, 6);
                case Motion.DashBack: return Dash(buf, anchor, facingRight, 4);
            }
            return false;
        }

        //this lets a straight direction also count the diagonals next to it
        //diagonals still have to be exact because that is what keeps motions from getting mixed up
        static bool Matches(int required, int raw)
        {
            if (required == raw) return true;
            switch (required)
            {
                case 2: return raw == 1 || raw == 3;
                case 8: return raw == 7 || raw == 9;
                case 6: return raw == 3 || raw == 9;
                case 4: return raw == 1 || raw == 7;
            }
            return false;
        }

        //it walks backwards through time looking for each direction of the motion in reverse order
        //each matched direction has to be different from the one after it so just holding down forward never counts as a whole motion
        static bool Sequence(int[] seq, InputBuffer buf, int anchor, bool facingRight)
        {
            int cursor = anchor;
            int limit = anchor + FinalWindow;
            int nextRaw = 0;
            for (int i = seq.Length - 1; i >= 0; i--)
            {
                bool found = false;
                for (int f = cursor; f <= limit && f < InputBuffer.Size - 2; f++)
                {
                    int raw = buf.NumpadAt(f, facingRight);
                    if (raw != nextRaw && Matches(seq[i], raw))
                    {
                        found = true;
                        nextRaw = raw;
                        cursor = f + 1;
                        limit = f + StepWindow;
                        break;
                    }
                }
                if (!found) return false;
            }
            return true;
        }

        static bool IsBackish(int n) { return n == 1 || n == 4 || n == 7; }
        static bool IsFwdish(int n) { return n == 3 || n == 6 || n == 9; }
        static bool IsDownish(int n) { return n == 1 || n == 2 || n == 3; }
        static bool IsUpish(int n) { return n == 7 || n == 8 || n == 9; }

        //this checks for a charge where you hold one way for a while and then press the other way
        //it lets you have a couple frames of gap so letting go for a tiny bit does not ruin it
        static bool Charge(InputBuffer buf, int anchor, bool facingRight, bool horizontal)
        {
            for (int f = anchor; f <= anchor + FinalWindow; f++)
            {
                int n = buf.NumpadAt(f, facingRight);
                bool release = horizontal ? IsFwdish(n) : IsUpish(n);
                if (!release) continue;
                int count = 0, gap = 0;
                for (int g = f + 1; g < InputBuffer.Size - 1; g++)
                {
                    int c = buf.NumpadAt(g, facingRight);
                    bool held = horizontal ? IsBackish(c) : IsDownish(c);
                    if (held) { count++; gap = 0; }
                    else if (count == 0 && g - f <= 6) continue;
                    else { gap++; if (gap > 2) break; }
                    if (count >= ChargeFrames) return true;
                }
            }
            return false;
        }

        //this is how many frames a charge has been held right now which the ui and ai like to know
        public static int ChargeHeld(InputBuffer buf, bool facingRight, bool horizontal)
        {
            int count = 0;
            for (int g = 0; g < InputBuffer.Size - 1; g++)
            {
                int c = buf.NumpadAt(g, facingRight);
                if (horizontal ? IsBackish(c) : IsDownish(c)) count++; else break;
            }
            return count;
        }

        //this checks for a double tap like 66 with neutral in between
        static bool Dash(InputBuffer buf, int anchor, bool facingRight, int dir)
        {
            if (buf.NumpadAt(anchor, facingRight) != dir) return false;
            if (buf.NumpadAt(anchor + 1, facingRight) != 5) return false;
            for (int f = anchor + 2; f <= anchor + DashWindow; f++)
            {
                int n = buf.NumpadAt(f, facingRight);
                if (n == dir) return true;
                if (n != 5) return false;
            }
            return false;
        }
    }
}

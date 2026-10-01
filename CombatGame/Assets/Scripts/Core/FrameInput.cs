using System;

namespace FightCore
{
    //these are all the buttons and directions the game cares about
    //it uses bit flags so one whole frame of input fits in one small number
    [Flags]
    public enum Btn : ushort
    {
        None = 0,
        Up = 1 << 0,
        Down = 1 << 1,
        Left = 1 << 2,
        Right = 1 << 3,
        LP = 1 << 4,
        MP = 1 << 5,
        HP = 1 << 6,
        LK = 1 << 7,
        MK = 1 << 8,
        HK = 1 << 9,
        Start = 1 << 10,

        Punches = LP | MP | HP,
        Kicks = LK | MK | HK,
        Attacks = Punches | Kicks,
        Dirs = Up | Down | Left | Right
    }

    //this is one frame of input for one player
    //it is super small on purpose so replays and online stuff stay cheap
    [Serializable]
    public struct FrameInput : IEquatable<FrameInput>
    {
        public ushort bits;

        public FrameInput(Btn b) { bits = (ushort)b; }

        public bool Has(Btn b) { return (bits & (ushort)b) != 0; }
        public Btn Buttons { get { return (Btn)bits & Btn.Attacks; } }

        //it cleans up opposite directions so left plus right is neutral and up plus down is up
        //and I feel that this is the fairest way to do it and it is what a lot of fighting games do
        public static FrameInput Clean(Btn raw)
        {
            if ((raw & Btn.Left) != 0 && (raw & Btn.Right) != 0) raw &= ~(Btn.Left | Btn.Right);
            if ((raw & Btn.Up) != 0 && (raw & Btn.Down) != 0) raw &= ~Btn.Down;
            return new FrameInput(raw);
        }

        //this turns the input into numpad notation where 5 is neutral and 6 is forward
        //it needs to know which way the fighter faces so forward and back make sense
        public int Numpad(bool facingRight)
        {
            bool up = Has(Btn.Up), down = Has(Btn.Down);
            bool fwd = facingRight ? Has(Btn.Right) : Has(Btn.Left);
            bool back = facingRight ? Has(Btn.Left) : Has(Btn.Right);
            int x = fwd ? 1 : (back ? -1 : 0);
            int y = up ? 1 : (down ? -1 : 0);
            return 5 + x + y * 3;
        }

        //this builds input back from numpad so the ai can think in numpad too
        public static Btn FromNumpad(int numpad, bool facingRight)
        {
            Btn b = Btn.None;
            int x = (numpad - 1) % 3 - 1;
            int y = (numpad - 1) / 3 - 1;
            if (y > 0) b |= Btn.Up;
            if (y < 0) b |= Btn.Down;
            if (x > 0) b |= facingRight ? Btn.Right : Btn.Left;
            if (x < 0) b |= facingRight ? Btn.Left : Btn.Right;
            return b;
        }

        public bool Equals(FrameInput o) { return bits == o.bits; }
        public override int GetHashCode() { return bits; }
    }
}

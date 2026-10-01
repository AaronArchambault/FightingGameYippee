namespace FightCore
{
    //this remembers the last bunch of frames of input for one player
    //it is what lets you do motions like quarter circle forward and it is where a lot of the feel comes from
    public class InputBuffer
    {
        public const int Size = 128;
        readonly FrameInput[] ring = new FrameInput[Size];
        int head = -1;

        //this is the frame number of the newest input
        public int NewestFrame { get; private set; } = -1;

        public void Clear()
        {
            for (int i = 0; i < Size; i++) ring[i] = default(FrameInput);
            head = -1;
            NewestFrame = -1;
        }

        public void Push(FrameInput input)
        {
            head = (head + 1) % Size;
            ring[head] = input;
            NewestFrame++;
        }

        //it gets the input from some frames ago where 0 is right now
        public FrameInput Get(int framesAgo)
        {
            if (framesAgo < 0 || framesAgo >= Size || framesAgo > NewestFrame) return default(FrameInput);
            int i = head - framesAgo;
            if (i < 0) i += Size;
            return ring[i];
        }

        public FrameInput GetAbs(int absFrame) { return Get(NewestFrame - absFrame); }

        //this checks if a button went from not held to held on that frame
        public bool PressedAt(int framesAgo, Btn mask)
        {
            ushort now = (ushort)(Get(framesAgo).bits & (ushort)mask);
            ushort prev = (ushort)(Get(framesAgo + 1).bits & (ushort)mask);
            return (now & ~prev) != 0;
        }

        public Btn PressedMaskAt(int framesAgo)
        {
            ushort now = Get(framesAgo).bits;
            ushort prev = Get(framesAgo + 1).bits;
            return (Btn)(now & ~prev) & Btn.Attacks;
        }

        public int NumpadAt(int framesAgo, bool facingRight) { return Get(framesAgo).Numpad(facingRight); }
    }
}

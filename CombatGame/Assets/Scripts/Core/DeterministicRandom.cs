namespace FightCore
{
    //this is a tiny random number maker that always gives the same numbers from the same seed
    //it is needed because unity random is not something you can trust to be the same every time
    public struct DeterministicRandom
    {
        public uint state;

        public DeterministicRandom(uint seed) { state = seed == 0 ? 0x9E3779B9u : seed; }

        //it uses xorshift which is fast and pretty simple
        public uint Next()
        {
            uint x = state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            state = x;
            return x;
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + (int)(Next() % (uint)(maxExclusive - minInclusive));
        }

        //it rolls a chance out of 1000 so I never need floats
        public bool Chance(int perMille) { return Range(0, 1000) < perMille; }
    }
}

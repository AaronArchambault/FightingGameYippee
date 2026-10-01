namespace FightCore
{
    //these are things that happened during a frame so the unity side can play sounds and sparks
    //the sim never plays anything itself it just writes these down
    public enum SimEventType : byte
    {
        AttackStart,
        Hit,
        CounterHit,
        Block,
        ProjectileSpawn,
        ProjectileClash,
        ProjectileEnd,
        ThrowStart,
        ThrowTech,
        ThrowLand,
        Jump,
        Land,
        Dash,
        Knockdown,
        KO,
        DoubleKO,
        RoundAnnounce,
        Fight,
        TimeUp,
        RoundWin,
        Draw,
        MatchWin,
        SuperFlash
    }

    public struct SimEvent
    {
        public SimEventType type;
        public int player;
        public int x, y;
        public HitLevel level;
        public int value;
        public int value2;
    }
}

using System;
using System.Collections.Generic;

namespace FightCore
{
    public enum MoveKind { Normal, Special, Throw, CommandThrow }

    //this is what the stick has to be doing when you press the button
    public enum DirReq { Any, Stand, Crouch, Forward, Back, DownForward, DownBack, Air, AirDown }

    //this is how the attack has to be blocked
    //high means overhead so you have to block standing and low means you have to block crouching
    public enum GuardType { Mid, High, Low, Unblockable }

    public enum HitEffect { Normal, Knockdown, HardKnockdown, Launch }

    //this is how big the hit feels which picks the sound and spark size
    public enum HitLevel { Light, Medium, Heavy, Special, Super }

    //these tags are so the ai knows what a move is good for
    [Flags]
    public enum AITag
    {
        None = 0,
        Poke = 1,
        AntiAir = 2,
        Reversal = 4,
        Projectile = 8,
        Overhead = 16,
        Low = 32,
        Throw = 64,
        CommandThrow = 128,
        Super = 256,
        Approach = 512,
        Fast = 1024,
        AirAttack = 2048
    }

    //this is everything about what happens when something connects
    [Serializable]
    public class HitData
    {
        public int damage = 500;
        public int chip = 0;
        public int hitstun = 15;
        public int blockstun = 12;
        public int hitstop = 9;
        public GuardType guard = GuardType.Mid;
        public HitEffect effect = HitEffect.Normal;
        public HitLevel level = HitLevel.Medium;
        public int pushbackHit = 500;
        public int pushbackBlock = 700;
        public int launchVX = 40;
        public int launchVY = 170;
        public int meterGain = 10;
        public bool chipCanKO = false;

        public HitData Clone() { return (HitData)MemberwiseClone(); }
    }

    //this sets the velocity of the fighter on a certain frame of a move
    //airborne makes the fighter leave the ground which is how uppercuts and flying kicks work
    [Serializable]
    public class VelocityKey
    {
        public int frame;
        public int vx;
        public int vy;
        public bool airborne;

        public VelocityKey() { }
        public VelocityKey(int frame, int vx, int vy = 0, bool airborne = false)
        {
            this.frame = frame; this.vx = vx; this.vy = vy; this.airborne = airborne;
        }
    }

    [Serializable]
    public class ProjectileDef
    {
        public int spawnX = 700;
        public int spawnY = 0;
        public int speed = 80;
        public int lifetime = 120;
        public BoxRect box = new BoxRect(0, 1150, 500, 400);
        public HitData hit = new HitData();
        //this is how many hits it can do and also how strong it is when two of them clash
        public int durability = 1;
        public int hitInterval = 4;
        public int visualSize = 1;
    }

    //this is one move with all of its frame data
    //startup is the frame the first active frame lands on so a 4 frame jab hits on frame 4
    [Serializable]
    public class MoveDef
    {
        public string id = "move";
        public string displayName = "Move";
        public MoveKind kind = MoveKind.Normal;
        public bool isSuper;
        public Motion motion = Motion.None;
        public DirReq dir = DirReq.Stand;
        public Btn buttons = Btn.LP;
        public int priority = 0;
        public string requiresPrev = "";

        public int startup = 5;
        public int active = 3;
        public int recovery = 10;
        public int landingRecovery = 0;

        public HitData hit = new HitData();
        public List<FrameBox> hitboxes = new List<FrameBox>();
        public List<FrameBox> hurtboxes = new List<FrameBox>();
        public bool crouching;
        public bool useAirHurtbox;

        //these are the frames where the move cannot be hit by strikes which is what makes reversals work
        public int invulnStart;
        public int invulnEnd;
        public bool throwInvuln;

        public bool specialCancel;
        public bool superCancel;
        public int cancelEnd;
        public List<string> chains = new List<string>();

        public int meterCost;
        public int meterOnUse;
        public int superFreeze;

        public List<VelocityKey> velocity = new List<VelocityKey>();
        public int gravity;

        public bool spawnsProjectile;
        public int projectileFrame;
        public ProjectileDef projectile = new ProjectileDef();

        public int throwRange;
        public bool throwSwapSides;

        public AITag aiTags;

        //this lets a multi hit move end with a knockdown on its last hit group
        public int finisherGroup = -1;
        public HitEffect finisherEffect = HitEffect.Knockdown;
        public int finisherLaunchVY = 220;
        [NonSerialized] HitData finisherHit;

        public HitData HitFor(int group)
        {
            if (group != finisherGroup || finisherGroup < 0) return hit;
            if (finisherHit == null)
            {
                finisherHit = hit.Clone();
                finisherHit.effect = finisherEffect;
                finisherHit.launchVY = finisherLaunchVY;
                finisherHit.launchVX = Math.Max(hit.launchVX, 50);
            }
            return finisherHit;
        }

        //these get filled in when the fighter is built so the sim never has to look up strings
        [NonSerialized] public int index;
        [NonSerialized] public int requiresPrevIndex = -1;
        [NonSerialized] public int[] chainIndices = new int[0];

        public int TotalFrames { get { return startup + active - 1 + recovery; } }
        public bool IsThrow { get { return kind == MoveKind.Throw || kind == MoveKind.CommandThrow; } }
        public bool IsActiveFrame(int f) { return f >= startup && f < startup + active; }
        public int CancelEndFrame { get { return cancelEnd > 0 ? cancelEnd : startup + active + 3; } }
        public bool InvulnOn(int f) { return invulnEnd > 0 && f >= invulnStart && f <= invulnEnd; }

        //this is roughly how far forward the move reaches which the ai uses for spacing
        public int Reach()
        {
            if (spawnsProjectile) return 99999;
            if (IsThrow) return throwRange;
            int best = 0;
            for (int i = 0; i < hitboxes.Count; i++)
            {
                int r = hitboxes[i].rect.cx + hitboxes[i].rect.w / 2;
                if (r > best) best = r;
            }
            int travel = 0;
            for (int i = 0; i < velocity.Count; i++) travel = Math.Max(travel, velocity[i].vx * Math.Max(1, startup - velocity[i].frame));
            return best + Math.Max(0, travel);
        }

        public MoveDef Clone()
        {
            var m = (MoveDef)MemberwiseClone();
            m.hit = hit.Clone();
            m.finisherHit = null;
            m.hitboxes = new List<FrameBox>();
            foreach (var b in hitboxes) m.hitboxes.Add(new FrameBox(b.start, b.end, b.rect, b.hitGroup));
            m.hurtboxes = new List<FrameBox>();
            foreach (var b in hurtboxes) m.hurtboxes.Add(new FrameBox(b.start, b.end, b.rect, b.hitGroup));
            m.chains = new List<string>(chains);
            m.velocity = new List<VelocityKey>();
            foreach (var v in velocity) m.velocity.Add(new VelocityKey(v.frame, v.vx, v.vy, v.airborne));
            m.projectile = new ProjectileDef
            {
                spawnX = projectile.spawnX, spawnY = projectile.spawnY, speed = projectile.speed, lifetime = projectile.lifetime,
                box = projectile.box, hit = projectile.hit.Clone(), durability = projectile.durability,
                hitInterval = projectile.hitInterval, visualSize = projectile.visualSize
            };
            return m;
        }
    }
}

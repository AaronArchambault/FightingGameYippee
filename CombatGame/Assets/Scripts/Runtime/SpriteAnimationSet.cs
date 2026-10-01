using System;
using System.Collections.Generic;
using UnityEngine;
using FightCore;

namespace FightGame
{
    //this is how a clip lines its sprites up with the frame data
    public enum ClipTiming
    {
        //it splits the sprites into startup active and recovery so the hit sprite always shows on the real active frames
        MatchFrameData,
        //it stretches the whole clip evenly over the move no matter how long the move is
        FitToMove,
        //it uses the durations exactly as you typed them
        ExactDurations
    }

    //this is one sprite in a clip and how many game frames it stays on screen
    [Serializable]
    public class SpriteFrame
    {
        public Sprite sprite;
        [Tooltip("how many game frames this sprite shows for and the game runs at 60 frames a second")]
        [Min(1)] public int duration = 3;
        [Tooltip("nudges this one sprite if it does not line up with the others")]
        public Vector2 offset;
    }

    //this is one animation like idle or a fireball
    [Serializable]
    public class SpriteClip
    {
        [Tooltip("a move id like st_lp or fb_h or a state name like idle walkf crouch jump hitstun block knockdown win")]
        public string key = "idle";
        public List<SpriteFrame> frames = new List<SpriteFrame>();
        [Tooltip("loops for states like idle and walk and holds the last sprite when it is off")]
        public bool loop = true;
        [Tooltip("only matters for attacks")]
        public ClipTiming timing = ClipTiming.MatchFrameData;
        [Tooltip("for Match Frame Data this is how many sprites are the wind up before the hit")]
        [Min(0)] public int startupSprites = 1;
        [Tooltip("for Match Frame Data this is how many sprites show while the hitbox is out")]
        [Min(1)] public int activeSprites = 1;

        public int TotalDuration
        {
            get
            {
                int t = 0;
                for (int i = 0; i < frames.Count; i++) t += Mathf.Max(1, frames[i].duration);
                return t;
            }
        }
    }

    //this is a whole set of sprite animations for one fighter
    //you fill it from your own sprite sheets and the game picks the right sprite every frame straight from the sim
    //it never uses unity time so the animation can not drift away from the hitboxes
    [CreateAssetMenu(menuName = "Fighting Game/Sprite Animation Set", fileName = "NewSpriteAnimations")]
    public class SpriteAnimationSet : ScriptableObject
    {
        [Tooltip("scales the sprites if your art is bigger or smaller than the fighter hurtboxes")]
        public float scale = 1f;
        [Tooltip("turn this on if your sprites are drawn facing left")]
        public bool artFacesLeft;
        public List<SpriteClip> clips = new List<SpriteClip>();

        [NonSerialized] Dictionary<string, SpriteClip> lookup;

        void OnValidate() { lookup = null; }

        //call this after changing clips from code so the lookup gets rebuilt
        public void Invalidate() { lookup = null; }

        public SpriteClip Find(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (lookup == null)
            {
                lookup = new Dictionary<string, SpriteClip>();
                foreach (var c in clips)
                    if (c != null && !string.IsNullOrEmpty(c.key) && c.frames.Count > 0 && !lookup.ContainsKey(c.key)) lookup[c.key] = c;
            }
            SpriteClip clip;
            return lookup.TryGetValue(key, out clip) ? clip : null;
        }

        //these are the names the game looks for in order so you do not need every single animation
        //like if you have no walkb clip it uses walkf and if there is no walkf it uses idle
        static readonly string[][] stateKeys = BuildStateKeys();

        static string[][] BuildStateKeys()
        {
            var vals = (FState[])Enum.GetValues(typeof(FState));
            var keys = new string[vals.Length][];
            foreach (var v in vals)
            {
                string[] k;
                switch (v)
                {
                    case FState.Intro: k = new[] { "intro", "idle" }; break;
                    case FState.Idle: k = new[] { "idle" }; break;
                    case FState.WalkF: k = new[] { "walkf", "idle" }; break;
                    case FState.WalkB: k = new[] { "walkb", "walkf", "idle" }; break;
                    case FState.Crouch: k = new[] { "crouch", "idle" }; break;
                    case FState.PreJump: k = new[] { "prejump", "crouch", "idle" }; break;
                    case FState.Air: k = new[] { "jump", "idle" }; break;
                    case FState.Land: k = new[] { "land", "crouch", "idle" }; break;
                    case FState.DashF: k = new[] { "dashf", "walkf", "idle" }; break;
                    case FState.DashB: k = new[] { "dashb", "walkb", "walkf", "idle" }; break;
                    case FState.HitStun: k = new[] { "hitstun", "idle" }; break;
                    case FState.AirHitStun: k = new[] { "airhit", "hitstun", "idle" }; break;
                    case FState.BlockStun: k = new[] { "block", "idle" }; break;
                    case FState.Knockdown: k = new[] { "knockdown", "hitstun", "idle" }; break;
                    case FState.Thrown: k = new[] { "thrown", "hitstun", "idle" }; break;
                    case FState.Throwing: k = new[] { "throwing", "attack", "idle" }; break;
                    case FState.TechRecover: k = new[] { "tech", "block", "idle" }; break;
                    case FState.KO: k = new[] { "ko", "knockdown", "hitstun", "idle" }; break;
                    case FState.Win: k = new[] { "win", "idle" }; break;
                    default: k = new[] { "attack", "idle" }; break;
                }
                keys[(int)v] = k;
            }
            return keys;
        }

        //this is every state name the game looks for and the editor uses it to make empty clips for you
        public static readonly string[] AllStateKeys =
        {
            "idle", "walkf", "walkb", "crouch", "prejump", "jump", "land", "dashf", "dashb",
            "hitstun", "crouchhit", "airhit", "block", "crouchblock", "knockdown", "thrown", "throwing",
            "tech", "ko", "win", "intro", "attack"
        };

        //this picks the sprite to show for a fighter right now
        public SpriteFrame Resolve(FighterSim f)
        {
            SpriteClip clip;
            var m = f.Move;
            if (f.state == FState.Attack && m != null)
            {
                clip = Find(m.id);
                if (clip != null) return SampleMove(clip, m, f.moveFrame);
                clip = Find(f.airborne ? "jump" : "attack") ?? Find("idle");
                return clip != null ? SampleLoop(clip, f.moveFrame) : null;
            }

            //crouching versions are checked first when they make sense
            if (f.state == FState.HitStun && f.crouchHit) { clip = Find("crouchhit"); if (clip != null) return SampleLoop(clip, f.stateFrame); }
            if (f.state == FState.BlockStun && f.crouchBlocking) { clip = Find("crouchblock"); if (clip != null) return SampleLoop(clip, f.stateFrame); }
            if (f.state == FState.Throwing && m != null) { clip = Find("throwing") ?? Find(m.id); if (clip != null) return SampleLoop(clip, f.stateFrame); }

            var keys = stateKeys[(int)f.state];
            for (int i = 0; i < keys.Length; i++)
            {
                clip = Find(keys[i]);
                if (clip != null) return SampleLoop(clip, f.stateFrame);
            }
            return null;
        }

        //this plays a clip by counting frames and it loops or holds the last sprite
        public static SpriteFrame SampleLoop(SpriteClip clip, int frame)
        {
            int total = clip.TotalDuration;
            if (total <= 0) return null;
            int t = clip.loop ? ((frame % total) + total) % total : Mathf.Clamp(frame, 0, total - 1);
            return Walk(clip, 0, clip.frames.Count, t, total, total);
        }

        //this plays an attack clip lined up with the move frame data
        //moveFrame starts at 1 on the first frame of the move
        public static SpriteFrame SampleMove(SpriteClip clip, MoveDef m, int moveFrame)
        {
            int count = clip.frames.Count;
            if (count == 0) return null;
            int total = Mathf.Max(1, m.TotalFrames);
            int f0 = Mathf.Clamp(moveFrame - 1, 0, total - 1);

            if (clip.timing == ClipTiming.ExactDurations)
            {
                int dur = clip.TotalDuration;
                int t = clip.loop ? f0 % dur : Mathf.Min(f0, dur - 1);
                return Walk(clip, 0, count, t, dur, dur);
            }
            if (clip.timing == ClipTiming.FitToMove)
                return Walk(clip, 0, count, f0, total, clip.TotalDuration);

            //this is the match frame data mode
            //it works out where startup and active and recovery begin then plays each group of sprites across its own part
            int sCount = Mathf.Clamp(clip.startupSprites, 0, count);
            int aCount = Mathf.Clamp(clip.activeSprites, 0, count - sCount);
            int rCount = count - sCount - aCount;
            int startupLen = Mathf.Max(0, m.startup - 1);
            int activeLen = Mathf.Max(1, m.active);
            int recoveryLen = Mathf.Max(0, total - startupLen - activeLen);

            if (f0 < startupLen && sCount > 0) return Segment(clip, 0, sCount, f0, startupLen);
            if (f0 < startupLen + activeLen && aCount > 0) return Segment(clip, sCount, aCount, f0 - startupLen, activeLen);
            if (rCount > 0 && f0 >= startupLen + activeLen) return Segment(clip, sCount + aCount, rCount, f0 - startupLen - activeLen, recoveryLen);
            //if a part has no sprites it just shows the closest sprite it does have
            if (f0 < startupLen) return clip.frames[Mathf.Min(sCount, count - 1)];
            if (f0 < startupLen + activeLen) return clip.frames[Mathf.Max(0, sCount - 1)];
            return clip.frames[Mathf.Max(0, sCount + aCount - 1)];
        }

        static SpriteFrame Segment(SpriteClip clip, int start, int len, int local, int segFrames)
        {
            int weight = 0;
            for (int i = start; i < start + len; i++) weight += Mathf.Max(1, clip.frames[i].duration);
            return Walk(clip, start, len, local, Mathf.Max(1, segFrames), weight);
        }

        //this walks through the sprites using their durations as weights
        static SpriteFrame Walk(SpriteClip clip, int start, int len, int t, int span, int weight)
        {
            if (len <= 0) return null;
            float pos = span <= 0 ? 0f : (t + 0.5f) / span * weight;
            float acc = 0f;
            for (int i = start; i < start + len; i++)
            {
                acc += Mathf.Max(1, clip.frames[i].duration);
                if (pos < acc) return clip.frames[i];
            }
            return clip.frames[start + len - 1];
        }
    }
}

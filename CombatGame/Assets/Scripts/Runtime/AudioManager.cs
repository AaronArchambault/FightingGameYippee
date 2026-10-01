using System;
using System.Collections.Generic;
using UnityEngine;

namespace FightGame
{
    //these are all the sounds the game knows how to ask for
    public enum SfxId
    {
        HitLight, HitMedium, HitHeavy, HitSpecial, CounterHit, Block,
        WhooshLight, WhooshHeavy, Fireball, ProjectileClash,
        Jump, Land, Dash, Throw, ThrowTech, Knockdown,
        SuperFlash, KO, RoundAnnounce, Fight, RoundWin, UIMove, UIConfirm, UIBack
    }

    //this plays every sound using a pool of audio sources
    //it has a limit on voices so a big combo cannot make a wall of noise
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Range(0f, 1f)] public float sfxVolume = 0.8f;
        [Range(0f, 1f)] public float musicVolume = 0.45f;
        public SoundBank bank;

        [Tooltip("how many sounds can play at once")]
        [Range(4, 32)] public int voices = 16;
        AudioSource[] sources;
        float[] startTimes;
        int[] priorities;
        AudioSource music;

        readonly Dictionary<SfxId, SoundBank.Entry> lookup = new Dictionary<SfxId, SoundBank.Entry>();
        AudioClip[] fallback;
        AudioClip fallbackMusic;
        readonly int[] lastPlayedFrame = new int[64];
        System.Random rng = new System.Random(7);

        public static AudioManager Create(SoundBank bank)
        {
            var go = new GameObject("AudioManager");
            var am = go.AddComponent<AudioManager>();
            am.bank = bank;
            am.Init();
            return am;
        }

        bool ready;

        void Awake() { Init(); }

        public void Init()
        {
            if (ready) return;
            ready = true;
            Instance = this;
            sources = new AudioSource[voices];
            startTimes = new float[voices];
            priorities = new int[voices];
            for (int i = 0; i < voices; i++)
            {
                var child = new GameObject("Voice" + i);
                child.transform.SetParent(transform, false);
                var s = child.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                sources[i] = s;
            }
            music = gameObject.AddComponent<AudioSource>();
            music.loop = true;
            music.playOnAwake = false;

            if (bank != null)
                foreach (var e in bank.entries)
                    if (e != null && e.clips != null && e.clips.Length > 0) lookup[e.id] = e;

            //it makes all the fallback sounds once at the start so there is no hitch later
            fallback = ProceduralSfx.BuildAll();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        //priority matters when all the voices are busy so hits steal from footsteps and not the other way around
        static int PriorityOf(SfxId id)
        {
            switch (id)
            {
                case SfxId.KO: case SfxId.SuperFlash: case SfxId.RoundAnnounce: case SfxId.Fight: return 5;
                case SfxId.HitHeavy: case SfxId.CounterHit: case SfxId.HitSpecial: case SfxId.Throw: return 4;
                case SfxId.HitLight: case SfxId.HitMedium: case SfxId.Block: case SfxId.ThrowTech: return 3;
                case SfxId.Fireball: case SfxId.ProjectileClash: case SfxId.Knockdown: return 2;
                default: return 1;
            }
        }

        //pan goes from left to right so hits on the left side of the screen come out of the left speaker
        public void Play(SfxId id, float volume = 1f, float pan = 0f)
        {
            int idx = (int)id;
            //this stops the exact same sound from stacking on the same frame which just makes it louder and muddy
            if (lastPlayedFrame[idx] == Time.frameCount) return;
            lastPlayedFrame[idx] = Time.frameCount;

            AudioClip clip;
            float vol = volume, pitchVar = 0.05f;
            SoundBank.Entry e;
            if (lookup.TryGetValue(id, out e))
            {
                clip = e.clips[rng.Next(e.clips.Length)];
                vol *= e.volume;
                pitchVar = e.pitchVariance;
            }
            else clip = fallback[idx];
            if (clip == null) return;
            PlayClip(clip, vol, pan, PriorityOf(id), pitchVar);
        }

        //this plays any clip through the pool like a fighter voice line or a move sound from a FighterAsset
        public void PlayClip(AudioClip clip, float volume = 1f, float pan = 0f, int prio = 3, float pitchVar = 0.03f)
        {
            if (clip == null || sources == null) return;
            float vol = volume;
            int pick = -1;
            float oldest = float.MaxValue;
            for (int i = 0; i < sources.Length; i++)
            {
                if (!sources[i].isPlaying) { pick = i; break; }
                if (priorities[i] <= prio && startTimes[i] < oldest) { oldest = startTimes[i]; pick = i; }
            }
            if (pick < 0) return;

            var s = sources[pick];
            s.clip = clip;
            s.volume = vol * sfxVolume;
            s.pitch = 1f + ((float)rng.NextDouble() * 2f - 1f) * pitchVar;
            s.panStereo = Mathf.Clamp(pan, -1f, 1f) * 0.6f;
            s.Play();
            startTimes[pick] = Time.unscaledTime;
            priorities[pick] = prio;
        }

        //the stage can give its own music and that wins over the bank
        public void PlayMusic(bool battle, AudioClip overrideClip = null)
        {
            AudioClip clip = overrideClip;
            if (clip == null && bank != null) clip = battle ? bank.battleMusic : bank.menuMusic;
            if (clip == null && battle)
            {
                if (fallbackMusic == null) fallbackMusic = ProceduralSfx.BuildMusicLoop();
                clip = fallbackMusic;
            }
            if (music.clip == clip && music.isPlaying) return;
            music.Stop();
            music.clip = clip;
            music.volume = musicVolume;
            if (clip != null) music.Play();
        }

        public void StopMusic() { music.Stop(); }
        public void SetMusicPaused(bool paused) { if (paused) music.Pause(); else music.UnPause(); }
    }

    //this makes simple sound effects out of math so the game has punchy sounds even with no audio files
    //every hit is kind of a low thump plus a noise crack and the thump pitch drops fast which is what makes it feel heavy
    public static class ProceduralSfx
    {
        const int Rate = 44100;
        static uint seed = 12345;

        static float Noise()
        {
            seed = seed * 1664525u + 1013904223u;
            return ((seed >> 9) / 4194304f) * 2f - 1f;
        }

        public static AudioClip[] BuildAll()
        {
            var ids = (SfxId[])Enum.GetValues(typeof(SfxId));
            var clips = new AudioClip[ids.Length];
            foreach (var id in ids) clips[(int)id] = Build(id);
            return clips;
        }

        static AudioClip Build(SfxId id)
        {
            switch (id)
            {
                case SfxId.HitLight: return Hit("hit_light", 0.11f, 190f, 90f, 0.55f, 45f);
                case SfxId.HitMedium: return Hit("hit_medium", 0.17f, 150f, 60f, 0.7f, 30f);
                case SfxId.HitHeavy: return Hit("hit_heavy", 0.28f, 120f, 40f, 0.9f, 18f);
                case SfxId.HitSpecial: return Hit("hit_special", 0.3f, 130f, 45f, 0.95f, 16f);
                case SfxId.CounterHit: return Bell("counter", 0.35f, new[] { 1320f, 1980f, 2640f }, 10f);
                case SfxId.Block: return Block();
                case SfxId.WhooshLight: return Whoosh("whoosh_l", 0.1f, 0.35f);
                case SfxId.WhooshHeavy: return Whoosh("whoosh_h", 0.2f, 0.18f);
                case SfxId.Fireball: return Sweep("fireball", 0.4f, 260f, 720f, 0.5f, true);
                case SfxId.ProjectileClash: return Bell("clash", 0.3f, new[] { 900f, 1350f, 2100f }, 14f);
                case SfxId.Jump: return Sweep("jump", 0.09f, 220f, 420f, 0.25f, false);
                case SfxId.Land: return Hit("land", 0.08f, 90f, 60f, 0.35f, 60f);
                case SfxId.Dash: return Whoosh("dash", 0.15f, 0.25f);
                case SfxId.Throw: return Hit("throw", 0.3f, 100f, 45f, 0.9f, 14f);
                case SfxId.ThrowTech: return Bell("tech", 0.2f, new[] { 1500f, 2250f }, 22f);
                case SfxId.Knockdown: return Hit("knockdown", 0.4f, 85f, 35f, 0.8f, 10f);
                case SfxId.SuperFlash: return Sweep("super", 0.7f, 350f, 1700f, 0.45f, true);
                case SfxId.KO: return Boom();
                case SfxId.RoundAnnounce: return Chord("round", new[] { 523f, 659f }, 0.5f);
                case SfxId.Fight: return Chord("fight", new[] { 523f, 659f, 784f, 1047f }, 0.7f);
                case SfxId.RoundWin: return Chord("win", new[] { 392f, 523f, 659f }, 0.8f);
                case SfxId.UIMove: return Bell("ui_move", 0.06f, new[] { 1200f }, 50f);
                case SfxId.UIConfirm: return Chord("ui_ok", new[] { 880f, 1320f }, 0.15f);
                case SfxId.UIBack: return Chord("ui_back", new[] { 660f, 440f }, 0.15f);
            }
            return null;
        }

        static AudioClip Make(string name, float[] data)
        {
            //this soft clips so loud sounds get crunchy instead of harsh
            for (int i = 0; i < data.Length; i++) data[i] = (float)Math.Tanh(data[i] * 1.4f) * 0.9f;
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip Hit(string name, float len, float f0, float f1, float noiseAmt, float decay)
        {
            int n = (int)(len * Rate);
            var d = new float[n];
            double phase = 0;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float k = t / len;
                float freq = f1 + (f0 - f1) * Mathf.Exp(-k * 8f);
                phase += 2 * Math.PI * freq / Rate;
                float body = (float)Math.Sin(phase) * Mathf.Exp(-t * decay * 0.6f);
                lp += (Noise() - lp) * 0.35f;
                float crack = lp * Mathf.Exp(-t * decay * 2.5f) * noiseAmt;
                d[i] = body * 0.9f + crack;
            }
            return Make(name, d);
        }

        static AudioClip Block()
        {
            int n = (int)(0.12f * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Exp(-t * 45f);
                d[i] = (Mathf.Sin(2 * Mathf.PI * 1150f * t) * 0.4f + Mathf.Sin(2 * Mathf.PI * 1730f * t) * 0.3f + Noise() * 0.5f * Mathf.Exp(-t * 120f)) * env
                     + Mathf.Sin(2 * Mathf.PI * 160f * t) * Mathf.Exp(-t * 50f) * 0.5f;
            }
            return Make("block", d);
        }

        static AudioClip Whoosh(string name, float len, float bright)
        {
            int n = (int)(len * Rate);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                float env = Mathf.Sin(k * Mathf.PI) * Mathf.Sin(k * Mathf.PI);
                float cut = bright * (0.3f + env);
                lp += (Noise() - lp) * cut;
                d[i] = lp * env * 1.3f;
            }
            return Make(name, d);
        }

        static AudioClip Sweep(string name, float len, float f0, float f1, float noiseAmt, bool wobble)
        {
            int n = (int)(len * Rate);
            var d = new float[n];
            double phase = 0;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float k = t / len;
                float freq = Mathf.Lerp(f0, f1, Mathf.Sqrt(k)) * (wobble ? 1f + Mathf.Sin(t * 60f) * 0.03f : 1f);
                phase += 2 * Math.PI * freq / Rate;
                float env = Mathf.Min(1f, k * 20f) * (1f - k);
                lp += (Noise() - lp) * 0.2f;
                d[i] = ((float)Math.Sin(phase) * 0.5f + (float)Math.Sin(phase * 2) * 0.2f + lp * noiseAmt) * env;
            }
            return Make(name, d);
        }

        static AudioClip Bell(string name, float len, float[] freqs, float decay)
        {
            int n = (int)(len * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float v = 0f;
                for (int j = 0; j < freqs.Length; j++) v += Mathf.Sin(2 * Mathf.PI * freqs[j] * t) / freqs.Length;
                d[i] = v * Mathf.Exp(-t * decay) * Mathf.Min(1f, t * 400f);
            }
            return Make(name, d);
        }

        static AudioClip Chord(string name, float[] freqs, float len)
        {
            int n = (int)(len * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float k = t / len;
                float v = 0f;
                for (int j = 0; j < freqs.Length; j++)
                {
                    //each note comes in a tiny bit after the last one so it sounds like a little fanfare
                    float start = j * 0.06f;
                    if (t < start) continue;
                    float tt = t - start;
                    float saw = (tt * freqs[j]) % 1f * 2f - 1f;
                    v += (Mathf.Sin(2 * Mathf.PI * freqs[j] * tt) * 0.7f + saw * 0.15f) / freqs.Length;
                }
                d[i] = v * (1f - k) * Mathf.Min(1f, t * 200f) * 0.9f;
            }
            return Make(name, d);
        }

        static AudioClip Boom()
        {
            int n = (int)(1.1f * Rate);
            var d = new float[n];
            double phase = 0;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float freq = 30f + 50f * Mathf.Exp(-t * 6f);
                phase += 2 * Math.PI * freq / Rate;
                lp += (Noise() - lp) * 0.08f;
                float v = (float)Math.Sin(phase) * Mathf.Exp(-t * 3f) + lp * 1.5f * Mathf.Exp(-t * 4f);
                //this is a cheap echo so the ko feels big
                int echo = i - (int)(0.12f * Rate);
                if (echo > 0) v += d[echo] * 0.35f;
                d[i] = v;
            }
            return Make("ko", d);
        }

        //this makes a short looping beat so the fight is not silent
        //it is kind of basic but it gives the match energy until you drop in real music
        public static AudioClip BuildMusicLoop()
        {
            const int rate = 22050;
            const float bpm = 132f;
            float beat = 60f / bpm;
            int bars = 4;
            int n = (int)(beat * 4 * bars * rate);
            var d = new float[n];
            float[] bassNotes = { 55f, 55f, 65.4f, 49f };
            uint s = 99;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float b = t / beat;
                int beatIndex = (int)b;
                float inBeat = b - beatIndex;
                float inEighth = (b * 2f) % 1f;
                float v = 0f;
                //kick on every beat
                float kt = inBeat * beat;
                v += Mathf.Sin(2 * Mathf.PI * (50f + 90f * Mathf.Exp(-kt * 30f)) * kt) * Mathf.Exp(-kt * 9f) * 0.8f;
                //snare on two and four
                if (beatIndex % 2 == 1)
                {
                    s = s * 1664525u + 1013904223u;
                    float nz = ((s >> 9) / 4194304f) * 2f - 1f;
                    v += nz * Mathf.Exp(-kt * 18f) * 0.35f + Mathf.Sin(2 * Mathf.PI * 190f * kt) * Mathf.Exp(-kt * 25f) * 0.25f;
                }
                //hats on eighths
                {
                    s = s * 1664525u + 1013904223u;
                    float nz = ((s >> 9) / 4194304f) * 2f - 1f;
                    float ht = inEighth * beat * 0.5f;
                    v += nz * Mathf.Exp(-ht * 90f) * 0.12f;
                }
                //bass line
                int bar = (beatIndex / 4) % bars;
                float bf = bassNotes[bar] * ((beatIndex % 4 == 3 && inBeat > 0.5f) ? 1.5f : 1f);
                float saw = (t * bf) % 1f * 2f - 1f;
                v += saw * 0.22f * (0.6f + 0.4f * Mathf.Exp(-inEighth * 4f));
                d[i] = (float)Math.Tanh(v * 1.2f) * 0.7f;
            }
            var clip = AudioClip.Create("fight_loop", n, 1, rate, false);
            clip.SetData(d, 0);
            return clip;
        }
    }
}

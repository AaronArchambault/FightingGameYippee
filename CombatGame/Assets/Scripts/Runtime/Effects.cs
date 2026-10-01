using UnityEngine;
using FightCore;

namespace FightGame
{
    public enum SparkKind { Hit, Counter, Block, Clash, Dust, Tech, Burst, Puff }

    //this is one pooled hit spark made of a few sprites
    //it animates itself over a few frames and then tells the pool it is done
    public class Spark : MonoBehaviour
    {
        SpriteRenderer core, ring;
        SpriteRenderer[] streaks;
        float age, life, scale;
        Color color;
        SparkKind kind;
        float spin;

        public void Init()
        {
            core = SpriteFactory.Make("Core", transform, SpriteFactory.SoftCircle, Color.white, 80);
            ring = SpriteFactory.Make("Ring", transform, SpriteFactory.Ring, Color.white, 81);
            streaks = new SpriteRenderer[6];
            for (int i = 0; i < streaks.Length; i++)
                streaks[i] = SpriteFactory.Make("Streak", transform, SpriteFactory.Square, Color.white, 82);
        }

        public void Play(Vector3 pos, Color c, float s, SparkKind k, float randomSeed)
        {
            transform.position = pos;
            color = c;
            scale = s;
            kind = k;
            age = 0f;
            spin = randomSeed * 360f;
            switch (k)
            {
                case SparkKind.Dust: life = 0.35f; break;
                case SparkKind.Puff: life = 0.25f; break;
                case SparkKind.Block: life = 0.2f; break;
                case SparkKind.Burst: life = 0.5f; break;
                default: life = 0.22f + 0.05f * s; break;
            }
            transform.rotation = Quaternion.Euler(0f, 0f, spin);
            Tick(0f);
        }

        //it returns false when the spark is finished
        public bool Tick(float dt)
        {
            age += dt;
            float t = Mathf.Clamp01(age / life);
            float fade = 1f - t;
            bool streaky = kind == SparkKind.Hit || kind == SparkKind.Counter || kind == SparkKind.Clash || kind == SparkKind.Burst || kind == SparkKind.Tech;

            switch (kind)
            {
                case SparkKind.Dust:
                case SparkKind.Puff:
                    core.color = FightUtil.WithAlpha(color, 0.6f * fade);
                    core.transform.localScale = Vector3.one * scale * (0.5f + t * 1.2f);
                    core.transform.localPosition = new Vector3(0f, t * 0.3f * scale, 0f);
                    ring.color = Color.clear;
                    break;
                case SparkKind.Block:
                    core.color = FightUtil.WithAlpha(color, 0.9f * fade);
                    core.transform.localScale = Vector3.one * scale * 0.6f;
                    core.transform.localPosition = Vector3.zero;
                    ring.color = FightUtil.WithAlpha(Color.white, fade);
                    ring.transform.localScale = Vector3.one * scale * (0.4f + t * 1.1f);
                    break;
                default:
                    //this is a big bright flash that pops out then a ring that grows so the hit feels heavy
                    float pop = t < 0.15f ? t / 0.15f : 1f;
                    core.color = FightUtil.WithAlpha(Color.Lerp(Color.white, color, t * 1.5f), fade);
                    core.transform.localScale = Vector3.one * scale * (0.5f + pop * 0.7f) * (1f - t * 0.5f);
                    core.transform.localPosition = Vector3.zero;
                    ring.color = FightUtil.WithAlpha(color, fade * 0.9f);
                    ring.transform.localScale = Vector3.one * scale * (0.3f + t * 1.6f);
                    break;
            }

            for (int i = 0; i < streaks.Length; i++)
            {
                var st = streaks[i];
                if (!streaky) { st.color = Color.clear; continue; }
                float ang = i * (360f / streaks.Length) + (i % 2) * 17f;
                float dist = scale * (0.2f + t * 0.9f);
                float rad = ang * Mathf.Deg2Rad;
                st.transform.localPosition = new Vector3(Mathf.Cos(rad) * dist, Mathf.Sin(rad) * dist, 0f);
                st.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
                st.transform.localScale = new Vector3(scale * 0.55f * fade, scale * 0.07f * fade, 1f);
                st.color = FightUtil.WithAlpha(i % 2 == 0 ? Color.white : color, fade);
            }
            return age < life;
        }
    }

    //this is a see through copy that fades out and it is used for dash trails
    public class Afterimage : MonoBehaviour
    {
        public SpriteRenderer sr;
        float age;
        const float Life = 0.25f;
        Color c;

        public void Play(Vector3 pos, Color color, float height)
        {
            transform.position = pos + new Vector3(0f, height * 0.5f, 0f);
            transform.localScale = new Vector3(height * 0.45f, height, 1f);
            c = color;
            age = 0f;
            sr.color = FightUtil.WithAlpha(c, 0.35f);
        }

        public bool Tick(float dt)
        {
            age += dt;
            sr.color = FightUtil.WithAlpha(c, 0.35f * (1f - age / Life));
            return age < Life;
        }
    }

    //this is the look of one fireball and it just follows the sim projectile in the same slot
    public class ProjectileView : MonoBehaviour
    {
        SpriteRenderer glow, core;
        SpriteRenderer[] trail;
        public void Init()
        {
            glow = SpriteFactory.Make("Glow", transform, SpriteFactory.SoftCircle, Color.white, 60);
            core = SpriteFactory.Make("Core", transform, SpriteFactory.Circle, Color.white, 62);
            trail = new SpriteRenderer[4];
            for (int i = 0; i < trail.Length; i++) trail[i] = SpriteFactory.Make("Trail", transform, SpriteFactory.SoftCircle, Color.white, 59);
        }

        public void Sync(Projectile p, Color color, int frame)
        {
            var b = p.def.box;
            float w = b.w / 1000f, h = b.h / 1000f;
            transform.position = FightUtil.ToWorld(p.x + p.dir * b.cx, p.y + b.cy, -0.1f);
            float pulse = 1f + Mathf.Sin(frame * 0.6f) * 0.08f;
            float big = p.def.visualSize > 1 ? 1.3f : 1f;
            core.transform.localScale = new Vector3(w * 0.8f, h * 0.9f, 1f) * pulse * big;
            core.color = Color.Lerp(Color.white, color, 0.35f);
            glow.transform.localScale = new Vector3(w * 1.9f, h * 2.1f, 1f) * pulse * big;
            glow.color = FightUtil.WithAlpha(color, 0.85f);
            for (int i = 0; i < trail.Length; i++)
            {
                float k = (i + 1) / (float)(trail.Length + 1);
                trail[i].transform.localPosition = new Vector3(-p.dir * w * 0.55f * (i + 1), Mathf.Sin(frame * 0.5f + i) * 0.04f, 0f);
                trail[i].transform.localScale = new Vector3(w * (1.2f - k * 0.7f), h * (1.2f - k * 0.7f), 1f) * big;
                trail[i].color = FightUtil.WithAlpha(color, 0.5f * (1f - k));
            }
        }
    }

    //this owns all the pooled visual effects
    public class EffectsManager : MonoBehaviour
    {
        ObjectPool<Spark> sparks;
        ObjectPool<Afterimage> afterimages;
        ObjectPool<ProjectileView> projectilePool;
        readonly ProjectileView[] projViews = new ProjectileView[12];
        SpriteRenderer dim;
        float dimAlpha;
        int seedCounter;

        public void Init()
        {
            sparks = new ObjectPool<Spark>(() =>
            {
                var go = new GameObject("Spark");
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<Spark>();
                s.Init();
                return s;
            }, 24, 64);

            afterimages = new ObjectPool<Afterimage>(() =>
            {
                var go = new GameObject("Afterimage");
                go.transform.SetParent(transform, false);
                var a = go.AddComponent<Afterimage>();
                a.sr = go.AddComponent<SpriteRenderer>();
                a.sr.sprite = SpriteFactory.SoftCircle;
                a.sr.sortingOrder = 15;
                return a;
            }, 16, 48);

            projectilePool = new ObjectPool<ProjectileView>(() =>
            {
                var go = new GameObject("ProjectileView");
                go.transform.SetParent(transform, false);
                var v = go.AddComponent<ProjectileView>();
                v.Init();
                return v;
            }, 6, 12);

            //this is the dark screen that shows up during a super flash
            dim = SpriteFactory.Make("SuperDim", transform, SpriteFactory.Square, new Color(0, 0, 0, 0), 5);
            dim.transform.localScale = new Vector3(200f, 200f, 1f);
            dim.transform.position = new Vector3(0f, 0f, 2f);
        }

        public void Spark(Vector3 pos, Color c, float scale, SparkKind kind)
        {
            var s = sparks.Get();
            seedCounter++;
            s.Play(pos, c, scale, kind, (seedCounter * 0.618f) % 1f);
        }

        public void Trail(Vector3 feet, Color c, float height)
        {
            afterimages.Get().Play(feet, c, height);
        }

        public void ClearAll()
        {
            sparks.ReleaseAll();
            afterimages.ReleaseAll();
            projectilePool.ReleaseAll();
            for (int i = 0; i < projViews.Length; i++) projViews[i] = null;
        }

        //this is called every render frame to animate everything that is out
        public void Tick(float dt, MatchSim sim, Color p1Color, Color p2Color)
        {
            var list = sparks.Active;
            for (int i = list.Count - 1; i >= 0; i--) if (!list[i].Tick(dt)) sparks.Release(list[i]);
            var al = afterimages.Active;
            for (int i = al.Count - 1; i >= 0; i--) if (!al[i].Tick(dt)) afterimages.Release(al[i]);

            if (sim == null) return;
            for (int i = 0; i < sim.projectiles.Length && i < projViews.Length; i++)
            {
                var p = sim.projectiles[i];
                if (p.active)
                {
                    if (projViews[i] == null) projViews[i] = projectilePool.Get();
                    projViews[i].Sync(p, p.owner == 0 ? p1Color : p2Color, sim.frame);
                }
                else if (projViews[i] != null)
                {
                    projectilePool.Release(projViews[i]);
                    projViews[i] = null;
                }
            }

            //it fades the dark screen in and out for supers
            float target = sim.superFreeze > 0 ? 0.6f : 0f;
            dimAlpha = Mathf.MoveTowards(dimAlpha, target, dt * 4f);
            dim.color = new Color(0f, 0f, 0.05f, dimAlpha);
        }
    }
}

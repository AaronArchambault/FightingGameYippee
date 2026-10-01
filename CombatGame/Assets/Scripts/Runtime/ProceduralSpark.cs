using UnityEngine;

namespace FightGame
{
    public enum SparkKind { Hit, Counter, Block, Clash, Dust, Tech, Burst, Puff }

    //this is the built in hit spark made from shapes and it is used for any kind you did not give a prefab for
    public class ProceduralSpark : PooledEffect
    {
        public SparkKind kind;
        SpriteRenderer core, ring;
        SpriteRenderer[] streaks;
        float age, life, scale;
        Color color;
        static int seedCounter;

        void Build()
        {
            core = SpriteFactory.Make("Core", transform, SpriteFactory.SoftCircle, Color.white, 80);
            ring = SpriteFactory.Make("Ring", transform, SpriteFactory.Ring, Color.white, 81);
            streaks = new SpriteRenderer[6];
            for (int i = 0; i < streaks.Length; i++)
                streaks[i] = SpriteFactory.Make("Streak", transform, SpriteFactory.Square, Color.white, 82);
        }

        public override void Play(Vector3 pos, Color c, float s, int facing)
        {
            if (core == null) Build();
            transform.position = pos;
            color = c;
            scale = s;
            age = 0f;
            seedCounter++;
            switch (kind)
            {
                case SparkKind.Dust: life = 0.35f; break;
                case SparkKind.Puff: life = 0.25f; break;
                case SparkKind.Block: life = 0.2f; break;
                case SparkKind.Burst: life = 0.5f; break;
                default: life = 0.22f + 0.05f * s; break;
            }
            transform.rotation = Quaternion.Euler(0f, 0f, (seedCounter * 0.618f % 1f) * 360f);
            Tick(0f);
        }

        public override bool Tick(float dt)
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
}

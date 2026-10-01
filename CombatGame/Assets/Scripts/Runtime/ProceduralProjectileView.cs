using UnityEngine;
using FightCore;

namespace FightGame
{
    //this is the built in glowing ball fireball look
    public class ProceduralProjectileView : ProjectileView
    {
        SpriteRenderer glow, core;
        SpriteRenderer[] trail;

        public override void Init()
        {
            glow = SpriteFactory.Make("Glow", transform, SpriteFactory.SoftCircle, Color.white, 60);
            core = SpriteFactory.Make("Core", transform, SpriteFactory.Circle, Color.white, 62);
            trail = new SpriteRenderer[4];
            for (int i = 0; i < trail.Length; i++) trail[i] = SpriteFactory.Make("Trail", transform, SpriteFactory.SoftCircle, Color.white, 59);
        }

        public override void Sync(Projectile p, Color color, int frame)
        {
            base.Sync(p, color, frame);
            var b = p.def.box;
            float w = b.w / 1000f, h = b.h / 1000f;
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
}

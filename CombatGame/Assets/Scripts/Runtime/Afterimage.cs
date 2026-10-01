using UnityEngine;

namespace FightGame
{
    //this is a see through blur that fades out and it is used for dash trails
    public class Afterimage : PooledEffect
    {
        public float life = 0.25f;
        public float startAlpha = 0.35f;
        SpriteRenderer sr;
        float age;
        Color c;

        public override void Play(Vector3 feet, Color color, float height, int facing)
        {
            if (sr == null)
            {
                sr = gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.SoftCircle;
                sr.sortingOrder = 15;
            }
            transform.position = feet + new Vector3(0f, height * 0.5f, 0f);
            transform.localScale = new Vector3(height * 0.45f, height, 1f);
            c = color;
            age = 0f;
            sr.color = FightUtil.WithAlpha(c, startAlpha);
        }

        public override bool Tick(float dt)
        {
            age += dt;
            sr.color = FightUtil.WithAlpha(c, startAlpha * (1f - age / life));
            return age < life;
        }
    }
}

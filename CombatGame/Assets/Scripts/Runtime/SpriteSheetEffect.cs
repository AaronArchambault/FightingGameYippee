using UnityEngine;

namespace FightGame
{
    //this plays a list of sprites once like a hit spark from your own sprite sheet
    //make a prefab with this on it and drag it into the EffectsManager or a MoveFx slot on a fighter
    public class SpriteSheetEffect : PooledEffect
    {
        public Sprite[] frames;
        [Tooltip("how many sprites play per second")]
        public float fps = 30f;
        public SpriteRenderer spriteRenderer;
        public int sortingOrder = 80;
        [Tooltip("hits that are stronger make the effect bigger")]
        public bool scaleWithStrength = true;
        public float baseScale = 1f;
        [Tooltip("tints the sprite with the hit color and turn it off if your art already has color")]
        public bool useTint;
        [Tooltip("flips the effect to point away from the attacker")]
        public bool flipWithFacing = true;
        [Tooltip("random spin so repeated sparks do not look copy pasted")]
        public bool randomRotation;
        [Tooltip("fades out over the last part of the animation")]
        public bool fadeOut = true;

        float time;
        int lastIndex = -1;

        void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = sortingOrder;
        }

        public override void Play(Vector3 position, Color tint, float scale, int facing)
        {
            transform.position = position;
            float s = baseScale * (scaleWithStrength ? scale : 1f);
            transform.localScale = new Vector3(flipWithFacing && facing < 0 ? -s : s, s, 1f);
            transform.rotation = randomRotation ? Quaternion.Euler(0f, 0f, Random.Range(0, 360)) : Quaternion.identity;
            spriteRenderer.color = useTint ? tint : Color.white;
            time = 0f;
            lastIndex = -1;
            Tick(0f);
        }

        public override bool Tick(float dt)
        {
            if (frames == null || frames.Length == 0) return false;
            time += dt;
            int i = Mathf.FloorToInt(time * fps);
            if (i >= frames.Length) return false;
            if (i != lastIndex) { spriteRenderer.sprite = frames[i]; lastIndex = i; }
            if (fadeOut)
            {
                float k = i / (float)frames.Length;
                var c = spriteRenderer.color;
                c.a = k < 0.6f ? 1f : Mathf.Clamp01((1f - k) / 0.4f);
                spriteRenderer.color = c;
            }
            return true;
        }
    }
}

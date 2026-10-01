using UnityEngine;
using FightCore;

namespace FightGame
{
    //this draws a fireball with your own looping sprites
    //make a prefab with this and a SpriteRenderer then put it in a MoveFx slot on the fighter for that move
    public class SpriteProjectileView : ProjectileView
    {
        public Sprite[] frames;
        [Tooltip("how many game frames each sprite stays up")]
        [Min(1)] public int framesPerSprite = 3;
        public SpriteRenderer spriteRenderer;
        public float scale = 1f;
        [Tooltip("tints the sprite with the fighter accent color")]
        public bool useOwnerColor;
        [Tooltip("turn this on if the sprite is drawn flying left")]
        public bool artFacesLeft;
        public int sortingOrder = 60;

        int startFrame = -1;

        public override void Init()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = sortingOrder;
        }

        public override void Begin(Projectile p) { startFrame = -1; }

        public override void Sync(Projectile p, Color ownerColor, int simFrame)
        {
            base.Sync(p, ownerColor, simFrame);
            if (startFrame < 0) startFrame = simFrame;
            float dir = p.dir * (artFacesLeft ? -1f : 1f);
            transform.localScale = new Vector3(scale * dir, scale, 1f);
            spriteRenderer.color = useOwnerColor ? ownerColor : Color.white;
            if (frames != null && frames.Length > 0)
            {
                int i = ((simFrame - startFrame) / framesPerSprite) % frames.Length;
                spriteRenderer.sprite = frames[i];
            }
        }
    }
}

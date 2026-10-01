using UnityEngine;
using FightCore;

namespace FightGame
{
    //this draws a fighter with your own sprite sheets
    //it picks the sprite every frame from a SpriteAnimationSet using the frame data so the art always matches the hitboxes
    //put it on a prefab and set the prefab as the View Prefab on a FighterAsset or just set Animations on the FighterAsset and one gets made for you
    public class SpriteFighterView : FighterView
    {
        [Header("Sprites")]
        public SpriteAnimationSet animations;
        [Tooltip("the main sprite and it is made for you if empty")]
        public SpriteRenderer body;

        [Header("Hit Flash")]
        [Tooltip("this draws a solid colored copy of the sprite on top for a few frames when hit so hits read really clearly")]
        public bool silhouetteFlash = true;
        [Tooltip("a material that draws the sprite as one solid color and if empty it tries the built in GUI Text Shader")]
        public Material flashMaterial;
        public Color hitFlashColor = Color.white;
        [Min(0)] public int hitFlashFrames = 3;
        [Tooltip("tint while blocking")]
        public Color blockTint = new Color(0.65f, 0.8f, 1f);

        [Header("Readability")]
        [Tooltip("glows during the wind up of heavies and specials so the other player can react")]
        public bool anticipationGlow = true;
        public Color anticipationColor = new Color(1f, 0.8f, 0.3f);
        [Tooltip("player two gets this tint in mirror matches so you can tell them apart")]
        public Color mirrorTint = new Color(0.75f, 0.8f, 1f);

        SpriteRenderer flash, glow;
        bool mirror;
        Sprite lastSprite;

        protected override bool ArtFacesLeft { get { return animations != null && animations.artFacesLeft; } }

        public override void Setup(FighterDef def, int player, FighterAsset asset)
        {
            base.Setup(def, player, asset);
            if (animations == null && asset != null) animations = asset.animations;
            if (body == null) body = SpriteFactory.Make("Body", visualRoot, null, Color.white, sortingBase);
            float sc = animations != null ? animations.scale : 1f;
            body.transform.localScale = new Vector3(sc, sc, 1f);

            if (silhouetteFlash)
            {
                if (flashMaterial == null)
                {
                    var sh = Shader.Find("GUI/Text Shader");
                    if (sh != null) flashMaterial = new Material(sh);
                }
                if (flashMaterial != null)
                {
                    flash = SpriteFactory.Make("Flash", body.transform, null, Color.clear, sortingBase + 1);
                    flash.sharedMaterial = flashMaterial;
                }
            }
            if (anticipationGlow)
            {
                glow = SpriteFactory.Make("Glow", visualRoot, SpriteFactory.SoftCircle, Color.clear, sortingBase - 1);
                glow.transform.localPosition = new Vector3(0f, def.standHurt.h / 2000f, 0f);
                glow.transform.localScale = new Vector3(def.standHurt.w / 350f, def.standHurt.h / 650f, 1f);
            }
        }

        //it is told by the manager if both players picked the same fighter
        public void SetMirror(bool isMirror) { mirror = isMirror; }

        protected override void OnSync(MatchSim sim, FighterSim f)
        {
            if (animations == null) return;
            var fr = animations.Resolve(f);
            if (fr != null)
            {
                if (fr.sprite != lastSprite) { body.sprite = fr.sprite; lastSprite = fr.sprite; }
                body.transform.localPosition = new Vector3(fr.offset.x, fr.offset.y, 0f);
            }

            Color c = Color.white;
            if (mirror && player == 1) c = mirrorTint;
            if (f.state == FState.BlockStun && f.stateFrame < 4) c *= blockTint;
            body.color = c;
            body.sortingOrder = currentSorting;

            if (flash != null)
            {
                bool on = InHitFlash(sim, f, hitFlashFrames);
                flash.sprite = body.sprite;
                flash.color = on ? hitFlashColor : Color.clear;
                flash.sortingOrder = currentSorting + 1;
            }

            if (glow != null)
            {
                float a = Anticipation(f);
                if (f.Move != null && f.Move.isSuper && f.state == FState.Attack) a = Mathf.Max(a, 0.6f);
                glow.color = FightUtil.WithAlpha(anticipationColor, a * 0.45f);
                glow.sortingOrder = currentSorting - 1;
            }
        }
    }
}

using UnityEngine;
using FightCore;

namespace FightGame
{
    //this is the base for anything that draws a fighter
    //it handles the stuff every look needs like position facing the shadow and the hitstop shake
    //the kinds that come with the game are PuppetFighterView SpriteFighterView and AnimatorFighterView
    //you can make your own by inheriting from this and overriding OnSync
    //views only ever read the sim and never change it which is what keeps the fight deterministic
    public class FighterView : MonoBehaviour
    {
        [Header("Shared")]
        [Tooltip("the child that gets flipped and shaken and if it is empty one gets made for you")]
        public Transform visualRoot;
        [Tooltip("the blob shadow on the floor and it is made for you if empty")]
        public SpriteRenderer shadow;
        public bool showShadow = true;
        public float shadowWidth = 1.3f;
        [Tooltip("how far the fighter wiggles while frozen in hitstop")]
        public float hitstopShake = 0.05f;
        [Tooltip("sorting order for the fighter and the one attacking gets the boost so they draw on top")]
        public int sortingBase = 20;
        public int attackerSortingBoost = 20;

        protected FighterDef def;
        protected FighterAsset asset;
        protected int player;
        protected int currentSorting;

        public int Player { get { return player; } }

        //sprites are drawn facing right unless the art says otherwise
        protected virtual bool ArtFacesLeft { get { return false; } }

        public virtual void Setup(FighterDef def, int player, FighterAsset asset)
        {
            this.def = def;
            this.player = player;
            this.asset = asset;
            if (visualRoot == null)
            {
                visualRoot = new GameObject("Visual").transform;
                visualRoot.SetParent(transform, false);
            }
            if (shadow == null && showShadow)
                shadow = SpriteFactory.Make("Shadow", transform, SpriteFactory.SoftCircle, new Color(0f, 0f, 0f, 0.45f), 1);
            if (shadow != null) shadow.gameObject.SetActive(showShadow);
        }

        //this is called every render frame by the manager
        public void Sync(MatchSim sim)
        {
            var f = sim.fighters[player];
            transform.position = FightUtil.ToWorld(f.x, f.y, player == 0 ? 0f : 0.01f);

            //the shadow stays on the floor and shrinks when you jump
            if (shadow != null && showShadow)
            {
                float h = Mathf.Clamp01(f.y / 3000f);
                float w = shadowWidth * Mathf.Clamp(def.standHurt.w / 600f, 0.8f, 1.4f);
                shadow.transform.position = new Vector3(transform.position.x, 0.02f, 0.5f);
                shadow.transform.localScale = new Vector3(w * (1f - h * 0.5f), 0.25f * (1f - h * 0.5f), 1f);
            }

            //this flips the art to face the other fighter
            var s = visualRoot.localScale;
            float facing = f.facing * (ArtFacesLeft ? -1f : 1f);
            s.x = Mathf.Abs(s.x) * facing;
            visualRoot.localScale = s;

            //this is the hitstop shake where the one getting hit wiggles while everything is frozen
            float shake = 0f;
            if (f.hitstop > 0 && IsHurt(f))
                shake = ((sim.frame & 1) == 0 ? 1f : -1f) * hitstopShake * Mathf.Min(1f, f.hitstop / 4f);
            visualRoot.localPosition = new Vector3(shake, visualRoot.localPosition.y, visualRoot.localPosition.z);

            //the one who is attacking gets drawn on top which is kind of how street fighter does it
            currentSorting = (f.state == FState.Attack || f.state == FState.Throwing) ? sortingBase + attackerSortingBoost : sortingBase;

            OnSync(sim, f);
        }

        protected virtual void OnSync(MatchSim sim, FighterSim f) { }

        protected static bool IsHurt(FighterSim f)
        {
            return f.state == FState.HitStun || f.state == FState.BlockStun || f.state == FState.AirHitStun;
        }

        //this is true for the first few frames after getting hit and it is used for the white flash
        protected static bool InHitFlash(MatchSim sim, FighterSim f, int frames)
        {
            int since = sim.frame - f.lastHurtFrame;
            return since >= 0 && since < frames && (f.state == FState.HitStun || f.state == FState.AirHitStun);
        }

        //this is true during the startup of moves you should be able to see coming like heavies and specials
        protected static float Anticipation(FighterSim f)
        {
            var m = f.Move;
            if (f.state != FState.Attack || m == null) return 0f;
            if (f.moveFrame >= m.startup) return 0f;
            if (m.hit.level < HitLevel.Heavy && m.kind == MoveKind.Normal) return 0f;
            return Mathf.Clamp01(f.moveFrame / (float)Mathf.Max(1, m.startup - 1));
        }
    }
}

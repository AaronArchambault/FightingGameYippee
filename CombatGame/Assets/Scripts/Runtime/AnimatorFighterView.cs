using UnityEngine;
using FightCore;

namespace FightGame
{
    //this drives a normal unity Animator from the sim
    //name each animator state after a move id like st_lp or a state name like idle walkf crouch jump hitstun block knockdown win
    //it sets the exact time from the frame count every frame so the animation can never drift from the frame data
    public class AnimatorFighterView : FighterView
    {
        public Animator animator;
        [Tooltip("how many game frames one loop of an idle or walk animation takes")]
        [Min(1)] public int loopFrames = 60;
        [Tooltip("which animator layer to drive")]
        public int layer;


        public override void Setup(FighterDef def, int player, FighterAsset asset)
        {
            base.Setup(def, player, asset);
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.speed = 0f;
        }

        protected override void OnSync(MatchSim sim, FighterSim f)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            int hash;
            float norm;
            if (f.state == FState.Attack && f.Move != null && Has(f.Move.id, out hash))
            {
                norm = Mathf.Clamp01((f.moveFrame - 1) / (float)Mathf.Max(1, f.Move.TotalFrames));
            }
            else
            {
                hash = StateHash(f);
                if (hash == 0) return;
                norm = (f.stateFrame % loopFrames) / (float)loopFrames;
            }
            animator.Play(hash, layer, norm);
            animator.Update(0f);
        }

        bool Has(string key, out int hash)
        {
            hash = Animator.StringToHash(key);
            return animator.HasState(layer, hash);
        }

        int StateHash(FighterSim f)
        {
            int hash;
            //it tries the most specific name first then falls back the same way the sprite sets do
            switch (f.state)
            {
                case FState.Idle: if (Has("idle", out hash)) return hash; break;
                case FState.WalkF: if (Has("walkf", out hash)) return hash; break;
                case FState.WalkB: if (Has("walkb", out hash) || Has("walkf", out hash)) return hash; break;
                case FState.Crouch: case FState.PreJump: case FState.Land: if (Has("crouch", out hash)) return hash; break;
                case FState.Air: if (Has("jump", out hash)) return hash; break;
                case FState.DashF: if (Has("dashf", out hash) || Has("walkf", out hash)) return hash; break;
                case FState.DashB: if (Has("dashb", out hash) || Has("walkb", out hash)) return hash; break;
                case FState.HitStun: if ((f.crouchHit && Has("crouchhit", out hash)) || Has("hitstun", out hash)) return hash; break;
                case FState.AirHitStun: if (Has("airhit", out hash) || Has("hitstun", out hash)) return hash; break;
                case FState.BlockStun: if ((f.crouchBlocking && Has("crouchblock", out hash)) || Has("block", out hash)) return hash; break;
                case FState.Knockdown: if (Has("knockdown", out hash)) return hash; break;
                case FState.KO: if (Has("ko", out hash) || Has("knockdown", out hash)) return hash; break;
                case FState.Thrown: if (Has("thrown", out hash) || Has("hitstun", out hash)) return hash; break;
                case FState.Throwing: if (Has("throwing", out hash)) return hash; break;
                case FState.TechRecover: if (Has("tech", out hash)) return hash; break;
                case FState.Win: if (Has("win", out hash)) return hash; break;
            }
            return Has("idle", out hash) ? hash : 0;
        }
    }
}

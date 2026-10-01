using UnityEngine;
using FightCore;

namespace FightGame
{
    //this is one debug box with a see through fill and a solid outline
    public class DebugBox : MonoBehaviour
    {
        SpriteRenderer fill;
        SpriteRenderer[] edges = new SpriteRenderer[4];

        public void Init()
        {
            fill = SpriteFactory.Make("Fill", transform, SpriteFactory.Square, Color.white, 90);
            for (int i = 0; i < 4; i++) edges[i] = SpriteFactory.Make("Edge", transform, SpriteFactory.Square, Color.white, 91);
        }

        public void Set(WorldBox b, Color c)
        {
            float l = b.left * FightUtil.SimToWorld, r = b.right * FightUtil.SimToWorld;
            float bo = b.bottom * FightUtil.SimToWorld, t = b.top * FightUtil.SimToWorld;
            float w = Mathf.Max(0.01f, r - l), h = Mathf.Max(0.01f, t - bo);
            const float th = 0.025f;
            transform.position = new Vector3((l + r) * 0.5f, (bo + t) * 0.5f, -1f);
            fill.transform.localScale = new Vector3(w, h, 1f);
            fill.color = FightUtil.WithAlpha(c, 0.25f);
            SetEdge(0, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, th, 1f), c);
            SetEdge(1, new Vector3(0f, -h * 0.5f, 0f), new Vector3(w, th, 1f), c);
            SetEdge(2, new Vector3(-w * 0.5f, 0f, 0f), new Vector3(th, h, 1f), c);
            SetEdge(3, new Vector3(w * 0.5f, 0f, 0f), new Vector3(th, h, 1f), c);
        }

        void SetEdge(int i, Vector3 pos, Vector3 scale, Color c)
        {
            edges[i].transform.localPosition = pos;
            edges[i].transform.localScale = scale;
            edges[i].color = c;
        }
    }

    //this shows hitboxes hurtboxes pushboxes and throw ranges on top of the fight
    //red is where you can hit green is where you can get hit yellow is the pushbox and purple is throw range
    //when a fighter is invincible their hurtbox turns white so you can see reversals working
    public class HitboxDebugView : MonoBehaviour
    {
        public static readonly Color HitColor = new Color(1f, 0.15f, 0.15f);
        public static readonly Color HurtColor = new Color(0.2f, 1f, 0.35f);
        public static readonly Color InvulnColor = new Color(1f, 1f, 1f);
        public static readonly Color PushColor = new Color(1f, 0.9f, 0.2f);
        public static readonly Color ThrowColor = new Color(0.8f, 0.3f, 1f);
        public static readonly Color ProjColor = new Color(1f, 0.45f, 0.1f);

        ObjectPool<DebugBox> pool;
        readonly WorldBox[] hurt = new WorldBox[6];
        public bool visible;

        void Awake() { Init(); }

        public void Init()
        {
            if (pool != null) return;
            pool = new ObjectPool<DebugBox>(() =>
            {
                var go = new GameObject("DebugBox");
                go.transform.SetParent(transform, false);
                var b = go.AddComponent<DebugBox>();
                b.Init();
                return b;
            }, 20, 48);
        }

        int used;

        //it reuses the boxes that are already showing so it is not turning objects on and off every frame
        void Add(WorldBox b, Color c)
        {
            var list = pool.Active;
            DebugBox box = used < list.Count ? list[used] : pool.Get();
            used++;
            box.Set(b, c);
        }

        //this redraws all the boxes every frame using the pool so nothing gets made or thrown away
        public void Sync(MatchSim sim)
        {
            used = 0;
            if (visible && sim != null) Collect(sim);
            var list = pool.Active;
            for (int i = list.Count - 1; i >= used; i--) pool.Release(list[i]);
        }

        void Collect(MatchSim sim)
        {
            for (int i = 0; i < 2; i++)
            {
                var f = sim.fighters[i];
                Add(f.GetPushbox(), PushColor);
                int n = f.GetHurtboxes(hurt);
                bool inv = f.IsStrikeInvulnerable;
                for (int h = 0; h < n; h++) Add(hurt[h], inv ? InvulnColor : HurtColor);

                var m = f.Move;
                if (f.state == FState.Attack && m != null)
                {
                    for (int h = 0; h < m.hitboxes.Count; h++)
                        if (m.hitboxes[h].ActiveOn(f.moveFrame)) Add(m.hitboxes[h].rect.ToWorld(f.x, f.y, f.facing), HitColor);
                    if (m.IsThrow && m.IsActiveFrame(f.moveFrame))
                    {
                        int reach = m.throwRange;
                        int x0 = f.x, x1 = f.x + f.facing * reach;
                        Add(new WorldBox(Mathf.Min(x0, x1), Mathf.Max(x0, x1), 400, 1400), ThrowColor);
                    }
                }
            }
            for (int i = 0; i < sim.projectiles.Length; i++)
            {
                var p = sim.projectiles[i];
                if (p.active) Add(p.Box, ProjColor);
            }
        }
    }
}

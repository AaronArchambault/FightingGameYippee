using System;
using System.Collections.Generic;
using UnityEngine;
using FightCore;

namespace FightGame
{
    //this owns every pooled visual effect like sparks trails and fireballs
    //drop your own prefabs into the slots to replace the built in looks and anything left empty uses the built in one
    public class EffectsManager : MonoBehaviour
    {
        [Serializable]
        public class SparkSlot
        {
            public SparkKind kind;
            [Tooltip("a prefab with a PooledEffect like SpriteSheetEffect on it")]
            public PooledEffect prefab;
        }

        [Header("Your Effects")]
        [Tooltip("replace the built in spark for any kind of hit")]
        public List<SparkSlot> sparkPrefabs = new List<SparkSlot>();
        [Tooltip("used for dash trails and empty means the built in blur")]
        public PooledEffect afterimagePrefab;
        [Tooltip("used for fireballs that do not have their own prefab on the fighter")]
        public ProjectileView defaultProjectilePrefab;

        [Header("Look")]
        [Tooltip("makes every spark bigger or smaller")]
        public float sparkScale = 1f;
        [Tooltip("the dark screen during a super flash")]
        public Color superDimColor = new Color(0f, 0f, 0.05f, 0.6f);
        public int superDimSortingOrder = 5;
        [Tooltip("how many of each effect get made at the start so the first hit does not hitch")]
        [Min(0)] public int prewarm = 6;

        //each prefab gets its own pool and null means the built in procedural look
        readonly Dictionary<UnityEngine.Object, ObjectPool<PooledEffect>> effectPools = new Dictionary<UnityEngine.Object, ObjectPool<PooledEffect>>();
        readonly Dictionary<SparkKind, ObjectPool<PooledEffect>> builtInSparks = new Dictionary<SparkKind, ObjectPool<PooledEffect>>();
        readonly List<ObjectPool<PooledEffect>> allEffectPools = new List<ObjectPool<PooledEffect>>();
        readonly Dictionary<UnityEngine.Object, ObjectPool<ProjectileView>> projectilePools = new Dictionary<UnityEngine.Object, ObjectPool<ProjectileView>>();
        ObjectPool<ProjectileView> builtInProjectiles;
        ObjectPool<PooledEffect> builtInAfterimages;

        readonly ProjectileView[] slotViews = new ProjectileView[16];
        readonly ObjectPool<ProjectileView>[] slotPools = new ObjectPool<ProjectileView>[16];

        SpriteRenderer dim;
        float dimAlpha;
        bool ready;

        //this lets the manager ask which prefab a fireball should use
        public Func<Projectile, ProjectileView> projectilePrefabFor;

        public void Init()
        {
            if (ready) return;
            ready = true;
            dim = SpriteFactory.Make("SuperDim", transform, SpriteFactory.Square, Color.clear, superDimSortingOrder);
            dim.transform.localScale = new Vector3(200f, 200f, 1f);
            dim.transform.position = new Vector3(0f, 0f, 2f);
        }

        void Awake() { Init(); }

        ObjectPool<PooledEffect> PoolFor(PooledEffect prefab)
        {
            ObjectPool<PooledEffect> pool;
            if (effectPools.TryGetValue(prefab, out pool)) return pool;
            pool = new ObjectPool<PooledEffect>(() => Instantiate(prefab, transform), prewarm, 48);
            effectPools[prefab] = pool;
            allEffectPools.Add(pool);
            return pool;
        }

        ObjectPool<PooledEffect> BuiltInSpark(SparkKind kind)
        {
            ObjectPool<PooledEffect> pool;
            if (builtInSparks.TryGetValue(kind, out pool)) return pool;
            pool = new ObjectPool<PooledEffect>(() =>
            {
                var go = new GameObject("Spark_" + kind);
                go.transform.SetParent(transform, false);
                var sp = go.AddComponent<ProceduralSpark>();
                sp.kind = kind;
                return sp;
            }, kind == SparkKind.Hit ? prewarm * 2 : prewarm / 2, 48);
            builtInSparks[kind] = pool;
            allEffectPools.Add(pool);
            return pool;
        }

        PooledEffect PrefabForKind(SparkKind kind)
        {
            for (int i = 0; i < sparkPrefabs.Count; i++)
                if (sparkPrefabs[i] != null && sparkPrefabs[i].kind == kind && sparkPrefabs[i].prefab != null) return sparkPrefabs[i].prefab;
            return null;
        }

        //this plays a spark and an override prefab wins over everything like a hit effect from one move
        public void Spark(SparkKind kind, Vector3 pos, Color c, float scale, int facing, PooledEffect overridePrefab = null)
        {
            var prefab = overridePrefab != null ? overridePrefab : PrefabForKind(kind);
            var pool = prefab != null ? PoolFor(prefab) : BuiltInSpark(kind);
            pool.Get().Play(pos, c, scale * sparkScale, facing);
        }

        public void Trail(Vector3 feet, Color c, float height, int facing)
        {
            ObjectPool<PooledEffect> pool;
            if (afterimagePrefab != null) pool = PoolFor(afterimagePrefab);
            else
            {
                if (builtInAfterimages == null)
                {
                    builtInAfterimages = new ObjectPool<PooledEffect>(() =>
                    {
                        var go = new GameObject("Afterimage");
                        go.transform.SetParent(transform, false);
                        return go.AddComponent<Afterimage>();
                    }, prewarm, 48);
                    allEffectPools.Add(builtInAfterimages);
                }
                pool = builtInAfterimages;
            }
            pool.Get().Play(feet, c, height, facing);
        }

        ObjectPool<ProjectileView> ProjectilePool(ProjectileView prefab)
        {
            if (prefab == null)
            {
                if (builtInProjectiles == null)
                    builtInProjectiles = new ObjectPool<ProjectileView>(() =>
                    {
                        var go = new GameObject("Projectile");
                        go.transform.SetParent(transform, false);
                        var v = go.AddComponent<ProceduralProjectileView>();
                        v.Init();
                        return v;
                    }, 4, 16);
                return builtInProjectiles;
            }
            ObjectPool<ProjectileView> pool;
            if (projectilePools.TryGetValue(prefab, out pool)) return pool;
            pool = new ObjectPool<ProjectileView>(() =>
            {
                var v = Instantiate(prefab, transform);
                v.Init();
                return v;
            }, 2, 16);
            projectilePools[prefab] = pool;
            return pool;
        }

        public void ClearAll()
        {
            for (int i = 0; i < allEffectPools.Count; i++) allEffectPools[i].ReleaseAll();
            for (int i = 0; i < slotViews.Length; i++)
            {
                if (slotViews[i] != null) slotPools[i].Release(slotViews[i]);
                slotViews[i] = null;
                slotPools[i] = null;
            }
        }

        //this is called every render frame to animate everything that is out
        public void Tick(float dt, MatchSim sim, Color p1Color, Color p2Color)
        {
            for (int p = 0; p < allEffectPools.Count; p++)
            {
                var list = allEffectPools[p].Active;
                for (int i = list.Count - 1; i >= 0; i--)
                    if (!list[i].Tick(dt)) allEffectPools[p].Release(list[i]);
            }

            if (sim != null)
            {
                for (int i = 0; i < sim.projectiles.Length && i < slotViews.Length; i++)
                {
                    var p = sim.projectiles[i];
                    if (p.active)
                    {
                        if (slotViews[i] == null)
                        {
                            var prefab = projectilePrefabFor != null ? projectilePrefabFor(p) : null;
                            if (prefab == null) prefab = defaultProjectilePrefab;
                            slotPools[i] = ProjectilePool(prefab);
                            slotViews[i] = slotPools[i].Get();
                            slotViews[i].Begin(p);
                        }
                        slotViews[i].Sync(p, p.owner == 0 ? p1Color : p2Color, sim.frame);
                    }
                    else if (slotViews[i] != null)
                    {
                        slotPools[i].Release(slotViews[i]);
                        slotViews[i] = null;
                        slotPools[i] = null;
                    }
                }
            }

            //it fades the dark screen in and out for supers
            float target = sim != null && sim.superFreeze > 0 ? superDimColor.a : 0f;
            dimAlpha = Mathf.MoveTowards(dimAlpha, target, dt * 4f);
            dim.color = FightUtil.WithAlpha(superDimColor, dimAlpha);
        }
    }
}

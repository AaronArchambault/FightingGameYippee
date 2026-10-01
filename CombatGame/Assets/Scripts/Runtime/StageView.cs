using System;
using System.Collections.Generic;
using UnityEngine;

namespace FightGame
{
    //this is the stage art with parallax layers
    //put your own background sprites as children and add them to Layers with how much they follow the camera
    //a factor of 0 sticks to the world like the floor and 1 sticks to the camera like a far away sky
    public class StageView : MonoBehaviour
    {
        [Serializable]
        public class ParallaxLayer
        {
            public Transform target;
            [Range(0f, 1f)] public float follow = 0.5f;
            [HideInInspector] public float startX;
        }

        public List<ParallaxLayer> layers = new List<ParallaxLayer>();

        [Tooltip("music for fights on this stage and empty uses the sound bank")]
        public AudioClip music;

        [Tooltip("if there are no layers it builds the placeholder city stage when the game starts")]
        public bool buildPlaceholderIfEmpty = true;

        [Tooltip("only used to draw the walls in the scene view and the real width comes from the rules on the FightGameManager")]
        public float previewHalfWidth = 7.2f;

        void Start()
        {
            if (layers.Count == 0 && transform.childCount == 0 && buildPlaceholderIfEmpty) BuildPlaceholder();
            foreach (var l in layers) if (l.target != null) l.startX = l.target.position.x;
        }

        //this is the parallax where far layers follow the camera more so they look far away
        public void Tick(Vector3 camPos)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                var l = layers[i];
                if (l.target == null) continue;
                var p = l.target.position;
                l.target.position = new Vector3(l.startX + camPos.x * l.follow, p.y, p.z);
            }
        }

        //this shows where the walls are in the scene view so you can line your art up with them
        void OnDrawGizmos()
        {
            float w = previewHalfWidth;
            var mgr = FightGameManager.Instance != null ? FightGameManager.Instance : FightUtil.FindInScene<FightGameManager>();
            if (mgr != null && mgr.rules != null) w = mgr.rules.stageHalfWidth * FightUtil.SimToWorld;
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.8f);
            Gizmos.DrawLine(new Vector3(-w, 0f, 0f), new Vector3(-w, 6f, 0f));
            Gizmos.DrawLine(new Vector3(w, 0f, 0f), new Vector3(w, 6f, 0f));
            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.8f);
            Gizmos.DrawLine(new Vector3(-w, 0f, 0f), new Vector3(w, 0f, 0f));
        }

        //this builds the made up city stage as real child objects
        //the editor scene builder calls this too so you get objects you can move and swap out
        public void BuildPlaceholder()
        {
            //the sky is a few stacked strips so it looks like a gradient without needing a texture
            var sky = new GameObject("Sky").transform;
            sky.SetParent(transform, false);
            var top = new Color(0.1f, 0.08f, 0.25f);
            var bottom = new Color(0.95f, 0.45f, 0.35f);
            for (int i = 0; i < 10; i++)
            {
                var strip = SpriteFactory.Make("SkyStrip", sky, SpriteFactory.Square, Color.Lerp(bottom, top, i / 9f), -100);
                strip.transform.localPosition = new Vector3(0f, -1f + i * 1.3f, 20f);
                strip.transform.localScale = new Vector3(60f, 1.35f, 1f);
            }
            AddLayer(sky, 0.95f);

            var sun = SpriteFactory.Make("Sun", transform, SpriteFactory.SoftCircle, new Color(1f, 0.85f, 0.6f, 0.9f), -95);
            sun.transform.position = new Vector3(3f, 4.2f, 19f);
            sun.transform.localScale = Vector3.one * 5f;
            AddLayer(sun.transform, 0.9f);

            var far = new GameObject("FarCity").transform;
            far.SetParent(transform, false);
            BuildCity(far, 40, 1.2f, 3.2f, new Color(0.25f, 0.15f, 0.35f), -90, 11);
            AddLayer(far, 0.7f);
            var near = new GameObject("NearCity").transform;
            near.SetParent(transform, false);
            BuildCity(near, 30, 0.8f, 2.2f, new Color(0.15f, 0.08f, 0.2f), -80, 29);
            AddLayer(near, 0.4f);

            //this is the floor where the fighters stand
            var ground = new GameObject("Ground").transform;
            ground.SetParent(transform, false);
            var floor = SpriteFactory.Make("Floor", ground, SpriteFactory.Square, new Color(0.18f, 0.14f, 0.18f), -50);
            floor.transform.position = new Vector3(0f, -2f, 5f);
            floor.transform.localScale = new Vector3(40f, 4f, 1f);
            var edge = SpriteFactory.Make("FloorEdge", ground, SpriteFactory.Square, new Color(0.45f, 0.3f, 0.35f), -49);
            edge.transform.position = new Vector3(0f, -0.04f, 5f);
            edge.transform.localScale = new Vector3(40f, 0.08f, 1f);
            for (int i = 0; i < 4; i++)
            {
                var line = SpriteFactory.Make("FloorLine", ground, SpriteFactory.Square, new Color(0.25f, 0.2f, 0.25f), -49);
                line.transform.position = new Vector3(0f, -0.35f - i * 0.45f, 5f);
                line.transform.localScale = new Vector3(40f, 0.04f, 1f);
            }

            //these are the walls at each end so you can see where the corner is
            for (int s = -1; s <= 1; s += 2)
            {
                var pillar = SpriteFactory.Make(s < 0 ? "WallLeft" : "WallRight", ground, SpriteFactory.Square, new Color(0.3f, 0.2f, 0.28f), -40);
                pillar.transform.position = new Vector3(s * (previewHalfWidth + 0.35f), 3f, 4f);
                pillar.transform.localScale = new Vector3(0.7f, 8f, 1f);
            }
        }

        //it uses a fixed seed so the city looks the same every time
        void BuildCity(Transform parent, int count, float minH, float maxH, Color c, int order, int seed)
        {
            var rng = new System.Random(seed);
            float x = -18f;
            for (int i = 0; i < count && x < 18f; i++)
            {
                float w = 0.6f + (float)rng.NextDouble() * 1.2f;
                float h = minH + (float)rng.NextDouble() * (maxH - minH);
                var b = SpriteFactory.Make("Building", parent, SpriteFactory.Square, c, order);
                b.transform.localPosition = new Vector3(x + w * 0.5f, h * 0.5f, 10f);
                b.transform.localScale = new Vector3(w, h, 1f);
                for (int k = 0; k < 3; k++)
                {
                    if (rng.NextDouble() < 0.5) continue;
                    var win = SpriteFactory.Make("Window", b.transform, SpriteFactory.Square, new Color(1f, 0.8f, 0.4f, 0.6f), order + 1);
                    win.transform.localPosition = new Vector3(((float)rng.NextDouble() - 0.5f) * 0.6f, ((float)rng.NextDouble() - 0.5f) * 0.7f, 0f);
                    win.transform.localScale = new Vector3(0.08f / w, 0.08f / h, 1f);
                }
                x += w + (float)rng.NextDouble() * 0.2f;
            }
        }

        void AddLayer(Transform t, float follow)
        {
            layers.Add(new ParallaxLayer { target = t, follow = follow, startX = t.position.x });
        }
    }
}

using System.Collections.Generic;
using System.IO;
using UnityEngine;
using FightCore;

namespace FightGame
{
    //this builds a simple stage out of shapes with a few parallax layers so it has some depth
    //you can replace it with real art by turning off buildPlaceholderStage on the bootstrap
    public class Stage : MonoBehaviour
    {
        class Layer { public Transform t; public float factor; }
        readonly List<Layer> layers = new List<Layer>();

        public static Stage Create()
        {
            var go = new GameObject("Stage");
            var s = go.AddComponent<Stage>();
            s.Build();
            return s;
        }

        void Build()
        {
            //this is the sky made from a tiny gradient texture stretched over the background
            var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false);
            var top = new Color(0.1f, 0.08f, 0.25f);
            var bottom = new Color(0.95f, 0.45f, 0.35f);
            for (int y = 0; y < 64; y++) tex.SetPixel(0, y, Color.Lerp(bottom, top, y / 63f));
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            var skySprite = Sprite.Create(tex, new Rect(0, 0, 1, 64), new Vector2(0.5f, 0.5f), 1f);
            var sky = SpriteFactory.Make("Sky", transform, skySprite, Color.white, -100);
            sky.transform.localScale = new Vector3(60f, 0.2f, 1f);
            sky.transform.position = new Vector3(0f, 5f, 20f);
            AddLayer(sky.transform, 0.95f);

            var sun = SpriteFactory.Make("Sun", transform, SpriteFactory.SoftCircle, new Color(1f, 0.85f, 0.6f, 0.9f), -95);
            sun.transform.position = new Vector3(3f, 4.2f, 19f);
            sun.transform.localScale = Vector3.one * 5f;
            AddLayer(sun.transform, 0.9f);

            //far and near city layers that scroll at different speeds
            var far = new GameObject("FarCity").transform;
            far.SetParent(transform, false);
            BuildCity(far, 40, 1.2f, 3.2f, new Color(0.25f, 0.15f, 0.35f), -90, 11);
            AddLayer(far, 0.7f);
            var near = new GameObject("NearCity").transform;
            near.SetParent(transform, false);
            BuildCity(near, 30, 0.8f, 2.2f, new Color(0.15f, 0.08f, 0.2f), -80, 29);
            AddLayer(near, 0.4f);

            //this is the floor where the fighters stand
            var floor = SpriteFactory.Make("Floor", transform, SpriteFactory.Square, new Color(0.18f, 0.14f, 0.18f), -50);
            floor.transform.position = new Vector3(0f, -2f, 5f);
            floor.transform.localScale = new Vector3(40f, 4f, 1f);
            var edge = SpriteFactory.Make("FloorEdge", transform, SpriteFactory.Square, new Color(0.45f, 0.3f, 0.35f), -49);
            edge.transform.position = new Vector3(0f, -0.04f, 5f);
            edge.transform.localScale = new Vector3(40f, 0.08f, 1f);
            for (int i = 0; i < 4; i++)
            {
                var line = SpriteFactory.Make("FloorLine", transform, SpriteFactory.Square, new Color(0.25f, 0.2f, 0.25f), -49);
                line.transform.position = new Vector3(0f, -0.35f - i * 0.45f, 5f);
                line.transform.localScale = new Vector3(40f, 0.04f, 1f);
            }

            //these are the walls at each end of the stage so you can see where the corner is
            float wall = MatchSim.StageHalf * FightUtil.SimToWorld;
            for (int s = -1; s <= 1; s += 2)
            {
                var pillar = SpriteFactory.Make("Wall", transform, SpriteFactory.Square, new Color(0.3f, 0.2f, 0.28f), -40);
                pillar.transform.position = new Vector3(s * (wall + 0.35f), 3f, 4f);
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
                //a few little windows so it reads as a city and not just blocks
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

        void AddLayer(Transform t, float factor)
        {
            layers.Add(new Layer { t = t, factor = factor });
        }

        //this is the parallax where far layers follow the camera more so they look far away
        public void Tick(Vector3 camPos)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                var l = layers[i];
                var p = l.t.position;
                l.t.position = new Vector3(camPos.x * l.factor + (l.t.name == "Sun" ? 3f : 0f), p.y, p.z);
            }
        }
    }

    //this saves and loads replays as json files
    //since the sim is deterministic a replay is just the fighters plus every input which is tiny
    public static class ReplayStorage
    {
        public static string Folder { get { return Path.Combine(Application.persistentDataPath, "Replays"); } }
        public static string LastPath { get { return Path.Combine(Folder, "last_match.json"); } }

        public static void Save(ReplayData data)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var json = JsonUtility.ToJson(data);
                File.WriteAllText(LastPath, json);
                //it also keeps a copy with the time in the name so you have a history
                var stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
                File.WriteAllText(Path.Combine(Folder, "replay_" + stamp + ".json"), json);
            }
            catch (System.Exception e) { Debug.LogWarning("Could not save replay: " + e.Message); }
        }

        public static ReplayData LoadLast()
        {
            try
            {
                if (!File.Exists(LastPath)) return null;
                return JsonUtility.FromJson<ReplayData>(File.ReadAllText(LastPath));
            }
            catch (System.Exception e) { Debug.LogWarning("Could not load replay: " + e.Message); return null; }
        }
    }
}

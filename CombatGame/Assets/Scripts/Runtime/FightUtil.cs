using UnityEngine;
using FightCore;

namespace FightGame
{
    //this is a bunch of small helpers that a lot of the unity scripts share
    public static class FightUtil
    {
        //the sim uses ints where 1000 is one unity unit so this turns them into real positions
        public const float SimToWorld = 1f / 1000f;

        public static Vector3 ToWorld(int x, int y, float z = 0f)
        {
            return new Vector3(x * SimToWorld, y * SimToWorld, z);
        }

        public static Color RGB(int rgb, float a = 1f)
        {
            return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);
        }

        public static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        //this gets the font that comes with unity so the ui works without importing anything
        static Font font;
        public static Font DefaultFont
        {
            get
            {
                if (font != null) return font;
#if UNITY_2022_2_OR_NEWER
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
                return font;
            }
        }

        //these are the colors for each hit strength so sparks and shakes match what happened
        public static Color LevelColor(HitLevel level)
        {
            switch (level)
            {
                case HitLevel.Light: return new Color(1f, 0.95f, 0.7f);
                case HitLevel.Medium: return new Color(1f, 0.8f, 0.3f);
                case HitLevel.Heavy: return new Color(1f, 0.55f, 0.15f);
                case HitLevel.Special: return new Color(1f, 0.4f, 0.2f);
                default: return new Color(1f, 0.25f, 0.6f);
            }
        }

        public static float LevelScale(HitLevel level)
        {
            switch (level)
            {
                case HitLevel.Light: return 0.8f;
                case HitLevel.Medium: return 1.1f;
                case HitLevel.Heavy: return 1.5f;
                case HitLevel.Special: return 1.4f;
                default: return 2f;
            }
        }
    }

    //this makes simple sprites in code so the game runs with zero art and you can swap in real art later
    public static class SpriteFactory
    {
        static Sprite square, squareTop, circle, softCircle, ring;

        //this is a white square with the pivot in the middle
        public static Sprite Square { get { if (square == null) square = MakeSquare(new Vector2(0.5f, 0.5f)); return square; } }

        //this is a white square with the pivot at the top so it can swing like an arm or a leg
        public static Sprite SquareTop { get { if (squareTop == null) squareTop = MakeSquare(new Vector2(0.5f, 1f)); return squareTop; } }

        public static Sprite Circle { get { if (circle == null) circle = MakeCircle(64, 0f, false); return circle; } }
        public static Sprite SoftCircle { get { if (softCircle == null) softCircle = MakeCircle(64, 1f, false); return softCircle; } }
        public static Sprite Ring { get { if (ring == null) ring = MakeCircle(64, 0f, true); return ring; } }

        static Sprite MakeSquare(Vector2 pivot)
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            //it uses 4 pixels per unit so the sprite is exactly one unit big and scale equals size
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), pivot, 4f);
        }

        static Sprite MakeCircle(int size, float softness, bool ringOnly)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r, dy = y + 0.5f - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                    float a;
                    if (ringOnly) a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.85f) * 9f);
                    else if (softness > 0f) a = Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d);
                    else a = Mathf.Clamp01((1f - d) * r);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            }
            tex.SetPixels32(px);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        //this is a quick way to make a colored sprite object
        public static SpriteRenderer Make(string name, Transform parent, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }
    }
}

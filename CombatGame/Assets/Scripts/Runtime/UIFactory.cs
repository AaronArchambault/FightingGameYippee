using UnityEngine;
using UnityEngine.UI;

namespace FightGame
{
    //this builds ui pieces in code so the whole game works from an empty scene
    public static class UIFactory
    {
        public static Canvas Canvas(string name, int order)
        {
            var go = new GameObject(name);
            return MakeCanvas(go, order);
        }

        //if a ui script is on an object that is not inside a canvas this turns that object into one
        //so you can put the hud or menus on any empty object and it still shows up
        public static void EnsureCanvas(GameObject go, int order)
        {
            if (go.GetComponentInParent<Canvas>() == null) MakeCanvas(go, order);
        }

        //this tells unity the scene changed after building something from a right click menu so it gets saved
        public static void MarkDirty(Component c)
        {
#if UNITY_EDITOR
            if (Application.isPlaying) return;
            UnityEditor.EditorUtility.SetDirty(c);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
#endif
        }

        static Canvas MakeCanvas(GameObject go, int order)
        {
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        //this makes a rect that fills its parent with some padding
        public static RectTransform Fill(string name, Transform parent, float pad = 0f)
        {
            var rt = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
            return rt;
        }

        public static Image Image(string name, Transform parent, Color c)
        {
            var rt = Fill(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        public static Image Box(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color c)
        {
            var rt = Rect(name, parent, anchor, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        public static Text Text(string name, Transform parent, string text, int size, Vector2 anchor, Vector2 pos, Vector2 box, TextAnchor align, Color c, bool outline = true)
        {
            var rt = Rect(name, parent, anchor, anchor, pos, box);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = FightUtil.DefaultFont;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = c;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.fontStyle = FontStyle.Bold;
            if (outline)
            {
                var o = rt.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.85f);
                o.effectDistance = new Vector2(2f, -2f);
            }
            return t;
        }
    }
}

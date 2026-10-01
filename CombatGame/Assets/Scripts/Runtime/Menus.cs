using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FightGame
{
    //this is a button you can also push left and right on to change a value
    //it is for stuff like picking your fighter or the cpu level with a stick or the arrow keys
    public class CyclerButton : Button
    {
        public Action<int> onCycle;

        public override void OnMove(AxisEventData e)
        {
            if (e.moveDir == MoveDirection.Left) { onCycle?.Invoke(-1); e.Use(); return; }
            if (e.moveDir == MoveDirection.Right) { onCycle?.Invoke(1); e.Use(); return; }
            base.OnMove(e);
        }
    }

    //this builds every menu in code which is the title screen the select screen pause and results
    public class MenuSystem : MonoBehaviour
    {
        Canvas canvas;
        readonly Dictionary<string, RectTransform> panels = new Dictionary<string, RectTransform>();
        readonly Dictionary<string, Selectable> firstSelected = new Dictionary<string, Selectable>();
        public string Current { get; private set; }

        static readonly Color ButtonColor = new Color(0.12f, 0.12f, 0.18f, 0.95f);
        static readonly Color ButtonHighlight = new Color(0.85f, 0.25f, 0.2f, 1f);

        public static MenuSystem Create()
        {
            var c = UIFactory.Canvas("Menus", 20);
            var m = c.gameObject.AddComponent<MenuSystem>();
            m.canvas = c;
            return m;
        }

        public RectTransform Panel(string id, bool dimBackground)
        {
            var rt = UIFactory.Fill("Panel_" + id, canvas.transform);
            if (dimBackground)
            {
                var img = rt.gameObject.AddComponent<Image>();
                img.color = new Color(0f, 0f, 0f, 0.7f);
            }
            rt.gameObject.SetActive(false);
            panels[id] = rt;
            return rt;
        }

        public Text Title(RectTransform panel, string text, float y, int size = 96)
        {
            var t = UIFactory.Text("Title", panel, text, size, new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(1600f, 140f), TextAnchor.MiddleCenter, Color.white);
            t.GetComponent<Outline>().effectDistance = new Vector2(4f, -4f);
            return t;
        }

        public Text Label(RectTransform panel, string text, float y, int size, Color c)
        {
            return UIFactory.Text("Label", panel, text, size, new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(1600f, size + 20f), TextAnchor.MiddleCenter, c);
        }

        //this makes a normal menu button
        public Button AddButton(string panelId, string text, float y, Action onClick)
        {
            var b = MakeButton<Button>(panelId, text, y, 520f);
            b.onClick.AddListener(() => { AudioManager.Instance?.Play(SfxId.UIConfirm); onClick(); });
            return b;
        }

        //this makes a left and right value picker and it returns the text so you can update it
        public Text AddCycler(string panelId, float y, Action<int> onCycle, out CyclerButton button)
        {
            var b = MakeButton<CyclerButton>(panelId, "", y, 760f);
            b.onCycle = d => { AudioManager.Instance?.Play(SfxId.UIMove); onCycle(d); };
            b.onClick.AddListener(() => { AudioManager.Instance?.Play(SfxId.UIMove); onCycle(1); });
            button = b;
            return b.GetComponentInChildren<Text>();
        }

        T MakeButton<T>(string panelId, string text, float y, float width) where T : Button
        {
            var panel = panels[panelId];
            var rt = UIFactory.Rect("Button", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(width, 72f));
            var img = rt.gameObject.AddComponent<Image>();
            img.color = Color.white;
            var b = rt.gameObject.AddComponent<T>();
            var cb = b.colors;
            cb.normalColor = ButtonColor;
            cb.highlightedColor = ButtonHighlight;
            cb.selectedColor = ButtonHighlight;
            cb.pressedColor = Color.white;
            cb.fadeDuration = 0.05f;
            b.colors = cb;
            b.targetGraphic = img;
            UIFactory.Text("Text", rt, text, 38, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 72f), TextAnchor.MiddleCenter, Color.white);
            if (!firstSelected.ContainsKey(panelId)) firstSelected[panelId] = b;
            //it uses automatic navigation going up and down so a gamepad can move through the list
            var nav = b.navigation;
            nav.mode = Navigation.Mode.Vertical;
            b.navigation = nav;
            return b;
        }

        public void Show(string id)
        {
            foreach (var kv in panels) kv.Value.gameObject.SetActive(kv.Key == id);
            Current = id;
            if (id != null && firstSelected.ContainsKey(id) && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(firstSelected[id].gameObject);
            }
        }

        public void Hide() { Show(null); }

        //if you click off a button with the mouse and then use a pad this puts the selection back
        void Update()
        {
            if (Current == null || EventSystem.current == null) return;
            if (EventSystem.current.currentSelectedGameObject == null && firstSelected.ContainsKey(Current))
                EventSystem.current.SetSelectedGameObject(firstSelected[Current].gameObject);
        }
    }
}

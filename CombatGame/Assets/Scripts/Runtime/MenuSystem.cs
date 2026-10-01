using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FightGame
{
    //this runs every menu screen
    //it finds all the MenuPanel MenuButton MenuCycler MenuText and MenuImage pieces under it and hooks them up
    //so you can design your own menus in the scene and as long as the pieces are tagged it all just works
    public class MenuSystem : MonoBehaviour
    {
        [Tooltip("plays the ui sounds from the AudioManager")]
        public bool playSounds = true;

        public event Action<MenuAction> ActionPressed;
        public event Action<CyclerTarget, int> Cycled;

        readonly Dictionary<MenuPanelId, MenuPanel> panels = new Dictionary<MenuPanelId, MenuPanel>();
        readonly Dictionary<CyclerTarget, MenuCycler> cyclers = new Dictionary<CyclerTarget, MenuCycler>();
        readonly Dictionary<MenuTextId, Text> texts = new Dictionary<MenuTextId, Text>();
        readonly Dictionary<MenuImageId, Image> images = new Dictionary<MenuImageId, Image>();
        bool wired;
        MenuPanel current;

        public bool IsOpen { get { return current != null; } }
        public bool IsShowing(MenuPanelId id) { return current != null && current.id == id; }

        public static MenuSystem Create()
        {
            var c = UIFactory.Canvas("Menus", 20);
            var m = c.gameObject.AddComponent<MenuSystem>();
            m.BuildDefault();
            return m;
        }

        //this finds and hooks up every menu piece and it is safe to call more than once
        public void Wire()
        {
            if (wired) return;
            wired = true;
            foreach (var p in GetComponentsInChildren<MenuPanel>(true))
            {
                panels[p.id] = p;
                p.gameObject.SetActive(false);
            }
            foreach (var b in GetComponentsInChildren<MenuButton>(true))
            {
                var action = b.action;
                var btn = b.GetComponent<Button>();
                btn.onClick.AddListener(() =>
                {
                    if (playSounds && AudioManager.Instance != null) AudioManager.Instance.Play(action == MenuAction.BackToMain || action == MenuAction.BackToPause ? SfxId.UIBack : SfxId.UIConfirm);
                    if (ActionPressed != null) ActionPressed(action);
                });
            }
            foreach (var c in GetComponentsInChildren<MenuCycler>(true))
            {
                cyclers[c.target] = c;
                var target = c.target;
                var btn = c.GetComponent<CyclerButton>();
                Action<int> cycle = d =>
                {
                    if (playSounds && AudioManager.Instance != null) AudioManager.Instance.Play(SfxId.UIMove);
                    if (Cycled != null) Cycled(target, d);
                };
                btn.onCycle = cycle;
                btn.onClick.AddListener(() => cycle(1));
            }
            foreach (var t in GetComponentsInChildren<MenuText>(true)) texts[t.id] = t.GetComponent<Text>();
            foreach (var im in GetComponentsInChildren<MenuImage>(true)) images[im.id] = im.GetComponent<Image>();
        }

        public bool HasPanel(MenuPanelId id) { Wire(); return panels.ContainsKey(id); }

        public void Show(MenuPanelId id)
        {
            Wire();
            current = null;
            foreach (var kv in panels)
            {
                bool on = kv.Key == id;
                kv.Value.gameObject.SetActive(on);
                if (on) current = kv.Value;
            }
            Select();
        }

        public void Hide()
        {
            Wire();
            foreach (var kv in panels) kv.Value.gameObject.SetActive(false);
            current = null;
        }

        void Select()
        {
            if (current == null || EventSystem.current == null) return;
            var first = current.firstSelected != null ? current.firstSelected : current.GetComponentInChildren<Selectable>();
            EventSystem.current.SetSelectedGameObject(null);
            if (first != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        public void SetText(MenuTextId id, string value)
        {
            Wire();
            Text t;
            if (texts.TryGetValue(id, out t) && t != null) t.text = value;
        }

        public void SetCycler(CyclerTarget target, string value)
        {
            Wire();
            MenuCycler c;
            if (cyclers.TryGetValue(target, out c) && c.label != null) c.label.text = value;
        }

        public void SetCyclerVisible(CyclerTarget target, bool visible)
        {
            Wire();
            MenuCycler c;
            if (cyclers.TryGetValue(target, out c)) c.gameObject.SetActive(visible);
        }

        public void SetImage(MenuImageId id, Sprite s)
        {
            Wire();
            Image im;
            if (!images.TryGetValue(id, out im) || im == null) return;
            im.sprite = s;
            im.enabled = s != null;
        }

        //if you click off a button with the mouse and then use a pad this puts the selection back
        void Update()
        {
            if (current == null || EventSystem.current == null) return;
            if (EventSystem.current.currentSelectedGameObject == null) Select();
        }

        //true when there are no menu screens under this object yet
        public bool IsEmpty { get { return GetComponentsInChildren<MenuPanel>(true).Length == 0; } }

        //right click the component header in the inspector and pick this to build the default menus in your scene
        [ContextMenu("Build Default Layout")]
        void BuildDefaultFromMenu()
        {
            if (!IsEmpty) { Debug.LogWarning("Fighting Game: there are already menu panels here so nothing was built"); return; }
            UIFactory.EnsureCanvas(gameObject, 20);
            BuildDefault();
            UIFactory.MarkDirty(this);
        }

        //this is the default menu layout built as real objects
        //the editor scene builder calls this so you get menus you can restyle and move around in the scene
        public void BuildDefault()
        {
            var main = Panel(MenuPanelId.Main, false);
            Title(main, "FIGHT CORE", 330, 130);
            Label(main, "a deterministic 2D fighting game", 240, 30, new Color(1f, 0.85f, 0.5f));
            Btn(main, "VERSUS CPU", 120, MenuAction.VersusCPU);
            Btn(main, "VERSUS PLAYER", 35, MenuAction.VersusPlayer);
            Btn(main, "TRAINING", -50, MenuAction.Training);
            Btn(main, "CPU VS CPU", -135, MenuAction.CPUvsCPU);
            Btn(main, "WATCH LAST REPLAY", -220, MenuAction.WatchReplay);
            Btn(main, "QUIT", -305, MenuAction.Quit);
            Label(main, "", -380, 30, new Color(1f, 0.5f, 0.4f)).gameObject.AddComponent<MenuText>().id = MenuTextId.MainNote;
            Label(main, "P1  WASD  U I O punches  J K L kicks        P2  Arrows  Num 4 5 6 punches  Num 1 2 3 kicks        Pads work too", -440, 24, new Color(1f, 1f, 1f, 0.75f));

            var sel = Panel(MenuPanelId.Select, true);
            Title(sel, "", 360, 80).gameObject.AddComponent<MenuText>().id = MenuTextId.ModeTitle;
            Portrait(sel, MenuImageId.P1Portrait, -620f);
            Portrait(sel, MenuImageId.P2Portrait, 620f);
            Cycler(sel, 170, CyclerTarget.P1Fighter);
            Cycler(sel, 80, CyclerTarget.P2Fighter);
            Cycler(sel, -10, CyclerTarget.CpuLevel);
            Btn(sel, "FIGHT", -130, MenuAction.StartFight);
            Btn(sel, "BACK", -215, MenuAction.BackToMain);
            Label(sel, "left and right to change    confirm to cycle", -320, 26, new Color(1f, 1f, 1f, 0.6f));

            var pause = Panel(MenuPanelId.Pause, true);
            Title(pause, "PAUSED", 300, 96);
            Btn(pause, "RESUME", 120, MenuAction.Resume);
            Btn(pause, "RESTART", 35, MenuAction.Restart);
            Btn(pause, "MOVE LIST", -50, MenuAction.MoveList);
            Btn(pause, "CHARACTER SELECT", -135, MenuAction.CharacterSelect);
            Btn(pause, "MAIN MENU", -220, MenuAction.MainMenu);

            var moves = Panel(MenuPanelId.MoveList, true);
            Btn(moves, "BACK", -420, MenuAction.BackToPause);
            var ml = UIFactory.Text("MoveList", moves.transform, "", 30, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1300f, 760f), TextAnchor.UpperLeft, Color.white);
            ml.fontStyle = FontStyle.Normal;
            ml.supportRichText = true;
            ml.gameObject.AddComponent<MenuText>().id = MenuTextId.MoveList;

            var res = Panel(MenuPanelId.Results, true);
            Title(res, "", 300, 110).gameObject.AddComponent<MenuText>().id = MenuTextId.ResultTitle;
            Label(res, "", 200, 36, new Color(1f, 0.85f, 0.4f)).gameObject.AddComponent<MenuText>().id = MenuTextId.ResultSub;
            Btn(res, "REMATCH", 60, MenuAction.Restart);
            Btn(res, "CHARACTER SELECT", -25, MenuAction.CharacterSelect);
            Btn(res, "MAIN MENU", -110, MenuAction.MainMenu);

            wired = false;
        }

        static readonly Color ButtonColor = new Color(0.12f, 0.12f, 0.18f, 0.95f);
        static readonly Color ButtonHighlight = new Color(0.85f, 0.25f, 0.2f, 1f);

        MenuPanel Panel(MenuPanelId id, bool dim)
        {
            var rt = UIFactory.Fill("Panel_" + id, transform);
            if (dim) rt.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);
            var p = rt.gameObject.AddComponent<MenuPanel>();
            p.id = id;
            rt.gameObject.SetActive(false);
            return p;
        }

        Text Title(MenuPanel panel, string text, float y, int size)
        {
            var t = UIFactory.Text("Title", panel.transform, text, size, new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(1600f, 140f), TextAnchor.MiddleCenter, Color.white);
            t.GetComponent<Outline>().effectDistance = new Vector2(4f, -4f);
            return t;
        }

        Text Label(MenuPanel panel, string text, float y, int size, Color c)
        {
            return UIFactory.Text("Label", panel.transform, text, size, new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(1600f, size + 20f), TextAnchor.MiddleCenter, c);
        }

        void Portrait(MenuPanel panel, MenuImageId id, float x)
        {
            var img = UIFactory.Box(id.ToString(), panel.transform, new Vector2(0.5f, 0.5f), new Vector2(x, 100f), new Vector2(360f, 360f), Color.white);
            img.preserveAspect = true;
            img.enabled = false;
            img.gameObject.AddComponent<MenuImage>().id = id;
        }

        T MakeButton<T>(MenuPanel panel, string text, float y, float width) where T : Button
        {
            var rt = UIFactory.Rect(text.Length > 0 ? text : "Cycler", panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(width, 72f));
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
            var nav = b.navigation;
            nav.mode = Navigation.Mode.Vertical;
            b.navigation = nav;
            if (panel.firstSelected == null) panel.firstSelected = b;
            return b;
        }

        void Btn(MenuPanel panel, string text, float y, MenuAction action)
        {
            MakeButton<Button>(panel, text, y, 520f).gameObject.AddComponent<MenuButton>().action = action;
        }

        void Cycler(MenuPanel panel, float y, CyclerTarget target)
        {
            var b = MakeButton<CyclerButton>(panel, "", y, 760f);
            b.name = "Cycler_" + target;
            var c = b.gameObject.AddComponent<MenuCycler>();
            c.target = target;
            c.label = b.GetComponentInChildren<Text>();
        }
    }
}

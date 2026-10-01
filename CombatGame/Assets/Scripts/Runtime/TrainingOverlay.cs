using System.Text;
using UnityEngine;
using UnityEngine.UI;
using FightCore;

namespace FightGame
{
    //this is all the training mode info on screen
    //it has the frame data readout the input history and the dummy settings
    //each panel is a normal ui object so you can move and restyle them in the scene
    public class TrainingOverlay : MonoBehaviour
    {
        public GameObject frameDataPanel;
        public Text frameDataText;
        public GameObject inputPanel;
        public Text inputText;
        public GameObject dummyPanel;
        public Text dummyText;
        public Text helpText;

        [Header("Colors")]
        public string plusColor = "#6CFF6C";
        public string minusColor = "#FF6C6C";
        [Tooltip("how many inputs the history keeps")]
        [Range(5, 40)] public int historyLength = 20;

        readonly StringBuilder sb = new StringBuilder(512);

        struct InputEntry { public int numpad; public Btn buttons; public int frames; }
        InputEntry[] history;
        int historyCount;
        bool inputsDirty = true;
        int lastFrameDataVersion = -1;
        int lastDummyKey = -1;
        Canvas canvas;

        [HideInInspector] public bool ShowFrameData = true;
        [HideInInspector] public bool ShowInputs = true;

        void Awake()
        {
            canvas = GetComponentInParent<Canvas>();
            history = new InputEntry[historyLength];
        }

        public static TrainingOverlay Create()
        {
            var c = UIFactory.Canvas("TrainingOverlay", 11);
            var t = c.gameObject.AddComponent<TrainingOverlay>();
            t.BuildDefault();
            return t;
        }

        public void SetHelp(string text) { if (helpText != null) helpText.text = text; }

        public void SetVisible(bool v)
        {
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.gameObject == gameObject) canvas.enabled = v;
            else gameObject.SetActive(v);
        }

        //this is called once per sim frame with what player one pressed
        public void PushInput(FrameInput input, bool facingRight)
        {
            if (history == null || history.Length != historyLength) { history = new InputEntry[historyLength]; historyCount = 0; }
            int n = input.Numpad(facingRight);
            Btn b = input.Buttons;
            if (historyCount > 0 && history[0].numpad == n && history[0].buttons == b)
            {
                if (history[0].frames < 99) { history[0].frames++; inputsDirty = true; }
                return;
            }
            for (int i = history.Length - 1; i > 0; i--) history[i] = history[i - 1];
            history[0] = new InputEntry { numpad = n, buttons = b, frames = 1 };
            if (historyCount < history.Length) historyCount++;
            inputsDirty = true;
        }

        public void ClearInputs() { historyCount = 0; inputsDirty = true; lastDummyKey = -1; lastFrameDataVersion = -1; }

        public void Tick(FrameDataTracker tracker, int trackerVersion, DummyController d, AIDifficulty cpuLevel, bool paused)
        {
            if (frameDataPanel != null) frameDataPanel.SetActive(ShowFrameData);
            if (inputPanel != null) inputPanel.SetActive(ShowInputs);

            if (ShowInputs && inputsDirty && inputText != null)
            {
                inputsDirty = false;
                sb.Length = 0;
                for (int i = 0; i < historyCount; i++)
                {
                    var e = history[i];
                    sb.Append(e.frames.ToString().PadLeft(2)).Append("  ").Append(e.numpad);
                    AppendButtons(e.buttons);
                    sb.Append('\n');
                }
                inputText.text = sb.ToString();
            }

            if (ShowFrameData && trackerVersion != lastFrameDataVersion && frameDataText != null)
            {
                lastFrameDataVersion = trackerVersion;
                sb.Length = 0;
                sb.Append("<b>").Append(tracker.moveName).Append("</b>\n");
                sb.Append("Startup ").Append(tracker.startup).Append("   Active ").Append(tracker.active).Append("   Recovery ").Append(tracker.recovery).Append('\n');
                sb.Append("Damage ").Append(tracker.damage).Append('\n');
                if (tracker.hasAdvantage)
                {
                    string col = tracker.advantage > 0 ? plusColor : (tracker.advantage < 0 ? minusColor : "#FFFFFF");
                    sb.Append(tracker.lastWasBlock ? "On Block " : "On Hit ");
                    sb.Append("<color=").Append(col).Append('>');
                    if (tracker.advantage > 0) sb.Append('+');
                    sb.Append(tracker.advantage).Append("</color>\n");
                }
                else sb.Append("On Hit or Block  ...\n");
                sb.Append("Combo ").Append(tracker.comboHits).Append(" hits  ").Append(tracker.comboDamage).Append(" dmg");
                frameDataText.supportRichText = true;
                frameDataText.text = sb.ToString();
            }

            if (dummyText == null) return;
            //it only rebuilds the dummy text when something about the dummy changed
            int key = (int)d.stance * 7 + (int)d.block * 131 + (d.wakeupReversal ? 1 : 0) * 1031 + (d.isRecording ? 1 : 0) * 4099 + d.recording.Count * 8209 + (int)cpuLevel * 31 + (paused ? 1 : 0) * 65537;
            if (key == lastDummyKey) return;
            lastDummyKey = key;
            sb.Length = 0;
            sb.Append("<b>DUMMY</b>\n");
            sb.Append("Stance  ").Append(d.stance).Append('\n');
            sb.Append("Block   ").Append(d.block).Append('\n');
            sb.Append("Wakeup DP  ").Append(d.wakeupReversal ? "On" : "Off").Append('\n');
            if (d.isRecording) sb.Append("<color=#FF5050>RECORDING  ").Append(d.recording.Count).Append("</color>\n");
            else sb.Append("Recording  ").Append(d.recording.Count).Append(" frames\n");
            sb.Append("CPU Level  ").Append(cpuLevel);
            if (paused) sb.Append("\n<color=#FFD040>PAUSED</color>");
            dummyText.supportRichText = true;
            dummyText.text = sb.ToString();
        }

        void AppendButtons(Btn b)
        {
            if ((b & Btn.LP) != 0) sb.Append(" LP");
            if ((b & Btn.MP) != 0) sb.Append(" MP");
            if ((b & Btn.HP) != 0) sb.Append(" HP");
            if ((b & Btn.LK) != 0) sb.Append(" LK");
            if ((b & Btn.MK) != 0) sb.Append(" MK");
            if ((b & Btn.HK) != 0) sb.Append(" HK");
        }

        public bool IsEmpty
        {
            get { return frameDataText == null && inputText == null && dummyText == null && helpText == null; }
        }

        //right click the component header in the inspector and pick this to build the default panels in your scene
        [ContextMenu("Build Default Layout")]
        void BuildDefaultFromMenu()
        {
            if (!IsEmpty) { Debug.LogWarning("Fighting Game: this overlay already has pieces hooked up so nothing was built"); return; }
            UIFactory.EnsureCanvas(gameObject, 11);
            BuildDefault();
            UIFactory.MarkDirty(this);
        }

        //this builds the default panels as real objects and fills in the fields
        public void BuildDefault()
        {
            var root = transform;
            var fd = UIFactory.Box("FrameDataPanel", root, new Vector2(0f, 1f), new Vector2(250f, -270f), new Vector2(460f, 250f), new Color(0f, 0f, 0f, 0.6f));
            frameDataPanel = fd.gameObject;
            frameDataText = UIFactory.Text("FrameData", fd.transform, "", 26, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(430f, 230f), TextAnchor.UpperLeft, Color.white, false);

            var ip = UIFactory.Box("InputPanel", root, new Vector2(0f, 0.5f), new Vector2(110f, -150f), new Vector2(190f, 560f), new Color(0f, 0f, 0f, 0.5f));
            inputPanel = ip.gameObject;
            inputText = UIFactory.Text("Inputs", ip.transform, "", 24, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170f, 540f), TextAnchor.UpperLeft, Color.white, false);
            inputText.fontStyle = FontStyle.Normal;

            var dp = UIFactory.Box("DummyPanel", root, new Vector2(1f, 1f), new Vector2(-250f, -270f), new Vector2(460f, 250f), new Color(0f, 0f, 0f, 0.6f));
            dummyPanel = dp.gameObject;
            dummyText = UIFactory.Text("Dummy", dp.transform, "", 26, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(430f, 230f), TextAnchor.UpperLeft, Color.white, false);

            helpText = UIFactory.Text("Help", root, "", 22, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(1800f, 40f), TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.8f));
        }
    }
}

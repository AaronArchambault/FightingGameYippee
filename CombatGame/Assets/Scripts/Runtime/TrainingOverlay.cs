using System.Text;
using UnityEngine;
using UnityEngine.UI;
using FightCore;

namespace FightGame
{
    //this is all the training mode info on screen
    //it has the frame data readout the input history and the dummy settings
    public class TrainingOverlay : MonoBehaviour
    {
        Canvas canvas;
        Text frameData, inputs, dummy, help;
        GameObject frameDataGo, inputsGo;
        readonly StringBuilder sb = new StringBuilder(512);

        //this is the input history where each line is one input and how many frames it was held
        struct InputEntry { public int numpad; public Btn buttons; public int frames; }
        const int HistoryLen = 20;
        readonly InputEntry[] history = new InputEntry[HistoryLen];
        int historyCount;
        bool inputsDirty = true;
        int lastFrameDataVersion = -1;
        int lastDummyKey = -1;

        public bool ShowFrameData = true;
        public bool ShowInputs = true;

        public static TrainingOverlay Create()
        {
            var c = UIFactory.Canvas("TrainingOverlay", 11);
            var t = c.gameObject.AddComponent<TrainingOverlay>();
            t.canvas = c;
            t.Build();
            return t;
        }

        void Build()
        {
            var root = canvas.transform;
            var fdBack = UIFactory.Box("FrameDataBack", root, new Vector2(0f, 1f), new Vector2(250f, -270f), new Vector2(460f, 250f), new Color(0f, 0f, 0f, 0.6f));
            frameDataGo = fdBack.gameObject;
            frameData = UIFactory.Text("FrameData", fdBack.transform, "", 26, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(430f, 230f), TextAnchor.UpperLeft, Color.white, false);

            var inBack = UIFactory.Box("InputBack", root, new Vector2(0f, 0.5f), new Vector2(110f, -150f), new Vector2(190f, 560f), new Color(0f, 0f, 0f, 0.5f));
            inputsGo = inBack.gameObject;
            inputs = UIFactory.Text("Inputs", inBack.transform, "", 24, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170f, 540f), TextAnchor.UpperLeft, Color.white, false);
            inputs.fontStyle = FontStyle.Normal;

            var dBack = UIFactory.Box("DummyBack", root, new Vector2(1f, 1f), new Vector2(-250f, -270f), new Vector2(460f, 250f), new Color(0f, 0f, 0f, 0.6f));
            dummy = UIFactory.Text("Dummy", dBack.transform, "", 26, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(430f, 230f), TextAnchor.UpperLeft, Color.white, false);

            help = UIFactory.Text("Help", root, "", 22, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(1800f, 40f), TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.8f));
            help.text = "F1 Boxes   F2 Frame Data   F3 Inputs   F4 Dummy Stance   F5 Dummy Block   F6 Record   F7 Playback   F8 Wakeup DP   F9 Reset (hold left or right for corner)   F10 Pause   F11 Step   Tab CPU Level";
        }

        public void SetVisible(bool v) { canvas.enabled = v; }

        //this is called once per sim frame with what player one pressed
        public void PushInput(FrameInput input, bool facingRight)
        {
            int n = input.Numpad(facingRight);
            Btn b = input.Buttons;
            if (historyCount > 0 && history[0].numpad == n && history[0].buttons == b)
            {
                if (history[0].frames < 99) { history[0].frames++; inputsDirty = true; }
                return;
            }
            for (int i = HistoryLen - 1; i > 0; i--) history[i] = history[i - 1];
            history[0] = new InputEntry { numpad = n, buttons = b, frames = 1 };
            if (historyCount < HistoryLen) historyCount++;
            inputsDirty = true;
        }

        public void ClearInputs() { historyCount = 0; inputsDirty = true; }

        public void Tick(FrameDataTracker tracker, int trackerVersion, DummyController d, AIDifficulty cpuLevel, bool paused)
        {
            frameDataGo.SetActive(ShowFrameData);
            inputsGo.SetActive(ShowInputs);

            if (ShowInputs && inputsDirty)
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
                inputs.text = sb.ToString();
            }

            if (ShowFrameData && trackerVersion != lastFrameDataVersion)
            {
                lastFrameDataVersion = trackerVersion;
                sb.Length = 0;
                sb.Append("<b>").Append(tracker.moveName).Append("</b>\n");
                sb.Append("Startup ").Append(tracker.startup).Append("   Active ").Append(tracker.active).Append("   Recovery ").Append(tracker.recovery).Append('\n');
                sb.Append("Damage ").Append(tracker.damage).Append('\n');
                if (tracker.hasAdvantage)
                {
                    string col = tracker.advantage > 0 ? "#6CFF6C" : (tracker.advantage < 0 ? "#FF6C6C" : "#FFFFFF");
                    sb.Append(tracker.lastWasBlock ? "On Block " : "On Hit ");
                    sb.Append("<color=").Append(col).Append('>');
                    if (tracker.advantage > 0) sb.Append('+');
                    sb.Append(tracker.advantage).Append("</color>\n");
                }
                else sb.Append("On Hit or Block  ...\n");
                sb.Append("Combo ").Append(tracker.comboHits).Append(" hits  ").Append(tracker.comboDamage).Append(" dmg");
                frameData.supportRichText = true;
                frameData.text = sb.ToString();
            }

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
            dummy.supportRichText = true;
            dummy.text = sb.ToString();
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
    }
}

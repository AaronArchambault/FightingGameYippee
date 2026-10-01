using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using FightCore;
//unity has its own Motion class for animation so this says we mean the fight one with the special move inputs
using Motion = FightCore.Motion;

namespace FightGame
{
    public enum GameMode { VsCPU, Versus, CPUvsCPU, Training, Replay }

    //this is the main loop of the game
    //it runs the sim at exactly 60 frames a second no matter what the screen refresh rate is
    //then it draws whatever the sim says and turns sim events into sparks and sounds and shakes
    //the sim never touches unity and unity never changes the sim except through inputs which is what keeps it deterministic
    public class GameRunner : MonoBehaviour
    {
        public const float StepSeconds = 1f / 60f;

        //these get set up by the bootstrap
        public CameraRig camRig;
        public EffectsManager fx;
        public HitboxDebugView boxes;
        public FightHUD hud;
        public TrainingOverlay trainingUI;
        public MenuSystem menus;
        public Stage stage;
        public AudioManager audioMgr;
        public bool attractMode = true;

        readonly PlayerInputSource[] inputs = new PlayerInputSource[2];
        List<RosterEntry> roster;

        //these are the choices from the select screen
        GameMode mode = GameMode.VsCPU;
        int p1Pick, p2Pick = 1;
        AIDifficulty cpuLevel = AIDifficulty.Normal;
        AIDifficulty cpu2Level = AIDifficulty.Hard;

        //this is everything about the match that is running right now
        MatchSim sim;
        FighterView[] views = new FighterView[2];
        readonly AIBrain[] ai = new AIBrain[2];
        FighterDef[] defs = new FighterDef[2];
        readonly DummyController dummy = new DummyController();
        FrameDataTracker tracker = new FrameDataTracker();
        int trackerVersion;
        ReplayData recording;
        ReplayData playback;
        int playbackIndex;
        bool isAttract;
        GameMode runningMode;
        Color[] accent = new Color[2];

        double accumulator;
        bool paused;
        bool trainingFrozen;
        int slowmoTicks;
        float matchOverTimer = -1f;
        bool resultsShown;

        Text selP1, selP2, selCpu, selMode, resultTitle, resultSub, moveListText, mainNote;
        CyclerButton cpuCycler;

        public MatchSim Sim { get { return sim; } }

        public void Init()
        {
            roster = RosterLoader.Load();
            inputs[0] = new PlayerInputSource(0);
            inputs[1] = new PlayerInputSource(1);
            //when you plug in or pull out a controller it hands out the pads again
            InputSystem.onDeviceChange += OnDeviceChange;
            BuildMenus();
            ShowMainMenu();
        }

        void OnDestroy()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            for (int i = 0; i < 2; i++) if (inputs[i] != null) inputs[i].Dispose();
        }

        void OnDeviceChange(InputDevice d, InputDeviceChange change)
        {
            if (change == InputDeviceChange.Added || change == InputDeviceChange.Removed || change == InputDeviceChange.Reconnected || change == InputDeviceChange.Disconnected)
                for (int i = 0; i < 2; i++) inputs[i]?.AssignDevices();
        }

        //this is the menu part

        void BuildMenus()
        {
            var main = menus.Panel("main", false);
            menus.Title(main, "FIGHT CORE", 330, 130);
            menus.Label(main, "a deterministic 2D fighting game", 240, 30, new Color(1f, 0.85f, 0.5f));
            menus.AddButton("main", "VERSUS CPU", 120, () => OpenSelect(GameMode.VsCPU));
            menus.AddButton("main", "VERSUS PLAYER", 35, () => OpenSelect(GameMode.Versus));
            menus.AddButton("main", "TRAINING", -50, () => OpenSelect(GameMode.Training));
            menus.AddButton("main", "CPU VS CPU", -135, () => OpenSelect(GameMode.CPUvsCPU));
            menus.AddButton("main", "WATCH LAST REPLAY", -220, WatchLastReplay);
            menus.AddButton("main", "QUIT", -305, Quit);
            mainNote = menus.Label(main, "", -380, 30, new Color(1f, 0.5f, 0.4f));
            menus.Label(main, "P1  WASD  U I O punches  J K L kicks        P2  Arrows  Num 4 5 6 punches  Num 1 2 3 kicks        Pads work too", -440, 24, new Color(1f, 1f, 1f, 0.75f));

            var sel = menus.Panel("select", true);
            selMode = menus.Title(sel, "", 360, 80);
            CyclerButton dummyBtn;
            selP1 = menus.AddCycler("select", 170, d => { p1Pick = Wrap(p1Pick + d, roster.Count); RefreshSelect(); }, out dummyBtn);
            selP2 = menus.AddCycler("select", 80, d => { p2Pick = Wrap(p2Pick + d, roster.Count); RefreshSelect(); }, out dummyBtn);
            selCpu = menus.AddCycler("select", -10, d => { cpuLevel = (AIDifficulty)Wrap((int)cpuLevel + d, 4); RefreshSelect(); }, out cpuCycler);
            menus.AddButton("select", "FIGHT", -130, () => StartMatch(mode, null));
            menus.AddButton("select", "BACK", -215, ShowMainMenu);
            menus.Label(sel, "left and right to change    confirm to cycle", -320, 26, new Color(1f, 1f, 1f, 0.6f));

            var pause = menus.Panel("pause", true);
            menus.Title(pause, "PAUSED", 300);
            menus.AddButton("pause", "RESUME", 120, Resume);
            menus.AddButton("pause", "RESTART", 35, () => StartMatch(runningMode, runningMode == GameMode.Replay ? playback : null));
            menus.AddButton("pause", "MOVE LIST", -50, ShowMoveList);
            menus.AddButton("pause", "CHARACTER SELECT", -135, () => { EndMatch(); OpenSelect(mode); });
            menus.AddButton("pause", "MAIN MENU", -220, () => { EndMatch(); ShowMainMenu(); });

            var moves = menus.Panel("moves", true);
            menus.AddButton("moves", "BACK", -420, () => menus.Show("pause"));
            moveListText = UIFactory.Text("MoveList", moves, "", 30, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1300f, 760f), TextAnchor.UpperLeft, Color.white);
            moveListText.fontStyle = FontStyle.Normal;

            var results = menus.Panel("results", true);
            resultTitle = menus.Title(results, "", 300, 110);
            resultSub = menus.Label(results, "", 200, 36, new Color(1f, 0.85f, 0.4f));
            menus.AddButton("results", "REMATCH", 60, () => StartMatch(runningMode, runningMode == GameMode.Replay ? playback : null));
            menus.AddButton("results", "CHARACTER SELECT", -25, () => { EndMatch(); OpenSelect(mode); });
            menus.AddButton("results", "MAIN MENU", -110, () => { EndMatch(); ShowMainMenu(); });
        }

        static int Wrap(int v, int n) { return ((v % n) + n) % n; }

        void OpenSelect(GameMode m)
        {
            mode = m == GameMode.Replay ? GameMode.VsCPU : m;
            RefreshSelect();
            menus.Show("select");
        }

        void RefreshSelect()
        {
            string[] modeNames = { "VERSUS CPU", "VERSUS PLAYER", "CPU VS CPU", "TRAINING", "REPLAY" };
            selMode.text = modeNames[(int)mode];
            selP1.text = "<  P1   " + roster[p1Pick].Name.ToUpper() + "  (" + roster[p1Pick].source.archetype + ")  >";
            string p2Label = mode == GameMode.Training ? "DUMMY" : (mode == GameMode.Versus ? "P2" : "CPU");
            selP2.text = "<  " + p2Label + "   " + roster[p2Pick].Name.ToUpper() + "  (" + roster[p2Pick].source.archetype + ")  >";
            selCpu.text = "<  CPU LEVEL   " + cpuLevel.ToString().ToUpper() + "  >";
            cpuCycler.gameObject.SetActive(mode != GameMode.Versus);
        }

        void ShowMainMenu()
        {
            menus.Show("main");
            hud.SetVisible(false);
            trainingUI.SetVisible(false);
            boxes.visible = false;
            if (attractMode) StartAttract();
            else audioMgr.PlayMusic(false);
        }

        void ShowMoveList()
        {
            var d = defs[0];
            var sb = new StringBuilder();
            sb.Append("<b>").Append(d.displayName.ToUpper()).Append("</b>   ").Append(d.archetype).Append("\n\n");
            foreach (var m in d.moves)
            {
                if (m.kind == MoveKind.Normal && m.dir != DirReq.Forward && m.dir != DirReq.Back && m.dir != DirReq.DownForward && m.dir != DirReq.AirDown) continue;
                sb.Append(m.isSuper ? "<color=#FFD040>" : "").Append(m.displayName.PadRight(22)).Append("   ").Append(Notation(m));
                if (m.isSuper) sb.Append("   (").Append(m.meterCost / 100).Append(" bars)</color>");
                sb.Append('\n');
            }
            sb.Append("\nThrow  LP plus LK      Tech  LP plus LK when grabbed      Dash  66 or 44");
            moveListText.supportRichText = true;
            moveListText.text = sb.ToString();
            menus.Show("moves");
        }

        //this turns a move into numpad notation which is how fighting game players write moves
        public static string Notation(MoveDef m)
        {
            string motion = "";
            switch (m.motion)
            {
                case Motion.QCF: motion = "236"; break;
                case Motion.QCB: motion = "214"; break;
                case Motion.DP: motion = "623"; break;
                case Motion.RDP: motion = "421"; break;
                case Motion.HCF: motion = "41236"; break;
                case Motion.HCB: motion = "63214"; break;
                case Motion.Super236236: motion = "236236"; break;
                case Motion.Super214214: motion = "214214"; break;
                case Motion.ChargeBackForward: motion = "[4]6"; break;
                case Motion.ChargeDownUp: motion = "[2]8"; break;
            }
            if (m.motion == Motion.None)
            {
                switch (m.dir)
                {
                    case DirReq.Forward: motion = "6"; break;
                    case DirReq.Back: motion = "4"; break;
                    case DirReq.DownForward: motion = "3"; break;
                    case DirReq.DownBack: motion = "1"; break;
                    case DirReq.Crouch: motion = "2"; break;
                    case DirReq.Air: motion = "j."; break;
                    case DirReq.AirDown: motion = "j.2"; break;
                    default: motion = "5"; break;
                }
            }
            string b;
            if (m.buttons == Btn.Punches) b = "P";
            else if (m.buttons == Btn.Kicks) b = "K";
            else
            {
                var sb = new StringBuilder();
                if ((m.buttons & Btn.LP) != 0) sb.Append("LP ");
                if ((m.buttons & Btn.MP) != 0) sb.Append("MP ");
                if ((m.buttons & Btn.HP) != 0) sb.Append("HP ");
                if ((m.buttons & Btn.LK) != 0) sb.Append("LK ");
                if ((m.buttons & Btn.MK) != 0) sb.Append("MK ");
                if ((m.buttons & Btn.HK) != 0) sb.Append("HK ");
                b = sb.ToString().Trim();
            }
            string prefix = string.IsNullOrEmpty(m.requiresPrev) ? "" : "after " + m.requiresPrev + "  ";
            return prefix + motion + " " + b;
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void WatchLastReplay()
        {
            var r = ReplayStorage.LoadLast();
            if (r == null || r.Length == 0)
            {
                mainNote.text = "no replay yet so play a match first";
                audioMgr.Play(SfxId.UIBack);
                return;
            }
            StartMatch(GameMode.Replay, r);
        }

        //this is the match setup part

        void StartAttract()
        {
            //this runs two nightmare cpus behind the title screen so the menu feels alive
            int a = Random.Range(0, roster.Count), b = Random.Range(0, roster.Count);
            SetupMatch(roster[a].MakeDef(), roster[b].MakeDef(), false, null, roster[a].asset, roster[b].asset);
            isAttract = true;
            runningMode = GameMode.CPUvsCPU;
            ai[0] = new AIBrain(0, AIDifficulty.Nightmare, (uint)System.Environment.TickCount);
            ai[1] = new AIBrain(1, AIDifficulty.Hard, (uint)System.Environment.TickCount + 7);
            hud.SetVisible(false);
            audioMgr.PlayMusic(false);
        }

        public void StartMatch(GameMode m, ReplayData replay)
        {
            menus.Hide();
            runningMode = m;
            isAttract = false;
            paused = false;
            trainingFrozen = false;
            audioMgr.SetMusicPaused(false);

            RosterEntry e1, e2;
            if (m == GameMode.Replay)
            {
                e1 = roster[RosterLoader.IndexOf(roster, replay.p1Fighter)];
                e2 = roster[RosterLoader.IndexOf(roster, replay.p2Fighter)];
            }
            else { e1 = roster[p1Pick]; e2 = roster[p2Pick]; }

            SetupMatch(e1.MakeDef(), e2.MakeDef(), m == GameMode.Training, replay, e1.asset, e2.asset);

            uint seed = (uint)System.Environment.TickCount;
            ai[0] = m == GameMode.CPUvsCPU ? new AIBrain(0, cpu2Level, seed) : null;
            ai[1] = (m == GameMode.VsCPU || m == GameMode.CPUvsCPU || m == GameMode.Training) ? new AIBrain(1, cpuLevel, seed + 99) : null;

            //it records every match except training so you can always watch the last one back
            recording = (m == GameMode.Training || m == GameMode.Replay) ? null : new ReplayData { p1Fighter = e1.Id, p2Fighter = e2.Id };

            hud.SetVisible(true);
            hud.SetNames(defs[0].displayName.ToUpper(), defs[1].displayName.ToUpper());
            trainingUI.SetVisible(m == GameMode.Training);
            trainingUI.ClearInputs();
            tracker = new FrameDataTracker();
            boxes.visible = m == GameMode.Training && boxes.visible;
            if (m == GameMode.Training) hud.Announce("TRAINING", 1.2f);
            if (m == GameMode.Replay) hud.Announce("REPLAY", 1.2f);
            audioMgr.PlayMusic(true);
        }

        void SetupMatch(FighterDef a, FighterDef b, bool training, ReplayData replay, FighterAsset assetA, FighterAsset assetB)
        {
            ClearViews();
            defs[0] = a;
            defs[1] = b;
            sim = new MatchSim(a, b, training);
            if (training) sim.ResetPositions(-1000, 1000);
            playback = replay;
            playbackIndex = 0;
            accumulator = 0;
            slowmoTicks = 0;
            matchOverTimer = -1f;
            resultsShown = false;
            accent[0] = FightUtil.RGB(a.accentRGB);
            accent[1] = FightUtil.RGB(b.accentRGB);
            if (a.accentRGB == b.accentRGB) accent[1] = Color.Lerp(accent[1], Color.cyan, 0.5f);

            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("Fighter" + (i + 1));
                views[i] = go.AddComponent<FighterView>();
                views[i].Build(i == 0 ? a : b, i, i == 0 ? assetA : assetB);
            }
            fx.ClearAll();
            camRig.Snap(sim);
        }

        void ClearViews()
        {
            for (int i = 0; i < 2; i++)
            {
                if (views[i] != null) Destroy(views[i].gameObject);
                views[i] = null;
            }
        }

        void EndMatch()
        {
            ClearViews();
            sim = null;
            fx.ClearAll();
            paused = false;
            audioMgr.SetMusicPaused(false);
        }

        void Resume()
        {
            paused = false;
            menus.Hide();
            audioMgr.SetMusicPaused(false);
        }

        //this is the frame loop part

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < 2; i++) inputs[i].Poll();

            bool inRealMatch = sim != null && !isAttract;
            if (inRealMatch && !resultsShown)
            {
                bool startPressed = inputs[0].StartPressed || inputs[1].StartPressed;
                if (startPressed)
                {
                    if (!paused) { paused = true; menus.Show("pause"); audioMgr.SetMusicPaused(true); }
                    else if (menus.Current == "pause") Resume();
                }
            }
            if (runningMode == GameMode.Training && inRealMatch && !paused) TrainingKeys();

            if (sim != null && !paused)
            {
                accumulator += dt;
                //if the game hitches it catches up a few frames but not too many or it would spiral
                if (accumulator > StepSeconds * 5) accumulator = StepSeconds * 5;
                while (accumulator >= StepSeconds)
                {
                    //the ko slowdown just makes each sim frame take longer in real time and the sim itself does not change
                    float step = slowmoTicks > 0 ? StepSeconds * 2.5f : StepSeconds;
                    if (accumulator < step) break;
                    accumulator -= step;
                    if (slowmoTicks > 0) slowmoTicks--;
                    if (trainingFrozen && !stepRequested) continue;
                    stepRequested = false;
                    SimStep();
                    if (sim == null) break;
                }
            }

            DrawFrame(dt);
        }

        bool stepRequested;

        void SimStep()
        {
            FrameInput p1 = default(FrameInput), p2 = default(FrameInput);
            switch (runningMode)
            {
                case GameMode.Versus:
                    p1 = inputs[0].Read(); p2 = inputs[1].Read(); break;
                case GameMode.VsCPU:
                    p1 = inputs[0].Read(); p2 = ai[1].Decide(sim); inputs[1].Read(); break;
                case GameMode.CPUvsCPU:
                    p1 = ai[0].Decide(sim); p2 = ai[1].Decide(sim); inputs[0].Read(); inputs[1].Read(); break;
                case GameMode.Training:
                    {
                        var human = inputs[0].Read();
                        inputs[1].Read();
                        if (dummy.isRecording)
                        {
                            //while recording player one controls the dummy just like street fighter training mode
                            p2 = human;
                            dummy.Record(human, sim.fighters[1].FacingRight);
                        }
                        else
                        {
                            p1 = human;
                            p2 = dummy.Decide(sim, 1, ai[1]);
                        }
                        trainingUI.PushInput(human, sim.fighters[dummy.isRecording ? 1 : 0].FacingRight);
                        break;
                    }
                case GameMode.Replay:
                    inputs[0].Read(); inputs[1].Read();
                    if (playbackIndex < playback.Length)
                    {
                        p1 = new FrameInput { bits = (ushort)playback.p1Inputs[playbackIndex] };
                        p2 = new FrameInput { bits = (ushort)playback.p2Inputs[playbackIndex] };
                        playbackIndex++;
                    }
                    else { OnReplayFinished(); return; }
                    break;
            }

            if (recording != null && sim.phase != Phase.MatchOver) recording.Add(p1, p2);
            sim.Tick(p1, p2);

            for (int i = 0; i < sim.eventCount; i++) HandleEvent(sim.events[i]);

            //dash trails
            for (int i = 0; i < 2; i++)
            {
                var f = sim.fighters[i];
                if ((f.state == FState.DashF || f.state == FState.DashB) && sim.frame % 3 == 0)
                    fx.Trail(FightUtil.ToWorld(f.x, f.y, 0.2f), accent[i], f.def.standHurt.h / 1000f);
            }

            if (runningMode == GameMode.Training)
            {
                int before = tracker.hasAdvantage ? tracker.advantage : 999;
                tracker.Update(sim, 0);
                if (sim.eventCount > 0 || before != (tracker.hasAdvantage ? tracker.advantage : 999)) trackerVersion++;
            }

            if (sim.phase == Phase.MatchOver && matchOverTimer < 0f)
            {
                matchOverTimer = 0f;
                if (recording != null)
                {
                    recording.finalChecksum = sim.Checksum();
                    ReplayStorage.Save(recording);
                    recording = null;
                }
            }
        }

        void OnReplayFinished()
        {
            if (resultsShown) return;
            bool ok = sim.Checksum() == playback.finalChecksum;
            ShowResults(ok ? "REPLAY VERIFIED" : "REPLAY MISMATCH", ok ? "every frame matched the original match" : "the fighter data changed since this was recorded");
        }

        void ShowResults(string title, string sub)
        {
            resultsShown = true;
            paused = true;
            resultTitle.text = title;
            resultSub.text = sub;
            menus.Show("results");
        }

        void DrawFrame(float dt)
        {
            float visualDt = paused ? 0f : dt;
            if (sim != null)
            {
                bool dim = sim.superFreeze > 0;
                for (int i = 0; i < 2; i++) if (views[i] != null) views[i].Sync(sim, dim);
                boxes.Sync(sim);
                hud.Tick(sim, dt);
                if (runningMode == GameMode.Training && !isAttract)
                    trainingUI.Tick(tracker, trackerVersion, dummy, cpuLevel, trainingFrozen);

                if (matchOverTimer >= 0f && !resultsShown)
                {
                    matchOverTimer += dt;
                    if (isAttract && matchOverTimer > 2f) StartAttract();
                    else if (!isAttract && runningMode != GameMode.Replay && matchOverTimer > 2.5f)
                    {
                        int w = sim.matchWinner;
                        ShowResults(w < 0 ? "DRAW" : defs[w].displayName.ToUpper() + " WINS", w < 0 ? "" : (w == 0 ? "PLAYER 1" : (runningMode == GameMode.Versus ? "PLAYER 2" : "CPU")));
                    }
                }
            }
            fx.Tick(visualDt, sim, accent[0], accent[1]);
            camRig.Tick(sim, dt);
            stage.Tick(camRig.transform.position);
        }

        static bool K(Key k)
        {
            var kb = Keyboard.current;
            return kb != null && kb[k].wasPressedThisFrame;
        }

        //this is all the training mode hotkeys
        void TrainingKeys()
        {
            var kb = Keyboard.current;
            var pad = inputs[0].AssignedPad;
            if (kb == null && pad == null) return;

            if (K(Key.F1) || (pad != null && pad.selectButton.wasPressedThisFrame)) boxes.visible = !boxes.visible;
            if (K(Key.F2)) trainingUI.ShowFrameData = !trainingUI.ShowFrameData;
            if (K(Key.F3)) trainingUI.ShowInputs = !trainingUI.ShowInputs;
            if (K(Key.F4)) { dummy.stance = (DummyStance)Wrap((int)dummy.stance + 1, 5); if (dummy.stance == DummyStance.Playback && dummy.recording.Count == 0) dummy.stance = DummyStance.Stand; }
            if (K(Key.F5)) dummy.block = (DummyBlock)Wrap((int)dummy.block + 1, 4);
            if (K(Key.F6))
            {
                if (dummy.isRecording) { dummy.StopRecording(); hud.Announce("RECORDED", 1f); }
                else { dummy.StartRecording(); hud.Announce("RECORDING", 1f, "you control the dummy now"); }
            }
            if (K(Key.F7) && dummy.recording.Count > 0)
            {
                dummy.StopRecording();
                dummy.stance = dummy.stance == DummyStance.Playback ? DummyStance.Stand : DummyStance.Playback;
            }
            if (K(Key.F8)) dummy.wakeupReversal = !dummy.wakeupReversal;
            if (K(Key.F9))
            {
                //holding left or right while resetting puts the dummy in that corner
                bool left = kb != null && kb[Key.A].isPressed;
                bool right = kb != null && kb[Key.D].isPressed;
                if (left) sim.ResetPositions(-5800, -6900);
                else if (right) sim.ResetPositions(5800, 6900);
                else sim.ResetPositions(-1000, 1000);
                fx.ClearAll();
                camRig.Snap(sim);
            }
            if (K(Key.F10)) trainingFrozen = !trainingFrozen;
            if (K(Key.F11) && trainingFrozen) { stepRequested = true; accumulator = StepSeconds; }
            if (K(Key.Tab))
            {
                cpuLevel = (AIDifficulty)Wrap((int)cpuLevel + 1, 4);
                ai[1] = new AIBrain(1, cpuLevel, (uint)System.Environment.TickCount);
            }
        }

        //this turns the sim events into game feel
        //every hit gets a spark a sound and a shake that all scale with how strong the hit was
        void HandleEvent(SimEvent e)
        {
            var pos = FightUtil.ToWorld(e.x, e.y, -0.5f);
            float pan = pos.x / 6f;
            bool sound = !isAttract;
            float lvl = FightUtil.LevelScale(e.level);

            switch (e.type)
            {
                case SimEventType.AttackStart:
                    if (sound) audioMgr.Play(e.level >= HitLevel.Heavy ? SfxId.WhooshHeavy : SfxId.WhooshLight, 0.5f, pan);
                    break;

                case SimEventType.Hit:
                case SimEventType.CounterHit:
                    {
                        bool counter = e.type == SimEventType.CounterHit;
                        fx.Spark(pos, FightUtil.LevelColor(e.level), lvl * (counter ? 1.3f : 1f), counter ? SparkKind.Counter : SparkKind.Hit);
                        if (sound)
                        {
                            SfxId id = e.level == HitLevel.Light ? SfxId.HitLight : e.level == HitLevel.Medium ? SfxId.HitMedium : e.level == HitLevel.Heavy ? SfxId.HitHeavy : SfxId.HitSpecial;
                            audioMgr.Play(id, 1f, pan);
                            if (counter) audioMgr.Play(SfxId.CounterHit, 0.7f, pan);
                        }
                        camRig.Shake(0.12f + 0.12f * lvl + (counter ? 0.2f : 0f));
                        if (counter) hud.Popup(e.player, "COUNTER", new Color(1f, 0.35f, 0.3f));
                        break;
                    }

                case SimEventType.Block:
                    fx.Spark(pos, new Color(0.4f, 0.7f, 1f), lvl * 0.8f, SparkKind.Block);
                    if (sound) audioMgr.Play(SfxId.Block, 0.8f, pan);
                    camRig.Shake(0.08f * lvl);
                    break;

                case SimEventType.ProjectileSpawn:
                    if (sound) audioMgr.Play(SfxId.Fireball, 0.8f, pan);
                    break;

                case SimEventType.ProjectileClash:
                    fx.Spark(pos, Color.white, 1.5f, SparkKind.Clash);
                    if (sound) audioMgr.Play(SfxId.ProjectileClash, 1f, pan);
                    camRig.Shake(0.25f);
                    break;

                case SimEventType.ProjectileEnd:
                    fx.Spark(pos, accent[Mathf.Clamp(e.player, 0, 1)], 0.8f, SparkKind.Puff);
                    break;

                case SimEventType.ThrowStart:
                    if (sound) audioMgr.Play(SfxId.WhooshHeavy, 0.8f, pan);
                    break;

                case SimEventType.ThrowLand:
                    fx.Spark(new Vector3(pos.x, 0.2f, -0.5f), new Color(0.8f, 0.7f, 0.6f), 2f, SparkKind.Dust);
                    if (sound) audioMgr.Play(SfxId.Throw, 1f, pan);
                    camRig.Shake(0.5f);
                    break;

                case SimEventType.ThrowTech:
                    fx.Spark(pos, Color.white, 1.4f, SparkKind.Tech);
                    if (sound) audioMgr.Play(SfxId.ThrowTech, 1f, pan);
                    hud.Popup(0, "THROW TECH", new Color(0.6f, 0.9f, 1f));
                    hud.Popup(1, "THROW TECH", new Color(0.6f, 0.9f, 1f));
                    break;

                case SimEventType.Jump:
                    if (sound) audioMgr.Play(SfxId.Jump, 0.35f, pan);
                    break;

                case SimEventType.Land:
                    fx.Spark(new Vector3(pos.x, 0.1f, -0.5f), new Color(0.8f, 0.75f, 0.7f), 0.6f, SparkKind.Dust);
                    if (sound) audioMgr.Play(SfxId.Land, 0.4f, pan);
                    break;

                case SimEventType.Dash:
                    fx.Spark(new Vector3(pos.x, 0.1f, -0.5f), new Color(0.8f, 0.75f, 0.7f), 0.8f, SparkKind.Dust);
                    if (sound) audioMgr.Play(SfxId.Dash, 0.6f, pan);
                    break;

                case SimEventType.Knockdown:
                    fx.Spark(new Vector3(pos.x, 0.15f, -0.5f), new Color(0.8f, 0.7f, 0.6f), 1.8f, SparkKind.Dust);
                    if (sound) audioMgr.Play(SfxId.Knockdown, 0.9f, pan);
                    camRig.Shake(0.3f);
                    break;

                case SimEventType.SuperFlash:
                    fx.Spark(pos, new Color(1f, 0.85f, 0.2f), 3f, SparkKind.Burst);
                    if (sound) audioMgr.Play(SfxId.SuperFlash, 1f, pan);
                    camRig.Punch(pos, 1f);
                    hud.Flash(0.25f);
                    break;

                case SimEventType.KO:
                case SimEventType.DoubleKO:
                    fx.Spark(pos, new Color(1f, 0.3f, 0.2f), 3.5f, SparkKind.Burst);
                    if (sound) audioMgr.Play(SfxId.KO, 1f, pan);
                    camRig.Shake(1f);
                    camRig.Punch(pos, 1f);
                    hud.Flash(0.8f);
                    slowmoTicks = 50;
                    if (!isAttract) hud.Announce(e.type == SimEventType.DoubleKO ? "DOUBLE K.O." : "K.O.", 2f);
                    break;

                case SimEventType.RoundAnnounce:
                    if (isAttract) break;
                    {
                        bool final = sim.fighters[0].roundWins == sim.roundsToWin - 1 && sim.fighters[1].roundWins == sim.roundsToWin - 1;
                        hud.Announce(final ? "FINAL ROUND" : "ROUND " + e.value, 1.4f);
                        if (sound) audioMgr.Play(SfxId.RoundAnnounce, 1f);
                    }
                    break;

                case SimEventType.Fight:
                    if (isAttract) break;
                    hud.Announce("FIGHT!", 0.9f);
                    if (sound) audioMgr.Play(SfxId.Fight, 1f);
                    break;

                case SimEventType.TimeUp:
                    if (!isAttract) hud.Announce("TIME", 1.5f);
                    if (sound) audioMgr.Play(SfxId.KO, 0.6f);
                    break;

                case SimEventType.RoundWin:
                    if (isAttract) break;
                    {
                        var w = sim.fighters[e.player];
                        bool perfect = w.health >= w.def.maxHealth;
                        hud.Announce(w.def.displayName.ToUpper() + " WINS", 1.8f, perfect ? "PERFECT" : "");
                        if (sound) audioMgr.Play(SfxId.RoundWin, 1f);
                    }
                    break;

                case SimEventType.Draw:
                    if (!isAttract) hud.Announce("DRAW", 1.8f);
                    break;
            }
        }
    }
}
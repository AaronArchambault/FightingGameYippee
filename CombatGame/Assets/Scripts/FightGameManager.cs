using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using FightCore;

namespace FightGame
{
    public enum GameMode { VsCPU, Versus, CPUvsCPU, Training, Replay }
    public enum StartScreen { MainMenu, Training, VersusCPU, CPUvsCPU }

    //this is the main game object that runs everything
    //it runs the sim at exactly 60 frames a second then draws whatever the sim says
    //and turns sim events into sparks sounds and shakes
    //every piece it uses is a slot in the inspector and if a slot is empty it looks in the scene and if it is not there it builds a default one
    //so it works from one empty object but you can also build the whole scene yourself and customize every part
    [DefaultExecutionOrder(-50)]
    public class FightGameManager : MonoBehaviour
    {
        public const float StepSeconds = 1f / 60f;
        public static FightGameManager Instance { get; private set; }

        [Header("Scene Pieces (empty means find or build one)")]
        public CameraRig cameraRig;
        public StageView stage;
        public EffectsManager effects;
        public HitboxDebugView hitboxView;
        public AudioManager audioManager;
        public FightHUD hud;
        public TrainingOverlay trainingOverlay;
        public MenuSystem menus;
        [Tooltip("where fighters get spawned and empty means the root of the scene")]
        public Transform fighterParent;

        [Header("Fighters")]
        [Tooltip("empty means it uses FightingGameRoster in Resources and if that is missing the built in fighters")]
        public RosterAsset roster;
        [Tooltip("used for fighters that have no View Prefab and no Animations and empty means the shape puppet")]
        public FighterView defaultViewPrefab;

        [Header("Rules (these change the fight and get saved in replays)")]
        public MatchRules rules = new MatchRules();

        [Header("Feel (these only change how it looks and sounds)")]
        public GameFeelSettings feel = new GameFeelSettings();

        [Header("Controls")]
        [Tooltip("an Input Actions asset with maps called Player1 and Player2 and empty means the default controls")]
        public InputActionAsset inputActions;
        public TrainingKeys trainingKeys = new TrainingKeys();

        [Header("Startup")]
        public StartScreen startScreen = StartScreen.MainMenu;
        [Tooltip("two cpus fight behind the title screen")]
        public bool attractMode = true;
        public AIDifficulty defaultCpuLevel = AIDifficulty.Normal;
        [Tooltip("the level of player one in cpu vs cpu")]
        public AIDifficulty cpuVsCpuLevel = AIDifficulty.Hard;
        [Tooltip("vsync keeps the picture smooth and the sim still runs at exactly 60 no matter the refresh rate")]
        public bool vSync = true;

        readonly PlayerInputSource[] inputs = new PlayerInputSource[2];
        List<RosterEntry> entries;

        //these are the choices from the select screen
        GameMode mode = GameMode.VsCPU;
        int p1Pick, p2Pick = 1;
        AIDifficulty cpuLevel;

        //this is everything about the match that is running right now
        MatchSim sim;
        readonly FighterView[] views = new FighterView[2];
        readonly AIBrain[] ai = new AIBrain[2];
        readonly FighterDef[] defs = new FighterDef[2];
        readonly FighterAsset[] assets = new FighterAsset[2];
        readonly Color[] accent = new Color[2];
        readonly DummyController dummy = new DummyController();
        FrameDataTracker tracker = new FrameDataTracker();
        int trackerVersion;
        ReplayData recording, playback;
        int playbackIndex;
        bool isAttract;
        GameMode runningMode;

        double accumulator;
        bool paused, trainingFrozen, stepRequested, resultsShown;
        int slowmoTicks;
        float matchOverTimer = -1f;

        public MatchSim Sim { get { return sim; } }

        void Awake()
        {
            Instance = this;
            QualitySettings.vSyncCount = vSync ? 1 : 0;
            if (!vSync) Application.targetFrameRate = 120;
            rules.Sanitize();
            SetUpScenePieces();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            InputSystem.onDeviceChange -= OnDeviceChange;
            for (int i = 0; i < 2; i++) if (inputs[i] != null) inputs[i].Dispose();
        }

        //this fills every empty slot by looking in the scene first and building a default one if it is not there
        void SetUpScenePieces()
        {
            if (FightUtil.FindInScene<EventSystem>() == null)
            {
                //it turns the object off while adding the module so the default actions get hooked up before it starts
                var es = new GameObject("EventSystem");
                es.SetActive(false);
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
                es.SetActive(true);
            }
            if (cameraRig == null) cameraRig = FightUtil.FindInScene<CameraRig>();
            if (cameraRig == null)
            {
                foreach (var c in Camera.allCameras) c.gameObject.SetActive(false);
                cameraRig = CameraRig.Create();
            }
            if (audioManager == null) audioManager = FightUtil.FindInScene<AudioManager>();
            if (audioManager == null) audioManager = AudioManager.Create(null);
            if (stage == null) stage = FightUtil.FindInScene<StageView>();
            if (stage == null) stage = new GameObject("Stage").AddComponent<StageView>();
            if (effects == null) effects = FightUtil.FindInScene<EffectsManager>();
            if (effects == null) effects = new GameObject("Effects").AddComponent<EffectsManager>();
            if (hitboxView == null) hitboxView = FightUtil.FindInScene<HitboxDebugView>();
            if (hitboxView == null) hitboxView = new GameObject("HitboxDebug").AddComponent<HitboxDebugView>();
            if (hud == null) hud = FightUtil.FindInScene<FightHUD>();
            if (hud == null) hud = FightHUD.Create();
            if (trainingOverlay == null) trainingOverlay = FightUtil.FindInScene<TrainingOverlay>();
            if (trainingOverlay == null) trainingOverlay = TrainingOverlay.Create();
            if (menus == null) menus = FightUtil.FindInScene<MenuSystem>();
            if (menus == null) menus = MenuSystem.Create();
            if (fighterParent == null) fighterParent = new GameObject("Fighters").transform;

            //if you added the ui scripts yourself but never built anything under them this fills them in so the game still works
            if (hud.IsEmpty) { UIFactory.EnsureCanvas(hud.gameObject, 10); hud.BuildDefault(); }
            if (trainingOverlay.IsEmpty) { UIFactory.EnsureCanvas(trainingOverlay.gameObject, 11); trainingOverlay.BuildDefault(); }
            if (menus.IsEmpty) { UIFactory.EnsureCanvas(menus.gameObject, 20); menus.BuildDefault(); }

            effects.Init();
            hitboxView.Init();
            audioManager.Init();
            effects.projectilePrefabFor = ProjectilePrefabFor;
        }

        void Start()
        {
            cpuLevel = defaultCpuLevel;
            entries = RosterLoader.Load(roster);
            p2Pick = Mathf.Min(1, entries.Count - 1);
            inputs[0] = new PlayerInputSource(0, inputActions);
            inputs[1] = new PlayerInputSource(1, inputActions);
            //when you plug in or pull out a controller it hands out the pads again
            InputSystem.onDeviceChange += OnDeviceChange;

            menus.Wire();
            WarnAboutMissingScreens();
            menus.ActionPressed += OnMenuAction;
            menus.Cycled += OnMenuCycle;
            trainingOverlay.SetHelp(trainingKeys.HelpText());

            switch (startScreen)
            {
                case StartScreen.Training: mode = GameMode.Training; StartMatch(GameMode.Training, null); break;
                case StartScreen.VersusCPU: mode = GameMode.VsCPU; StartMatch(GameMode.VsCPU, null); break;
                case StartScreen.CPUvsCPU: mode = GameMode.CPUvsCPU; StartMatch(GameMode.CPUvsCPU, null); break;
                default: ShowMainMenu(); break;
            }
        }

        //this tells you in the console if your own menus are missing a screen so it is not a mystery
        void WarnAboutMissingScreens()
        {
            var missing = new StringBuilder();
            foreach (MenuPanelId id in System.Enum.GetValues(typeof(MenuPanelId)))
                if (!menus.HasPanel(id)) missing.Append(id).Append(' ');
            if (missing.Length > 0)
                Debug.Log("Fighting Game: your menus have no screen for " + missing + "so the game skips those parts");
        }

        void OnDeviceChange(InputDevice d, InputDeviceChange change)
        {
            if (change == InputDeviceChange.Added || change == InputDeviceChange.Removed || change == InputDeviceChange.Reconnected || change == InputDeviceChange.Disconnected)
                for (int i = 0; i < 2; i++) if (inputs[i] != null) inputs[i].AssignDevices();
        }

        //this is the menu part

        void OnMenuAction(MenuAction a)
        {
            switch (a)
            {
                case MenuAction.VersusCPU: OpenSelect(GameMode.VsCPU); break;
                case MenuAction.VersusPlayer: OpenSelect(GameMode.Versus); break;
                case MenuAction.Training: OpenSelect(GameMode.Training); break;
                case MenuAction.CPUvsCPU: OpenSelect(GameMode.CPUvsCPU); break;
                case MenuAction.WatchReplay: WatchLastReplay(); break;
                case MenuAction.Quit: Quit(); break;
                case MenuAction.StartFight: StartMatch(mode, null); break;
                case MenuAction.BackToMain: ShowMainMenu(); break;
                case MenuAction.Resume: Resume(); break;
                case MenuAction.Restart: StartMatch(runningMode, runningMode == GameMode.Replay ? playback : null); break;
                case MenuAction.MoveList: ShowMoveList(); break;
                case MenuAction.BackToPause: menus.Show(MenuPanelId.Pause); break;
                case MenuAction.CharacterSelect: EndMatch(); OpenSelect(mode); break;
                case MenuAction.MainMenu: EndMatch(); ShowMainMenu(); break;
            }
        }

        void OnMenuCycle(CyclerTarget t, int d)
        {
            switch (t)
            {
                case CyclerTarget.P1Fighter: p1Pick = Wrap(p1Pick + d, entries.Count); break;
                case CyclerTarget.P2Fighter: p2Pick = Wrap(p2Pick + d, entries.Count); break;
                case CyclerTarget.CpuLevel: cpuLevel = (AIDifficulty)Wrap((int)cpuLevel + d, 4); break;
            }
            RefreshSelect();
        }

        static int Wrap(int v, int n) { return n <= 0 ? 0 : ((v % n) + n) % n; }

        void OpenSelect(GameMode m)
        {
            mode = m == GameMode.Replay ? GameMode.VsCPU : m;
            //no select screen means it just starts with the fighters you picked last time
            if (!menus.HasPanel(MenuPanelId.Select)) { StartMatch(mode, null); return; }
            RefreshSelect();
            menus.Show(MenuPanelId.Select);
        }

        void RefreshSelect()
        {
            string[] modeNames = { "VERSUS CPU", "VERSUS PLAYER", "CPU VS CPU", "TRAINING", "REPLAY" };
            menus.SetText(MenuTextId.ModeTitle, modeNames[(int)mode]);
            var a = entries[p1Pick];
            var b = entries[p2Pick];
            menus.SetCycler(CyclerTarget.P1Fighter, "<  P1   " + a.Name.ToUpper() + "  (" + a.source.archetype + ")  >");
            string p2Label = mode == GameMode.Training ? "DUMMY" : (mode == GameMode.Versus ? "P2" : "CPU");
            menus.SetCycler(CyclerTarget.P2Fighter, "<  " + p2Label + "   " + b.Name.ToUpper() + "  (" + b.source.archetype + ")  >");
            menus.SetCycler(CyclerTarget.CpuLevel, "<  CPU LEVEL   " + cpuLevel.ToString().ToUpper() + "  >");
            menus.SetCyclerVisible(CyclerTarget.CpuLevel, mode != GameMode.Versus);
            menus.SetImage(MenuImageId.P1Portrait, a.asset != null ? a.asset.portrait : null);
            menus.SetImage(MenuImageId.P2Portrait, b.asset != null ? b.asset.portrait : null);
            menus.SetText(MenuTextId.P1Description, a.source.archetype.ToString());
            menus.SetText(MenuTextId.P2Description, b.source.archetype.ToString());
        }

        void ShowMainMenu()
        {
            menus.Show(MenuPanelId.Main);
            menus.SetText(MenuTextId.MainNote, "");
            hud.SetVisible(false);
            trainingOverlay.SetVisible(false);
            hitboxView.visible = false;
            if (attractMode) StartAttract();
            else { EndMatch(); audioManager.PlayMusic(false); }
        }

        void ShowMoveList()
        {
            var d = defs[0];
            var sb = new StringBuilder();
            sb.Append("<b>").Append(d.displayName.ToUpper()).Append("</b>   ").Append(d.archetype).Append("\n\n");
            foreach (var m in d.moves)
            {
                if (m.kind == MoveKind.Normal && m.dir != DirReq.Forward && m.dir != DirReq.Back && m.dir != DirReq.DownForward && m.dir != DirReq.AirDown) continue;
                sb.Append(m.isSuper ? "<color=#FFD040>" : "").Append(m.displayName.PadRight(22)).Append("   ").Append(FightUtil.Notation(m));
                if (m.isSuper) sb.Append("   (").Append(m.meterCost / 100).Append(" bars)</color>");
                sb.Append('\n');
            }
            sb.Append("\nThrow  LP plus LK      Tech  LP plus LK when grabbed      Dash  66 or 44");
            menus.SetText(MenuTextId.MoveList, sb.ToString());
            menus.Show(MenuPanelId.MoveList);
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
                menus.SetText(MenuTextId.MainNote, "no replay yet so play a match first");
                audioManager.Play(SfxId.UIBack);
                return;
            }
            StartMatch(GameMode.Replay, r);
        }

        //this is the match setup part

        void StartAttract()
        {
            //this runs two cpus behind the title screen so the menu feels alive
            int a = Random.Range(0, entries.Count), b = Random.Range(0, entries.Count);
            SetupMatch(entries[a], entries[b], false, null, rules);
            isAttract = true;
            runningMode = GameMode.CPUvsCPU;
            ai[0] = new AIBrain(0, AIDifficulty.Nightmare, (uint)System.Environment.TickCount);
            ai[1] = new AIBrain(1, AIDifficulty.Hard, (uint)System.Environment.TickCount + 7);
            hud.SetVisible(false);
            audioManager.PlayMusic(false);
        }

        public void StartMatch(GameMode m, ReplayData replay)
        {
            menus.Hide();
            runningMode = m;
            isAttract = false;
            paused = false;
            trainingFrozen = false;
            audioManager.SetMusicPaused(false);

            RosterEntry e1, e2;
            MatchRules r = rules;
            if (m == GameMode.Replay)
            {
                e1 = entries[RosterLoader.IndexOf(entries, replay.p1Fighter)];
                e2 = entries[RosterLoader.IndexOf(entries, replay.p2Fighter)];
                if (replay.rules != null) r = replay.rules;
            }
            else { e1 = entries[p1Pick]; e2 = entries[p2Pick]; }

            SetupMatch(e1, e2, m == GameMode.Training, replay, r);

            uint seed = (uint)System.Environment.TickCount;
            ai[0] = m == GameMode.CPUvsCPU ? new AIBrain(0, cpuVsCpuLevel, seed) : null;
            ai[1] = (m == GameMode.VsCPU || m == GameMode.CPUvsCPU || m == GameMode.Training) ? new AIBrain(1, cpuLevel, seed + 99) : null;

            //it records every match except training so you can always watch the last one back
            recording = (m == GameMode.Training || m == GameMode.Replay) ? null : new ReplayData { p1Fighter = e1.Id, p2Fighter = e2.Id, rules = sim.rules.Clone() };

            hud.SetVisible(true);
            hud.SetNames(defs[0].displayName.ToUpper(), defs[1].displayName.ToUpper());
            hud.SetPortraits(e1.asset != null ? e1.asset.portrait : null, e2.asset != null ? e2.asset.portrait : null);
            trainingOverlay.SetVisible(m == GameMode.Training);
            trainingOverlay.ClearInputs();
            tracker = new FrameDataTracker();
            hitboxView.visible = m == GameMode.Training && hitboxView.visible;
            if (m == GameMode.Training) hud.Announce("TRAINING", 1.2f);
            if (m == GameMode.Replay) hud.Announce("REPLAY", 1.2f);
            audioManager.PlayMusic(true, stage != null ? stage.music : null);
        }

        void SetupMatch(RosterEntry e1, RosterEntry e2, bool training, ReplayData replay, MatchRules r)
        {
            ClearViews();
            defs[0] = e1.MakeDef();
            defs[1] = e2.MakeDef();
            assets[0] = e1.asset;
            assets[1] = e2.asset;
            sim = new MatchSim(defs[0], defs[1], training, r);
            if (training) sim.ResetPositions(-1000, 1000);
            playback = replay;
            playbackIndex = 0;
            accumulator = 0;
            slowmoTicks = 0;
            matchOverTimer = -1f;
            resultsShown = false;
            accent[0] = FightUtil.RGB(defs[0].accentRGB);
            accent[1] = FightUtil.RGB(defs[1].accentRGB);
            if (defs[0].accentRGB == defs[1].accentRGB) accent[1] = Color.Lerp(accent[1], Color.cyan, 0.5f);

            bool mirror = e1.Id == e2.Id;
            for (int i = 0; i < 2; i++) views[i] = SpawnView(defs[i], i, assets[i], mirror);
            effects.ClearAll();
            cameraRig.Snap(sim);
        }

        //this picks how a fighter is drawn
        //a View Prefab on the fighter wins then Animations on the fighter then the default prefab then the shape puppet
        FighterView SpawnView(FighterDef def, int player, FighterAsset asset, bool mirror)
        {
            FighterView view;
            FighterView prefab = asset != null && asset.viewPrefab != null ? asset.viewPrefab : null;
            if (prefab == null && (asset == null || asset.animations == null)) prefab = defaultViewPrefab;

            if (prefab != null) view = Instantiate(prefab, fighterParent);
            else
            {
                var go = new GameObject();
                go.transform.SetParent(fighterParent, false);
                if (asset != null && asset.animations != null) view = go.AddComponent<SpriteFighterView>();
                else view = go.AddComponent<PuppetFighterView>();
            }
            view.name = "P" + (player + 1) + " " + def.displayName;
            view.Setup(def, player, asset);
            var sv = view as SpriteFighterView;
            if (sv != null) sv.SetMirror(mirror);
            return view;
        }

        ProjectileView ProjectilePrefabFor(Projectile p)
        {
            var fx = MoveFxFor(p.owner, p.moveIndex);
            return fx != null ? fx.projectilePrefab : null;
        }

        MoveFx MoveFxFor(int player, int moveIndex)
        {
            if (player < 0 || player > 1 || assets[player] == null || defs[player] == null) return null;
            if (moveIndex < 0 || moveIndex >= defs[player].moves.Count) return null;
            return assets[player].FxFor(defs[player].moves[moveIndex].id);
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
            effects.ClearAll();
            paused = false;
            audioManager.SetMusicPaused(false);
        }

        void Resume()
        {
            if (!menus.HasPanel(MenuPanelId.Pause)) hud.Announce("", 0.01f);
            paused = false;
            menus.Hide();
            audioManager.SetMusicPaused(false);
        }

        //this is the frame loop part

        void Update()
        {
            if (inputs[0] == null) return;
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < 2; i++) inputs[i].Poll();

            bool inRealMatch = sim != null && !isAttract;
            if (inRealMatch && !resultsShown && (inputs[0].StartPressed || inputs[1].StartPressed))
            {
                if (!paused)
                {
                    paused = true;
                    menus.Show(MenuPanelId.Pause);
                    audioManager.SetMusicPaused(true);
                    if (!menus.HasPanel(MenuPanelId.Pause)) hud.Announce("PAUSED", 9999f, "press start again to keep playing");
                }
                else if (menus.IsShowing(MenuPanelId.Pause) || !menus.HasPanel(MenuPanelId.Pause)) Resume();
            }
            if (runningMode == GameMode.Training && inRealMatch && !paused) TrainingHotkeys();

            if (sim != null && !paused)
            {
                accumulator += dt;
                //if the game hitches it catches up a few frames but not too many or it would spiral
                if (accumulator > StepSeconds * 5) accumulator = StepSeconds * 5;
                while (accumulator >= StepSeconds)
                {
                    //the ko slowdown just makes each sim frame take longer in real time and the sim itself does not change
                    float step = slowmoTicks > 0 ? StepSeconds * feel.koSlowmoFactor : StepSeconds;
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
                        trainingOverlay.PushInput(human, sim.fighters[dummy.isRecording ? 1 : 0].FacingRight);
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

            if (feel.dashTrails)
                for (int i = 0; i < 2; i++)
                {
                    var f = sim.fighters[i];
                    if ((f.state == FState.DashF || f.state == FState.DashB) && sim.frame % feel.trailEveryFrames == 0)
                        effects.Trail(FightUtil.ToWorld(f.x, f.y, 0.2f), accent[i], f.def.standHurt.h / 1000f, f.facing);
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
            //no results screen means it goes back to the main menu after the match
            if (!menus.HasPanel(MenuPanelId.Results)) { EndMatch(); ShowMainMenu(); return; }
            resultsShown = true;
            paused = true;
            menus.SetText(MenuTextId.ResultTitle, title);
            menus.SetText(MenuTextId.ResultSub, sub);
            menus.Show(MenuPanelId.Results);
        }

        void DrawFrame(float dt)
        {
            float visualDt = paused ? 0f : dt;
            if (sim != null)
            {
                for (int i = 0; i < 2; i++) if (views[i] != null) views[i].Sync(sim);
                hitboxView.Sync(sim);
                hud.Tick(sim, dt);
                if (runningMode == GameMode.Training && !isAttract)
                    trainingOverlay.Tick(tracker, trackerVersion, dummy, cpuLevel, trainingFrozen);

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
            effects.Tick(visualDt, sim, accent[0], accent[1]);
            cameraRig.Tick(sim, dt);
            if (stage != null) stage.Tick(cameraRig.transform.position);
        }

        static bool Pressed(Key k)
        {
            var kb = Keyboard.current;
            return kb != null && k != Key.None && kb[k].wasPressedThisFrame;
        }

        static bool Held(Key k)
        {
            var kb = Keyboard.current;
            return kb != null && k != Key.None && kb[k].isPressed;
        }

        //this is all the training mode hotkeys and you can change them on the manager
        void TrainingHotkeys()
        {
            var k = trainingKeys;
            var pad = inputs[0].AssignedPad;
            if (Pressed(k.toggleHitboxes) || (pad != null && pad.selectButton.wasPressedThisFrame)) hitboxView.visible = !hitboxView.visible;
            if (Pressed(k.toggleFrameData)) trainingOverlay.ShowFrameData = !trainingOverlay.ShowFrameData;
            if (Pressed(k.toggleInputs)) trainingOverlay.ShowInputs = !trainingOverlay.ShowInputs;
            if (Pressed(k.dummyStance)) { dummy.stance = (DummyStance)Wrap((int)dummy.stance + 1, 5); if (dummy.stance == DummyStance.Playback && dummy.recording.Count == 0) dummy.stance = DummyStance.Stand; }
            if (Pressed(k.dummyBlock)) dummy.block = (DummyBlock)Wrap((int)dummy.block + 1, 4);
            if (Pressed(k.record))
            {
                if (dummy.isRecording) { dummy.StopRecording(); hud.Announce("RECORDED", 1f); }
                else { dummy.StartRecording(); hud.Announce("RECORDING", 1f, "you control the dummy now"); }
            }
            if (Pressed(k.playback) && dummy.recording.Count > 0)
            {
                dummy.StopRecording();
                dummy.stance = dummy.stance == DummyStance.Playback ? DummyStance.Stand : DummyStance.Playback;
            }
            if (Pressed(k.wakeupReversal)) dummy.wakeupReversal = !dummy.wakeupReversal;
            if (Pressed(k.resetPositions))
            {
                //holding left or right while resetting puts the dummy in that corner
                int wall = sim.StageHalf;
                if (Held(k.cornerLeft)) sim.ResetPositions(-wall + 1400, -wall + 300);
                else if (Held(k.cornerRight)) sim.ResetPositions(wall - 1400, wall - 300);
                else sim.ResetPositions(-1000, 1000);
                effects.ClearAll();
                cameraRig.Snap(sim);
            }
            if (Pressed(k.freeze)) trainingFrozen = !trainingFrozen;
            if (Pressed(k.stepFrame) && trainingFrozen) { stepRequested = true; accumulator = StepSeconds; }
            if (Pressed(k.cycleCpuLevel))
            {
                cpuLevel = (AIDifficulty)Wrap((int)cpuLevel + 1, 4);
                ai[1] = new AIBrain(1, cpuLevel, (uint)System.Environment.TickCount);
            }
        }

        //this is the game feel part that turns sim events into sparks sounds and shakes
        //every hit gets a spark a sound and a shake that all scale with how strong the hit was
        void HandleEvent(SimEvent e)
        {
            var pos = FightUtil.ToWorld(e.x, e.y, -0.5f);
            float pan = pos.x / 6f;
            bool sound = !isAttract;
            float lvl = FightUtil.LevelScale(e.level);
            int facing = e.player >= 0 && e.player < 2 ? sim.fighters[e.player].facing : 1;
            var fx = (e.player >= 0 && e.player < 2) ? MoveFxFor(e.player, e.move) : null;

            switch (e.type)
            {
                case SimEventType.AttackStart:
                    if (!sound) break;
                    if (fx != null && fx.startSound != null) audioManager.PlayClip(fx.startSound, 1f, pan);
                    else if (feel.attackWhooshes) audioManager.Play(e.level >= HitLevel.Heavy ? SfxId.WhooshHeavy : SfxId.WhooshLight, feel.whooshVolume, pan);
                    if (fx != null && fx.voice != null) audioManager.PlayClip(fx.voice, 1f, pan, 4);
                    else if (e.level >= HitLevel.Heavy) Shout(e.player, pan);
                    break;

                case SimEventType.Hit:
                case SimEventType.CounterHit:
                    {
                        bool counter = e.type == SimEventType.CounterHit;
                        effects.Spark(counter ? SparkKind.Counter : SparkKind.Hit, pos, FightUtil.LevelColor(e.level), lvl * (counter ? 1.3f : 1f), facing, fx != null ? fx.hitEffect : null);
                        if (sound)
                        {
                            if (fx != null && fx.hitSound != null) audioManager.PlayClip(fx.hitSound, 1f, pan, 4);
                            else
                            {
                                SfxId id = e.level == HitLevel.Light ? SfxId.HitLight : e.level == HitLevel.Medium ? SfxId.HitMedium : e.level == HitLevel.Heavy ? SfxId.HitHeavy : SfxId.HitSpecial;
                                audioManager.Play(id, 1f, pan);
                            }
                            if (counter) audioManager.Play(SfxId.CounterHit, 0.7f, pan);
                            if (e.level >= HitLevel.Heavy) Hurt(e.value, pan);
                        }
                        cameraRig.Shake(feel.hitShake + feel.hitShakePerLevel * lvl + (counter ? feel.counterHitBonus : 0f));
                        if (counter) hud.Popup(e.player, "COUNTER", new Color(1f, 0.35f, 0.3f));
                        break;
                    }

                case SimEventType.Block:
                    effects.Spark(SparkKind.Block, pos, new Color(0.4f, 0.7f, 1f), lvl * 0.8f, facing);
                    if (sound) audioManager.Play(SfxId.Block, 0.8f, pan);
                    cameraRig.Shake(feel.blockShake * lvl);
                    break;

                case SimEventType.ProjectileSpawn:
                    if (sound && (fx == null || fx.startSound == null)) audioManager.Play(SfxId.Fireball, 0.8f, pan);
                    break;

                case SimEventType.ProjectileClash:
                    effects.Spark(SparkKind.Clash, pos, Color.white, 1.5f, facing);
                    if (sound) audioManager.Play(SfxId.ProjectileClash, 1f, pan);
                    cameraRig.Shake(feel.clashShake);
                    break;

                case SimEventType.ProjectileEnd:
                    effects.Spark(SparkKind.Puff, pos, accent[Mathf.Clamp(e.player, 0, 1)], 0.8f, facing);
                    break;

                case SimEventType.ThrowStart:
                    if (sound) audioManager.Play(SfxId.WhooshHeavy, 0.8f, pan);
                    break;

                case SimEventType.ThrowLand:
                    effects.Spark(SparkKind.Dust, new Vector3(pos.x, 0.2f, -0.5f), new Color(0.8f, 0.7f, 0.6f), 2f, facing);
                    if (sound) { audioManager.Play(SfxId.Throw, 1f, pan); Hurt(e.value, pan); }
                    cameraRig.Shake(feel.throwShake);
                    break;

                case SimEventType.ThrowTech:
                    effects.Spark(SparkKind.Tech, pos, Color.white, 1.4f, facing);
                    if (sound) audioManager.Play(SfxId.ThrowTech, 1f, pan);
                    hud.Popup(0, "THROW TECH", new Color(0.6f, 0.9f, 1f));
                    hud.Popup(1, "THROW TECH", new Color(0.6f, 0.9f, 1f));
                    break;

                case SimEventType.Jump:
                    if (sound) audioManager.Play(SfxId.Jump, 0.35f, pan);
                    break;

                case SimEventType.Land:
                    effects.Spark(SparkKind.Dust, new Vector3(pos.x, 0.1f, -0.5f), new Color(0.8f, 0.75f, 0.7f), 0.6f, facing);
                    if (sound) audioManager.Play(SfxId.Land, 0.4f, pan);
                    break;

                case SimEventType.Dash:
                    effects.Spark(SparkKind.Dust, new Vector3(pos.x, 0.1f, -0.5f), new Color(0.8f, 0.75f, 0.7f), 0.8f, facing);
                    if (sound) audioManager.Play(SfxId.Dash, 0.6f, pan);
                    break;

                case SimEventType.Knockdown:
                    effects.Spark(SparkKind.Dust, new Vector3(pos.x, 0.15f, -0.5f), new Color(0.8f, 0.7f, 0.6f), 1.8f, facing);
                    if (sound) audioManager.Play(SfxId.Knockdown, 0.9f, pan);
                    cameraRig.Shake(feel.knockdownShake);
                    break;

                case SimEventType.SuperFlash:
                    effects.Spark(SparkKind.Burst, pos, new Color(1f, 0.85f, 0.2f), 3f, facing);
                    if (sound)
                    {
                        audioManager.Play(SfxId.SuperFlash, 1f, pan);
                        var v = VoiceOf(e.player);
                        if (v != null && v.superCall != null) audioManager.PlayClip(v.superCall, 1f, pan, 5);
                    }
                    cameraRig.Punch(pos, feel.superZoom);
                    hud.Flash(feel.superFlash);
                    break;

                case SimEventType.KO:
                case SimEventType.DoubleKO:
                    effects.Spark(SparkKind.Burst, pos, new Color(1f, 0.3f, 0.2f), 3.5f, facing);
                    if (sound)
                    {
                        audioManager.Play(SfxId.KO, 1f, pan);
                        var v = VoiceOf(e.player);
                        if (v != null && v.ko != null) audioManager.PlayClip(v.ko, 1f, pan, 5);
                    }
                    cameraRig.Shake(feel.koShake);
                    cameraRig.Punch(pos, 1f);
                    hud.Flash(feel.koFlash);
                    slowmoTicks = feel.koSlowmoFrames;
                    if (!isAttract) hud.Announce(e.type == SimEventType.DoubleKO ? "DOUBLE K.O." : "K.O.", 2f);
                    break;

                case SimEventType.RoundAnnounce:
                    if (isAttract) break;
                    {
                        bool final = sim.fighters[0].roundWins == sim.roundsToWin - 1 && sim.fighters[1].roundWins == sim.roundsToWin - 1;
                        hud.Announce(final ? "FINAL ROUND" : "ROUND " + e.value, 1.4f);
                        audioManager.Play(SfxId.RoundAnnounce, 1f);
                    }
                    break;

                case SimEventType.Fight:
                    if (isAttract) break;
                    hud.Announce("FIGHT!", 0.9f);
                    audioManager.Play(SfxId.Fight, 1f);
                    break;

                case SimEventType.TimeUp:
                    if (isAttract) break;
                    hud.Announce("TIME", 1.5f);
                    audioManager.Play(SfxId.KO, 0.6f);
                    break;

                case SimEventType.RoundWin:
                    if (isAttract) break;
                    {
                        var w = sim.fighters[e.player];
                        bool perfect = w.health >= w.def.maxHealth;
                        hud.Announce(w.def.displayName.ToUpper() + " WINS", 1.8f, perfect ? "PERFECT" : "");
                        audioManager.Play(SfxId.RoundWin, 1f);
                        var v = VoiceOf(e.player);
                        if (v != null && v.win != null) audioManager.PlayClip(v.win, 1f, 0f, 5);
                    }
                    break;

                case SimEventType.Draw:
                    if (!isAttract) hud.Announce("DRAW", 1.8f);
                    break;
            }
        }

        FighterVoice VoiceOf(int player)
        {
            if (player < 0 || player > 1 || assets[player] == null) return null;
            return assets[player].voice;
        }

        //voice lines use unity random on purpose since they are only sound and never touch the fight
        void Shout(int player, float pan)
        {
            var v = VoiceOf(player);
            if (v == null || v.attackShouts == null || v.attackShouts.Length == 0) return;
            if (Random.value > v.shoutChance) return;
            audioManager.PlayClip(v.attackShouts[Random.Range(0, v.attackShouts.Length)], 1f, pan, 3);
        }

        void Hurt(int player, float pan)
        {
            var v = VoiceOf(player);
            if (v == null || v.hurt == null || v.hurt.Length == 0) return;
            audioManager.PlayClip(v.hurt[Random.Range(0, v.hurt.Length)], 1f, pan, 3);
        }
    }
}

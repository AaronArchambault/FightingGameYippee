using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace FightGame
{
    //this is the only thing you need in the scene
    //put it on an empty GameObject and press play and it builds the camera stage audio ui and the game loop
    public class FightingGameBootstrap : MonoBehaviour
    {
        [Tooltip("put your own sounds and music in here and anything left empty uses the made up sounds")]
        public SoundBank soundBank;

        [Tooltip("turn this off if you made your own stage art in the scene")]
        public bool buildPlaceholderStage = true;

        [Tooltip("two cpus fight behind the title screen")]
        public bool attractMode = true;

        [Tooltip("turns off any cameras already in the scene since the game makes its own")]
        public bool replaceExistingCameras = true;

        [Tooltip("vsync keeps the picture smooth and the sim still runs at exactly 60 no matter the refresh rate")]
        public bool vSync = true;

        //it builds everything in Start so any EventSystem already in the scene has had time to turn on
        void Start()
        {
            QualitySettings.vSyncCount = vSync ? 1 : 0;
            if (!vSync) Application.targetFrameRate = 120;

            if (replaceExistingCameras)
            {
                foreach (var c in Camera.allCameras) c.gameObject.SetActive(false);
            }

            if (EventSystem.current == null)
            {
                //it turns the object off while adding the module so the default actions get hooked up before it starts
                var es = new GameObject("EventSystem");
                es.SetActive(false);
                es.AddComponent<EventSystem>();
                var module = es.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
                es.SetActive(true);
            }

            var cam = CameraRig.Create();
            var audio = AudioManager.Create(soundBank);

            Stage stage = null;
            if (buildPlaceholderStage) stage = Stage.Create();
            else stage = new GameObject("StageParallaxNone").AddComponent<Stage>();

            var fxGo = new GameObject("Effects");
            var fx = fxGo.AddComponent<EffectsManager>();
            fx.Init();

            var boxGo = new GameObject("HitboxDebug");
            var boxes = boxGo.AddComponent<HitboxDebugView>();
            boxes.Init();

            var runner = gameObject.AddComponent<GameRunner>();
            runner.camRig = cam;
            runner.fx = fx;
            runner.boxes = boxes;
            runner.hud = FightHUD.Create();
            runner.trainingUI = TrainingOverlay.Create();
            runner.menus = MenuSystem.Create();
            runner.stage = stage;
            runner.audioMgr = audio;
            runner.attractMode = attractMode;
            runner.Init();
        }
    }
}

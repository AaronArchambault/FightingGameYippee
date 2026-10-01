using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using FightCore;

namespace FightGame
{
    //this reads one player's controls with the new input system and turns them into a FrameInput for the sim
    //if you give the manager an Input Actions asset with maps called Player1 and Player2 it uses your bindings
    //if not it builds the default bindings in code so it always works out of the box
    public class PlayerInputSource : IDisposable
    {
        public readonly int player;
        readonly InputActionMap map;
        readonly InputAction[] actions = new InputAction[11];
        static readonly Btn[] bits =
        {
            Btn.Up, Btn.Down, Btn.Left, Btn.Right, Btn.LP, Btn.MP, Btn.HP, Btn.LK, Btn.MK, Btn.HK, Btn.Start
        };

        //these are the action names your asset needs in each map
        public static readonly string[] ActionNames = { "Up", "Down", "Left", "Right", "LP", "MP", "HP", "LK", "MK", "HK", "Start" };

        //this remembers presses that happened between sim frames
        //it is so a super quick tap that goes down and up inside one render frame still counts
        Btn latched;

        public Gamepad AssignedPad { get; private set; }

        public static string MapName(int player) { return "Player" + (player + 1); }

        public PlayerInputSource(int player, InputActionAsset asset = null)
        {
            this.player = player;
            InputActionMap source = asset != null ? asset.FindActionMap(MapName(player)) : null;
            if (source != null)
            {
                //it copies your map so each player gets their own and changing devices does not touch the asset
                map = source.Clone();
            }
            else
            {
                if (asset != null) Debug.LogWarning("Fighting Game: the input asset has no map called " + MapName(player) + " so default controls are used");
                map = new InputActionMap(MapName(player));
                AddDefaultBindings(map, player);
            }
            for (int i = 0; i < ActionNames.Length; i++)
            {
                actions[i] = map.FindAction(ActionNames[i]);
                if (actions[i] == null)
                {
                    Debug.LogWarning("Fighting Game: input map " + MapName(player) + " is missing the action " + ActionNames[i]);
                    actions[i] = map.AddAction(ActionNames[i], InputActionType.Button);
                }
            }
            AssignDevices();
            map.Enable();
        }

        //this adds the default actions and bindings to a map
        //the editor also uses it to make an Input Actions asset you can edit
        public static void AddDefaultBindings(InputActionMap map, int player)
        {
            for (int i = 0; i < ActionNames.Length; i++)
                if (map.FindAction(ActionNames[i]) == null) map.AddAction(ActionNames[i], InputActionType.Button);

            if (player == 0)
            {
                //player one keyboard is WASD to move with U I O for punches and J K L for kicks
                Bind(map, "Up", "<Keyboard>/w"); Bind(map, "Down", "<Keyboard>/s"); Bind(map, "Left", "<Keyboard>/a"); Bind(map, "Right", "<Keyboard>/d");
                Bind(map, "LP", "<Keyboard>/u"); Bind(map, "MP", "<Keyboard>/i"); Bind(map, "HP", "<Keyboard>/o");
                Bind(map, "LK", "<Keyboard>/j"); Bind(map, "MK", "<Keyboard>/k"); Bind(map, "HK", "<Keyboard>/l");
                Bind(map, "Start", "<Keyboard>/escape");
            }
            else
            {
                //player two keyboard is the arrow keys with numpad 4 5 6 for punches and numpad 1 2 3 for kicks
                Bind(map, "Up", "<Keyboard>/upArrow"); Bind(map, "Down", "<Keyboard>/downArrow"); Bind(map, "Left", "<Keyboard>/leftArrow"); Bind(map, "Right", "<Keyboard>/rightArrow");
                Bind(map, "LP", "<Keyboard>/numpad4"); Bind(map, "MP", "<Keyboard>/numpad5"); Bind(map, "HP", "<Keyboard>/numpad6");
                Bind(map, "LK", "<Keyboard>/numpad1"); Bind(map, "MK", "<Keyboard>/numpad2"); Bind(map, "HK", "<Keyboard>/numpad3");
                Bind(map, "Start", "<Keyboard>/backspace");
            }

            //this is the gamepad layout and it is kind of like the classic six button street fighter layout
            //the face buttons are light and medium and the right bumper and trigger are the heavies
            Bind(map, "Up", "<Gamepad>/dpad/up"); Bind(map, "Down", "<Gamepad>/dpad/down"); Bind(map, "Left", "<Gamepad>/dpad/left"); Bind(map, "Right", "<Gamepad>/dpad/right");
            Bind(map, "Up", "<Gamepad>/leftStick/up"); Bind(map, "Down", "<Gamepad>/leftStick/down"); Bind(map, "Left", "<Gamepad>/leftStick/left"); Bind(map, "Right", "<Gamepad>/leftStick/right");
            Bind(map, "LP", "<Gamepad>/buttonWest"); Bind(map, "MP", "<Gamepad>/buttonNorth"); Bind(map, "HP", "<Gamepad>/rightShoulder");
            Bind(map, "LK", "<Gamepad>/buttonSouth"); Bind(map, "MK", "<Gamepad>/buttonEast"); Bind(map, "HK", "<Gamepad>/rightTrigger");
            Bind(map, "Start", "<Gamepad>/start");
        }

        static void Bind(InputActionMap map, string action, string path)
        {
            map.FindAction(action).AddBinding(path);
        }

        //this gives each player their own gamepad so both pads do not control both fighters
        //player one gets the first pad and player two gets the second one
        public void AssignDevices()
        {
            var pads = Gamepad.all;
            AssignedPad = pads.Count > player ? pads[player] : null;
            int count = (Keyboard.current != null ? 1 : 0) + (AssignedPad != null ? 1 : 0);
            var devices = new InputDevice[count];
            int n = 0;
            if (Keyboard.current != null) devices[n++] = Keyboard.current;
            if (AssignedPad != null) devices[n++] = AssignedPad;
            map.devices = new ReadOnlyArray<InputDevice>(devices);
        }

        //this is called every render frame so quick taps get caught
        public void Poll()
        {
            for (int i = 0; i < actions.Length; i++)
                if (actions[i].WasPressedThisFrame()) latched |= bits[i];
        }

        //this is called right when the sim needs input for a frame
        public FrameInput Read()
        {
            Btn b = latched;
            latched = Btn.None;
            for (int i = 0; i < actions.Length; i++)
                if (actions[i].IsPressed()) b |= bits[i];
            //the start button is not part of the fight itself so it gets taken out here
            b &= ~Btn.Start;
            return FrameInput.Clean(b);
        }

        public bool StartPressed { get { return actions[10].WasPressedThisFrame(); } }

        public void Dispose()
        {
            map.Disable();
            map.Dispose();
        }
    }
}

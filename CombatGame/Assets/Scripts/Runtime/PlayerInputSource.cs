using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using FightCore;

namespace FightGame
{
    //this reads one player's controls with the new input system and turns them into a FrameInput for the sim
    //it builds all the actions in code so you do not need to set up an input actions asset to get going
    public class PlayerInputSource : IDisposable
    {
        public readonly int player;
        readonly InputActionMap map;
        readonly InputAction[] actions = new InputAction[11];
        static readonly Btn[] bits =
        {
            Btn.Up, Btn.Down, Btn.Left, Btn.Right, Btn.LP, Btn.MP, Btn.HP, Btn.LK, Btn.MK, Btn.HK, Btn.Start
        };
        static readonly string[] names = { "Up", "Down", "Left", "Right", "LP", "MP", "HP", "LK", "MK", "HK", "Start" };

        //this remembers presses that happened between sim frames
        //it is so a super quick tap that goes down and up inside one render frame still counts
        Btn latched;

        public Gamepad AssignedPad { get; private set; }

        public PlayerInputSource(int player)
        {
            this.player = player;
            map = new InputActionMap("Player" + (player + 1));
            for (int i = 0; i < names.Length; i++) actions[i] = map.AddAction(names[i], InputActionType.Button);

            if (player == 0)
            {
                //player one keyboard is WASD to move with U I O for punches and J K L for kicks
                Bind("Up", "<Keyboard>/w"); Bind("Down", "<Keyboard>/s"); Bind("Left", "<Keyboard>/a"); Bind("Right", "<Keyboard>/d");
                Bind("LP", "<Keyboard>/u"); Bind("MP", "<Keyboard>/i"); Bind("HP", "<Keyboard>/o");
                Bind("LK", "<Keyboard>/j"); Bind("MK", "<Keyboard>/k"); Bind("HK", "<Keyboard>/l");
                Bind("Start", "<Keyboard>/escape");
            }
            else
            {
                //player two keyboard is the arrow keys with numpad 4 5 6 for punches and numpad 1 2 3 for kicks
                Bind("Up", "<Keyboard>/upArrow"); Bind("Down", "<Keyboard>/downArrow"); Bind("Left", "<Keyboard>/leftArrow"); Bind("Right", "<Keyboard>/rightArrow");
                Bind("LP", "<Keyboard>/numpad4"); Bind("MP", "<Keyboard>/numpad5"); Bind("HP", "<Keyboard>/numpad6");
                Bind("LK", "<Keyboard>/numpad1"); Bind("MK", "<Keyboard>/numpad2"); Bind("HK", "<Keyboard>/numpad3");
                Bind("Start", "<Keyboard>/backspace");
            }

            //this is the gamepad layout and it is kind of like the classic six button street fighter layout
            //the face buttons are light and medium and the right bumper and trigger are the heavies
            Bind("Up", "<Gamepad>/dpad/up"); Bind("Down", "<Gamepad>/dpad/down"); Bind("Left", "<Gamepad>/dpad/left"); Bind("Right", "<Gamepad>/dpad/right");
            Bind("Up", "<Gamepad>/leftStick/up"); Bind("Down", "<Gamepad>/leftStick/down"); Bind("Left", "<Gamepad>/leftStick/left"); Bind("Right", "<Gamepad>/leftStick/right");
            Bind("LP", "<Gamepad>/buttonWest"); Bind("MP", "<Gamepad>/buttonNorth"); Bind("HP", "<Gamepad>/rightShoulder");
            Bind("LK", "<Gamepad>/buttonSouth"); Bind("MK", "<Gamepad>/buttonEast"); Bind("HK", "<Gamepad>/rightTrigger");
            Bind("Start", "<Gamepad>/start");

            AssignDevices();
            map.Enable();
        }

        void Bind(string action, string path)
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

using UnityEngine;
using UnityEngine.UI;

namespace FightGame
{
    public enum MenuAction
    {
        VersusCPU, VersusPlayer, Training, CPUvsCPU, WatchReplay, Quit,
        StartFight, BackToMain, Resume, Restart, MoveList, BackToPause, CharacterSelect, MainMenu
    }

    //put this on any Button and pick what it does
    //the MenuSystem hooks it up when the game starts so you do not need to set up OnClick yourself
    [RequireComponent(typeof(Button))]
    public class MenuButton : MonoBehaviour
    {
        public MenuAction action;
    }
}

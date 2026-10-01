using UnityEngine;
using UnityEngine.UI;

namespace FightGame
{
    public enum MenuPanelId { Main, Select, Pause, MoveList, Results }

    //put this on the root of each menu screen so the game knows which screen it is
    public class MenuPanel : MonoBehaviour
    {
        public MenuPanelId id;
        [Tooltip("what gets selected when this screen opens so pads and keyboards work right away")]
        public Selectable firstSelected;
    }
}

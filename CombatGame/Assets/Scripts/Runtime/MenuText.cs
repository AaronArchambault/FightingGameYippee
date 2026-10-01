using UnityEngine;
using UnityEngine.UI;

namespace FightGame
{
    public enum MenuTextId { ModeTitle, ResultTitle, ResultSub, MoveList, MainNote, P1Description, P2Description }

    //put this on a Text the game should fill in like the results title or the move list
    [RequireComponent(typeof(Text))]
    public class MenuText : MonoBehaviour
    {
        public MenuTextId id;
    }
}

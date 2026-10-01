using UnityEngine;
using UnityEngine.UI;

namespace FightGame
{
    public enum MenuImageId { P1Portrait, P2Portrait }

    //put this on an Image to show a fighter portrait on the select screen
    [RequireComponent(typeof(Image))]
    public class MenuImage : MonoBehaviour
    {
        public MenuImageId id;
    }
}

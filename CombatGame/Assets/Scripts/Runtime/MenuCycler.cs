using UnityEngine;
using UnityEngine.UI;

namespace FightGame
{
    public enum CyclerTarget { P1Fighter, P2Fighter, CpuLevel }

    //put this on a CyclerButton to make it pick a fighter or the cpu level
    [RequireComponent(typeof(CyclerButton))]
    public class MenuCycler : MonoBehaviour
    {
        public CyclerTarget target;
        [Tooltip("the text that shows the current value")]
        public Text label;
    }
}

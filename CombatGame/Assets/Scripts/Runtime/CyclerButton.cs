using System;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FightGame
{
    //this is a button you can also push left and right on to change a value
    //it is for stuff like picking your fighter or the cpu level with a stick or the arrow keys
    public class CyclerButton : Button
    {
        public Action<int> onCycle;

        public override void OnMove(AxisEventData e)
        {
            if (e.moveDir == MoveDirection.Left) { if (onCycle != null) onCycle(-1); e.Use(); return; }
            if (e.moveDir == MoveDirection.Right) { if (onCycle != null) onCycle(1); e.Use(); return; }
            base.OnMove(e);
        }
    }
}

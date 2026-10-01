using System.Collections.Generic;
using UnityEngine;

namespace FightGame
{
    //this is the list of everyone you can pick
    [CreateAssetMenu(menuName = "Fighting Game/Roster", fileName = "FightingGameRoster")]
    public class RosterAsset : ScriptableObject
    {
        public List<FighterAsset> fighters = new List<FighterAsset>();
    }
}

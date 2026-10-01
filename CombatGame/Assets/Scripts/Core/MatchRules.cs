using System;

namespace FightCore
{
    //these are the match rules you can change in the inspector
    //they get copied into the match when it starts and saved inside replays so a replay always plays back with the same rules
    [Serializable]
    public class MatchRules
    {
        //how many rounds you need to win the match so 2 means best of 3
        public int roundsToWin = 2;

        //how long each round is in seconds and 0 means no timer at all
        public int roundSeconds = 99;

        //this is half the stage width in sim units where 1000 is one unity unit
        public int stageHalfWidth = 7200;

        //this is the furthest apart the two fighters can get so they stay on one screen
        public int maxSeparation = 6000;

        //how many extra hits you can land on someone already flying through the air
        public int juggleLimit = 3;

        //how many frames the round intro and the round end pause last
        public int introFrames = 100;
        public int roundEndFrames = 200;

        //training mode helpers
        public bool trainingInfiniteMeter = true;
        public bool trainingRefillHealth = true;

        public MatchRules Clone()
        {
            return (MatchRules)MemberwiseClone();
        }

        //this keeps silly numbers out so the sim can not break
        public void Sanitize()
        {
            if (roundsToWin < 1) roundsToWin = 1;
            if (roundsToWin > 5) roundsToWin = 5;
            if (roundSeconds < 0) roundSeconds = 0;
            if (stageHalfWidth < 3000) stageHalfWidth = 3000;
            if (maxSeparation < 2000) maxSeparation = 2000;
            if (juggleLimit < 0) juggleLimit = 0;
            if (introFrames < 1) introFrames = 1;
            if (roundEndFrames < 1) roundEndFrames = 1;
        }
    }
}

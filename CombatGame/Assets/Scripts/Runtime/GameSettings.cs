using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FightGame
{
    //these are the game feel numbers you can tune in the inspector on the FightGameManager
    //none of these change the fight itself they only change how it looks and sounds
    [Serializable]
    public class GameFeelSettings
    {
        [Header("Screen Shake")]
        public float hitShake = 0.12f;
        [Tooltip("extra shake added for each step of hit strength")]
        public float hitShakePerLevel = 0.12f;
        public float counterHitBonus = 0.2f;
        public float blockShake = 0.08f;
        public float throwShake = 0.5f;
        public float knockdownShake = 0.3f;
        public float clashShake = 0.25f;
        public float koShake = 1f;

        [Header("KO")]
        [Tooltip("how many sim frames play in slow motion after a ko")]
        [Min(0)] public int koSlowmoFrames = 50;
        [Tooltip("how much slower and 2 means half speed")]
        [Min(1f)] public float koSlowmoFactor = 2.5f;
        [Range(0f, 1f)] public float koFlash = 0.8f;

        [Header("Super")]
        [Range(0f, 1f)] public float superFlash = 0.25f;
        [Range(0f, 1f)] public float superZoom = 1f;

        [Header("Trails and Sounds")]
        public bool dashTrails = true;
        [Min(1)] public int trailEveryFrames = 3;
        [Tooltip("plays a whoosh when any attack starts")]
        public bool attackWhooshes = true;
        [Range(0f, 1f)] public float whooshVolume = 0.5f;
    }

    //these are the training mode hotkeys and you can change them to whatever you want
    [Serializable]
    public class TrainingKeys
    {
        public Key toggleHitboxes = Key.F1;
        public Key toggleFrameData = Key.F2;
        public Key toggleInputs = Key.F3;
        public Key dummyStance = Key.F4;
        public Key dummyBlock = Key.F5;
        public Key record = Key.F6;
        public Key playback = Key.F7;
        public Key wakeupReversal = Key.F8;
        public Key resetPositions = Key.F9;
        public Key freeze = Key.F10;
        public Key stepFrame = Key.F11;
        public Key cycleCpuLevel = Key.Tab;
        [Tooltip("hold this while resetting to put the dummy in the left corner")]
        public Key cornerLeft = Key.A;
        [Tooltip("hold this while resetting to put the dummy in the right corner")]
        public Key cornerRight = Key.D;

        public string HelpText()
        {
            return toggleHitboxes + " Boxes   " + toggleFrameData + " Frame Data   " + toggleInputs + " Inputs   " + dummyStance + " Dummy Stance   "
                 + dummyBlock + " Dummy Block   " + record + " Record   " + playback + " Playback   " + wakeupReversal + " Wakeup DP   "
                 + resetPositions + " Reset (hold " + cornerLeft + " or " + cornerRight + " for corner)   " + freeze + " Pause   " + stepFrame + " Step   " + cycleCpuLevel + " CPU Level";
        }
    }
}

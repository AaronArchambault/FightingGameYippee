using System;
using System.Collections.Generic;
using UnityEngine;

namespace FightGame
{
    //this is where you put your real sound files
    //if a sound is missing here it just uses the made up one from ProceduralSfx so nothing is ever silent
    [CreateAssetMenu(menuName = "Fighting Game/Sound Bank", fileName = "FightingGameSounds")]
    public class SoundBank : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public SfxId id;
            [Tooltip("if you put more than one it picks one at random so repeated hits do not sound robotic")]
            public AudioClip[] clips;
            [Range(0f, 1f)] public float volume = 1f;
            [Range(0f, 0.3f)] public float pitchVariance = 0.06f;
        }

        public List<Entry> entries = new List<Entry>();
        public AudioClip battleMusic;
        public AudioClip menuMusic;
    }
}

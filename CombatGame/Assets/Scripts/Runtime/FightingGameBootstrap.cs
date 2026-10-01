using UnityEngine;

namespace FightGame
{
    //this is the old way to start the game and it is only here so older scenes keep working
    //it just adds a FightGameManager if the scene does not have one
    //for new scenes use Tools then Fighting Game then Build Scene Objects instead
    [DefaultExecutionOrder(-100)]
    public class FightingGameBootstrap : MonoBehaviour
    {
        public SoundBank soundBank;

        void Awake()
        {
            if (FightUtil.FindInScene<FightGameManager>() != null) return;
            if (soundBank != null && FightUtil.FindInScene<AudioManager>() == null) AudioManager.Create(soundBank);
            gameObject.AddComponent<FightGameManager>();
        }
    }
}

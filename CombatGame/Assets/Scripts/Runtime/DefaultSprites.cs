using UnityEngine;

namespace FightGame
{
    //this holds the basic shapes as real sprite files
    //the scene builder makes this for you and puts it in Resources so it loads by itself
    [CreateAssetMenu(menuName = "Fighting Game/Default Sprites", fileName = "FightingGameDefaultSprites")]
    public class DefaultSprites : ScriptableObject
    {
        public const string ResourcePath = "FightingGameDefaultSprites";
        public Sprite square;
        public Sprite squareTop;
        public Sprite circle;
        public Sprite softCircle;
        public Sprite ring;
    }
}

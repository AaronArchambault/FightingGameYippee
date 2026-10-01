using UnityEngine;

namespace FightGame
{
    //this is the base for any effect that gets pooled like hit sparks dust and trails
    //to make your own effect inherit from this or just use SpriteSheetEffect on a prefab
    public class PooledEffect : MonoBehaviour
    {
        //facing is 1 when the attacker faces right and negative 1 when they face left so effects can point the right way
        public virtual void Play(Vector3 position, Color tint, float scale, int facing)
        {
            transform.position = position;
        }

        //it returns false when the effect is finished so the pool can take it back
        public virtual bool Tick(float dt) { return false; }
    }
}

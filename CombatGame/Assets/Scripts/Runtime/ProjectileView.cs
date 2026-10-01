using UnityEngine;
using FightCore;

namespace FightGame
{
    //this is the base for how a fireball looks
    //the built in look is ProceduralProjectileView and SpriteProjectileView uses your own sprites
    //to make your own inherit from this and override Sync
    public class ProjectileView : MonoBehaviour
    {
        //this is called once when the pool first makes it
        public virtual void Init() { }

        //this is called when a new fireball starts using this view
        public virtual void Begin(Projectile p) { }

        //this is called every render frame while the fireball is alive
        public virtual void Sync(Projectile p, Color ownerColor, int simFrame)
        {
            var b = p.def.box;
            transform.position = FightUtil.ToWorld(p.x + p.dir * b.cx, p.y + b.cy, -0.1f);
        }
    }
}

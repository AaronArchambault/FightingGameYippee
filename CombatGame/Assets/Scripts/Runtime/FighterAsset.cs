using System;
using System.Collections.Generic;
using UnityEngine;
using FightCore;

namespace FightGame
{
    //these are the sound and effect overrides for one move
    //anything you leave empty just uses the normal sound or spark
    [Serializable]
    public class MoveFx
    {
        [Tooltip("the move id like fb_h or st_lp and you can see them in the hitbox editor")]
        public string moveId;
        public AudioClip startSound;
        public AudioClip hitSound;
        [Tooltip("a voice line like the name of the special move")]
        public AudioClip voice;
        [Tooltip("a prefab with a PooledEffect on it that plays when this move hits")]
        public PooledEffect hitEffect;
        [Tooltip("a prefab with a ProjectileView on it for fireballs from this move")]
        public ProjectileView projectilePrefab;
    }

    //these are the voice clips for a fighter
    [Serializable]
    public class FighterVoice
    {
        [Tooltip("random shouts when doing heavy attacks and specials")]
        public AudioClip[] attackShouts;
        [Range(0f, 1f)] public float shoutChance = 0.35f;
        public AudioClip[] hurt;
        public AudioClip ko;
        public AudioClip win;
        public AudioClip superCall;
    }

    //this is a fighter saved as an asset so you can edit their moves and boxes in the editor
    //the actual fight data is a FighterDef so the sim can use it without knowing about unity
    //everything else on here is just how the fighter looks and sounds
    [CreateAssetMenu(menuName = "Fighting Game/Fighter", fileName = "NewFighter")]
    public class FighterAsset : ScriptableObject
    {
        public FighterDef def = new FighterDef();

        [Header("Looks")]
        [Tooltip("your own sprite sheet animations and if this is set the fighter uses them automatically")]
        public SpriteAnimationSet animations;

        [Tooltip("a prefab with any FighterView on it and this wins over everything else so you can fully customize how the fighter is drawn")]
        public FighterView viewPrefab;

        [Tooltip("optional portrait for the select screen")]
        public Sprite portrait;

        [Header("Sounds and Effects")]
        public FighterVoice voice = new FighterVoice();
        public List<MoveFx> moveFx = new List<MoveFx>();

        [NonSerialized] Dictionary<string, MoveFx> fxLookup;

        void OnValidate() { fxLookup = null; }

        public MoveFx FxFor(string moveId)
        {
            if (string.IsNullOrEmpty(moveId) || moveFx == null) return null;
            if (fxLookup == null)
            {
                fxLookup = new Dictionary<string, MoveFx>();
                foreach (var f in moveFx) if (f != null && !string.IsNullOrEmpty(f.moveId)) fxLookup[f.moveId] = f;
            }
            MoveFx fx;
            return fxLookup.TryGetValue(moveId, out fx) ? fx : null;
        }

        //it makes a copy for the match so nothing that happens in a fight changes the saved asset
        public FighterDef CreateRuntimeCopy()
        {
            var copy = JsonUtility.FromJson<FighterDef>(JsonUtility.ToJson(def));
            copy.Rebuild();
            return copy;
        }
    }

    //this is one entry on the select screen and it works if you have assets or not
    public class RosterEntry
    {
        public FighterAsset asset;
        public FighterDef source;

        public string Id { get { return source.id; } }
        public string Name { get { return source.displayName; } }

        public FighterDef MakeDef()
        {
            if (asset != null) return asset.CreateRuntimeCopy();
            var copy = JsonUtility.FromJson<FighterDef>(JsonUtility.ToJson(source));
            copy.Rebuild();
            return copy;
        }
    }

    //this loads the roster you put on the manager or the one in Resources and if there is none it uses the one built in code
    public static class RosterLoader
    {
        public const string ResourcePath = "FightingGameRoster";

        public static List<RosterEntry> Load(RosterAsset roster = null)
        {
            var list = new List<RosterEntry>();
            if (roster == null) roster = Resources.Load<RosterAsset>(ResourcePath);
            if (roster != null)
            {
                foreach (var fa in roster.fighters)
                    if (fa != null && fa.def != null) list.Add(new RosterEntry { asset = fa, source = fa.def });
            }
            if (list.Count == 0)
            {
                foreach (var d in RosterFactory.CreateDefaultRoster()) list.Add(new RosterEntry { source = d });
            }
            return list;
        }

        public static int IndexOf(List<RosterEntry> list, string id)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].Id == id) return i;
            return 0;
        }
    }
}

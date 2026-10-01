using System.Collections.Generic;
using UnityEngine;
using FightCore;

namespace FightGame
{
    //this is a fighter saved as an asset so you can edit their moves and boxes in the editor
    //the actual data is a FighterDef so the sim can use it without knowing about unity
    [CreateAssetMenu(menuName = "Fighting Game/Fighter", fileName = "NewFighter")]
    public class FighterAsset : ScriptableObject
    {
        public FighterDef def = new FighterDef();

        [Tooltip("optional prefab with a SpriteRenderer or model that replaces the placeholder body")]
        public GameObject viewPrefab;

        [Tooltip("optional animator where each state is named the same as the move id like st_lp or idle")]
        public RuntimeAnimatorController animator;

        [Tooltip("optional portrait for the select screen")]
        public Sprite portrait;

        //it makes a copy for the match so nothing you do in a fight changes the saved asset
        public FighterDef CreateRuntimeCopy()
        {
            var json = JsonUtility.ToJson(def);
            var copy = JsonUtility.FromJson<FighterDef>(json);
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

    //this loads the roster from Resources if you made the assets and if not it just uses the one built in code
    public static class RosterLoader
    {
        public const string ResourcePath = "FightingGameRoster";

        public static List<RosterEntry> Load()
        {
            var list = new List<RosterEntry>();
            var roster = Resources.Load<RosterAsset>(ResourcePath);
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

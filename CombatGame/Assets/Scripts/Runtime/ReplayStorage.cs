using System.IO;
using UnityEngine;
using FightCore;

namespace FightGame
{
    //this saves and loads replays as json files
    //since the sim is deterministic a replay is just the fighters plus the rules plus every input which is tiny
    public static class ReplayStorage
    {
        public static string Folder { get { return Path.Combine(Application.persistentDataPath, "Replays"); } }
        public static string LastPath { get { return Path.Combine(Folder, "last_match.json"); } }

        public static void Save(ReplayData data)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var json = JsonUtility.ToJson(data);
                File.WriteAllText(LastPath, json);
                //it also keeps a copy with the time in the name so you have a history
                var stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
                File.WriteAllText(Path.Combine(Folder, "replay_" + stamp + ".json"), json);
            }
            catch (System.Exception e) { Debug.LogWarning("Could not save replay: " + e.Message); }
        }

        public static ReplayData LoadLast()
        {
            try
            {
                if (!File.Exists(LastPath)) return null;
                return JsonUtility.FromJson<ReplayData>(File.ReadAllText(LastPath));
            }
            catch (System.Exception e) { Debug.LogWarning("Could not load replay: " + e.Message); return null; }
        }
    }
}

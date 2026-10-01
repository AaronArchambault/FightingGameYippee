using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FightCore;

namespace FightGame.EditorTools
{
    //these are the menu items under Tools so setting up the project is a couple of clicks
    public static class FightingGameMenu
    {
        const string Root = "Assets/FightingGame";
        const string FighterFolder = Root + "/Fighters";
        const string ResourcesFolder = Root + "/Resources";

        //this turns the code roster into real assets you can edit with the hitbox editor
        [MenuItem("Tools/Fighting Game/Create Default Roster Assets")]
        public static void CreateRoster()
        {
            EnsureFolder(Root, "Fighters");
            EnsureFolder(Root, "Resources");

            var roster = AssetDatabase.LoadAssetAtPath<RosterAsset>(ResourcesFolder + "/" + RosterLoader.ResourcePath + ".asset");
            if (roster == null)
            {
                roster = ScriptableObject.CreateInstance<RosterAsset>();
                AssetDatabase.CreateAsset(roster, ResourcesFolder + "/" + RosterLoader.ResourcePath + ".asset");
            }

            int made = 0;
            foreach (var def in RosterFactory.CreateDefaultRoster())
            {
                string path = FighterFolder + "/" + def.id + ".asset";
                var fa = AssetDatabase.LoadAssetAtPath<FighterAsset>(path);
                //it never overwrites a fighter you already made so your edits are safe
                if (fa == null)
                {
                    fa = ScriptableObject.CreateInstance<FighterAsset>();
                    fa.def = def;
                    AssetDatabase.CreateAsset(fa, path);
                    made++;
                }
                if (!roster.fighters.Contains(fa)) roster.fighters.Add(fa);
            }
            EditorUtility.SetDirty(roster);
            AssetDatabase.SaveAssets();
            Selection.activeObject = roster;
            Debug.Log("Fighting Game: made " + made + " fighter assets and the roster is in " + ResourcesFolder);
        }

        [MenuItem("Tools/Fighting Game/Create Sound Bank")]
        public static void CreateSoundBank()
        {
            EnsureFolder(Root, "Audio");
            var bank = ScriptableObject.CreateInstance<SoundBank>();
            foreach (SfxId id in System.Enum.GetValues(typeof(SfxId)))
                bank.entries.Add(new SoundBank.Entry { id = id, clips = new AudioClip[0] });
            string path = AssetDatabase.GenerateUniqueAssetPath(Root + "/Audio/FightingGameSounds.asset");
            AssetDatabase.CreateAsset(bank, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = bank;

            //it also hooks the bank up to the bootstrap in the open scene if there is one
            var boot = FindOne<FightingGameBootstrap>();
            if (boot != null && boot.soundBank == null)
            {
                Undo.RecordObject(boot, "Assign Sound Bank");
                boot.soundBank = bank;
                EditorSceneManager.MarkSceneDirty(boot.gameObject.scene);
            }
        }

        //this adds the bootstrap to the open scene and removes the default camera since the game makes its own
        [MenuItem("Tools/Fighting Game/Set Up Current Scene")]
        public static void SetupScene()
        {
            if (FindOne<FightingGameBootstrap>() != null)
            {
                Debug.Log("Fighting Game: the scene already has a FightingGameBootstrap");
                return;
            }
            var go = new GameObject("FightingGame");
            go.AddComponent<FightingGameBootstrap>();
            Undo.RegisterCreatedObjectUndo(go, "Add Fighting Game");
            foreach (var cam in FindAll<Camera>())
                if (cam.name == "Main Camera") Undo.DestroyObjectImmediate(cam.gameObject);
            Selection.activeObject = go;
            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log("Fighting Game: scene is ready so press Play");
        }

        //this writes every move's frame data into a spreadsheet file so you can balance in a table
        [MenuItem("Tools/Fighting Game/Export Frame Data CSV")]
        public static void ExportCsv()
        {
            string path = EditorUtility.SaveFilePanel("Export Frame Data", "", "frame_data.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            var sb = new StringBuilder();
            sb.AppendLine("Fighter,Move Id,Name,Input,Kind,Startup,Active,Recovery,Total,Damage,Chip,Hitstun,Blockstun,On Hit,On Block,Guard,Hit Effect,Invincible,Meter Cost");
            foreach (var e in RosterLoader.Load())
            {
                var d = e.source;
                foreach (var m in d.moves)
                {
                    int after = m.TotalFrames - m.startup;
                    string inv = m.invulnEnd > 0 ? m.invulnStart + " to " + m.invulnEnd : "";
                    sb.Append(Csv(d.displayName)).Append(',').Append(Csv(m.id)).Append(',').Append(Csv(m.displayName)).Append(',')
                      .Append(Csv(GameRunner.Notation(m))).Append(',').Append(m.isSuper ? "Super" : m.kind.ToString()).Append(',')
                      .Append(m.startup).Append(',').Append(m.active).Append(',').Append(m.recovery).Append(',').Append(m.TotalFrames).Append(',')
                      .Append(m.hit.damage).Append(',').Append(m.hit.chip).Append(',').Append(m.hit.hitstun).Append(',').Append(m.hit.blockstun).Append(',')
                      .Append(m.hit.hitstun - after).Append(',').Append(m.hit.blockstun - after).Append(',')
                      .Append(m.hit.guard).Append(',').Append(m.hit.effect).Append(',').Append(Csv(inv)).Append(',').Append(m.meterCost).AppendLine();
                }
            }
            File.WriteAllText(path, sb.ToString());
            Debug.Log("Fighting Game: frame data saved to " + path);
            EditorUtility.RevealInFinder(path);
        }

        //unity 2023 renamed the find functions so this picks the right one and avoids warnings
        static T FindOne<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<T>();
#else
            return Object.FindObjectOfType<T>();
#endif
        }

        static T[] FindAll<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<T>();
#endif
        }

        static string Csv(string s)
        {
            if (s == null) return "";
            return s.Contains(",") || s.Contains("\"") ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}

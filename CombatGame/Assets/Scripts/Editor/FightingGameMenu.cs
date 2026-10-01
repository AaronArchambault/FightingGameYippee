using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using FightCore;

namespace FightGame.EditorTools
{
    //these are the menu items under Tools so setting up the project is a few clicks
    //everything they make goes in Assets/FightingGameData so your own stuff stays apart from the code
    public static class FightingGameMenu
    {
        public const string DataRoot = "Assets/FightingGameData";
        const string Menu = "Tools/Fighting Game/";

        //this is the main one and it builds every scene piece as real objects you can see and edit
        [MenuItem(Menu + "Build Scene Objects", false, 0)]
        public static void BuildScene()
        {
            if (FindOne<FightGameManager>() != null)
            {
                EditorUtility.DisplayDialog("Fighting Game", "This scene already has a FightGameManager. Delete it first if you want to rebuild from scratch.", "OK");
                return;
            }
            SpriteFactory.Saved = EnsureDefaultSprites();

            //it removes the default camera since the game uses its own camera rig
            foreach (var cam in FindAll<Camera>())
                if (cam.GetComponent<CameraRig>() == null) Undo.DestroyObjectImmediate(cam.gameObject);
            foreach (var old in FindAll<FightingGameBootstrap>()) Undo.DestroyObjectImmediate(old.gameObject);

            var root = new GameObject("Fight Game");
            Undo.RegisterCreatedObjectUndo(root, "Build Fighting Game Scene");
            var mgr = root.AddComponent<FightGameManager>();

            var camGo = new GameObject("Main Camera");
            camGo.transform.SetParent(root.transform, false);
            camGo.tag = "MainCamera";
            var c = camGo.AddComponent<Camera>();
            c.orthographic = true;
            c.orthographicSize = 3.4f;
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(0.08f, 0.08f, 0.12f);
            c.nearClipPlane = 0.1f;
            c.farClipPlane = 100f;
            camGo.transform.position = new Vector3(0f, 2.6f, -10f);
            camGo.AddComponent<AudioListener>();
            mgr.cameraRig = camGo.AddComponent<CameraRig>();

            var audioGo = new GameObject("Audio");
            audioGo.transform.SetParent(root.transform, false);
            mgr.audioManager = audioGo.AddComponent<AudioManager>();
            var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(DataRoot + "/Audio/FightingGameSounds.asset");
            if (bank != null) mgr.audioManager.bank = bank;

            var stageGo = new GameObject("Stage");
            stageGo.transform.SetParent(root.transform, false);
            mgr.stage = stageGo.AddComponent<StageView>();
            mgr.stage.BuildPlaceholder();

            var fxGo = new GameObject("Effects");
            fxGo.transform.SetParent(root.transform, false);
            mgr.effects = fxGo.AddComponent<EffectsManager>();

            var boxGo = new GameObject("Hitbox Debug");
            boxGo.transform.SetParent(root.transform, false);
            mgr.hitboxView = boxGo.AddComponent<HitboxDebugView>();

            var fighters = new GameObject("Fighters");
            fighters.transform.SetParent(root.transform, false);
            mgr.fighterParent = fighters.transform;

            var ui = new GameObject("UI");
            ui.transform.SetParent(root.transform, false);
            var hudCanvas = UIFactory.Canvas("Fight HUD", 10);
            hudCanvas.transform.SetParent(ui.transform, false);
            mgr.hud = hudCanvas.gameObject.AddComponent<FightHUD>();
            mgr.hud.BuildDefault();
            var trCanvas = UIFactory.Canvas("Training Overlay", 11);
            trCanvas.transform.SetParent(ui.transform, false);
            mgr.trainingOverlay = trCanvas.gameObject.AddComponent<TrainingOverlay>();
            mgr.trainingOverlay.BuildDefault();
            var menuCanvas = UIFactory.Canvas("Menus", 20);
            menuCanvas.transform.SetParent(ui.transform, false);
            mgr.menus = menuCanvas.gameObject.AddComponent<MenuSystem>();
            mgr.menus.BuildDefault();

            if (FindOne<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.transform.SetParent(ui.transform, false);
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            mgr.roster = AssetDatabase.LoadAssetAtPath<RosterAsset>(DataRoot + "/Resources/" + RosterLoader.ResourcePath + ".asset");
            mgr.inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(DataRoot + "/FightingGameControls.inputactions");

            Selection.activeObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("Fighting Game: scene built so press Play and everything under Fight Game can be edited");
        }

        //this saves the basic shapes as png files so scene objects keep their sprites after saving
        public static DefaultSprites EnsureDefaultSprites()
        {
            string assetPath = DataRoot + "/Resources/" + DefaultSprites.ResourcePath + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<DefaultSprites>(assetPath);
            if (existing != null && existing.square != null) return existing;
            EnsureFolder(DataRoot + "/Resources");
            EnsureFolder(DataRoot + "/Generated");
            SpriteFactory.Saved = null;
            var ds = existing != null ? existing : ScriptableObject.CreateInstance<DefaultSprites>();
            ds.square = SavePng("Square", SpriteFactory.SquareTexture(), new Vector2(0.5f, 0.5f), 4f, FilterMode.Point);
            ds.squareTop = SavePng("SquareTop", SpriteFactory.SquareTexture(), new Vector2(0.5f, 1f), 4f, FilterMode.Point);
            ds.circle = SavePng("Circle", SpriteFactory.CircleTexture(0f, false), new Vector2(0.5f, 0.5f), 64f, FilterMode.Bilinear);
            ds.softCircle = SavePng("SoftCircle", SpriteFactory.CircleTexture(1f, false), new Vector2(0.5f, 0.5f), 64f, FilterMode.Bilinear);
            ds.ring = SavePng("Ring", SpriteFactory.CircleTexture(0f, true), new Vector2(0.5f, 0.5f), 64f, FilterMode.Bilinear);
            if (existing == null) AssetDatabase.CreateAsset(ds, assetPath);
            EditorUtility.SetDirty(ds);
            AssetDatabase.SaveAssets();
            return ds;
        }

        static Sprite SavePng(string name, Texture2D tex, Vector2 pivot, float ppu, FilterMode filter)
        {
            string path = DataRoot + "/Generated/" + name + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = ppu;
            imp.filterMode = filter;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            var settings = new TextureImporterSettings();
            imp.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            imp.SetTextureSettings(settings);
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        //this turns the built in fighters into assets you can edit with the hitbox editor
        [MenuItem(Menu + "Create Default Roster Assets", false, 20)]
        public static void CreateRoster()
        {
            EnsureFolder(DataRoot + "/Fighters");
            EnsureFolder(DataRoot + "/Resources");
            string rosterPath = DataRoot + "/Resources/" + RosterLoader.ResourcePath + ".asset";
            var roster = AssetDatabase.LoadAssetAtPath<RosterAsset>(rosterPath);
            if (roster == null)
            {
                roster = ScriptableObject.CreateInstance<RosterAsset>();
                AssetDatabase.CreateAsset(roster, rosterPath);
            }
            int made = 0;
            foreach (var def in RosterFactory.CreateDefaultRoster())
            {
                string path = DataRoot + "/Fighters/" + def.id + ".asset";
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
            var mgr = FindOne<FightGameManager>();
            if (mgr != null && mgr.roster == null) { Undo.RecordObject(mgr, "Assign Roster"); mgr.roster = roster; EditorSceneManager.MarkSceneDirty(mgr.gameObject.scene); }
            Selection.activeObject = roster;
            Debug.Log("Fighting Game: made " + made + " fighter assets in " + DataRoot + "/Fighters");
        }

        [MenuItem(Menu + "Create Sound Bank", false, 21)]
        public static void CreateSoundBank()
        {
            EnsureFolder(DataRoot + "/Audio");
            string path = DataRoot + "/Audio/FightingGameSounds.asset";
            var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(path);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<SoundBank>();
                foreach (SfxId id in System.Enum.GetValues(typeof(SfxId)))
                    bank.entries.Add(new SoundBank.Entry { id = id, clips = new AudioClip[0] });
                AssetDatabase.CreateAsset(bank, path);
                AssetDatabase.SaveAssets();
            }
            var am = FindOne<AudioManager>();
            if (am != null && am.bank == null) { Undo.RecordObject(am, "Assign Sound Bank"); am.bank = bank; EditorSceneManager.MarkSceneDirty(am.gameObject.scene); }
            Selection.activeObject = bank;
        }

        //this writes the default controls into an Input Actions asset so you can rebind everything in unity's input editor
        [MenuItem(Menu + "Create Input Actions Asset", false, 22)]
        public static void CreateInputAsset()
        {
            EnsureFolder(DataRoot);
            string path = DataRoot + "/FightingGameControls.inputactions";
            if (!File.Exists(path))
            {
                var asset = ScriptableObject.CreateInstance<InputActionAsset>();
                for (int p = 0; p < 2; p++)
                {
                    var map = new InputActionMap(PlayerInputSource.MapName(p));
                    PlayerInputSource.AddDefaultBindings(map, p);
                    asset.AddActionMap(map);
                }
                File.WriteAllText(path, asset.ToJson());
                Object.DestroyImmediate(asset);
                AssetDatabase.ImportAsset(path);
            }
            var loaded = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            var mgr = FindOne<FightGameManager>();
            if (mgr != null && loaded != null) { Undo.RecordObject(mgr, "Assign Controls"); mgr.inputActions = loaded; EditorSceneManager.MarkSceneDirty(mgr.gameObject.scene); }
            Selection.activeObject = loaded;
        }

        //this makes an animation set for the selected fighter with an empty clip for every state and move so you just drop sprites in
        [MenuItem(Menu + "Sprites/Create Animation Set For Selected Fighter", false, 40)]
        public static void CreateAnimationSet()
        {
            var fa = Selection.activeObject as FighterAsset;
            if (fa == null) { EditorUtility.DisplayDialog("Fighting Game", "Select a Fighter asset in the Project window first.", "OK"); return; }
            EnsureFolder(DataRoot + "/Animations");
            string path = AssetDatabase.GenerateUniqueAssetPath(DataRoot + "/Animations/" + fa.def.id + "_Animations.asset");
            var set = ScriptableObject.CreateInstance<SpriteAnimationSet>();
            SpriteAnimationSetEditor.AddMissingClips(set, fa);
            AssetDatabase.CreateAsset(set, path);
            Undo.RecordObject(fa, "Assign Animations");
            fa.animations = set;
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            Selection.activeObject = set;
        }

        [MenuItem(Menu + "Sprites/Create Animation Set For Selected Fighter", true)]
        static bool CanCreateAnimationSet() { return Selection.activeObject is FighterAsset; }

        //this makes a sprite fighter prefab for the selected fighter that you can open and customize
        [MenuItem(Menu + "Sprites/Create Sprite View Prefab For Selected Fighter", false, 41)]
        public static void CreateSpriteViewPrefab()
        {
            var fa = Selection.activeObject as FighterAsset;
            if (fa == null) { EditorUtility.DisplayDialog("Fighting Game", "Select a Fighter asset in the Project window first.", "OK"); return; }
            var ds = EnsureDefaultSprites();
            EnsureFolder(DataRoot + "/Prefabs");

            var go = new GameObject(fa.def.id + "_View");
            var view = go.AddComponent<SpriteFighterView>();
            var visual = new GameObject("Visual").transform;
            visual.SetParent(go.transform, false);
            view.visualRoot = visual;
            var body = new GameObject("Body").AddComponent<SpriteRenderer>();
            body.transform.SetParent(visual, false);
            body.sortingOrder = view.sortingBase;
            view.body = body;
            var shadow = new GameObject("Shadow").AddComponent<SpriteRenderer>();
            shadow.transform.SetParent(go.transform, false);
            shadow.sprite = ds.softCircle;
            shadow.color = new Color(0f, 0f, 0f, 0.45f);
            shadow.sortingOrder = 1;
            view.shadow = shadow;
            view.animations = fa.animations;
            view.flashMaterial = EnsureFlashMaterial();
            if (fa.animations != null && fa.animations.clips.Count > 0)
            {
                var idle = fa.animations.Find("idle");
                if (idle != null && idle.frames.Count > 0) body.sprite = idle.frames[0].sprite;
            }

            string path = AssetDatabase.GenerateUniqueAssetPath(DataRoot + "/Prefabs/" + fa.def.id + "_View.prefab");
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            Undo.RecordObject(fa, "Assign View Prefab");
            fa.viewPrefab = prefab.GetComponent<FighterView>();
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            Selection.activeObject = prefab;
        }

        [MenuItem(Menu + "Sprites/Create Sprite View Prefab For Selected Fighter", true)]
        static bool CanCreateView() { return Selection.activeObject is FighterAsset; }

        [MenuItem(Menu + "Sprites/Create Sprite Effect Prefab", false, 60)]
        public static void CreateEffectPrefab()
        {
            EnsureFolder(DataRoot + "/Prefabs");
            var go = new GameObject("NewHitSpark");
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<SpriteSheetEffect>().frames = SelectedSprites().ToArray();
            SaveAndSelectPrefab(go, DataRoot + "/Prefabs/NewHitSpark.prefab");
        }

        [MenuItem(Menu + "Sprites/Create Sprite Projectile Prefab", false, 61)]
        public static void CreateProjectilePrefab()
        {
            EnsureFolder(DataRoot + "/Prefabs");
            var go = new GameObject("NewProjectile");
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<SpriteProjectileView>().frames = SelectedSprites().ToArray();
            SaveAndSelectPrefab(go, DataRoot + "/Prefabs/NewProjectile.prefab");
        }

        static void SaveAndSelectPrefab(GameObject go, string path)
        {
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            Selection.activeObject = prefab;
            Debug.Log("Fighting Game: made " + path + " and if you had sprites selected they are already in it");
        }

        //this makes the material used for the white hit flash on sprite fighters
        //having it as a real asset means it is included when you make a build
        public static Material EnsureFlashMaterial()
        {
            EnsureFolder(DataRoot + "/Materials");
            string path = DataRoot + "/Materials/FighterHitFlash.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            var shader = Shader.Find("GUI/Text Shader");
            if (shader == null) return null;
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        //this gets sprites from the project selection and if you select a sliced sprite sheet it gets every sprite in it
        public static List<Sprite> SelectedSprites()
        {
            var list = new List<Sprite>();
            foreach (var o in Selection.objects)
            {
                var s = o as Sprite;
                if (s != null) { if (!list.Contains(s)) list.Add(s); continue; }
                if (o is Texture2D)
                {
                    foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(o)))
                    {
                        var ss = sub as Sprite;
                        if (ss != null && !list.Contains(ss)) list.Add(ss);
                    }
                }
            }
            list.Sort((a, b) => NaturalCompare(a.name, b.name));
            return list;
        }

        //this sorts names the way people expect so run_2 comes before run_10
        public static int NaturalCompare(string a, string b)
        {
            int i = 0, j = 0;
            while (i < a.Length && j < b.Length)
            {
                if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
                {
                    long na = 0, nb = 0;
                    while (i < a.Length && char.IsDigit(a[i])) na = na * 10 + (a[i++] - '0');
                    while (j < b.Length && char.IsDigit(b[j])) nb = nb * 10 + (b[j++] - '0');
                    if (na != nb) return na.CompareTo(nb);
                }
                else
                {
                    int c = char.ToLowerInvariant(a[i]).CompareTo(char.ToLowerInvariant(b[j]));
                    if (c != 0) return c;
                    i++; j++;
                }
            }
            return a.Length.CompareTo(b.Length);
        }

        //this writes every move's frame data into a spreadsheet file so you can balance in a table
        [MenuItem(Menu + "Export Frame Data CSV", false, 80)]
        public static void ExportCsv()
        {
            string path = EditorUtility.SaveFilePanel("Export Frame Data", "", "frame_data.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            var mgr = FindOne<FightGameManager>();
            var sb = new StringBuilder();
            sb.AppendLine("Fighter,Move Id,Name,Input,Kind,Startup,Active,Recovery,Total,Damage,Chip,Hitstun,Blockstun,On Hit,On Block,Guard,Hit Effect,Invincible,Meter Cost");
            foreach (var e in RosterLoader.Load(mgr != null ? mgr.roster : null))
            {
                var d = e.source;
                foreach (var m in d.moves)
                {
                    int after = m.TotalFrames - m.startup;
                    string inv = m.invulnEnd > 0 ? m.invulnStart + " to " + m.invulnEnd : "";
                    sb.Append(Csv(d.displayName)).Append(',').Append(Csv(m.id)).Append(',').Append(Csv(m.displayName)).Append(',')
                      .Append(Csv(FightUtil.Notation(m))).Append(',').Append(m.isSuper ? "Super" : m.kind.ToString()).Append(',')
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

        static string Csv(string s)
        {
            if (s == null) return "";
            return s.Contains(",") || s.Contains("\"") ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        }

        //this makes a folder path one level at a time
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        //unity 2023 renamed the find functions so this picks the right one and avoids warnings
        public static T FindOne<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<T>();
#else
            return Object.FindObjectOfType<T>();
#endif
        }

        public static T[] FindAll<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<T>();
#endif
        }
    }
}

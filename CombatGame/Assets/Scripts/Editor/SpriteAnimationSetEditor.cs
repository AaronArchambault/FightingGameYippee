using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FightGame.EditorTools
{
    //this is the inspector for sprite animation sets
    //it adds buttons to fill a clip straight from a sliced sprite sheet and to make empty clips for a fighter
    //and it has a little preview so you can scrub through a clip
    [CustomEditor(typeof(SpriteAnimationSet))]
    public class SpriteAnimationSetEditor : Editor
    {
        int clipIndex;
        int previewFrame;
        int defaultDuration = 3;
        FighterAsset fighter;

        public override void OnInspectorGUI()
        {
            var set = (SpriteAnimationSet)target;

            EditorGUILayout.HelpBox("1  Slice your sprite sheet in the Sprite Editor\n2  Pick a clip below\n3  Select the sprites or the whole sheet in the Project window\n4  Click Fill Clip From Selection\n\nAttacks line up with the frame data automatically. Use the Hitbox Editor to check the art against the boxes.", MessageType.Info);

            var keys = new List<string>();
            foreach (var c in set.clips) keys.Add(c == null || string.IsNullOrEmpty(c.key) ? "(no key)" : c.key + "  (" + c.frames.Count + ")");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Quick Fill", EditorStyles.boldLabel);
            if (keys.Count > 0)
            {
                clipIndex = Mathf.Clamp(clipIndex, 0, keys.Count - 1);
                clipIndex = EditorGUILayout.Popup("Clip", clipIndex, keys.ToArray());
                defaultDuration = Mathf.Max(1, EditorGUILayout.IntField("Frames Per Sprite", defaultDuration));
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Fill Clip From Selection"))
                {
                    var sprites = FightingGameMenu.SelectedSprites();
                    if (sprites.Count == 0) EditorUtility.DisplayDialog("Fighting Game", "Select some sprites or a sliced sprite sheet in the Project window first.", "OK");
                    else
                    {
                        Undo.RecordObject(set, "Fill Clip");
                        var clip = set.clips[clipIndex];
                        clip.frames.Clear();
                        foreach (var s in sprites) clip.frames.Add(new SpriteFrame { sprite = s, duration = defaultDuration });
                        Dirty(set);
                    }
                }
                if (GUILayout.Button("Clear Clip"))
                {
                    Undo.RecordObject(set, "Clear Clip");
                    set.clips[clipIndex].frames.Clear();
                    Dirty(set);
                }
                EditorGUILayout.EndHorizontal();
                DrawPreview(set.clips[clipIndex]);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Clips For A Fighter", EditorStyles.boldLabel);
            fighter = (FighterAsset)EditorGUILayout.ObjectField("Fighter", fighter, typeof(FighterAsset), false);
            if (GUILayout.Button("Add Missing Clips (every state and every move)"))
            {
                Undo.RecordObject(set, "Add Clips");
                AddMissingClips(set, fighter);
                Dirty(set);
            }
            if (GUILayout.Button("Remove Empty Clips"))
            {
                Undo.RecordObject(set, "Remove Empty Clips");
                set.clips.RemoveAll(c => c == null || c.frames.Count == 0);
                Dirty(set);
            }

            EditorGUILayout.Space();
            DrawDefaultInspector();
        }

        void Dirty(SpriteAnimationSet set)
        {
            EditorUtility.SetDirty(set);
            //this makes the lookup rebuild since the clips changed
            set.Invalidate();
            Repaint();
        }

        //this shows one sprite of the clip so you can check the order
        void DrawPreview(SpriteClip clip)
        {
            if (clip == null || clip.frames.Count == 0) return;
            previewFrame = EditorGUILayout.IntSlider("Preview Sprite", previewFrame, 0, clip.frames.Count - 1);
            var sp = clip.frames[Mathf.Clamp(previewFrame, 0, clip.frames.Count - 1)].sprite;
            if (sp == null) return;
            var r = GUILayoutUtility.GetRect(160, 160, GUILayout.ExpandWidth(true));
            DrawSprite(r, sp);
        }

        //this draws a sprite fit inside a box keeping its shape
        public static void DrawSprite(Rect r, Sprite sp)
        {
            var tex = sp.texture;
            if (tex == null) return;
            var tr = sp.textureRect;
            float aspect = tr.width / tr.height;
            float w = r.width, h = r.height;
            if (w / h > aspect) w = h * aspect; else h = w / aspect;
            var box = new Rect(r.x + (r.width - w) / 2f, r.y + (r.height - h) / 2f, w, h);
            var uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
            GUI.DrawTextureWithTexCoords(box, tex, uv);
        }

        //this adds an empty clip for every state name and every move id that is not already in the set
        public static void AddMissingClips(SpriteAnimationSet set, FighterAsset fighter)
        {
            var have = new HashSet<string>();
            foreach (var c in set.clips) if (c != null && !string.IsNullOrEmpty(c.key)) have.Add(c.key);
            foreach (var k in SpriteAnimationSet.AllStateKeys)
            {
                if (have.Contains(k)) continue;
                bool loops = k == "idle" || k == "walkf" || k == "walkb" || k == "crouch" || k == "win" || k == "intro";
                set.clips.Add(new SpriteClip { key = k, loop = loops });
                have.Add(k);
            }
            if (fighter == null || fighter.def == null) return;
            foreach (var m in fighter.def.moves)
            {
                if (have.Contains(m.id)) continue;
                set.clips.Add(new SpriteClip { key = m.id, loop = false, timing = ClipTiming.MatchFrameData, startupSprites = 2, activeSprites = 1 });
                have.Add(m.id);
            }
        }
    }
}

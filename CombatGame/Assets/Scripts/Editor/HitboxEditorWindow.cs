using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using FightCore;

namespace FightGame.EditorTools
{
    //this is the visual hitbox editor
    //you pick a fighter and a move then scrub through the frames and drag boxes around right on the preview
    //red boxes are hitboxes green boxes are hurtboxes and the yellow outline is the pushbox
    public class HitboxEditorWindow : EditorWindow
    {
        FighterAsset asset;
        int moveIndex;
        int frame = 1;
        bool playing;
        double lastPlayTime;
        float zoom = 110f;
        Vector2 pan = new Vector2(0f, 0f);
        Vector2 moveScroll, sideScroll;
        bool snap = true;

        //this is which box is selected where isHit means it is in the hitbox list and not the hurtbox list
        bool selIsHit = true;
        int selIndex = -1;

        enum Drag { None, Move, Resize, Pan }
        Drag drag;
        int dragCorner;
        Vector2 dragStartMouse;
        BoxRect dragStartRect;

        [MenuItem("Tools/Fighting Game/Hitbox Editor")]
        public static void Open()
        {
            var w = GetWindow<HitboxEditorWindow>("Hitbox Editor");
            w.minSize = new Vector2(900, 520);
            if (Selection.activeObject is FighterAsset) w.asset = (FighterAsset)Selection.activeObject;
        }

        void OnSelectionChange()
        {
            if (Selection.activeObject is FighterAsset && Selection.activeObject != asset)
            {
                asset = (FighterAsset)Selection.activeObject;
                moveIndex = 0; frame = 1; selIndex = -1;
                Repaint();
            }
        }

        MoveDef CurrentMove
        {
            get
            {
                if (asset == null || asset.def == null || asset.def.moves.Count == 0) return null;
                moveIndex = Mathf.Clamp(moveIndex, 0, asset.def.moves.Count - 1);
                return asset.def.moves[moveIndex];
            }
        }

        void Update()
        {
            //this plays the move at real game speed so you can feel the timing
            if (!playing) return;
            var m = CurrentMove;
            if (m == null) { playing = false; return; }
            if (EditorApplication.timeSinceStartup - lastPlayTime >= 1.0 / 60.0)
            {
                lastPlayTime = EditorApplication.timeSinceStartup;
                frame++;
                if (frame > m.TotalFrames) frame = 1;
                Repaint();
            }
        }

        void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            var newAsset = (FighterAsset)EditorGUILayout.ObjectField(asset, typeof(FighterAsset), false, GUILayout.Width(260));
            if (newAsset != asset) { asset = newAsset; moveIndex = 0; frame = 1; selIndex = -1; }
            GUILayout.FlexibleSpace();
            snap = GUILayout.Toggle(snap, "Snap 25", EditorStyles.toolbarButton, GUILayout.Width(70));
            if (GUILayout.Button("Reset View", EditorStyles.toolbarButton, GUILayout.Width(80))) { zoom = 110f; pan = Vector2.zero; }
            EditorGUILayout.EndHorizontal();

            if (asset == null)
            {
                EditorGUILayout.HelpBox("Pick a Fighter asset up top. If you do not have any yet use Tools > Fighting Game > Create Default Roster Assets.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            DrawMoveList();
            EditorGUILayout.BeginVertical();
            var m = CurrentMove;
            if (m != null)
            {
                DrawTimeline(m);
                var canvas = GUILayoutUtility.GetRect(10, 10, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                DrawCanvas(canvas, m);
            }
            EditorGUILayout.EndVertical();
            if (m != null) DrawSidePanel(m);
            EditorGUILayout.EndHorizontal();
        }

        void DrawMoveList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(190));
            GUILayout.Label(asset.def.displayName + "  moves", EditorStyles.boldLabel);
            moveScroll = EditorGUILayout.BeginScrollView(moveScroll);
            for (int i = 0; i < asset.def.moves.Count; i++)
            {
                var mv = asset.def.moves[i];
                var style = i == moveIndex ? EditorStyles.helpBox : EditorStyles.label;
                if (GUILayout.Button(mv.id + "   " + mv.displayName, style))
                {
                    moveIndex = i; frame = Mathf.Clamp(mv.startup, 1, mv.TotalFrames); selIndex = -1;
                    GUI.FocusControl(null);
                }
            }
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("Duplicate Move"))
            {
                Undo.RecordObject(asset, "Duplicate Move");
                var copy = CurrentMove.Clone();
                copy.id = CurrentMove.id + "_copy";
                asset.def.moves.Insert(moveIndex + 1, copy);
                moveIndex++;
                Dirty();
            }
            EditorGUILayout.EndVertical();
        }

        //this is the strip across the top that shows startup active and recovery in different colors
        void DrawTimeline(MoveDef m)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(28))) { frame = Mathf.Max(1, frame - 1); playing = false; }
            if (GUILayout.Button(playing ? "Stop" : "Play", GUILayout.Width(50))) { playing = !playing; lastPlayTime = EditorApplication.timeSinceStartup; }
            if (GUILayout.Button(">", GUILayout.Width(28))) { frame = Mathf.Min(m.TotalFrames, frame + 1); playing = false; }
            frame = EditorGUILayout.IntSlider(frame, 1, Mathf.Max(1, m.TotalFrames));
            EditorGUILayout.EndHorizontal();

            var r = GUILayoutUtility.GetRect(10, 18, GUILayout.ExpandWidth(true));
            int total = Mathf.Max(1, m.TotalFrames);
            float w = r.width / total;
            for (int f = 1; f <= total; f++)
            {
                Color c;
                if (f < m.startup) c = new Color(0.25f, 0.6f, 0.3f);
                else if (m.IsActiveFrame(f)) c = new Color(0.85f, 0.2f, 0.2f);
                else c = new Color(0.25f, 0.35f, 0.7f);
                if (m.InvulnOn(f)) c = Color.Lerp(c, Color.white, 0.45f);
                var cell = new Rect(r.x + (f - 1) * w, r.y, Mathf.Max(1f, w - 1f), r.height);
                EditorGUI.DrawRect(cell, c);
                if (f == frame) EditorGUI.DrawRect(new Rect(cell.x, cell.y - 2, cell.width, r.height + 4), new Color(1f, 1f, 0.2f, 0.8f));
            }
            string phase = frame < m.startup ? "Startup" : (m.IsActiveFrame(frame) ? "Active" : "Recovery");
            GUILayout.Label("Frame " + frame + " of " + m.TotalFrames + "   " + phase + (m.InvulnOn(frame) ? "   INVINCIBLE" : "") + "      green startup   red active   blue recovery   white invincible");
        }

        //this turns sim units into pixels on the preview
        Vector2 Origin(Rect r) { return new Vector2(r.center.x - 150f + pan.x, r.yMax - 40f + pan.y); }

        Rect BoxToScreen(Rect r, BoxRect b)
        {
            var o = Origin(r);
            float s = zoom / 1000f;
            return new Rect(o.x + (b.cx - b.w / 2f) * s, o.y - (b.cy + b.h / 2f) * s, b.w * s, b.h * s);
        }

        void DrawCanvas(Rect r, MoveDef m)
        {
            EditorGUI.DrawRect(r, new Color(0.13f, 0.13f, 0.15f));
            GUI.BeginClip(r);
            var local = new Rect(0, 0, r.width, r.height);
            var o = Origin(local);
            float s = zoom / 1000f;

            //this is a grid where each line is 500 units which is half a unity unit
            Handles.color = new Color(1f, 1f, 1f, 0.06f);
            for (int gx = -8000; gx <= 8000; gx += 500) { float x = o.x + gx * s; Handles.DrawLine(new Vector3(x, 0), new Vector3(x, local.height)); }
            for (int gy = 0; gy <= 5000; gy += 500) { float y = o.y - gy * s; Handles.DrawLine(new Vector3(0, y), new Vector3(local.width, y)); }
            Handles.color = new Color(1f, 1f, 1f, 0.5f);
            Handles.DrawLine(new Vector3(0, o.y), new Vector3(local.width, o.y));
            Handles.DrawLine(new Vector3(o.x, o.y), new Vector3(o.x, o.y - 2200 * s));

            var def = asset.def;
            BoxRect baseHurt = (m.dir == DirReq.Air || m.dir == DirReq.AirDown || m.useAirHurtbox) ? def.airHurt : (m.crouching ? def.crouchHurt : def.standHurt);
            bool inv = m.InvulnOn(frame);
            DrawBox(local, baseHurt, inv ? Color.white : HitboxDebugView.HurtColor, false);

            int pushH = m.crouching ? 1200 : 1600;
            var push = BoxToScreen(local, new BoxRect(0, pushH / 2, def.pushWidth, pushH));
            Handles.DrawSolidRectangleWithOutline(push, Color.clear, HitboxDebugView.PushColor);

            for (int i = 0; i < m.hurtboxes.Count; i++)
                if (m.hurtboxes[i].ActiveOn(frame)) DrawBox(local, m.hurtboxes[i].rect, HitboxDebugView.HurtColor, !selIsHit && selIndex == i);
            for (int i = 0; i < m.hitboxes.Count; i++)
            {
                bool on = m.hitboxes[i].ActiveOn(frame);
                //it shows hitboxes from other frames as a faint ghost so you can line things up
                if (on) DrawBox(local, m.hitboxes[i].rect, HitboxDebugView.HitColor, selIsHit && selIndex == i);
                else DrawGhost(local, m.hitboxes[i].rect);
            }
            if (m.IsThrow && m.IsActiveFrame(frame))
            {
                var tr = new Rect(o.x, o.y - 1400 * s, m.throwRange * s, 1000 * s);
                Handles.DrawSolidRectangleWithOutline(tr, new Color(0.8f, 0.3f, 1f, 0.2f), HitboxDebugView.ThrowColor);
            }
            if (m.spawnsProjectile && frame >= m.projectileFrame)
            {
                var pb = m.projectile.box;
                var world = new BoxRect(m.projectile.spawnX + pb.cx, m.projectile.spawnY + pb.cy, pb.w, pb.h);
                DrawBox(local, world, HitboxDebugView.ProjColor, false);
            }

            //this is a little stick figure so you know where the fighter is standing
            Handles.color = new Color(1f, 1f, 1f, 0.35f);
            Handles.DrawWireDisc(new Vector3(o.x, o.y - 1750 * s), Vector3.forward, 160 * s);

            HandleMouse(local, m);
            GUI.EndClip();
        }

        void DrawBox(Rect local, BoxRect b, Color c, bool selected)
        {
            var sr = BoxToScreen(local, b);
            Handles.DrawSolidRectangleWithOutline(sr, new Color(c.r, c.g, c.b, 0.25f), selected ? Color.yellow : c);
            if (!selected) return;
            foreach (var p in Corners(sr)) EditorGUI.DrawRect(new Rect(p.x - 4, p.y - 4, 8, 8), Color.yellow);
        }

        void DrawGhost(Rect local, BoxRect b)
        {
            Handles.DrawSolidRectangleWithOutline(BoxToScreen(local, b), Color.clear, new Color(1f, 0.2f, 0.2f, 0.25f));
        }

        static Vector2[] Corners(Rect r)
        {
            return new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax) };
        }

        List<FrameBox> SelList(MoveDef m) { return selIsHit ? m.hitboxes : m.hurtboxes; }

        FrameBox Selected(MoveDef m)
        {
            var l = SelList(m);
            return selIndex >= 0 && selIndex < l.Count ? l[selIndex] : null;
        }

        //this is all the clicking and dragging on the preview
        void HandleMouse(Rect local, MoveDef m)
        {
            var e = Event.current;
            float s = zoom / 1000f;
            if (e.type == EventType.ScrollWheel)
            {
                zoom = Mathf.Clamp(zoom * (e.delta.y > 0 ? 0.9f : 1.1f), 30f, 400f);
                e.Use(); Repaint(); return;
            }
            if (e.type == EventType.MouseDown && (e.button == 2 || e.alt))
            {
                drag = Drag.Pan; dragStartMouse = e.mousePosition; e.Use(); return;
            }
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                //first it checks the corner handles on the box you already have selected
                var sel = Selected(m);
                if (sel != null && sel.ActiveOn(frame))
                {
                    var cs = Corners(BoxToScreen(local, sel.rect));
                    for (int i = 0; i < 4; i++)
                    {
                        if (Vector2.Distance(cs[i], e.mousePosition) < 8f)
                        {
                            Undo.RecordObject(asset, "Resize Box");
                            drag = Drag.Resize; dragCorner = i; dragStartMouse = e.mousePosition; dragStartRect = sel.rect;
                            e.Use(); return;
                        }
                    }
                }
                //then it picks the box under the mouse with hitboxes first since those are what you edit most
                if (Pick(local, m.hitboxes, true, e.mousePosition) || Pick(local, m.hurtboxes, false, e.mousePosition))
                {
                    Undo.RecordObject(asset, "Move Box");
                    drag = Drag.Move; dragStartMouse = e.mousePosition; dragStartRect = Selected(m).rect;
                    e.Use(); Repaint(); return;
                }
                selIndex = -1; Repaint();
            }
            if (e.type == EventType.MouseDrag && drag != Drag.None)
            {
                var delta = e.mousePosition - dragStartMouse;
                if (drag == Drag.Pan) { pan += e.delta; e.Use(); Repaint(); return; }
                var sel = Selected(m);
                if (sel == null) { drag = Drag.None; return; }
                int dx = Snap(delta.x / s), dy = Snap(-delta.y / s);
                var b = dragStartRect;
                if (drag == Drag.Move) { b.cx = dragStartRect.cx + dx; b.cy = dragStartRect.cy + dy; }
                else
                {
                    //it works out the new edges from whichever corner you grabbed
                    float left = dragStartRect.cx - dragStartRect.w / 2f, right = dragStartRect.cx + dragStartRect.w / 2f;
                    float bottom = dragStartRect.cy - dragStartRect.h / 2f, top = dragStartRect.cy + dragStartRect.h / 2f;
                    if (dragCorner == 0 || dragCorner == 2) left += dx; else right += dx;
                    if (dragCorner == 0 || dragCorner == 1) top += dy; else bottom += dy;
                    if (right - left < 25) right = left + 25;
                    if (top - bottom < 25) top = bottom + 25;
                    b.w = Mathf.RoundToInt(right - left); b.h = Mathf.RoundToInt(top - bottom);
                    b.cx = Mathf.RoundToInt((left + right) / 2f); b.cy = Mathf.RoundToInt((bottom + top) / 2f);
                }
                sel.rect = b;
                Dirty();
                e.Use();
            }
            if (e.type == EventType.MouseUp) drag = Drag.None;
        }

        int Snap(float v) { return snap ? Mathf.RoundToInt(v / 25f) * 25 : Mathf.RoundToInt(v); }

        bool Pick(Rect local, List<FrameBox> list, bool isHit, Vector2 mouse)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (!list[i].ActiveOn(frame)) continue;
                if (!BoxToScreen(local, list[i].rect).Contains(mouse)) continue;
                selIsHit = isHit; selIndex = i;
                return true;
            }
            return false;
        }

        //this is the panel on the right with the numbers for the selected box and the move frame data
        void DrawSidePanel(MoveDef m)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(300));
            sideScroll = EditorGUILayout.BeginScrollView(sideScroll);

            EditorGUILayout.LabelField("Boxes", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Hitbox")) AddBox(m, true);
            if (GUILayout.Button("Add Hurtbox")) AddBox(m, false);
            EditorGUILayout.EndHorizontal();

            var sel = Selected(m);
            if (sel != null)
            {
                EditorGUILayout.LabelField(selIsHit ? "Selected Hitbox " + selIndex : "Selected Hurtbox " + selIndex);
                EditorGUI.BeginChangeCheck();
                int start = EditorGUILayout.IntField("Start Frame", sel.start);
                int end = EditorGUILayout.IntField("End Frame", sel.end);
                int cx = EditorGUILayout.IntField("Forward (cx)", sel.rect.cx);
                int cy = EditorGUILayout.IntField("Height (cy)", sel.rect.cy);
                int w = EditorGUILayout.IntField("Width", sel.rect.w);
                int h = EditorGUILayout.IntField("Tall", sel.rect.h);
                int group = selIsHit ? EditorGUILayout.IntSlider("Hit Group", sel.hitGroup, 0, 7) : sel.hitGroup;
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(asset, "Edit Box");
                    sel.start = Mathf.Max(1, start); sel.end = Mathf.Max(sel.start, end);
                    sel.rect = new BoxRect(cx, cy, Mathf.Max(1, w), Mathf.Max(1, h));
                    sel.hitGroup = group;
                    Dirty();
                }
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Match Active Frames"))
                {
                    Undo.RecordObject(asset, "Match Active");
                    sel.start = m.startup; sel.end = m.startup + m.active - 1; Dirty();
                }
                if (GUILayout.Button("Delete"))
                {
                    Undo.RecordObject(asset, "Delete Box");
                    SelList(m).RemoveAt(selIndex); selIndex = -1; Dirty();
                }
                EditorGUILayout.EndHorizontal();
            }
            else EditorGUILayout.HelpBox("Click a box to select it. Drag to move it and drag the yellow corners to resize. Scroll to zoom and alt drag to pan.", MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Frame Data", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            string id = EditorGUILayout.TextField("Id", m.id);
            string disp = EditorGUILayout.TextField("Name", m.displayName);
            int su = EditorGUILayout.IntField("Startup", m.startup);
            int ac = EditorGUILayout.IntField("Active", m.active);
            int rc = EditorGUILayout.IntField("Recovery", m.recovery);
            int dmg = EditorGUILayout.IntField("Damage", m.hit.damage);
            int chip = EditorGUILayout.IntField("Chip", m.hit.chip);
            int hs = EditorGUILayout.IntField("Hitstun", m.hit.hitstun);
            int bs = EditorGUILayout.IntField("Blockstun", m.hit.blockstun);
            int stop = EditorGUILayout.IntField("Hitstop", m.hit.hitstop);
            var guard = (GuardType)EditorGUILayout.EnumPopup("Guard", m.hit.guard);
            var eff = (HitEffect)EditorGUILayout.EnumPopup("On Hit", m.hit.effect);
            var lvl = (HitLevel)EditorGUILayout.EnumPopup("Strength", m.hit.level);
            int invS = EditorGUILayout.IntField("Invincible From", m.invulnStart);
            int invE = EditorGUILayout.IntField("Invincible To", m.invulnEnd);
            bool sc = EditorGUILayout.Toggle("Special Cancel", m.specialCancel);
            bool suc = EditorGUILayout.Toggle("Super Cancel", m.superCancel);
            bool crouch = EditorGUILayout.Toggle("Crouching", m.crouching);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(asset, "Edit Frame Data");
                m.id = id; m.displayName = disp;
                m.startup = Mathf.Max(1, su); m.active = Mathf.Max(1, ac); m.recovery = Mathf.Max(0, rc);
                m.hit.damage = dmg; m.hit.chip = chip; m.hit.hitstun = hs; m.hit.blockstun = bs; m.hit.hitstop = stop;
                m.hit.guard = guard; m.hit.effect = eff; m.hit.level = lvl;
                m.invulnStart = invS; m.invulnEnd = invE;
                m.specialCancel = sc; m.superCancel = suc; m.crouching = crouch;
                Dirty();
            }

            //this is the rough advantage if the move hits on its first active frame
            //training mode measures the real number so use that to double check
            int after = m.TotalFrames - m.startup;
            int onHit = m.hit.hitstun - after;
            int onBlock = m.hit.blockstun - after;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Total Frames", m.TotalFrames.ToString());
            EditorGUILayout.LabelField("About On Hit", (onHit > 0 ? "+" : "") + onHit);
            EditorGUILayout.LabelField("About On Block", (onBlock > 0 ? "+" : "") + onBlock);
            EditorGUILayout.LabelField("Notation", GameRunner.Notation(m));

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void AddBox(MoveDef m, bool hit)
        {
            Undo.RecordObject(asset, hit ? "Add Hitbox" : "Add Hurtbox");
            int s = hit ? m.startup : frame;
            int e = hit ? m.startup + m.active - 1 : frame;
            var fb = new FrameBox(s, e, hit ? new BoxRect(700, 1200, 500, 300) : new BoxRect(500, 1200, 400, 300));
            if (hit) { m.hitboxes.Add(fb); selIsHit = true; selIndex = m.hitboxes.Count - 1; }
            else { m.hurtboxes.Add(fb); selIsHit = false; selIndex = m.hurtboxes.Count - 1; }
            frame = Mathf.Clamp(s, 1, m.TotalFrames);
            Dirty();
        }

        void Dirty()
        {
            EditorUtility.SetDirty(asset);
            asset.def.Rebuild();
            Repaint();
        }
    }
}

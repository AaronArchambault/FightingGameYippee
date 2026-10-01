using UnityEngine;
using FightCore;

namespace FightGame
{
    //this is the placeholder fighter made out of shapes and it is what you see before you add your own art
    //it poses itself from the frame data so it pulls back during startup then snaps out on the active frames
    //you can tweak the colors and proportions in the inspector if you put it on a prefab
    public class PuppetFighterView : FighterView
    {
        [Header("Puppet")]
        [Tooltip("leave these clear to use the colors from the fighter data")]
        public Color bodyColorOverride = Color.clear;
        public Color accentColorOverride = Color.clear;
        public Color skinColor = new Color(0.95f, 0.78f, 0.62f);
        [Tooltip("how much the puppet glows during the wind up of heavy moves")]
        [Range(0f, 1f)] public float anticipationGlow = 0.45f;

        Color bodyColor, accentColor;
        float rigScale = 1f, widthScale = 1f;

        Transform pose, torsoPivot, head, shoulderF, shoulderB, hipF, hipB;
        SpriteRenderer torso, belt, headSr, band, armF, armB, fistF, fistB, legF, legB, footF, footB, glow;
        SpriteRenderer[] parts;
        int[] partOrder;
        int lastSortBase = -1;

        static readonly Vector3 HipPos = new Vector3(0f, 0.95f, 0f);

        public override void Setup(FighterDef def, int player, FighterAsset asset)
        {
            base.Setup(def, player, asset);
            bodyColor = bodyColorOverride.a > 0f ? bodyColorOverride : FightUtil.RGB(def.colorRGB);
            accentColor = accentColorOverride.a > 0f ? accentColorOverride : FightUtil.RGB(def.accentRGB);
            //it makes player two a bit darker so you can tell who is who in a mirror match
            if (player == 1) bodyColor = Color.Lerp(bodyColor, new Color(0.2f, 0.2f, 0.25f), 0.35f);

            rigScale = def.standHurt.h / 1900f;
            widthScale = Mathf.Clamp(def.standHurt.w / 600f, 0.8f, 1.4f);

            pose = new GameObject("Pose").transform;
            pose.SetParent(visualRoot, false);
            pose.localScale = new Vector3(rigScale, rigScale, 1f);

            glow = SpriteFactory.Make("Glow", pose, SpriteFactory.SoftCircle, new Color(1, 1, 1, 0), 0);
            glow.transform.localPosition = new Vector3(0f, 1f, 0f);
            glow.transform.localScale = new Vector3(2.6f, 3.2f, 1f);

            hipB = MakePivot("HipB", pose, HipPos + new Vector3(-0.1f * widthScale, 0f, 0f));
            hipF = MakePivot("HipF", pose, HipPos + new Vector3(0.1f * widthScale, 0f, 0f));
            legB = Limb("LegB", hipB, 0.24f * widthScale, 0.95f, Color.Lerp(bodyColor, Color.black, 0.25f));
            legF = Limb("LegF", hipF, 0.24f * widthScale, 0.95f, bodyColor);
            footB = End("FootB", legB.transform, 0.3f, Color.Lerp(accentColor, Color.black, 0.3f));
            footF = End("FootF", legF.transform, 0.3f, accentColor);

            torsoPivot = MakePivot("TorsoPivot", pose, HipPos);
            torso = SpriteFactory.Make("Torso", torsoPivot, SpriteFactory.Square, bodyColor, 0);
            torso.transform.localPosition = new Vector3(0f, 0.38f, 0f);
            torso.transform.localScale = new Vector3(0.5f * widthScale, 0.8f, 1f);
            belt = SpriteFactory.Make("Belt", torsoPivot, SpriteFactory.Square, accentColor, 0);
            belt.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            belt.transform.localScale = new Vector3(0.54f * widthScale, 0.12f, 1f);

            head = MakePivot("Head", torsoPivot, new Vector3(0.04f, 0.98f, 0f));
            headSr = SpriteFactory.Make("HeadShape", head, SpriteFactory.Circle, skinColor, 0);
            headSr.transform.localScale = new Vector3(0.36f, 0.4f, 1f);
            band = SpriteFactory.Make("Band", head, SpriteFactory.Square, accentColor, 0);
            band.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            band.transform.localScale = new Vector3(0.38f, 0.07f, 1f);

            shoulderB = MakePivot("ShoulderB", torsoPivot, new Vector3(-0.08f, 0.7f, 0f));
            shoulderF = MakePivot("ShoulderF", torsoPivot, new Vector3(0.1f, 0.7f, 0f));
            armB = Limb("ArmB", shoulderB, 0.17f * widthScale, 0.62f, Color.Lerp(skinColor, Color.black, 0.2f));
            armF = Limb("ArmF", shoulderF, 0.17f * widthScale, 0.62f, skinColor);
            fistB = End("FistB", armB.transform, 0.24f, Color.Lerp(accentColor, Color.black, 0.3f));
            fistF = End("FistF", armF.transform, 0.24f, accentColor);

            //this is the draw order from back to front so the near arm and leg are on top
            parts = new[] { armB, fistB, legB, footB, torso, belt, headSr, band, legF, footF, armF, fistF };
            partOrder = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
        }

        static Transform MakePivot(string n, Transform parent, Vector3 pos)
        {
            var t = new GameObject(n).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        //a limb is a pivot with a sprite that hangs down from it so rotating the pivot swings the whole limb
        static SpriteRenderer Limb(string n, Transform pivot, float width, float length, Color c)
        {
            var sr = SpriteFactory.Make(n, pivot, SpriteFactory.SquareTop, c, 0);
            sr.transform.localScale = new Vector3(width, length, 1f);
            return sr;
        }

        //this is a fist or a foot stuck to the end of a limb
        //it undoes the limb scale so it stays round when the arm stretches
        static SpriteRenderer End(string n, Transform limb, float size, Color c)
        {
            var sr = SpriteFactory.Make(n, limb.parent, SpriteFactory.Circle, c, 0);
            sr.transform.localScale = new Vector3(size, size, 1f);
            return sr;
        }

        //these are angles in degrees where 0 is hanging straight down and positive swings forward
        struct PoseData
        {
            public float lean, hipY, rootRot;
            public float armFAng, armFLen, armBAng, armBLen;
            public float legFAng, legFLen, legBAng, legBLen;
            public float headTilt;
            public Color tint;
            public float tintAmount;
            public float glow;
            public Color glowColor;
        }

        //this is called every render frame and it just copies the sim state onto the puppet
        protected override void OnSync(MatchSim sim, FighterSim f)
        {
            var p = ComputePose(sim, f);

            pose.localRotation = Quaternion.Euler(0f, 0f, p.rootRot);
            torsoPivot.localPosition = new Vector3(0f, p.hipY, 0f);
            torsoPivot.localRotation = Quaternion.Euler(0f, 0f, p.lean);
            hipF.localPosition = new Vector3(0.1f * widthScale, p.hipY, 0f);
            hipB.localPosition = new Vector3(-0.1f * widthScale, p.hipY, 0f);
            head.localRotation = Quaternion.Euler(0f, 0f, p.headTilt);

            SetLimb(shoulderF, armF, fistF, p.armFAng, p.armFLen);
            SetLimb(shoulderB, armB, fistB, p.armBAng, p.armBLen);
            SetLimb(hipF, legF, footF, p.legFAng, p.legFLen);
            SetLimb(hipB, legB, footB, p.legBAng, p.legBLen);

            var bc = Color.Lerp(bodyColor, p.tint, p.tintAmount);
            var sc = Color.Lerp(skinColor, p.tint, p.tintAmount);
            torso.color = bc;
            legF.color = bc;
            legB.color = Color.Lerp(bc, Color.black, 0.25f);
            armF.color = sc;
            armB.color = Color.Lerp(sc, Color.black, 0.2f);
            headSr.color = sc;
            ApplyGlow(p);

            if (currentSorting != lastSortBase)
            {
                lastSortBase = currentSorting;
                for (int i = 0; i < parts.Length; i++) parts[i].sortingOrder = currentSorting + partOrder[i];
                glow.sortingOrder = currentSorting - 1;
            }
        }

        void ApplyGlow(PoseData p)
        {
            glow.color = FightUtil.WithAlpha(p.glowColor, p.glow);
        }

        void SetLimb(Transform pivot, SpriteRenderer limb, SpriteRenderer end, float angle, float len)
        {
            pivot.localRotation = Quaternion.Euler(0f, 0f, angle);
            var s = limb.transform.localScale;
            s.y = len;
            limb.transform.localScale = s;
            end.transform.localPosition = new Vector3(0f, -len, 0f);
        }

        //this turns a direction into a limb angle where the limb hangs down at zero
        static float AngleTo(Vector2 d)
        {
            return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + 90f;
        }

        PoseData ComputePose(MatchSim sim, FighterSim f)
        {
            var p = new PoseData
            {
                hipY = HipPos.y,
                armFAng = 25f, armFLen = 0.62f, armBAng = 15f, armBLen = 0.62f,
                legFAng = 10f, legFLen = 0.95f, legBAng = -10f, legBLen = 0.95f,
                tint = Color.white,
                glowColor = accentColor
            };
            int sf = f.stateFrame;
            float t = sim.frame * 0.08f + player * 1.3f;

            switch (f.state)
            {
                case FState.Idle:
                case FState.Intro:
                    //this is the fight stance with a little breathing bob so it feels alive
                    p.hipY -= 0.04f + Mathf.Sin(t) * 0.02f;
                    p.lean = -6f;
                    p.armFAng = 120f; p.armFLen = 0.5f;
                    p.armBAng = 100f; p.armBLen = 0.45f;
                    p.legFAng = 22f; p.legBAng = -22f;
                    break;

                case FState.WalkF:
                case FState.WalkB:
                    {
                        float w = Mathf.Sin(sf * 0.3f) * 24f * (f.state == FState.WalkF ? 1f : -1f);
                        p.hipY -= 0.04f + Mathf.Abs(Mathf.Sin(sf * 0.3f)) * 0.03f;
                        p.lean = f.state == FState.WalkF ? -10f : 0f;
                        p.armFAng = 115f; p.armFLen = 0.5f; p.armBAng = 100f; p.armBLen = 0.45f;
                        p.legFAng = 10f + w; p.legBAng = -10f - w;
                        break;
                    }

                case FState.Crouch:
                    CrouchPose(ref p);
                    break;

                case FState.PreJump:
                case FState.Land:
                    CrouchPose(ref p);
                    p.hipY += 0.15f;
                    break;

                case FState.Air:
                    //it tucks the legs when jumping so you can read a jump right away
                    p.hipY += 0.1f;
                    p.legFAng = 70f; p.legFLen = 0.6f; p.legBAng = 40f; p.legBLen = 0.6f;
                    p.armFAng = 140f; p.armFLen = 0.5f; p.armBAng = 110f; p.armBLen = 0.5f;
                    break;

                case FState.DashF:
                    p.lean = -22f; p.armFAng = 60f; p.armBAng = 40f; p.legFAng = 45f; p.legBAng = -35f;
                    break;

                case FState.DashB:
                    p.lean = 15f; p.armFAng = 100f; p.armBAng = 80f; p.legFAng = 20f; p.legBAng = -40f;
                    //it flickers during the invincible part of the backdash so you can see it
                    if (sf <= 8) { p.glow = 0.25f; p.glowColor = Color.white; }
                    break;

                case FState.Attack:
                    AttackPose(sim, f, ref p);
                    break;

                case FState.HitStun:
                    {
                        float k = f.stun > 0 ? 1f : 0.3f;
                        p.lean = 22f * k;
                        p.headTilt = 20f * k;
                        p.armFAng = -20f; p.armBAng = -35f;
                        p.legFAng = 25f; p.legBAng = -5f;
                        if (f.crouchHit) { CrouchPose(ref p); p.lean = 20f; }
                        break;
                    }

                case FState.BlockStun:
                    //this is the guard pose with both arms up in front
                    if (f.crouchBlocking) CrouchPose(ref p);
                    p.lean = 8f;
                    p.armFAng = 150f; p.armFLen = 0.42f;
                    p.armBAng = 140f; p.armBLen = 0.45f;
                    if (sf < 4) { p.tint = new Color(0.5f, 0.7f, 1f); p.tintAmount = 0.5f; }
                    break;

                case FState.AirHitStun:
                    p.rootRot = Mathf.Clamp(35f + sf * 3f, 35f, 80f);
                    p.armFAng = -40f; p.armBAng = -60f;
                    p.legFAng = 30f; p.legBAng = 10f;
                    break;

                case FState.Thrown:
                    p.rootRot = sf * 9f;
                    p.armFAng = -60f; p.armBAng = -80f;
                    break;

                case FState.Throwing:
                    p.lean = -18f;
                    p.armFAng = 90f + Mathf.Min(sf, 20) * 3f; p.armFLen = 0.75f;
                    p.armBAng = 80f + Mathf.Min(sf, 20) * 3f; p.armBLen = 0.75f;
                    break;

                case FState.Knockdown:
                case FState.KO:
                    {
                        //this is lying on the floor and getting back up at the end of the knockdown
                        float up = f.state == FState.Knockdown ? Mathf.Clamp01((12f - f.knockdownLeft) / 12f) : 0f;
                        p.rootRot = Mathf.Lerp(88f, 0f, up);
                        p.hipY = Mathf.Lerp(0.2f, HipPos.y, up);
                        p.armFAng = Mathf.Lerp(-150f, 60f, up); p.armBAng = Mathf.Lerp(-170f, 40f, up);
                        p.legFAng = Mathf.Lerp(-10f, 30f, up); p.legBAng = Mathf.Lerp(5f, -20f, up);
                        break;
                    }

                case FState.TechRecover:
                    p.lean = 20f; p.armFAng = 130f; p.armBAng = 150f;
                    p.glow = 0.3f; p.glowColor = Color.white;
                    break;

                case FState.Win:
                    p.armFAng = 175f + Mathf.Sin(t * 2f) * 8f; p.armFLen = 0.7f;
                    p.armBAng = 30f; p.legFAng = 15f; p.legBAng = -15f;
                    break;
            }

            //this flashes the fighter white for a couple frames when they get hit so every hit reads clearly
            int since = sim.frame - f.lastHurtFrame;
            if (since >= 0 && since < 3 && (f.state == FState.HitStun || f.state == FState.AirHitStun))
            {
                p.tint = Color.white; p.tintAmount = 0.85f;
            }
            return p;
        }

        void CrouchPose(ref PoseData p)
        {
            p.hipY = 0.55f;
            p.lean = -12f;
            p.legFAng = 70f; p.legFLen = 0.65f;
            p.legBAng = -45f; p.legBLen = 0.65f;
            p.armFAng = 115f; p.armFLen = 0.48f;
            p.armBAng = 95f; p.armBLen = 0.45f;
        }

        //this poses an attack from its frame data
        //it pulls back during startup then snaps the limb out to the hitbox then eases back during recovery
        //the pull back is the anticipation that lets the other player react to what they see
        void AttackPose(MatchSim sim, FighterSim f, ref PoseData p)
        {
            var m = f.Move;
            if (m == null) return;
            if (f.airborne) { p.legFAng = 60f; p.legFLen = 0.65f; p.legBAng = 30f; p.legBLen = 0.65f; p.hipY += 0.1f; }
            else if (m.crouching) CrouchPose(ref p);
            else { p.legFAng = 22f; p.legBAng = -22f; }

            bool kick = (m.buttons & Btn.Kicks) != 0 && (m.buttons & Btn.Punches) == 0;
            bool both = m.IsThrow || m.spawnsProjectile;

            //it finds the hitbox that matters right now so the limb points at where the attack really hits
            Vector2 target = new Vector2(0.9f, 1.2f);
            FrameBox hb = null;
            for (int i = 0; i < m.hitboxes.Count; i++)
            {
                var b = m.hitboxes[i];
                if (b.ActiveOn(f.moveFrame)) { hb = b; break; }
                if (hb == null || (b.start <= f.moveFrame && b.start > hb.start)) hb = b;
            }
            if (hb != null) target = new Vector2(hb.rect.cx / 1000f + hb.rect.w / 2000f * 0.6f, hb.rect.cy / 1000f);
            else if (m.spawnsProjectile) target = new Vector2(1f, (m.projectile.spawnY + m.projectile.box.cy) / 1000f);
            else if (m.IsThrow) target = new Vector2(m.throwRange / 1000f * 0.8f, 1.1f);
            target /= rigScale;

            int frame = f.moveFrame;
            int activeStart = hb != null ? hb.start : m.startup;
            int activeEnd = hb != null ? hb.end : m.startup + m.active - 1;
            float windup, extend;
            if (frame < activeStart)
            {
                float tt = activeStart <= 1 ? 1f : (frame - 1f) / Mathf.Max(1f, activeStart - 1f);
                windup = Mathf.Sin(tt * Mathf.PI * 0.5f);
                extend = frame >= activeStart - 1 ? 0.6f : 0f;
            }
            else if (frame <= activeEnd) { windup = 0f; extend = 1f; }
            else
            {
                float rec = Mathf.Max(1f, m.TotalFrames - activeEnd);
                extend = 1f - Mathf.Clamp01((frame - activeEnd) / rec);
                extend = extend * extend;
                windup = 0f;
            }

            //heavy stuff and specials glow a bit while they start up so they are easy to spot
            if (frame < activeStart && (m.hit.level >= HitLevel.Heavy || m.kind != MoveKind.Normal))
            {
                p.glow = (0.15f + 0.3f * windup) * anticipationGlow / 0.45f;
                p.glowColor = m.isSuper ? new Color(1f, 0.85f, 0.2f) : accentColor;
                p.tint = accentColor;
                p.tintAmount = 0.2f * windup;
            }
            if (m.isSuper) { p.glow = Mathf.Max(p.glow, 0.35f); p.glowColor = new Color(1f, 0.85f, 0.2f); }
            if (m.InvulnOn(frame)) { p.glow = Mathf.Max(p.glow, 0.3f); p.glowColor = Color.white; }

            p.lean = Mathf.Lerp(-8f, 14f, windup) - 18f * extend;

            Vector2 shoulder = new Vector2(0.1f, p.hipY + 0.7f);
            Vector2 hip = new Vector2(0.1f, p.hipY);
            if (kick)
            {
                var d = target - hip;
                float len = Mathf.Clamp(d.magnitude, 0.5f, 1.8f);
                float outAng = AngleTo(d);
                float pulled = 100f;
                p.legFAng = Mathf.Lerp(Mathf.Lerp(p.legFAng, pulled, windup), outAng, extend);
                p.legFLen = Mathf.Lerp(Mathf.Lerp(p.legFLen, 0.55f, windup), len, extend);
                p.lean += 10f * extend;
                p.armFAng = 110f; p.armFLen = 0.45f; p.armBAng = 60f; p.armBLen = 0.5f;
            }
            else
            {
                var d = target - shoulder;
                float len = Mathf.Clamp(d.magnitude, 0.45f, 1.7f);
                float outAng = AngleTo(d);
                p.armFAng = Mathf.Lerp(Mathf.Lerp(p.armFAng, 40f, windup), outAng, extend);
                p.armFLen = Mathf.Lerp(Mathf.Lerp(p.armFLen, 0.4f, windup), len, extend);
                if (both)
                {
                    p.armBAng = p.armFAng;
                    p.armBLen = p.armFLen * 0.95f;
                }
                else
                {
                    p.armBAng = Mathf.Lerp(100f, 30f, extend);
                    p.armBLen = 0.45f;
                }
            }
        }
    }
}

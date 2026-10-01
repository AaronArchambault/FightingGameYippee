using System;
using UnityEngine;
using UnityEngine.UI;
using FightCore;

namespace FightGame
{
    //this is the fight hud with health meter timer rounds combo counter and the announcer
    //every piece is a normal ui object in the scene so you can move it restyle it or give it your own sprites
    //if a bar Image is set to Filled it uses fill amount and if not it stretches the bar by its anchors
    //it only rebuilds text when a number actually changes so it does not make garbage every frame
    public class FightHUD : MonoBehaviour
    {
        [Serializable]
        public class SideWidgets
        {
            public Image healthFill;
            [Tooltip("the red bar behind health that drains after a combo")]
            public Image healthTrail;
            public Image meterFill;
            public Text nameText;
            public Text meterLevelText;
            [Tooltip("round win markers in order")]
            public Image[] roundPips = new Image[0];
            public Image portrait;
            [Tooltip("this gets a little punch scale when the combo goes up")]
            public RectTransform comboRoot;
            public Text comboHits;
            public Text comboDamage;
            [Tooltip("shows things like COUNTER and THROW TECH")]
            public Text popup;
        }

        class SideState
        {
            public float trailPct = 1f, trailDelay, comboShow, popupShow, comboPunch;
            public int lastCombo = -1, lastMeterLevel = -1;
        }

        public SideWidgets player1 = new SideWidgets();
        public SideWidgets player2 = new SideWidgets();

        [Header("Middle")]
        public Text timer;
        public RectTransform announcerRoot;
        public Text announcer;
        public Text subAnnouncer;
        [Tooltip("a full screen image used for the white flash on supers and ko")]
        public Image flash;

        [Header("Colors")]
        public Color healthColor = new Color(1f, 0.85f, 0.15f);
        public Color healthLowColor = new Color(1f, 0.3f, 0.15f);
        public Color trailColor = new Color(0.85f, 0.1f, 0.1f);
        public Color meterColor = new Color(0.2f, 0.7f, 1f);
        public Color meterFullColor = new Color(1f, 0.85f, 0.2f);
        public Color pipOnColor = new Color(1f, 0.8f, 0.2f);
        public Color pipOffColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        public Color comboColor = new Color(1f, 0.85f, 0.2f);
        public Color timerLowColor = new Color(1f, 0.35f, 0.3f);

        [Header("Text")]
        [Tooltip("{0} is the number of hits")]
        public string comboFormat = "{0} HITS";
        [Tooltip("{0} is the damage")]
        public string damageFormat = "{0} DAMAGE";
        public string noTimerText = "--";

        [Header("Feel")]
        [Range(0f, 1f)] public float lowHealthPercent = 0.25f;
        public float trailDelay = 0.4f;
        public float trailDrainSpeed = 0.6f;
        public float comboLinger = 1.2f;

        readonly SideState[] state = { new SideState(), new SideState() };
        float announceTime, announceDur, flashAlpha;
        int lastTimer = int.MinValue;
        Canvas canvas;

        SideWidgets Side(int i) { return i == 0 ? player1 : player2; }

        void Awake() { canvas = GetComponentInParent<Canvas>(); }

        //this is the one the game makes if your scene has no hud
        public static FightHUD Create()
        {
            var c = UIFactory.Canvas("FightHUD", 10);
            var hud = c.gameObject.AddComponent<FightHUD>();
            hud.BuildDefault();
            return hud;
        }

        public void SetNames(string p1, string p2)
        {
            if (player1.nameText != null) player1.nameText.text = p1;
            if (player2.nameText != null) player2.nameText.text = p2;
            for (int i = 0; i < 2; i++) { state[i].trailPct = 1f; state[i].lastCombo = -1; state[i].lastMeterLevel = -1; }
            lastTimer = int.MinValue;
        }

        public void SetPortraits(Sprite p1, Sprite p2)
        {
            SetPortrait(player1.portrait, p1);
            SetPortrait(player2.portrait, p2);
        }

        static void SetPortrait(Image img, Sprite s)
        {
            if (img == null) return;
            img.sprite = s;
            img.enabled = s != null;
        }

        public void Announce(string text, float seconds, string sub = "")
        {
            if (announcer != null) announcer.text = text;
            if (subAnnouncer != null) subAnnouncer.text = sub;
            announceTime = 0f;
            announceDur = seconds;
        }

        public void Flash(float alpha) { flashAlpha = Mathf.Max(flashAlpha, alpha); }

        //this shows the little label under the combo like counter or throw tech
        public void Popup(int side, string text, Color c)
        {
            var w = Side(side);
            if (w.popup == null) return;
            w.popup.text = text;
            w.popup.color = c;
            state[side].popupShow = comboLinger;
        }

        public void SetVisible(bool v)
        {
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.gameObject == gameObject) canvas.enabled = v;
            else gameObject.SetActive(v);
        }

        public void Tick(MatchSim sim, float dt)
        {
            if (sim == null) return;
            for (int i = 0; i < 2; i++) TickSide(sim, i, dt);

            int t = (sim.training || !sim.HasTimer) ? -1 : sim.TimerSeconds;
            if (t != lastTimer && timer != null)
            {
                lastTimer = t;
                timer.text = t < 0 ? noTimerText : t.ToString();
                timer.color = t >= 0 && t <= 10 ? timerLowColor : Color.white;
            }

            //this makes the announcer text pop in big then settle then fade out
            announceTime += dt;
            if (announceDur > 0f && announcer != null)
            {
                float k = announceTime / announceDur;
                float scale = k < 0.08f ? Mathf.Lerp(2f, 1f, k / 0.08f) : 1f;
                float alpha = k < 0.8f ? 1f : Mathf.Clamp01((1f - k) / 0.2f);
                if (announcerRoot != null) announcerRoot.localScale = Vector3.one * scale;
                announcer.color = FightUtil.WithAlpha(announcer.color, alpha);
                if (subAnnouncer != null) subAnnouncer.color = FightUtil.WithAlpha(subAnnouncer.color, alpha);
                if (k >= 1f) { announceDur = 0f; announcer.text = ""; if (subAnnouncer != null) subAnnouncer.text = ""; }
            }

            flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, dt * 3f);
            if (flash != null) flash.color = FightUtil.WithAlpha(flash.color, flashAlpha);
        }

        void TickSide(MatchSim sim, int i, float dt)
        {
            var w = Side(i);
            var s = state[i];
            var f = sim.fighters[i];
            bool left = i == 0;
            float pct = Mathf.Clamp01(f.health / (float)f.def.maxHealth);
            SetBar(w.healthFill, pct, left);
            if (w.healthFill != null)
                w.healthFill.color = pct < lowHealthPercent ? Color.Lerp(healthLowColor, healthColor, Mathf.PingPong(Time.unscaledTime * 3f, 1f) * 0.5f) : healthColor;

            //the red trailing bar waits until the combo is over then drains so you can see how much a combo did
            bool beingComboed = f.state == FState.HitStun || f.state == FState.AirHitStun;
            if (pct >= s.trailPct) { s.trailPct = pct; s.trailDelay = trailDelay; }
            else if (beingComboed) s.trailDelay = trailDelay;
            else if (s.trailDelay > 0f) s.trailDelay -= dt;
            else s.trailPct = Mathf.MoveTowards(s.trailPct, pct, dt * trailDrainSpeed);
            SetBar(w.healthTrail, s.trailPct, left);

            float m = f.meter / (float)FighterSim.MaxMeter;
            SetBar(w.meterFill, m, left);
            int level = f.meter / 100;
            if (level != s.lastMeterLevel && w.meterLevelText != null) { s.lastMeterLevel = level; w.meterLevelText.text = level.ToString(); }
            if (w.meterFill != null)
            {
                bool full = f.meter >= FighterSim.MaxMeter;
                w.meterFill.color = full ? Color.Lerp(meterFullColor, Color.white, Mathf.PingPong(Time.unscaledTime * 4f, 1f) * 0.5f)
                                         : (level > 0 ? Color.Lerp(meterColor, meterFullColor, 0.35f) : meterColor);
            }

            if (w.roundPips != null)
                for (int p = 0; p < w.roundPips.Length; p++)
                {
                    if (w.roundPips[p] == null) continue;
                    w.roundPips[p].gameObject.SetActive(p < sim.roundsToWin);
                    w.roundPips[p].color = f.roundWins > p ? pipOnColor : pipOffColor;
                }

            //the combo counter shows on the attacker side and counts the hits on the other fighter
            var victim = sim.fighters[1 - i];
            int hits = victim.comboHits;
            bool active = victim.state == FState.HitStun || victim.state == FState.AirHitStun;
            if (active && hits >= 2)
            {
                if (hits != s.lastCombo)
                {
                    s.lastCombo = hits;
                    if (w.comboHits != null) w.comboHits.text = string.Format(comboFormat, hits);
                    if (w.comboDamage != null) w.comboDamage.text = string.Format(damageFormat, victim.comboDamage);
                    s.comboPunch = 1f;
                }
                s.comboShow = comboLinger;
            }
            else s.comboShow -= dt;
            if (!active) s.lastCombo = -1;
            s.comboPunch = Mathf.MoveTowards(s.comboPunch, 0f, dt * 6f);
            if (w.comboRoot != null) w.comboRoot.localScale = Vector3.one * (1f + s.comboPunch * 0.25f);
            float ca = Mathf.Clamp01(s.comboShow / 0.3f);
            if (w.comboHits != null) w.comboHits.color = FightUtil.WithAlpha(comboColor, ca);
            if (w.comboDamage != null) w.comboDamage.color = FightUtil.WithAlpha(w.comboDamage.color, ca);

            s.popupShow -= dt;
            if (w.popup != null) w.popup.color = FightUtil.WithAlpha(w.popup.color, Mathf.Clamp01(s.popupShow / 0.3f));
        }

        //it fills player one bars from the left and player two bars from the right so health drains out toward the edges like street fighter
        static void SetBar(Image img, float pct, bool fromLeft)
        {
            if (img == null) return;
            if (img.type == Image.Type.Filled) { img.fillAmount = pct; return; }
            var rt = img.rectTransform;
            if (fromLeft) { rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(pct, 1f); }
            else { rt.anchorMin = new Vector2(1f - pct, 0f); rt.anchorMax = new Vector2(1f, 1f); }
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        //this builds the default layout as real objects and fills in all the fields
        //the editor scene builder calls this so you get a hud you can edit right in the scene
        public void BuildDefault()
        {
            var root = transform;
            BuildSide(root, 0, player1);
            BuildSide(root, 1, player2);

            UIFactory.Box("TimerBack", root, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(130f, 100f), new Color(0f, 0f, 0f, 0.6f));
            timer = UIFactory.Text("Timer", root, "99", 72, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(130f, 100f), TextAnchor.MiddleCenter, Color.white);

            announcerRoot = UIFactory.Rect("Announcer", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(1400f, 200f));
            announcer = UIFactory.Text("Text", announcerRoot, "", 150, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 200f), TextAnchor.MiddleCenter, Color.white);
            announcer.GetComponent<Outline>().effectDistance = new Vector2(5f, -5f);
            subAnnouncer = UIFactory.Text("SubAnnouncer", root, "", 48, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(1400f, 80f), TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.4f));

            flash = UIFactory.Image("Flash", root, new Color(1f, 1f, 1f, 0f));
        }

        void BuildSide(Transform root, int i, SideWidgets w)
        {
            bool left = i == 0;
            float sign = left ? -1f : 1f;
            var side = UIFactory.Fill("Player" + (i + 1), root);
            var anchor = new Vector2(0.5f, 1f);
            float barW = 720f, barH = 44f;
            var barPos = new Vector2(sign * (barW * 0.5f + 80f), -60f);

            var frame = UIFactory.Box("HealthFrame", side, anchor, barPos, new Vector2(barW + 8f, barH + 8f), new Color(0f, 0f, 0f, 0.8f));
            var back = UIFactory.Image("Back", frame.transform, new Color(0.15f, 0.15f, 0.2f, 1f));
            back.rectTransform.offsetMin = new Vector2(4f, 4f); back.rectTransform.offsetMax = new Vector2(-4f, -4f);
            w.healthTrail = UIFactory.Image("Trail", back.transform, trailColor);
            w.healthFill = UIFactory.Image("Fill", back.transform, healthColor);

            w.nameText = UIFactory.Text("Name", side, "", 36, anchor, new Vector2(barPos.x, -110f), new Vector2(barW, 44f), left ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, Color.white);

            var portrait = UIFactory.Box("Portrait", side, anchor, new Vector2(sign * (barW + 150f), -80f), new Vector2(110f, 110f), Color.white);
            portrait.preserveAspect = true;
            portrait.enabled = false;
            w.portrait = portrait;

            //these are the round win dots next to the timer
            w.roundPips = new Image[3];
            for (int p = 0; p < w.roundPips.Length; p++)
                w.roundPips[p] = UIFactory.Box("RoundPip" + (p + 1), side, anchor, new Vector2(sign * (110f + p * 34f), -108f), new Vector2(24f, 24f), pipOffColor);

            //this is the super meter at the bottom split into three bars
            float mW = 480f, mH = 26f;
            var mAnchor = new Vector2(left ? 0f : 1f, 0f);
            var mPos = new Vector2(-sign * (mW * 0.5f + 60f), 50f);
            var mFrame = UIFactory.Box("MeterFrame", side, mAnchor, mPos, new Vector2(mW + 6f, mH + 6f), new Color(0f, 0f, 0f, 0.8f));
            var mBack = UIFactory.Image("Back", mFrame.transform, new Color(0.1f, 0.12f, 0.2f, 1f));
            mBack.rectTransform.offsetMin = new Vector2(3f, 3f); mBack.rectTransform.offsetMax = new Vector2(-3f, -3f);
            w.meterFill = UIFactory.Image("Fill", mBack.transform, meterColor);
            for (int d = 1; d < 3; d++)
            {
                var div = UIFactory.Image("Divider", mBack.transform, new Color(0f, 0f, 0f, 0.9f));
                div.rectTransform.anchorMin = new Vector2(d / 3f, 0f);
                div.rectTransform.anchorMax = new Vector2(d / 3f, 1f);
                div.rectTransform.offsetMin = new Vector2(-2f, 0f);
                div.rectTransform.offsetMax = new Vector2(2f, 0f);
            }
            w.meterLevelText = UIFactory.Text("MeterLevel", side, "0", 44, mAnchor, mPos + new Vector2(-sign * (mW * 0.5f + 34f), 4f), new Vector2(60f, 60f), TextAnchor.MiddleCenter, meterColor);

            //this is the combo counter on your side of the screen
            w.comboRoot = UIFactory.Rect("Combo", side, new Vector2(left ? 0f : 1f, 0.5f), new Vector2(left ? 0f : 1f, 0.5f), new Vector2(-sign * 230f, 120f), new Vector2(400f, 200f));
            var align = left ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
            w.comboHits = UIFactory.Text("Hits", w.comboRoot, "", 80, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(400f, 100f), align, comboColor);
            w.comboDamage = UIFactory.Text("Damage", w.comboRoot, "", 34, new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(400f, 50f), align, Color.white);
            w.popup = UIFactory.Text("Popup", w.comboRoot, "", 40, new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(400f, 50f), align, new Color(1f, 0.35f, 0.3f));
        }
    }
}

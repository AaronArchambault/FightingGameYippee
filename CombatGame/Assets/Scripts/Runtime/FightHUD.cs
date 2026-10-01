using UnityEngine;
using UnityEngine.UI;
using FightCore;

namespace FightGame
{
    //this is the fight hud with health meter timer rounds combo counter and the announcer
    //it only rebuilds text when a number actually changes so it does not make garbage every frame
    public class FightHUD : MonoBehaviour
    {
        class Side
        {
            public RectTransform fill, trail, meterFill;
            public Image fillImg, meterImg;
            public Text name, meterText, comboText, comboDmg, popup;
            public Image[] pips;
            public float trailPct = 1f, trailDelay;
            public float comboShow, popupShow;
            public int lastCombo = -1, lastMeterLevel = -1;
            public RectTransform comboRoot;
            public float comboPunch;
        }

        readonly Side[] sides = { new Side(), new Side() };
        Text timer, announcer, subAnnouncer;
        RectTransform announcerRt;
        Image flash;
        float announceTime, announceDur, flashAlpha;
        int lastTimer = -1;
        Canvas canvas;
        public bool trainingLabel;

        static readonly Color HealthColor = new Color(1f, 0.85f, 0.15f);
        static readonly Color HealthLow = new Color(1f, 0.3f, 0.15f);
        static readonly Color TrailColor = new Color(0.85f, 0.1f, 0.1f);
        static readonly Color MeterColor = new Color(0.2f, 0.7f, 1f);
        static readonly Color MeterFull = new Color(1f, 0.85f, 0.2f);

        public static FightHUD Create()
        {
            var c = UIFactory.Canvas("FightHUD", 10);
            var hud = c.gameObject.AddComponent<FightHUD>();
            hud.canvas = c;
            hud.Build();
            return hud;
        }

        void Build()
        {
            var root = canvas.transform;
            for (int i = 0; i < 2; i++) BuildSide(root, i);

            UIFactory.Box("TimerBack", root, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(130f, 100f), new Color(0f, 0f, 0f, 0.6f));
            timer = UIFactory.Text("Timer", root, "99", 72, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(130f, 100f), TextAnchor.MiddleCenter, Color.white);

            announcerRt = UIFactory.Rect("Announcer", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(1400f, 200f));
            announcer = UIFactory.Text("Text", announcerRt, "", 150, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 200f), TextAnchor.MiddleCenter, Color.white);
            announcer.GetComponent<Outline>().effectDistance = new Vector2(5f, -5f);
            subAnnouncer = UIFactory.Text("Sub", root, "", 48, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(1400f, 80f), TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.4f));

            flash = UIFactory.Image("Flash", root, new Color(1f, 1f, 1f, 0f));
        }

        void BuildSide(Transform root, int i)
        {
            var s = sides[i];
            bool left = i == 0;
            float sign = left ? -1f : 1f;
            var anchor = new Vector2(0.5f, 1f);
            float barW = 720f, barH = 44f;
            var barPos = new Vector2(sign * (barW * 0.5f + 80f), -60f);

            var frame = UIFactory.Box("HealthFrame" + i, root, anchor, barPos, new Vector2(barW + 8f, barH + 8f), new Color(0f, 0f, 0f, 0.8f));
            var back = UIFactory.Image("Back", frame.transform, new Color(0.15f, 0.15f, 0.2f, 1f));
            back.rectTransform.offsetMin = new Vector2(4f, 4f); back.rectTransform.offsetMax = new Vector2(-4f, -4f);
            var trailImg = UIFactory.Image("Trail", back.transform, TrailColor);
            s.trail = trailImg.rectTransform;
            s.fillImg = UIFactory.Image("Fill", back.transform, HealthColor);
            s.fill = s.fillImg.rectTransform;

            s.name = UIFactory.Text("Name" + i, root, "", 36, anchor, barPos, new Vector2(barW, 44f), left ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, Color.white);
            s.name.rectTransform.anchoredPosition = new Vector2(barPos.x, -110f);

            //these are the round win dots next to the timer
            s.pips = new Image[2];
            for (int p = 0; p < 2; p++)
            {
                s.pips[p] = UIFactory.Box("Pip" + p, root, anchor, new Vector2(sign * (110f + p * 34f), -108f), new Vector2(24f, 24f), new Color(0.2f, 0.2f, 0.2f, 0.9f));
            }

            //this is the super meter at the bottom split into three bars
            float mW = 480f, mH = 26f;
            var mAnchor = new Vector2(left ? 0f : 1f, 0f);
            var mPos = new Vector2(-sign * (mW * 0.5f + 60f), 50f);
            var mFrame = UIFactory.Box("MeterFrame" + i, root, mAnchor, mPos, new Vector2(mW + 6f, mH + 6f), new Color(0f, 0f, 0f, 0.8f));
            var mBack = UIFactory.Image("Back", mFrame.transform, new Color(0.1f, 0.12f, 0.2f, 1f));
            mBack.rectTransform.offsetMin = new Vector2(3f, 3f); mBack.rectTransform.offsetMax = new Vector2(-3f, -3f);
            s.meterImg = UIFactory.Image("Fill", mBack.transform, MeterColor);
            s.meterFill = s.meterImg.rectTransform;
            for (int d = 1; d < 3; d++)
            {
                var div = UIFactory.Image("Div", mBack.transform, new Color(0f, 0f, 0f, 0.9f));
                div.rectTransform.anchorMin = new Vector2(d / 3f, 0f);
                div.rectTransform.anchorMax = new Vector2(d / 3f, 1f);
                div.rectTransform.offsetMin = new Vector2(-2f, 0f);
                div.rectTransform.offsetMax = new Vector2(2f, 0f);
            }
            s.meterText = UIFactory.Text("MeterLv" + i, root, "0", 44, mAnchor, mPos + new Vector2(-sign * (mW * 0.5f + 34f), 4f), new Vector2(60f, 60f), TextAnchor.MiddleCenter, MeterColor);

            //this is the combo counter on your side of the screen
            s.comboRoot = UIFactory.Rect("Combo" + i, root, new Vector2(left ? 0f : 1f, 0.5f), new Vector2(left ? 0f : 1f, 0.5f), new Vector2(-sign * 230f, 120f), new Vector2(400f, 200f));
            var align = left ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
            s.comboText = UIFactory.Text("Hits", s.comboRoot, "", 80, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(400f, 100f), align, new Color(1f, 0.85f, 0.2f));
            s.comboDmg = UIFactory.Text("Dmg", s.comboRoot, "", 34, new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(400f, 50f), align, Color.white);
            s.popup = UIFactory.Text("Popup", s.comboRoot, "", 40, new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(400f, 50f), align, new Color(1f, 0.35f, 0.3f));
        }

        public void SetNames(string p1, string p2)
        {
            sides[0].name.text = p1;
            sides[1].name.text = p2;
            for (int i = 0; i < 2; i++) { sides[i].trailPct = 1f; sides[i].lastCombo = -1; sides[i].lastMeterLevel = -1; }
        }

        public void Announce(string text, float seconds, string sub = "")
        {
            announcer.text = text;
            subAnnouncer.text = sub;
            announceTime = 0f;
            announceDur = seconds;
        }

        public void Flash(float alpha) { flashAlpha = Mathf.Max(flashAlpha, alpha); }

        //this shows the little label under the combo like counter or reversal
        public void Popup(int attackerSide, string text, Color c)
        {
            var s = sides[attackerSide];
            s.popup.text = text;
            s.popup.color = c;
            s.popupShow = 1.2f;
        }

        public void SetVisible(bool v) { canvas.enabled = v; }

        public void Tick(MatchSim sim, float dt)
        {
            if (sim == null) return;
            for (int i = 0; i < 2; i++) TickSide(sim, i, dt);

            int t = sim.training ? -1 : sim.TimerSeconds;
            if (t != lastTimer)
            {
                lastTimer = t;
                timer.text = t < 0 ? "--" : t.ToString();
                timer.color = t >= 0 && t <= 10 ? new Color(1f, 0.35f, 0.3f) : Color.white;
            }

            //this makes the announcer text pop in big then settle then fade out
            announceTime += dt;
            if (announceDur > 0f)
            {
                float k = announceTime / announceDur;
                float scale = k < 0.08f ? Mathf.Lerp(2f, 1f, k / 0.08f) : 1f;
                float alpha = k < 0.8f ? 1f : Mathf.Clamp01((1f - k) / 0.2f);
                announcerRt.localScale = Vector3.one * scale;
                announcer.color = new Color(1f, 1f, 1f, alpha);
                subAnnouncer.color = new Color(1f, 0.9f, 0.4f, alpha);
                if (k >= 1f) { announceDur = 0f; announcer.text = ""; subAnnouncer.text = ""; }
            }

            flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, dt * 3f);
            flash.color = new Color(1f, 1f, 1f, flashAlpha);
        }

        void TickSide(MatchSim sim, int i, float dt)
        {
            var s = sides[i];
            var f = sim.fighters[i];
            bool left = i == 0;
            float pct = Mathf.Clamp01(f.health / (float)f.def.maxHealth);
            SetBar(s.fill, pct, left);
            s.fillImg.color = pct < 0.25f ? Color.Lerp(HealthLow, HealthColor, Mathf.PingPong(Time.unscaledTime * 3f, 1f) * 0.5f) : HealthColor;

            //the red trailing bar waits until the combo is over then drains so you can see how much a combo did
            bool beingComboed = f.state == FState.HitStun || f.state == FState.AirHitStun;
            if (pct >= s.trailPct) { s.trailPct = pct; s.trailDelay = 0.4f; }
            else if (beingComboed) s.trailDelay = 0.4f;
            else if (s.trailDelay > 0f) s.trailDelay -= dt;
            else s.trailPct = Mathf.MoveTowards(s.trailPct, pct, dt * 0.6f);
            SetBar(s.trail, s.trailPct, left);

            float m = f.meter / (float)FighterSim.MaxMeter;
            SetBar(s.meterFill, m, left);
            int level = f.meter / 100;
            if (level != s.lastMeterLevel) { s.lastMeterLevel = level; s.meterText.text = level.ToString(); }
            bool full = f.meter >= FighterSim.MaxMeter;
            s.meterImg.color = full ? Color.Lerp(MeterFull, Color.white, Mathf.PingPong(Time.unscaledTime * 4f, 1f) * 0.5f) : (level > 0 ? Color.Lerp(MeterColor, MeterFull, 0.35f) : MeterColor);

            for (int p = 0; p < s.pips.Length; p++)
                s.pips[p].color = f.roundWins > p ? new Color(1f, 0.8f, 0.2f) : new Color(0.2f, 0.2f, 0.2f, 0.9f);

            //the combo counter shows on the attacker side and counts the hits on the other fighter
            var victim = sim.fighters[1 - i];
            int hits = victim.comboHits;
            bool active = victim.state == FState.HitStun || victim.state == FState.AirHitStun;
            if (active && hits >= 2)
            {
                if (hits != s.lastCombo)
                {
                    s.lastCombo = hits;
                    s.comboText.text = hits + " HITS";
                    s.comboDmg.text = victim.comboDamage + " DAMAGE";
                    s.comboPunch = 1f;
                }
                s.comboShow = 1.2f;
            }
            else s.comboShow -= dt;
            if (!active) s.lastCombo = -1;
            s.comboPunch = Mathf.MoveTowards(s.comboPunch, 0f, dt * 6f);
            s.comboRoot.localScale = Vector3.one * (1f + s.comboPunch * 0.25f);
            float ca = Mathf.Clamp01(s.comboShow / 0.3f);
            s.comboText.color = new Color(1f, 0.85f, 0.2f, ca);
            s.comboDmg.color = new Color(1f, 1f, 1f, ca);

            s.popupShow -= dt;
            var pc = s.popup.color; pc.a = Mathf.Clamp01(s.popupShow / 0.3f); s.popup.color = pc;
        }

        //it fills player one bars from the left and player two bars from the right so health drains out toward the edges like street fighter
        static void SetBar(RectTransform rt, float pct, bool fromLeft)
        {
            if (fromLeft) { rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(pct, 1f); }
            else { rt.anchorMin = new Vector2(1f - pct, 0f); rt.anchorMax = new Vector2(1f, 1f); }
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}

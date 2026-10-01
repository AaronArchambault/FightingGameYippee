using System.Collections.Generic;

namespace FightCore
{
    //this builds the default fighters in code so the game works right away without making any assets
    //you can turn these into editable assets with the menu in the editor and then tweak them in the hitbox editor
    public static class RosterFactory
    {
        public static FighterDef[] CreateDefaultRoster()
        {
            return new[] { Kaito(), Brick(), Vex(), Lumen() };
        }

        //this is a little settings bundle so each fighter can have faster or stronger normals
        struct Tune
        {
            public int startupMod, dmgPct, reachPct;
            public Tune(int s, int d, int r) { startupMod = s; dmgPct = d; reachPct = r; }
        }

        static HitData Hit(int dmg, int hitstun, int blockstun, HitLevel lvl, GuardType g = GuardType.Mid)
        {
            var h = new HitData();
            h.damage = dmg;
            h.hitstun = hitstun;
            h.blockstun = blockstun;
            h.level = lvl;
            h.guard = g;
            switch (lvl)
            {
                case HitLevel.Light: h.hitstop = 8; h.pushbackHit = 420; h.pushbackBlock = 600; h.meterGain = 8; break;
                case HitLevel.Medium: h.hitstop = 10; h.pushbackHit = 550; h.pushbackBlock = 750; h.meterGain = 15; break;
                case HitLevel.Heavy: h.hitstop = 12; h.pushbackHit = 700; h.pushbackBlock = 900; h.meterGain = 22; break;
                case HitLevel.Special: h.hitstop = 11; h.pushbackHit = 650; h.pushbackBlock = 850; h.meterGain = 25; break;
                case HitLevel.Super: h.hitstop = 7; h.pushbackHit = 150; h.pushbackBlock = 300; h.meterGain = 0; break;
            }
            return h;
        }

        //this makes a normal attack with one hitbox on its active frames
        //it also stretches the hurtbox out onto the limb so whiffing a big button can get you hit
        static MoveDef Normal(string id, string name, DirReq dir, Btn btn, int st, int act, int rec, HitData hit, BoxRect box, Tune t)
        {
            st = System.Math.Max(3, st + t.startupMod);
            hit.damage = hit.damage * t.dmgPct / 100;
            box.cx = box.cx * t.reachPct / 100;
            box.w = box.w * t.reachPct / 100;
            var m = new MoveDef();
            m.id = id;
            m.displayName = name;
            m.kind = MoveKind.Normal;
            m.dir = dir;
            m.buttons = btn;
            m.startup = st;
            m.active = act;
            m.recovery = rec;
            m.hit = hit;
            m.crouching = dir == DirReq.Crouch || dir == DirReq.DownForward || dir == DirReq.DownBack;
            m.hitboxes.Add(new FrameBox(st, st + act - 1, box));
            if (dir != DirReq.Air && dir != DirReq.AirDown)
                m.hurtboxes.Add(new FrameBox(st, st + act - 1 + rec / 2, new BoxRect(box.cx - box.w / 10, box.cy, box.w * 8 / 10, box.h)));
            if (hit.level == HitLevel.Light) m.aiTags |= AITag.Fast;
            return m;
        }

        //this is the full set of normals everyone gets and then each fighter tweaks it
        static void AddStandardNormals(FighterDef f, Tune t)
        {
            var m = f.moves;
            var stlp = Normal("st_lp", "Standing Jab", DirReq.Stand, Btn.LP, 4, 2, 7, Hit(300, 12, 10, HitLevel.Light), new BoxRect(550, 1350, 450, 220), t);
            stlp.chains.Add("st_lp"); stlp.chains.Add("cr_lp"); stlp.specialCancel = true; stlp.aiTags |= AITag.Poke;
            m.Add(stlp);
            var stmp = Normal("st_mp", "Standing Strong", DirReq.Stand, Btn.MP, 6, 3, 12, Hit(550, 17, 13, HitLevel.Medium), new BoxRect(650, 1250, 600, 260), t);
            stmp.specialCancel = true; stmp.aiTags |= AITag.Poke;
            m.Add(stmp);
            var sthp = Normal("st_hp", "Standing Fierce", DirReq.Stand, Btn.HP, 9, 4, 19, Hit(850, 21, 16, HitLevel.Heavy), new BoxRect(720, 1300, 700, 320), t);
            sthp.specialCancel = true;
            m.Add(sthp);
            m.Add(Normal("st_lk", "Standing Short", DirReq.Stand, Btn.LK, 5, 3, 9, Hit(300, 13, 11, HitLevel.Light), new BoxRect(600, 650, 560, 280), t));
            var stmk = Normal("st_mk", "Standing Forward", DirReq.Stand, Btn.MK, 7, 3, 15, Hit(550, 17, 13, HitLevel.Medium), new BoxRect(780, 900, 700, 260), t);
            stmk.aiTags |= AITag.Poke;
            m.Add(stmk);
            m.Add(Normal("st_hk", "Standing Roundhouse", DirReq.Stand, Btn.HK, 11, 4, 20, Hit(900, 21, 15, HitLevel.Heavy), new BoxRect(820, 1450, 700, 360), t));

            var crlp = Normal("cr_lp", "Crouching Jab", DirReq.Crouch, Btn.LP, 4, 2, 7, Hit(280, 12, 10, HitLevel.Light), new BoxRect(560, 800, 450, 220), t);
            crlp.chains.Add("cr_lp"); crlp.chains.Add("st_lp"); crlp.specialCancel = true;
            m.Add(crlp);
            var crmp = Normal("cr_mp", "Crouching Strong", DirReq.Crouch, Btn.MP, 6, 3, 12, Hit(520, 17, 13, HitLevel.Medium), new BoxRect(600, 900, 560, 260), t);
            crmp.specialCancel = true;
            m.Add(crmp);
            var crhp = Normal("cr_hp", "Crouching Fierce", DirReq.Crouch, Btn.HP, 7, 4, 20, Hit(850, 20, 14, HitLevel.Heavy), new BoxRect(420, 1650, 480, 900), t);
            crhp.specialCancel = true; crhp.aiTags |= AITag.AntiAir;
            m.Add(crhp);
            var crlk = Normal("cr_lk", "Crouching Short", DirReq.Crouch, Btn.LK, 5, 2, 9, Hit(250, 12, 10, HitLevel.Light, GuardType.Low), new BoxRect(620, 200, 560, 240), t);
            crlk.chains.Add("cr_lp"); crlk.aiTags |= AITag.Low;
            m.Add(crlk);
            var crmk = Normal("cr_mk", "Crouching Forward", DirReq.Crouch, Btn.MK, 7, 3, 14, Hit(500, 16, 13, HitLevel.Medium, GuardType.Low), new BoxRect(820, 220, 760, 260), t);
            crmk.specialCancel = true; crmk.aiTags |= AITag.Low | AITag.Poke;
            m.Add(crmk);
            var sweepHit = Hit(900, 20, 14, HitLevel.Heavy, GuardType.Low);
            sweepHit.effect = HitEffect.HardKnockdown; sweepHit.launchVY = 60;
            var crhk = Normal("cr_hk", "Sweep", DirReq.Crouch, Btn.HK, 9, 3, 24, sweepHit, new BoxRect(880, 200, 820, 260), t);
            crhk.aiTags |= AITag.Low;
            m.Add(crhk);

            m.Add(Air(Normal("j_lp", "Jumping Jab", DirReq.Air, Btn.LP, 4, 8, 3, Hit(300, 14, 12, HitLevel.Light, GuardType.High), new BoxRect(450, 700, 450, 300), t)));
            m.Add(Air(Normal("j_mp", "Jumping Strong", DirReq.Air, Btn.MP, 5, 6, 4, Hit(550, 17, 14, HitLevel.Medium, GuardType.High), new BoxRect(550, 650, 500, 300), t)));
            m.Add(Air(Normal("j_hp", "Jumping Fierce", DirReq.Air, Btn.HP, 7, 5, 5, Hit(800, 20, 16, HitLevel.Heavy, GuardType.High), new BoxRect(600, 500, 550, 450), t)));
            m.Add(Air(Normal("j_lk", "Jumping Short", DirReq.Air, Btn.LK, 4, 8, 3, Hit(300, 14, 12, HitLevel.Light, GuardType.High), new BoxRect(450, 300, 500, 300), t)));
            m.Add(Air(Normal("j_mk", "Jumping Forward", DirReq.Air, Btn.MK, 5, 7, 4, Hit(550, 17, 14, HitLevel.Medium, GuardType.High), new BoxRect(550, 350, 600, 350), t)));
            m.Add(Air(Normal("j_hk", "Jumping Roundhouse", DirReq.Air, Btn.HK, 7, 6, 5, Hit(850, 20, 16, HitLevel.Heavy, GuardType.High), new BoxRect(600, 250, 650, 400), t)));

            AddThrows(f, 1200);
        }

        static MoveDef Air(MoveDef m)
        {
            m.aiTags |= AITag.AirAttack;
            m.useAirHurtbox = true;
            return m;
        }

        static void AddThrows(FighterDef f, int dmg)
        {
            var h = Hit(dmg, 0, 0, HitLevel.Heavy, GuardType.Unblockable);
            h.launchVY = 140; h.launchVX = 60; h.meterGain = 20;
            var fwd = new MoveDef { id = "throw_f", displayName = "Throw", kind = MoveKind.Throw, dir = DirReq.Any, buttons = Btn.LP | Btn.LK, startup = 5, active = 3, recovery = 23, throwRange = 850, hit = h, aiTags = AITag.Throw };
            var back = new MoveDef { id = "throw_b", displayName = "Back Throw", kind = MoveKind.Throw, dir = DirReq.Back, buttons = Btn.LP | Btn.LK, startup = 5, active = 3, recovery = 23, throwRange = 850, hit = h.Clone(), throwSwapSides = true, priority = 1 };
            f.moves.Add(fwd);
            f.moves.Add(back);
        }

        static MoveDef Special(string id, string name, Motion motion, Btn btn, int st, int act, int rec, HitData hit)
        {
            return new MoveDef
            {
                id = id, displayName = name, kind = MoveKind.Special, motion = motion, dir = DirReq.Any, buttons = btn,
                startup = st, active = act, recovery = rec, hit = hit
            };
        }

        static MoveDef Fireball(string id, string name, Motion motion, Btn btn, int speed, int startup, int recovery, GuardType guard, BoxRect box)
        {
            var h = Hit(600, 18, 16, HitLevel.Special, guard);
            h.chip = 120; h.hitstop = 8; h.pushbackBlock = 900; h.meterGain = 20;
            var m = Special(id, name, motion, btn, startup, 1, recovery, h);
            m.spawnsProjectile = true;
            m.projectileFrame = startup;
            m.projectile = new ProjectileDef { speed = speed, lifetime = 160, box = box, hit = h, durability = 1, spawnX = 700 };
            m.aiTags = AITag.Projectile;
            return m;
        }

        static MoveDef Uppercut(string id, string name, Motion motion, Btn btn, int dmg, int vy, int vx, int invulnEnd, int landing)
        {
            var h = Hit(dmg, 0, 20, HitLevel.Special);
            h.effect = HitEffect.Knockdown; h.launchVY = 210; h.launchVX = 30; h.hitstop = 13;
            var m = Special(id, name, motion, btn, 3, 9, 16, h);
            m.invulnStart = 1; m.invulnEnd = invulnEnd; m.throwInvuln = true;
            m.velocity.Add(new VelocityKey(3, vx, vy, true));
            m.hitboxes.Add(new FrameBox(3, 11, new BoxRect(350, 1300, 520, 1000)));
            m.landingRecovery = landing;
            m.aiTags = AITag.AntiAir | AITag.Reversal;
            return m;
        }

        static MoveDef Rush(string id, string name, Motion motion, Btn btn, int hits, int dmgEach, int speed)
        {
            var h = Hit(dmgEach, 30, 12, HitLevel.Super);
            h.chip = 150; h.chipCanKO = true;
            int startup = 5;
            int active = hits * 6;
            var m = Special(id, name, motion, btn, startup, active, 30, h);
            m.isSuper = true;
            m.meterCost = 200;
            m.superFreeze = 40;
            m.invulnStart = 1; m.invulnEnd = 14; m.throwInvuln = true;
            m.velocity.Add(new VelocityKey(1, speed));
            m.velocity.Add(new VelocityKey(startup + active - 2, 0));
            for (int i = 0; i < hits; i++)
                m.hitboxes.Add(new FrameBox(startup + i * 6, startup + i * 6 + 2, new BoxRect(600, 1100, 750, 1000), i));
            m.finisherGroup = hits - 1;
            m.finisherEffect = HitEffect.HardKnockdown;
            m.aiTags = AITag.Super | AITag.Reversal;
            return m;
        }

        //this is Kaito and he is the all rounder with a fireball an uppercut and a spinning kick
        static FighterDef Kaito()
        {
            var f = new FighterDef { id = "kaito", displayName = "Kaito", archetype = Archetype.AllRounder, colorRGB = 0xF2F2F2, accentRGB = 0xC62828, maxHealth = 10000 };
            AddStandardNormals(f, new Tune(0, 100, 100));

            var oh = Normal("oh_mp", "Axe Chop", DirReq.Forward, Btn.MP, 18, 2, 14, Hit(500, 16, 12, HitLevel.Medium, GuardType.High), new BoxRect(600, 1100, 550, 800), new Tune(0, 100, 100));
            oh.aiTags |= AITag.Overhead;
            f.moves.Add(oh);

            var fbBox = new BoxRect(0, 1150, 500, 400);
            f.moves.Add(Fireball("fb_l", "Hadoken L", Motion.QCF, Btn.LP, 55, 12, 32, GuardType.Mid, fbBox));
            f.moves.Add(Fireball("fb_m", "Hadoken M", Motion.QCF, Btn.MP, 75, 12, 32, GuardType.Mid, fbBox));
            f.moves.Add(Fireball("fb_h", "Hadoken H", Motion.QCF, Btn.HP, 100, 12, 32, GuardType.Mid, fbBox));

            f.moves.Add(Uppercut("dp_l", "Shoryu L", Motion.DP, Btn.LP, 1000, 190, 25, 6, 12));
            f.moves.Add(Uppercut("dp_h", "Shoryu H", Motion.DP, Btn.HP | Btn.MP, 1400, 240, 35, 8, 18));

            var th = Hit(350, 18, 12, HitLevel.Special);
            var tatsu = Special("tatsu", "Tatsu", Motion.QCB, Btn.Kicks, 7, 18, 14, th);
            tatsu.gravity = 3;
            tatsu.velocity.Add(new VelocityKey(5, 70, 40, true));
            tatsu.hitboxes.Add(new FrameBox(7, 10, new BoxRect(350, 1100, 900, 400), 0));
            tatsu.hitboxes.Add(new FrameBox(13, 16, new BoxRect(350, 1100, 900, 400), 1));
            tatsu.hitboxes.Add(new FrameBox(19, 24, new BoxRect(350, 1100, 900, 400), 2));
            tatsu.finisherGroup = 2;
            tatsu.landingRecovery = 8;
            tatsu.aiTags = AITag.Approach;
            f.moves.Add(tatsu);

            f.moves.Add(Rush("super", "Shinku Rush", Motion.Super236236, Btn.Punches, 5, 520, 110));

            f.combos.Add(new ComboRoute("low into fireball", 0, "cr_mk", "fb_h"));
            f.combos.Add(new ComboRoute("jabs into fireball", 0, "cr_lp", "cr_lp", "fb_l"));
            f.combos.Add(new ComboRoute("strong into dp", 0, "st_mp", "dp_h"));
            f.combos.Add(new ComboRoute("fierce into tatsu", 0, "st_hp", "tatsu"));
            f.combos.Add(new ComboRoute("fierce into dp", 0, "st_hp", "dp_h"));
            f.combos.Add(new ComboRoute("low into super", 200, "cr_mk", "super"));
            f.combos.Add(new ComboRoute("fierce into super", 200, "st_hp", "super"));
            f.combos.Add(new ComboRoute("jump in", 0, "j_hk", "st_hp", "dp_h"));
            return f;
        }

        //this is Brick and he is the grappler who is slow and tanky but hits really hard and has a command grab
        static FighterDef Brick()
        {
            var f = new FighterDef
            {
                id = "brick", displayName = "Brick", archetype = Archetype.Grappler, colorRGB = 0x8D6E63, accentRGB = 0x1565C0,
                maxHealth = 11500, walkForward = 32, walkBack = 26, dashFrames = 20, dashSpeed = 75, backdashFrames = 24, backdashSpeed = 60,
                jumpVY = 240, jumpVX = 45, gravity = 14, pushWidth = 640,
                standHurt = new BoxRect(0, 950, 700, 1900), crouchHurt = new BoxRect(0, 650, 780, 1300), airHurt = new BoxRect(0, 850, 700, 1400)
            };
            AddStandardNormals(f, new Tune(1, 118, 110));

            var cgHit = Hit(2400, 0, 0, HitLevel.Heavy, GuardType.Unblockable);
            cgHit.launchVY = 190; cgHit.launchVX = 40; cgHit.meterGain = 30;
            var cg = new MoveDef
            {
                id = "cmd_grab", displayName = "Bear Hug", kind = MoveKind.CommandThrow, motion = Motion.HCB, dir = DirReq.Any, buttons = Btn.Punches,
                startup = 5, active = 2, recovery = 40, throwRange = 950, hit = cgHit, aiTags = AITag.CommandThrow
            };
            f.moves.Add(cg);

            var shHit = Hit(1100, 0, 18, HitLevel.Special);
            shHit.effect = HitEffect.Knockdown; shHit.launchVY = 150; shHit.launchVX = 70;
            var sh = Special("shoulder", "Shoulder Charge", Motion.QCB, Btn.Punches, 14, 8, 16, shHit);
            sh.velocity.Add(new VelocityKey(6, 110));
            sh.velocity.Add(new VelocityKey(22, 0));
            sh.hitboxes.Add(new FrameBox(14, 21, new BoxRect(500, 1100, 600, 1200)));
            sh.aiTags = AITag.Approach;
            f.moves.Add(sh);

            var hb = Uppercut("headbutt", "Rising Headbutt", Motion.DP, Btn.Punches, 1300, 170, 15, 7, 16);
            hb.startup = 5;
            hb.hitboxes[0] = new FrameBox(5, 11, new BoxRect(250, 1800, 600, 900));
            hb.velocity[0].frame = 5;
            f.moves.Add(hb);

            var suHit = Hit(3800, 0, 0, HitLevel.Super, GuardType.Unblockable);
            suHit.launchVY = 230; suHit.launchVX = 30;
            var su = new MoveDef
            {
                id = "super", displayName = "Earthquake Slam", kind = MoveKind.CommandThrow, isSuper = true, motion = Motion.Super214214, dir = DirReq.Any,
                buttons = Btn.Punches, startup = 2, active = 3, recovery = 50, throwRange = 1150, hit = suHit, meterCost = 200, superFreeze = 30,
                invulnStart = 1, invulnEnd = 4, aiTags = AITag.Super | AITag.CommandThrow
            };
            f.moves.Add(su);

            f.combos.Add(new ComboRoute("strong into shoulder", 0, "st_mp", "shoulder"));
            f.combos.Add(new ComboRoute("low into shoulder", 0, "cr_mk", "shoulder"));
            f.combos.Add(new ComboRoute("fierce into headbutt", 0, "st_hp", "headbutt"));
            f.combos.Add(new ComboRoute("jump in", 0, "j_hp", "st_hp", "shoulder"));
            return f;
        }

        //this is Vex and she is the rushdown with fast buttons a three part rekka and a dive kick
        static FighterDef Vex()
        {
            var f = new FighterDef
            {
                id = "vex", displayName = "Vex", archetype = Archetype.Rushdown, colorRGB = 0x7E57C2, accentRGB = 0x00E5FF,
                maxHealth = 9000, walkForward = 55, walkBack = 42, dashFrames = 14, dashSpeed = 125, backdashFrames = 20, backdashSpeed = 85,
                jumpVY = 260, jumpVX = 62, gravity = 16, pushWidth = 480,
                standHurt = new BoxRect(0, 870, 520, 1740), crouchHurt = new BoxRect(0, 560, 600, 1120), airHurt = new BoxRect(0, 780, 520, 1250)
            };
            var t = new Tune(-1, 90, 90);
            AddStandardNormals(f, t);

            var oh = Normal("oh_mp", "Heel Drop", DirReq.Forward, Btn.MP, 16, 2, 13, Hit(450, 16, 12, HitLevel.Medium, GuardType.High), new BoxRect(550, 1050, 500, 750), new Tune(0, 100, 100));
            oh.aiTags |= AITag.Overhead;
            f.moves.Add(oh);

            var slHit = Hit(700, 0, 14, HitLevel.Medium, GuardType.Low);
            slHit.effect = HitEffect.Knockdown; slHit.launchVY = 110;
            var slide = Normal("slide", "Slide", DirReq.DownForward, Btn.HK, 10, 6, 16, slHit, new BoxRect(650, 200, 700, 300), new Tune(0, 100, 100));
            slide.velocity.Add(new VelocityKey(1, 100));
            slide.velocity.Add(new VelocityKey(16, 0));
            slide.aiTags |= AITag.Low | AITag.Approach;
            f.moves.Add(slide);

            var r1 = Special("rekka1", "Rekka 1", Motion.QCF, Btn.Punches, 9, 3, 20, Hit(600, 18, 14, HitLevel.Special));
            r1.velocity.Add(new VelocityKey(1, 60)); r1.velocity.Add(new VelocityKey(11, 0));
            r1.hitboxes.Add(new FrameBox(9, 11, new BoxRect(550, 1200, 650, 400)));
            r1.cancelEnd = 28;
            r1.aiTags = AITag.Approach;
            f.moves.Add(r1);

            var r2 = Special("rekka2", "Rekka 2", Motion.QCF, Btn.Punches, 8, 3, 20, Hit(600, 18, 14, HitLevel.Special));
            r2.requiresPrev = "rekka1";
            r2.velocity.Add(new VelocityKey(1, 60)); r2.velocity.Add(new VelocityKey(10, 0));
            r2.hitboxes.Add(new FrameBox(8, 10, new BoxRect(550, 1150, 650, 400)));
            r2.cancelEnd = 28;
            f.moves.Add(r2);

            //the last rekka is an overhead so it is kind of a mixup with the low one
            var r3h = Hit(900, 0, 12, HitLevel.Special, GuardType.High);
            r3h.effect = HitEffect.Knockdown; r3h.launchVY = 200;
            var r3 = Special("rekka3", "Rekka 3", Motion.QCF, Btn.Punches, 14, 3, 22, r3h);
            r3.requiresPrev = "rekka2";
            r3.velocity.Add(new VelocityKey(1, 50, 90, true));
            r3.gravity = 10;
            r3.hitboxes.Add(new FrameBox(14, 16, new BoxRect(550, 700, 650, 700)));
            r3.landingRecovery = 10;
            f.moves.Add(r3);

            var r2lh = Hit(700, 0, 12, HitLevel.Special, GuardType.Low);
            r2lh.effect = HitEffect.Knockdown; r2lh.launchVY = 100;
            var r2l = Special("rekka_low", "Rekka Low", Motion.QCB, Btn.Kicks, 11, 3, 22, r2lh);
            r2l.requiresPrev = "rekka1";
            r2l.velocity.Add(new VelocityKey(1, 70)); r2l.velocity.Add(new VelocityKey(13, 0));
            r2l.hitboxes.Add(new FrameBox(11, 13, new BoxRect(650, 200, 750, 300)));
            f.moves.Add(r2l);

            var dkh = Hit(600, 16, 12, HitLevel.Medium, GuardType.High);
            var dive = Special("divekick", "Dive Kick", Motion.None, Btn.MK | Btn.HK, 5, 30, 4, dkh);
            dive.dir = DirReq.AirDown;
            dive.velocity.Add(new VelocityKey(5, 90, -150));
            dive.hitboxes.Add(new FrameBox(5, 34, new BoxRect(350, 150, 500, 400)));
            dive.useAirHurtbox = true;
            dive.landingRecovery = 6;
            dive.aiTags = AITag.AirAttack | AITag.Approach;
            f.moves.Add(dive);

            var flip = Uppercut("flipkick", "Flip Kick", Motion.DP, Btn.Kicks, 1200, 200, 20, 6, 16);
            flip.startup = 4;
            flip.hitboxes[0] = new FrameBox(4, 11, new BoxRect(400, 1200, 600, 1100));
            flip.velocity[0].frame = 4;
            f.moves.Add(flip);

            f.moves.Add(Rush("super", "Phantom Rave", Motion.Super236236, Btn.Kicks, 6, 430, 130));

            f.combos.Add(new ComboRoute("jab rekka", 0, "cr_lp", "cr_lp", "rekka1", "rekka2", "rekka3"));
            f.combos.Add(new ComboRoute("strong rekka", 0, "st_mp", "rekka1", "rekka2", "rekka3"));
            f.combos.Add(new ComboRoute("low rekka", 0, "cr_mk", "rekka1", "rekka_low"));
            f.combos.Add(new ComboRoute("fierce super", 200, "st_hp", "super"));
            f.combos.Add(new ComboRoute("low super", 200, "cr_mk", "super"));
            f.combos.Add(new ComboRoute("jump in", 0, "j_mk", "st_mp", "rekka1", "rekka2", "rekka3"));
            return f;
        }

        //this is Lumen and they are the zoner with long pokes and charge fireballs and want to keep you out
        static FighterDef Lumen()
        {
            var f = new FighterDef
            {
                id = "lumen", displayName = "Lumen", archetype = Archetype.Zoner, colorRGB = 0x4DD0E1, accentRGB = 0xFFD54F,
                maxHealth = 9500, walkForward = 38, walkBack = 36, dashFrames = 20, dashSpeed = 80,
                jumpVY = 240, jumpVX = 50, gravity = 12
            };
            AddStandardNormals(f, new Tune(1, 100, 122));

            var orbBox = new BoxRect(0, 1200, 450, 450);
            var orbL = Fireball("orb_l", "Sun Orb L", Motion.ChargeBackForward, Btn.LP | Btn.MP, 70, 11, 24, GuardType.Mid, orbBox);
            var orbH = Fireball("orb_h", "Sun Orb H", Motion.ChargeBackForward, Btn.HP, 105, 11, 24, GuardType.Mid, orbBox);
            f.moves.Add(orbL);
            f.moves.Add(orbH);

            var wave = Fireball("wave", "Ground Wave", Motion.QCB, Btn.Kicks, 55, 14, 26, GuardType.Low, new BoxRect(0, 250, 600, 300));
            wave.projectile.spawnX = 600;
            f.moves.Add(wave);

            var arc = Uppercut("arc", "Rising Arc", Motion.ChargeDownUp, Btn.Kicks, 1200, 220, 20, 7, 14);
            arc.hitboxes[0] = new FrameBox(3, 11, new BoxRect(450, 1400, 800, 1100));
            f.moves.Add(arc);

            var slashHit = Hit(900, 0, 14, HitLevel.Special);
            slashHit.effect = HitEffect.Knockdown; slashHit.launchVY = 160;
            var slash = Special("slash", "Mirror Slash", Motion.QCF, Btn.Kicks, 10, 4, 20, slashHit);
            slash.velocity.Add(new VelocityKey(2, 90)); slash.velocity.Add(new VelocityKey(14, 0));
            slash.hitboxes.Add(new FrameBox(10, 13, new BoxRect(600, 1100, 800, 700)));
            slash.aiTags = AITag.Approach;
            f.moves.Add(slash);

            var beamHit = Hit(500, 26, 10, HitLevel.Super);
            beamHit.chip = 120; beamHit.chipCanKO = true; beamHit.hitstop = 5;
            var beam = Special("super", "Solar Beam", Motion.Super236236, Btn.Punches, 10, 1, 40, beamHit);
            beam.isSuper = true; beam.meterCost = 200; beam.superFreeze = 35;
            beam.invulnStart = 1; beam.invulnEnd = 14;
            beam.spawnsProjectile = true; beam.projectileFrame = 10;
            beam.projectile = new ProjectileDef { speed = 170, lifetime = 90, box = new BoxRect(0, 1150, 1200, 600), hit = beamHit, durability = 6, hitInterval = 4, spawnX = 800, visualSize = 3 };
            beam.aiTags = AITag.Super | AITag.Projectile;
            f.moves.Add(beam);

            f.combos.Add(new ComboRoute("low into slash", 0, "cr_mk", "slash"));
            f.combos.Add(new ComboRoute("fierce into slash", 0, "st_hp", "slash"));
            f.combos.Add(new ComboRoute("low into beam", 200, "cr_mk", "super"));
            f.combos.Add(new ComboRoute("jump in", 0, "j_hp", "st_hp", "slash"));
            return f;
        }
    }
}

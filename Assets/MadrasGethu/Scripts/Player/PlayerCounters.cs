using UnityEngine;

namespace MadrasGethu
{
    /// <summary>
    /// Counters (Batman / Assassin's Creed style). A perfect parry (F as the blue F shows) slows time;
    /// your next button picks the counter:
    ///   nothing ........ Counter Punch
    ///   LMB ............ Kai Odaippu: catch the arm, elbow into the joint, short punch (he can't attack for a while)
    ///   E .............. Counter Throw: hip in, throw him over your back
    ///   Q (1 bar) ...... Counter Finisher: wrist twist to his knees, headbutt, knee to the face
    /// Situation counters:
    ///   parry a weapon swing ......... Weapon Steal (automatic): yank it out of his hands and hit him with it
    ///   E as a grab arrives (yellow) . Grab Reversal: knock his hands aside and grab him instead
    ///   break free when held ......... Escape Strike: elbow / headbutt / knee on the way out, he's left dazed
    ///   dodge a charge, then RMB ..... Matador: trip him as he runs past, face-first
    ///   two punches at once .......... Double Counter: catch both fists, duck, their heads meet
    /// </summary>
    public partial class PlayerFighter
    {
        Weapon stolenWeapon;
        float lastDodgeStart = -10f;
        bool clashDouble;
        Vector3 clashOtherStart, clashOtherPoint;

        /// <summary>Another rowdy whose punch lands at the same moment (for the double counter).</summary>
        EnemyFighter SecondAttacker(EnemyFighter first)
        {
            if (!MoveUnlocks.Has(MoveId.DoubleCounter)) return null;
            foreach (var f in All)
            {
                if (!(f is EnemyFighter e) || e == first || !e.IsTelegraphing || e.CurrentTelegraph != TelegraphColor.Red || e.IsFeinting) continue;
                if (e.TimeToImpact > 0.7f) continue;   // forgiving: two punches rarely land on the same frame
                if (Flat(e.transform.position - transform.position).magnitude < 3.6f) return e;
            }
            return null;
        }

        void ClearCounterInput()
        {
            lightBufferedUntil = heavyBufferedUntil = 0f;
        }

        /// <summary>While the perfect parry's slow motion lasts: pick the counter.</summary>
        void AddCounterPrompts()
        {
            if (MoveUnlocks.Has(MoveId.KaiOdaippu)) Prompts.Add(new PromptItem { key = "LMB", label = "Arm break" });
            if (MoveUnlocks.Has(MoveId.CounterThrow)) Prompts.Add(new PromptItem { key = "E", label = "Throw" });
            if (MoveUnlocks.Has(MoveId.CounterFinisher)) Prompts.Add(new PromptItem { key = "Q", label = "Counter finisher", dim = Gethu < GethuBar });
        }

        /// <summary>Called during the perfect parry. Returns true once a counter has started.</summary>
        bool UpdateCounterChoice(EnemyFighter target)
        {
            if (stolenWeapon != null)
            {
                if (stateTimer < 0.12f) return false;
                StartWeaponSteal(target);
                return true;
            }
            AddCounterPrompts();
            if (GameInput.LightPressed && MoveUnlocks.Has(MoveId.KaiOdaippu)) { StartKaiOdaippu(target); return true; }
            if (GameInput.GrabPressed && MoveUnlocks.Has(MoveId.CounterThrow)) { StartCounterThrow(target); return true; }
            if (GameInput.GethuPressed && MoveUnlocks.Has(MoveId.CounterFinisher))
            {
                if (Gethu >= GethuBar) { StartCounterFinisher(target); return true; }
                if (GM != null) GM.AddPopup("Need 1 Gethu bar", Center + Vector3.up, Color.gray, 0.6f);
            }
            if (stateTimer < 0.34f) return false;
            ClearCounterInput();
            StartAttack(MassMode ? counterUppercut : counter, target);
            return true;
        }

        void Announce(string name, Vector3 at)
        {
            if (GM == null) return;
            GM.AddPopup(name, at + Vector3.up * 1.1f, new Color(0.5f, 0.85f, 1f), 1.1f);
        }

        // ------------------------------------------------------------------ after a perfect parry

        void StartKaiOdaippu(EnemyFighter target)
        {
            ClearCounterInput();
            target.EnterGrabbed(false);
            grabbed = target;
            grabFromBehind = false;
            Announce("KAI ODAIPPU!", target.Center);
            StartStrikeCombo(KaiOdaippuCombo());
        }

        StrikeCombo KaiOdaippuCombo()
        {
            var c = new StrikeCombo
            {
                name = "KAI ODAIPPU", id = MoveId.KaiOdaippu, grip = GripKind.Wrist, gripLean = 5f,
                releaseAtEnd = true, injureArm = 8f, doubleOverAfter = 2.2f,
            };
            // Left hand holds his wrist; a short elbow (hook) into his locked elbow, then a straight punch.
            var elbow = c.Add(Beat(0.3f, FighterPose.Hook, 10f, "ELBOW!"));
            elbow.holdCollar = true; elbow.lead = 0.25f; elbow.victimZ = 0.7f; elbow.victimPitch = -8f; elbow.crack = true;
            var punch = c.Add(Beat(0.75f, FighterPose.Cross, 9f, "PUNCH"));
            punch.holdCollar = true; punch.victimZ = 0.78f; punch.victimPitch = -18f;
            return c;
        }

        void StartCounterThrow(EnemyFighter target)
        {
            ClearCounterInput();
            target.EnterGrabbed(false);
            StartWrestle(CounterThrowMove(), target);
        }

        // Hip throw: catch the arm, turn in so your back is against his chest, and throw him over your back.
        WrestleMove CounterThrowMove()
        {
            var catchArm = Pose(With(S(QteKey.None, null, 0.15f, FighterPose.Grab), new Vector3(0f, 0.95f, 0.6f), 0f, 0f), 0f, 5f);
            catchArm.shot = CamShot.Side;
            // Turn round under him (his chest ends up on your back).
            var turnIn = Pose(With(S(QteKey.None, null, 0.25f, FighterPose.Grab), new Vector3(0f, 1.2f, 0.2f), 40f, 0.05f), -0.3f, 30f);
            turnIn.hero = new Vector3(0f, 0f, 0.35f);
            turnIn.yaw = 180f;
            turnIn.shot = CamShot.Side;
            // Over the top: straight over your head, then let go.
            var over = Pose(With(S(QteKey.None, null, 0.16f, FighterPose.Throw), new Vector3(0f, 1.75f, 0.35f), 140f, 0f), -0.45f, 55f);
            over.yaw = 180f;
            over.anim = true;
            over.shot = CamShot.Side;
            return new WrestleMove
            {
                name = "COUNTER THROW", id = MoveId.CounterThrow, cost = 0f, grip = GripKind.Waist, cinematic = false,
                throwAtEnd = true, throwDamage = 16f, throwAlongFacing = true,
            }.Step(catchArm).Step(turnIn).Step(over);
        }

        void StartCounterFinisher(EnemyFighter target)
        {
            ClearCounterInput();
            Gethu = Mathf.Max(0f, Gethu - GethuBar);
            target.EnterGrabbed(false);
            StartWrestle(CounterFinisherMove(), target);
        }

        // Twist his wrist until he drops to his knees, headbutt (ring prompt), knee to the face.
        WrestleMove CounterFinisherMove()
        {
            var m = new WrestleMove
            {
                name = "COUNTER FINISHER", id = MoveId.CounterFinisher, cost = 0f, grip = GripKind.Wrist,
                releasePush = new Vector3(0f, 1.2f, 2.6f),   // knocked onto his back, away from you
            };
            var twist = Pose(Shot(With(S(QteKey.None, null, 0.4f, FighterPose.Grab), new Vector3(0f, 0.95f, 0.68f), 0f, 0f), CamShot.LowHero), -0.05f, 12f);
            twist.victimDrop = -0.62f;   // down on his knees
            twist.victimLean = 20f;
            var headbutt = Pose(Shot(S(QteKey.Light, "HEADBUTT", 0.18f, FighterPose.Grab), CamShot.Side), -0.15f, 30f);
            headbutt.heroStrike = GrabStrikeKind.Headbutt;
            headbutt.damage = 12f;
            var snap = Pose(S(QteKey.None, null, 0.18f, FighterPose.Grab), -0.1f, 15f);
            snap.victimLean = -15f;
            snap.shot = CamShot.Side;
            var knee = Pose(Shot(Impact(S(QteKey.None, null, 0.22f, FighterPose.Grab), 26f), CamShot.GroundImpact), -0.05f, 5f);
            knee.heroStrike = GrabStrikeKind.Knee;
            knee.victimLean = -30f;
            var after = Pose(S(QteKey.None, null, 0.35f, FighterPose.Grab), 0f, 0f);
            after.shot = CamShot.Side;
            return m.Step(twist).Step(headbutt).Step(snap).Step(knee).Step(after);
        }

        void StartWeaponSteal(EnemyFighter target)
        {
            ClearCounterInput();
            var w = stolenWeapon;
            stolenWeapon = null;
            HeldWeapon = w;
            w.Attach(this);
            GameAudio.Play(Sfx.Grab, w.transform.position, 1f);
            var steal = w.MakeAttack(false);
            steal.name = "Weapon Steal";
            steal.announce = true;
            steal.damage *= 1.4f;
            steal.knockback = 6f;
            steal.launch = 3f;
            steal.heavy = true;
            steal.unblockable = true;
            StartAttack(steal, target);
        }

        // ------------------------------------------------------------------ grab reversal

        /// <summary>E as a rowdy's grab arrives (yellow): knock his hands aside and grab him instead.</summary>
        bool TryGrabReversal()
        {
            if (!MoveUnlocks.Has(MoveId.GrabReversal) || HeldWeapon != null) return false;
            EnemyFighter best = null;
            float bestDist = 2.9f;
            foreach (var f in All)
            {
                if (!(f is EnemyFighter e) || !e.IsGrabTelegraph || e.TimeToImpact > 0.5f) continue;
                float d = Flat(e.transform.position - transform.position).magnitude;
                if (d < bestDist) { bestDist = d; best = e; }
            }
            if (best == null) return false;
            best.CancelForCounter();
            BeginGrab(best, false);
            grabActionCooldown = 0.25f;
            GameAudio.Play(Sfx.Parry, best.Center, 0.8f, 1.2f);
            NotifyMoveLanded("Grab Reversal");
            AddGethu(6f);
            Announce("REVERSED!", best.Center);
            if (GM != null) GM.SlowMo(0.35f, 0.3f);
            return true;
        }

        // ------------------------------------------------------------------ escape strike

        /// <summary>Breaking free: hit back on the way out. He's left dazed.</summary>
        void StartEscapeStrike(EnemyFighter holder, bool fromBehind)
        {
            ClearCounterInput();
            holder.EnterGrabbed(false);
            grabbed = holder;
            grabFromBehind = false;
            Announce("ESCAPE STRIKE!", holder.Center);
            StartStrikeCombo(EscapeCombo(fromBehind));
        }

        StrikeCombo EscapeCombo(bool fromBehind)
        {
            var c = new StrikeCombo { name = "ESCAPE STRIKE", id = MoveId.EscapeStrike, releaseAtEnd = true, dazeAfterRelease = 3f };
            float t = 0.2f;
            if (fromBehind)
            {
                // He was behind you: back elbow as you turn round to face him.
                var elbow = c.Add(Beat(0.35f, FighterPose.BackElbow, 8f, "ELBOW"));
                elbow.lead = 0.3f; elbow.turnIn = 180f; elbow.victimZ = 0.7f; elbow.victimPitch = -15f;
                t = 0.75f;
            }
            var head = c.Add(Beat(t, FighterPose.Headbutt, 8f, "HEADBUTT"));
            head.ik = GrabStrikeKind.Headbutt; head.lead = 0.17f; head.holdCollar = !fromBehind; head.victimZ = 0.62f; head.victimPitch = -20f;
            var knee = c.Add(Beat(t + 0.4f, FighterPose.Knee, 9f, "KNEE"));
            knee.ik = GrabStrikeKind.Knee; knee.lead = 0.17f; knee.holdCollar = !fromBehind; knee.victimZ = 0.62f; knee.victimPitch = 40f;
            return c;
        }

        // ------------------------------------------------------------------ matador (vs the charge)

        /// <summary>RMB just after dodging a charge: trip him as he runs past.</summary>
        bool TryMatador()
        {
            if (!MoveUnlocks.Has(MoveId.Matador) || Time.time - lastDodgeStart > 1.0f) return false;
            foreach (var f in All)
            {
                if (!(f is EnemyFighter e) || !e.IsCharging) continue;
                Vector3 to = Flat(transform.position - e.transform.position);
                if (to.magnitude > 3.0f) continue;
                Motor.FaceDirection(e.transform.position - transform.position, instant: true);
                Visual.Play(FighterPose.LowKick, 0.45f, 0.45f);
                GameAudio.Play(Sfx.Whoosh, Center, 0.6f, 0.9f);
                e.Tripped(e.ChargeDirection);
                NotifyMoveLanded("Matador");
                RegisterHits(1, 12f);
                Announce("MATADOR!", e.Center);
                if (GM != null) { GM.SlowMo(0.3f, 0.5f); GM.Shake(0.3f); GM.HitStop(0.08f); }
                if (cam != null) cam.FovKick(5f);
                ClearCounterInput();
                SetState(PlayerState.Stagger);
                stateTimer = 0.05f;
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ double counter

        /// <summary>Two punches at once: catch both fists, duck, and pull them into each other.</summary>
        void StartDoubleCounter(EnemyFighter a, EnemyFighter b)
        {
            ClearCounterInput();
            a.CancelForCounter();
            b.CancelForCounter();
            a.EnterGrabbed(false);
            b.EnterGrabbed(false);
            Vector3 center = transform.position + Flat(transform.forward).normalized * 0.85f;
            Vector3 across = Flat(b.transform.position - a.transform.position);
            across = across.sqrMagnitude > 0.01f ? across.normalized : transform.right;
            clashVictim = a;
            clashOther = b;
            clashDouble = true;
            clashDir = across;
            clashVictimStart = a.transform.position;
            clashPoint = center - across * 0.28f;
            clashOtherStart = b.transform.position;
            clashOtherPoint = center + across * 0.28f;
            clashHeroStart = clashHeroEnd = transform.position;
            clashed = false;
            Motor.SetDesiredVelocity(Vector3.zero, instant: true);
            Visual.Play(FighterPose.Parry, ClashTime + 0.3f, ClashTime + 0.2f);
            Announce("DOUBLE COUNTER!", Center);
            GameAudio.Play(Sfx.Parry, Center, 1f);
            if (GM != null) GM.SlowMo(0.25f, 0.5f);
            SetState(PlayerState.HeadClash);
        }
    }
}

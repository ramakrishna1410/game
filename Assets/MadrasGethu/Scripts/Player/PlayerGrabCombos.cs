using System.Collections.Generic;
using UnityEngine;

namespace MadrasGethu
{
    /// <summary>One blow in a grab strike combo. Distances are along the line from the hero to the rowdy.</summary>
    public class ComboBeat
    {
        public float at;                    // seconds from the start of the combo when the blow lands
        public float lead = 0.22f;          // how long before 'at' the swing starts
        public FighterPose pose;            // the hero's animation
        public GrabStrikeKind ik;           // knee / headbutt / ear clap done with IK instead (needs no animation file)
        public bool noAnim;                 // done with the hands' grip alone (arm twist)
        public bool holdCollar;             // left hand still holds his collar for this blow
        public float heroZ;                 // where the hero is when it lands
        public bool run;                    // cover the distance at a run (otherwise steps)
        public bool jump;                   // leap into it (flying knee, dropkick)
        public float spin;                  // degrees the hero turns through during the swing (spinning backfist)
        public float victimZ = 0.6f;        // where the rowdy ends up after it
        public float victimPitch;           // + doubled over, - rocked back
        public float damage = 6f;
        public string label;
        public Vector3 knockout;            // non-zero: the final blow (impulse: x right, y up, z away from the hero)
        public float gap = 0.12f;           // at least this long after the previous blow lands before this one starts
        public float maxSpeed = 1.6f;       // how much the animation may be sped up to hit on time (kicks: less)
        public bool started, landed, jumped;
    }

    public class StrikeCombo
    {
        public string name;
        public MoveId id;
        public float cost;
        public readonly List<ComboBeat> beats = new List<ComboBeat>();
        public bool cinematic;
        public GripKind grip = GripKind.Collar;   // where the hands are while he's held
        public float gripLean;
        public bool releaseAtEnd;           // let go when it's over (arm twist) instead of keeping hold
        public float injureArm;             // seconds he can't attack afterwards
        public float dazeOnRelease;         // ear clap: dizzy when you let go of him
        public ComboBeat Add(ComboBeat b) { beats.Add(b); return b; }
    }

    /// <summary>
    /// Grab strike combos (Sleeping Dogs / Yakuza style strings, no QTE). After grabbing with E, each click
    /// is read as part of a short code; the second click picks the combo, which then plays out:
    ///   LMB LMB (LMB) ...... Royapuram Rush: two hooks holding his collar, (third LMB) uppercut
    ///   keep tapping LMB ... Machine Gun Kuthu: rapid punches to the face, then the body, RMB finishes
    ///   RMB RMB ............ Kasimedu Kick Storm: shove, front kick, roundhouse
    ///   LMB RMB ............ Thalaivar Special: headbutt, knee, spinning backfist
    ///   RMB LMB ............ Jallikattu: push kick, run in, flying knee
    ///   LMB + RMB together . Gethu Mass Combo (full Gethu): jab, cross, body kick, hook, knee, dropkick
    /// A single click on its own is one quick blow (punch or knee) and you keep hold of him.
    /// The hero really steps, runs and jumps (his legs animate); each blow's animation is timed to land with it.
    /// </summary>
    public partial class PlayerFighter
    {
        [Header("Grab combos")]
        [Tooltip("Dragging speed while holding Shift.")]
        public float dragSprintSpeed = 3.4f;

        const float CodeWindow = 0.38f;      // time to give the second click
        const float TogetherWindow = 0.09f;  // LMB and RMB this close together = both at once

        char codeFirst;                      // 'L' / 'R' while waiting for the second click
        float codeTime;

        StrikeCombo combo;
        EnemyFighter comboVictim;
        float comboTime;
        Vector3 comboOrigin;
        Quaternion comboRotation;
        float comboVictimZ, comboVictimPitch, comboVictimZTarget, comboVictimPitchTarget;
        bool comboHolding;                   // still holding his collar
        bool comboKnockedOut;
        float comboEndAt;
        float comboRoom;

        // Royapuram Rush: waits for the third click, or turns into rapid punches if you keep tapping.
        bool rushWaiting;
        float rushWaitUntil;
        int rushTaps;
        float rushUppercutAt;                // a click after the hooks: uppercut, unless another click follows quickly
        bool rapid, rapidUsed;
        int rapidCount;
        float rapidLastPunch, rapidLastTap;
        const int RapidFacePunches = 6, RapidMaxPunches = 12;

        public bool InGrabCombo => State == PlayerState.Combo;

        /// <summary>Holding stance (upper body) between blows while he's still held by the collar.</summary>
        bool ComboHoldsStance => combo != null && comboHolding
            && !combo.beats.Exists(b => b.started && !b.landed && b.ik == GrabStrikeKind.None && !b.noAnim);
        public string GrabComboName => combo != null ? combo.name : null;
        public bool RapidPunching => rapid;

        // ------------------------------------------------------------------ reading the clicks

        /// <summary>Called every frame while holding someone. Returns true if a combo (or single blow) started.</summary>
        bool UpdateGrabComboInput()
        {
            bool l = GameInput.LightPressed, r = GameInput.HeavyPressed;
            lightBufferedUntil = heavyBufferedUntil = 0f;

            if (codeFirst == '\0')
            {
                if (l && r) return StartComboByCode('L', 'R', together: true);
                if (l || r)
                {
                    codeFirst = l ? 'L' : 'R';
                    codeTime = Time.time;
                    Visual.Flinch(0.15f);   // a little set: tightening the grip
                }
                return false;
            }

            // RMB held down (not clicked): twist his arm.
            if (codeFirst == 'R' && !l && !r && GameInput.HeavyHeld && Time.time - codeTime > 0.26f && MoveUnlocks.Has(MoveId.ArmTwist))
            {
                codeFirst = '\0';
                StartStrikeCombo(ArmTwistCombo());
                return true;
            }

            if (l || r)
            {
                char second = l ? 'L' : 'R';
                bool together = Time.time - codeTime <= TogetherWindow && second != codeFirst;
                char first = codeFirst;
                codeFirst = '\0';
                return StartComboByCode(first, second, together);
            }

            if (Time.time - codeTime > CodeWindow)
            {
                // Just one click: one quick blow, and you keep hold of him.
                char single = codeFirst;
                codeFirst = '\0';
                SingleGrabStrike(single == 'L');
                return true;
            }
            return false;
        }

        void SingleGrabStrike(bool punch)
        {
            if (grabbed == null) return;
            grabStrikePose = punch ? FighterPose.Cross : FighterPose.Knee;
            if (punch)
            {
                pendingGrabStrike = Visual.PlayTimed(FighterPose.Cross, 0.18f, 0.25f);
                grabStrikeTimer = pendingGrabStrike + 0.25f;
                Visual.FreeRightHand(grabStrikeTimer);
                grabActionCooldown = grabStrikeTimer + 0.02f;
            }
            else
            {
                const float strikeTime = 0.42f;
                pendingGrabStrike = Visual.GrabStrike(GrabStrikeKind.Knee, strikeTime);
                grabActionCooldown = strikeTime + 0.02f;
            }
        }

        bool StartComboByCode(char first, char second, bool together)
        {
            if (grabbed == null) return false;
            StrikeCombo c = null;
            if (together && Gethu >= maxGethu && MoveUnlocks.Has(MoveId.GethuMassCombo)) c = GethuMassCombo();
            else if (first == 'L' && second == 'L' && MoveUnlocks.Has(MoveId.RoyapuramRush)) c = RoyapuramRush();
            else if (first == 'R' && second == 'R' && MoveUnlocks.Has(MoveId.KasimeduKickStorm)) c = KasimeduKickStorm();
            else if (first == 'L' && second == 'R' && MoveUnlocks.Has(MoveId.ThalaivarSpecial)) c = ThalaivarSpecial();
            else if (first == 'R' && second == 'L' && MoveUnlocks.Has(MoveId.Jallikattu)) c = Jallikattu();
            if (c == null)
            {
                SingleGrabStrike(first == 'L');
                return true;
            }
            StartStrikeCombo(c);
            return true;
        }

        // ------------------------------------------------------------------ the combos

        static ComboBeat Beat(float at, FighterPose pose, float damage, string label)
        {
            return new ComboBeat { at = at, pose = pose, damage = damage, label = label };
        }

        // Two punches holding him by the collar; the third click lets go with an uppercut (added when you click).
        StrikeCombo RoyapuramRush()
        {
            var c = new StrikeCombo { name = "ROYAPURAM RUSH", id = MoveId.RoyapuramRush };
            var h1 = c.Add(Beat(0.22f, FighterPose.Cross, 7f, "HOOK"));
            h1.holdCollar = true; h1.victimZ = 0.62f; h1.victimPitch = -14f;
            var h2 = c.Add(Beat(0.58f, FighterPose.Cross, 8f, "HOOK"));
            h2.holdCollar = true; h2.victimZ = 0.64f; h2.victimPitch = -18f;
            return c;
        }

        ComboBeat RushUppercut(float at)
        {
            var b = Beat(at, FighterPose.Uppercut, 16f, "UPPERCUT!");
            b.lead = 0.26f; b.heroZ = 0.15f; b.victimZ = 0.72f; b.victimPitch = -25f;
            b.knockout = new Vector3(0f, 3.5f, 2.2f);
            return b;
        }

        StrikeCombo KasimeduKickStorm()
        {
            var c = new StrikeCombo { name = "KASIMEDU KICK STORM", id = MoveId.KasimeduKickStorm };
            var shove = c.Add(Beat(0.3f, FighterPose.Push, 4f, "SHOVE"));
            shove.lead = 0.25f; shove.victimZ = 1.35f; shove.victimPitch = -12f;
            var front = c.Add(Beat(1.0f, FighterPose.FrontKick, 10f, "FRONT KICK"));
            front.lead = 0.45f; front.gap = 0.25f; front.maxSpeed = 1.4f; front.heroZ = 0.45f; front.victimZ = 1.55f; front.victimPitch = 42f;
            var round = c.Add(Beat(1.9f, FighterPose.Roundhouse, 18f, "ROUNDHOUSE!"));
            round.lead = 0.5f; round.gap = 0.35f; round.maxSpeed = 1.4f; round.heroZ = 0.55f; round.victimZ = 1.6f; round.victimPitch = 20f;
            round.knockout = new Vector3(2.2f, 1.2f, 1.2f);   // spun round and dropped beside you, not thrown away
            return c;
        }

        StrikeCombo ThalaivarSpecial()
        {
            var c = new StrikeCombo { name = "THALAIVAR SPECIAL", id = MoveId.ThalaivarSpecial };
            var head = c.Add(Beat(0.22f, FighterPose.Headbutt, 8f, "HEADBUTT"));
            head.ik = GrabStrikeKind.Headbutt; head.lead = 0.17f; head.holdCollar = true; head.victimZ = 0.6f; head.victimPitch = -20f;
            var knee = c.Add(Beat(0.62f, FighterPose.Knee, 9f, "KNEE"));
            knee.ik = GrabStrikeKind.Knee; knee.lead = 0.17f; knee.holdCollar = true; knee.victimZ = 0.58f; knee.victimPitch = 42f;
            var back = c.Add(Beat(1.2f, FighterPose.Backfist, 16f, "SPINNING BACKFIST!"));
            back.lead = 0.36f; back.spin = 360f; back.heroZ = 0.05f; back.victimZ = 0.8f; back.victimPitch = 15f;
            back.knockout = new Vector3(-2.4f, 1.2f, 1.4f);
            return c;
        }

        StrikeCombo Jallikattu()
        {
            var c = new StrikeCombo { name = "JALLIKATTU", id = MoveId.Jallikattu };
            var push = c.Add(Beat(0.45f, FighterPose.PushKick, 6f, "PUSH KICK"));
            push.lead = 0.4f; push.maxSpeed = 1.4f; push.victimZ = 2.0f; push.victimPitch = -15f;
            // Two or three running steps after the push kick, then leave the ground for the knee.
            var knee = c.Add(Beat(1.6f, FighterPose.FlyingKnee, 18f, "FLYING KNEE!"));
            knee.lead = 0.5f; knee.gap = 0.4f; knee.maxSpeed = 1.4f; knee.run = true; knee.jump = true;
            knee.heroZ = 1.4f; knee.victimZ = 2.05f; knee.victimPitch = 5f;
            knee.knockout = new Vector3(0f, 1.6f, 3.2f);
            return c;
        }

        // Arm twist: both hands on his wrist, crank the arm so he bends over, knee into the elbow. He's let go
        // with a bad arm: drops his weapon and can't attack for a while.
        StrikeCombo ArmTwistCombo()
        {
            var c = new StrikeCombo { name = "ARM TWIST", id = MoveId.ArmTwist, grip = GripKind.Wrist, gripLean = 10f, releaseAtEnd = true, injureArm = 7f };
            var twist = c.Add(Beat(0.4f, FighterPose.Grab, 6f, "TWIST"));
            twist.noAnim = true; twist.holdCollar = true; twist.lead = 0.35f; twist.victimZ = 0.7f; twist.victimPitch = 55f;
            var snap = c.Add(Beat(0.95f, FighterPose.Knee, 14f, "SNAP!"));
            snap.ik = GrabStrikeKind.Knee; snap.holdCollar = true; snap.lead = 0.17f; snap.victimZ = 0.68f; snap.victimPitch = 62f;
            return c;
        }

        // Ear clap: open palms on both ears. You keep hold of him; when you let go he reels about, dizzy.
        StrikeCombo EarClapCombo()
        {
            var c = new StrikeCombo { name = "EAR CLAP", id = MoveId.EarClap, dazeOnRelease = 3.5f };
            var clap = c.Add(Beat(0.32f, FighterPose.Push, 9f, "EAR CLAP!"));
            clap.ik = GrabStrikeKind.EarClap; clap.holdCollar = true; clap.lead = 0.3f; clap.victimZ = 0.62f; clap.victimPitch = -8f;
            return c;
        }

        StrikeCombo GethuMassCombo()
        {
            var c = new StrikeCombo { name = "GETHU MASS COMBO", id = MoveId.GethuMassCombo, cost = maxGethu, cinematic = true };
            var jab = c.Add(Beat(0.25f, FighterPose.Jab, 6f, "JAB")); jab.victimZ = 0.75f; jab.victimPitch = -8f;
            var cross = c.Add(Beat(0.5f, FighterPose.Cross, 7f, "CROSS")); cross.victimZ = 0.8f; cross.victimPitch = -12f;
            var kick = c.Add(Beat(0.85f, FighterPose.FrontKick, 9f, "BODY KICK")); kick.lead = 0.4f; kick.gap = 0.15f; kick.maxSpeed = 1.4f; kick.heroZ = 0.1f; kick.victimZ = 0.95f; kick.victimPitch = 32f;
            var hook = c.Add(Beat(1.15f, FighterPose.Hook, 8f, "HOOK")); hook.heroZ = 0.25f; hook.victimZ = 1.0f; hook.victimPitch = -10f;
            var knee = c.Add(Beat(1.45f, FighterPose.Knee, 9f, "KNEE")); knee.ik = GrabStrikeKind.Knee; knee.lead = 0.17f; knee.heroZ = 0.45f; knee.victimZ = 0.95f; knee.victimPitch = 45f;
            var drop = c.Add(Beat(2.15f, FighterPose.FlyingKick, 22f, "DROPKICK!"));
            drop.lead = 0.45f; drop.gap = 0.2f; drop.maxSpeed = 1.4f; drop.jump = true; drop.heroZ = 0.5f; drop.victimZ = 1.1f; drop.victimPitch = 20f;
            drop.knockout = new Vector3(0f, 2f, 5f);
            return c;
        }

        // ------------------------------------------------------------------ playing a combo

        void StartStrikeCombo(StrikeCombo c)
        {
            var victim = grabbed;
            if (victim == null) return;
            grabbed = null;   // the combo drives him now (he stays in the grabbed state until knocked out)
            combo = c;
            comboVictim = victim;
            comboTime = 0f;
            comboOrigin = transform.position;
            Vector3 fwd = Flat(victim.transform.position - transform.position);
            if (fwd.sqrMagnitude < 0.01f) fwd = transform.forward;
            comboRotation = Quaternion.LookRotation(fwd.normalized);
            transform.rotation = comboRotation;
            comboVictimZ = comboVictimZTarget = Mathf.Max(0.5f, fwd.magnitude);
            comboVictimPitch = comboVictimPitchTarget = 0f;
            comboHolding = true;
            comboKnockedOut = false;
            comboEndAt = float.MaxValue;
            comboRoom = Room(comboRotation * Vector3.forward, 4f);
            rushWaiting = c.id == MoveId.RoyapuramRush;
            rushWaitUntil = float.MaxValue;
            rushTaps = 0;
            rushUppercutAt = float.MaxValue;
            rapid = rapidUsed = false;
            rapidCount = 0;
            pendingGrabStrike = -1f;
            if (c.cost > 0f) Gethu = Mathf.Max(0f, Gethu - c.cost);
            SetState(PlayerState.Combo);
            if (GM != null && c.cinematic)
            {
                GM.AddPopup(c.name, Center + Vector3.up * 1.3f, new Color(1f, 0.65f, 0.15f), 1.4f);
                GM.SlowMo(0.5f, 0.25f);
            }
        }

        Vector3 ComboPoint(float z) => comboOrigin + comboRotation * new Vector3(0f, 0f, z);

        void UpdateGrabCombo()
        {
            float dt = Time.deltaTime;
            comboTime += dt;
            var victim = comboVictim;
            if (combo == null || victim == null || (victim.IsDead && !comboKnockedOut))
            {
                EndStrikeCombo();
                return;
            }

            // Royapuram Rush: third click = uppercut, fast tapping = rapid punches, nothing = keep holding him.
            if (rushWaiting) UpdateRushInput();
            if (rapid) UpdateRapid();

            ComboBeat next = null;
            for (int i = 0; i < combo.beats.Count; i++)
            {
                var b = combo.beats[i];
                if (!b.started && comboTime >= b.at - b.lead && (i == 0 || combo.beats[i - 1].landed)) StartBeat(i);
                if (b.started && !b.landed && b.jump && !b.jumped && b.at - comboTime <= 0.32f)
                {
                    b.jumped = true;   // leave the ground so he comes down as the blow lands
                    Motor.AddImpulse(Vector3.up * 4.6f);
                }
                if (b.started && !b.landed && comboTime >= b.at) LandBeat(i);
                if (!b.landed && next == null) next = b;
            }

            MoveHeroForCombo(next, dt);
            if (cam != null) ComboCamera();

            // The rowdy reacts: rocked back, doubled over, stumbling away (until he's knocked out).
            if (!comboKnockedOut)
            {
                // He staggers back over a few steps (his legs walk it), and his body reacts at once.
                float kz = 1f - Mathf.Exp(-6f * dt);
                float k = 1f - Mathf.Exp(-12f * dt);
                comboVictimZ = Mathf.Lerp(comboVictimZ, Mathf.Min(comboVictimZTarget, comboRoom - 0.3f), kz);
                comboVictimPitch = Mathf.Lerp(comboVictimPitch, comboVictimPitchTarget, k);
                Vector3 heroFlat = Flat(transform.position - comboOrigin);
                float heroZ = Vector3.Dot(heroFlat, comboRotation * Vector3.forward);
                float z = Mathf.Max(comboVictimZ, heroZ + 0.45f);   // never inside the hero
                victim.HoldAt(ComboPoint(z), comboRotation * Quaternion.Euler(0f, 180f, 0f));
                victim.Visual.SetCarry(Quaternion.Euler(comboVictimPitch, 0f, 0f));
                victim.Visual.CarryWalks = true;
                if (comboHolding)
                {
                    if (combo.grip == GripKind.Collar) Visual.SetGrip(victim, false);
                    else Visual.SetGrapple(victim, combo.grip, 0f, combo.gripLean);
                }
            }

            bool allLanded = combo.beats.TrueForAll(b => b.landed);
            if (allLanded && !rushWaiting && !rapid && comboEndAt == float.MaxValue)
                comboEndAt = comboTime + (comboKnockedOut ? 0.55f : 0.25f);
            if (comboTime >= comboEndAt) EndStrikeCombo();
        }

        /// <summary>Side-on close-up for every grab combo, framing both of them.</summary>
        void ComboCamera()
        {
            Vector3 fwd = comboRotation * Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, fwd);
            if (Vector3.Dot(cam.transform.position - transform.position, side) < 0f) side = -side;
            Vector3 mid = Vector3.Lerp(transform.position, comboVictim.transform.position, 0.5f) + Vector3.up * 1.0f;
            float span = Vector3.Distance(transform.position, comboVictim.transform.position) * 0.5f + 1.2f;
            cam.SetCinematicFraming(mid + side * 3.2f + Vector3.up * 0.5f, mid, 48f, 0f, mid, span);
        }

        void MoveHeroForCombo(ComboBeat next, float dt)
        {
            Vector3 fwd = comboRotation * Vector3.forward;
            float heroZ = Vector3.Dot(Flat(transform.position - comboOrigin), fwd);
            Vector3 v = Vector3.zero;
            // Steps (or runs) into place before a blow starts, then plants his feet for it, so the kick
            // animation isn't sliding along the ground. A leaping blow keeps travelling through the air.
            if (next != null && (!next.started || next.jump))
            {
                float target = Mathf.Min(next.heroZ, comboRoom - 0.6f);
                if (next.jump && !next.started) target -= 0.6f;
                float arrive = next.started ? next.at : next.at - next.lead;
                float timeLeft = Mathf.Max(0.12f, arrive - comboTime);
                float speed = (target - heroZ) / timeLeft;
                speed = Mathf.Clamp(speed, -2f, next.run ? 6f : 2.6f);
                if (Mathf.Abs(target - heroZ) < 0.03f) speed = 0f;
                v = fwd * speed;
            }
            Motor.SetDesiredVelocity(v, instant: true);

            // Facing: down the combo line, except while spinning into a backfist.
            float spin = 0f;
            foreach (var b in combo.beats)
                if (b.spin != 0f && b.started && !b.landed)
                    spin = b.spin * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((comboTime - (b.at - b.lead)) / b.lead));
            transform.rotation = comboRotation * Quaternion.Euler(0f, -spin, 0f);
        }

        /// <summary>Moves beat i and everything after it later by delta seconds.</summary>
        void DelayBeats(int i, float delta)
        {
            if (delta <= 0f) return;
            for (int j = i; j < combo.beats.Count; j++)
                if (!combo.beats[j].landed) combo.beats[j].at += delta;
        }

        void StartBeat(int index)
        {
            var b = combo.beats[index];
            b.started = true;
            float lead = Mathf.Max(0.08f, b.at - comboTime);
            if (!b.holdCollar && comboHolding)
            {
                comboHolding = false;
                Visual.SetGrip(null, false);   // let go of his collar
            }
            if (b.noAnim) { }
            else if (b.ik != GrabStrikeKind.None) Visual.GrabStrike(b.ik, lead / 0.4f);
            else
            {
                // Played near its natural speed: the blow lands when the animation's contact frame does,
                // and the rest of the combo waits for it.
                float actual = Visual.PlayTimed(b.pose, lead, 0.45f, b.maxSpeed, 0.4f);
                DelayBeats(index, comboTime + actual - b.at);
                if (b.holdCollar) Visual.FreeRightHand(actual + 0.3f);
            }
        }

        void LandBeat(int index)
        {
            var b = combo.beats[index];
            b.landed = true;
            // The next blow starts only after this one has had its moment (follow-through, his reaction).
            if (index + 1 < combo.beats.Count)
            {
                var n = combo.beats[index + 1];
                DelayBeats(index + 1, comboTime + n.gap - (n.at - n.lead));
            }
            var victim = comboVictim;
            Vector3 fwd = comboRotation * Vector3.forward;
            Vector3 right = comboRotation * Vector3.right;
            float mult = MassMode ? massModeDamageMultiplier : 1f;
            Vector3 contact = victim.Center + Vector3.up * 0.25f;

            if (b.knockout != Vector3.zero)
            {
                comboKnockedOut = true;
                Visual.SetGrip(null, false);
                Vector3 impulse = right * b.knockout.x + Vector3.up * b.knockout.y + fwd * b.knockout.z;
                victim.Visual.ClearCarry();
                victim.ReleaseFromGrab(impulse, knockDown: true);
                victim.ReceiveHit(new HitInfo { attacker = this, damage = b.damage * mult, direction = fwd, unblockable = true, hitsDowned = true, heavy = true });
                NotifyMoveLanded(rapidUsed ? "Machine Gun Kuthu" : combo.name);
                if (GM != null)
                {
                    GM.HitStop(0.12f);
                    GM.SlowMo(0.3f, 0.4f);
                    GM.Shake(0.45f, 0.3f);
                    GM.AddPopup((rapidUsed ? "MACHINE GUN KUTHU" : combo.name) + "!", contact + Vector3.up * 1.0f, new Color(1f, 0.7f, 0.15f), 1.4f);
                }
                if (cam != null) cam.ZoomPunch(7f);
            }
            else
            {
                victim.ReceiveHit(new HitInfo { attacker = this, damage = Mathf.Min(b.damage * mult, Mathf.Max(0f, victim.Health - 1f)), direction = fwd, unblockable = true, hitsDowned = true });
                victim.Visual.Flinch(1f);
                comboVictimZTarget = b.victimZ;
                comboVictimPitchTarget = b.victimPitch;
                // A quick extra jolt on the hit itself, then he settles into the new position.
                comboVictimPitch += b.victimPitch > 10f ? 12f : -10f;
                if (GM != null)
                {
                    GM.HitStop(rapid ? 0.035f : 0.06f);
                    GM.Shake(rapid ? 0.08f : 0.16f);
                }
            }
            FX.HitSpark(contact, b.knockout != Vector3.zero);
            RegisterHits(1, rapid ? 2f : 5f);
            if (!string.IsNullOrEmpty(b.label) && GM != null && b.knockout == Vector3.zero)
                GM.AddPopup(b.label, contact + Vector3.up * 0.6f, new Color(1f, 0.85f, 0.4f), 0.7f);
        }

        // ------------------------------------------------------------------ Royapuram Rush / rapid punches

        void UpdateRushInput()
        {
            bool l = GameInput.LightPressed;
            lightBufferedUntil = heavyBufferedUntil = 0f;
            if (l) rushTaps++;
            bool hooksDone = combo.beats.TrueForAll(b => b.landed);
            if (hooksDone && rushWaitUntil == float.MaxValue) rushWaitUntil = comboTime + 0.45f;

            // Still tapping after the two clicks that started the rush: straight into the rapid punches.
            if (rushTaps >= 2 && MoveUnlocks.Has(MoveId.MachineGunKuthu))
            {
                rushWaiting = false;
                rapid = rapidUsed = true;
                rapidCount = 0;
                rapidLastPunch = comboTime - 1f;
                rapidLastTap = comboTime;
                if (GM != null) GM.AddPopup("MACHINE GUN KUTHU!", Center + Vector3.up * 1.3f, new Color(1f, 0.7f, 0.2f), 1.2f);
                return;
            }
            if (!hooksDone) return;
            if (l && rushUppercutAt == float.MaxValue)
            {
                // A third click: the uppercut, unless it turns out to be the start of fast tapping.
                rushUppercutAt = comboTime + 0.2f;
                return;
            }
            if (comboTime >= rushUppercutAt)
            {
                rushWaiting = false;
                combo.Add(RushUppercut(comboTime + 0.28f));
                return;
            }
            if (rushUppercutAt == float.MaxValue && comboTime > rushWaitUntil)
            {
                // No third click: keep holding him.
                rushWaiting = false;
            }
        }

        /// <summary>Machine Gun Kuthu: every tap is another punch (as fast as you tap, up to a limit).</summary>
        void UpdateRapid()
        {
            bool l = GameInput.LightPressed, r = GameInput.HeavyPressed;
            lightBufferedUntil = heavyBufferedUntil = 0f;
            Prompts.Add(new PromptItem { key = "LMB", label = "Tap fast!" });
            Prompts.Add(new PromptItem { key = "RMB", label = "Finish" });
            if (l) rapidLastTap = comboTime;

            bool lastPending = combo.beats.Exists(x => !x.landed);
            bool face = rapidCount < RapidFacePunches;
            bool finish = r || rapidCount >= RapidMaxPunches || comboTime - rapidLastTap > 0.6f;
            if (finish && !lastPending)
            {
                rapid = false;
                var fin = face ? RapidHaymaker(comboTime + 0.3f) : RushUppercut(comboTime + 0.28f);
                combo.Add(fin);
                return;
            }
            if (!l || lastPending || comboTime - rapidLastPunch < 0.12f) return;

            // Next punch: straight to the face holding the collar, then let go and dig into the body.
            rapidLastPunch = comboTime;
            rapidCount++;
            var b = new ComboBeat
            {
                at = comboTime + 0.1f, lead = 0.1f, damage = 3.5f, gap = 0f, maxSpeed = 2.6f,
                label = rapidCount == RapidFacePunches ? "BODY!" : null,
            };
            if (face)
            {
                b.pose = FighterPose.Cross;
                b.holdCollar = true;
                b.victimZ = 0.62f;
                b.victimPitch = -10f - rapidCount * 2.5f;
            }
            else
            {
                int n = rapidCount - RapidFacePunches;
                b.pose = n % 2 == 0 ? FighterPose.BodyBlow : FighterPose.Hook;
                b.heroZ = 0.05f * n;
                b.victimZ = 0.75f + 0.05f * n;
                b.victimPitch = 15f + n * 5f;
            }
            combo.Add(b);
        }

        ComboBeat RapidHaymaker(float at)
        {
            var b = Beat(at, FighterPose.Haymaker, 18f, "HAYMAKER!");
            b.lead = 0.3f; b.heroZ = 0.1f; b.victimZ = 0.75f;
            b.knockout = new Vector3(0f, 1.5f, 4f);
            return b;
        }

        void EndStrikeCombo()
        {
            var victim = comboVictim;
            var c = combo;
            combo = null;
            comboVictim = null;
            rapid = false;
            rushWaiting = false;
            Motor.SetDesiredVelocity(Vector3.zero, instant: true);

            // Holds that end on their own (arm twist, ear clap) count as landed when they finish.
            if (c != null && !comboKnockedOut && (c.releaseAtEnd || c.dazeOnRelease > 0f) && c.beats.TrueForAll(b => b.landed))
                NotifyMoveLanded(c.name);

            if (victim != null && !victim.IsDead && victim.IsGrabbed && !comboKnockedOut)
            {
                if (c != null && c.releaseAtEnd)
                {
                    victim.Visual.ClearCarry();
                    victim.ReleaseFromGrab(Flat(victim.transform.position - transform.position).normalized * 2f, knockDown: false);
                    if (c.injureArm > 0f)
                    {
                        victim.InjureArm(c.injureArm);
                        if (GM != null) GM.AddPopup("ARM INJURED", victim.Center + Vector3.up, new Color(1f, 0.6f, 0.3f), 1f);
                    }
                }
                else if (comboHolding)
                {
                    if (c != null && c.dazeOnRelease > 0f)
                    {
                        victim.DazeWhenReleased(c.dazeOnRelease);
                        if (GM != null) GM.AddPopup("DIZZY", victim.Center + Vector3.up * 1.1f, new Color(1f, 0.95f, 0.4f), 0.9f);
                    }
                    // Combo ended without a knockout (Royapuram Rush without the uppercut): keep hold of him.
                    victim.Visual.ClearCarry();
                    grabbed = victim;
                    grabPullStart = victim.transform.position;
                    grabFromBehind = false;
                    Visual.SetGrip(victim, false);
                    SetState(PlayerState.Grab);
                    stateTimer = 1.5f;
                    grabActionCooldown = 0.1f;
                    if (cam != null) cam.EndCinematic(0.25f);
                    return;
                }
                victim.Visual.ClearCarry();
                victim.ReleaseFromGrab(Vector3.zero, knockDown: false);
            }
            Visual.SetGrip(null, false);
            SetState(PlayerState.Free);
            if (c != null && cam != null) cam.EndCinematic(0.25f);
        }
    }
}

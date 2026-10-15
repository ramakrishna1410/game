using System.Collections.Generic;
using UnityEngine;

namespace MadrasGethu
{
    /// <summary>
    /// The hero: a former North Chennai boxer. Handles movement, the boxing combo system,
    /// block and counter, grabs and throws, environmental finishers and the Gethu meter.
    /// </summary>
    public partial class PlayerFighter : Fighter
    {
        public enum PlayerState { Free, Attack, Dodge, Block, Grab, Stagger, Down, Finisher, Dead, Jump, Parry, Held, Wrestle, Combo }

        /// <summary>One button prompt for the HUD: a key cap (or mouse button) and what it does.</summary>
        public struct PromptItem
        {
            public string key;     // "LMB", "RMB", "E", "F", "Q", "C", "SPACE"
            public string label;
            public bool dim;       // shown greyed out (e.g. not enough Gethu yet)
        }

        [Header("Movement")]
        public float walkSpeed = 1.6f;
        public float runSpeed = 4.2f;
        public float sprintSpeed = 6.2f;
        public float blockMoveSpeed = 1.1f;
        public float dragSpeed = 1.6f;
        [Tooltip("In a fight (an enemy within this range) the hero faces him and steps around in his guard.")]
        public float combatLockRange = 4.5f;
        public float combatStepSpeed = 1.8f;

        [Header("Jump")]
        public float jumpSpeed = 6.2f;
        public float airSpeed = 3.5f;

        [Header("Dodge")]
        [Tooltip("Sidestep / backstep distance while squared up to a rowdy.")]
        public float stepDistance = 1.9f;
        public float stepDuration = 0.4f;
        [Tooltip("Roll distance when not locked onto anyone.")]
        public float rollDistance = 3.2f;
        public float rollDuration = 0.6f;
        public float dodgeInvulnerableUntil = 0.3f;

        [Header("Gethu (heat) meter")]
        public float maxGethu = 100f;
        public float massModeDuration = 8f;
        public float massModeDamageMultiplier = 1.6f;

        [Header("Combos")]
        public List<AttackData> lightChain = new List<AttackData>();
        public List<AttackData> heavyEnders = new List<AttackData>();
        public AttackData stomp;
        public AttackData counter;
        public AttackData counterUppercut;
        public AttackData counterThrow;
        public AttackData knee;
        public AttackData grabMiss;
        [Header("Special moves")]
        public AttackData heavyFollowUp;   // heavy ender -> heavy again
        public AttackData flyingKick;      // jump -> attack
        public AttackData runningAttack;   // sprint -> light
        public AttackData dodgeStrike;     // dodge -> attack
        public AttackData shoulderBarge;   // L H -> H
        public AttackData bargeHaymaker;   // L H -> H -> H
        public AttackData airHammer;       // L L H -> H (uppercut launched him)
        public AttackData chargedHaymaker; // hold RMB
        public AttackData legSweep;        // back + RMB
        public AttackData shoulderRush;    // forward + RMB from a distance
        public AttackData clothesline;     // sprint + RMB
        public AttackData backElbow;       // LMB with a rowdy right behind you

        [Header("Combo grades")]
        [Tooltip("Hits needed for Nalla / Semma / Mass / Thalaivaa.")]
        public int[] gradeHits = { 5, 10, 20, 35 };
        static readonly string[] GradeNames = { "NALLA!", "SEMMA!", "MASS!", "THALAIVAA!" };

        [Header("Practice")]
        [Tooltip("Gethu meter stays full (practice yard).")]
        public bool infiniteGethu;
        [Tooltip("Takes no damage (practice yard).")]
        public bool invincible;

        /// <summary>Raised when a named move connects (combo specials, finishers): the practice checklist listens.</summary>
        public event System.Action<string> MoveLanded;

        public void NotifyMoveLanded(string moveName)
        {
            if (MoveLanded != null && !string.IsNullOrEmpty(moveName)) MoveLanded(moveName);
        }

        public PlayerState State { get; private set; } = PlayerState.Free;
        public float Gethu { get; private set; }
        public float GethuFraction => Gethu / maxGethu;
        public bool MassMode => massModeTimer > 0f;
        public float MassModeFraction => massModeTimer / massModeDuration;
        public int ComboHits { get; private set; }
        public float ComboTimer { get; private set; }
        /// <summary>Button prompts to show right now (key caps at the bottom of the screen).</summary>
        public readonly List<PromptItem> Prompts = new List<PromptItem>();
        /// <summary>0 = none, 1 = Nalla, 2 = Semma, 3 = Mass, 4 = Thalaivaa.</summary>
        public int ComboGrade
        {
            get
            {
                int g = 0;
                for (int i = 0; i < gradeHits.Length; i++) if (ComboHits >= gradeHits[i]) g = i + 1;
                return g;
            }
        }
        public string ComboGradeName => ComboGrade > 0 ? GradeNames[Mathf.Min(ComboGrade, GradeNames.Length) - 1] : null;
        /// <summary>Gethu earned is multiplied by this while the combo is going (x1.25 per grade).</summary>
        public float GethuMultiplier => 1f + 0.25f * ComboGrade;
        /// <summary>One of the three Gethu meter segments: the price of a wrestling finisher.</summary>
        public float GethuBar => maxGethu / 3f;
        public bool ChargingHaymaker => charging;
        public float HaymakerCharge => Mathf.Clamp01(chargeTime / ChargeFull);
        public EnemyFighter Grabbed => grabbed;

        ThirdPersonCamera cam;
        float stateTimer;
        AttackData currentAttack;
        float strikeAt;           // when the current attack's hit lands (matches the animation's contact frame)
        Fighter attackTarget;
        bool struck;
        Vector3 lungeDirection;
        float lungeRemaining;
        float lungeSpeed;
        int lightIndex;           // how many light attacks into the current string
        float lightBufferedUntil;
        float heavyBufferedUntil;
        float dodgeCooldown;
        Vector3 dodgeDirection;
        float dodgeDistanceNow;
        float dodgeDurationNow;
        float lastDodgeEnd = -10f;
        bool charging;
        float chargeTime;
        const float ChargeMin = 0.4f;
        const float ChargeFull = 1.0f;
        float massModeTimer;
        float grabActionCooldown;

        EnemyFighter grabbed;

        // Held by an enemy (grab, bear hug, hold from behind)
        EnemyFighter heldBy;
        public float BreakFreeProgress { get; private set; }
        public bool IsHeld => State == PlayerState.Held;
        public bool IsHeldBy(EnemyFighter e) => State == PlayerState.Held && heldBy == e;
        public bool HeldFromBehind { get; private set; }

        // Weapon in hand
        public Weapon HeldWeapon { get; private set; }
        bool grabFromBehind;
        Vector3 grabPullStart;
        const float GrabPullTime = 0.18f;

        // Parry
        [Header("Parry")]
        [Tooltip("Press block this close (seconds) to the enemy's hit landing for a PERFECT parry.")]
        public float perfectParryWindow = 0.25f;
        EnemyFighter parryTarget;
        bool parryPerfect;
        bool parryCounterStarted;

        // Wall slam / finisher target point
        Vector3 finisherEndPoint;
        float finisherDamage;
        string finisherTitle;
        bool finisherIsWallSlam;

        // Finisher sequence
        FinisherSpot finisherSpot;
        EnemyFighter finisherVictim;
        bool finisherImpactDone;
        Vector3 finisherVictimStart;

        GameManager GM => GameManager.Instance;

        protected override void Awake()
        {
            base.Awake();
            team = Team.Player;
            displayName = "Maaran";
            if (lightChain.Count == 0) BuildDefaultMoves();
        }

        public void SetCamera(ThirdPersonCamera camera) => cam = camera;

        /// <summary>North Chennai street boxing: jab-cross-hook-body, with a heavy ender after each step.</summary>
        void BuildDefaultMoves()
        {
            lightChain.Add(new AttackData("Jab", FighterPose.Jab, 7f)
                { startup = 0.12f, recovery = 0.30f, range = 1.5f, lunge = 0.9f, knockback = 1.5f, gethuGain = 4f });
            lightChain.Add(new AttackData("Cross", FighterPose.Cross, 8f)
                { startup = 0.13f, recovery = 0.32f, range = 1.55f, lunge = 0.9f, knockback = 1.8f, gethuGain = 4f });
            lightChain.Add(new AttackData("Hook", FighterPose.Hook, 9f)
                { startup = 0.15f, recovery = 0.36f, range = 1.6f, lunge = 0.9f, knockback = 2.2f, arc = 140f, gethuGain = 5f });
            lightChain.Add(new AttackData("Body Blow", FighterPose.BodyBlow, 11f)
                { startup = 0.16f, recovery = 0.42f, range = 1.6f, lunge = 1.0f, knockback = 3.5f, gethuGain = 6f, hitStop = 0.07f, shake = 0.15f });

            // Heavy ender depends on how many lights came before it (like Yakuza's rush + finisher strings).
            heavyEnders.Add(new AttackData("Haymaker", FighterPose.Haymaker, 16f)
                { announce = true, startup = 0.28f, recovery = 0.55f, range = 1.7f, lunge = 1.2f, knockback = 7f, heavy = true, gethuGain = 8f, hitStop = 0.09f, shake = 0.25f });
            heavyEnders.Add(new AttackData("Kasimedu Kick", FighterPose.Kick, 15f)
                { announce = true, startup = 0.26f, recovery = 0.55f, range = 1.95f, lunge = 1.0f, knockback = 9f, heavy = true, gethuGain = 8f, hitStop = 0.09f, shake = 0.25f });
            heavyEnders.Add(new AttackData("Uppercut", FighterPose.Uppercut, 18f)
                { announce = true, startup = 0.24f, recovery = 0.55f, range = 1.6f, lunge = 1.0f, knockback = 2f, launch = 7f, heavy = true, gethuGain = 10f, hitStop = 0.1f, shake = 0.3f });
            heavyEnders.Add(new AttackData("Spinning Backfist", FighterPose.Backfist, 18f)
                { announce = true, startup = 0.26f, recovery = 0.55f, range = 1.9f, arc = 360f, lunge = 0.5f, knockback = 8f, heavy = true, gethuGain = 10f, hitStop = 0.1f, shake = 0.3f });
            heavyEnders.Add(new AttackData("Royapuram Rush Finisher", FighterPose.Haymaker, 26f)
                { announce = true, startup = 0.28f, recovery = 0.65f, range = 1.8f, lunge = 1.2f, knockback = 10f, launch = 4f, heavy = true, gethuGain = 14f, hitStop = 0.13f, shake = 0.4f });

            stomp = new AttackData("Stomp", FighterPose.Stomp, 14f)
                { startup = 0.2f, recovery = 0.4f, range = 1.4f, arc = 140f, lunge = 1.0f, knockback = 1f, heavy = true, hitsDowned = true, gethuGain = 6f, hitStop = 0.08f, shake = 0.2f };
            counter = new AttackData("Counter Punch", FighterPose.Counter, 22f)
                { announce = true, startup = 0.12f, recovery = 0.35f, range = 2.2f, arc = 120f, lunge = 2.5f, knockback = 8f, launch = 3f, heavy = true, gethuGain = 15f, hitStop = 0.12f, shake = 0.3f };
            counterUppercut = new AttackData("Counter Uppercut", FighterPose.Uppercut, 24f)
                { announce = true, startup = 0.14f, recovery = 0.45f, range = 2.2f, arc = 120f, lunge = 2.5f, knockback = 3f, launch = 8f, heavy = true, gethuGain = 15f, hitStop = 0.14f, shake = 0.35f };
            counterThrow = new AttackData("Counter Throw", FighterPose.Throw, 20f)
                { announce = true, startup = 0.16f, recovery = 0.5f, range = 2.2f, arc = 120f, lunge = 2.5f, knockback = 13f, launch = 4f, heavy = true, gethuGain = 15f, hitStop = 0.12f, shake = 0.35f };

            heavyFollowUp = new AttackData("Double Hammer", FighterPose.Slam, 20f)
                { announce = true, startup = 0.3f, recovery = 0.6f, range = 1.8f, arc = 120f, lunge = 1.0f, knockback = 4f, launch = 6f, heavy = true, hitsDowned = true, gethuGain = 12f, hitStop = 0.12f, shake = 0.4f };
            flyingKick = new AttackData("Flying Kick", FighterPose.FlyingKick, 20f)
                { announce = true, startup = 0.18f, recovery = 0.5f, range = 2.0f, arc = 100f, lunge = 2.5f, knockback = 11f, launch = 3f, heavy = true, gethuGain = 12f, hitStop = 0.12f, shake = 0.35f };
            runningAttack = new AttackData("Running Knee", FighterPose.FlyingKick, 16f)
                { announce = true, startup = 0.16f, recovery = 0.45f, range = 1.9f, arc = 100f, lunge = 2.2f, knockback = 9f, launch = 2f, heavy = true, gethuGain = 10f, hitStop = 0.1f, shake = 0.3f };
            dodgeStrike = new AttackData("Pivot Backfist", FighterPose.Backfist, 15f)
                { announce = true, startup = 0.14f, recovery = 0.4f, range = 1.9f, arc = 360f, lunge = 0.8f, knockback = 7f, heavy = true, gethuGain = 10f, hitStop = 0.1f, shake = 0.25f };

            shoulderBarge = new AttackData("Shoulder Barge", FighterPose.ShoulderBarge, 12f)
                { announce = true, startup = 0.2f, recovery = 0.45f, range = 1.7f, arc = 110f, lunge = 1.6f, knockback = 4f, heavy = true, gethuGain = 9f, hitStop = 0.08f, shake = 0.25f };
            bargeHaymaker = new AttackData("Kasimedu Haymaker", FighterPose.Haymaker, 24f)
                { announce = true, startup = 0.26f, recovery = 0.6f, range = 1.8f, lunge = 1.3f, knockback = 11f, launch = 3f, heavy = true, gethuGain = 14f, hitStop = 0.13f, shake = 0.4f };
            airHammer = new AttackData("Air Hammer", FighterPose.Slam, 20f)
                { announce = true, startup = 0.22f, recovery = 0.55f, range = 2.0f, arc = 140f, lunge = 1.2f, knockback = 3f, launch = 0.5f, heavy = true, hitsDowned = true, gethuGain = 14f, hitStop = 0.14f, shake = 0.45f };
            chargedHaymaker = new AttackData("Charged Haymaker", FighterPose.Haymaker, 30f)
                { announce = true, startup = 0.12f, recovery = 0.6f, range = 1.9f, lunge = 1.6f, knockback = 5f, heavy = true, unblockable = true, dazes = true, gethuGain = 15f, hitStop = 0.16f, shake = 0.5f };
            legSweep = new AttackData("Leg Sweep", FighterPose.LegSweep, 10f)
                { announce = true, startup = 0.2f, recovery = 0.5f, range = 2.0f, arc = 360f, lunge = 0.3f, knockback = 2f, launch = 3f, heavy = true, gethuGain = 10f, hitStop = 0.08f, shake = 0.25f };
            shoulderRush = new AttackData("Shoulder Rush", FighterPose.ShoulderBarge, 14f)
                { announce = true, startup = 0.32f, recovery = 0.5f, range = 1.7f, arc = 100f, lunge = 4.5f, knockback = 9f, launch = 2f, heavy = true, gethuGain = 10f, hitStop = 0.1f, shake = 0.35f };
            clothesline = new AttackData("Clothesline", FighterPose.Clothesline, 15f)
                { announce = true, startup = 0.18f, recovery = 0.5f, range = 1.9f, arc = 120f, lunge = 2.4f, knockback = 3f, launch = 5f, heavy = true, gethuGain = 11f, hitStop = 0.12f, shake = 0.35f };
            backElbow = new AttackData("Back Elbow", FighterPose.BackElbow, 9f)
                { announce = true, startup = 0.12f, recovery = 0.35f, range = 1.6f, arc = 120f, lunge = 0.4f, knockback = 4f, gethuGain = 7f, hitStop = 0.08f, shake = 0.18f };

            knee = new AttackData("Knee", FighterPose.Knee, 7f)
                { startup = 0f, recovery = 0.3f, gethuGain = 5f, hitStop = 0.05f, shake = 0.12f };
            grabMiss = new AttackData("Grab Miss", FighterPose.Grab, 0f)
                { startup = 0.15f, recovery = 0.25f, lunge = 0.8f, gethuGain = 0f };   // damage 0 = no strike
        }

        // ------------------------------------------------------------------ main loop

        void Update()
        {
            float dt = Time.deltaTime;
            stateTimer += dt;
            dodgeCooldown -= dt;
            grabActionCooldown -= dt;
            Prompts.Clear();
            grabStrikeTimer -= dt;
            // Holding stance: while gripping (not mid-strike) and during wrestling lifts.
            Visual.SetAnimatorBool("Holding", (State == PlayerState.Grab && grabStrikeTimer <= 0f)
                                              || (State == PlayerState.Wrestle && WrestleHoldsStance)
                                              || (State == PlayerState.Combo && ComboHoldsStance));
            Visual.SetAnimatorBool("Held", State == PlayerState.Held);

            if (ComboTimer > 0f)
            {
                ComboTimer -= dt;
                if (ComboTimer <= 0f) ComboHits = 0;
            }

            if (massModeTimer > 0f)
            {
                massModeTimer -= dt;
                Gethu = maxGethu * Mathf.Clamp01(massModeTimer / massModeDuration);
                if (massModeTimer <= 0f && GM != null)
                    GM.AddPopup("Gethu Mode over", Center + Vector3.up, Color.gray, 0.7f);
            }

            if (IsDead) return;
            if (infiniteGethu && !MassMode) Gethu = maxGethu;

            if (GameInput.LightPressed) lightBufferedUntil = Time.time + 0.45f;
            if (GameInput.HeavyPressed) heavyBufferedUntil = Time.time + 0.45f;

            bool enemiesNear = CombatUtil.FindTarget(this, Vector3.zero, 7f, true) != null;
            Visual.SetCombatStance(enemiesNear || State == PlayerState.Block);
            if (cam != null) cam.InCombat = enemiesNear;

            switch (State)
            {
                case PlayerState.Free: UpdateFree(); break;
                case PlayerState.Attack: UpdateAttack(); break;
                case PlayerState.Dodge: UpdateDodge(); break;
                case PlayerState.Jump: UpdateJump(); break;
                case PlayerState.Block: UpdateBlock(); break;
                case PlayerState.Grab: UpdateGrab(); break;
                case PlayerState.Parry: UpdateParry(); break;
                case PlayerState.Held: UpdateHeld(); break;
                case PlayerState.Finisher: UpdateFinisher(); break;
                case PlayerState.Wrestle: UpdateWrestle(); break;
                case PlayerState.Combo: UpdateGrabCombo(); break;
                case PlayerState.Stagger:
                    if (stateTimer >= 0.4f) SetState(PlayerState.Free);
                    break;
                case PlayerState.Down:
                    if (stateTimer >= 1.1f && Motor.IsGrounded)
                    {
                        Visual.SetLying(false);
                        Visual.Play(FighterPose.GetUp, 0.4f);
                        SetState(PlayerState.Stagger);
                        stateTimer = 0f;
                    }
                    break;
            }
        }

        void SetState(PlayerState s)
        {
            if (s != PlayerState.Grab) codeFirst = '\0';   // a half-entered combo code doesn't carry over
            State = s;
            stateTimer = 0f;
            charging = false;
        }

        Vector3 MoveInputWorld()
        {
            Vector2 input = GameInput.Move;
            Quaternion basis = cam != null ? cam.FlatRotation : Quaternion.identity;
            return basis * new Vector3(input.x, 0f, input.y);
        }

        // ------------------------------------------------------------------ free movement

        void UpdateFree()
        {
            Vector3 move = MoveInputWorld();
            // Close to a rowdy: square up to him and step around in the guard (sprint breaks away).
            var focus = GameInput.Sprint ? null : CombatUtil.FindTarget(this, Vector3.zero, combatLockRange);
            if (focus != null)
            {
                Motor.SetDesiredVelocity(move * combatStepSpeed);
                Motor.FaceDirection(focus.transform.position - transform.position);
            }
            else
            {
                float speed = GameInput.Sprint ? sprintSpeed : runSpeed;
                if (move.magnitude < 0.5f) speed = walkSpeed;
                Motor.SetDesiredVelocity(move * speed);
                if (move.sqrMagnitude > 0.01f) Motor.FaceDirection(move);
            }

            if (stateTimer > 0.6f) lightIndex = 0;   // pause too long and the combo string restarts
            AddFreePrompts();
            if (TryCommonActions(allowAttacks: true)) return;
            if (GameInput.BlockHeld) SetState(PlayerState.Block);
        }

        /// <summary>Actions that can start from Free (and cancel attack recovery). Returns true if one started.</summary>
        bool TryCommonActions(bool allowAttacks)
        {
            if (GameInput.BlockPressed && TryCounter()) return true;

            if (GameInput.DodgePressed && dodgeCooldown <= 0f)
            {
                StartDodge();
                return true;
            }

            if (GameInput.JumpPressed && Motor.IsGrounded && State != PlayerState.Attack)
            {
                StartJump();
                return true;
            }

            if (GameInput.GethuPressed && (TryStartFreeFinisher() || TryActivateGethu())) return true;

            if (GameInput.GrabPressed)
            {
                TryGrab();
                return true;
            }

            if (!allowAttacks) return false;

            bool heavy = Time.time < heavyBufferedUntil;
            bool light = Time.time < lightBufferedUntil;
            if (!heavy && !light) return false;
            heavyBufferedUntil = 0f;
            lightBufferedUntil = 0f;

            // Weapon in hand: swing (light) or overhead smash (heavy) instead of boxing combos.
            if (HeldWeapon != null)
            {
                StartAttack(HeldWeapon.MakeAttack(heavy), null);
                lightIndex = 0;
                return true;
            }

            // Dodge -> attack: spin out of the dodge with a backfist.
            if (Time.time - lastDodgeEnd < 0.4f)
            {
                StartAttack(dodgeStrike, null);
                lightIndex = 0;
                return true;
            }

            bool running = GameInput.Sprint && Motor.MoveVelocity.magnitude > runSpeed * 0.8f;

            // Sprint -> light: running knee into whoever is ahead.
            if (light && running)
            {
                StartAttack(runningAttack, null);
                lightIndex = 0;
                return true;
            }

            if (heavy)
            {
                var downed = CombatUtil.FindTarget(this, MoveInputWorld(), 2.2f, includeDowned: true);
                AttackData next = State == PlayerState.Attack ? FollowUpFor(currentAttack) : null;
                Fighter forced = null;
                if (downed != null && downed.IsDowned && next != airHammer)
                {
                    next = stomp;
                    forced = downed;
                }
                else if (next == null)
                {
                    next = PickOpeningHeavy(running, out forced);
                }
                StartAttack(next, forced);
                lightIndex = 0;
                return true;
            }

            // A rowdy right behind you and nobody in front: elbow him without turning first.
            if (lightIndex == 0 && MoveUnlocks.Has(MoveId.BackElbow)
                && CombatUtil.FindTarget(this, transform.forward, 2.4f) == null)
            {
                var behind = CombatUtil.FindTarget(this, -transform.forward, 1.8f);
                if (behind != null)
                {
                    StartAttack(backElbow, behind);
                    return true;
                }
            }

            if (lightIndex >= lightChain.Count) lightIndex = 0;
            StartAttack(lightChain[lightIndex], null);
            lightIndex++;
            return true;
        }

        // ------------------------------------------------------------------ attacks

        void StartAttack(AttackData attack, Fighter forcedTarget)
        {
            SetState(PlayerState.Attack);
            currentAttack = attack;
            struck = false;

            attackTarget = forcedTarget != null
                ? forcedTarget
                : CombatUtil.FindTarget(this, MoveInputWorld().sqrMagnitude > 0.01f ? MoveInputWorld() : transform.forward, 3.2f,
                    attack.hitsDowned);

            // Step in during the wind-up so the hit lands when we arrive.
            lungeRemaining = 0.1f;   // punching the air: just a small weight shift
            if (attackTarget != null)
            {
                Vector3 to = Flat(attackTarget.transform.position - transform.position);
                Motor.FaceDirection(to, instant: true);
                float gap = to.magnitude - attackTarget.radius - radius;
                lungeRemaining = Mathf.Clamp(gap - attack.range * 0.45f, 0f, attack.lunge);
            }
            lungeDirection = transform.forward;
            // The animation is timed so the fist/foot connects exactly when the hit is applied (strikeAt).
            strikeAt = Visual.PlayTimed(attack.pose, attack.startup, attack.recovery);
            lungeSpeed = Mathf.Min(16f, lungeRemaining / Mathf.Max(strikeAt, 0.05f));
            if (attack.announce && GM != null)
                GM.AddPopup(attack.name.ToUpper(), Center + Vector3.up * 1.1f, new Color(1f, 0.85f, 0.35f), 0.75f);
        }

        /// <summary>Heavy pressed during a heavy: which move continues the string (null = none).</summary>
        AttackData FollowUpFor(AttackData previous)
        {
            if (previous == null) return null;
            if (heavyEnders.Count > 1 && previous == heavyEnders[1] && MoveUnlocks.Has(MoveId.ShoulderBarge)) return shoulderBarge;
            if (previous == shoulderBarge) return bargeHaymaker;
            if (heavyEnders.Count > 2 && previous == heavyEnders[2] && MoveUnlocks.Has(MoveId.AirHammer)) return airHammer;
            if (heavyEnders.Contains(previous)) return heavyFollowUp;
            return null;
        }

        /// <summary>A heavy that starts a string: depends on lights before it, the stick direction and running.</summary>
        AttackData PickOpeningHeavy(bool running, out Fighter forced)
        {
            forced = null;
            if (running && MoveUnlocks.Has(MoveId.Clothesline)) return clothesline;

            if (lightIndex == 0)
            {
                Vector3 input = MoveInputWorld();
                var focus = CombatUtil.FindTarget(this, Vector3.zero, 5.5f);
                if (focus != null && input.sqrMagnitude > 0.25f)
                {
                    Vector3 to = Flat(focus.transform.position - transform.position);
                    float dot = Vector3.Dot(input.normalized, to.normalized);
                    // Pulling away from him: sweep his legs (hits everyone around you).
                    if (dot < -0.5f && to.magnitude < 2.6f && MoveUnlocks.Has(MoveId.LegSweep))
                    {
                        forced = focus;
                        return legSweep;
                    }
                    // Pushing at him from out of reach: shoulder rush across the gap.
                    if (dot > 0.6f && to.magnitude > 2.6f && MoveUnlocks.Has(MoveId.ShoulderRush))
                    {
                        forced = focus;
                        return shoulderRush;
                    }
                }
            }
            return heavyEnders[Mathf.Min(lightIndex, heavyEnders.Count - 1)];
        }

        void UpdateAttack()
        {
            // A counter is always available, even in the middle of your own punch.
            if (currentAttack != counter && currentAttack != counterUppercut && currentAttack != counterThrow
                && GameInput.BlockPressed && TryCounter())
                return;

            // Lone haymaker with RMB still held: wind it up (release to throw it, guard-breaking and dazing).
            if (!struck && heavyEnders.Count > 0 && currentAttack == heavyEnders[0] && MoveUnlocks.Has(MoveId.ChargedHaymaker))
            {
                if (UpdateHaymakerCharge()) return;
            }

            if (!struck && lungeRemaining > 0f)
            {
                float step = Mathf.Min(lungeRemaining, lungeSpeed * Time.deltaTime);
                lungeRemaining -= step;
                if (Time.deltaTime > 0f) Motor.SetDesiredVelocity(lungeDirection * (step / Time.deltaTime), instant: true);
            }

            if (!struck && stateTimer >= strikeAt)
            {
                struck = true;
                if (attackTarget != null && !attackTarget.IsDead)
                    Motor.FaceDirection(attackTarget.transform.position - transform.position, instant: true);
                if (currentAttack.damage > 0f) PerformStrike();
            }

            float total = strikeAt + currentAttack.recovery;
            // Dodge / block / grab can cut a move short; the next punch only comes in the combo window,
            // so every punch plays out and button-mashing gives a steady jab-cross-hook-body rhythm.
            bool canDefend = struck && stateTimer >= strikeAt + 0.06f;
            bool canChain = struck && stateTimer >= strikeAt + currentAttack.recovery * 0.55f;
            if (canDefend && TryCommonActions(allowAttacks: canChain)) return;

            if (stateTimer >= total)
            {
                if (currentAttack.heavy) lightIndex = 0;
                SetState(PlayerState.Free);
            }
        }

        /// <summary>Returns true while the haymaker is being charged (the attack timeline is paused).</summary>
        bool UpdateHaymakerCharge()
        {
            if (!charging)
            {
                if (stateTimer < strikeAt * 0.5f || !GameInput.HeavyHeld) return false;
                charging = true;
                chargeTime = 0f;
                Visual.Play(FighterPose.Windup, ChargeFull + 0.3f);
            }

            chargeTime += Time.deltaTime;
            stateTimer -= Time.deltaTime;   // hold the wind-up
            Motor.SetDesiredVelocity(Vector3.zero);
            if (attackTarget != null && !attackTarget.IsDead)
                Motor.FaceDirection(attackTarget.transform.position - transform.position);
            Prompts.Add(new PromptItem { key = "RMB", label = chargeTime >= ChargeMin ? "Release!" : "Hold..." });
            if (chargeTime >= ChargeMin && Mathf.Repeat(chargeTime, 0.25f) < Time.deltaTime)
                FX.Burst(Center + Vector3.up * 0.4f + transform.right * 0.3f, 4, 2f, 0.05f, new Color(1f, 0.8f, 0.3f), new Color(2f, 1.5f, 0.3f), 0.25f, 0f);

            if (GameInput.HeavyHeld && chargeTime < ChargeFull) return true;

            charging = false;
            if (chargeTime >= ChargeMin)
            {
                if (cam != null) cam.FovKick(5f);
                StartAttack(chargedHaymaker, attackTarget);
            }
            else
            {
                strikeAt = stateTimer + Visual.PlayTimed(currentAttack.pose, Mathf.Max(0.08f, strikeAt - stateTimer), currentAttack.recovery);
            }
            return true;
        }

        void PerformStrike()
        {
            float mult = MassMode ? massModeDamageMultiplier : 1f;
            var result = CombatUtil.Strike(this, currentAttack, mult);
            if (result.Connected && HeldWeapon != null &&
                (currentAttack.pose == FighterPose.WeaponSwing || currentAttack.pose == FighterPose.WeaponSmash))
            {
                if (HeldWeapon.RegisterHit()) HeldWeapon = null;
            }
            if (result.hits > 0)
            {
                NotifyMoveLanded(currentAttack.name);
                RegisterHits(result.hits, currentAttack.gethuGain * result.hits);
                if (GM != null)
                {
                    GM.HitStop(currentAttack.hitStop);
                    GM.Shake(currentAttack.shake);
                }
                if (currentAttack.heavy && cam != null) cam.FovKick(4f);
            }
            else if (result.blocked > 0)
            {
                // Guard broke our rhythm: short recoil, chain resets. Use a heavy or a grab to open them up.
                lightIndex = 0;
                Motor.AddLunge(-transform.forward, 0.4f);
                if (GM != null) GM.HitStop(0.04f);
                stateTimer -= 0.15f;   // longer recovery after hitting a guard
            }
        }

        void RegisterHits(int hits, float gethu)
        {
            int gradeBefore = ComboGrade;
            ComboHits += hits;
            ComboTimer = 2f;
            AddGethu(gethu * GethuMultiplier);
            if (ComboGrade > gradeBefore && GM != null)
            {
                GM.AddPopup(ComboGradeName, Center + Vector3.up * 1.4f, new Color(1f, 0.75f, 0.15f), 1.3f + 0.15f * ComboGrade);
                if (ComboGrade >= 3) GM.SlowMo(0.5f, 0.25f);
            }
        }

        public void AddGethu(float amount)
        {
            if (MassMode) return;
            Gethu = Mathf.Clamp(Gethu + amount, 0f, maxGethu);
        }

        public void OnEnemyDefeated(EnemyFighter enemy)
        {
            AddGethu(enemy.Profile != null ? enemy.Profile.gethuReward : 8f);
            if (GM != null && enemy.IsBoss)
                GM.SlowMo(0.2f, 1.2f);
        }

        // ------------------------------------------------------------------ dodge

        /// <summary>
        /// Squared up to a rowdy: a quick sidestep / backstep that keeps facing him.
        /// Otherwise: a roll in the stick direction. Speed eases out, so it reads as a step, not a skate.
        /// </summary>
        void StartDodge()
        {
            SetState(PlayerState.Dodge);
            lightIndex = 0;
            Motor.ClearImpulse();

            Vector3 input = MoveInputWorld();
            var focus = CombatUtil.FindTarget(this, Vector3.zero, combatLockRange);
            FighterPose pose;

            if (focus != null && input.sqrMagnitude > 0.01f && Vector3.Dot(input.normalized, Flat(focus.transform.position - transform.position).normalized) > 0.5f)
                focus = null;   // pushing towards him: roll past instead of stepping

            if (focus != null)
            {
                Motor.FaceDirection(focus.transform.position - transform.position, instant: true);
                Vector3 local = input.sqrMagnitude > 0.01f ? transform.InverseTransformDirection(input.normalized) : Vector3.back;
                if (Mathf.Abs(local.x) > Mathf.Abs(local.z))
                    pose = local.x > 0f ? FighterPose.DodgeRight : FighterPose.DodgeLeft;
                else
                    pose = FighterPose.DodgeBack;
                dodgeDirection = input.sqrMagnitude > 0.01f ? input.normalized : -transform.forward;
                dodgeDistanceNow = stepDistance;
                dodgeDurationNow = stepDuration;
            }
            else
            {
                if (input.sqrMagnitude > 0.01f)
                {
                    dodgeDirection = input.normalized;
                    Motor.FaceDirection(dodgeDirection, instant: true);
                    pose = FighterPose.Roll;
                    dodgeDistanceNow = rollDistance;
                    dodgeDurationNow = rollDuration;
                }
                else
                {
                    dodgeDirection = -transform.forward;
                    pose = FighterPose.DodgeBack;
                    dodgeDistanceNow = stepDistance;
                    dodgeDurationNow = stepDuration;
                }
            }

            dodgeCooldown = dodgeDurationNow + 0.12f;
            Visual.Play(pose, dodgeDurationNow, dodgeDurationNow);
        }

        void UpdateDodge()
        {
            float t = stateTimer / dodgeDurationNow;
            if (t >= 1f)
            {
                lastDodgeEnd = Time.time;
                SetState(PlayerState.Free);
                return;
            }
            // Ease-out: fast push off, then settle (covers exactly dodgeDistanceNow).
            float speed = Mathf.PI * 0.5f * dodgeDistanceNow / dodgeDurationNow * Mathf.Cos(t * Mathf.PI * 0.5f);
            Motor.SetDesiredVelocity(dodgeDirection * speed, instant: true);
            if (GameInput.BlockPressed && TryCounter()) return;
        }

        // ------------------------------------------------------------------ jump

        void StartJump()
        {
            SetState(PlayerState.Jump);
            Motor.AddImpulse(Vector3.up * jumpSpeed);
            Visual.Play(FighterPose.Jump, 0.9f);
            lightIndex = 0;
        }

        void UpdateJump()
        {
            Vector3 move = MoveInputWorld();
            Motor.SetDesiredVelocity(move * airSpeed);
            if (move.sqrMagnitude > 0.01f) Motor.FaceDirection(move);

            // Jump -> attack: flying kick at whoever is ahead.
            if (Time.time < lightBufferedUntil || Time.time < heavyBufferedUntil)
            {
                lightBufferedUntil = heavyBufferedUntil = 0f;
                StartAttack(flyingKick, null);
                return;
            }

            if (stateTimer > 0.2f && Motor.IsGrounded)
            {
                FX.Dust(transform.position, 6);
                SetState(PlayerState.Free);
            }
        }

        /// <summary>Knocked to the ground: rowdies wait for the hero to get up instead of kicking him.</summary>
        public override bool IsDowned => State == PlayerState.Down;

        bool Invulnerable =>
            (State == PlayerState.Dodge && stateTimer < dodgeInvulnerableUntil) ||
            State == PlayerState.Finisher ||
            State == PlayerState.Wrestle ||
            State == PlayerState.Combo ||
            State == PlayerState.Parry ||
            (State == PlayerState.Attack && !struck &&
             (currentAttack == counter || currentAttack == counterUppercut || currentAttack == counterThrow));

        // ------------------------------------------------------------------ block & parry

        void UpdateBlock()
        {
            Vector3 move = MoveInputWorld();
            Motor.SetDesiredVelocity(move * blockMoveSpeed);
            var target = CombatUtil.FindTarget(this, transform.forward, 6f);
            if (target != null) Motor.FaceDirection(target.transform.position - transform.position);
            Visual.Play(FighterPose.Block, 0.1f);

            if (TryCommonActions(allowAttacks: true)) return;
            if (!GameInput.BlockHeld) SetState(PlayerState.Free);
        }

        /// <summary>
        /// Parry: tap block while a rowdy flashes red.
        /// PERFECT (pressed just before his hit lands): his arm is knocked aside, he stumbles well back and
        /// the hero follows with an automatic counter. EARLY: deflect only, he staggers and you get an opening.
        /// </summary>
        bool TryCounter()
        {
            EnemyFighter best = null;
            float bestDist = 3.6f;
            foreach (var f in All)
            {
                if (f is EnemyFighter e && e.IsTelegraphing && e.CurrentTelegraph == TelegraphColor.Red)
                {
                    float d = Flat(e.transform.position - transform.position).magnitude;
                    if (d < bestDist) { bestDist = d; best = e; }
                }
            }
            if (best == null) return false;

            if (best.IsFeinting)
            {
                // Bit on the fake: he punishes you.
                best.PunishParry(this);
                return true;
            }

            parryTarget = best;
            parryPerfect = best.TimeToImpact <= perfectParryWindow;
            parryCounterStarted = false;

            Vector3 toEnemy = Flat(best.transform.position - transform.position).normalized;
            Motor.FaceDirection(toEnemy, instant: true);
            best.Parried(parryPerfect, toEnemy);
            SetState(PlayerState.Parry);
            Visual.Play(FighterPose.Parry, 0.35f, 0.35f);
            Motor.AddLunge(-toEnemy, 0.2f);   // brace

            Vector3 contact = Vector3.Lerp(Center, best.Center, 0.5f) + Vector3.up * 0.3f;
            FX.Burst(contact, parryPerfect ? 22 : 10, parryPerfect ? 7f : 4f, 0.06f,
                new Color(0.7f, 0.9f, 1f), new Color(2.5f, 3f, 4f), 0.3f, 0f);
            AddGethu(parryPerfect ? 10f : 4f);

            if (GM != null)
            {
                if (parryPerfect)
                {
                    GM.SlowMo(0.2f, 0.55f);
                    GM.HitStop(0.06f);
                    GM.AddPopup("PERFECT PARRY!", best.Center + Vector3.up * 0.9f, new Color(0.4f, 0.9f, 1f), 1.2f);
                }
                else
                {
                    GM.SlowMo(0.55f, 0.2f);
                    GM.AddPopup("PARRY — OPENING!", best.Center + Vector3.up * 0.9f, new Color(0.7f, 0.85f, 1f), 0.9f);
                }
                GM.Shake(parryPerfect ? 0.2f : 0.1f);
            }
            if (cam != null) cam.FovKick(parryPerfect ? 6f : 3f);
            return true;
        }

        void UpdateParry()
        {
            // Another rowdy swinging at the same time? Parry him too (Batman-style chains).
            if (stateTimer > 0.1f && GameInput.BlockPressed && TryCounter()) return;

            if (parryPerfect && !parryCounterStarted && stateTimer >= 0.2f)
            {
                parryCounterStarted = true;
                if (parryTarget != null && !parryTarget.IsDead)
                {
                    float roll = Random.value;
                    var move = MassMode || roll < 0.34f ? counterUppercut : roll < 0.67f ? counter : counterThrow;
                    StartAttack(move, parryTarget);
                    return;
                }
            }
            if (stateTimer >= (parryPerfect ? 0.3f : 0.32f))
                SetState(PlayerState.Free);
        }

        // ------------------------------------------------------------------ grabs

        void TryGrab()
        {
            // A rowdy begging for mercy nearby: E spares him.
            foreach (var f in All)
            {
                if (f is EnemyFighter beggar && beggar.IsBegging && Flat(beggar.transform.position - transform.position).magnitude < 3.2f)
                {
                    beggar.LetGo();
                    AddGethu(15f);
                    return;
                }
            }

            // Armed: E throws the weapon at the nearest rowdy in front.
            if (HeldWeapon != null)
            {
                ThrowWeapon();
                return;
            }

            // Weapon at your feet: pick it up.
            var weapon = Weapon.FindNearbyOnGround(transform.position, 1.5f);
            if (weapon != null)
            {
                Visual.Play(FighterPose.PickUp, 0.4f, 0.5f);
                HeldWeapon = weapon;
                weapon.Attach(this);
                if (GM != null) GM.AddPopup(weapon.displayName.ToUpper(), Center + Vector3.up, new Color(1f, 0.85f, 0.4f), 0.7f);
                SetState(PlayerState.Stagger);   // short pick-up pause
                stateTimer = 0.15f;
                return;
            }

            var target = CombatUtil.FindTarget(this, MoveInputWorld().sqrMagnitude > 0.01f ? MoveInputWorld() : transform.forward, 2.0f)
                as EnemyFighter;
            if (target != null && target.CanBeGrabbed)
            {
                Vector3 toEnemy = Flat(target.transform.position - transform.position).normalized;
                // He's facing away from us: grab him from behind.
                grabFromBehind = Vector3.Dot(target.transform.forward, toEnemy) > 0.35f;
                grabbed = target;
                grabbed.EnterGrabbed(grabFromBehind);
                grabPullStart = target.transform.position;
                Motor.FaceDirection(toEnemy, instant: true);
                SetState(PlayerState.Grab);
                grabActionCooldown = GrabPullTime + 0.05f;
                Visual.Play(FighterPose.Grab, 999f);
                Visual.SetGrip(target, grabFromBehind);
                lightIndex = 0;
                if (grabFromBehind && GM != null)
                    GM.AddPopup("FROM BEHIND!", target.Center + Vector3.up, new Color(1f, 0.85f, 0.4f), 0.7f);
                return;
            }

            if (target != null && GM != null)
                GM.AddPopup("TOO HEAVY! Stagger him first", target.Center + Vector3.up, new Color(1f, 0.6f, 0.3f), 0.7f);

            // Whiffed grab
            StartAttack(grabMiss, null);
        }

        Vector3 HoldPosition => transform.position + transform.forward * (grabFromBehind ? 0.5f : 0.6f);

        void UpdateGrab()
        {
            if (grabbed == null || grabbed.IsDead)
            {
                ReleaseGrab(Vector3.zero, false);
                return;
            }

            // Drag him around (towards a tea kadai or a wall, for instance).
            Vector3 move = MoveInputWorld();
            // Shift: hurry him along (towards a tea kadai or the auto) instead of the slow drag.
            Motor.SetDesiredVelocity(move * (GameInput.Sprint ? dragSprintSpeed : dragSpeed));
            if (move.sqrMagnitude > 0.01f) Motor.FaceDirection(move);

            // Reach and yank him in, then keep him at arm's length by the collar.
            float pull = Mathf.Clamp01(stateTimer / GrabPullTime);
            Vector3 hold = HoldPosition;
            Vector3 pos = Vector3.Lerp(grabPullStart, hold, pull * pull * (3f - 2f * pull));
            Quaternion facing = Quaternion.LookRotation(grabFromBehind ? transform.forward : -transform.forward);
            // A little struggle so he never looks frozen.
            pos += transform.right * Mathf.Sin(Time.time * 7f) * 0.03f;
            grabbed.HoldAt(pos, facing);

            UpdatePendingGrabStrike();

            var spot = FinisherSpot.FindUsable(transform.position);
            Vector3 aimNow = move.sqrMagnitude > 0.01f ? move.normalized : transform.forward;
            WrestleMove wrestle = spot == null ? PickGrabFinisher(aimNow, move) : null;
            Prompts.Add(new PromptItem { key = "LMB", label = "Punch" });
            Prompts.Add(new PromptItem { key = "RMB", label = "Kick" });
            Prompts.Add(new PromptItem { key = "SHIFT", label = "Drag fast" });
            Prompts.Add(new PromptItem { key = "E", label = spot != null ? "Throw him in" : "Throw" });
            Prompts.Add(new PromptItem { key = "F", label = "Shove" });
            if (spot != null)
                Prompts.Add(new PromptItem { key = "Q", label = spot.finisherName, dim = Gethu < spot.gethuCost });
            else
                AddGrabFinisherPrompts(aimNow);

            if (stateTimer > 4.5f)
            {
                // He breaks free and shoves you off.
                var e = grabbed;
                ReleaseGrab(transform.forward * 3f, false);
                Motor.AddLunge(-transform.forward, 0.6f);
                SetState(PlayerState.Stagger);
                if (GM != null) GM.AddPopup("BROKE FREE", e.Center + Vector3.up, Color.gray, 0.7f);
                return;
            }

            if (grabActionCooldown > 0f) return;

            if (GameInput.GethuPressed)
            {
                if (spot != null && Gethu >= spot.gethuCost)
                {
                    StartGrabFinisher(PropSlam(spot), Flat(spot.SlamPoint - transform.position).normalized);
                    return;
                }
                if (spot == null && wrestle != null && Gethu >= wrestle.cost)
                {
                    // Turn to him first (S+Q turns you round while dragging him).
                    Vector3 toHim = Flat(grabbed.transform.position - transform.position);
                    StartGrabFinisher(wrestle, toHim.sqrMagnitude > 0.01f ? toHim.normalized : transform.forward);
                    return;
                }
                if (spot == null && wrestle == null && Gethu >= maxGethu) { StartFinisher(null); return; }
                if (GM != null) GM.AddPopup("Not enough Gethu", Center + Vector3.up, Color.gray, 0.6f);
            }

            Vector3 aim = move.sqrMagnitude > 0.01f ? move.normalized : transform.forward;

            // Shove: both hands into his chest, he stumbles back (and falls if he can't keep up).
            if (GameInput.BlockPressed)
            {
                var victim = grabbed;
                LetGo();
                Motor.FaceDirection(aim, instant: true);
                Visual.Play(FighterPose.Push, 0.4f, 0.55f);
                victim.Launch(this, aim, EnemyFighter.LaunchKind.Shove);
                victim.ReceiveHit(new HitInfo { attacker = this, damage = 5f, direction = aim, hitsDowned = true });
                RegisterHits(1, 5f);
                if (GM != null) { GM.Shake(0.15f); GM.AddPopup("SHOVE!", victim.Center + Vector3.up, new Color(1f, 0.85f, 0.4f), 0.7f); }
                SetState(PlayerState.Stagger);
                stateTimer = 0.1f;
                return;
            }

            // Throw, or a wall slam if there's a wall right where you're aiming.
            if (GameInput.GrabPressed)
            {
                if (TryWallSlam(aim)) return;

                // A real throw: swing him round by the collar, then let go.
                var victim = grabbed;
                float throwDamage = grabFromBehind ? 16f : 10f;
                LetGo();
                Motor.FaceDirection(aim, instant: true);
                StartWrestle(ThrowMove(throwDamage), victim);
                return;
            }

            // Clicks while holding him: grab strike combos (see PlayerGrabCombos.cs).
            if (pendingGrabStrike < 0f) UpdateGrabComboInput();
        }

        // Strike while holding someone: the hit waits for the animation's contact frame.
        float pendingGrabStrike = -1f;
        float grabStrikeTimer;
        FighterPose grabStrikePose;

        void UpdatePendingGrabStrike()
        {
            if (pendingGrabStrike < 0f || grabbed == null) return;
            pendingGrabStrike -= Time.deltaTime;
            if (pendingGrabStrike > 0f) return;
            pendingGrabStrike = -1f;

            float damage = grabStrikePose == FighterPose.Headbutt ? 9f : grabStrikePose == FighterPose.Cross ? 8f : 7f;
            float mult = MassMode ? massModeDamageMultiplier : 1f;
            var hit = knee.ToHit(this, transform.forward, mult);
            hit.damage = damage * mult;
            grabbed.ReceiveHit(hit);
            grabbed.Visual.Play(FighterPose.Flinch, 0.3f, 0.35f);
            FX.HitSpark(grabbed.Center + Vector3.up * (grabStrikePose == FighterPose.Knee ? -0.2f : 0.35f), false);
            RegisterHits(1, knee.gethuGain);
            if (GM != null) { GM.HitStop(knee.hitStop); GM.Shake(knee.shake); }
            if (grabbed != null && grabbed.IsDead) ReleaseGrab(Vector3.zero, false);
        }

        /// <summary>Wall right in the throw direction: run him into it head first.</summary>
        bool TryWallSlam(Vector3 aim)
        {
            Vector3 from = grabbed.Center;
            if (!Physics.SphereCast(from, 0.25f, aim, out var hit, 2.2f, CombatUtil.WorldMask, QueryTriggerInteraction.Ignore))
                return false;
            if (hit.collider.GetComponentInParent<FinisherSpot>() != null) return false;   // props: throw into them instead
            if (Vector3.Dot(hit.normal, Vector3.up) > 0.5f) return false;                  // that's the floor
            if (hit.collider.bounds.size.y < 1.2f) return false;                             // debris, boxes, bollards

            Motor.FaceDirection(aim, instant: true);
            finisherEndPoint = hit.point + hit.normal * (grabbed.radius + 0.15f);
            finisherEndPoint.y = transform.position.y;
            finisherDamage = grabFromBehind ? 32f : 26f;
            finisherTitle = "WALL SLAM!";
            finisherIsWallSlam = true;
            BeginFinisherSequence(null);
            return true;
        }

        // ------------------------------------------------------------------ weapons

        void ThrowWeapon()
        {
            var w = HeldWeapon;
            HeldWeapon = null;
            var target = CombatUtil.FindTarget(this, MoveInputWorld().sqrMagnitude > 0.01f ? MoveInputWorld() : transform.forward, 14f);
            Vector3 aim = target != null ? target.Center : Center + transform.forward * 10f;
            if (target != null) Motor.FaceDirection(target.transform.position - transform.position, instant: true);
            Visual.Play(FighterPose.ThrowObject, 0.35f, 0.5f);
            w.Throw(this, aim);
            SetState(PlayerState.Stagger);
            stateTimer = 0.1f;
        }

        void DropWeapon()
        {
            if (HeldWeapon == null) return;
            HeldWeapon.Drop();
            HeldWeapon = null;
        }

        // ------------------------------------------------------------------ held by an enemy

        /// <summary>An enemy's grab connects. Returns false if the hero can't be grabbed right now.</summary>
        public bool TryGetGrabbedBy(EnemyFighter enemy, bool fromBehind)
        {
            if (IsDead || Invulnerable || IsDowned || State == PlayerState.Held || State == PlayerState.Jump) return false;
            if (grabbed != null) ReleaseGrab(Vector3.zero, false);
            heldBy = enemy;
            HeldFromBehind = fromBehind;
            BreakFreeProgress = 0f;
            Motor.SetFrozen(true);
            SetState(PlayerState.Held);
            Visual.Play(FighterPose.Flinch, 0.3f);
            ComboHits = 0;
            lightIndex = 0;
            return true;
        }

        public void HeldAt(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>The holder let go (time ran out, he got hit, or he was knocked down).</summary>
        public void ReleasedByEnemy()
        {
            if (State != PlayerState.Held) return;
            heldBy = null;
            Motor.SetFrozen(false);
            SetState(PlayerState.Free);
        }

        void UpdateHeld()
        {
            if (heldBy == null || heldBy.IsDead)
            {
                ReleasedByEnemy();
                return;
            }
            Prompts.Add(new PromptItem { key = "E", label = "Tap fast to break free!" });

            // Mash to escape. A bear hug is harder to break than a collar grab.
            float perPress = heldBy.kind == EnemyKind.Thadiyan && !HeldFromBehind ? 0.11f : 0.16f;
            BreakFreeProgress = Mathf.Max(0f, BreakFreeProgress - 0.3f * Time.deltaTime);
            if (GameInput.GrabPressed)
            {
                BreakFreeProgress += perPress;
                Visual.Flinch(0.25f);
            }
            if (BreakFreeProgress < 1f) return;

            // Free: elbow / shove him off.
            var holder = heldBy;
            heldBy = null;
            Motor.SetFrozen(false);
            Vector3 away = Flat(holder.transform.position - transform.position).normalized;
            if (away.sqrMagnitude < 0.01f) away = -transform.forward;
            holder.HeroBrokeFree(away);
            Visual.Play(HeldFromBehind ? FighterPose.Backfist : FighterPose.Push, 0.35f, 0.45f);
            AddGethu(6f);
            if (GM != null) GM.AddPopup("BROKE FREE!", Center + Vector3.up, new Color(0.6f, 1f, 0.6f), 0.9f);
            SetState(PlayerState.Free);
        }

        /// <summary>Lets go of the grabbed rowdy without touching his state (he is being launched).</summary>
        void LetGo()
        {
            grabbed = null;
            Visual.SetGrip(null, false);
        }

        void ReleaseGrab(Vector3 push, bool knockDown)
        {
            if (grabbed != null)
            {
                grabbed.ReleaseFromGrab(push, knockDown);
                grabbed = null;
            }
            Visual.SetGrip(null, false);
            if (State == PlayerState.Grab) SetState(PlayerState.Free);
            Visual.Play(FighterPose.Idle, 0f);
        }

        // ------------------------------------------------------------------ Gethu: finishers and Mass Mode

        bool TryActivateGethu()
        {
            if (MassMode) return false;
            if (Gethu < maxGethu)
            {
                if (GM != null) GM.AddPopup("Gethu not full yet", Center + Vector3.up, Color.gray, 0.6f);
                return false;
            }

            massModeTimer = massModeDuration;
            if (GM != null)
            {
                GM.SlowMo(0.15f, 0.6f);
                GM.Shake(0.35f, 0.4f);
                GM.ShowBanner("GETHU MODE!", "Double-strong punches · no flinching · half damage", 2f);
            }
            if (cam != null) cam.FovKick(10f);
            FX.Burst(Center, 30, 8f, 0.1f, new Color(1f, 0.8f, 0.2f), new Color(3f, 2.2f, 0.4f), 0.6f, 0f);
            return true;
        }

        void StartFinisher(FinisherSpot spot)
        {
            finisherIsWallSlam = false;
            if (spot != null)
            {
                Gethu -= spot.gethuCost;
                Motor.FaceDirection(spot.transform.position - transform.position, instant: true);
                finisherEndPoint = new Vector3(spot.transform.position.x, transform.position.y, spot.transform.position.z);
                finisherDamage = spot.damage;
                finisherTitle = spot.finisherName;
            }
            else
            {
                Gethu = 0f;
                finisherEndPoint = transform.position + transform.forward * 0.9f;
                finisherDamage = 45f;
                finisherTitle = "GETHU SLAM!";
            }
            BeginFinisherSequence(spot);
        }

        void BeginFinisherSequence(FinisherSpot spot)
        {
            finisherSpot = spot;
            finisherVictim = grabbed;
            finisherVictimStart = grabbed.transform.position;
            finisherImpactDone = false;
            grabbed = null;
            Visual.SetGrip(null, false);
            SetState(PlayerState.Finisher);
            Visual.Play(finisherIsWallSlam ? FighterPose.Push : FighterPose.Slam, 0.6f, 0.9f);
            if (GM != null) GM.SlowMo(0.3f, finisherIsWallSlam ? 0.7f : 1.0f);
            if (cam != null) cam.FovKick(6f);
        }

        void UpdateFinisher()
        {
            const float impactTime = 0.32f;

            // Close side-on shot of the smash.
            if (cam != null)
            {
                Vector3 focus = Vector3.Lerp(transform.position, finisherEndPoint, 0.5f) + Vector3.up * 1.0f;
                Vector3 side = Vector3.Cross(Vector3.up, transform.forward);
                if (Vector3.Dot(cam.transform.position - transform.position, side) < 0f) side = -side;
                float span = Vector3.Distance(transform.position, finisherEndPoint) * 0.5f + 1.3f;
                cam.SetCinematicFraming(focus + side * 2.8f - transform.forward * 0.6f + Vector3.up * 0.2f, focus, 46f, 0f, focus, span);
            }

            if (!finisherImpactDone && finisherVictim != null)
            {
                float t = Mathf.Clamp01(stateTimer / impactTime);
                Vector3 end = finisherEndPoint;
                Vector3 arc = Vector3.up * Mathf.Sin(t * Mathf.PI) * (finisherIsWallSlam ? 0.2f : 1.2f);
                finisherVictim.HoldAt(Vector3.Lerp(finisherVictimStart, end, t) + arc,
                    Quaternion.LookRotation(-transform.forward));
            }

            if (!finisherImpactDone && stateTimer >= impactTime)
            {
                finisherImpactDone = true;
                FinisherImpact();
            }

            if (stateTimer >= 0.9f)
            {
                SetState(PlayerState.Free);
                if (cam != null) cam.EndCinematic(0.2f);
            }
        }

        void FinisherImpact()
        {
            var victim = finisherVictim;
            finisherVictim = null;
            Vector3 dir = transform.forward;
            float damage = finisherDamage;
            string title = finisherTitle;

            NotifyMoveLanded(title);
            if (victim != null && !victim.IsDead)
            {
                victim.ReleaseFromGrab(dir * 4f + Vector3.up * 3f, knockDown: true);
                victim.ReceiveHit(new HitInfo
                {
                    attacker = this, damage = damage, direction = dir,
                    knockback = 4f, launch = 3f, heavy = true, unblockable = true, hitsDowned = true,
                });
                RegisterHits(1, 0f);
            }

            if (finisherSpot != null)
            {
                finisherSpot.Break(dir);
            }
            else if (finisherIsWallSlam)
            {
                FX.Dust(finisherEndPoint + dir * 0.3f + Vector3.up * 1.2f, 18);
                FX.Burst(finisherEndPoint + dir * 0.3f + Vector3.up * 1.4f, 12, 4f, 0.08f,
                    new Color(0.6f, 0.6f, 0.55f), Color.black, 0.5f, -8f);
            }
            else
            {
                // Shockwave knocks down everyone nearby.
                var shock = new AttackData("Shockwave", FighterPose.Slam, 15f)
                    { range = 3.2f, arc = 360f, knockback = 7f, launch = 5f, heavy = true, unblockable = true, hitsDowned = true };
                var r = CombatUtil.Strike(this, shock, 1f);
                RegisterHits(r.hits, 0f);
                FX.Dust(transform.position + dir, 24);
            }

            if (cam != null) cam.ZoomPunch(7f);
            if (GM != null)
            {
                GM.HitStop(0.12f);
                GM.Shake(0.5f, 0.35f);
                GM.AddPopup(title, (victim != null ? victim.Center : Center) + Vector3.up, new Color(1f, 0.75f, 0.15f), 1.5f);
            }
            finisherSpot = null;
        }

        // ------------------------------------------------------------------ getting hit

        public override HitResult ReceiveHit(HitInfo hit)
        {
            if (IsDead || Invulnerable) return HitResult.Ignored;
            if (invincible) { Visual.Flinch(0.5f); return HitResult.Hit; }

            bool facing = Vector3.Dot(transform.forward, -hit.direction) > 0.2f;
            if (State == PlayerState.Block && facing && !hit.unblockable)
            {
                // Guard holds: slide back a little, his arm bounces off.
                Motor.AddLunge(hit.direction, hit.heavy ? 0.6f : 0.35f);
                AddGethu(2f);
                Visual.Flinch(0.4f);
                if (hit.attacker is EnemyFighter attacker) attacker.Recoil(hit.direction * -1f);
                FX.BlockSpark(Center + Vector3.up * 0.35f + transform.forward * 0.4f);
                if (GM != null) { GM.HitStop(0.05f); GM.Shake(0.08f); }
                return HitResult.Blocked;
            }

            if (State == PlayerState.Grab) ReleaseGrab(hit.direction * 2f, false);

            if (State == PlayerState.Held && hit.launch <= 0f)
            {
                // Held: you take it, but stay in his grip.
                ApplyDamage(hit.damage * (MassMode ? 0.5f : 1f), hit, HitResult.Hit);
                Visual.Flash();
                Visual.Flinch(0.8f);
                if (GM != null) GM.Shake(0.15f);
                return HitResult.Hit;
            }
            if (State == PlayerState.Held) ReleasedByEnemy();

            float damage = hit.damage * (MassMode ? 0.5f : 1f);
            ApplyDamage(damage, hit, HitResult.Hit);
            Visual.Flash();
            ComboHits = 0;
            ComboTimer = 0f;
            if (!MassMode) Gethu = Mathf.Max(0f, Gethu - 4f);
            if (GM != null) GM.Shake(hit.heavy ? 0.3f : 0.15f);
            if (IsDead) return HitResult.Hit;

            lightIndex = 0;
            if (MassMode)
            {
                Visual.Flinch(0.3f);
                return HitResult.Hit;
            }

            Motor.FaceDirection(-hit.direction, instant: true);
            if (hit.launch > 0f)
            {
                DropWeapon();
                SetState(PlayerState.Down);
                Visual.SetLying(true);
                Visual.Play(FighterPose.Knockdown, 1.1f);
                Motor.AddImpulse(hit.direction * hit.knockback + Vector3.up * hit.launch);
            }
            else
            {
                SetState(PlayerState.Stagger);
                Visual.Play(FighterPose.Flinch, 0.4f);
                Visual.Flinch(1f);
                Motor.AddImpulse(hit.direction * hit.knockback);
            }
            return HitResult.Hit;
        }

        protected override void OnDeath(HitInfo killingBlow)
        {
            if (grabbed != null) ReleaseGrab(Vector3.zero, false);
            if (State == PlayerState.Held) { heldBy = null; Motor.SetFrozen(false); }
            DropWeapon();
            SetState(PlayerState.Dead);
            massModeTimer = 0f;
            Visual.SetCombatStance(false);
            Vector3 push = killingBlow.direction * 4f + Vector3.up * 3f;
            if (Visual.CanRagdoll)
            {
                Motor.SetFrozen(true);
                Visual.StartRagdoll(push);
            }
            else
            {
                Visual.SetLying(true);
                Visual.Play(FighterPose.Dead, 999f);
                Motor.AddImpulse(push);
            }
            if (GM != null) GM.OnPlayerDied();
        }

        public void Respawn(Vector3 position)
        {
            if (wrestleMove != null) EndWrestle();
            if (combo != null) EndStrikeCombo();
            codeFirst = '\0';
            if (cam != null) cam.EndCinematic(0f);
            if (grabbed != null) ReleaseGrab(Vector3.zero, false);
            heldBy = null;
            Motor.SetFrozen(false);
            DropWeapon();
            ResetHealth(maxHealth);
            Gethu = 0f;
            massModeTimer = 0f;
            ComboHits = 0;
            lightIndex = 0;
            Motor.ClearImpulse();
            Motor.Teleport(position);
            Visual.ResetPose();
            SetState(PlayerState.Free);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace MadrasGethu
{
    /// <summary>
    /// The hero: a former North Chennai boxer. Handles movement, the boxing combo system,
    /// block and counter, grabs and throws, environmental finishers and the Gethu meter.
    /// </summary>
    public class PlayerFighter : Fighter
    {
        public enum PlayerState { Free, Attack, Dodge, Block, Grab, Stagger, Down, Finisher, Dead, Jump }

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

        public PlayerState State { get; private set; } = PlayerState.Free;
        public float Gethu { get; private set; }
        public float GethuFraction => Gethu / maxGethu;
        public bool MassMode => massModeTimer > 0f;
        public float MassModeFraction => massModeTimer / massModeDuration;
        public int ComboHits { get; private set; }
        public float ComboTimer { get; private set; }
        public string ContextPrompt { get; private set; }
        public EnemyFighter Grabbed => grabbed;

        ThirdPersonCamera cam;
        float stateTimer;
        AttackData currentAttack;
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
        bool followUpUsed;
        float massModeTimer;
        float grabActionCooldown;

        EnemyFighter grabbed;

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
            ContextPrompt = null;
            Visual.SetAnimatorBool("Holding", State == PlayerState.Grab);

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
                case PlayerState.Finisher: UpdateFinisher(); break;
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
            State = s;
            stateTimer = 0f;
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

            if (GameInput.GethuPressed && TryActivateGethu()) return true;

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

            // Dodge -> attack: spin out of the dodge with a backfist.
            if (Time.time - lastDodgeEnd < 0.4f)
            {
                StartAttack(dodgeStrike, null);
                lightIndex = 0;
                return true;
            }

            // Sprint -> light: running knee into whoever is ahead.
            if (light && GameInput.Sprint && Motor.MoveVelocity.magnitude > runSpeed * 0.8f)
            {
                StartAttack(runningAttack, null);
                lightIndex = 0;
                return true;
            }

            if (heavy)
            {
                var downed = CombatUtil.FindTarget(this, MoveInputWorld(), 2.2f, includeDowned: true);
                if (downed != null && downed.IsDowned)
                {
                    StartAttack(stomp, downed);
                }
                else if (State == PlayerState.Attack && heavyEnders.Contains(currentAttack) && !followUpUsed)
                {
                    // Heavy ender -> heavy again: Double Hammer.
                    followUpUsed = true;
                    StartAttack(heavyFollowUp, null);
                }
                else
                {
                    followUpUsed = false;
                    StartAttack(heavyEnders[Mathf.Min(lightIndex, heavyEnders.Count - 1)], null);
                }
                lightIndex = 0;
                return true;
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
            lungeSpeed = Mathf.Min(16f, lungeRemaining / Mathf.Max(attack.startup, 0.05f));
            Visual.Play(attack.pose, attack.startup + 0.2f, attack.startup + attack.recovery);
            if (attack.announce && GM != null)
                GM.AddPopup(attack.name.ToUpper(), Center + Vector3.up * 1.1f, new Color(1f, 0.85f, 0.35f), 0.75f);
        }

        void UpdateAttack()
        {
            // A counter is always available, even in the middle of your own punch.
            if (currentAttack != counter && currentAttack != counterUppercut && currentAttack != counterThrow
                && GameInput.BlockPressed && TryCounter())
                return;

            if (!struck && lungeRemaining > 0f)
            {
                float step = Mathf.Min(lungeRemaining, lungeSpeed * Time.deltaTime);
                lungeRemaining -= step;
                if (Time.deltaTime > 0f) Motor.SetDesiredVelocity(lungeDirection * (step / Time.deltaTime), instant: true);
            }

            if (!struck && stateTimer >= currentAttack.startup)
            {
                struck = true;
                if (attackTarget != null && !attackTarget.IsDead)
                    Motor.FaceDirection(attackTarget.transform.position - transform.position, instant: true);
                if (currentAttack.damage > 0f) PerformStrike();
            }

            float total = currentAttack.startup + currentAttack.recovery;
            // Dodge / block / grab can cut a move short; the next punch only comes in the combo window,
            // so every punch plays out and button-mashing gives a steady jab-cross-hook-body rhythm.
            bool canDefend = struck && stateTimer >= currentAttack.startup + 0.06f;
            bool canChain = struck && stateTimer >= currentAttack.startup + currentAttack.recovery * 0.55f;
            if (canDefend && TryCommonActions(allowAttacks: canChain)) return;

            if (stateTimer >= total)
            {
                if (currentAttack.heavy) lightIndex = 0;
                SetState(PlayerState.Free);
            }
        }

        void PerformStrike()
        {
            float mult = MassMode ? massModeDamageMultiplier : 1f;
            var result = CombatUtil.Strike(this, currentAttack, mult);
            if (result.hits > 0)
            {
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
            ComboHits += hits;
            ComboTimer = 2f;
            AddGethu(gethu);
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
            (State == PlayerState.Attack && !struck &&
             (currentAttack == counter || currentAttack == counterUppercut || currentAttack == counterThrow));

        // ------------------------------------------------------------------ block & counter

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

        /// <summary>Counter: tap block while an enemy flashes red (Sleeping Dogs style).</summary>
        bool TryCounter()
        {
            EnemyFighter best = null;
            float bestDist = 3.6f;
            foreach (var f in All)
            {
                if (f is EnemyFighter e && e.IsTelegraphing)
                {
                    float d = Flat(e.transform.position - transform.position).magnitude;
                    if (d < bestDist) { bestDist = d; best = e; }
                }
            }
            if (best == null) return false;

            best.Countered();
            // Mix it up: counter punch, counter uppercut (launch) or counter throw (send him flying).
            float roll = Random.value;
            var move = MassMode || roll < 0.34f ? counterUppercut : roll < 0.67f ? counter : counterThrow;
            StartAttack(move, best);
            if (GM != null)
            {
                GM.SlowMo(0.25f, 0.45f);
                GM.Shake(0.15f);
            }
            if (cam != null) cam.FovKick(5f);
            return true;
        }

        // ------------------------------------------------------------------ grabs

        void TryGrab()
        {
            var target = CombatUtil.FindTarget(this, MoveInputWorld().sqrMagnitude > 0.01f ? MoveInputWorld() : transform.forward, 2.0f)
                as EnemyFighter;
            if (target != null && target.CanBeGrabbed)
            {
                grabbed = target;
                grabbed.EnterGrabbed();
                Motor.FaceDirection(target.transform.position - transform.position, instant: true);
                SetState(PlayerState.Grab);
                grabActionCooldown = 0.2f;
                Visual.Play(FighterPose.Grab, 999f);
                lightIndex = 0;
                return;
            }

            if (target != null && GM != null)
                GM.AddPopup("TOO HEAVY! Stagger him first", target.Center + Vector3.up, new Color(1f, 0.6f, 0.3f), 0.7f);

            // Whiffed grab
            StartAttack(grabMiss, null);
        }

        void UpdateGrab()
        {
            if (grabbed == null || grabbed.IsDead)
            {
                ReleaseGrab(Vector3.zero, false);
                return;
            }

            // Drag the enemy around (towards a tea kadai, for instance).
            Vector3 move = MoveInputWorld();
            Motor.SetDesiredVelocity(move * dragSpeed);
            if (move.sqrMagnitude > 0.01f) Motor.FaceDirection(move);
            grabbed.HoldAt(transform.position + transform.forward * (0.75f + grabbed.radius),
                Quaternion.LookRotation(-transform.forward));

            var spot = FinisherSpot.FindUsable(transform.position);
            if (spot != null && Gethu >= spot.gethuCost)
                ContextPrompt = $"Q / RT  —  {spot.finisherName}";
            else if (spot != null)
                ContextPrompt = $"Need {spot.gethuCost:0} Gethu for the {spot.promptName} finisher";
            else if (Gethu >= maxGethu)
                ContextPrompt = "Q / RT  —  GETHU SLAM!";
            else
                ContextPrompt = "LMB / X  knee   ·   E / B or RMB / Y  throw";

            if (stateTimer > 4.5f)
            {
                // He wriggles free
                ReleaseGrab(transform.forward * 3f, false);
                SetState(PlayerState.Free);
                return;
            }

            if (grabActionCooldown > 0f) return;

            if (GameInput.GethuPressed)
            {
                if (spot != null && Gethu >= spot.gethuCost) { StartFinisher(spot); return; }
                if (spot == null && Gethu >= maxGethu) { StartFinisher(null); return; }
            }

            if (GameInput.GrabPressed || Time.time < heavyBufferedUntil)
            {
                heavyBufferedUntil = 0f;
                var victim = grabbed;
                grabbed = null;
                Vector3 throwDir = move.sqrMagnitude > 0.01f ? move.normalized : transform.forward;
                Motor.FaceDirection(throwDir, instant: true);
                victim.Throw(this, throwDir);
                victim.ReceiveHit(new HitInfo { attacker = this, damage = 10f, direction = throwDir, hitsDowned = true });
                RegisterHits(1, 8f);
                Visual.Play(FighterPose.Throw, 0.4f, 0.7f);
                if (GM != null) GM.Shake(0.2f);
                SetState(PlayerState.Stagger);   // brief recovery
                stateTimer = 0.1f;
                return;
            }

            if (Time.time < lightBufferedUntil)
            {
                lightBufferedUntil = 0f;
                grabActionCooldown = knee.recovery;
                Visual.Play(FighterPose.Knee, 0.25f, 0.4f);
                float mult = MassMode ? massModeDamageMultiplier : 1f;
                grabbed.ReceiveHit(knee.ToHit(this, transform.forward, mult));
                FX.HitSpark(grabbed.Center, false);
                RegisterHits(1, knee.gethuGain);
                if (GM != null) { GM.HitStop(knee.hitStop); GM.Shake(knee.shake); }
                if (grabbed != null && grabbed.IsDead) ReleaseGrab(Vector3.zero, false);
            }
        }

        void ReleaseGrab(Vector3 push, bool knockDown)
        {
            if (grabbed != null)
            {
                grabbed.ReleaseFromGrab(push, knockDown);
                grabbed = null;
            }
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
            finisherSpot = spot;
            finisherVictim = grabbed;
            finisherVictimStart = grabbed.transform.position;
            finisherImpactDone = false;
            grabbed = null;
            SetState(PlayerState.Finisher);

            if (spot != null)
            {
                Gethu -= spot.gethuCost;
                Motor.FaceDirection(spot.transform.position - transform.position, instant: true);
            }
            else
            {
                Gethu = 0f;
            }

            Visual.Play(FighterPose.Slam, 0.6f, 0.9f);
            if (GM != null) GM.SlowMo(0.3f, 1.0f);
            if (cam != null) cam.FovKick(6f);
        }

        void UpdateFinisher()
        {
            const float impactTime = 0.32f;

            if (!finisherImpactDone && finisherVictim != null)
            {
                float t = Mathf.Clamp01(stateTimer / impactTime);
                Vector3 end = finisherSpot != null
                    ? new Vector3(finisherSpot.transform.position.x, transform.position.y, finisherSpot.transform.position.z)
                    : transform.position + transform.forward * 0.9f;
                Vector3 arc = Vector3.up * Mathf.Sin(t * Mathf.PI) * 1.2f;
                finisherVictim.HoldAt(Vector3.Lerp(finisherVictimStart, end, t) + arc,
                    Quaternion.LookRotation(-transform.forward));
            }

            if (!finisherImpactDone && stateTimer >= impactTime)
            {
                finisherImpactDone = true;
                FinisherImpact();
            }

            if (stateTimer >= 0.9f)
                SetState(PlayerState.Free);
        }

        void FinisherImpact()
        {
            var victim = finisherVictim;
            finisherVictim = null;
            Vector3 dir = transform.forward;
            float damage = finisherSpot != null ? finisherSpot.damage : 45f;
            string title = finisherSpot != null ? finisherSpot.finisherName : "GETHU SLAM!";

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
            else
            {
                // Shockwave knocks down everyone nearby.
                var shock = new AttackData("Shockwave", FighterPose.Slam, 15f)
                    { range = 3.2f, arc = 360f, knockback = 7f, launch = 5f, heavy = true, unblockable = true, hitsDowned = true };
                var r = CombatUtil.Strike(this, shock, 1f);
                RegisterHits(r.hits, 0f);
                FX.Dust(transform.position + dir, 24);
            }

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

            bool facing = Vector3.Dot(transform.forward, -hit.direction) > 0.2f;
            if (State == PlayerState.Block && facing && !hit.unblockable)
            {
                Motor.AddImpulse(hit.direction * hit.knockback * 0.6f);
                AddGethu(2f);
                Visual.Flinch(0.3f);
                if (GM != null) GM.HitStop(0.04f);
                return HitResult.Blocked;
            }

            if (State == PlayerState.Grab) ReleaseGrab(hit.direction * 2f, false);

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
            SetState(PlayerState.Dead);
            massModeTimer = 0f;
            Visual.SetLying(true);
            Visual.Play(FighterPose.Dead, 999f);
            Visual.SetCombatStance(false);
            Motor.AddImpulse(killingBlow.direction * 4f + Vector3.up * 3f);
            if (GM != null) GM.OnPlayerDied();
        }

        public void Respawn(Vector3 position)
        {
            if (grabbed != null) ReleaseGrab(Vector3.zero, false);
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

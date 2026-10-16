using System.Collections.Generic;
using UnityEngine;

namespace MadrasGethu
{
    /// <summary>
    /// Street enemy AI, Assassin's Creed / Sleeping Dogs style, with moves only enemies have.
    ///
    /// Crowd: rowdies WAIT in a wide ring (stance, pacing, taunts, the odd thrown bottle, walking over to
    /// grab a lathi) or are ENGAGED (close, allowed to attack). EnemyDirector lets one attack at a time.
    ///
    /// Every attack is telegraphed by colour: RED strike (parry/block), ORANGE unblockable / charge /
    /// ground pound (dodge), YELLOW grab (dodge, or mash E to break free once caught).
    ///
    /// Rowdy: wild swings, stomach kicks, shoves, collar grabs, hold-from-behind, thrown bottles, weapons.
    /// Thadiyan: shoulder charge, ground pound, bear hug, armour against light hits.
    /// Boss: combos, feints, counters a flurry, grab-throw, and at half health rages with an iron pipe.
    /// </summary>
    public class EnemyFighter : Fighter
    {
        enum AIState
        {
            Approach, Wait, Engage, CloseIn, Windup, Recover, Stagger, Down, GetUp, Grabbed, Dead,
            Charging, HoldingHero, ThrowingObject, SeekWeapon, Begging, Fleeing, Roaring, Dazed,
        }

        public EnemyKind kind;
        public EnemyProfile Profile { get; private set; }

        AIState state = AIState.Approach;
        float stateTimer;
        AttackData currentAttack;
        float strafeDirection = 1f;
        float nextAttackTime;
        float waitRadius;
        float engageRadius;
        bool pacing;
        float paceTimer;
        float nextEngageCheck;
        float nextTauntTime;
        int poiseHits;
        bool hasToken;

        // Combos and feints (boss)
        AttackData[] currentCombo;
        int comboStep;
        bool feinting;
        int blockStreak;
        float lastBlockTime;

        // Feint punish: the counter-punch in flight
        PlayerFighter punishTarget;
        float punishIn;

        // Swing animation timed to land with the hit
        bool strikeAnimStarted;
        float strikeLead;

        // Charge (Thadiyan)
        Vector3 chargeDirection;
        bool chargeHit;

        // Holding the hero (grab / bear hug / hold from behind)
        bool holdFromBehind;
        float squeezeTimer;

        // Weapons and thrown objects
        public Weapon HeldWeapon { get; private set; }
        Weapon seekTarget;
        float nextWeaponCheck;
        float nextThrowTime;
        bool throwReleased;
        static float globalNextThrow;
        static readonly HashSet<Weapon> reservedWeapons = new HashSet<Weapon>();

        // Mood
        bool phaseTwo;
        bool hurtShouted;
        bool surrendered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            globalNextThrow = 0f;
            reservedWeapons.Clear();
        }

        // Being thrown into other enemies
        Fighter thrower;
        float thrownTimer;
        readonly HashSet<Fighter> thrownHits = new HashSet<Fighter>();

        public bool IsTelegraphing => state == AIState.Windup;
        public TelegraphColor CurrentTelegraph => state == AIState.Windup && currentAttack != null ? currentAttack.Telegraph : TelegraphColor.None;
        public bool IsFeinting => state == AIState.Windup && feinting;
        public bool IsGrabbed => state == AIState.Grabbed;
        public bool IsBegging => state == AIState.Begging;
        public bool IsFleeing => state == AIState.Fleeing;
        public bool IsDazed => state == AIState.Dazed;

        /// <summary>Practice dummy: never moves on his own, never attacks, never dies.</summary>
        public bool isDummy;
        protected override bool Immortal => isDummy;
        float lastHitTime;
        /// <summary>The hero can grab him now (Thadiyan included) and use a wrestling finisher.</summary>
        public bool IsOpen => state == AIState.Dazed || state == AIState.Stagger;
        public override bool IsDowned => state == AIState.Down || state == AIState.GetUp;
        /// <summary>Lying on the ground (not already getting up).</summary>
        public bool IsLyingDown => state == AIState.Down;
        public override bool CanBeTargeted => !IsDead && state != AIState.Grabbed;
        public bool IsBoss => kind == EnemyKind.Boss;

        public bool CanBeGrabbed =>
            !IsDead && !IsDowned && state != AIState.Grabbed && state != AIState.HoldingHero &&
            (Profile.grabbable || state == AIState.Stagger || state == AIState.Dazed);

        // Dazed and wall bounces
        bool dazedOnce;
        float wallBounceTimer;
        float dazeWobble;

        float MoodSpeed => !IsBoss && HealthFraction < 0.3f ? 0.7f : phaseTwo ? 1.15f : 1f;
        float MoodCooldown => !IsBoss && HealthFraction < 0.3f ? 1.4f : phaseTwo ? 0.7f : 1f;

        public void Configure(EnemyKind enemyKind)
        {
            kind = enemyKind;
            Profile = EnemyProfile.Get(enemyKind);
            displayName = Profile.displayName;
            team = Team.Enemy;
            ResetHealth(Profile.health);
            radius = 0.4f * Profile.scale;
            waitRadius = Random.Range(5.5f, 7.5f);
            engageRadius = Random.Range(2.3f, 3.0f);
            strafeDirection = Random.value < 0.5f ? -1f : 1f;
            nextTauntTime = Time.time + Random.Range(3f, 8f);
            nextThrowTime = Time.time + Random.Range(6f, 12f);
            nextWeaponCheck = Time.time + Random.Range(2f, 5f);
        }

        protected override void Awake()
        {
            base.Awake();
            if (Profile == null) Configure(kind);
        }

        PlayerFighter Player => GameManager.Instance != null ? GameManager.Instance.Player : null;

        void Update()
        {
            Visual.SetAnimatorBool("Held", state == AIState.Grabbed);
            Visual.SetAnimatorBool("Holding", state == AIState.HoldingHero);
            Visual.SetAnimatorBool("Begging", state == AIState.Begging);
            // Kneeling or doubled over (low kick, liver shot) plays its own pose instead of the dizzy sway.
            Visual.SetAnimatorBool("Dazed", state == AIState.Dazed && Time.time >= ownReactionUntil);
            if (state == AIState.Dead) return;
            float dt = Time.deltaTime;
            stateTimer -= dt;

            var player = Player;
            bool playerAlive = player != null && !player.IsDead;

            if (thrownTimer > 0f)
            {
                thrownTimer -= dt;
                HitOthersWhileThrown();
            }
            if (punishTarget != null)
            {
                punishIn -= dt;
                if (punishIn <= 0f)
                {
                    Vector3 dir = Flat(punishTarget.transform.position - transform.position).normalized;
                    if (Flat(punishTarget.transform.position - transform.position).magnitude < 2.6f)
                        punishTarget.ReceiveHit(new HitInfo { attacker = this, damage = 12f, direction = dir, knockback = 4f, heavy = true, unblockable = true });
                    punishTarget = null;
                }
            }
            if (wallBounceTimer > 0f)
            {
                wallBounceTimer -= dt;
                CheckWallBounce();
            }

            if (!hurtShouted && !IsBoss && !isDummy && HealthFraction < 0.3f)
            {
                hurtShouted = true;
                Shouts.Say(this, ShoutType.Hurt);
            }

            if (!playerAlive && state != AIState.Down && state != AIState.GetUp && state != AIState.Grabbed)
            {
                // The hero is down for good: stand and gloat.
                if (state == AIState.HoldingHero) StopHoldingHero();
                ReleaseToken();
                Visual.SetTelegraph(false);
                Visual.SetCombatStance(false);
                return;
            }

            Vector3 toPlayer = playerAlive ? Flat(player.transform.position - transform.position) : Vector3.zero;
            float dist = toPlayer.magnitude;
            Vector3 dirToPlayer = dist > 0.01f ? toPlayer / dist : transform.forward;

            switch (state)
            {
                case AIState.Approach:
                    if (isDummy) { DummyIdle(); break; }
                    Visual.SetCombatStance(dist < 9f);
                    MoveTowards(dirToPlayer, Profile.runSpeed * MoodSpeed);
                    if (dist <= waitRadius + 1f) state = AIState.Wait;
                    break;

                case AIState.Wait:
                    if (isDummy) { DummyIdle(); break; }
                    if (Time.time >= nextEngageCheck)
                    {
                        nextEngageCheck = Time.time + 0.4f;
                        if (EnemyDirector.RequestEngage(this, player.transform.position))
                        {
                            state = AIState.Engage;
                            nextAttackTime = Time.time + Random.Range(0.8f, 1.8f);
                            break;
                        }
                    }
                    if (TryStartThrow(player, dist)) break;
                    if (TryStartWeaponSeek()) break;
                    HoldRing(dirToPlayer, dist, waitRadius, 1.0f * MoodSpeed, allowTaunt: true);
                    break;

                case AIState.Engage:
                    if (isDummy) { DummyIdle(); break; }
                    if (!EnemyDirector.IsEngaged(this))
                    {
                        state = AIState.Wait;   // swapped out: step back and watch
                        break;
                    }
                    HoldRing(dirToPlayer, dist, engageRadius, Profile.circleSpeed * MoodSpeed, allowTaunt: false);
                    if (Time.time >= nextAttackTime && !player.IsDowned && !player.IsHeld && dist < engageRadius + 1.5f
                        && EnemyDirector.RequestAttack(this))
                    {
                        hasToken = true;
                        ChooseAttack(player, dist);
                        state = AIState.CloseIn;
                        stateTimer = 2.5f;
                    }
                    else if (Time.time >= nextAttackTime && player.IsHeld && !player.IsHeldBy(this) && dist < engageRadius + 2f
                             && EnemyDirector.RequestAttack(this))
                    {
                        // A friend is holding him from behind: free hits.
                        hasToken = true;
                        currentAttack = Profile.attacks[0];
                        currentCombo = null;
                        feinting = false;
                        state = AIState.CloseIn;
                        stateTimer = 2f;
                    }
                    break;

                case AIState.CloseIn:
                {
                    bool charge = currentAttack.kind == AttackKind.Charge;
                    float reach = charge ? currentAttack.range : currentAttack.range * 0.85f + player.radius;
                    if (!charge) MoveTowards(dirToPlayer, Mathf.Min(Profile.runSpeed, 3.2f) * MoodSpeed);
                    else Motor.FaceDirection(dirToPlayer);
                    if (dist <= reach)
                        BeginWindup();
                    else if (stateTimer <= 0f || player.IsDowned)
                        EndAttack();
                    break;
                }

                case AIState.Windup:
                    Motor.FaceDirection(dirToPlayer);
                    if (!strikeAnimStarted && strikeLead > 0f && stateTimer <= strikeLead && !feinting)
                    {
                        strikeAnimStarted = true;
                        Visual.PlayTimed(currentAttack.pose, Mathf.Max(0.05f, stateTimer), currentAttack.recovery);
                        GameAudio.Play(Sfx.Whoosh, Center + transform.forward * 0.4f, 0.45f, 0.95f);
                    }
                    if (stateTimer <= 0f) ReleaseAttack(dist, dirToPlayer, player);
                    break;

                case AIState.Recover:
                    if (stateTimer <= 0f) ContinueComboOrEnd(player, dist);
                    break;

                case AIState.Charging:
                    UpdateCharge(player);
                    break;

                case AIState.HoldingHero:
                    UpdateHoldingHero(player);
                    break;

                case AIState.ThrowingObject:
                    Motor.FaceDirection(dirToPlayer);
                    if (!throwReleased && stateTimer <= 0.3f)
                    {
                        throwReleased = true;
                        ReleaseThrownObject(player);
                    }
                    if (stateTimer <= 0f) state = AIState.Wait;
                    break;

                case AIState.SeekWeapon:
                    UpdateSeekWeapon();
                    break;

                case AIState.Stagger:
                    if (stateTimer <= 0f) ResumeStance();
                    break;

                case AIState.Roaring:
                    if (stateTimer <= 0f) ResumeStance();
                    break;

                case AIState.Down:
                {
                    bool landed = Visual.IsRagdoll ? Visual.RagdollSettled : Motor.IsGrounded;
                    if (thrownTimer <= 0f && thrower != null && landed)
                    {
                        FX.Dust(transform.position, 10);   // thud on landing
                        thrower = null;
                    }
                    if (stateTimer <= 0f && landed)
                    {
                        state = AIState.GetUp;
                        if (Visual.IsRagdoll)
                        {
                            // Get up from however he landed: on his back or face down.
                            bool faceUp = Visual.EndRagdoll();
                            Motor.SetFrozen(false);
                            stateTimer = 1.3f;
                            Visual.Play(faceUp ? FighterPose.GetUp : FighterPose.GetUpFront, stateTimer, stateTimer);
                        }
                        else
                        {
                            stateTimer = 0.8f;
                            Visual.SetLying(false);
                            Visual.Play(FighterPose.GetUp, 0.8f);
                        }
                    }
                    break;
                }

                case AIState.GetUp:
                    if (stateTimer <= 0f)
                    {
                        ResumeStance();
                        nextAttackTime = Mathf.Max(nextAttackTime, Time.time + Random.Range(1f, 2f));
                    }
                    break;

                case AIState.Begging:
                    Motor.FaceDirection(dirToPlayer);
                    if (stateTimer <= 0f) StartFleeing();   // nobody decided: he makes a run for it
                    break;

                case AIState.Fleeing:
                    UpdateFleeing(dirToPlayer);
                    break;

                case AIState.Grabbed:
                    break;

                case AIState.Dazed:
                {
                    // Seeing stars: sways on the spot, can't fight back.
                    dazeWobble += dt;
                    if (Time.time < ownReactionUntil) { Motor.SetDesiredVelocity(Vector3.zero); if (stateTimer <= 0f) ResumeStance(); break; }
                    Vector3 sway = transform.right * Mathf.Sin(dazeWobble * 3.1f) * 0.35f + transform.forward * Mathf.Sin(dazeWobble * 1.7f) * 0.2f;
                    Motor.SetDesiredVelocity(sway);
                    if (Visual.IsPlaceholder && Mathf.Repeat(dazeWobble, 0.9f) < dt) Visual.Flinch(0.6f);
                    if (stateTimer <= 0f) ResumeStance();
                    break;
                }
            }
        }

        /// <summary>Dummy: stands his ground in a guard, and heals back up a little after you stop hitting him.</summary>
        void DummyIdle()
        {
            Motor.SetDesiredVelocity(Vector3.zero);
            Visual.SetCombatStance(true);
            if (Time.time - lastHitTime > 2.5f && Health < maxHealth)
            {
                Heal(maxHealth);
                dazedOnce = false;
            }
        }

        /// <summary>Back to fighting stance: close in if this rowdy holds a combat slot, otherwise wait.</summary>
        void ResumeStance()
        {
            if (surrendered) { StartFleeing(); return; }
            state = EnemyDirector.IsEngaged(this) ? AIState.Engage : AIState.Wait;
        }

        // ------------------------------------------------------------------ movement

        void MoveTowards(Vector3 direction, float speed)
        {
            Vector3 v = (direction + Separation() * 0.8f).normalized * speed;
            Motor.SetDesiredVelocity(v);
            Motor.FaceDirection(direction);
        }

        /// <summary>
        /// Keep a distance from the hero in fighting stance, facing him, with stop-and-go side steps,
        /// like a real street fighter sizing someone up.
        /// </summary>
        void HoldRing(Vector3 dirToPlayer, float dist, float ringRadius, float stepSpeed, bool allowTaunt)
        {
            Visual.SetCombatStance(true);

            // Far behind (the hero ran off): jog to catch up.
            if (dist > ringRadius + 3f)
            {
                MoveTowards(dirToPlayer, Mathf.Min(Profile.runSpeed, 3.5f) * MoodSpeed);
                return;
            }

            paceTimer -= Time.deltaTime;
            if (paceTimer <= 0f)
            {
                pacing = !pacing;
                paceTimer = pacing ? Random.Range(1.0f, 2.5f) : Random.Range(0.8f, 2.2f);
                if (pacing && Random.value < 0.5f) strafeDirection = -strafeDirection;
                if (!pacing && allowTaunt && Time.time > nextTauntTime && Random.value < 0.35f)
                {
                    Visual.Play(FighterPose.Taunt, 1.5f);
                    Shouts.Say(this, ShoutType.Taunt, 0.6f);
                    nextTauntTime = Time.time + Random.Range(6f, 12f);
                }
            }

            Vector3 tangent = Vector3.Cross(Vector3.up, dirToPlayer) * strafeDirection;
            float radial = Mathf.Clamp(dist - ringRadius, -1f, 1f);
            if (Mathf.Abs(radial) < 0.25f) radial = 0f;   // close enough: don't shuffle back and forth
            Vector3 v = dirToPlayer * radial * 0.8f + (pacing ? tangent * 0.7f : Vector3.zero) + Separation() * 1.2f;
            v = Vector3.ClampMagnitude(v, 1f) * stepSpeed;

            Motor.SetDesiredVelocity(v);
            Motor.FaceDirection(dirToPlayer);
        }

        Vector3 Separation()
        {
            Vector3 push = Vector3.zero;
            foreach (var f in All)
            {
                if (f == this || f.team != team || f.IsDead) continue;
                Vector3 away = Flat(transform.position - f.transform.position);
                float d = away.magnitude;
                if (d < 1.8f && d > 0.01f) push += away / d * (1.8f - d);
            }
            return push;
        }

        // ------------------------------------------------------------------ choosing and making attacks

        void ChooseAttack(PlayerFighter player, float dist)
        {
            currentCombo = null;
            comboStep = 0;
            feinting = false;

            if (HeldWeapon != null)
            {
                bool heavy = Random.value < 0.3f;
                currentAttack = HeldWeapon.MakeAttack(heavy);
                currentAttack.startup = heavy ? 0.8f : 0.55f;
                currentAttack.recovery = heavy ? 0.8f : 0.6f;
                currentAttack.announce = false;
                currentAttack.gethuGain = 0f;
                if (heavy) currentAttack.unblockable = true;   // orange: dodge the overhead smash
                return;
            }

            if (Profile.combos.Count > 0 && Random.value < Profile.comboChance)
            {
                currentCombo = Profile.combos[Random.Range(0, Profile.combos.Count)];
                currentAttack = currentCombo[0];
                return;
            }

            currentAttack = Profile.PickAttack();
            if (currentAttack.kind == AttackKind.Strike && !currentAttack.unblockable && Random.value < Profile.feintChance)
                feinting = true;
        }

        void BeginWindup()
        {
            state = AIState.Windup;
            float startup = currentAttack.startup;
            if (currentCombo != null && comboStep > 0) startup = Mathf.Max(0.28f, startup * 0.65f);   // combo follow-ups come faster
            stateTimer = startup;
            Visual.SetTelegraph(currentAttack.Telegraph);
            // Wind up first, then start the swing so the fist arrives exactly as the windup ends (when the hit lands).
            strikeAnimStarted = false;
            float swing = currentAttack.kind == AttackKind.Charge ? 0f : Visual.ImpactTime(currentAttack.pose) / 1.1f;
            strikeLead = Mathf.Min(startup * 0.9f, swing);
            if (strikeLead < 0.05f) strikeLead = 0f;
            Visual.Play(FighterPose.Windup, startup - strikeLead, startup - strikeLead);

            if (GameManager.Instance != null)
            {
                if (currentAttack.Telegraph == TelegraphColor.Orange)
                    GameManager.Instance.AddPopup(currentAttack.kind == AttackKind.Charge ? "CHARGE! DODGE!" : "UNBLOCKABLE!",
                        Center + Vector3.up, new Color(1f, 0.55f, 0.1f), 0.8f);
                else if (currentAttack.Telegraph == TelegraphColor.Yellow)
                    GameManager.Instance.AddPopup("GRAB! DODGE!", Center + Vector3.up, new Color(1f, 0.9f, 0.2f), 0.8f);
            }
            if (comboStep == 0) Shouts.Say(this, currentAttack.kind == AttackKind.Grab ? ShoutType.Grab : ShoutType.Attack, 0.3f);
        }

        void ReleaseAttack(float dist, Vector3 dirToPlayer, PlayerFighter player)
        {
            Visual.SetTelegraph(false);

            if (feinting)
            {
                // Fake: pull the punch, step back, laugh.
                feinting = false;
                Visual.Play(FighterPose.Taunt, 0.6f, 0.6f);
                Motor.AddLunge(-transform.forward, 0.5f);
                Shouts.Say(this, ShoutType.Feint, 0.8f);
                state = AIState.Recover;
                stateTimer = 0.5f;
                return;
            }

            switch (currentAttack.kind)
            {
                case AttackKind.Charge:
                    chargeDirection = dirToPlayer;
                    chargeHit = false;
                    state = AIState.Charging;
                    stateTimer = 1.3f;
                    Visual.Play(FighterPose.Charge, 1.3f, 1.3f);
                    return;

                case AttackKind.Grab:
                {
                    if (!strikeAnimStarted) Visual.Play(currentAttack.pose, 0.4f, 0.6f);
                    Motor.AddLunge(transform.forward, Mathf.Clamp(dist - 0.8f, 0f, currentAttack.lunge));
                    bool inFront = Vector3.Angle(transform.forward, Flat(player.transform.position - transform.position)) < 60f;
                    if (dist <= currentAttack.range + player.radius + 0.4f && inFront)
                    {
                        // From behind him (he's facing away) -> hold him for the gang.
                        holdFromBehind = Vector3.Dot(player.transform.forward, dirToPlayer) > 0.35f && kind == EnemyKind.Rowdy;
                        if (player.TryGetGrabbedBy(this, holdFromBehind))
                        {
                            state = AIState.HoldingHero;
                            stateTimer = HoldDuration;
                            squeezeTimer = 0.4f;
                            Visual.SetGrip(player, holdFromBehind);
                            EnemyDirector.HeroHeldFromBehind = holdFromBehind;
                            if (GameManager.Instance != null)
                                GameManager.Instance.AddPopup(holdFromBehind ? "HELD FROM BEHIND!" : currentAttack.name.ToUpper() + "!",
                                    player.Center + Vector3.up, new Color(1f, 0.85f, 0.2f), 0.9f);
                            return;
                        }
                    }
                    state = AIState.Recover;   // missed: stumbles past
                    stateTimer = currentAttack.recovery;
                    return;
                }
            }

            // Strikes, weapon swings, ground pound. (The swing animation already started during the windup.)
            if (!strikeAnimStarted) Visual.Play(currentAttack.pose, 0.3f, currentAttack.recovery + 0.3f);
            float lunge = Mathf.Clamp(dist - currentAttack.range * 0.6f, 0f, currentAttack.lunge);
            Motor.AddLunge(transform.forward, lunge);
            var result = CombatUtil.Strike(this, currentAttack);
            if (currentAttack.kind == AttackKind.GroundPound)
            {
                FX.Dust(transform.position, 28);
                if (GameManager.Instance != null) GameManager.Instance.Shake(0.45f, 0.35f);
            }
            else if (result.hits > 0 && GameManager.Instance != null)
            {
                GameManager.Instance.Shake(currentAttack.heavy ? 0.25f : 0.12f);
            }
            if (result.Connected && HeldWeapon != null && HeldWeapon.RegisterHit()) HeldWeapon = null;
            state = AIState.Recover;
            stateTimer = currentAttack.recovery;
            if (currentCombo != null && comboStep < currentCombo.Length - 1) stateTimer = 0.15f;
        }

        void ContinueComboOrEnd(PlayerFighter player, float dist)
        {
            if (currentCombo != null && comboStep < currentCombo.Length - 1 && player != null && !player.IsDowned && !player.IsDead)
            {
                comboStep++;
                currentAttack = currentCombo[comboStep];
                if (dist <= currentAttack.range + player.radius + 0.6f) BeginWindup();
                else { state = AIState.CloseIn; stateTimer = 1.2f; }
                return;
            }
            EndAttack();
        }

        void EndAttack()
        {
            currentCombo = null;
            feinting = false;
            ReleaseToken();
            ResumeStance();
            nextAttackTime = Time.time + Random.Range(Profile.attackCooldown.x, Profile.attackCooldown.y) * MoodCooldown;
        }

        void ReleaseToken()
        {
            if (!hasToken) return;
            hasToken = false;
            EnemyDirector.Release(this);
        }

        /// <summary>Called when the player counters this enemy's telegraphed attack.</summary>
        public void Countered() => Parried(true, -transform.forward);

        /// <summary>The hero bit on a feint: punish him with a quick, unparryable jab.</summary>
        public void PunishParry(PlayerFighter hero)
        {
            feinting = false;
            Visual.SetTelegraph(false);
            // The punch lands when his fist gets there, not the moment he starts it.
            punishTarget = hero;
            punishIn = Visual.PlayTimed(FighterPose.Jab, 0.14f, 0.3f);
            Shouts.Say(this, ShoutType.Feint);
            if (GameManager.Instance != null)
                GameManager.Instance.AddPopup("FEINT!", Center + Vector3.up, new Color(1f, 0.5f, 0.3f), 1f);
            state = AIState.Recover;
            stateTimer = 0.5f;
        }

        // ------------------------------------------------------------------ Thadiyan charge

        void UpdateCharge(PlayerFighter player)
        {
            Motor.SetDesiredVelocity(chargeDirection * 9f, instant: true);
            Motor.FaceDirection(chargeDirection, true);

            if (!chargeHit && player != null)
            {
                float d = Flat(player.transform.position - transform.position).magnitude;
                if (d < radius + player.radius + 0.45f)
                {
                    chargeHit = true;
                    player.ReceiveHit(currentAttack.ToHit(this, chargeDirection));
                    if (GameManager.Instance != null) GameManager.Instance.Shake(0.4f, 0.3f);
                }
            }

            // Crashing into a prop or a wall leaves him dazed: your opening.
            var spot = FinisherSpot.FindHit(transform.position, radius + 0.3f);
            bool wall = Physics.SphereCast(Center, 0.4f, chargeDirection, out var hit, 0.6f, CombatUtil.WorldMask, QueryTriggerInteraction.Ignore)
                        && hit.collider.bounds.size.y > 1.2f && Vector3.Dot(hit.normal, Vector3.up) < 0.5f;
            if (spot != null || wall)
            {
                if (spot != null) spot.Break(chargeDirection);
                FX.Dust(transform.position + chargeDirection * 0.6f, 16);
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.Shake(0.4f, 0.3f);
                    GameManager.Instance.AddPopup("STUNNED!", Center + Vector3.up, new Color(0.6f, 0.9f, 1f), 1f);
                }
                Motor.ClearImpulse();
                ReleaseToken();
                state = AIState.Stagger;
                stateTimer = 1.8f;
                Visual.Play(FighterPose.StaggerBack, 1.8f, 1.2f);
                poiseHits = Profile.poise;   // he will flinch from your hits while dazed
                return;
            }

            if (stateTimer <= 0f)
            {
                state = AIState.Recover;
                stateTimer = currentAttack.recovery;
            }
        }

        // ------------------------------------------------------------------ holding the hero

        float HoldDuration =>
            holdFromBehind ? 3.0f :
            Profile.grabOutcome == "bearhug" ? 2.6f :
            Profile.grabOutcome == "throw" ? 1.4f : 1.8f;

        void UpdateHoldingHero(PlayerFighter player)
        {
            if (player == null || !player.IsHeldBy(this))
            {
                StopHoldingHero();
                ResumeStance();
                return;
            }

            Vector3 holdPos = transform.position + transform.forward * (holdFromBehind ? 0.5f : 0.6f);
            Quaternion facing = Quaternion.LookRotation(holdFromBehind ? transform.forward : -transform.forward);
            player.HeldAt(holdPos, facing);

            if (Profile.grabOutcome == "bearhug" && !holdFromBehind)
            {
                squeezeTimer -= Time.deltaTime;
                if (squeezeTimer <= 0f)
                {
                    squeezeTimer = 0.4f;
                    player.ReceiveHit(new HitInfo { attacker = this, damage = 4f, direction = transform.forward, unblockable = true });
                    FX.HitSpark(player.Center, false);
                }
            }

            if (stateTimer > 0f) return;

            // He didn't break free in time.
            Vector3 dir = transform.forward;
            StopHoldingHero();
            player.ReleasedByEnemy();
            if (holdFromBehind)
            {
                player.ReceiveHit(new HitInfo { attacker = this, damage = 4f, direction = dir, knockback = 5f, unblockable = true });
            }
            else if (Profile.grabOutcome == "headbutt")
            {
                Visual.Play(FighterPose.Headbutt, 0.4f, 0.5f);
                player.ReceiveHit(new HitInfo { attacker = this, damage = 12f, direction = dir, knockback = 4f, heavy = true, unblockable = true });
            }
            else
            {
                // Bear hug or boss: throws him down the street.
                Visual.Play(FighterPose.Throw, 0.5f, 0.7f);
                player.ReceiveHit(new HitInfo { attacker = this, damage = 12f, direction = dir, knockback = 9f, launch = 5f, heavy = true, unblockable = true });
            }
            state = AIState.Recover;
            stateTimer = 0.6f;
        }

        void StopHoldingHero()
        {
            Visual.SetGrip(null, false);
            if (holdFromBehind) EnemyDirector.HeroHeldFromBehind = false;
            holdFromBehind = false;
        }

        /// <summary>The hero mashed free: elbow / shove, and the holder stumbles back.</summary>
        public void HeroBrokeFree(Vector3 pushDirection)
        {
            StopHoldingHero();
            ReleaseToken();
            state = AIState.Stagger;
            stateTimer = 1.0f;
            Visual.Play(FighterPose.StaggerBack, 1.0f, 1.0f);
            Motor.AddLunge(pushDirection, 1.3f);
            Shouts.Say(this, ShoutType.Hurt, 0.5f);
        }

        // ------------------------------------------------------------------ thrown bottles / bricks

        bool TryStartThrow(PlayerFighter player, float dist)
        {
            if (!Profile.throwsObjects || HeldWeapon != null || player == null || player.IsDowned) return false;
            if (Time.time < nextThrowTime || Time.time < globalNextThrow || dist < 4.5f || dist > 11f) return false;
            nextThrowTime = Time.time + Random.Range(10f, 16f);
            globalNextThrow = Time.time + Random.Range(4f, 7f);
            state = AIState.ThrowingObject;
            stateTimer = 0.7f;
            throwReleased = false;
            Visual.Play(FighterPose.ThrowObject, 0.7f, 0.7f);
            Shouts.Say(this, ShoutType.Throw, 0.6f);
            return true;
        }

        void ReleaseThrownObject(PlayerFighter player)
        {
            if (player == null) return;
            Vector3 hand = Visual.RightHand.position;
            var w = Weapon.Create(Random.value < 0.6f ? WeaponType.Bottle : WeaponType.Brick, hand);
            w.transform.position = hand;   // Create lays it on the ground; throw it from the hand
            Vector3 lead = player.Motor.MoveVelocity * 0.4f;
            w.Throw(this, player.Center + lead);
            if (GameManager.Instance != null)
                GameManager.Instance.AddPopup("INCOMING!", player.Center + Vector3.up * 1.2f, new Color(1f, 0.9f, 0.3f), 0.7f);
        }

        // ------------------------------------------------------------------ picking up weapons

        bool TryStartWeaponSeek()
        {
            if (!Profile.picksUpWeapons || HeldWeapon != null || Time.time < nextWeaponCheck) return false;
            nextWeaponCheck = Time.time + Random.Range(3f, 6f);
            if (Random.value > 0.4f) return false;
            var w = Weapon.FindNearbyOnGround(transform.position, 8f);
            if (w == null || reservedWeapons.Contains(w)) return false;
            reservedWeapons.Add(w);
            seekTarget = w;
            state = AIState.SeekWeapon;
            stateTimer = 5f;
            return true;
        }

        void UpdateSeekWeapon()
        {
            if (seekTarget == null || !seekTarget.OnGround || stateTimer <= 0f)
            {
                if (seekTarget != null) reservedWeapons.Remove(seekTarget);
                seekTarget = null;
                state = AIState.Wait;
                return;
            }
            Vector3 to = Flat(seekTarget.transform.position - transform.position);
            if (to.magnitude > 0.8f)
            {
                MoveTowards(to.normalized, 3f * MoodSpeed);
                return;
            }
            Visual.Play(FighterPose.PickUp, 0.6f, 0.6f);
            PickUp(seekTarget);
            reservedWeapons.Remove(seekTarget);
            seekTarget = null;
            Shouts.Say(this, ShoutType.PickUp, 0.7f);
            state = AIState.Wait;
        }

        public void PickUp(Weapon w)
        {
            if (w == null) return;
            HeldWeapon = w;
            w.Attach(this);
        }

        /// <summary>Weapon knocked out of his hand (perfect parry, knockdown, death).</summary>
        public void Disarm()
        {
            if (HeldWeapon == null) return;
            HeldWeapon.Drop();
            HeldWeapon = null;
            if (GameManager.Instance != null)
                GameManager.Instance.AddPopup("DISARMED!", Center + Vector3.up, new Color(0.6f, 0.9f, 1f), 0.8f);
        }

        // ------------------------------------------------------------------ surrender (last rowdy standing)

        /// <summary>Last rowdy standing loses his nerve: begs for mercy or runs for it.</summary>
        public void Surrender()
        {
            if (IsDead || surrendered || kind != EnemyKind.Rowdy) return;
            surrendered = true;
            ReleaseToken();
            EnemyDirector.Disengage(this);
            Disarm();
            if (state == AIState.HoldingHero) { var p = Player; StopHoldingHero(); if (p != null) p.ReleasedByEnemy(); }
            if (Random.value < 0.5f)
            {
                state = AIState.Begging;
                stateTimer = 10f;
                Visual.SetTelegraph(false);
                Visual.SetCombatStance(false);
                Visual.Play(FighterPose.Beg, 10f);
                Shouts.Say(this, ShoutType.Beg);
                if (GameManager.Instance != null)
                    GameManager.Instance.AddPopup("HE'S BEGGING", Center + Vector3.up * 1.6f, new Color(0.9f, 0.9f, 0.9f), 0.8f);
            }
            else
            {
                StartFleeing();
            }
        }

        /// <summary>The hero spared him.</summary>
        public void LetGo()
        {
            if (state != AIState.Begging) return;
            if (GameManager.Instance != null)
                GameManager.Instance.AddPopup("MERCY", Center + Vector3.up, new Color(0.7f, 1f, 0.7f), 1f);
            StartFleeing();
        }

        void StartFleeing()
        {
            surrendered = true;
            state = AIState.Fleeing;
            stateTimer = 12f;
            Visual.SetCombatStance(false);
            Shouts.Say(this, ShoutType.Flee, 0.8f);
        }

        void UpdateFleeing(Vector3 dirToPlayer)
        {
            // Run for the nearest end of the street, away from the hero.
            float endZ = transform.position.z >= 0f ? 31f : -31f;
            Vector3 toEnd = Flat(new Vector3(0f, 0f, endZ) - transform.position).normalized;
            if (Vector3.Dot(toEnd, dirToPlayer) > 0.6f) toEnd = -toEnd;   // hero is in the way: other end
            MoveTowards((toEnd + Separation() * 0.5f).normalized, Profile.runSpeed * 1.1f);
            if (Mathf.Abs(transform.position.z) > 30f || stateTimer <= 0f)
            {
                if (GameManager.Instance != null) GameManager.Instance.EnemyEscaped(this);
                else Destroy(gameObject);
            }
        }

        // ------------------------------------------------------------------ taking hits

        public override HitResult ReceiveHit(HitInfo hit)
        {
            if (IsDead) return HitResult.Ignored;
            if (state == AIState.Roaring) return HitResult.Ignored;
            if (IsDowned && !hit.hitsDowned) return HitResult.Ignored;
            lastHitTime = Time.time;

            // Begging: any blow finishes him.
            if (state == AIState.Begging && hit.attacker != null && hit.attacker.team == Team.Player)
            {
                if (GameManager.Instance != null)
                    GameManager.Instance.AddPopup("NO MERCY", Center + Vector3.up, new Color(1f, 0.4f, 0.3f), 1f);
                hit.damage = Health + 1f;
                ApplyDamage(hit.damage, hit, HitResult.Hit);
                return HitResult.Hit;
            }

            // Bigger rowdies sometimes put their guard up while circling.
            bool canBlock = !isDummy && (state == AIState.Engage || state == AIState.Wait || state == AIState.Approach) && !hit.heavy && !hit.unblockable;
            if (canBlock && Random.value < Profile.blockChance)
            {
                Motor.FaceDirection(-hit.direction, true);
                Visual.Play(FighterPose.Block, 0.4f);
                GameAudio.Play(Sfx.Block, Center + Vector3.up * 0.35f, 0.8f);
                Motor.AddImpulse(hit.direction * hit.knockback * 0.5f);
                if (GameManager.Instance != null)
                    GameManager.Instance.AddPopup("BLOCKED", Center + Vector3.up * 0.8f, new Color(0.7f, 0.85f, 1f), 0.7f);
                if (Profile.countersBlocks) TryRiposte();
                return HitResult.Blocked;
            }

            Visual.SetTelegraph(false);
            Visual.Flash();
            if (hit.attacker != null && hit.attacker.team == Team.Player && !surrendered && !isDummy)
                EnemyDirector.ForceEngage(this, hit.attacker.transform.position);
            ApplyDamage(hit.damage, hit, HitResult.Hit);
            if (IsDead) return HitResult.Hit;

            if (Profile.hasPhaseTwo && !phaseTwo && !isDummy && HealthFraction <= 0.5f)
            {
                EnterPhaseTwo();
                return HitResult.Hit;
            }

            if (state == AIState.Grabbed)
            {
                Visual.Flinch(0.8f);
                return HitResult.Hit;
            }

            // A clean charged haymaker: knocked silly on his feet.
            if (hit.dazes && hit.launch <= 0f && state != AIState.Down)
            {
                Motor.FaceDirection(-hit.direction, true);
                Motor.AddImpulse(hit.direction * hit.knockback);
                Daze(2.8f);
                return HitResult.Hit;
            }

            // Already dazed: smaller hits keep him reeling (big ones still put him down).
            if (state == AIState.Dazed && hit.launch <= 0f && !(hit.heavy && hit.knockback >= 8f))
            {
                Visual.Flinch(1f);
                Visual.Play(FighterPose.Flinch, 0.3f, 0.35f);
                Motor.AddImpulse(hit.direction * hit.knockback * 0.5f);
                stateTimer = Mathf.Max(0.5f, stateTimer - 0.25f);
                return HitResult.Hit;
            }

            if (state == AIState.HoldingHero)
            {
                var p = Player;
                StopHoldingHero();
                if (p != null) p.ReleasedByEnemy();
            }

            Motor.FaceDirection(-hit.direction, true);

            if (hit.launch > 0f || (hit.heavy && hit.knockback >= 8f))
            {
                KnockDown(hit.direction * hit.knockback + Vector3.up * hit.launch, 1.6f);
                // Sent flying by the hero: a wall in the way bounces him back, dazed.
                if (hit.attacker is PlayerFighter && hit.knockback >= 6f && MoveUnlocks.Has(MoveId.WallBounce))
                    wallBounceTimer = 0.7f;
                return HitResult.Hit;
            }

            // Worn down: a heavy blow leaves him dazed once (wide open for a wrestling finisher).
            if (hit.heavy && !dazedOnce && !IsBoss && state != AIState.Down && HealthFraction < 0.35f && hit.attacker is PlayerFighter)
            {
                dazedOnce = true;
                Motor.FaceDirection(-hit.direction, true);
                Motor.AddImpulse(hit.direction * hit.knockback * 0.5f);
                Daze(2.6f);
                return HitResult.Hit;
            }

            if (state == AIState.Down)
            {
                // Stomped while lying down
                stateTimer = Mathf.Max(stateTimer, 0.8f);
                if (Visual.IsRagdoll) Visual.StartRagdoll(hit.direction * 2f + Vector3.down * 2f);
                return HitResult.Hit;
            }

            bool armoured = Profile.poise > 0 && !hit.heavy && poiseHits < Profile.poise
                            && state != AIState.Fleeing;
            if (armoured)
            {
                poiseHits++;
                Visual.Flinch(0.4f);
                Motor.AddImpulse(hit.direction * hit.knockback * 0.3f);
                return HitResult.Hit;
            }

            poiseHits = 0;
            ReleaseToken();
            currentCombo = null;
            bool wasFleeing = state == AIState.Fleeing;
            state = AIState.Stagger;
            stateTimer = hit.heavy ? 0.65f : 0.42f;
            Visual.Play(FighterPose.Flinch, stateTimer);
            Visual.Flinch(1f);
            Motor.AddImpulse(hit.direction * hit.knockback);
            if (wasFleeing) surrendered = true;
            return HitResult.Hit;
        }

        /// <summary>Boss: blocks two of your punches in a row and snaps back with a quick counter.</summary>
        void TryRiposte()
        {
            if (Time.time - lastBlockTime > 2f) blockStreak = 0;
            lastBlockTime = Time.time;
            blockStreak++;
            if (blockStreak < 2 || Player == null) return;
            blockStreak = 0;
            currentCombo = null;
            feinting = false;
            currentAttack = Profile.attacks[0].Clone();
            currentAttack.startup = 0.24f;
            state = AIState.Windup;
            stateTimer = currentAttack.startup;
            strikeAnimStarted = false;
            strikeLead = 0f;
            Visual.Play(FighterPose.Windup, currentAttack.startup, currentAttack.startup);
            Visual.SetTelegraph(TelegraphColor.Red);
            if (GameManager.Instance != null)
                GameManager.Instance.AddPopup("HE READ YOU!", Center + Vector3.up, new Color(1f, 0.5f, 0.3f), 0.9f);
        }

        void EnterPhaseTwo()
        {
            phaseTwo = true;
            ReleaseToken();
            currentCombo = null;
            Visual.SetTelegraph(false);
            state = AIState.Roaring;
            stateTimer = 1.4f;
            Visual.Play(FighterPose.Roar, 1.4f, 1.4f);
            Shouts.Say(this, ShoutType.BossRage);
            var pipe = Weapon.Create(WeaponType.IronPipe, transform.position);
            PickUp(pipe);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ShowBanner("KUMARAN ANNA IS FURIOUS", "He's grabbed an iron pipe and called his boys!", 2.5f);
                GameManager.Instance.Shake(0.4f, 0.5f);
                GameManager.Instance.SpawnReinforcements(EnemyKind.Rowdy, 2);
            }
        }

        void KnockDown(Vector3 impulse, float duration)
        {
            ReleaseToken();
            currentCombo = null;
            poiseHits = 0;
            if (state == AIState.HoldingHero) { var p = Player; StopHoldingHero(); if (p != null) p.ReleasedByEnemy(); }
            state = AIState.Down;
            stateTimer = duration;
            if (Visual.CanRagdoll)
            {
                // Real model: go limp and fall under physics.
                Motor.SetFrozen(true);
                Visual.StartRagdoll(impulse);
                stateTimer = Mathf.Max(duration, 1.3f);
            }
            else
            {
                Visual.ClearCarry();
                Visual.Play(FighterPose.Knockdown, duration);
                Visual.SetLying(true);
                Motor.AddImpulse(impulse);
                Vector3 flat = new Vector3(impulse.x, 0f, impulse.z);
                if (flat.magnitude >= 9f) Visual.Tumble(flat, 0.65f);   // hit hard enough to roll over
            }
            if (HeldWeapon != null && Random.value < 0.5f) Disarm();
        }

        protected override void OnDeath(HitInfo killingBlow)
        {
            ReleaseToken();
            EnemyDirector.Disengage(this);   // frees the slot so a waiting rowdy steps in
            if (state == AIState.Grabbed) Motor.SetFrozen(false);
            if (state == AIState.HoldingHero) { var p = Player; StopHoldingHero(); if (p != null) p.ReleasedByEnemy(); }
            if (seekTarget != null) reservedWeapons.Remove(seekTarget);
            Disarm();
            state = AIState.Dead;
            Visual.SetTelegraph(false);
            Visual.SetCombatStance(false);
            Vector3 deathPush = killingBlow.direction * Mathf.Max(killingBlow.knockback, 4f) + Vector3.up * 3f;
            if (Visual.CanRagdoll)
            {
                Motor.SetFrozen(true);
                Visual.StartRagdoll(deathPush);
            }
            else
            {
                Visual.ClearCarry();
                Visual.SetLying(true);
                Visual.Play(FighterPose.Dead, 999f);
                Motor.AddImpulse(deathPush);
            }

            if (GameManager.Instance != null && killingBlow.attacker is PlayerFighter player)
                player.OnEnemyDefeated(this);

            Invoke(nameof(DisableBody), 1.5f);
            Destroy(gameObject, 6f);
        }

        void DisableBody()
        {
            // Let the player walk over the body.
            Motor.SetFrozen(true);
        }

        // ------------------------------------------------------------------ grabs, throws and shoves

        public enum LaunchKind { Throw, Shove, Bowl }

        float ownReactionUntil;   // kneeling / doubled over: holds its own pose while dazed
        float dazeOnRelease;      // ear clap: when the hero lets go he reels about instead of fighting back

        /// <summary>Low kick to the knee: he drops to one knee, open for a knee to the face.</summary>
        public void KneelDown(float seconds)
        {
            if (IsDead || IsDowned || state == AIState.Grabbed) return;
            Daze(seconds);
            if (state != AIState.Dazed) return;
            ownReactionUntil = Time.time + seconds;
            Visual.Play(FighterPose.Kneel, seconds);
        }

        /// <summary>Liver shot: folds over holding his side, can't fight back for a few seconds.</summary>
        public void DoubleOver(float seconds)
        {
            if (IsDead || IsDowned || state == AIState.Grabbed) return;
            Daze(seconds);
            if (state != AIState.Dazed) return;
            ownReactionUntil = Time.time + Mathf.Min(seconds, 1.6f);
            Visual.Play(FighterPose.GutHit, Mathf.Min(seconds, 1.6f));
        }

        /// <summary>Arm twisted and snapped: drops his weapon and can't attack for a while.</summary>
        public void InjureArm(float seconds)
        {
            Disarm();
            nextAttackTime = Mathf.Max(nextAttackTime, Time.time + seconds);
        }

        /// <summary>Ear clap: when the hero lets go (without knocking him down) he reels, dazed.</summary>
        public void DazeWhenReleased(float seconds) => dazeOnRelease = seconds;

        /// <summary>Thrown off the jetty: a splash and he's gone (the practice dummy is put back on his mark).</summary>
        public void ThrownIntoSea(PlayerFighter by)
        {
            if (isDummy)
            {
                if (state == AIState.Grabbed) ReleaseFromGrab(Vector3.down * 2f, knockDown: true);
                if (GameManager.Instance != null) GameManager.Instance.RespawnDummySoon(this, 1.2f);
                return;
            }
            if (state == AIState.Grabbed) Motor.SetFrozen(false);
            ReceiveHit(new HitInfo { attacker = by, damage = Health + 1f, direction = Vector3.down, unblockable = true, hitsDowned = true });
        }

        /// <summary>Held by the collar (front) or from behind. He struggles in the hero's grip.</summary>
        public void EnterGrabbed(bool fromBehind)
        {
            ReleaseToken();
            Visual.SetTelegraph(false);
            // Fight-stance footwork (so he can stumble backwards when dragged), but no guard up: the
            // held / struggling pose takes the arms.
            Visual.SetCombatStance(true);
            Visual.Play(FighterPose.Flinch, 0.4f);
            state = AIState.Grabbed;
            GrabbedFromBehind = fromBehind;
            Motor.SetFrozen(true);
        }

        public bool GrabbedFromBehind { get; private set; }

        public void HoldAt(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>Lets go of the enemy, optionally knocking it down (finishers).</summary>
        public void ReleaseFromGrab(Vector3 push, bool knockDown)
        {
            if (state != AIState.Grabbed) return;
            Motor.SetFrozen(false);
            if (knockDown)
            {
                KnockDown(push, 1.8f);
            }
            else
            {
                Visual.ClearCarry();
                state = AIState.Stagger;
                stateTimer = 0.5f;
                Visual.Play(FighterPose.StaggerBack, 0.5f, 0.6f);
                Motor.AddImpulse(push);
                if (dazeOnRelease > 0f)
                {
                    float s = dazeOnRelease;
                    dazeOnRelease = 0f;
                    Daze(s);
                }
            }
            dazeOnRelease = 0f;
        }

        /// <summary>
        /// Thrown (flies, tumbles head over heels, knocks others down) or shoved (stumbles backwards;
        /// a hard shove takes him off his feet). Either way he smashes props he crashes into.
        /// </summary>
        public void Launch(Fighter by, Vector3 direction, LaunchKind kind)
        {
            direction.y = 0f;
            direction.Normalize();
            if (state == AIState.Grabbed) Motor.SetFrozen(false);
            Visual.ClearCarry();
            Motor.FaceDirection(-direction, true);
            thrower = by;
            launchKind = kind;
            thrownHits.Clear();

            if (kind == LaunchKind.Bowl)
            {
                // Hurled low along the road: he skids and rolls into everyone in the way.
                KnockDown(direction * 11f + Vector3.up * 1.6f, 2.0f);
                if (!Visual.CanRagdoll) Visual.Tumble(direction, 0.9f);
                thrownTimer = 1.4f;
            }
            else if (kind == LaunchKind.Throw)
            {
                // A real throw: a couple of metres through the air, then he hits the street and rolls.
                KnockDown(direction * 8f + Vector3.up * 4f, 2.0f);
                if (!Visual.CanRagdoll) Visual.Tumble(direction, 0.75f);
                thrownTimer = 1.0f;
            }
            else
            {
                // Shove: stagger backwards fast; he loses his footing and falls after a few steps.
                ReleaseToken();
                state = AIState.Stagger;
                stateTimer = 0.9f;
                Visual.Play(FighterPose.StaggerBack, 0.9f, 0.9f);
                Motor.AddLunge(direction, 3.2f);
                thrownTimer = 0.7f;
                fallAfterShove = 0.55f;
            }
        }

        LaunchKind launchKind;
        float fallAfterShove;

        /// <summary>How fast the body is really moving (the ragdoll's, while limp).</summary>
        Vector3 BodyVelocity => Visual.IsRagdoll ? Visual.RagdollVelocity : Motor.Velocity;

        /// <summary>The hero deflected this rowdy's attack. Perfect parry: knocked well off balance.</summary>
        public void Parried(bool perfect, Vector3 pushDirection)
        {
            Visual.SetTelegraph(false);
            ReleaseToken();
            currentCombo = null;
            feinting = false;
            if (perfect) Disarm();   // a perfect parry knocks the weapon out of his hand
            state = AIState.Stagger;
            stateTimer = perfect ? 1.0f : 0.75f;
            Visual.Play(FighterPose.StaggerBack, stateTimer, stateTimer);
            Motor.ClearImpulse();
            Motor.AddLunge(pushDirection, perfect ? 1.4f : 0.8f);
            Motor.FaceDirection(-pushDirection, true);
        }

        /// <summary>His punch bounced off the hero's guard: arm knocked back, half a step back.</summary>
        public void Recoil(Vector3 pushDirection)
        {
            if (IsDead || IsDowned || state == AIState.Grabbed) return;
            ReleaseToken();
            Visual.SetTelegraph(false);
            state = AIState.Stagger;
            stateTimer = 0.45f;
            Visual.Play(FighterPose.StaggerBack, 0.45f, 0.5f);
            Motor.AddLunge(pushDirection, 0.5f);
        }

        /// <summary>Knocked silly: stands swaying, can't attack, and can be grabbed (even the Thadiyan).</summary>
        public void Daze(float seconds)
        {
            if (IsDead || IsDowned || state == AIState.Grabbed || state == AIState.Begging || state == AIState.Fleeing) return;
            if (state == AIState.HoldingHero) { var p = Player; StopHoldingHero(); if (p != null) p.ReleasedByEnemy(); }
            ReleaseToken();
            currentCombo = null;
            feinting = false;
            poiseHits = 0;
            Visual.SetTelegraph(false);
            Visual.SetLying(false);
            Visual.StopTumble();
            state = AIState.Dazed;
            stateTimer = IsBoss ? seconds * 0.6f : seconds;
            dazeWobble = 0f;
            Visual.Play(FighterPose.StaggerBack, 0.6f, 0.6f);
            if (GameManager.Instance != null)
                GameManager.Instance.AddPopup("DAZED!", Center + Vector3.up * 1.1f, new Color(1f, 0.95f, 0.4f), 1f);
        }

        /// <summary>Keeps a downed rowdy on the ground a little longer (elbow drop, leg drop).</summary>
        public void PinDown(float seconds)
        {
            if (state == AIState.Down) stateTimer = Mathf.Max(stateTimer, seconds);
        }

        /// <summary>Knocked flying into a wall: he bounces off it and staggers back, dazed, into the hero's reach.</summary>
        void CheckWallBounce()
        {
            if (state != AIState.Down) { wallBounceTimer = 0f; return; }
            if (Visual.IsRagdoll) { CheckWallSplat(); return; }
            Vector3 v = Flat(Motor.Velocity);
            if (v.magnitude < 3f) return;
            Vector3 dir = v.normalized;
            if (!Physics.SphereCast(Center, 0.3f, dir, out var hit, radius + 0.35f, CombatUtil.WorldMask, QueryTriggerInteraction.Ignore))
                return;
            if (hit.collider.GetComponentInParent<Fighter>() != null) return;
            if (hit.collider.GetComponentInParent<FinisherSpot>() != null) return;   // props break instead
            if (Mathf.Abs(hit.normal.y) > 0.5f || hit.collider.bounds.size.y < 1.2f) return;

            wallBounceTimer = 0f;
            thrownTimer = 0f;
            Motor.ClearImpulse();
            Vector3 back = Flat(Vector3.Reflect(v, hit.normal)).normalized;
            ApplyDamage(6f, new HitInfo { attacker = Player, damage = 6f, direction = -dir, heavy = true }, HitResult.Hit);
            FX.Dust(hit.point + Vector3.up * 0.2f, 14);
            FX.HitSpark(hit.point, true);
            if (IsDead) return;
            Daze(2.4f);
            Motor.FaceDirection(-back, true);
            Motor.AddLunge(back, 1.2f);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddPopup("WALL BOUNCE!", Center + Vector3.up, new Color(1f, 0.8f, 0.2f), 1.2f);
                GameManager.Instance.Shake(0.3f);
                GameManager.Instance.HitStop(0.08f);
            }
            if (Player != null)
            {
                Player.AddGethu(8f);
                Player.NotifyMoveLanded("Wall Bounce");
            }
        }

        /// <summary>Ragdolled into a wall: physics does the slump; this adds the damage, sound and reward.</summary>
        void CheckWallSplat()
        {
            Vector3 v = Flat(Visual.RagdollVelocity);
            if (v.magnitude < 2.5f) return;
            Vector3 from = Visual.RagdollCenter;
            if (!Physics.SphereCast(from, 0.25f, v.normalized, out var hit, 0.6f, CombatUtil.WorldMask, QueryTriggerInteraction.Ignore))
                return;
            if (hit.collider.GetComponentInParent<FinisherSpot>() != null) return;   // props break instead
            if (Mathf.Abs(hit.normal.y) > 0.5f || hit.collider.bounds.size.y < 1.2f) return;

            wallBounceTimer = 0f;
            ApplyDamage(6f, new HitInfo { attacker = Player, damage = 6f, direction = v.normalized, heavy = true }, HitResult.Hit);
            FX.Dust(hit.point, 14);
            FX.HitSpark(hit.point, true);
            stateTimer = Mathf.Max(stateTimer, 1.6f);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddPopup("WALL SPLAT!", hit.point + Vector3.up * 0.8f, new Color(1f, 0.8f, 0.2f), 1.2f);
                GameManager.Instance.Shake(0.3f);
                GameManager.Instance.HitStop(0.08f);
            }
            if (Player != null)
            {
                Player.AddGethu(8f);
                Player.NotifyMoveLanded("Wall Bounce");
            }
        }

        /// <summary>Seconds until this rowdy's telegraphed hit lands (huge when not attacking).</summary>
        public float TimeToImpact => state == AIState.Windup ? Mathf.Max(0f, stateTimer) : float.MaxValue;

        void HitOthersWhileThrown()
        {
            if (thrower == null) return;
            bool shove = launchKind == LaunchKind.Shove;

            if (shove && fallAfterShove > 0f)
            {
                fallAfterShove -= Time.deltaTime;
                if (fallAfterShove <= 0f && state == AIState.Stagger && Motor.Velocity.magnitude > 2.5f)
                {
                    // Too fast to keep his feet: goes down and rolls backwards.
                    Vector3 v = Flat(Motor.Velocity);
                    KnockDown(v * 0.6f + Vector3.up * 2f, 1.6f);
                    Visual.Tumble(v, 0.6f);
                }
            }

            // Crashing into a tea kadai / crates / auto: smash it, no Gethu needed.
            var spot = FinisherSpot.FindHit(transform.position, radius + 0.6f);
            Vector3 flyDir = BodyVelocity.sqrMagnitude > 0.1f ? Flat(BodyVelocity).normalized : -transform.forward;
            if (spot != null && Vector3.Dot(flyDir, Flat(spot.transform.position - transform.position)) > 0f)
            {
                spot.Break(flyDir);
                thrownTimer = 0f;
                ReceiveHit(new HitInfo { attacker = thrower, damage = spot.damage * 0.5f, direction = flyDir, knockback = 2f, launch = 2.5f, heavy = true, unblockable = true, hitsDowned = true });
                Visual.Tumble(flyDir, 0.6f);
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.AddPopup(spot.finisherName, Center + Vector3.up, new Color(1f, 0.75f, 0.15f), 1.3f);
                    GameManager.Instance.Shake(0.45f, 0.3f);
                    GameManager.Instance.HitStop(0.1f);
                    GameManager.Instance.SlowMo(0.35f, 0.5f);
                }
                if (thrower is PlayerFighter p) p.AddGethu(12f);
                return;
            }

            // Bowling into other rowdies.
            foreach (var f in All.ToArray())
            {
                if (f == this || f.team != team || f.IsDead || thrownHits.Contains(f)) continue;
                Vector3 d = Flat(f.transform.position - transform.position);
                if (d.magnitude > f.radius + radius + 0.4f) continue;
                thrownHits.Add(f);
                var dir = BodyVelocity.sqrMagnitude > 0.1f ? Flat(BodyVelocity).normalized : d.normalized;
                f.ReceiveHit(new HitInfo
                {
                    attacker = thrower,
                    damage = shove ? 6f : 15f,
                    direction = dir,
                    knockback = shove ? 4f : 9f,
                    launch = shove ? 0f : 4f,
                    heavy = true,
                    unblockable = true,
                });
                if (!shove && f is EnemyFighter other && !other.IsDead) other.Visual.Tumble(dir, 0.7f);
                FX.HitSpark(f.Center, true);
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.AddPopup(shove ? "KNOCKED INTO!" : "STRIKE!", f.Center + Vector3.up, new Color(1f, 0.8f, 0.2f));
                    GameManager.Instance.Shake(0.25f);
                }
            }
        }
    }
}

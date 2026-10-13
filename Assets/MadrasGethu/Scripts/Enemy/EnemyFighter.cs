using System.Collections.Generic;
using UnityEngine;

namespace MadrasGethu
{
    /// <summary>
    /// Street rowdy AI, Assassin's Creed / Sleeping Dogs style.
    /// Rowdies walk in, then either WAIT in a wide ring (fighting stance, pacing, watching, the odd taunt)
    /// or are ENGAGED (close to the hero, allowed to attack). EnemyDirector decides who is engaged and
    /// lets only one attack at a time. Every attack is telegraphed with a red flash: the counter window.
    /// </summary>
    public class EnemyFighter : Fighter
    {
        enum AIState { Approach, Wait, Engage, CloseIn, Windup, Recover, Stagger, Down, GetUp, Grabbed, Dead }

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

        // Being thrown into other enemies
        Fighter thrower;
        float thrownTimer;
        readonly HashSet<Fighter> thrownHits = new HashSet<Fighter>();

        public bool IsTelegraphing => state == AIState.Windup;
        public bool IsGrabbed => state == AIState.Grabbed;
        public override bool IsDowned => state == AIState.Down || state == AIState.GetUp;
        public override bool CanBeTargeted => !IsDead && state != AIState.Grabbed;
        public bool IsBoss => kind == EnemyKind.Boss;
        bool Waiting => state == AIState.Wait || state == AIState.Approach;

        public bool CanBeGrabbed =>
            !IsDead && !IsDowned && state != AIState.Grabbed &&
            (Profile.grabbable || state == AIState.Stagger);

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

            if (!playerAlive && state != AIState.Down && state != AIState.GetUp && state != AIState.Grabbed)
            {
                // The hero is down for good: stand and gloat.
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
                    Visual.SetCombatStance(dist < 9f);
                    MoveTowards(dirToPlayer, Profile.runSpeed);
                    if (dist <= waitRadius + 1f) state = AIState.Wait;
                    break;

                case AIState.Wait:
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
                    HoldRing(dirToPlayer, dist, waitRadius, 1.0f, allowTaunt: true);
                    break;

                case AIState.Engage:
                    if (!EnemyDirector.IsEngaged(this))
                    {
                        state = AIState.Wait;   // swapped out: step back and watch
                        break;
                    }
                    HoldRing(dirToPlayer, dist, engageRadius, Profile.circleSpeed, allowTaunt: false);
                    if (Time.time >= nextAttackTime && !player.IsDowned && dist < engageRadius + 1.5f
                        && EnemyDirector.RequestAttack(this))
                    {
                        hasToken = true;
                        currentAttack = Profile.attacks[Random.Range(0, Profile.attacks.Count)];
                        state = AIState.CloseIn;
                        stateTimer = 2.5f;
                    }
                    break;

                case AIState.CloseIn:
                    MoveTowards(dirToPlayer, Mathf.Min(Profile.runSpeed, 3.2f));
                    if (dist <= currentAttack.range * 0.85f + player.radius)
                        BeginWindup();
                    else if (stateTimer <= 0f || player.IsDowned)
                        EndAttack();
                    break;

                case AIState.Windup:
                    Motor.FaceDirection(dirToPlayer);
                    if (stateTimer <= 0f) ReleaseAttack(dist);
                    break;

                case AIState.Recover:
                    if (stateTimer <= 0f) EndAttack();
                    break;

                case AIState.Stagger:
                    if (stateTimer <= 0f) ResumeStance();
                    break;

                case AIState.Down:
                    if (stateTimer <= 0f && Motor.IsGrounded)
                    {
                        state = AIState.GetUp;
                        stateTimer = 0.8f;
                        Visual.SetLying(false);
                        Visual.Play(FighterPose.GetUp, 0.8f);
                    }
                    break;

                case AIState.GetUp:
                    if (stateTimer <= 0f)
                    {
                        ResumeStance();
                        nextAttackTime = Mathf.Max(nextAttackTime, Time.time + Random.Range(1f, 2f));
                    }
                    break;

                case AIState.Grabbed:
                    break;
            }
        }

        /// <summary>Back to fighting stance: close in if this rowdy holds a combat slot, otherwise wait.</summary>
        void ResumeStance()
        {
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
                MoveTowards(dirToPlayer, Mathf.Min(Profile.runSpeed, 3.5f));
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

        // ------------------------------------------------------------------ attacking

        void BeginWindup()
        {
            state = AIState.Windup;
            stateTimer = currentAttack.startup;
            Visual.Play(FighterPose.Windup, currentAttack.startup, currentAttack.startup);
            Visual.SetTelegraph(true);
            if (currentAttack.unblockable && GameManager.Instance != null)
                GameManager.Instance.AddPopup("UNBLOCKABLE!", Center + Vector3.up, new Color(1f, 0.4f, 0.1f), 0.8f);
        }

        void ReleaseAttack(float dist)
        {
            Visual.SetTelegraph(false);
            Visual.Play(currentAttack.pose, 0.3f, currentAttack.recovery + 0.3f);
            float lunge = Mathf.Clamp(dist - currentAttack.range * 0.6f, 0f, currentAttack.lunge);
            Motor.AddLunge(transform.forward, lunge);
            var result = CombatUtil.Strike(this, currentAttack);
            if (result.hits > 0 && GameManager.Instance != null)
                GameManager.Instance.Shake(currentAttack.heavy ? 0.25f : 0.12f);
            state = AIState.Recover;
            stateTimer = currentAttack.recovery;
        }

        void EndAttack()
        {
            ReleaseToken();
            ResumeStance();
            nextAttackTime = Time.time + Random.Range(Profile.attackCooldown.x, Profile.attackCooldown.y);
        }

        void ReleaseToken()
        {
            if (!hasToken) return;
            hasToken = false;
            EnemyDirector.Release(this);
        }

        /// <summary>Called when the player counters this enemy's telegraphed attack.</summary>
        public void Countered()
        {
            Visual.SetTelegraph(false);
            ReleaseToken();
            state = AIState.Stagger;
            stateTimer = 0.8f;
            Visual.Play(FighterPose.Flinch, 0.8f);
        }

        // ------------------------------------------------------------------ taking hits

        public override HitResult ReceiveHit(HitInfo hit)
        {
            if (IsDead) return HitResult.Ignored;
            if (IsDowned && !hit.hitsDowned) return HitResult.Ignored;

            // Bigger rowdies sometimes put their guard up while circling.
            bool canBlock = (state == AIState.Engage || state == AIState.Wait || state == AIState.Approach) && !hit.heavy && !hit.unblockable;
            if (canBlock && Random.value < Profile.blockChance)
            {
                Motor.FaceDirection(-hit.direction, true);
                Visual.Play(FighterPose.Block, 0.4f);
                Motor.AddImpulse(hit.direction * hit.knockback * 0.5f);
                if (GameManager.Instance != null)
                    GameManager.Instance.AddPopup("BLOCKED", Center + Vector3.up * 0.8f, new Color(0.7f, 0.85f, 1f), 0.7f);
                return HitResult.Blocked;
            }

            Visual.SetTelegraph(false);
            Visual.Flash();
            if (hit.attacker != null && hit.attacker.team == Team.Player)
                EnemyDirector.ForceEngage(this, hit.attacker.transform.position);
            ApplyDamage(hit.damage, hit, HitResult.Hit);
            if (IsDead) return HitResult.Hit;

            if (state == AIState.Grabbed)
            {
                Visual.Flinch(0.8f);
                return HitResult.Hit;
            }

            Motor.FaceDirection(-hit.direction, true);

            if (hit.launch > 0f || (hit.heavy && hit.knockback >= 8f))
            {
                KnockDown(hit.direction * hit.knockback + Vector3.up * hit.launch, 1.6f);
                return HitResult.Hit;
            }

            if (state == AIState.Down)
            {
                // Stomped while lying down
                stateTimer = Mathf.Max(stateTimer, 0.8f);
                return HitResult.Hit;
            }

            bool armoured = Profile.poise > 0 && !hit.heavy && poiseHits < Profile.poise;
            if (armoured)
            {
                poiseHits++;
                Visual.Flinch(0.4f);
                Motor.AddImpulse(hit.direction * hit.knockback * 0.3f);
                return HitResult.Hit;
            }

            poiseHits = 0;
            ReleaseToken();
            state = AIState.Stagger;
            stateTimer = hit.heavy ? 0.65f : 0.42f;
            Visual.Play(FighterPose.Flinch, stateTimer);
            Visual.Flinch(1f);
            Motor.AddImpulse(hit.direction * hit.knockback);
            return HitResult.Hit;
        }

        void KnockDown(Vector3 impulse, float duration)
        {
            ReleaseToken();
            poiseHits = 0;
            state = AIState.Down;
            stateTimer = duration;
            Visual.Play(FighterPose.Knockdown, duration);
            Visual.SetLying(true);
            Motor.AddImpulse(impulse);
        }

        protected override void OnDeath(HitInfo killingBlow)
        {
            ReleaseToken();
            EnemyDirector.Disengage(this);   // frees the slot so a waiting rowdy steps in
            if (state == AIState.Grabbed) Motor.SetFrozen(false);
            state = AIState.Dead;
            Visual.SetTelegraph(false);
            Visual.SetCombatStance(false);
                        Visual.SetLying(true);
            Visual.Play(FighterPose.Dead, 999f);
            Motor.AddImpulse(killingBlow.direction * Mathf.Max(killingBlow.knockback, 4f) + Vector3.up * 3f);

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

        // ------------------------------------------------------------------ grabs and throws

        public void EnterGrabbed()
        {
            ReleaseToken();
            Visual.SetTelegraph(false);
            Visual.SetCombatStance(false);
            Visual.Play(FighterPose.Flinch, 999f);
            state = AIState.Grabbed;
            Motor.SetFrozen(true);
        }

        public void HoldAt(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>Lets go of the enemy, optionally knocking it down (throws, finishers).</summary>
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
                state = AIState.Stagger;
                stateTimer = 0.5f;
                Visual.Play(FighterPose.Flinch, 0.5f);
                Motor.AddImpulse(push);
            }
        }

        public void Throw(Fighter by, Vector3 direction)
        {
            ReleaseFromGrab(direction * 13f + Vector3.up * 5f, true);
            thrower = by;
            thrownTimer = 0.9f;
            thrownHits.Clear();
        }

        void HitOthersWhileThrown()
        {
            if (thrower == null) return;

            // Thrown into a tea kadai / crates / auto: smash it, no Gethu needed.
            var spot = FinisherSpot.FindHit(transform.position, radius + 0.6f);
            Vector3 flyDir = Motor.Velocity.sqrMagnitude > 0.1f ? Flat(Motor.Velocity).normalized : transform.forward;
            if (spot != null && Vector3.Dot(flyDir, Flat(spot.transform.position - transform.position)) > 0f)
            {
                Vector3 dir = flyDir;
                spot.Break(dir);
                thrownTimer = 0f;
                ReceiveHit(new HitInfo { attacker = thrower, damage = spot.damage * 0.5f, direction = dir, knockback = 2f, launch = 2f, heavy = true, unblockable = true, hitsDowned = true });
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

            foreach (var f in All.ToArray())
            {
                if (f == this || f.team != team || f.IsDead || thrownHits.Contains(f)) continue;
                Vector3 d = Flat(f.transform.position - transform.position);
                if (d.magnitude > f.radius + radius + 0.4f) continue;
                thrownHits.Add(f);
                var dir = Motor.Velocity.sqrMagnitude > 0.1f ? Flat(Motor.Velocity).normalized : d.normalized;
                f.ReceiveHit(new HitInfo
                {
                    attacker = thrower,
                    damage = 15f,
                    direction = dir,
                    knockback = 7f,
                    launch = 4f,
                    heavy = true,
                    unblockable = true,
                });
                FX.HitSpark(f.Center, true);
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.AddPopup("STRIKE!", f.Center + Vector3.up, new Color(1f, 0.8f, 0.2f));
                    GameManager.Instance.Shake(0.25f);
                }
            }
        }
    }
}

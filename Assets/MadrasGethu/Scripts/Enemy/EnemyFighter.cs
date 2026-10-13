using System.Collections.Generic;
using UnityEngine;

namespace MadrasGethu
{
    /// <summary>
    /// Street rowdy AI. Walks in, circles the player with the rest of the gang, and takes turns
    /// attacking (via EnemyDirector). Every attack is telegraphed with a red flash: that is the
    /// player's window to counter.
    /// </summary>
    public class EnemyFighter : Fighter
    {
        enum AIState { Approach, Circle, CloseIn, Windup, Recover, Stagger, Down, GetUp, Grabbed, Dead }

        public EnemyKind kind;
        public EnemyProfile Profile { get; private set; }

        AIState state = AIState.Approach;
        float stateTimer;
        AttackData currentAttack;
        float strafeDirection = 1f;
        float nextStrafeFlip;
        float nextAttackTime;
        float circleRadius;
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
            circleRadius = Random.Range(2.8f, 4.2f);
            strafeDirection = Random.value < 0.5f ? -1f : 1f;
            nextAttackTime = Time.time + Random.Range(1f, 2.5f);
        }

        protected override void Awake()
        {
            base.Awake();
            if (Profile == null) Configure(kind);
        }

        PlayerFighter Player => GameManager.Instance != null ? GameManager.Instance.Player : null;

        void Update()
        {
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
                // Player is down for good: gloat in place.
                ReleaseToken();
                Visual.SetTelegraph(false);
                Visual.SetCombatStance(false);
                Visual.SetLocomotion(0f);
                return;
            }

            Vector3 toPlayer = playerAlive ? Flat(player.transform.position - transform.position) : Vector3.zero;
            float dist = toPlayer.magnitude;
            Vector3 dirToPlayer = dist > 0.01f ? toPlayer / dist : transform.forward;

            switch (state)
            {
                case AIState.Approach:
                    Visual.SetCombatStance(dist < 6f);
                    MoveTowards(dirToPlayer, Profile.runSpeed);
                    if (dist <= circleRadius + 0.3f) state = AIState.Circle;
                    break;

                case AIState.Circle:
                    Circle(dirToPlayer, dist);
                    if (Time.time >= nextAttackTime && EnemyDirector.RequestAttack(this))
                    {
                        hasToken = true;
                        currentAttack = Profile.attacks[Random.Range(0, Profile.attacks.Count)];
                        state = AIState.CloseIn;
                        stateTimer = 2.5f;
                    }
                    break;

                case AIState.CloseIn:
                    MoveTowards(dirToPlayer, Profile.runSpeed);
                    if (dist <= currentAttack.range * 0.85f + player.radius)
                        BeginWindup();
                    else if (stateTimer <= 0f)
                        EndAttack();
                    break;

                case AIState.Windup:
                    Motor.FaceDirection(dirToPlayer);
                    Visual.SetLocomotion(0f);
                    if (stateTimer <= 0f) ReleaseAttack(dist);
                    break;

                case AIState.Recover:
                    Visual.SetLocomotion(0f);
                    if (stateTimer <= 0f) EndAttack();
                    break;

                case AIState.Stagger:
                    Visual.SetLocomotion(0f);
                    if (stateTimer <= 0f) state = AIState.Circle;
                    break;

                case AIState.Down:
                    Visual.SetLocomotion(0f);
                    if (stateTimer <= 0f && Motor.IsGrounded)
                    {
                        state = AIState.GetUp;
                        stateTimer = 0.6f;
                        Visual.SetLying(false);
                        Visual.Play(FighterPose.GetUp, 0.6f);
                    }
                    break;

                case AIState.GetUp:
                    if (stateTimer <= 0f)
                    {
                        state = AIState.Circle;
                        nextAttackTime = Time.time + Random.Range(0.5f, 1.5f);
                    }
                    break;

                case AIState.Grabbed:
                    Visual.SetLocomotion(0f);
                    break;
            }
        }

        // ------------------------------------------------------------------ movement

        void MoveTowards(Vector3 direction, float speed)
        {
            Vector3 v = (direction + Separation() * 0.8f).normalized * speed;
            Motor.SetDesiredVelocity(v);
            Motor.FaceDirection(direction);
            Visual.SetLocomotion(speed / 6f);
        }

        void Circle(Vector3 dirToPlayer, float dist)
        {
            Visual.SetCombatStance(true);
            if (Time.time > nextStrafeFlip)
            {
                nextStrafeFlip = Time.time + Random.Range(1.5f, 4f);
                if (Random.value < 0.4f) strafeDirection = -strafeDirection;
            }

            Vector3 tangent = Vector3.Cross(Vector3.up, dirToPlayer) * strafeDirection;
            float radial = Mathf.Clamp(dist - circleRadius, -1f, 1f);
            Vector3 v = tangent * 0.7f + dirToPlayer * radial + Separation() * 1.2f;
            v = Vector3.ClampMagnitude(v, 1f) * Profile.circleSpeed;

            Motor.SetDesiredVelocity(v);
            Motor.FaceDirection(dirToPlayer);
            Visual.SetLocomotion(v.magnitude / 6f);
        }

        Vector3 Separation()
        {
            Vector3 push = Vector3.zero;
            foreach (var f in All)
            {
                if (f == this || f.team != team || f.IsDead) continue;
                Vector3 away = Flat(transform.position - f.transform.position);
                float d = away.magnitude;
                if (d < 1.6f && d > 0.01f) push += away / d * (1.6f - d);
            }
            return push;
        }

        // ------------------------------------------------------------------ attacking

        void BeginWindup()
        {
            state = AIState.Windup;
            stateTimer = currentAttack.startup;
            Visual.Play(FighterPose.Windup, currentAttack.startup);
            Visual.SetTelegraph(true);
            if (currentAttack.unblockable && GameManager.Instance != null)
                GameManager.Instance.AddPopup("UNBLOCKABLE!", Center + Vector3.up, new Color(1f, 0.4f, 0.1f), 0.8f);
        }

        void ReleaseAttack(float dist)
        {
            Visual.SetTelegraph(false);
            Visual.Play(currentAttack.pose, 0.3f);
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
            state = AIState.Circle;
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
            bool canBlock = (state == AIState.Circle || state == AIState.Approach) && !hit.heavy && !hit.unblockable;
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
            if (state == AIState.Grabbed) Motor.SetFrozen(false);
            state = AIState.Dead;
            Visual.SetTelegraph(false);
            Visual.SetCombatStance(false);
            Visual.SetLocomotion(0f);
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

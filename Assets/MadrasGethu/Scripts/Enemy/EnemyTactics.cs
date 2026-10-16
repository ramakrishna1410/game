using UnityEngine;

namespace MadrasGethu
{
    /// <summary>
    /// Rowdy street smarts, kept fair: every one of these shows a warning or has a clear way out.
    ///   - Reading: the hero repeats a string 3 times, they block it. A heavy right after a read block breaks the guard.
    ///   - Grab escape: a fresh rowdy can shove a grab off (never dazed, staggered or grabbed from behind).
    ///   - No free holds: 1.5 s after the hero grabs someone, a friend comes for him from behind (red !).
    ///   - Flanking: one rowdy at a time circles behind the hero before attacking (red ! and an arrow on screen).
    /// </summary>
    public partial class EnemyFighter
    {
        static EnemyFighter flanker;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetTactics() => flanker = null;
        bool flanking;
        float flankTimer;
        float readBlockTime = -10f;
        float lastShrug = -10f;

        /// <summary>The one rowdy currently circling behind the hero (null if none).</summary>
        public static EnemyFighter Flanker => flanker != null && !flanker.IsDead && flanker.flanking ? flanker : null;

        /// <summary>Sneaking round behind the hero, or winding up from there: the HUD shows a red ! and an arrow.</summary>
        public bool SneakWarning => flanking && (state == AIState.CloseIn || state == AIState.Windup);

        /// <summary>Just blocked a read string: a heavy now breaks his guard.</summary>
        public bool GuardOpen => Time.time - readBlockTime < 1.2f;

        bool BehindHero(PlayerFighter p)
        {
            Vector3 fwd = Flat(p.transform.forward).normalized;
            Vector3 toMe = Flat(transform.position - p.transform.position).normalized;
            return Vector3.Dot(fwd, toMe) < -0.35f;
        }

        /// <summary>Starts circling round behind the hero for this attack. Forced: holding-punisher (always goes).</summary>
        bool StartFlank(PlayerFighter player, bool forced)
        {
            if (Flanker != null && Flanker != this) return false;
            if (currentAttack == null || currentAttack.kind == AttackKind.Charge || currentAttack.kind == AttackKind.GroundPound) return false;
            if (!forced)
            {
                if (Random.value >= Profile.flankChance) return false;
                // Only worth it while he's looking at someone else.
                Vector3 toMe = Flat(transform.position - player.transform.position).normalized;
                if (Vector3.Dot(Flat(player.transform.forward).normalized, toMe) > 0.3f) return false;
            }
            flanking = true;
            flanker = this;
            flankTimer = 2.4f;
            feinting = false;   // no tricks from behind: the warning is the fair part
            return true;
        }

        void EndFlank()
        {
            if (flanker == this) flanker = null;
            flanking = false;
        }

        /// <summary>Per-frame bookkeeping (from Update): the flank ends with the attack.</summary>
        void UpdateTactics()
        {
            if (flanking && state != AIState.CloseIn && state != AIState.Windup) EndFlank();
        }

        /// <summary>
        /// CloseIn while flanking: walk round (not through) the hero to a spot behind him, facing him.
        /// Returns true while still getting there.
        /// </summary>
        bool UpdateFlankMove(PlayerFighter player, Vector3 dirToPlayer, float dist)
        {
            if (!flanking) return false;
            flankTimer -= Time.deltaTime;
            if (BehindHero(player) || flankTimer <= 0f || dist > 5f) return false;
            Vector3 behind = player.transform.position - Flat(player.transform.forward).normalized * 1.2f;
            Vector3 to = Flat(behind - transform.position);
            if (to.magnitude < 0.3f) return false;
            Vector3 d = to.normalized;
            if (Vector3.Dot(d, dirToPlayer) > 0.2f && dist < 2.8f)
            {
                // The straight way goes through him: go round the side.
                Vector3 tangent = Vector3.Cross(Vector3.up, dirToPlayer);
                if (Vector3.Dot(tangent, d) < 0f) tangent = -tangent;
                d = (tangent - dirToPlayer * 0.25f).normalized;
            }
            Visual.SetCombatStance(true);
            Motor.SetDesiredVelocity((d + Separation() * 0.5f).normalized * Mathf.Min(Profile.runSpeed, 3.4f) * MoodSpeed);
            Motor.FaceDirection(dirToPlayer);
            return true;
        }

        /// <summary>The hero is throwing the string he keeps repeating: guard up. Returns true if blocked.</summary>
        bool TryReadBlock(HitInfo hit)
        {
            if (!(hit.attacker is PlayerFighter p) || !p.CurrentHitIsRead) return false;
            if (isDummy && !sparring) return false;
            if (hit.heavy || hit.unblockable) return false;
            if (state != AIState.Engage && state != AIState.Wait && state != AIState.Approach && state != AIState.CloseIn
                && state != AIState.Recover && state != AIState.Stagger) return false;
            if (state == AIState.Stagger) ResumeStance();
            Motor.FaceDirection(-hit.direction, true);
            Visual.Play(FighterPose.Block, 0.4f);
            GameAudio.Play(Sfx.Block, Center + Vector3.up * 0.35f, 0.8f);
            Motor.AddImpulse(hit.direction * hit.knockback * 0.4f);
            readBlockTime = Time.time;
            if (GameManager.Instance != null)
            {
                if (p.AnnounceRead())
                    GameManager.Instance.AddPopup("HE'S READING YOU! SWITCH IT UP", Center + Vector3.up * 1.0f, new Color(1f, 0.55f, 0.3f), 1.3f);
                else
                    GameManager.Instance.AddPopup("BLOCKED", Center + Vector3.up * 0.8f, new Color(0.7f, 0.85f, 1f), 0.6f);
            }
            return true;
        }

        /// <summary>A fresh rowdy shoves the hero's hands off as he reaches for the collar.</summary>
        public bool TryShrugOffGrab(PlayerFighter by, bool fromBehind)
        {
            if (fromBehind || IsDead || IsDowned || (isDummy && !sparring)) return false;
            if (state == AIState.Dazed || state == AIState.Stagger || state == AIState.Grabbed || state == AIState.Begging
                || state == AIState.Fleeing) return false;
            if (Time.time - lastShrug < 3f || Random.value >= Profile.grabEscape) return false;
            lastShrug = Time.time;
            Motor.FaceDirection(by.transform.position - transform.position, true);
            Visual.Play(FighterPose.RowdyShove, 0.45f, 0.4f);
            GameAudio.Play(Sfx.Block, Center + Vector3.up * 0.3f, 0.7f, 0.9f);
            if (GameManager.Instance != null)
                GameManager.Instance.AddPopup("SHRUGGED OFF!", Center + Vector3.up, new Color(1f, 0.6f, 0.3f), 0.8f);
            return true;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace MadrasGethu
{
    public static class CombatUtil
    {
        static readonly List<Fighter> scratch = new List<Fighter>();

        public struct StrikeResult
        {
            public int hits;
            public int blocked;
            public bool Connected => hits > 0 || blocked > 0;
        }

        /// <summary>
        /// Applies an attack to every opposing fighter inside the attack's range and arc,
        /// measured from the attacker's position and facing.
        /// </summary>
        public static StrikeResult Strike(Fighter attacker, AttackData attack, float damageMultiplier = 1f)
        {
            var result = new StrikeResult();
            Vector3 origin = attacker.transform.position;
            Vector3 forward = attacker.transform.forward;

            scratch.Clear();
            scratch.AddRange(Fighter.All);
            foreach (var target in scratch)
            {
                if (target == null || target == attacker || target.team == attacker.team || !target.CanBeTargeted)
                    continue;
                if (target.IsDowned && !attack.hitsDowned)
                    continue;

                Vector3 to = target.transform.position - origin;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist > attack.range + target.radius)
                    continue;
                if (dist > 0.05f && Vector3.Angle(forward, to) > attack.arc * 0.5f)
                    continue;

                Vector3 dir = dist > 0.05f ? to / dist : forward;
                var r = target.ReceiveHit(attack.ToHit(attacker, dir, damageMultiplier));
                if (r == HitResult.Hit)
                {
                    result.hits++;
                    FX.HitSpark(target.Center + Vector3.up * 0.3f - dir * 0.3f, attack.heavy);
                }
                else if (r == HitResult.Blocked)
                {
                    result.blocked++;
                    FX.BlockSpark(target.Center + Vector3.up * 0.3f - dir * 0.4f);
                }
            }
            return result;
        }

        /// <summary>
        /// Finds the best opposing fighter to auto-aim at: close, and roughly in the direction
        /// the player is pushing (or facing when there is no input).
        /// </summary>
        public static Fighter FindTarget(Fighter self, Vector3 preferredDirection, float maxDistance, bool includeDowned = false)
        {
            Fighter best = null;
            float bestScore = float.MaxValue;
            preferredDirection.y = 0f;
            bool hasDir = preferredDirection.sqrMagnitude > 0.01f;
            if (hasDir) preferredDirection.Normalize();

            foreach (var f in Fighter.All)
            {
                if (f == self || f.team == self.team || !f.CanBeTargeted) continue;
                if (f.IsDowned && !includeDowned) continue;
                Vector3 to = f.transform.position - self.transform.position;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist > maxDistance) continue;
                float angle = hasDir && dist > 0.05f ? Vector3.Angle(preferredDirection, to) : 0f;
                if (angle > 100f) continue;
                float score = dist + angle * 0.03f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = f;
                }
            }
            return best;
        }
    }
}

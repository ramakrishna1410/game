using System.Collections.Generic;
using UnityEngine;

namespace MadrasGethu
{
    /// <summary>
    /// Decides who fights, Assassin's Creed / Sleeping Dogs style:
    /// - only a few rowdies are "engaged" (close to the hero, allowed to attack),
    /// - the rest wait in a wider ring in their fighting stance, pacing and watching,
    /// - only one engaged rowdy attacks at a time, with a breather between attacks.
    /// When an engaged rowdy goes down for good, the closest waiting one steps in.
    /// </summary>
    public static class EnemyDirector
    {
        /// <summary>How many rowdies can be close to the hero at once.</summary>
        public static int MaxEngaged = 2;
        /// <summary>How many can be mid-attack at the same moment.</summary>
        public static int MaxAttackers = 1;
        /// <summary>Breather (seconds) after any attack before the next one may start.</summary>
        public static Vector2 AttackGap = new Vector2(0.9f, 1.7f);

        static readonly HashSet<EnemyFighter> engaged = new HashSet<EnemyFighter>();
        static readonly HashSet<EnemyFighter> attackers = new HashSet<EnemyFighter>();
        static readonly List<EnemyFighter> waiting = new List<EnemyFighter>();
        static float nextAttackTime;

        /// <summary>A rowdy is holding the hero from behind: his friends get to swing (two at once).</summary>
        public static bool HeroHeldFromBehind;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            engaged.Clear();
            attackers.Clear();
            waiting.Clear();
            nextAttackTime = 0f;
            HeroHeldFromBehind = false;
        }

        static void Clean()
        {
            engaged.RemoveWhere(e => e == null || e.IsDead);
            attackers.RemoveWhere(e => e == null || e.IsDead);
            waiting.RemoveAll(e => e == null || e.IsDead);
        }

        public static bool IsEngaged(EnemyFighter enemy) => engaged.Contains(enemy);

        /// <summary>
        /// A waiting rowdy asks to step in. Granted when a slot is free and it is the best candidate:
        /// a waiting boss always goes first, otherwise the one closest to the hero.
        /// </summary>
        public static bool RequestEngage(EnemyFighter enemy, Vector3 heroPosition)
        {
            Clean();
            if (engaged.Contains(enemy)) return true;
            if (!waiting.Contains(enemy)) waiting.Add(enemy);
            if (engaged.Count >= MaxEngaged) return false;

            EnemyFighter best = null;
            float bestScore = float.MaxValue;
            foreach (var w in waiting)
            {
                if (w.IsDowned) continue;
                float score = (w.transform.position - heroPosition).sqrMagnitude;
                if (w.IsBoss) score -= 10000f;
                if (score < bestScore) { bestScore = score; best = w; }
            }
            if (best != enemy) return false;

            waiting.Remove(enemy);
            engaged.Add(enemy);
            return true;
        }

        /// <summary>
        /// The hero picked a fight with a waiting rowdy: he joins the fight right away and, if the slots are
        /// full, the engaged rowdy furthest from the hero (and not mid-attack) steps back to wait.
        /// </summary>
        public static void ForceEngage(EnemyFighter enemy, Vector3 heroPosition)
        {
            Clean();
            if (engaged.Contains(enemy)) return;
            if (engaged.Count >= MaxEngaged)
            {
                EnemyFighter furthest = null;
                float furthestDist = -1f;
                foreach (var e in engaged)
                {
                    if (attackers.Contains(e) || e.IsGrabbed) continue;
                    float d = (e.transform.position - heroPosition).sqrMagnitude;
                    if (d > furthestDist) { furthestDist = d; furthest = e; }
                }
                if (furthest == null) return;
                engaged.Remove(furthest);
                waiting.Add(furthest);
            }
            waiting.Remove(enemy);
            engaged.Add(enemy);
        }

        /// <summary>Gives up the close-combat slot (death, or being sent back to wait).</summary>
        public static void Disengage(EnemyFighter enemy)
        {
            engaged.Remove(enemy);
            attackers.Remove(enemy);
            waiting.Remove(enemy);
        }

        /// <summary>An engaged rowdy asks to throw a punch. Returns true if it may.</summary>
        public static bool RequestAttack(EnemyFighter enemy)
        {
            Clean();
            if (!engaged.Contains(enemy)) return false;
            if (attackers.Contains(enemy)) return true;
            int max = HeroHeldFromBehind ? MaxAttackers + 1 : MaxAttackers;
            if (attackers.Count >= max || (!HeroHeldFromBehind && Time.time < nextAttackTime)) return false;
            attackers.Add(enemy);
            return true;
        }

        /// <summary>The attack is over (landed, missed or interrupted): start the breather.</summary>
        public static void Release(EnemyFighter enemy)
        {
            if (attackers.Remove(enemy))
                nextAttackTime = Time.time + Random.Range(AttackGap.x, AttackGap.y);
        }
    }
}

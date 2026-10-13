using System.Collections.Generic;
using UnityEngine;

namespace MadrasGethu
{
    /// <summary>
    /// Limits how many rowdies attack at the same time, like Sleeping Dogs / Yakuza crowds:
    /// most of the group circles and taunts while one or two step in to swing.
    /// </summary>
    public static class EnemyDirector
    {
        public static int MaxAttackers = 2;
        static readonly HashSet<EnemyFighter> attackers = new HashSet<EnemyFighter>();
        static float nextTokenTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            attackers.Clear();
            nextTokenTime = 0f;
        }

        /// <summary>Asks for permission to attack. Returns true if granted.</summary>
        public static bool RequestAttack(EnemyFighter enemy)
        {
            attackers.RemoveWhere(e => e == null || e.IsDead);
            if (attackers.Contains(enemy)) return true;
            if (attackers.Count >= MaxAttackers || Time.time < nextTokenTime) return false;
            attackers.Add(enemy);
            // Small gap between attackers so hits arrive one after another and can be countered.
            nextTokenTime = Time.time + Random.Range(0.35f, 0.7f);
            return true;
        }

        public static void Release(EnemyFighter enemy)
        {
            attackers.Remove(enemy);
        }
    }
}

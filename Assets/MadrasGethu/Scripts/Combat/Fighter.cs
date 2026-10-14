using System;
using System.Collections.Generic;
using UnityEngine;

namespace MadrasGethu
{
    /// <summary>
    /// Base class for anything that can fight: the player and the rowdies.
    /// Holds health and team, and keeps a registry that attacks use to find targets
    /// (no physics layers needed).
    /// </summary>
    [RequireComponent(typeof(FighterMotor))]
    public abstract class Fighter : MonoBehaviour
    {
        public static readonly List<Fighter> All = new List<Fighter>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        public string displayName = "Fighter";
        public Team team;
        public float maxHealth = 100f;
        public float radius = 0.4f;
        public float height = 1.8f;

        public float Health { get; protected set; }
        public float HealthFraction => maxHealth > 0f ? Health / maxHealth : 0f;
        public bool IsDead => Health <= 0f;

        /// <summary>Lying on the ground. Only attacks with hitsDowned can hit a downed fighter.</summary>
        public virtual bool IsDowned => false;
        public virtual bool CanBeTargeted => !IsDead;

        public FighterMotor Motor { get; private set; }

        FighterVisual visual;
        public FighterVisual Visual => visual != null ? visual : (visual = GetComponentInChildren<FighterVisual>());

        public Vector3 Center => transform.position + Vector3.up * (height * 0.6f);

        public event Action<Fighter, HitInfo, HitResult> Damaged;
        public event Action<Fighter> Died;

        protected virtual void Awake()
        {
            Motor = GetComponent<FighterMotor>();
            Health = maxHealth;
        }

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);

        /// <summary>Called by attackers. Implementations decide on block, dodge, armour and reactions.</summary>
        public abstract HitResult ReceiveHit(HitInfo hit);

        /// <summary>Practice dummies: take hits and react, but never go below 1 health.</summary>
        protected virtual bool Immortal => false;

        protected void ApplyDamage(float amount, HitInfo hit, HitResult result)
        {
            if (IsDead) return;
            if (Immortal)
            {
                Health = Mathf.Max(1f, Health - amount);
                Damaged?.Invoke(this, hit, result);
                return;
            }
            Health = Mathf.Max(0f, Health - amount);
            Damaged?.Invoke(this, hit, result);
            if (Health <= 0f)
            {
                OnDeath(hit);
                Died?.Invoke(this);
            }
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            Health = Mathf.Min(maxHealth, Health + amount);
        }

        /// <summary>Resets health; used when a new character takes over this fighter's stats.</summary>
        public void ResetHealth(float newMax)
        {
            maxHealth = newMax;
            Health = newMax;
        }

        protected abstract void OnDeath(HitInfo killingBlow);

        protected static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}

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
        /// <summary>A story cutscene is moving this character: the hero's controls and the rowdy AI are off.</summary>
        [System.NonSerialized] public bool cutsceneControlled;

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

        protected virtual void OnDestroy()
        {
            // A ragdolled body is detached from the fighter's root: clean it up too.
            if (visual != null && visual.transform.parent != transform) Destroy(visual.gameObject);
        }

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
                ImpactFeedback(amount, hit);
                Damaged?.Invoke(this, hit, result);
                return;
            }
            Health = Mathf.Max(0f, Health - amount);
            ImpactFeedback(amount, hit);
            Damaged?.Invoke(this, hit, result);
            if (Health <= 0f)
            {
                OnDeath(hit);
                Died?.Invoke(this);
            }
        }

        /// <summary>The thud of the blow and the body's reaction to it (head snapped aside, folded over...).</summary>
        void ImpactFeedback(float amount, HitInfo hit)
        {
            if (amount <= 0f) return;
            if (hit.sound != Sfx.Silent)
                GameAudio.Play(GameAudio.ImpactFor(hit), Center + Vector3.up * 0.3f, Mathf.Clamp(0.55f + amount / 30f, 0.5f, 1f));
            if (Visual != null) Visual.HitReact(hit.direction, hit.pose, Mathf.Clamp(amount / 12f, 0.4f, 1.5f));
            if (team == Team.Enemy && UnityEngine.Random.value < 0.3f) GameAudio.Play(Sfx.Grunt, Center + Vector3.up * 0.6f, 0.8f);
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

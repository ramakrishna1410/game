using System.Collections.Generic;
using UnityEngine;

namespace MadrasGethu
{
    public enum WeaponType { Lathi, IronPipe, Plank, Bottle, Brick }

    /// <summary>
    /// A street weapon: lies on the ground until someone picks it up, is swung in fights, wears out and
    /// breaks, and can be thrown. Bottles shatter on the first hit; bricks are mostly for throwing.
    /// Placeholder shapes are built in code; swap in real models later via the 'model' override.
    /// </summary>
    public class Weapon : MonoBehaviour
    {
        public static readonly List<Weapon> All = new List<Weapon>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        public WeaponType type;
        public string displayName;
        public float damage = 12f;       // light swing damage (heavy smash does more)
        public float reach = 2.0f;
        public int durability = 6;       // hits before it breaks
        public float throwDamage = 14f;
        public Color color = Color.gray;

        /// <summary>Grip position/rotation in the hand bone's space (tweak if weapons sit wrong in real models).</summary>
        public static Vector3 HandOffset = new Vector3(0f, 0.08f, 0.03f);
        public static Vector3 HandEuler = new Vector3(-90f, 0f, 0f);

        public Fighter Holder { get; private set; }
        public bool OnGround => Holder == null && !flying;
        public int HitsLeft { get; private set; }

        bool flying;
        Vector3 velocity;
        Fighter thrower;
        float flightTime;
        Transform visual;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        // ------------------------------------------------------------------ creation

        public static Weapon Create(WeaponType type, Vector3 position)
        {
            var go = new GameObject(type.ToString()) { layer = 2 };
            go.transform.position = position;
            var w = go.AddComponent<Weapon>();
            w.type = type;
            w.Configure();
            w.BuildVisual();
            w.LayOnGround(position);
            return w;
        }

        void Configure()
        {
            switch (type)
            {
                case WeaponType.Lathi:
                    displayName = "Lathi"; damage = 13f; reach = 2.3f; durability = 8; throwDamage = 10f;
                    color = new Color(0.55f, 0.4f, 0.2f); break;
                case WeaponType.IronPipe:
                    displayName = "Iron Pipe"; damage = 17f; reach = 2.0f; durability = 10; throwDamage = 16f;
                    color = new Color(0.45f, 0.45f, 0.48f); break;
                case WeaponType.Plank:
                    displayName = "Wooden Plank"; damage = 14f; reach = 2.1f; durability = 4; throwDamage = 12f;
                    color = new Color(0.62f, 0.48f, 0.3f); break;
                case WeaponType.Bottle:
                    displayName = "Bottle"; damage = 20f; reach = 1.6f; durability = 1; throwDamage = 12f;
                    color = new Color(0.2f, 0.5f, 0.25f); break;
                case WeaponType.Brick:
                    displayName = "Brick"; damage = 15f; reach = 1.5f; durability = 2; throwDamage = 16f;
                    color = new Color(0.6f, 0.25f, 0.18f); break;
            }
            HitsLeft = durability;
        }

        void BuildVisual()
        {
            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);
            visual.gameObject.layer = 2;
            // Built along local +Z so it points out of the fist.
            switch (type)
            {
                case WeaponType.Lathi:
                    Part(PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.55f), new Vector3(0.045f, 0.75f, 0.045f), Quaternion.Euler(90f, 0f, 0f)); break;
                case WeaponType.IronPipe:
                    Part(PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.4f), new Vector3(0.06f, 0.55f, 0.06f), Quaternion.Euler(90f, 0f, 0f)); break;
                case WeaponType.Plank:
                    Part(PrimitiveType.Cube, new Vector3(0f, 0f, 0.45f), new Vector3(0.12f, 0.035f, 1.05f), Quaternion.identity); break;
                case WeaponType.Bottle:
                    Part(PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.16f), new Vector3(0.08f, 0.1f, 0.08f), Quaternion.Euler(90f, 0f, 0f));
                    Part(PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.02f), new Vector3(0.035f, 0.06f, 0.035f), Quaternion.Euler(90f, 0f, 0f)); break;
                case WeaponType.Brick:
                    Part(PrimitiveType.Cube, new Vector3(0f, 0f, 0.08f), new Vector3(0.1f, 0.07f, 0.21f), Quaternion.identity); break;
            }
        }

        void Part(PrimitiveType shape, Vector3 pos, Vector3 scale, Quaternion rot)
        {
            var go = Palette.Primitive(shape, visual, pos, scale, color, collider: false);
            go.transform.localRotation = rot;
            go.layer = 2;
        }

        // ------------------------------------------------------------------ holding

        public void Attach(Fighter holder)
        {
            Holder = holder;
            flying = false;
            Transform hand = holder.Visual != null ? holder.Visual.RightHand : holder.transform;
            transform.SetParent(hand, false);
            if (holder.Visual != null && holder.Visual.IsPlaceholder)
            {
                transform.localPosition = new Vector3(0f, -0.62f, 0f);
                transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            else
            {
                // Mixamo hands: fingers point along the bone's local Y.
                transform.localPosition = HandOffset;
                transform.localRotation = Quaternion.Euler(HandEuler);
            }
            transform.localScale = new Vector3(1f / Mathf.Max(0.01f, hand.lossyScale.x), 1f / Mathf.Max(0.01f, hand.lossyScale.y), 1f / Mathf.Max(0.01f, hand.lossyScale.z));
        }

        /// <summary>Let go: it falls to the street next to the holder.</summary>
        public void Drop()
        {
            var at = transform.position;
            Holder = null;
            transform.SetParent(null, true);
            transform.localScale = Vector3.one;
            LayOnGround(at + Random.insideUnitSphere * 0.3f);
        }

        void LayOnGround(Vector3 near)
        {
            transform.position = new Vector3(near.x, 0.05f, near.z);
            transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }

        /// <summary>Called when a swing connects. Returns true if the weapon broke.</summary>
        public bool RegisterHit()
        {
            HitsLeft--;
            if (HitsLeft > 0) return false;
            Shatter();
            return true;
        }

        void Shatter()
        {
            Color c = type == WeaponType.Bottle ? new Color(0.5f, 0.9f, 0.6f) : color;
            GameAudio.Play(type == WeaponType.Bottle ? Sfx.Glass : Sfx.Smash, transform.position, 1f);
            FX.Burst(transform.position, type == WeaponType.Bottle ? 16 : 10, 4f, 0.05f, c,
                type == WeaponType.Bottle ? c * 0.6f : Color.black, 0.5f, -9f);
            if (GameManager.Instance != null)
                GameManager.Instance.AddPopup(type == WeaponType.Bottle ? "SMASH!" : displayName.ToUpper() + " BROKE",
                    transform.position + Vector3.up * 0.5f, new Color(1f, 0.8f, 0.5f), 0.7f);
            Holder = null;
            Destroy(gameObject);
        }

        // ------------------------------------------------------------------ swings

        /// <summary>Quick swing (light) or overhead smash (heavy) built for this weapon.</summary>
        public AttackData MakeAttack(bool heavy)
        {
            if (!heavy)
                return new AttackData(displayName + " Swing", FighterPose.WeaponSwing, damage)
                {
                    startup = 0.2f, recovery = 0.38f, range = reach, arc = 130f, lunge = 0.8f,
                    knockback = 4f, gethuGain = 6f, hitStop = 0.08f, shake = 0.18f,
                };
            return new AttackData(displayName + " Smash", FighterPose.WeaponSmash, damage * 1.7f)
            {
                announce = true, startup = 0.34f, recovery = 0.6f, range = reach, arc = 100f, lunge = 1.0f,
                knockback = 7f, launch = 3.5f, heavy = true, gethuGain = 10f, hitStop = 0.12f, shake = 0.35f,
            };
        }

        // ------------------------------------------------------------------ throwing

        /// <summary>Throw at a point (arc), damaging the first opponent it reaches.</summary>
        public void Throw(Fighter by, Vector3 target)
        {
            if (Holder != null) transform.SetParent(null, true);
            Holder = null;
            transform.localScale = Vector3.one;
            thrower = by;
            flying = true;
            flightTime = 0f;

            Vector3 start = transform.position;
            float t = Mathf.Clamp(Vector3.Distance(start, target) / 14f, 0.35f, 0.9f);
            velocity = (target - start) / t - 0.5f * Physics.gravity * t;
        }

        void Update()
        {
            if (!flying) return;
            float dt = Time.deltaTime;
            flightTime += dt;
            velocity += Physics.gravity * dt;
            transform.position += velocity * dt;
            if (visual != null) visual.Rotate(720f * dt, 0f, 0f, Space.Self);

            foreach (var f in Fighter.All.ToArray())
            {
                if (f == null || f == thrower || f.IsDead || (thrower != null && f.team == thrower.team)) continue;
                Vector3 d = f.Center - transform.position;
                if (d.magnitude > f.radius + 0.45f) continue;

                var dir = new Vector3(velocity.x, 0f, velocity.z).normalized;
                f.ReceiveHit(new HitInfo
                {
                    attacker = thrower, damage = throwDamage, direction = dir,
                    knockback = 3f, heavy = type == WeaponType.Brick || type == WeaponType.IronPipe,
                });
                FX.HitSpark(transform.position, true);
                Shatter();
                return;
            }

            if ((flightTime > 0.1f && transform.position.y <= 0.05f) || flightTime > 3f)
            {
                flying = false;
                if (type == WeaponType.Bottle) { Shatter(); return; }
                LayOnGround(transform.position);
                FX.Dust(transform.position, 4);
            }
        }

        public static Weapon FindNearbyOnGround(Vector3 position, float maxDistance)
        {
            Weapon best = null;
            float bestDist = maxDistance;
            foreach (var w in All)
            {
                if (w == null || !w.OnGround) continue;
                Vector3 d = w.transform.position - position;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist < bestDist) { bestDist = dist; best = w; }
            }
            return best;
        }
    }
}

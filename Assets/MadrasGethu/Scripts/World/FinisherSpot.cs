using System.Collections.Generic;
using UnityEngine;

namespace MadrasGethu
{
    /// <summary>
    /// A breakable prop the player can smash a grabbed enemy into (Sleeping Dogs-style
    /// environmental kill), e.g. a tea kadai, a stack of fish crates or a parked auto.
    /// Drag an enemy close while grabbing and press Gethu (Q / RT).
    /// </summary>
    public class FinisherSpot : MonoBehaviour
    {
        public enum SpotKind
        {
            Smash,    // tea kadai, crates, auto: smash his head into it, it breaks
            Sea,      // jetty edge: throw him into the harbour
            IceBox,   // fish market ice box: head in, slam the lid on him
        }

        public static readonly List<FinisherSpot> All = new List<FinisherSpot>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        public SpotKind kind = SpotKind.Smash;
        [Tooltip("Sea: the way to the water (flat, world space).")]
        public Vector3 outDirection = Vector3.right;
        [Tooltip("Ice box: the lid's hinge (rotates round its local X axis to open).")]
        public Transform lid;
        public string finisherName = "TEA KADAI SMASH!";
        public string promptName = "Tea Kadai";
        public float damage = 60f;
        public float gethuCost = 30f;
        public float useRadius = 2.6f;
        [Tooltip("Rough size of the prop itself, for thrown enemies crashing into it.")]
        public float bodyRadius = 0.9f;
        public Color debrisColor = new Color(0.6f, 0.4f, 0.2f);

        /// <summary>Visual pieces that are hidden when the prop breaks.</summary>
        public GameObject intactVisual;

        public bool IsBroken { get; private set; }

        /// <summary>Where the enemy's head gets slammed.</summary>
        public Vector3 SlamPoint => transform.position + Vector3.up * (kind == SpotKind.IceBox ? 0.7f : 0.9f);

        /// <summary>Ice box lid: 0 = shut, 1 = wide open.</summary>
        public void SetLidOpen(float amount)
        {
            if (lid != null) lid.localRotation = Quaternion.Euler(-105f * Mathf.Clamp01(amount), 0f, 0f);
        }

        readonly List<GameObject> debris = new List<GameObject>();

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static FinisherSpot FindUsable(Vector3 position)
        {
            FinisherSpot best = null;
            float bestDist = float.MaxValue;
            foreach (var s in All)
            {
                if (s.IsBroken) continue;
                Vector3 d = s.transform.position - position;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist <= s.useRadius && dist < bestDist)
                {
                    bestDist = dist;
                    best = s;
                }
            }
            return best;
        }

        /// <summary>An unbroken prop whose body is within reach of a flying (thrown) fighter.</summary>
        public static FinisherSpot FindHit(Vector3 position, float reach)
        {
            foreach (var s in All)
            {
                if (s.IsBroken || s.kind == SpotKind.Sea) continue;
                Vector3 d = s.transform.position - position;
                d.y = 0f;
                if (d.magnitude <= s.bodyRadius + reach) return s;
            }
            return null;
        }

        public void Break(Vector3 fromDirection)
        {
            if (IsBroken) return;
            IsBroken = true;
            GameAudio.Play(Sfx.Smash, SlamPoint, 1f);
            if (intactVisual != null) intactVisual.SetActive(false);

            for (int i = 0; i < 24; i++)
            {
                var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.name = "Debris";
                piece.transform.position = SlamPoint + Random.insideUnitSphere * 0.6f;
                piece.transform.rotation = Random.rotation;
                piece.transform.localScale = new Vector3(Random.Range(0.1f, 0.5f), Random.Range(0.05f, 0.2f), Random.Range(0.1f, 0.4f));
                Color c = Color.Lerp(debrisColor, Color.white, Random.Range(0f, 0.3f));
                piece.GetComponent<Renderer>().sharedMaterial = Palette.Get(c);
                var rb = piece.AddComponent<Rigidbody>();
                rb.mass = 0.3f;
                Vector3 force = (fromDirection.normalized + Random.insideUnitSphere * 0.8f + Vector3.up) * Random.Range(2f, 5f);
                rb.AddForce(force, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.Impulse);
                debris.Add(piece);
            }
            FX.Dust(transform.position, 18);
            FX.Burst(SlamPoint, 16, 7f, 0.08f, new Color(1f, 0.9f, 0.6f), new Color(3f, 2.4f, 1.2f), 0.4f, -6f);
        }

        public void Repair()
        {
            foreach (var d in debris)
                if (d != null) Destroy(d);
            debris.Clear();
            IsBroken = false;
            if (intactVisual != null) intactVisual.SetActive(true);
            SetLidOpen(0f);
        }
    }
}

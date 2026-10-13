using UnityEngine;

namespace MadrasGethu
{
    /// <summary>A tiny self-destroying effect piece spawned by FX.</summary>
    public class FXParticle : MonoBehaviour
    {
        public Vector3 velocity;
        public float gravity;
        public float life = 0.3f;
        float age;
        Vector3 startScale;

        void Start() => startScale = transform.localScale;

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            velocity.y += gravity * dt;
            transform.position += velocity * dt;
            transform.localScale = startScale * Mathf.Clamp01(1f - age / life);
            if (age >= life) Destroy(gameObject);
        }
    }
}

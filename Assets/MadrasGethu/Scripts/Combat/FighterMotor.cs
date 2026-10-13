using UnityEngine;

namespace MadrasGethu
{
    /// <summary>
    /// Shared CharacterController movement for the player and enemies:
    /// walking, knockback impulses, launches and gravity.
    /// The owner calls SetDesiredVelocity every frame.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(100)]
    public class FighterMotor : MonoBehaviour
    {
        public float gravity = -22f;
        public float turnSpeed = 600f;          // degrees per second
        public float acceleration = 14f;        // m/s² for walking/running (gives the body some weight)
        public float groundImpulseDecay = 18f;
        public float airImpulseDecay = 2f;

        CharacterController cc;
        Vector3 desired;
        Vector3 smoothedDesired;
        bool snapDesired;
        Vector3 impulse;
        float verticalVelocity;

        /// <summary>When frozen (e.g. held in a grab) the motor stops moving the character.</summary>
        public bool Frozen { get; private set; }
        public bool IsGrounded => cc.enabled && cc.isGrounded;
        public Vector3 Velocity { get; private set; }
        /// <summary>Walking/running velocity only (no knockback), used to pick walk/strafe/run animations.</summary>
        public Vector3 MoveVelocity => Frozen ? Vector3.zero : smoothedDesired;
        public CharacterController Controller => cc;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
        }

        /// <summary>Walk/run velocity for this frame. instant = skip acceleration (used for attack lunges).</summary>
        public void SetDesiredVelocity(Vector3 velocity, bool instant = false)
        {
            desired = new Vector3(velocity.x, 0f, velocity.z);
            snapDesired = instant;
        }

        public void AddImpulse(Vector3 velocity)
        {
            impulse += new Vector3(velocity.x, 0f, velocity.z);
            if (velocity.y > 0f)
                verticalVelocity = Mathf.Max(verticalVelocity, velocity.y);
        }

        /// <summary>Pushes the character so it slides roughly the given distance on the ground.</summary>
        public void AddLunge(Vector3 direction, float distance)
        {
            direction.y = 0f;
            if (distance <= 0f || direction.sqrMagnitude < 0.0001f) return;
            impulse += direction.normalized * Mathf.Sqrt(2f * groundImpulseDecay * distance);
        }

        public void ClearImpulse()
        {
            impulse = Vector3.zero;
        }

        public void FaceDirection(Vector3 direction, bool instant = false)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            var target = Quaternion.LookRotation(direction);
            transform.rotation = instant
                ? target
                : Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
        }

        public void SetFrozen(bool frozen)
        {
            Frozen = frozen;
            cc.enabled = !frozen;
            desired = Vector3.zero;
            smoothedDesired = Vector3.zero;
            impulse = Vector3.zero;
            verticalVelocity = 0f;
        }

        /// <summary>Moves the character instantly (safe with a CharacterController attached).</summary>
        public void Teleport(Vector3 position)
        {
            bool wasEnabled = cc.enabled;
            cc.enabled = false;
            transform.position = position;
            cc.enabled = wasEnabled;
        }

        void Update()
        {
            if (Frozen || !cc.enabled)
            {
                Velocity = Vector3.zero;
                return;
            }

            float dt = Time.deltaTime;
            bool grounded = cc.isGrounded;
            if (grounded && verticalVelocity < 0f)
                verticalVelocity = -2f;   // keeps the controller snapped to the ground
            verticalVelocity += gravity * dt;

            smoothedDesired = snapDesired
                ? desired
                : Vector3.MoveTowards(smoothedDesired, desired, acceleration * dt);
            snapDesired = false;

            Vector3 v = smoothedDesired + impulse;
            v.y = verticalVelocity;
            cc.Move(v * dt);
            Velocity = v;

            float decay = grounded ? groundImpulseDecay : airImpulseDecay;
            impulse = Vector3.MoveTowards(impulse, Vector3.zero, decay * dt);
            desired = Vector3.zero;

            // Safety net: if something falls out of the world, put it back on the street.
            if (transform.position.y < -10f)
                Teleport(new Vector3(0f, 1f, transform.position.z));
        }
    }
}

using UnityEngine;

namespace MadrasGethu
{
    /// <summary>
    /// Orbit camera that follows the player, avoids clipping into walls and supports screen shake.
    /// (Cinemachine can replace this later; this version has no package dependencies.)
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 4.6f;
        public float combatDistance = 5.4f;
        public float heightOffset = 1.55f;
        public float shoulderOffset = 0.35f;
        public float sensitivity = 1f;
        public float minPitch = -25f;
        public float maxPitch = 65f;
        public float collisionRadius = 0.25f;
        public float followSharpness = 14f;

        float yaw;
        float pitch = 15f;
        float currentDistance;
        float shakeAmount;
        float shakeTimer;
        float fovKick;
        Camera cam;
        float baseFov;
        Vector3 smoothedPivot;

        /// <summary>True while enemies are close; pulls the camera back to show the crowd.</summary>
        public bool InCombat { get; set; }

        /// <summary>Yaw-only rotation, used to make movement camera-relative.</summary>
        public Quaternion FlatRotation => Quaternion.Euler(0f, yaw, 0f);

        void Awake()
        {
            cam = GetComponent<Camera>();
            baseFov = cam != null ? cam.fieldOfView : 60f;
            currentDistance = distance;
        }

        void Start()
        {
            if (target != null)
            {
                yaw = target.eulerAngles.y;
                smoothedPivot = target.position + Vector3.up * heightOffset;
            }
            LockCursor(true);
        }

        public static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        public void Shake(float amount, float duration = 0.2f)
        {
            shakeAmount = Mathf.Max(shakeAmount, amount);
            shakeTimer = Mathf.Max(shakeTimer, duration);
        }

        public void FovKick(float degrees)
        {
            fovKick = Mathf.Max(fovKick, degrees);
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.unscaledDeltaTime;

            if (GameInput.UnlockCursorPressed) LockCursor(false);
            else if (Cursor.lockState != CursorLockMode.Locked && GameInput.ClickPressed) LockCursor(true);

            Vector2 look = GameInput.Look * sensitivity;
            yaw += look.x;
            pitch = Mathf.Clamp(pitch - look.y, minPitch, maxPitch);

            Vector3 pivotTarget = target.position + Vector3.up * heightOffset;
            smoothedPivot = Vector3.Lerp(smoothedPivot, pivotTarget, 1f - Mathf.Exp(-followSharpness * dt));

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = smoothedPivot + rot * new Vector3(shoulderOffset, 0f, 0f);

            float wanted = InCombat ? combatDistance : distance;
            float allowed = wanted;
            if (Physics.SphereCast(pivot, collisionRadius, rot * Vector3.back, out var hit, wanted,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                allowed = Mathf.Max(0.6f, hit.distance - 0.05f);

            // Snap in quickly when blocked, ease back out slowly.
            float rate = allowed < currentDistance ? 25f : 3f;
            currentDistance = Mathf.Lerp(currentDistance, allowed, 1f - Mathf.Exp(-rate * dt));

            Vector3 position = pivot + rot * new Vector3(0f, 0f, -currentDistance);

            if (shakeTimer > 0f)
            {
                shakeTimer -= dt;
                float t = Time.unscaledTime * 40f;
                Vector3 offset = new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, 0f) * 2f;
                position += rot * offset * shakeAmount;
                if (shakeTimer <= 0f) shakeAmount = 0f;
            }

            transform.SetPositionAndRotation(position, rot);

            if (cam != null)
            {
                fovKick = Mathf.MoveTowards(fovKick, 0f, dt * 30f);
                cam.fieldOfView = baseFov + fovKick;
            }
        }
    }
}

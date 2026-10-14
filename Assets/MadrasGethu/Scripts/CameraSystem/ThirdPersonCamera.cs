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

        [Header("Cinematic finishers")]
        [Tooltip("Close-up camera angles during finishers and wrestling moves.")]
        public bool cinematicFinishers = true;
        public float cinematicSharpness = 7f;

        float yaw;
        float pitch = 15f;
        float currentDistance;
        float shakeAmount;
        float shakeTimer;
        float fovKick;
        Camera cam;
        float baseFov;
        Vector3 smoothedPivot;

        // Cinematic shot (finishers): blended over the orbit camera.
        bool cineActive;
        float cineEndAt;
        float cineWeight;
        Vector3 cineWantPos, cineWantLook, cinePos, cineLook;
        float cineWantFov, cineWantRoll, cineFov, cineRoll;
        float zoomPunch;

        /// <summary>True while a finisher's close-up camera is showing (or blending out).</summary>
        public bool CinematicActive => cineActive || cineWeight > 0.01f;

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

        /// <summary>Quick zoom-in that springs back (impacts).</summary>
        public void ZoomPunch(float degrees)
        {
            zoomPunch = Mathf.Max(zoomPunch, degrees);
        }

        /// <summary>
        /// Requests a close-up shot for this frame (call every frame during a finisher). The camera glides
        /// between shots, so changing angles mid-move gives a smooth dolly instead of a hard cut.
        /// </summary>
        public void SetCinematicShot(Vector3 cameraPosition, Vector3 lookAt, float fov, float roll = 0f)
        {
            if (!cinematicFinishers) return;
            if (!CinematicActive)
            {
                cinePos = transform.position;
                cineLook = transform.position + transform.forward * 4f;
                cineFov = cam != null ? cam.fieldOfView : 58f;
                cineRoll = 0f;
            }
            cineActive = true;
            cineEndAt = float.MaxValue;
            cineWantPos = cameraPosition;
            cineWantLook = lookAt;
            cineWantFov = fov;
            cineWantRoll = roll;
        }

        /// <summary>Holds the last shot for a moment (to show the landing), then eases back to the normal camera.</summary>
        public void EndCinematic(float hold = 0.4f)
        {
            if (cineActive) cineEndAt = Mathf.Min(cineEndAt, Time.unscaledTime + hold);
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.unscaledDeltaTime;

            if (GameInput.UnlockCursorPressed) LockCursor(false);
            else if (Cursor.lockState != CursorLockMode.Locked && GameInput.ClickPressed) LockCursor(true);

            if (cineActive && Time.unscaledTime >= cineEndAt) cineActive = false;
            Vector2 look = cineActive ? Vector2.zero : GameInput.Look * sensitivity;
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

            fovKick = Mathf.MoveTowards(fovKick, 0f, dt * 30f);
            zoomPunch = Mathf.MoveTowards(zoomPunch, 0f, dt * 25f);
            float fov = baseFov + fovKick;

            // Blend in the finisher close-up.
            cineWeight = Mathf.MoveTowards(cineWeight, cineActive ? 1f : 0f, dt * (cineActive ? 4f : 2.2f));
            if (cineWeight > 0f)
            {
                float k = 1f - Mathf.Exp(-cinematicSharpness * dt);
                Vector3 want = cineWantPos;
                Vector3 toCam = want - cineWantLook;
                if (toCam.sqrMagnitude > 0.01f && Physics.SphereCast(cineWantLook, collisionRadius, toCam.normalized, out var cineHit, toCam.magnitude,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    want = cineWantLook + toCam.normalized * Mathf.Max(0.8f, cineHit.distance - 0.1f);
                cinePos = Vector3.Lerp(cinePos, want, k);
                cineLook = Vector3.Lerp(cineLook, cineWantLook, k);
                cineFov = Mathf.Lerp(cineFov, cineWantFov, k);
                cineRoll = Mathf.Lerp(cineRoll, cineWantRoll, k);

                Vector3 dir = cineLook - cinePos;
                Quaternion cineRot = dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 0f, cineRoll) : rot;
                float w = Mathf.SmoothStep(0f, 1f, cineWeight);
                Vector3 shake = position - (pivot + rot * new Vector3(0f, 0f, -currentDistance));
                position = Vector3.Lerp(position - shake, cinePos, w) + shake;
                rot = Quaternion.Slerp(rot, cineRot, w);
                fov = Mathf.Lerp(fov, cineFov + fovKick * 0.5f, w);
            }

            transform.SetPositionAndRotation(position, rot);
            if (cam != null) cam.fieldOfView = fov - zoomPunch;
        }
    }
}

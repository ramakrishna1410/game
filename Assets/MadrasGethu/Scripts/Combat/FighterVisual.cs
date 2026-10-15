using System.Collections.Generic;
using UnityEngine;

namespace MadrasGethu
{
    /// <summary>
    /// Builds a placeholder blocky body and animates it procedurally (punches, kicks, flinches, knockdowns),
    /// so the game is playable with no art at all.
    ///
    /// To use a real character later (e.g. from Mixamo), assign modelPrefab before the body is built.
    /// The placeholder is then skipped and every pose is sent to the model's Animator as a trigger
    /// with the same name (Jab, Cross, Hook, ...), plus a float "Speed" and a bool "Guard".
    /// See docs/MIXAMO_ANIMATIONS.md.
    /// </summary>
    public class FighterVisual : MonoBehaviour
    {
        [Header("Placeholder colours")]
        public Color skinColor = new Color(0.55f, 0.36f, 0.24f);
        public Color shirtColor = Color.white;
        public Color pantsColor = new Color(0.2f, 0.2f, 0.25f);
        public Color accentColor = Color.black;   // headband / towel / belt
        public bool hasMoustache = true;

        [Header("Optional real model")]
        public GameObject modelPrefab;
        public Animator animator;
        [Tooltip("Used when the model's Animator has no controller (e.g. an FBX dragged in directly).")]
        public RuntimeAnimatorController fallbackController;

        static readonly HashSet<string> warnedModels = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => warnedModels.Clear();

        Transform body;       // pivot at the feet; tilted for leaning and knockdowns
        Transform torso;
        Transform armL, armR, legL, legR;
        readonly List<Renderer> renderers = new List<Renderer>();
        Material[] originalMaterials;
        Renderer[] modelRenderers;
        HandIK handIK;
        Ragdoll ragdoll;
        Transform ownerRoot;   // the fighter's root (CharacterController); the body is detached from it while ragdolled
        float tumbleTimer;
        float tumbleDuration;
        Vector3 tumbleAxis;
        MaterialPropertyBlock tintBlock;
        Color currentTint = Color.white;
        HashSet<string> animatorParams;

        FighterPose pose = FighterPose.Idle;
        float poseTimer;
        bool inCombatStance;
        float speed01;
        float walkPhase;
        float lie;            // 0 standing .. 1 lying on the back
        bool lying;
        float flinch;
        float flashTimer;
        TelegraphColor telegraph;
        bool built;

        public bool UsesAnimator => animator != null;

        void Start()
        {
            if (!built) Build();
        }

        public void Build()
        {
            if (built) return;
            built = true;

            body = new GameObject("Body").transform;
            body.SetParent(transform, false);

            if (modelPrefab != null)
            {
                var model = Instantiate(modelPrefab, body);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                if (animator == null) animator = model.GetComponentInChildren<Animator>();
                if (animator == null && model.GetComponentInChildren<SkinnedMeshRenderer>() != null)
                    animator = model.AddComponent<Animator>();
                if (animator != null)
                {
                    if (animator.runtimeAnimatorController == null && fallbackController != null)
                        animator.runtimeAnimatorController = fallbackController;
                    animator.applyRootMotion = false;   // the code moves the character, not the animation
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
                WarnAboutModelProblems(modelPrefab.name);
                foreach (var col in model.GetComponentsInChildren<Collider>())
                    Destroy(col);
                modelRenderers = model.GetComponentsInChildren<Renderer>();
                if (animator != null && animator.isHuman)
                    handIK = animator.gameObject.AddComponent<HandIK>();
                tintBlock = new MaterialPropertyBlock();
                CacheAnimatorParams();
                SetLayerRecursively(model, gameObject.layer);
                ownerRoot = transform.parent;
                if (animator != null && animator.isHuman)
                {
                    ragdoll = animator.gameObject.AddComponent<Ragdoll>();
                    if (!ragdoll.Build(animator))
                    {
                        Destroy(ragdoll);
                        ragdoll = null;
                    }
                }
                return;
            }

            // Legs (pivot at the hip)
            legL = Limb("LegL", new Vector3(-0.13f, 0.85f, 0f), new Vector3(0.2f, 0.85f, 0.2f), pantsColor);
            legR = Limb("LegR", new Vector3(0.13f, 0.85f, 0f), new Vector3(0.2f, 0.85f, 0.2f), pantsColor);

            // Torso, with a lungi-style waist wrap
            torso = new GameObject("Torso").transform;
            torso.SetParent(body, false);
            torso.localPosition = new Vector3(0f, 0.85f, 0f);
            Part(PrimitiveType.Cube, torso, new Vector3(0f, 0.38f, 0f), new Vector3(0.52f, 0.62f, 0.3f), shirtColor);
            Part(PrimitiveType.Cube, torso, new Vector3(0f, 0.04f, 0f), new Vector3(0.54f, 0.1f, 0.32f), accentColor);

            // Head, hair and moustache
            Part(PrimitiveType.Cube, torso, new Vector3(0f, 0.73f, 0f), new Vector3(0.12f, 0.1f, 0.12f), skinColor);
            Part(PrimitiveType.Sphere, torso, new Vector3(0f, 0.92f, 0f), new Vector3(0.3f, 0.32f, 0.3f), skinColor);
            Part(PrimitiveType.Sphere, torso, new Vector3(0f, 1.0f, -0.02f), new Vector3(0.31f, 0.2f, 0.31f), new Color(0.06f, 0.05f, 0.05f));
            if (hasMoustache)
                Part(PrimitiveType.Cube, torso, new Vector3(0f, 0.87f, 0.145f), new Vector3(0.14f, 0.03f, 0.03f), new Color(0.06f, 0.05f, 0.05f));

            // Arms (pivot at the shoulder), fists in skin colour
            armL = Limb("ArmL", new Vector3(-0.34f, 0.63f, 0f), new Vector3(0.15f, 0.62f, 0.15f), shirtColor, torso, skinColor);
            armR = Limb("ArmR", new Vector3(0.34f, 0.63f, 0f), new Vector3(0.15f, 0.62f, 0.15f), shirtColor, torso, skinColor);

            originalMaterials = new Material[renderers.Count];
            for (int i = 0; i < renderers.Count; i++)
                originalMaterials[i] = renderers[i].sharedMaterial;
        }

        Transform Limb(string limbName, Vector3 pivot, Vector3 size, Color color, Transform parent = null, Color? endColor = null)
        {
            var p = new GameObject(limbName).transform;
            p.SetParent(parent != null ? parent : body, false);
            p.localPosition = pivot;
            Part(PrimitiveType.Cube, p, new Vector3(0f, -size.y * 0.5f, 0f), size, color);
            if (endColor.HasValue)
                Part(PrimitiveType.Cube, p, new Vector3(0f, -size.y, 0f), Vector3.one * (size.x * 1.25f), endColor.Value);
            return p;
        }

        void Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color color)
        {
            var go = Palette.Primitive(type, parent, pos, scale, color, collider: false);
            go.layer = gameObject.layer;
            renderers.Add(go.GetComponent<Renderer>());
        }

        /// <summary>Explains in the Console why a model would stand in a T-pose.</summary>
        void WarnAboutModelProblems(string modelName)
        {
            if (!warnedModels.Add(modelName)) return;
            const string fix = " Fix: put the FBX in Assets/MadrasGethu/Art/Characters and run 'Madras Gethu > Setup Characters (Mixamo)'.";
            if (animator == null)
                Debug.LogWarning($"[Madras Gethu] Model '{modelName}' has no Animator and no skinned mesh, so it cannot animate." + fix);
            else if (animator.runtimeAnimatorController == null)
                Debug.LogWarning($"[Madras Gethu] Model '{modelName}' has no Animator Controller, so it will stay in a T-pose." + fix);
            else if (animator.avatar == null || !animator.avatar.isHuman)
                Debug.LogWarning($"[Madras Gethu] Model '{modelName}' is not set up as a Humanoid, so the shared animations cannot play on it." + fix);
        }

        static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        readonly List<string> triggerParams = new List<string>();
        readonly Dictionary<string, float> clipLengths = new Dictionary<string, float>();

        void CacheAnimatorParams()
        {
            animatorParams = new HashSet<string>();
            triggerParams.Clear();
            clipLengths.Clear();
            if (animator == null || animator.runtimeAnimatorController == null) return;
            foreach (var p in animator.parameters)
            {
                animatorParams.Add(p.name);
                if (p.type == AnimatorControllerParameterType.Trigger) triggerParams.Add(p.name);
            }
            // The setup tool names each clip after its move (Jab, Cross, ...), so moves can be timed to the game.
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
                if (clip != null && !clipLengths.ContainsKey(clip.name))
                    clipLengths[clip.name] = clip.length;

            // Contact frames measured by the setup tool (also covers moves that borrow another move's clip).
            impactTimes.Clear();
            var timings = AnimationTimings.Load();
            if (timings != null)
            {
                string controllerName = animator.runtimeAnimatorController.name;
                foreach (var e in timings.entries)
                {
                    if (e.controller != controllerName) continue;
                    if (e.length > 0f) clipLengths[e.move] = e.length;
                    if (e.impact > 0f) impactTimes[e.move] = e.impact;
                }
            }
        }

        readonly Dictionary<string, float> impactTimes = new Dictionary<string, float>();

        /// <summary>
        /// Seconds into this move's animation (at normal speed) where the fist/foot connects. 0 if there is
        /// no animation for it. Without measured data, assumes 40% of the clip.
        /// </summary>
        public float ImpactTime(FighterPose move)
        {
            string trigger = move.ToString();
            if (animator == null || animatorParams == null || !animatorParams.Contains(trigger)) return 0f;
            if (impactTimes.TryGetValue(trigger, out float t)) return t;
            return clipLengths.TryGetValue(trigger, out float length) ? length * 0.4f : 0f;
        }

        /// <summary>
        /// Plays an attack so its contact frame lands exactly <paramref name="impactIn"/> seconds from now
        /// (within limits: very slow clips are sped up at most 2.6x). Returns when contact will really
        /// happen; the caller should apply the hit at that moment, not before.
        /// </summary>
        public float PlayTimed(FighterPose move, float impactIn, float afterImpact)
        {
            return PlayTimed(move, impactIn, afterImpact, 2.6f, 0.3f);
        }

        /// <summary>
        /// As above, but the clip may be sped up by at most maxSpeed (big kicks look like a twitch when
        /// squeezed), and the pose is held for up to maxRecover after the impact so the follow-through shows.
        /// </summary>
        public float PlayTimed(FighterPose move, float impactIn, float afterImpact, float maxSpeed, float maxRecover)
        {
            float clipImpact = ImpactTime(move);
            if (clipImpact <= 0f)
            {
                Play(move, impactIn + Mathf.Min(afterImpact, maxRecover), impactIn + afterImpact);
                return impactIn;
            }
            float speed = Mathf.Clamp(clipImpact / Mathf.Max(0.05f, impactIn), 0.6f, maxSpeed);
            float actual = clipImpact / speed;
            PlayAtSpeed(move, actual + Mathf.Min(afterImpact, maxRecover), speed);
            return actual;
        }

        // ------------------------------------------------------------------ public API

        /// <summary>Plays a pose for the given time, then returns to idle/guard.</summary>
        /// <param name="animationTime">
        /// If set, the move's animation is sped up or slowed down to last exactly this long, so the punch
        /// visibly lands when the game registers the hit. Otherwise it plays at a natural speed.
        /// </param>
        public void Play(FighterPose newPose, float duration, float animationTime = 0f)
        {
            // Held poses (block, grab) are re-requested every frame: keep them running instead of restarting.
            bool held = newPose == FighterPose.Block || newPose == FighterPose.Grab;
            if (held && newPose == pose && poseTimer > 0.05f)
            {
                poseTimer = Mathf.Max(poseTimer, duration);
                return;
            }
            pose = newPose;
            poseTimer = duration;

            float speed = 1.2f;
            if (animationTime > 0f && clipLengths.TryGetValue(newPose.ToString(), out float length) && length > 0f)
                speed = Mathf.Clamp(length / animationTime, 0.6f, 3.5f);
            SendTrigger(newPose, speed);
        }

        void PlayAtSpeed(FighterPose newPose, float duration, float speed)
        {
            pose = newPose;
            poseTimer = duration;
            SendTrigger(newPose, speed);
        }

        void SendTrigger(FighterPose newPose, float speed)
        {
            string trigger = newPose.ToString();
            if (animator == null || animatorParams == null || !animatorParams.Contains(trigger)) return;
            if (animatorParams.Contains("ActionSpeed")) animator.SetFloat("ActionSpeed", speed);
            // Clear older requests so extra button presses don't queue up and fire late.
            foreach (var t in triggerParams)
                if (t != trigger) animator.ResetTrigger(t);
            animator.SetTrigger(trigger);
        }

        public void SetCombatStance(bool on)
        {
            inCombatStance = on;
            if (animator != null && animatorParams != null && animatorParams.Contains("Guard"))
                animator.SetBool("Guard", on);
        }

        // Reference speeds (m/s) that match the walk and run animations.
        const float WalkReference = 1.4f;
        const float RunReference = 4.5f;
        FighterMotor motor;
        Vector3 lastRootPosition, draggedVelocity;

        /// <summary>
        /// Locomotion is driven automatically from the character's real movement (see UpdateLocomotion),
        /// so animations always match what the body is doing. Kept for older calls; does nothing now.
        /// </summary>
        public void SetLocomotion(float normalizedSpeed) { }

        /// <summary>Top of the character's head in world space (for health bars and popups).</summary>
        public float HeadTopY
        {
            get
            {
                if (modelRenderers != null)
                {
                    float top = float.MinValue;
                    foreach (var r in modelRenderers)
                        if (r != null && r.enabled) top = Mathf.Max(top, r.bounds.max.y);
                    if (top > float.MinValue) return top;
                }
                return transform.position.y + 1.95f * transform.lossyScale.y;
            }
        }

        /// <summary>Feeds the Animator Speed (relaxed walk/run) and MoveX/MoveZ (fight-stance stepping), smoothed.</summary>
        void UpdateLocomotion(float dt)
        {
            if (motor == null) motor = GetComponentInParent<FighterMotor>();
            Vector3 v = motor != null ? motor.MoveVelocity : Vector3.zero;
            // Moved by someone else (dragged by the collar): read the speed from how far he actually moved,
            // so his legs step along instead of sliding. Not while lifted off his feet.
            Vector3 rootPos = ownerRoot != null ? ownerRoot.position : transform.position;
            if (motor != null && motor.Frozen && dt > 0f)
            {
                Vector3 moved = (rootPos - lastRootPosition) / dt;
                moved.y = 0f;
                draggedVelocity = Vector3.Lerp(draggedVelocity, moved, 1f - Mathf.Exp(-10f * dt));
                v = (carried && !CarryWalks) || draggedVelocity.magnitude < 0.35f ? Vector3.zero : draggedVelocity;
            }
            lastRootPosition = rootPos;
            v.y = 0f;
            float m = v.magnitude;
            speed01 = Mathf.Clamp01(m / 6f);
            if (animator == null || animatorParams == null) return;

            // 0..0.5 = standing to walking, 0.5..1 = walking to running
            float norm = m <= WalkReference
                ? 0.5f * m / WalkReference
                : Mathf.Min(1f, 0.5f + 0.5f * (m - WalkReference) / (RunReference - WalkReference));
            Vector3 local = m > 0.05f ? transform.InverseTransformDirection(v / m) * norm : Vector3.zero;

            const float damp = 0.12f;
            if (animatorParams.Contains("Speed"))
                animator.SetFloat("Speed", m <= WalkReference ? 0.35f * m / WalkReference : Mathf.Min(1f, 0.35f + 0.45f * (m - WalkReference) / (RunReference - WalkReference)), damp, dt);
            if (animatorParams.Contains("MoveX")) animator.SetFloat("MoveX", local.x, damp, dt);
            if (animatorParams.Contains("MoveZ")) animator.SetFloat("MoveZ", local.z, damp, dt);
        }

        /// <summary>Wrestling grip and posture (hands on his body, crouch / lean), via IK. Real models only.</summary>
        public void SetGrapple(Fighter target, GripKind grip, float drop, float lean)
        {
            if (handIK != null) handIK.SetGrapple(target, grip, drop, lean);
        }

        /// <summary>Lets go of a wrestling grip gradually (posture and hands blend out over the time).</summary>
        public void ReleaseGripSlowly(float seconds)
        {
            if (handIK != null) handIK.ReleaseSlowly(seconds);
        }

        /// <summary>
        /// Knee or headbutt while holding someone. Real models do it with IK (feet planted, hands keep the
        /// grip); the placeholder body uses its pose. Returns when it connects.
        /// </summary>
        public float GrabStrike(GrabStrikeKind kind, float duration)
        {
            if (handIK != null)
            {
                handIK.Strike(kind, duration);
                return duration * 0.4f;
            }
            Play(kind == GrabStrikeKind.Knee ? FighterPose.Knee : FighterPose.Headbutt, duration, duration);
            return duration * 0.4f;
        }

        /// <summary>Takes the right hand off the collar for a moment so a punch can be thrown.</summary>
        public void FreeRightHand(float seconds)
        {
            if (handIK != null) handIK.FreeRightHand(seconds);
        }

        /// <summary>Hands on someone's collar (front) or shoulders (behind); null lets go.</summary>
        public void SetGrip(Fighter target, bool fromBehind)
        {
            if (handIK != null) handIK.SetGrip(target, fromBehind);
        }

        /// <summary>Head-over-heels roll in the travel direction (thrown, shoved hard, launched).</summary>
        public void Tumble(Vector3 worldDirection, float duration)
        {
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.01f) return;
            tumbleAxis = Vector3.Cross(Vector3.up, worldDirection.normalized);
            tumbleDuration = duration;
            tumbleTimer = duration;
        }

        bool carried;
        Quaternion carryRotation;

        /// <summary>
        /// Wrestling finishers: tilt the whole body around its hips (lifted across the shoulders, upside down
        /// for a powerbomb, ...). Rotation is in this fighter's local space. Call ClearCarry to stand back up.
        /// </summary>
        public void SetCarry(Quaternion localRotation)
        {
            carried = true;
            carryRotation = localRotation;
        }

        /// <summary>While carried (tilted by SetCarry) his feet are still on the ground: legs step along when he's moved.</summary>
        public bool CarryWalks { get; set; }

        public void ClearCarry()
        {
            CarryWalks = false;
            if (!carried) return;
            carried = false;
            if (body == null) return;
            body.localRotation = Quaternion.identity;
            body.localPosition = Vector3.zero;
        }

        public bool IsCarried => carried;

        void ApplyHipRotation(Quaternion rot)
        {
            Vector3 hipPoint = Vector3.up * 0.9f;
            body.localRotation = rot;
            body.localPosition = hipPoint - rot * hipPoint;
        }

        /// <summary>Cuts a tumble short (e.g. bounced off a wall back onto his feet).</summary>
        public void StopTumble()
        {
            if (tumbleTimer <= 0f) return;
            tumbleTimer = 0f;
            if (body == null) return;
            body.localRotation = Quaternion.identity;
            body.localPosition = Vector3.zero;
        }

        /// <summary>Rotates the whole body around its hips for a tumble; returns true while tumbling.</summary>
        bool UpdateTumble(float dt)
        {
            if (tumbleTimer <= 0f) return false;
            tumbleTimer -= dt;
            const float hip = 0.9f;
            if (tumbleTimer <= 0f)
            {
                body.localRotation = Quaternion.identity;
                body.localPosition = Vector3.zero;
                return false;
            }
            float t = 1f - tumbleTimer / tumbleDuration;
            float angle = 360f * Mathf.SmoothStep(0f, 1f, t);
            Vector3 localAxis = transform.InverseTransformDirection(tumbleAxis);
            Quaternion rot = Quaternion.AngleAxis(angle, localAxis);
            Vector3 hipPoint = Vector3.up * hip;
            body.localRotation = rot;
            body.localPosition = hipPoint - rot * hipPoint;
            return true;
        }

        /// <summary>Sets an Animator bool if the controller has it (e.g. "Holding" while grabbing, "Held" while grabbed).</summary>
        public void SetAnimatorBool(string parameter, bool value)
        {
            if (animator != null && animatorParams != null && animatorParams.Contains(parameter))
                animator.SetBool(parameter, value);
        }

        /// <summary>Back to a neutral standing pose (used on respawn, also resets the Animator).</summary>
        // ------------------------------------------------------------------ ragdoll

        public bool CanRagdoll => ragdoll != null;
        public bool IsRagdoll => ragdoll != null && ragdoll.Active;
        /// <summary>Ragdoll at rest (always true without a ragdoll).</summary>
        public bool RagdollSettled => ragdoll == null || !ragdoll.Active || ragdoll.Settled;
        public Vector3 RagdollVelocity => ragdoll != null ? ragdoll.Velocity : Vector3.zero;
        public Vector3 RagdollCenter => ragdoll != null && ragdoll.Active ? ragdoll.Hips.position : transform.position + Vector3.up;

        /// <summary>
        /// Goes limp and falls under physics with the given velocity (knockdowns, throws, slams, death).
        /// Keeps the exact pose the body is in, even mid-lift in a wrestling move. Calling it again while
        /// already limp adds to the motion (stomps).
        /// </summary>
        public void StartRagdoll(Vector3 velocity)
        {
            if (ragdoll == null) return;
            if (!ragdoll.Active)
            {
                ragdoll.CapturePose();
                carried = false;
                tumbleTimer = 0f;
                body.localRotation = Quaternion.identity;
                body.localPosition = Vector3.zero;
                ragdoll.RestorePose();
                if (handIK != null) handIK.SetGrip(null, false);
                // Detach from the character root, so the root can follow the body without dragging the bones.
                transform.SetParent(null, true);
            }
            // Gentler than the old scripted knockback: people fall, they don't fly across the street.
            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
            flat = Vector3.ClampMagnitude(flat * 0.6f, 6.5f);
            Vector3 v = flat + Vector3.up * Mathf.Clamp(velocity.y * 0.7f, -6f, 4.5f);
            Vector3 spin = flat.sqrMagnitude > 0.01f ? Vector3.Cross(Vector3.up, flat.normalized) * flat.magnitude * 0.8f : Vector3.zero;
            if (ragdoll.Active) ragdoll.Jolt(v);
            else ragdoll.Activate(v, spin);
        }

        /// <summary>
        /// Ends the ragdoll for a get-up: moves the character root under the body, reattaches the body and
        /// blends into the get-up animation. Returns true if he was lying on his back.
        /// </summary>
        public bool EndRagdoll()
        {
            if (ragdoll == null || !ragdoll.Active) return true;
            bool faceUp = ragdoll.FaceUp;
            ragdoll.GetUpPlacement(out var position, out var rotation);
            ragdoll.CapturePose();
            Reattach(position, rotation);
            ragdoll.RestorePose();
            ragdoll.Deactivate(blend: true);
            lying = false;
            return faceUp;
        }

        /// <summary>Snaps straight out of the ragdoll (respawn, reset).</summary>
        void StopRagdollInstant()
        {
            if (ragdoll == null || !ragdoll.Active) return;
            ragdoll.Deactivate(blend: false);
            Reattach(ownerRoot != null ? ownerRoot.position : transform.position, ownerRoot != null ? ownerRoot.rotation : transform.rotation);
        }

        void Reattach(Vector3 rootPosition, Quaternion rootRotation)
        {
            if (ownerRoot == null) return;
            ownerRoot.SetPositionAndRotation(rootPosition, rootRotation);
            transform.SetParent(ownerRoot, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        void LateUpdate()
        {
            // While limp, the (invisible) character root follows the body around, so targeting, health bars
            // and distance checks still work.
            if (ragdoll == null || !ragdoll.Active || ownerRoot == null) return;
            Vector3 p = ragdoll.Hips.position;
            float y = ownerRoot.position.y;
            if (Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out var hit, 4f, CombatUtil.WorldMask, QueryTriggerInteraction.Ignore))
                y = hit.point.y;
            ownerRoot.position = new Vector3(p.x, y, p.z);
        }

        public void ResetPose()
        {
            StopRagdollInstant();
            pose = FighterPose.Idle;
            poseTimer = 0f;
            lying = false;
            lie = 0f;
            telegraph = TelegraphColor.None;
            if (animator != null)
            {
                animator.Rebind();
                animator.Update(0f);
            }
        }

        public void SetLying(bool value)
        {
            lying = value;
        }

        public void Flinch(float amount = 1f)
        {
            flinch = Mathf.Max(flinch, amount);
        }

        public void Flash(float duration = 0.08f)
        {
            flashTimer = duration;
        }

        /// <summary>Red glow used by enemies to warn the player that an attack is coming (counter window).</summary>
        public void SetTelegraph(bool on)
        {
            telegraph = on ? TelegraphColor.Red : TelegraphColor.None;
        }

        public void SetTelegraph(TelegraphColor color)
        {
            telegraph = color;
        }

        static Color TelegraphTint(TelegraphColor c) =>
            c == TelegraphColor.Orange ? new Color(2.4f, 0.9f, 0.15f)
            : c == TelegraphColor.Yellow ? new Color(2.2f, 2.0f, 0.3f)
            : new Color(2.2f, 0.35f, 0.3f);

        /// <summary>Where a held weapon goes: the right hand bone, or the placeholder fist.</summary>
        public Transform RightHand
        {
            get
            {
                if (animator != null && animator.isHuman)
                {
                    var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                    if (hand != null) return hand;
                }
                return armR != null ? armR : transform;
            }
        }

        public bool IsPlaceholder => animator == null;

        // ------------------------------------------------------------------ animation

        void Update()
        {
            if (!built) return;
            float dt = Time.deltaTime;

            if (poseTimer > 0f)
            {
                poseTimer -= dt;
                if (poseTimer <= 0f) pose = FighterPose.Idle;
            }
            flinch = Mathf.MoveTowards(flinch, 0f, dt * 5f);
            lie = Mathf.MoveTowards(lie, lying ? 1f : 0f, dt * (lying ? 6f : 2.5f));

            UpdateMaterials(dt);
            UpdateLocomotion(dt);

            if (carried)
            {
                tumbleTimer = 0f;
                ApplyHipRotation(carryRotation);
                if (animator != null) SetAnimatorBool("Blocking", false);
                return;
            }
            bool tumbling = UpdateTumble(dt);

            if (animator != null)
            {
                SetAnimatorBool("Blocking", pose == FighterPose.Block && poseTimer > 0f);
                return;
            }
            if (tumbling) return;

            float k = 1f - Mathf.Exp(-22f * dt);

            // Whole-body tilt: lying down, flinching back, leaning into a run
            float lean = speed01 * 8f - flinch * 22f;
            if (pose == FighterPose.Dodge || pose == FighterPose.Roll) lean = 25f;
            if (pose == FighterPose.DodgeBack) lean = -15f;
            if (pose == FighterPose.StaggerBack) lean = -22f;
            if (pose == FighterPose.Suplex) lean = -70f;
            if (pose == FighterPose.Spear || pose == FighterPose.ShoulderBarge) lean = 35f;
            if (pose == FighterPose.ElbowDrop) lean = 50f;
            var bodyTarget = Quaternion.Euler(-90f * lie + lean * (1f - lie), 0f, 0f);
            body.localRotation = Quaternion.Slerp(body.localRotation, bodyTarget, k);

            // Walk cycle
            walkPhase += dt * Mathf.Lerp(0f, 11f, speed01);
            float swing = Mathf.Sin(walkPhase) * 35f * speed01;

            GetPoseTargets(out var aL, out var aR, out var lL, out var lR, out float twist, out float reachL, out float reachR);
            if (lL == Vector3.zero && lR == Vector3.zero && lie < 0.5f)
            {
                lL = new Vector3(swing, 0f, 0f);
                lR = new Vector3(-swing, 0f, 0f);
            }

            armL.localRotation = Quaternion.Slerp(armL.localRotation, Quaternion.Euler(aL), k);
            armR.localRotation = Quaternion.Slerp(armR.localRotation, Quaternion.Euler(aR), k);
            legL.localRotation = Quaternion.Slerp(legL.localRotation, Quaternion.Euler(lL), k);
            legR.localRotation = Quaternion.Slerp(legR.localRotation, Quaternion.Euler(lR), k);
            torso.localRotation = Quaternion.Slerp(torso.localRotation, Quaternion.Euler(0f, twist, 0f), k);

            armL.localPosition = Vector3.Lerp(armL.localPosition, new Vector3(-0.34f, 0.63f, reachL), k);
            armR.localPosition = Vector3.Lerp(armR.localPosition, new Vector3(0.34f, 0.63f, reachR), k);
        }

        void GetPoseTargets(out Vector3 aL, out Vector3 aR, out Vector3 lL, out Vector3 lR,
            out float twist, out float reachL, out float reachR)
        {
            // Arm hanging down = (0,0,0). x = -90 points it straight forward, -180 straight up.
            Vector3 guardL = new Vector3(-115f, 20f, 0f);
            Vector3 guardR = new Vector3(-115f, -20f, 0f);
            Vector3 restL = new Vector3(0f, 0f, -8f);
            Vector3 restR = new Vector3(0f, 0f, 8f);

            aL = inCombatStance ? guardL : restL;
            aR = inCombatStance ? guardR : restR;
            lL = lR = Vector3.zero;
            twist = 0f;
            reachL = reachR = 0f;

            switch (pose)
            {
                case FighterPose.Guard:
                    aL = guardL; aR = guardR; break;
                case FighterPose.Block:
                    aL = new Vector3(-155f, 30f, 0f); aR = new Vector3(-155f, -30f, 0f); reachL = reachR = 0.12f; break;
                case FighterPose.Jab:
                    aL = new Vector3(-92f, 8f, 0f); aR = guardR; reachL = 0.28f; twist = 15f; break;
                case FighterPose.Cross:
                case FighterPose.Counter:
                    aR = new Vector3(-92f, -8f, 0f); aL = guardL; reachR = 0.3f; twist = -25f; break;
                case FighterPose.Hook:
                    aL = new Vector3(-90f, 70f, 0f); aR = guardR; reachL = 0.15f; twist = 40f; break;
                case FighterPose.BodyBlow:
                    aR = new Vector3(-60f, -10f, 0f); aL = guardL; reachR = 0.3f; twist = -30f; break;
                case FighterPose.Haymaker:
                    aR = new Vector3(-95f, -45f, 0f); aL = new Vector3(-40f, 0f, -30f); reachR = 0.25f; twist = -50f; break;
                case FighterPose.Uppercut:
                    aR = new Vector3(-170f, -10f, 0f); aL = guardL; reachR = 0.2f; twist = -20f; break;
                case FighterPose.Backfist:
                    aL = new Vector3(-90f, -80f, 0f); aR = guardR; reachL = 0.1f; twist = 70f; break;
                case FighterPose.Kick:
                case FighterPose.FlyingKick:
                    lR = new Vector3(-88f, 0f, 0f); twist = -10f; break;
                case FighterPose.Jump:
                    lL = new Vector3(-50f, 0f, 0f); lR = new Vector3(-20f, 0f, 0f);
                    aL = new Vector3(-150f, 0f, -20f); aR = new Vector3(-150f, 0f, 20f); break;
                case FighterPose.Stomp:
                    lR = new Vector3(-35f, 0f, 0f); aL = new Vector3(-30f, 0f, -40f); aR = new Vector3(-30f, 0f, 40f); break;
                case FighterPose.Grab:
                    aL = new Vector3(-90f, 15f, 0f); aR = new Vector3(-90f, -15f, 0f); reachL = reachR = 0.15f; break;
                case FighterPose.Knee:
                    aL = new Vector3(-90f, 15f, 0f); aR = new Vector3(-90f, -15f, 0f); lR = new Vector3(-100f, 0f, 0f); break;
                case FighterPose.Throw:
                    aL = new Vector3(-80f, 60f, 0f); aR = new Vector3(-80f, -60f, 0f); twist = 80f; break;
                case FighterPose.Slam:
                    aL = new Vector3(-165f, 10f, 0f); aR = new Vector3(-165f, -10f, 0f); reachL = reachR = 0.2f; break;
                case FighterPose.Dodge:
                case FighterPose.DodgeLeft:
                case FighterPose.DodgeRight:
                case FighterPose.DodgeBack:
                case FighterPose.Roll:
                    aL = new Vector3(-60f, 20f, 0f); aR = new Vector3(-60f, -20f, 0f); break;
                case FighterPose.Windup:
                    aR = new Vector3(-140f, -50f, 0f); aL = guardL; reachR = -0.15f; twist = 35f; break;
                case FighterPose.Headbutt:
                    aL = new Vector3(-90f, 15f, 0f); aR = new Vector3(-90f, -15f, 0f); twist = 0f; break;
                case FighterPose.Push:
                    aL = new Vector3(-90f, 10f, 0f); aR = new Vector3(-90f, -10f, 0f); reachL = reachR = 0.35f; break;
                case FighterPose.Parry:
                    aL = new Vector3(-120f, 70f, 0f); aR = guardR; reachL = 0.15f; twist = 25f; break;
                case FighterPose.StaggerBack:
                case FighterPose.Flinch:
                    aL = new Vector3(-30f, 0f, -35f); aR = new Vector3(-30f, 0f, 35f); break;
                case FighterPose.ShoulderBarge:
                case FighterPose.Spear:
                    aL = new Vector3(-50f, 30f, 0f); aR = new Vector3(-70f, -20f, 0f); twist = 60f; break;
                case FighterPose.LegSweep:
                    lR = new Vector3(-20f, 0f, 70f); lL = new Vector3(-60f, 0f, 0f);
                    aL = new Vector3(-30f, 0f, -50f); aR = new Vector3(-30f, 0f, 50f); twist = 90f; break;
                case FighterPose.Clothesline:
                    aR = new Vector3(-90f, -90f, 0f); aL = new Vector3(-30f, 0f, -30f); twist = -30f; break;
                case FighterPose.BackElbow:
                    aR = new Vector3(-40f, 0f, 60f); aL = guardL; reachR = -0.2f; twist = -60f; break;
                case FighterPose.Lift:
                    aL = new Vector3(-170f, 15f, 0f); aR = new Vector3(-170f, -15f, 0f);
                    lL = new Vector3(-15f, 0f, 0f); lR = new Vector3(-15f, 0f, 0f); break;
                case FighterPose.Suplex:
                    aL = new Vector3(-180f, 10f, 0f); aR = new Vector3(-180f, -10f, 0f); break;
                case FighterPose.ElbowDrop:
                    aR = new Vector3(-20f, 0f, 90f); aL = new Vector3(-150f, 0f, -30f);
                    lL = new Vector3(-60f, 0f, 0f); lR = new Vector3(-30f, 0f, 0f); break;
                case FighterPose.Knockdown:
                case FighterPose.Dead:
                    aL = new Vector3(-170f, 0f, -20f); aR = new Vector3(-170f, 0f, 20f);
                    lL = new Vector3(0f, 0f, -10f); lR = new Vector3(0f, 0f, 10f); break;
            }
        }

        /// <summary>Real models keep their textures; flashes are done by tinting the base colour.</summary>
        void UpdateModelTint()
        {
            Color tint = Color.white;
            if (flashTimer > 0f)
                tint = new Color(2.2f, 2.2f, 2.2f);
            else if (telegraph != TelegraphColor.None && Mathf.Repeat(Time.time * 10f, 1f) < 0.6f)
                tint = TelegraphTint(telegraph);
            if (tint == currentTint) return;
            currentTint = tint;

            foreach (var r in modelRenderers)
            {
                if (r == null) continue;
                if (tint == Color.white)
                {
                    r.SetPropertyBlock(null);
                    continue;
                }
                r.GetPropertyBlock(tintBlock);
                tintBlock.SetColor("_BaseColor", tint);   // URP Lit
                tintBlock.SetColor("_Color", tint);       // Built-in Standard
                r.SetPropertyBlock(tintBlock);
            }
        }

        void UpdateMaterials(float dt)
        {
            flashTimer -= dt;
            if (modelRenderers != null)
            {
                UpdateModelTint();
                return;
            }
            if (originalMaterials == null) return;
            Material overrideMat = null;
            if (flashTimer > 0f)
                overrideMat = Palette.Get(Color.white, new Color(1f, 1f, 1f) * 1.5f);
            else if (telegraph != TelegraphColor.None && Mathf.Repeat(Time.time * 10f, 1f) < 0.6f)
            {
                Color t = TelegraphTint(telegraph);
                overrideMat = Palette.Get(new Color(Mathf.Min(1f, t.r * 0.45f), Mathf.Min(1f, t.g * 0.45f), Mathf.Min(1f, t.b * 0.45f)), t);
            }

            for (int i = 0; i < renderers.Count; i++)
            {
                if (renderers[i] == null) continue;
                var target = overrideMat != null ? overrideMat : originalMaterials[i];
                if (renderers[i].sharedMaterial != target)
                    renderers[i].sharedMaterial = target;
            }
        }
    }
}

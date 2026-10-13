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
        bool telegraph;
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
                tintBlock = new MaterialPropertyBlock();
                CacheAnimatorParams();
                SetLayerRecursively(model, gameObject.layer);
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

        void CacheAnimatorParams()
        {
            animatorParams = new HashSet<string>();
            if (animator == null) return;
            foreach (var p in animator.parameters)
                animatorParams.Add(p.name);
        }

        // ------------------------------------------------------------------ public API

        /// <summary>Plays a pose for the given time, then returns to idle/guard.</summary>
        public void Play(FighterPose newPose, float duration)
        {
            // Held poses (block, grab) are re-requested every frame: keep them running instead of restarting.
            if (newPose == pose && poseTimer > 0.05f)
            {
                poseTimer = Mathf.Max(poseTimer, duration);
                return;
            }
            pose = newPose;
            poseTimer = duration;
            if (animator != null && animatorParams != null && animatorParams.Contains(newPose.ToString()))
                animator.SetTrigger(newPose.ToString());
        }

        public void SetCombatStance(bool on)
        {
            inCombatStance = on;
            if (animator != null && animatorParams != null && animatorParams.Contains("Guard"))
                animator.SetBool("Guard", on);
        }

        public void SetLocomotion(float normalizedSpeed)
        {
            speed01 = Mathf.Clamp01(normalizedSpeed);
            if (animator != null && animatorParams != null && animatorParams.Contains("Speed"))
                animator.SetFloat("Speed", speed01);
        }

        /// <summary>Sets an Animator bool if the controller has it (e.g. "Holding" while grabbing, "Held" while grabbed).</summary>
        public void SetAnimatorBool(string parameter, bool value)
        {
            if (animator != null && animatorParams != null && animatorParams.Contains(parameter))
                animator.SetBool(parameter, value);
        }

        /// <summary>Back to a neutral standing pose (used on respawn, also resets the Animator).</summary>
        public void ResetPose()
        {
            pose = FighterPose.Idle;
            poseTimer = 0f;
            lying = false;
            lie = 0f;
            telegraph = false;
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
            telegraph = on;
        }

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

            if (animator != null)
            {
                SetAnimatorBool("Blocking", pose == FighterPose.Block && poseTimer > 0f);
                return;
            }

            float k = 1f - Mathf.Exp(-22f * dt);

            // Whole-body tilt: lying down, flinching back, leaning into a run
            float lean = speed01 * 8f - flinch * 22f;
            if (pose == FighterPose.Dodge) lean = 25f;
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
                    lR = new Vector3(-88f, 0f, 0f); twist = -10f; break;
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
                    aL = new Vector3(-60f, 20f, 0f); aR = new Vector3(-60f, -20f, 0f); break;
                case FighterPose.Windup:
                    aR = new Vector3(-140f, -50f, 0f); aL = guardL; reachR = -0.15f; twist = 35f; break;
                case FighterPose.Flinch:
                    aL = new Vector3(-30f, 0f, -35f); aR = new Vector3(-30f, 0f, 35f); break;
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
            else if (telegraph && Mathf.Repeat(Time.time * 10f, 1f) < 0.6f)
                tint = new Color(2.2f, 0.35f, 0.3f);
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
            else if (telegraph && Mathf.Repeat(Time.time * 10f, 1f) < 0.6f)
                overrideMat = Palette.Get(new Color(1f, 0.15f, 0.1f), new Color(2.5f, 0.2f, 0.1f));

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

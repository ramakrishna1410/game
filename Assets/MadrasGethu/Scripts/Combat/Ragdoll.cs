using System.Collections.Generic;
using UnityEngine;

namespace MadrasGethu
{
    /// <summary>
    /// Physics ragdoll for a Humanoid (Mixamo) model, built in code at startup.
    /// While animated, the body parts are kinematic and their colliders are off. When a fighter is
    /// knocked down, thrown, slammed or killed, the Animator is switched off and the body falls under
    /// physics. To get up, the final ragdoll pose is captured and blended smoothly into a
    /// get-up animation, so there is no snap.
    /// </summary>
    public class Ragdoll : MonoBehaviour
    {
        class Part
        {
            public Transform bone;
            public Rigidbody rb;
            public Collider col;
        }

        readonly List<Part> parts = new List<Part>();
        Animator animator;
        Transform hips, head, leftLeg, rightLeg;

        // Pose blending (ragdoll -> get-up animation)
        Transform[] blendBones;
        Quaternion[] savedLocal;
        Vector3 savedHipsPos;
        Quaternion savedHipsRot;
        float blendTime, blendDuration;

        float activeTime;
        float stillTime;

        public bool Active { get; private set; }
        public bool Blending => blendTime < blendDuration;
        public Transform Hips => hips;

        /// <summary>The body has come to rest on the ground.</summary>
        public bool Settled => Active && (stillTime > 0.35f || activeTime > 4f);

        public Vector3 Velocity => hips != null && Active ? GetVelocity(parts[0].rb) : Vector3.zero;

        /// <summary>Creates rigidbodies, colliders and joints for the main bones. Returns false if the rig is incomplete.</summary>
        public bool Build(Animator a)
        {
            animator = a;
            if (a == null || !a.isHuman) return false;
            hips = a.GetBoneTransform(HumanBodyBones.Hips);
            var chest = a.GetBoneTransform(HumanBodyBones.Chest);
            if (chest == null) chest = a.GetBoneTransform(HumanBodyBones.Spine);
            head = a.GetBoneTransform(HumanBodyBones.Head);
            var lUpperArm = a.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var lLowerArm = a.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            var lHand = a.GetBoneTransform(HumanBodyBones.LeftHand);
            var rUpperArm = a.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var rLowerArm = a.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var rHand = a.GetBoneTransform(HumanBodyBones.RightHand);
            var lUpperLeg = a.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            var lLowerLeg = a.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            var lFoot = a.GetBoneTransform(HumanBodyBones.LeftFoot);
            var rUpperLeg = a.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            var rLowerLeg = a.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            var rFoot = a.GetBoneTransform(HumanBodyBones.RightFoot);
            if (hips == null || chest == null || head == null || lUpperArm == null || lLowerArm == null || rUpperArm == null ||
                rLowerArm == null || lUpperLeg == null || lLowerLeg == null || rUpperLeg == null || rLowerLeg == null)
                return false;
            leftLeg = lUpperLeg;
            rightLeg = rUpperLeg;

            // Sizes are worked out in metres from the actual bone positions, so any model scale works.
            float hipWidth = Vector3.Distance(lUpperLeg.position, rUpperLeg.position);
            float torso = Vector3.Distance(hips.position, head.position);

            // Order matters: parents first (joints connect to the part already created for the parent bone).
            var hipsPart = AddPart(hips, null, 12f);
            AddBox(hipsPart, hips, (chest.position - hips.position) * 0.35f, new Vector3(hipWidth * 1.9f, torso * 0.28f, hipWidth * 1.2f));
            hips.gameObject.AddComponent<RagdollThud>().owner = this;
            var chestPart = AddPart(chest, hipsPart, 12f);
            chest.gameObject.AddComponent<RagdollThud>().owner = this;
            AddBox(chestPart, chest, (head.position - chest.position) * 0.35f, new Vector3(hipWidth * 2.2f, torso * 0.42f, hipWidth * 1.3f));
            SetLimits(chestPart, -20f, 20f, 20f, 15f);
            var headPart = AddPart(head, chestPart, 5f);
            AddSphere(headPart, head, Vector3.up * torso * 0.12f, torso * 0.16f);
            SetLimits(headPart, -40f, 25f, 30f, 25f);

            Limb(lUpperArm, lLowerArm, chestPart, 2.5f, 0.2f, -70f, 10f, 70f, 40f, out var lUpper);
            Limb(lLowerArm, lHand != null ? lHand : lLowerArm, lUpper, 1.5f, 0.18f, -120f, 0f, 10f, 10f, out _);
            Limb(rUpperArm, rLowerArm, chestPart, 2.5f, 0.2f, -70f, 10f, 70f, 40f, out var rUpper);
            Limb(rLowerArm, rHand != null ? rHand : rLowerArm, rUpper, 1.5f, 0.18f, -120f, 0f, 10f, 10f, out _);
            Limb(lUpperLeg, lLowerLeg, hipsPart, 7f, 0.22f, -20f, 70f, 30f, 20f, out var lThigh);
            Limb(lLowerLeg, lFoot != null ? lFoot : lLowerLeg, lThigh, 4f, 0.18f, -120f, 0f, 10f, 10f, out _);
            Limb(rUpperLeg, rLowerLeg, hipsPart, 7f, 0.22f, -20f, 70f, 30f, 20f, out var rThigh);
            Limb(rLowerLeg, rFoot != null ? rFoot : rLowerLeg, rThigh, 4f, 0.18f, -120f, 0f, 10f, 10f, out _);

            // Every humanoid bone takes part in the get-up blend.
            var bones = new List<Transform>();
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var b = a.GetBoneTransform((HumanBodyBones)i);
                if (b != null && b != hips) bones.Add(b);
            }
            blendBones = bones.ToArray();
            savedLocal = new Quaternion[blendBones.Length];
            blendDuration = 0.5f;
            blendTime = blendDuration;

            SetPhysics(false);
            return true;
        }

        Part AddPart(Transform bone, Part parent, float mass)
        {
            var part = new Part { bone = bone, rb = bone.gameObject.AddComponent<Rigidbody>() };
            part.rb.mass = mass;
            part.rb.solverIterations = 12;
            part.rb.maxDepenetrationVelocity = 2f;   // never shoot out of an overlap
            part.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            bone.gameObject.layer = CombatUtil.RagdollLayer;
            if (parent != null)
            {
                var joint = bone.gameObject.AddComponent<CharacterJoint>();
                joint.connectedBody = parent.rb;
                joint.enableProjection = true;
                joint.enablePreprocessing = false;
                // Twist around the bone's length: pick the bone's local axis that points to its child.
                joint.axis = Vector3.right;
                joint.swingAxis = Vector3.forward;
            }
            parts.Add(part);
            return part;
        }

        static void SetLimits(Part part, float lowTwist, float highTwist, float swing1, float swing2)
        {
            var joint = part.bone.GetComponent<CharacterJoint>();
            if (joint == null) return;
            joint.lowTwistLimit = new SoftJointLimit { limit = lowTwist };
            joint.highTwistLimit = new SoftJointLimit { limit = highTwist };
            joint.swing1Limit = new SoftJointLimit { limit = swing1 };
            joint.swing2Limit = new SoftJointLimit { limit = swing2 };
        }

        void Limb(Transform bone, Transform end, Part parent, float mass, float thickness,
            float lowTwist, float highTwist, float swing1, float swing2, out Part part)
        {
            part = AddPart(bone, parent, mass);
            Vector3 toEnd = end != bone ? end.position - bone.position : bone.TransformDirection(Vector3.up) * 0.25f;
            float length = toEnd.magnitude;

            // Capsule along the bone, in the bone's local space.
            Vector3 localDir = bone.InverseTransformDirection(toEnd.normalized);
            int axis = MaxAxis(localDir);
            float scale = Mathf.Max(0.0001f, AxisScale(bone.lossyScale, axis));
            var capsule = bone.gameObject.AddComponent<CapsuleCollider>();
            capsule.direction = axis;
            capsule.center = bone.InverseTransformPoint(bone.position + toEnd * 0.5f);
            capsule.height = length / scale;
            capsule.radius = length * thickness / scale;
            part.col = capsule;

            var joint = bone.GetComponent<CharacterJoint>();
            if (joint != null)
            {
                joint.axis = AxisVector(axis);
                joint.swingAxis = AxisVector((axis + 1) % 3);
            }
            SetLimits(part, lowTwist, highTwist, swing1, swing2);
        }

        void AddBox(Part part, Transform bone, Vector3 worldOffset, Vector3 worldSize)
        {
            var box = bone.gameObject.AddComponent<BoxCollider>();
            box.center = bone.InverseTransformPoint(bone.position + worldOffset);
            Vector3 s = bone.lossyScale;
            box.size = new Vector3(worldSize.x / Mathf.Max(0.0001f, Mathf.Abs(s.x)), worldSize.y / Mathf.Max(0.0001f, Mathf.Abs(s.y)),
                worldSize.z / Mathf.Max(0.0001f, Mathf.Abs(s.z)));
            part.col = box;
        }

        void AddSphere(Part part, Transform bone, Vector3 worldOffset, float worldRadius)
        {
            var sphere = bone.gameObject.AddComponent<SphereCollider>();
            sphere.center = bone.InverseTransformPoint(bone.position + worldOffset);
            sphere.radius = worldRadius / Mathf.Max(0.0001f, Mathf.Abs(bone.lossyScale.x));
            part.col = sphere;
        }

        static int MaxAxis(Vector3 v)
        {
            float x = Mathf.Abs(v.x), y = Mathf.Abs(v.y), z = Mathf.Abs(v.z);
            return x > y && x > z ? 0 : y > z ? 1 : 2;
        }

        static float AxisScale(Vector3 s, int axis) => Mathf.Abs(axis == 0 ? s.x : axis == 1 ? s.y : s.z);
        static Vector3 AxisVector(int axis) => axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;

        void SetPhysics(bool on)
        {
            foreach (var p in parts)
            {
                p.rb.isKinematic = !on;
                // Interpolation only while falling: on kinematic bones driven by the Animator it causes jitter.
                p.rb.interpolation = on ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
                if (p.col != null) p.col.enabled = on;
            }
        }

        static Vector3 GetVelocity(Rigidbody rb)
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        static void SetVelocity(Rigidbody rb, Vector3 v)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = v;
#else
            rb.velocity = v;
#endif
        }

        // ------------------------------------------------------------------ going limp

        /// <summary>Goes limp from the current pose, moving with the given velocity (m/s).</summary>
        public void Activate(Vector3 velocity, Vector3 spin)
        {
            if (!Active)
            {
                Active = true;
                activeTime = 0f;
                stillTime = 0f;
                blendTime = blendDuration;
                animator.enabled = false;
                SetPhysics(true);
                LiftOutOfGround();
                foreach (var p in parts)
                {
                    SetVelocity(p.rb, velocity);
                    p.rb.angularVelocity = spin;
                }
            }
            else
            {
                // Already down (stomped, elbow-dropped): add to what he's doing.
                foreach (var p in parts) p.rb.AddForce(velocity, ForceMode.VelocityChange);
            }
        }

        /// <summary>
        /// If any body part starts inside the road (a slam placed him a little too low), raise the whole body
        /// just above it. Otherwise the physics engine pushes him out violently and he shoots across the street.
        /// </summary>
        void LiftOutOfGround()
        {
            Physics.SyncTransforms();
            float lowest = float.MaxValue;
            foreach (var p in parts)
                if (p.col != null) lowest = Mathf.Min(lowest, p.col.bounds.min.y);
            if (lowest == float.MaxValue) return;
            float ground = transform.position.y;
            if (Physics.Raycast(hips.position + Vector3.up * 1.5f, Vector3.down, out var hit, 4f, CombatUtil.WorldMask, QueryTriggerInteraction.Ignore))
                ground = hit.point.y;
            if (lowest < ground + 0.02f)
            {
                hips.position += Vector3.up * (ground + 0.02f - lowest);
                Physics.SyncTransforms();
            }
        }

        public Vector3 HeadPosition => head != null ? head.position : transform.position;

        /// <summary>Punched while lying there: the head snaps to the side.</summary>
        public void HeadJolt(Vector3 velocity)
        {
            if (!Active || parts.Count < 3) return;
            parts[2].rb.AddForce(velocity, ForceMode.VelocityChange);
            stillTime = 0f;
        }

        /// <summary>Hit while lying there (stomp): a jolt to the torso.</summary>
        public void Jolt(Vector3 velocity)
        {
            if (!Active) return;
            parts[0].rb.AddForce(velocity, ForceMode.VelocityChange);
            if (parts.Count > 1) parts[1].rb.AddForce(velocity, ForceMode.VelocityChange);
            stillTime = 0f;
        }

        void FixedUpdate()
        {
            if (!Active) return;
            activeTime += Time.fixedDeltaTime;
            float speed = GetVelocity(parts[0].rb).magnitude;
            stillTime = speed < 0.35f && activeTime > 0.4f ? stillTime + Time.fixedDeltaTime : 0f;
        }

        /// <summary>Lying face up (on his back)?</summary>
        public bool FaceUp
        {
            get
            {
                Vector3 up = (head.position - hips.position).normalized;
                Vector3 right = (rightLeg.position - leftLeg.position).normalized;
                Vector3 front = Vector3.Cross(right, up);
                return front.y > 0f;
            }
        }

        /// <summary>Where the get-up animation should start: on the ground under the hips, facing the right way.</summary>
        public void GetUpPlacement(out Vector3 position, out Quaternion rotation)
        {
            position = hips.position;
            if (Physics.Raycast(hips.position + Vector3.up * 0.5f, Vector3.down, out var hit, 4f, CombatUtil.WorldMask, QueryTriggerInteraction.Ignore))
                position.y = hit.point.y;
            else
                position.y = transform.position.y;
            // Mixamo get-ups: on the back the feet point forward; face down the head points forward.
            Vector3 headDir = head.position - hips.position;
            headDir.y = 0f;
            if (headDir.sqrMagnitude < 0.0001f) headDir = transform.forward;
            Vector3 forward = FaceUp ? -headDir : headDir;
            rotation = Quaternion.LookRotation(forward.normalized);
        }

        /// <summary>Remembers the lying pose so the get-up animation can start from it.</summary>
        public void CapturePose()
        {
            savedHipsPos = hips.position;
            savedHipsRot = hips.rotation;
            for (int i = 0; i < blendBones.Length; i++) savedLocal[i] = blendBones[i].localRotation;
        }

        /// <summary>Puts the captured pose back on the bones (after the character root has moved).</summary>
        public void RestorePose()
        {
            hips.SetPositionAndRotation(savedHipsPos, savedHipsRot);
            for (int i = 0; i < blendBones.Length; i++) blendBones[i].localRotation = savedLocal[i];
        }

        /// <summary>Physics off, Animator back on, and blend from the lying pose into the animation.</summary>
        public void Deactivate(bool blend)
        {
            if (!Active) return;
            Active = false;
            SetPhysics(false);
            animator.enabled = true;
            blendTime = blend ? 0f : blendDuration;
        }

        void LateUpdate()
        {
            if (Active || blendTime >= blendDuration) return;
            blendTime += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(blendTime / blendDuration));
            // The Animator has just written the get-up pose; pull it towards the ragdoll pose, less each frame.
            hips.position = Vector3.Lerp(savedHipsPos, hips.position, t);
            hips.rotation = Quaternion.Slerp(savedHipsRot, hips.rotation, t);
            for (int i = 0; i < blendBones.Length; i++)
                blendBones[i].localRotation = Quaternion.Slerp(savedLocal[i], blendBones[i].localRotation, t);
        }
    }

    /// <summary>The thud of a limp body hitting the road (or a wall).</summary>
    public class RagdollThud : MonoBehaviour
    {
        public Ragdoll owner;
        static float lastThud;

        void OnCollisionEnter(Collision c)
        {
            if (owner == null || !owner.Active) return;
            float speed = c.relativeVelocity.magnitude;
            if (speed < 2.2f || Time.time - lastThud < 0.12f) return;
            lastThud = Time.time;
            Vector3 p = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
            GameAudio.Play(Sfx.BodyFall, p, Mathf.Clamp(speed / 6f, 0.4f, 1f));
        }
    }
}

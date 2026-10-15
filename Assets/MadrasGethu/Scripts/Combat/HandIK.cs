using UnityEngine;

namespace MadrasGethu
{
    /// <summary>Where the hero's hands go on the other man's body.</summary>
    public enum GripKind { Collar, Waist, ChestAndThigh, Thighs }

    /// <summary>
    /// Puts the hero's hands on the opponent's shirt collar while grabbing (front) or on his
    /// shoulders/neck (from behind), whatever animation is playing, and turns the head to look at him.
    /// Uses Unity's humanoid Animator IK; needs "IK Pass" on the Animator layer (the setup tool turns it on).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class HandIK : MonoBehaviour
    {
        public float blendSpeed = 9f;

        Animator animator;
        Fighter target;
        bool fromBehind;
        float weight;
        float targetWeight;

        // Grapple mode (wrestling moves): hands on chosen body parts, whole-body crouch / lean, feet planted.
        GripKind grip = GripKind.Collar;
        float drop, lean;              // metres (negative = crouch), degrees (positive = bend forward)
        bool feetPinned;
        Vector3 footLLocal, footRLocal;
        Quaternion footLRotLocal, footRRotLocal;
        float freeRightUntil;          // right hand off the collar while it throws a punch

        void Awake() => animator = GetComponent<Animator>();

        /// <summary>Start (target != null) or stop (null) gripping someone.</summary>
        public void SetGrip(Fighter who, bool behind)
        {
            if (who != null)
            {
                target = who;
                fromBehind = behind;
                targetWeight = 1f;
                grip = GripKind.Collar;
                drop = lean = 0f;
            }
            else
            {
                targetWeight = 0f;
                grip = GripKind.Collar;
                drop = lean = 0f;
                feetPinned = false;
            }
        }

        /// <summary>Wrestling: grip these body parts and hold this posture (call every frame while it changes).</summary>
        public void SetGrapple(Fighter who, GripKind kind, float bodyDrop, float bodyLean)
        {
            if (who != target || targetWeight <= 0f) feetPinned = false;
            target = who;
            targetWeight = 1f;
            grip = kind;
            drop = bodyDrop;
            lean = bodyLean;
        }

        /// <summary>Lets the right hand go for a moment (a punch while holding him by the collar).</summary>
        public void FreeRightHand(float seconds) => freeRightUntil = Time.time + seconds;

        void Update()
        {
            weight = Mathf.MoveTowards(weight, targetWeight, blendSpeed * Time.deltaTime);
            if (weight <= 0f && targetWeight <= 0f) target = null;
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || !animator.isHuman) return;
            if (target == null || weight <= 0.001f)
            {
                animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f);
                animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
                animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0f);
                animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0f);
                animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
                animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
                animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
                animator.SetLookAtWeight(0f);
                return;
            }

            Vector3 left, right, head;
            if (grip == GripKind.Collar) GetGripPoints(target, fromBehind, out left, out right, out head);
            else GetBodyGrip(target, grip, transform, out left, out right, out head);

            // With an upper-body layer, OnAnimatorIK runs once per layer: body and feet on the base layer,
            // hands and head on the top layer (which is applied last, so its hand pose would win anyway).
            bool baseLayer = layerIndex == 0;
            bool topLayer = layerIndex == animator.layerCount - 1;

            // Posture: crouch / rise and lean the whole upper body; legs bend to keep the feet where they were.
            bool posture = grip != GripKind.Collar || Mathf.Abs(drop) > 0.001f || Mathf.Abs(lean) > 0.001f;
            if (baseLayer && posture)
            {
                if (!feetPinned)
                {
                    feetPinned = true;
                    footLLocal = transform.InverseTransformPoint(animator.GetIKPosition(AvatarIKGoal.LeftFoot));
                    footRLocal = transform.InverseTransformPoint(animator.GetIKPosition(AvatarIKGoal.RightFoot));
                    footLRotLocal = Quaternion.Inverse(transform.rotation) * animator.GetIKRotation(AvatarIKGoal.LeftFoot);
                    footRRotLocal = Quaternion.Inverse(transform.rotation) * animator.GetIKRotation(AvatarIKGoal.RightFoot);
                }
                animator.bodyPosition += Vector3.up * drop * weight;
                animator.bodyRotation = Quaternion.AngleAxis(lean * weight, transform.right) * animator.bodyRotation;
                animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, weight);
                animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, weight);
                animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, weight);
                animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, weight);
                animator.SetIKPosition(AvatarIKGoal.LeftFoot, transform.TransformPoint(footLLocal));
                animator.SetIKPosition(AvatarIKGoal.RightFoot, transform.TransformPoint(footRLocal));
                animator.SetIKRotation(AvatarIKGoal.LeftFoot, transform.rotation * footLRotLocal);
                animator.SetIKRotation(AvatarIKGoal.RightFoot, transform.rotation * footRRotLocal);
            }
            else if (baseLayer)
            {
                animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f);
                animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
                animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0f);
                animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0f);
            }

            // Palms face into the grip: towards the target's spine.
            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f) toTarget = transform.forward;
            Quaternion palms = Quaternion.LookRotation(toTarget.normalized, Vector3.up);

            if (!topLayer) return;
            float rightWeight = Time.time < freeRightUntil ? 0f : weight;
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, weight);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, rightWeight);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, left);
            animator.SetIKPosition(AvatarIKGoal.RightHand, right);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, weight * 0.5f);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, rightWeight * 0.5f);
            animator.SetIKRotation(AvatarIKGoal.LeftHand, palms * Quaternion.Euler(0f, 0f, 90f));
            animator.SetIKRotation(AvatarIKGoal.RightHand, palms * Quaternion.Euler(0f, 0f, -90f));

            animator.SetLookAtWeight(weight * 0.8f, 0.15f, 0.9f, 0f, 0.6f);
            animator.SetLookAtPosition(head);
        }

        /// <summary>
        /// Hand targets on the other man's actual bones: around his waist, one on the chest and one under the
        /// thigh (body slam), or under both thighs (powerbomb). Each hand takes the point on its own side.
        /// </summary>
        public static void GetBodyGrip(Fighter who, GripKind kind, Transform hero, out Vector3 heroLeftHand, out Vector3 heroRightHand, out Vector3 head)
        {
            Transform t = who.transform;
            Vector3 hips = t.position + Vector3.up * 0.95f, chest = t.position + Vector3.up * 1.3f;
            Vector3 thighL = hips - t.right * 0.12f + Vector3.down * 0.25f, thighR = hips + t.right * 0.12f + Vector3.down * 0.25f;
            Vector3 side = t.right;
            head = t.position + Vector3.up * 1.65f;
            var anim = who.Visual != null ? who.Visual.animator : null;
            if (anim != null && anim.isHuman)
            {
                var h = anim.GetBoneTransform(HumanBodyBones.Hips);
                var c = anim.GetBoneTransform(HumanBodyBones.Chest) ?? anim.GetBoneTransform(HumanBodyBones.Spine);
                var hd = anim.GetBoneTransform(HumanBodyBones.Head);
                var ul = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                var ll = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                var ur = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                var lr = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                if (h != null) hips = h.position;
                if (c != null) chest = c.position;
                if (hd != null) head = hd.position;
                if (ul != null && ll != null) thighL = Vector3.Lerp(ul.position, ll.position, 0.45f);
                if (ur != null && lr != null) thighR = Vector3.Lerp(ur.position, lr.position, 0.45f);
                if (ul != null && ur != null) side = (ur.position - ul.position).normalized;
            }

            Vector3 a, b;
            switch (kind)
            {
                case GripKind.ChestAndThigh:
                    a = chest;
                    b = Vector3.Distance(thighL, hero.position) < Vector3.Distance(thighR, hero.position) ? thighL : thighR;
                    break;
                case GripKind.Thighs:
                    a = thighL; b = thighR;
                    break;
                default:   // waist: both sides of the hips, a little round towards his back
                    a = hips - side * 0.17f;
                    b = hips + side * 0.17f;
                    break;
            }
            // Left hand takes whichever point is on the hero's left.
            bool aIsLeft = Vector3.Dot(a - hero.position, hero.right) < Vector3.Dot(b - hero.position, hero.right);
            heroLeftHand = aIsLeft ? a : b;
            heroRightHand = aIsLeft ? b : a;
        }

        /// <summary>World positions of the collar (or shoulders from behind) on someone's body.</summary>
        public static void GetGripPoints(Fighter who, bool behind, out Vector3 heroLeftHand, out Vector3 heroRightHand, out Vector3 head)
        {
            Transform t = who.transform;
            Vector3 neckPos = t.position + Vector3.up * (who.height * 0.82f);
            Vector3 chestPos = t.position + Vector3.up * (who.height * 0.7f);
            head = t.position + Vector3.up * (who.height * 0.92f);

            var anim = who.Visual != null ? who.Visual.animator : null;
            if (anim != null && anim.isHuman)
            {
                var neck = anim.GetBoneTransform(HumanBodyBones.Neck);
                var chest = anim.GetBoneTransform(HumanBodyBones.UpperChest) ?? anim.GetBoneTransform(HumanBodyBones.Chest);
                var headBone = anim.GetBoneTransform(HumanBodyBones.Head);
                if (neck != null) neckPos = neck.position;
                if (chest != null) chestPos = chest.position;
                if (headBone != null) head = headBone.position;
            }

            Vector3 collar = Vector3.Lerp(chestPos, neckPos, 0.65f);
            Vector3 right = t.right;
            Vector3 forward = t.forward;

            if (!behind)
            {
                // Facing him: my left hand ends up on his right side of the collar, and vice versa.
                heroLeftHand = collar + right * 0.1f + forward * 0.12f;
                heroRightHand = collar - right * 0.1f + forward * 0.12f;
            }
            else
            {
                // Behind him: hands on the back of the collar / shoulders, same sides as his.
                heroLeftHand = collar - right * 0.16f - forward * 0.1f;
                heroRightHand = collar + right * 0.16f - forward * 0.1f;
            }
        }
    }
}

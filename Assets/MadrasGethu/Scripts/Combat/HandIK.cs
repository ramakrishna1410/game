using UnityEngine;

namespace MadrasGethu
{
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

        void Awake() => animator = GetComponent<Animator>();

        /// <summary>Start (target != null) or stop (null) gripping someone.</summary>
        public void SetGrip(Fighter who, bool behind)
        {
            if (who != null)
            {
                target = who;
                fromBehind = behind;
                targetWeight = 1f;
            }
            else
            {
                targetWeight = 0f;
            }
        }

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
                animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
                animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
                animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
                animator.SetLookAtWeight(0f);
                return;
            }

            GetGripPoints(target, fromBehind, out Vector3 left, out Vector3 right, out Vector3 head);

            // Palms face into the grip: towards the target's spine.
            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f) toTarget = transform.forward;
            Quaternion palms = Quaternion.LookRotation(toTarget.normalized, Vector3.up);

            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, weight);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, weight);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, left);
            animator.SetIKPosition(AvatarIKGoal.RightHand, right);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, weight * 0.5f);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, weight * 0.5f);
            animator.SetIKRotation(AvatarIKGoal.LeftHand, palms * Quaternion.Euler(0f, 0f, 90f));
            animator.SetIKRotation(AvatarIKGoal.RightHand, palms * Quaternion.Euler(0f, 0f, -90f));

            animator.SetLookAtWeight(weight * 0.8f, 0.15f, 0.9f, 0f, 0.6f);
            animator.SetLookAtPosition(head);
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

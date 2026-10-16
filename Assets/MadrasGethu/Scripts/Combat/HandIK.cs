using UnityEngine;

namespace MadrasGethu
{
    /// <summary>Strikes done procedurally while holding someone (no animation file needed).</summary>
    public enum GrabStrikeKind { None, Knee, Headbutt, EarClap }

    /// <summary>Where the hero's hands go on the other man's body.</summary>
    public enum GripKind
    {
        Collar, Waist, ChestAndThigh, Thighs,
        Neck,       // chokeslam: right hand on his throat, left on his belt
        Headlock,   // ice box: right arm over his neck, left hand on his back
        Clinch,     // Thai clinch: both hands behind his head, pulling it down
        Choke,      // choke from behind: right forearm across his throat, left hand behind his head
        ClawArm,    // being choked: both hands pulling at the choking forearm
        Ankles,     // giant swing: both hands round his lower legs
        Wrist,      // arm twist: his right wrist and elbow
        PipeChoke,  // pipe choke from behind: both hands either side of his neck, the pipe across his throat
        ClawPipe,   // being pipe-choked: both hands on the pipe either side of his own throat
    }

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
        float freeBothUntil;           // both hands off him (punching down from the mount)

        // Knee / headbutt while holding: a quick out-and-back curve applied with IK.
        GrabStrikeKind strikeKind;
        float strikeStart, strikeDuration;

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

        /// <summary>Knee to the stomach or headbutt, done with IK so the feet stay planted and the hands keep their grip.</summary>
        public void Strike(GrabStrikeKind kind, float duration)
        {
            strikeKind = kind;
            strikeStart = Time.time;
            strikeDuration = Mathf.Max(0.1f, duration);
        }

        /// <summary>0..1..0 over the strike: fast out (40%), slower back.</summary>
        float StrikeAmount
        {
            get
            {
                if (strikeKind == GrabStrikeKind.None) return 0f;
                float t = (Time.time - strikeStart) / strikeDuration;
                if (t >= 1f) { strikeKind = GrabStrikeKind.None; return 0f; }
                float c = t < 0.4f ? t / 0.4f : 1f - (t - 0.4f) / 0.6f;
                return c * c * (3f - 2f * c);
            }
        }

        /// <summary>Lets the right hand go for a moment (a punch while holding him by the collar).</summary>
        public void FreeRightHand(float seconds) => freeRightUntil = Time.time + seconds;

        /// <summary>Both hands off him for a moment, keeping the posture (punching down from the mount).</summary>
        public void FreeHands(float seconds) => freeBothUntil = Time.time + seconds;

        float slowReleaseSpeed;

        /// <summary>Lets go over a short time (after a slam the hero straightens up instead of popping up).</summary>
        public void ReleaseSlowly(float seconds)
        {
            targetWeight = 0f;
            slowReleaseSpeed = 1f / Mathf.Max(0.05f, seconds);
        }

        // Posture on its own (no hands on anyone): the rowdy bent over at the waist with his feet planted.
        bool postureOnly;
        float postureWeight;

        /// <summary>Bend at the waist / crouch with the feet staying where they are, hands free (being pushed into an ice box).</summary>
        public void SetPosture(float bodyDrop, float bodyLean)
        {
            if (target != null) return;   // gripping someone already uses the posture
            if (!postureOnly && postureWeight <= 0f) feetPinned = false;
            postureOnly = true;
            drop = bodyDrop;
            lean = bodyLean;
        }

        public void ClearPosture() => postureOnly = false;

        /// <summary>Take the feet's planted spots again from the current pose (after a big change of position).</summary>
        public void RepinFeet() => feetPinned = false;

        void Update()
        {
            postureWeight = Mathf.MoveTowards(postureWeight, postureOnly ? 1f : 0f, 6f * Time.deltaTime);
            if (!postureOnly && postureWeight <= 0f && target == null && weight <= 0f) { drop = lean = 0f; }
            float speed = targetWeight <= 0f && slowReleaseSpeed > 0f ? slowReleaseSpeed : blendSpeed;
            weight = Mathf.MoveTowards(weight, targetWeight, speed * Time.deltaTime);
            if (targetWeight > 0f || weight <= 0f) slowReleaseSpeed = 0f;
            if (weight <= 0f && targetWeight <= 0f) target = null;
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || !animator.isHuman) return;
            if (target == null && postureWeight > 0.001f)
            {
                if (layerIndex == 0)
                {
                    if (!feetPinned)
                    {
                        feetPinned = true;
                        footLLocal = transform.InverseTransformPoint(animator.GetIKPosition(AvatarIKGoal.LeftFoot));
                        footRLocal = transform.InverseTransformPoint(animator.GetIKPosition(AvatarIKGoal.RightFoot));
                        footLRotLocal = Quaternion.Inverse(transform.rotation) * animator.GetIKRotation(AvatarIKGoal.LeftFoot);
                        footRRotLocal = Quaternion.Inverse(transform.rotation) * animator.GetIKRotation(AvatarIKGoal.RightFoot);
                    }
                    float w = postureWeight;
                    animator.bodyPosition += Vector3.up * drop * w;
                    animator.bodyRotation = Quaternion.AngleAxis(lean * w, transform.right) * animator.bodyRotation;
                    animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, w);
                    animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, w);
                    animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, w);
                    animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, w);
                    animator.SetIKPosition(AvatarIKGoal.LeftFoot, transform.TransformPoint(footLLocal));
                    animator.SetIKPosition(AvatarIKGoal.RightFoot, transform.TransformPoint(footRLocal));
                    animator.SetIKRotation(AvatarIKGoal.LeftFoot, transform.rotation * footLRotLocal);
                    animator.SetIKRotation(AvatarIKGoal.RightFoot, transform.rotation * footRRotLocal);
                }
                animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
                animator.SetLookAtWeight(0f);
                return;
            }
            if (target == null || weight <= 0.001f)
            {
                animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f);
                animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
                animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0f);
                animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0f);
                if (layerIndex == 0) ApplyStrike();   // a knee in a combo works without holding anyone
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

            if (baseLayer) ApplyStrike();

            // Palms face into the grip: towards the target's spine.
            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f) toTarget = transform.forward;
            Quaternion palms = Quaternion.LookRotation(toTarget.normalized, Vector3.up);

            if (!topLayer) return;

            // Ear clap: both open palms swing out wide and slap in on his ears.
            if (strikeKind == GrabStrikeKind.EarClap)
            {
                float c = StrikeAmount;
                Vector3 side = transform.right;
                Vector3 wideL = head - side * 0.55f + Vector3.up * 0.05f, wideR = head + side * 0.55f + Vector3.up * 0.05f;
                Vector3 earL = head - side * 0.11f, earR = head + side * 0.11f;
                float swing = Mathf.Sin(Mathf.Clamp01(c) * Mathf.PI);
                left = Vector3.Lerp(Vector3.Lerp(left, wideL, swing), earL, c * c);
                right = Vector3.Lerp(Vector3.Lerp(right, wideR, swing), earR, c * c);
            }

            float handWeight = Time.time < freeBothUntil ? 0f : weight;
            float rightWeight = Time.time < freeRightUntil ? 0f : handWeight;
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, handWeight);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, rightWeight);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, left);
            animator.SetIKPosition(AvatarIKGoal.RightHand, right);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, handWeight * 0.5f);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, rightWeight * 0.5f);
            animator.SetIKRotation(AvatarIKGoal.LeftHand, palms * Quaternion.Euler(0f, 0f, 90f));
            animator.SetIKRotation(AvatarIKGoal.RightHand, palms * Quaternion.Euler(0f, 0f, -90f));

            animator.SetLookAtWeight(weight * 0.8f, 0.15f, 0.9f, 0f, 0.6f);
            if (strikeKind == GrabStrikeKind.Headbutt)
                animator.SetLookAtWeight(weight * 0.8f, 0.4f, 1f, 0f, 0.6f);
            animator.SetLookAtPosition(head);
        }

        void ApplyStrike()
        {
            float c = StrikeAmount;
            if (c <= 0f) return;
            if (strikeKind == GrabStrikeKind.Knee)
            {
                // Right knee drives up into his stomach; a slight lean back for balance.
                Vector3 raised = transform.TransformPoint(new Vector3(0.12f, 0.6f, 0.45f));
                Vector3 planted = animator.GetIKPosition(AvatarIKGoal.RightFoot);
                animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 1f);
                animator.SetIKPosition(AvatarIKGoal.RightFoot, Vector3.Lerp(planted, raised, c));
                animator.SetIKHintPositionWeight(AvatarIKHint.RightKnee, c);
                animator.SetIKHintPosition(AvatarIKHint.RightKnee, transform.TransformPoint(new Vector3(0.15f, 0.9f, 0.8f)));
                animator.bodyRotation = Quaternion.AngleAxis(-8f * c, transform.right) * animator.bodyRotation;
            }
            else if (strikeKind == GrabStrikeKind.Headbutt)
            {
                // Head and chest snap forward into his face, a little dip, then back.
                animator.bodyRotation = Quaternion.AngleAxis(28f * c, transform.right) * animator.bodyRotation;
                animator.bodyPosition += transform.forward * 0.1f * c + Vector3.down * 0.05f * c;
            }
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
            Vector3 neck = t.position + Vector3.up * 1.5f;
            Vector3 ankleL = t.position - t.right * 0.12f + Vector3.up * 0.2f, ankleR = t.position + t.right * 0.12f + Vector3.up * 0.2f;
            Vector3 wrist = t.position + t.right * 0.3f + Vector3.up * 1.0f, elbow = t.position + t.right * 0.25f + Vector3.up * 1.2f;
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
                var nk = anim.GetBoneTransform(HumanBodyBones.Neck);
                var fl = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                var fr = anim.GetBoneTransform(HumanBodyBones.RightFoot);
                var la = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
                var hn = anim.GetBoneTransform(HumanBodyBones.RightHand);
                if (nk != null) neck = nk.position;
                if (ll != null && fl != null) ankleL = Vector3.Lerp(ll.position, fl.position, 0.65f);
                if (lr != null && fr != null) ankleR = Vector3.Lerp(lr.position, fr.position, 0.65f);
                if (hn != null) wrist = hn.position;
                if (la != null) elbow = la.position;
            }

            // One-handed holds: each hand has its own job, so no left/right swap.
            switch (kind)
            {
                case GripKind.Neck:
                    heroRightHand = neck;
                    heroLeftHand = hips;
                    return;
                case GripKind.Headlock:
                    heroRightHand = neck - hero.right * 0.14f + Vector3.up * 0.06f;   // arm wrapped over his neck
                    heroLeftHand = chest;                                              // other hand on his back
                    return;
                case GripKind.Wrist:
                    heroLeftHand = wrist;
                    heroRightHand = elbow;
                    return;
                case GripKind.Clinch:
                {
                    // Hands cupped on the back of his head, either side.
                    Vector3 back = Vector3.Lerp(neck, head, 0.5f) + Vector3.up * 0.07f;
                    heroLeftHand = back - hero.right * 0.09f;
                    heroRightHand = back + hero.right * 0.09f;
                    return;
                }
                case GripKind.ClawArm:
                {
                    // 'who' is the one choking: grab his right forearm, near the elbow and near the wrist.
                    Vector3 elbowC = who.transform.position + Vector3.up * 1.35f, handC = who.transform.position + Vector3.up * 1.4f + who.transform.forward * 0.3f;
                    if (anim != null && anim.isHuman)
                    {
                        var ea = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
                        var ha = anim.GetBoneTransform(HumanBodyBones.RightHand);
                        if (ea != null) elbowC = ea.position;
                        if (ha != null) handC = ha.position;
                    }
                    heroLeftHand = Vector3.Lerp(elbowC, handC, 0.35f);
                    heroRightHand = Vector3.Lerp(elbowC, handC, 0.8f);
                    head = handC;
                    return;
                }
                case GripKind.ClawPipe:
                {
                    // 'who' holds the pipe in both fists across our throat: grab it just inside his fists.
                    Vector3 l = who.transform.position + Vector3.up * 1.4f - who.transform.right * 0.28f, r = l + who.transform.right * 0.56f;
                    if (anim != null && anim.isHuman)
                    {
                        var lh = anim.GetBoneTransform(HumanBodyBones.LeftHand);
                        var rh = anim.GetBoneTransform(HumanBodyBones.RightHand);
                        if (lh != null) l = lh.position;
                        if (rh != null) r = rh.position;
                    }
                    heroLeftHand = Vector3.Lerp(l, r, 0.3f);
                    heroRightHand = Vector3.Lerp(l, r, 0.7f);
                    head = (l + r) * 0.5f;
                    return;
                }
                case GripKind.Choke:
                    // From behind: the right forearm wraps across the front of his throat (hand at his far
                    // shoulder), the left hand presses the back of his head forward.
                    heroRightHand = neck - hero.right * 0.13f + hero.forward * 0.1f;
                    heroLeftHand = head - hero.forward * 0.1f + Vector3.up * 0.03f;
                    return;
                case GripKind.PipeChoke:
                    heroRightHand = neck + hero.right * 0.28f + hero.forward * 0.12f;
                    heroLeftHand = neck - hero.right * 0.28f + hero.forward * 0.12f;
                    return;
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
                case GripKind.Ankles:
                    a = ankleL; b = ankleR;
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

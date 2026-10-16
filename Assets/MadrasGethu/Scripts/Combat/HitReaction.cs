using UnityEngine;

namespace MadrasGethu
{
    /// <summary>
    /// Procedural hit reactions laid on top of whatever animation is playing: a hook snaps the head
    /// sideways, a straight punch snaps it back, an uppercut throws it up, a body shot folds the spine.
    /// Springs, so the body bounces back with a little overshoot. Rotations are in world axes taken from
    /// the character, so it works on any humanoid rig.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class HitReaction : MonoBehaviour
    {
        struct Spring
        {
            public float value, speed;
            public void Kick(float degrees) => speed += degrees * 14f;
            public void Step(float dt)
            {
                // Underdamped: snaps away, comes back, overshoots a touch.
                float acc = -170f * value - 15f * speed;
                speed += acc * dt;
                value += speed * dt;
            }
        }

        Animator animator;
        Transform head, neck, spine, chest;
        Spring headPitch, headYaw, headRoll, spinePitch, spineRoll;

        void Awake()
        {
            animator = GetComponent<Animator>();
            if (animator == null || !animator.isHuman) { enabled = false; return; }
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (head == null || spine == null) enabled = false;
        }

        /// <summary>A blow from 'direction' (attacker to target, world) of the given kind.</summary>
        public void React(Vector3 direction, FighterPose pose, float strength)
        {
            Transform root = transform;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) direction = -root.forward;
            direction.Normalize();
            // + when the blow travels towards his right, i.e. it came from his left
            float side = Vector3.Dot(direction, root.right) >= 0f ? 1f : -1f;
            float s = Mathf.Clamp(strength, 0.3f, 1.6f) * Random.Range(0.85f, 1.15f);

            switch (pose)
            {
                case FighterPose.Hook: case FighterPose.Haymaker: case FighterPose.Backfist: case FighterPose.Counter:
                case FighterPose.Roundhouse: case FighterPose.SpinKick: case FighterPose.RowdySwing: case FighterPose.BackElbow:
                    headYaw.Kick(side * 38f * s);          // face turned away with the blow
                    headRoll.Kick(-side * 18f * s);
                    spineRoll.Kick(-side * 8f * s);
                    break;
                case FighterPose.Uppercut: case FighterPose.FlyingKnee:
                    headPitch.Kick(-42f * s);              // chin up, head thrown back
                    spinePitch.Kick(-12f * s);
                    break;
                case FighterPose.BodyBlow: case FighterPose.FrontKick: case FighterPose.PushKick: case FighterPose.Kick:
                case FighterPose.Knee: case FighterPose.RowdyKick:
                    spinePitch.Kick(26f * s);              // folds over the blow
                    headPitch.Kick(12f * s);
                    break;
                case FighterPose.LowKick: case FighterPose.LegSweep:
                    spineRoll.Kick(side * 14f * s);        // knee buckles, body tips to the side
                    headRoll.Kick(side * 8f * s);
                    break;
                default:                                   // jab, cross and anything else: head snaps straight back
                    headPitch.Kick(-26f * s);
                    headYaw.Kick(Random.Range(-8f, 8f) * s);
                    spinePitch.Kick(-6f * s);
                    break;
            }
        }

        void LateUpdate()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            headPitch.Step(dt); headYaw.Step(dt); headRoll.Step(dt); spinePitch.Step(dt); spineRoll.Step(dt);
            Transform root = transform;
            Quaternion spineRot = Quaternion.AngleAxis(spinePitch.value, root.right) * Quaternion.AngleAxis(spineRoll.value, root.forward);
            Quaternion headRot = Quaternion.AngleAxis(headPitch.value, root.right) * Quaternion.AngleAxis(headYaw.value, Vector3.up)
                                 * Quaternion.AngleAxis(headRoll.value, root.forward);
            // Spread over two bones each so it bends rather than kinks.
            Quaternion halfSpine = Quaternion.Slerp(Quaternion.identity, spineRot, 0.5f);
            spine.rotation = halfSpine * spine.rotation;
            if (chest != null) chest.rotation = halfSpine * chest.rotation;
            else spine.rotation = halfSpine * spine.rotation;
            Quaternion halfHead = Quaternion.Slerp(Quaternion.identity, headRot, 0.5f);
            if (neck != null) neck.rotation = halfHead * neck.rotation;
            else head.rotation = halfHead * head.rotation;
            head.rotation = halfHead * head.rotation;
        }
    }
}

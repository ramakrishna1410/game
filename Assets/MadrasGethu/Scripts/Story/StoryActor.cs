using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MadrasGethu
{
    /// <summary>
    /// Lets the story move a character around: walk to a mark, turn to face someone, gesture.
    /// Works on story-only characters (Amma, Kutty...) and, while a cutscene has them, on the hero and rowdies.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class StoryActor : MonoBehaviour
    {
        public static readonly List<StoryActor> All = new List<StoryActor>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        public string castId;
        public CastMember Member => Cast.Get(castId);

        FighterMotor motor;
        FighterVisual visual;
        Fighter fighter;
        Vector3? walkTarget;
        float walkSpeed;
        float stopDistance;
        Vector3? faceTarget;
        Transform faceTransform;

        public Fighter Fighter => fighter;
        public FighterVisual Visual => visual != null ? visual : (visual = GetComponentInChildren<FighterVisual>());
        public FighterMotor Motor => motor;
        public bool Walking => walkTarget.HasValue;

        /// <summary>The cutscene has this character (the hero's controls and rowdy AI are off).</summary>
        public bool Directed
        {
            get => fighter == null || fighter.cutsceneControlled;
            set
            {
                if (fighter != null) fighter.cutsceneControlled = value;
                if (!value) { walkTarget = null; faceTarget = null; faceTransform = null; }
                else if (Visual != null) Visual.SetCombatStance(false);
            }
        }

        public Vector3 Head
        {
            get
            {
                float top = Visual != null ? Visual.HeadTopY : transform.position.y + 1.75f;
                return new Vector3(transform.position.x, top - 0.13f, transform.position.z);
            }
        }

        void Awake()
        {
            motor = GetComponent<FighterMotor>();
            fighter = GetComponent<Fighter>();
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static StoryActor Find(string id)
        {
            foreach (var a in All)
                if (a != null && a.castId == id) return a;
            return null;
        }

        public void WalkTo(Vector3 point, float speed = 1.5f, float stop = 0.12f)
        {
            walkTarget = new Vector3(point.x, transform.position.y, point.z);
            walkSpeed = speed;
            stopDistance = stop;
            faceTarget = null;
            faceTransform = null;
        }

        /// <summary>Walks there and waits until arrived (or gives up after a while).</summary>
        public IEnumerator WalkAndWait(Vector3 point, float speed = 1.5f, float stop = 0.12f)
        {
            WalkTo(point, speed, stop);
            float giveUp = Time.time + Vector3.Distance(transform.position, point) / Mathf.Max(0.3f, speed) * 2f + 1f;
            while (Walking && Time.time < giveUp) yield return null;
            if (Walking) { walkTarget = null; Teleport(point); }
        }

        public void Face(Vector3 point) { faceTarget = point; faceTransform = null; }
        public void Face(StoryActor other) { if (other != null) { faceTransform = other.transform; faceTarget = null; } }
        public void Face(Transform other) { faceTransform = other; faceTarget = null; }

        public void FaceNow(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(d);
            faceTarget = point;
            faceTransform = null;
        }

        public void Teleport(Vector3 point)
        {
            if (motor != null) motor.Teleport(point);
            else transform.position = point;
        }

        public void Place(Vector3 point, Vector3 lookAt)
        {
            Teleport(point);
            FaceNow(lookAt);
        }

        public void Gesture(FighterPose pose, float seconds = 2.5f)
        {
            if (Visual != null) Visual.Play(pose, seconds);
        }

        public void Hold(string animatorBool, bool on)
        {
            if (Visual != null) Visual.SetAnimatorBool(animatorBool, on);
        }

        void Update()
        {
            if (!Directed || motor == null) return;

            if (walkTarget.HasValue)
            {
                Vector3 to = walkTarget.Value - transform.position;
                to.y = 0f;
                float d = to.magnitude;
                if (d <= stopDistance) walkTarget = null;
                else
                {
                    // Ease in to the mark so they don't skid to a stop.
                    float speed = Mathf.Min(walkSpeed, 0.6f + d * 1.6f);
                    motor.SetDesiredVelocity(to / d * speed);
                    motor.FaceDirection(to);
                    return;
                }
            }

            Vector3? face = faceTransform != null ? faceTransform.position : faceTarget;
            if (face.HasValue) motor.FaceDirection(face.Value - transform.position);
        }
    }
}

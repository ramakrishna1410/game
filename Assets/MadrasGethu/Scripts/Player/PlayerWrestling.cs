using System.Collections.Generic;
using UnityEngine;

namespace MadrasGethu
{
    /// <summary>Which button a quick-time prompt asks for.</summary>
    public enum QteKey { None, Light, Heavy, Jump }

    /// <summary>Close-up camera angle for one beat of a finisher.</summary>
    public enum CamShot
    {
        LowHero,        // low in front, looking up at the lift
        Side,           // side-on, close: shows the whole arc
        OverShoulder,   // behind the hero's shoulder, looking at the victim
        Overhead,       // high above, looking down
        Wide,           // further out and higher: big swings
        GroundImpact,   // at street level beside the landing spot
        Front,          // in front of the victim, hero behind him
    }

    /// <summary>
    /// One beat of a wrestling finisher. Positions are in the hero's local space (x right, y up, z forward)
    /// measured from where he stood when the move began. Null fields keep the value from the previous step.
    /// </summary>
    public class WrestleStep
    {
        public QteKey qte;              // None = plays automatically
        public string qteLabel;
        public float duration = 0.3f;
        public FighterPose heroPose = FighterPose.Grab;
        public Vector3? hip;            // victim's hips at the end of the step
        public float? pitch;            // victim's body tilt (degrees around his own right axis; + = head forward)
        public float arc;               // extra height in the middle of the step
        public Vector3? hero;           // hero's feet at the end of the step
        public float damage;            // dealt at the end of the step (a small strike, or the impact)
        public bool impact;             // he hits the ground (or the wall) at the end of this step
        public CamShot shot = CamShot.Side;
        public float? drop;             // hero's body height change (metres, negative = crouch) at the end of the step
        public float? lean;             // hero's upper-body lean (degrees, + = bend forward, - = arch back)
        public bool anim;               // play the hero's own animation for this step (knee, jump...); otherwise hold + IK
        public float? yaw;              // hero turns this many degrees (from his starting facing) and carries him round
        public float? bodyPitch;        // hero's whole body tilts forward (elbow drop: falling onto him)
        public bool keepHold;           // impact without letting go (first slam of a double slam)
    }

    public class WrestleMove
    {
        public string name;
        public MoveId id;
        public float cost;
        public bool carry = true;       // false: the victim stays where he lies (elbow drop)
        public bool victimFacesAway;    // German suplex: he's held from behind
        public bool shockwave;          // powerbomb knocks down everyone nearby
        public bool wall;               // spinebuster: the impact is against a wall
        public GripKind grip = GripKind.Waist;   // where the hero's hands hold him
        public bool cinematic = true;   // close-up camera (quick throws stay on the normal camera)
        public FinisherSpot prop;       // smashed into a tea kadai / crates / auto
        public bool throwAtEnd;         // let go and throw him at the end (no slam)
        public float throwDamage;
        public readonly List<WrestleStep> steps = new List<WrestleStep>();

        public WrestleMove Step(WrestleStep s)
        {
            steps.Add(s);
            return this;
        }

        public int QteCount
        {
            get
            {
                int n = 0;
                foreach (var s in steps) if (s.qte != QteKey.None) n++;
                return n;
            }
        }
    }

    /// <summary>
    /// Wrestling finishers with quick-time prompts (Sleeping Dogs / Yakuza heat actions):
    /// Body Slam, Dhobi Pachad, German Suplex, Powerbomb, Spear, Elbow Drop and the wall Spinebuster.
    /// A shrinking ring shows when to press. PERFECT adds damage and refunds Gethu; a miss never cancels the move.
    /// </summary>
    public partial class PlayerFighter
    {
        public const float QteDuration = 1.0f;     // seconds (real time) before a prompt times out
        public const float QtePerfectAt = 0.7f;    // ring meets the target circle here
        const float QtePerfectWindow = 0.13f;

        WrestleMove wrestleMove;
        EnemyFighter wrestleVictim;
        int wrestleStep;
        float wrestleStepTimer;
        Vector3 wrestleOrigin;
        Quaternion wrestleRotation;
        Quaternion wrestleVictimRotation;
        Vector3 stepHipFrom, stepHeroFrom;
        float stepPitchFrom;
        Vector3 curHip, curHero;
        float curPitch;
        float stepDropFrom, stepLeanFrom, curDrop, curLean;
        float stepYawFrom, curYaw, stepBodyPitchFrom, curBodyPitch;
        bool wrestleReleased;   // he's been slammed and let go (later steps only move the hero)
        float roomAhead, roomBehind;
        float wrestleSide = 1f;   // which side of the hero the camera works from (keeps it on the side it was on)
        bool qteActive;
        float qteStart;
        int qtePerfects, qteMisses, qteDone;

        public bool QteActive => qteActive;
        public QteKey QteKeyNow => qteActive && wrestleMove != null ? wrestleMove.steps[wrestleStep].qte : QteKey.None;
        public string QteLabelNow => qteActive && wrestleMove != null ? wrestleMove.steps[wrestleStep].qteLabel : null;
        public float QteProgress => qteActive ? (Time.unscaledTime - qteStart) / QteDuration : 0f;
        public int QteNumber => qteDone + 1;
        public int QteTotal => wrestleMove != null ? wrestleMove.QteCount : 0;
        public string QteResultText { get; private set; }
        public float QteResultTime { get; private set; } = -10f;
        public string WrestleName => wrestleMove != null ? wrestleMove.name : null;

        float WrestleDamageMultiplier => Mathf.Max(0.5f, 1f + 0.2f * qtePerfects - 0.25f * qteMisses) * (MassMode ? massModeDamageMultiplier : 1f);

        // ------------------------------------------------------------------ the moves

        static WrestleStep S(QteKey key, string label, float duration, FighterPose pose)
        {
            return new WrestleStep { qte = key, qteLabel = label, duration = duration, heroPose = pose };
        }

        // Every lift has the same physical rhythm: crouch and get the hands in, drive up with the legs,
        // a beat at the top, then drop him fast and follow him down.

        // Scoop slam: head under his arm, one arm between his legs and one behind his shoulders. Scoop him
        // up across your chest (low, not overhead), turn a quarter while he's up, and slam him flat.
        WrestleMove BodySlam()
        {
            var tuck = Pose(Shot(With(S(QteKey.None, null, 0.25f, FighterPose.Grab), new Vector3(0f, 0.85f, 0.42f), 15f, 0f), CamShot.Side), -0.35f, 30f);
            var scoop = Pose(Shot(With(S(QteKey.Light, "SCOOP", 0.45f, FighterPose.Grab), new Vector3(0f, 1.3f, 0.32f), -95f, 0.15f), CamShot.Side), 0f, -5f);
            scoop.yaw = 40f;
            var turn = Pose(With(S(QteKey.None, null, 0.18f, FighterPose.Grab), new Vector3(0f, 1.34f, 0.32f), -97f, 0f), 0f, -8f);
            turn.yaw = 90f;
            turn.shot = CamShot.Side;
            var slam = Pose(Shot(Impact(With(S(QteKey.Heavy, "SLAM", 0.26f, FighterPose.Grab), new Vector3(0f, 0.35f, 0.85f), -95f, 0f), 30f), CamShot.GroundImpact), -0.45f, 38f);
            slam.yaw = 90f;
            return new WrestleMove { name = "BODY SLAM", id = MoveId.BodySlam, cost = GethuBar, grip = GripKind.ChestAndThigh }
                .Step(tuck).Step(scoop).Step(turn).Step(slam);
        }

        // Dhobi Pachad, the washerman's beat: collar and belt, swing him back over your head and beat him
        // face-down on the road like washing on a stone. Then pick him up and do it again.
        WrestleMove DhobiPachad()
        {
            var tuck = Pose(Shot(With(S(QteKey.None, null, 0.25f, FighterPose.Grab), new Vector3(0f, 0.9f, 0.45f), 0f, 0f), CamShot.Wide), -0.25f, 15f);
            var swingUp = Pose(Shot(With(S(QteKey.Light, "SWING HIM UP", 0.55f, FighterPose.Grab), new Vector3(0f, 2.05f, -0.1f), -150f, 0.2f), CamShot.Wide), 0.05f, -18f);
            var beat = Pose(Shot(Impact(With(S(QteKey.Heavy, "BEAT HIM DOWN", 0.28f, FighterPose.Grab), new Vector3(0f, 0.4f, 1.0f), -270f, 0.3f), 16f), CamShot.GroundImpact), -0.45f, 45f);
            beat.keepHold = true;
            var again = Pose(Shot(With(S(QteKey.Light, "PICK HIM UP", 0.5f, FighterPose.Grab), new Vector3(0f, 2.0f, -0.05f), -150f, 0.2f), CamShot.Wide), 0.05f, -18f);
            var finish = Pose(Shot(Impact(With(S(QteKey.Heavy, "AGAIN!", 0.28f, FighterPose.Grab), new Vector3(0f, 0.35f, 1.05f), -270f, 0.3f), 22f), CamShot.GroundImpact), -0.5f, 48f);
            return new WrestleMove { name = "DHOBI PACHAD", id = MoveId.DhobiPachad, cost = GethuBar, grip = GripKind.Waist }
                .Step(tuck).Step(swingUp).Step(beat).Step(again).Step(finish);
        }

        // German suplex: lock both arms round his waist from behind, jolt him off his feet a few times
        // (hip lifts, not knees: you can't knee a man from behind), then sit down, bridge back and throw
        // him over your head so he lands on his shoulders behind you.
        WrestleMove GermanSuplex()
        {
            var m = new WrestleMove { name = "GERMAN SUPLEX", id = MoveId.GermanSuplex, cost = GethuBar, victimFacesAway = true, grip = GripKind.Waist };
            m.Step(Pose(Shot(With(S(QteKey.None, null, 0.25f, FighterPose.Grab), new Vector3(0f, 0.9f, 0.38f), 0f, 0f), CamShot.Side), -0.1f, 15f));
            for (int i = 0; i < 3; i++)
            {
                var jolt = With(S(QteKey.Light, "HIP LIFT", 0.32f, FighterPose.Grab), new Vector3(0f, 0.92f, 0.38f), -5f, 0.22f);
                jolt.damage = 5f;
                jolt.shot = CamShot.Side;
                m.Step(Pose(jolt, -0.1f, 5f));
            }
            m.Step(Pose(Shot(With(S(QteKey.None, null, 0.3f, FighterPose.Grab), new Vector3(0f, 0.85f, 0.35f), 0f, 0f), CamShot.Side), -0.35f, 10f));
            // Lands on his upper back and shoulders (head not buried in the road), then the ragdoll rolls him over.
            return m.Step(Pose(Shot(Impact(With(S(QteKey.Heavy, "SUPLEX", 0.65f, FighterPose.Grab), new Vector3(0f, 0.75f, -0.75f), -150f, 0.9f), 34f), CamShot.Side), -0.55f, -65f));
        }

        // Real powerbomb: head under his chest, hands on his thighs, stand up with him sitting on your
        // shoulders facing you, rise once more, then sit out and drive him down on his back in front of you.
        WrestleMove Powerbomb()
        {
            var tuck = Pose(Shot(With(S(QteKey.None, null, 0.35f, FighterPose.Grab), new Vector3(0f, 0.85f, 0.35f), 35f, 0f), CamShot.Side), -0.4f, 40f);
            var lift = Pose(Shot(With(S(QteKey.Light, "LIFT", 0.65f, FighterPose.Grab), new Vector3(0f, 1.55f, 0.22f), -12f, 0.1f), CamShot.LowHero), 0f, 0f);
            var higher = Pose(Shot(With(S(QteKey.Jump, "HIGHER", 0.3f, FighterPose.Grab), new Vector3(0f, 1.8f, 0.25f), -25f, 0f), CamShot.LowHero), 0.05f, -8f);
            higher.hero = new Vector3(0f, 0.15f, 0f);
            var drive = Pose(Impact(With(S(QteKey.Heavy, "DRIVE HIM DOWN", 0.3f, FighterPose.Grab), new Vector3(0f, 0.35f, 1.0f), -95f, 0.15f), 45f), -0.6f, 45f);
            drive.hero = new Vector3(0f, 0f, 0.15f);
            drive.shot = CamShot.GroundImpact;
            return new WrestleMove { name = "POWERBOMB", id = MoveId.Powerbomb, cost = maxGethu, shockwave = true, grip = GripKind.Thighs }
                .Step(tuck).Step(lift).Step(higher).Step(drive);
        }

        // Spear: sprint at him, drive a shoulder into his stomach (he folds over it), keep driving with
        // the arms wrapped round him, and crash down together, the hero landing on top of him.
        // distance: how far ahead he is when Q is pressed (the hero runs to him; he isn't pulled in).
        WrestleMove Spear(float distance)
        {
            float d = Mathf.Max(0.8f, distance);
            var run = Pose(S(QteKey.None, null, Mathf.Clamp(d / 6.5f, 0.12f, 0.5f), FighterPose.Spear), -0.3f, 35f);
            run.hero = new Vector3(0f, 0f, d - 0.6f);
            run.shot = CamShot.OverShoulder;
            run.anim = true;
            var contact = Pose(With(S(QteKey.Heavy, "SPEAR", 0.18f, FighterPose.Grab), new Vector3(0f, 1.05f, d + 0.2f), 55f, 0f), -0.35f, 50f);
            contact.hero = new Vector3(0f, 0f, d - 0.2f);
            contact.shot = CamShot.Side;
            var drive = Pose(With(S(QteKey.None, null, 0.28f, FighterPose.Grab), new Vector3(0f, 0.95f, d + 1.4f), 25f, 0.1f), -0.4f, 55f);
            drive.hero = new Vector3(0f, 0f, d + 0.85f);
            drive.shot = CamShot.Side;
            var crash = Pose(Impact(With(S(QteKey.None, null, 0.18f, FighterPose.Grab), new Vector3(0f, 0.35f, d + 1.85f), -90f, 0f), 28f), -0.7f, 60f);
            crash.hero = new Vector3(0f, -0.2f, d + 1.3f);
            crash.shot = CamShot.GroundImpact;
            return new WrestleMove { name = "SPEAR", id = MoveId.Spear, cost = GethuBar, grip = GripKind.Waist }
                .Step(run).Step(contact).Step(drive).Step(crash);
        }

        // Elbow drop: leap up, then the whole body comes down on him with the elbow in his chest,
        // and roll off him to get back up.
        WrestleMove ElbowDrop(Vector3 victimLocal)
        {
            float z = Mathf.Max(0.7f, victimLocal.z);
            var jump = S(QteKey.Jump, "JUMP", 0.4f, FighterPose.Jump);
            jump.hero = new Vector3(victimLocal.x, 0.9f, z - 0.9f);
            jump.bodyPitch = 20f;
            jump.shot = CamShot.Side;
            jump.anim = true;
            var drop = Pose(Impact(S(QteKey.Light, "ELBOW", 0.22f, FighterPose.Grab), 26f), -0.2f, 20f);
            drop.hero = new Vector3(victimLocal.x, -0.55f, z - 0.75f);
            drop.bodyPitch = 80f;
            drop.shot = CamShot.Side;
            var rollOff = S(QteKey.None, null, 0.45f, FighterPose.Grab);
            rollOff.hero = new Vector3(victimLocal.x - 0.7f, 0f, z - 1.1f);
            rollOff.bodyPitch = 0f;
            rollOff.shot = CamShot.Side;
            return new WrestleMove { name = "ELBOW DROP", id = MoveId.ElbowDrop, cost = GethuBar, carry = false, grip = GripKind.ChestAndThigh }
                .Step(jump).Step(drop).Step(rollOff);
        }

        WrestleMove Spinebuster(float wallDistance)
        {
            float d = Mathf.Max(1.0f, wallDistance);
            var drive = Pose(Impact(With(S(QteKey.Heavy, "INTO THE WALL", 0.35f, FighterPose.Grab), new Vector3(0f, 1.1f, d - 0.35f), -75f, 0f), 36f), -0.15f, 30f);
            drive.hero = new Vector3(0f, 0f, Mathf.Max(0f, d - 1.0f));
            drive.shot = CamShot.Side;
            return new WrestleMove { name = "SPINEBUSTER", id = MoveId.Spinebuster, cost = GethuBar, wall = true, grip = GripKind.Waist }
                .Step(Pose(Shot(With(S(QteKey.None, null, 0.25f, FighterPose.Grab), new Vector3(0f, 0.85f, 0.45f), 0f, 0f), CamShot.LowHero), -0.3f, 25f))
                .Step(Pose(Shot(With(S(QteKey.Light, "LIFT", 0.45f, FighterPose.Grab), new Vector3(0f, 1.45f, 0.45f), -40f, 0f), CamShot.LowHero), 0f, 0f))
                .Step(drive);
        }

        /// <summary>Tea kadai / fish crates / auto: lift him, carry him over and smash him into it.</summary>
        WrestleMove PropSlam(FinisherSpot spot)
        {
            float d = Flat(spot.SlamPoint - transform.position).magnitude;
            var smash = Pose(Impact(With(S(QteKey.Heavy, "SMASH", 0.38f, FighterPose.Grab), new Vector3(0f, 0.9f, d - 0.2f), -90f, 0.2f), spot.damage), -0.3f, 35f);
            smash.hero = new Vector3(0f, 0f, Mathf.Max(0f, d - 1.3f));
            smash.shot = CamShot.Side;
            return new WrestleMove { name = spot.finisherName.TrimEnd('!'), id = MoveId.BodySlam, cost = spot.gethuCost, grip = GripKind.ChestAndThigh, prop = spot }
                .Step(Pose(Shot(With(S(QteKey.None, null, 0.3f, FighterPose.Grab), new Vector3(0f, 0.85f, 0.5f), 0f, 0f), CamShot.LowHero), -0.3f, 25f))
                .Step(Pose(Shot(With(S(QteKey.Light, "LIFT", 0.55f, FighterPose.Grab), new Vector3(0f, 1.6f, 0.25f), -80f, 0.1f), CamShot.LowHero), 0.05f, -5f))
                .Step(smash);
        }

        /// <summary>The ordinary throw (E): swing him round by the collar and let go. No Gethu, no prompts.</summary>
        WrestleMove ThrowMove(float damage)
        {
            var swing = Pose(With(S(QteKey.None, null, 0.24f, FighterPose.Grab), new Vector3(0.45f, 0.95f, 0.35f), 0f, 0f), -0.15f, 10f);
            var release = Pose(With(S(QteKey.None, null, 0.16f, FighterPose.Throw), new Vector3(0.1f, 1.0f, 0.95f), 10f, 0f), 0f, 20f);
            release.anim = true;
            return new WrestleMove { name = "THROW", cost = 0f, grip = GripKind.Collar, cinematic = false, throwAtEnd = true, throwDamage = damage }
                .Step(swing).Step(release);
        }

        static WrestleStep Pose(WrestleStep s, float drop, float lean)
        {
            s.drop = drop;
            s.lean = lean;
            return s;
        }

        static WrestleStep With(WrestleStep s, Vector3 hip, float pitch, float arc)
        {
            s.hip = hip;
            s.pitch = pitch;
            s.arc = arc;
            return s;
        }

        static WrestleStep Shot(WrestleStep s, CamShot shot)
        {
            s.shot = shot;
            return s;
        }

        static WrestleStep Impact(WrestleStep s, float damage)
        {
            s.impact = true;
            s.damage = damage;
            return s;
        }

        // ------------------------------------------------------------------ choosing a finisher

        /// <summary>
        /// The wrestling finisher Q does right now while holding someone (null = none). Chosen by the keys
        /// held with Q, never by chance:
        ///   from behind ........ German Suplex
        ///   Shift + Q .......... Powerbomb (full Gethu)
        ///   W + Q at a wall .... Spinebuster
        ///   S + Q .............. Dhobi Pachad
        ///   Q .................. Body Slam
        /// </summary>
        WrestleMove PickGrabFinisher(Vector3 aim, Vector3 move)
        {
            if (grabbed == null) return null;
            if (grabFromBehind) return MoveUnlocks.Has(MoveId.GermanSuplex) ? GermanSuplex() : null;
            Vector2 keys = GameInput.Move;
            if (GameInput.Sprint && Gethu >= maxGethu && MoveUnlocks.Has(MoveId.Powerbomb)) return Powerbomb();
            if (keys.y > 0.5f && MoveUnlocks.Has(MoveId.Spinebuster) && FindWall(grabbed.Center, transform.forward, 2.6f, out var wallHit))
                return Spinebuster(Flat(wallHit.point - transform.position).magnitude);
            if (keys.y < -0.5f && MoveUnlocks.Has(MoveId.DhobiPachad)) return DhobiPachad();
            return MoveUnlocks.Has(MoveId.BodySlam) ? BodySlam() : null;
        }

        /// <summary>Key caps for the finishers available while holding him.</summary>
        void AddGrabFinisherPrompts(Vector3 aim)
        {
            if (grabbed == null) return;
            bool dim = Gethu < GethuBar;
            if (grabFromBehind)
            {
                if (MoveUnlocks.Has(MoveId.GermanSuplex)) Prompts.Add(new PromptItem { key = "Q", label = "German Suplex", dim = dim });
                return;
            }
            if (MoveUnlocks.Has(MoveId.BodySlam)) Prompts.Add(new PromptItem { key = "Q", label = "Body Slam", dim = dim });
            if (MoveUnlocks.Has(MoveId.DhobiPachad)) Prompts.Add(new PromptItem { key = "S+Q", label = "Dhobi Pachad", dim = dim });
            if (MoveUnlocks.Has(MoveId.Spinebuster) && FindWall(grabbed.Center, transform.forward, 2.6f, out _))
                Prompts.Add(new PromptItem { key = "W+Q", label = "Spinebuster", dim = dim });
            if (MoveUnlocks.Has(MoveId.Powerbomb) && Gethu >= maxGethu)
                Prompts.Add(new PromptItem { key = "SHIFT+Q", label = "Powerbomb" });
        }

        void StartGrabFinisher(WrestleMove move, Vector3 aim)
        {
            var victim = grabbed;
            LetGo();
            Motor.FaceDirection(aim, instant: true);
            Gethu = Mathf.Max(0f, Gethu - move.cost);
            StartWrestle(move, victim);
        }

        /// <summary>Q when not holding anyone: elbow drop on a downed rowdy, or a spear while sprinting.</summary>
        bool TryStartFreeFinisher()
        {
            if (HeldWeapon != null) return false;

            var downed = DownedNearby(lyingOnly: true);
            if (downed != null && MoveUnlocks.Has(MoveId.ElbowDrop))
            {
                if (Gethu < GethuBar)
                {
                    if (GM != null) GM.AddPopup("Need 1 Gethu bar", Center + Vector3.up, Color.gray, 0.6f);
                    return true;
                }
                Motor.FaceDirection(downed.transform.position - transform.position, instant: true);
                Vector3 local = Quaternion.Inverse(transform.rotation) * (downed.transform.position - transform.position);
                Gethu -= GethuBar;
                StartWrestle(ElbowDrop(local), downed);
                return true;
            }

            var target = SpearTarget();
            if (target != null && Gethu >= GethuBar)
            {
                Motor.FaceDirection(target.transform.position - transform.position, instant: true);
                float gap = Flat(target.transform.position - transform.position).magnitude;
                target.EnterGrabbed(false);
                Gethu -= GethuBar;
                StartWrestle(Spear(gap), target);
                return true;
            }
            return false;
        }

        /// <summary>A rowdy on the ground nearby. lyingOnly: not one who is already getting up.</summary>
        EnemyFighter DownedNearby(bool lyingOnly = false)
        {
            var f = CombatUtil.FindTarget(this, MoveInputWorld().sqrMagnitude > 0.01f ? MoveInputWorld() : transform.forward, 2.4f, includeDowned: true) as EnemyFighter;
            if (f == null || f.IsDead || !f.IsDowned) return null;
            return lyingOnly && !f.IsLyingDown ? null : f;
        }

        /// <summary>Sprinting at a rowdy who can be taken off his feet.</summary>
        EnemyFighter SpearTarget()
        {
            if (!MoveUnlocks.Has(MoveId.Spear) || !GameInput.Sprint || Motor.MoveVelocity.magnitude < runSpeed * 0.8f) return null;
            var f = CombatUtil.FindTarget(this, transform.forward, 4.5f) as EnemyFighter;
            if (f == null || !f.CanBeGrabbed) return null;
            return Flat(f.transform.position - transform.position).magnitude > 1.0f ? f : null;
        }

        /// <summary>Button hints while walking around (shown as key caps by the HUD).</summary>
        void AddFreePrompts()
        {
            if (HeldWeapon != null)
            {
                Prompts.Add(new PromptItem { key = "LMB", label = "Swing" });
                Prompts.Add(new PromptItem { key = "RMB", label = "Smash" });
                Prompts.Add(new PromptItem { key = "E", label = "Throw " + HeldWeapon.displayName });
                return;
            }
            var weapon = Weapon.FindNearbyOnGround(transform.position, 1.5f);
            if (weapon != null) Prompts.Add(new PromptItem { key = "E", label = "Pick up " + weapon.displayName });

            var downed = DownedNearby();
            if (downed != null)
            {
                Prompts.Add(new PromptItem { key = "RMB", label = "Stomp" });
                if (MoveUnlocks.Has(MoveId.ElbowDrop) && downed.IsLyingDown)
                    Prompts.Add(new PromptItem { key = "Q", label = "Elbow Drop", dim = Gethu < GethuBar });
            }

            var near = CombatUtil.FindTarget(this, transform.forward, 2.2f) as EnemyFighter;
            if (near != null && near.IsDazed) Prompts.Add(new PromptItem { key = "E", label = "Grab him!" });

            if (SpearTarget() != null)
            {
                Prompts.Add(new PromptItem { key = "Q", label = "Spear", dim = Gethu < GethuBar });
                if (MoveUnlocks.Has(MoveId.Clothesline)) Prompts.Add(new PromptItem { key = "RMB", label = "Clothesline" });
            }

            if (Prompts.Count == 0 && Gethu >= maxGethu && !MassMode)
                Prompts.Add(new PromptItem { key = "Q", label = "GETHU MODE!" });
        }

        /// <summary>A wall (not a prop, not the floor, not a low box) within maxDistance in that direction.</summary>
        static bool FindWall(Vector3 from, Vector3 aim, float maxDistance, out RaycastHit hit)
        {
            if (!Physics.SphereCast(from, 0.25f, aim, out hit, maxDistance, CombatUtil.WorldMask, QueryTriggerInteraction.Ignore))
                return false;
            if (hit.collider.GetComponentInParent<Fighter>() != null) return false;
            if (hit.collider.GetComponentInParent<FinisherSpot>() != null) return false;
            if (Vector3.Dot(hit.normal, Vector3.up) > 0.5f) return false;
            return hit.collider.bounds.size.y >= 1.2f;
        }

        /// <summary>Free space in a direction (walls, boats, stalls), so finishers don't push bodies through them.</summary>
        float Room(Vector3 direction, float max)
        {
            float best = max;
            var hits = Physics.SphereCastAll(Center, 0.3f, direction, max, CombatUtil.WorldMask, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.distance <= 0f || h.collider.GetComponentInParent<Fighter>() != null) continue;
                if (Vector3.Dot(h.normal, Vector3.up) > 0.5f) continue;
                best = Mathf.Min(best, h.distance);
            }
            return best;
        }

        // ------------------------------------------------------------------ running a finisher

        void StartWrestle(WrestleMove move, EnemyFighter victim)
        {
            wrestleMove = move;
            wrestleVictim = victim;
            wrestleStep = -1;
            wrestleOrigin = transform.position;
            Vector3 fwd = Flat(transform.forward).normalized;
            wrestleRotation = Quaternion.LookRotation(fwd);
            wrestleVictimRotation = move.carry
                ? Quaternion.LookRotation(move.victimFacesAway ? fwd : -fwd)
                : victim.transform.rotation;
            wrestleSide = 1f;
            if (cam != null && Vector3.Dot(cam.transform.position - transform.position, Vector3.Cross(Vector3.up, fwd)) < 0f)
                wrestleSide = -1f;
            roomAhead = Room(fwd, 8f);
            roomBehind = Room(-fwd, 3f);

            curHip = Quaternion.Inverse(wrestleRotation) * (victim.transform.position + Vector3.up * 0.9f - wrestleOrigin);
            curHero = Vector3.zero;
            curPitch = 0f;
            curDrop = curLean = 0f;
            curYaw = curBodyPitch = 0f;
            wrestleReleased = false;
            qtePerfects = qteMisses = qteDone = 0;
            qteActive = false;

            if (!move.carry) victim.PinDown(3f);   // he stays down until the elbow lands
            Motor.SetFrozen(true);
            Visual.SetGrip(null, false);
            SetState(PlayerState.Wrestle);
            lightIndex = 0;
            lightBufferedUntil = heavyBufferedUntil = 0f;
            if (GM != null && move.cinematic)
            {
                GM.AddPopup(move.name, Center + Vector3.up * 1.3f, new Color(1f, 0.75f, 0.15f), 1.4f);
                GM.SlowMo(0.4f, 0.3f);
            }
            AdvanceWrestleStep();
        }

        void AdvanceWrestleStep()
        {
            wrestleStep++;
            if (wrestleMove == null || wrestleStep >= wrestleMove.steps.Count)
            {
                EndWrestle();
                return;
            }
            var st = wrestleMove.steps[wrestleStep];
            stepHipFrom = curHip;
            stepHeroFrom = curHero;
            stepPitchFrom = curPitch;
            stepDropFrom = curDrop;
            stepLeanFrom = curLean;
            stepYawFrom = curYaw;
            stepBodyPitchFrom = curBodyPitch;
            wrestleStepTimer = 0f;
            if (st.qte != QteKey.None)
            {
                qteActive = true;
                qteStart = Time.unscaledTime;
            }
            else
            {
                BeginStepMotion(st);
            }
        }

        void BeginStepMotion(WrestleStep st)
        {
            // Lifts and slams are done with the hands locked on him (IK) over the holding stance;
            // only steps with a real move of their own (knee, jump, run) play an animation.
            if (st.anim) Visual.Play(st.heroPose, st.duration + 0.15f, st.duration + 0.1f);
        }

        /// <summary>The hero should stand in his holding stance right now (no step animation playing).</summary>
        bool WrestleHoldsStance
        {
            get
            {
                if (wrestleMove == null || !wrestleMove.carry) return false;
                var st = wrestleMove.steps[Mathf.Clamp(wrestleStep, 0, wrestleMove.steps.Count - 1)];
                return qteActive || !st.anim;
            }
        }

        void UpdateWrestle()
        {
            if (wrestleMove == null) { SetState(PlayerState.Free); return; }
            if (wrestleVictim == null || wrestleVictim.IsDead)
            {
                EndWrestle();
                return;
            }

            var st = wrestleMove.steps[wrestleStep];
            if (qteActive)
            {
                ApplyWrestlePose(st, 0f);
                UpdateWrestleCamera(st, 0f, true);
                UpdateQte(st);
                return;
            }

            wrestleStepTimer += Time.deltaTime;
            float t = Mathf.Clamp01(wrestleStepTimer / Mathf.Max(0.01f, st.duration));
            ApplyWrestlePose(st, t);
            UpdateWrestleCamera(st, t, false);
            if (t < 1f) return;

            curHip = st.hip ?? stepHipFrom;
            curHero = st.hero ?? stepHeroFrom;
            curPitch = st.pitch ?? stepPitchFrom;
            curDrop = st.drop ?? stepDropFrom;
            curLean = st.lean ?? stepLeanFrom;
            curYaw = st.yaw ?? stepYawFrom;
            curBodyPitch = st.bodyPitch ?? stepBodyPitchFrom;
            FinishWrestleStep(st);
            if (State == PlayerState.Wrestle) AdvanceWrestleStep();
        }

        /// <summary>Places the hero and his victim along the current step (t = 0..1).</summary>
        void ApplyWrestlePose(WrestleStep st, float t)
        {
            // Slams accelerate into the ground; lifts ease in and out.
            float e = st.impact ? t * t : t * t * (3f - 2f * t);
            float lift = Mathf.Sin(t * Mathf.PI);

            Vector3 hero = Vector3.Lerp(stepHeroFrom, st.hero ?? stepHeroFrom, e);
            hero.z = Mathf.Clamp(hero.z, -(roomBehind - 0.4f), roomAhead - 0.4f);
            float yaw = Mathf.Lerp(stepYawFrom, st.yaw ?? stepYawFrom, e);
            Quaternion turn = Quaternion.AngleAxis(yaw, Vector3.up);
            Vector3 heroWorld = wrestleOrigin + wrestleRotation * hero;
            transform.position = heroWorld;
            transform.rotation = turn * wrestleRotation;

            // Whole-body tilt (falling onto him in the elbow drop).
            float bodyPitch = Mathf.Lerp(stepBodyPitchFrom, st.bodyPitch ?? stepBodyPitchFrom, e);
            if (Mathf.Abs(bodyPitch) > 0.5f) Visual.SetCarry(Quaternion.Euler(bodyPitch, 0f, 0f));
            else Visual.ClearCarry();

            if (wrestleReleased) return;
            if (!wrestleMove.carry && !st.impact) return;   // elbow drop: hands go to him only as you land on him
            // Hands stay on his body and the hero's own body crouches, drives up and follows him down.
            float drop = Mathf.Lerp(stepDropFrom, st.drop ?? stepDropFrom, e);
            float lean = Mathf.Lerp(stepLeanFrom, st.lean ?? stepLeanFrom, e);
            Visual.SetGrapple(wrestleVictim, wrestleMove.grip, drop, lean);
            if (!wrestleMove.carry) return;

            Vector3 hip = Vector3.Lerp(stepHipFrom, st.hip ?? stepHipFrom, e) + Vector3.up * st.arc * lift;
            hip.z = Mathf.Clamp(hip.z, -(roomBehind - 0.3f), roomAhead - 0.3f);
            float pitch = Mathf.Lerp(stepPitchFrom, st.pitch ?? stepPitchFrom, e);
            // He turns with the hero: his offset from the hero rotates by the hero's turn.
            Vector3 hipWorld = heroWorld + turn * (wrestleRotation * (hip - hero));
            wrestleVictim.HoldAt(hipWorld - Vector3.up * 0.9f, turn * wrestleVictimRotation);
            wrestleVictim.Visual.SetCarry(Quaternion.Euler(pitch, 0f, 0f));
        }

        static void ShotLayout(CamShot shot, out Vector3 offset, out float lookHeight, out float fov, out float roll)
        {
            // offset: x = to the side, y = height above the street, z = along the hero's facing (from the focus point)
            switch (shot)
            {
                case CamShot.LowHero: offset = new Vector3(1.5f, 0.5f, 2.2f); lookHeight = 1.5f; fov = 50f; roll = 3f; break;
                case CamShot.OverShoulder: offset = new Vector3(-0.9f, 1.9f, -2.2f); lookHeight = 1.1f; fov = 50f; roll = 0f; break;
                case CamShot.Overhead: offset = new Vector3(1.0f, 3.6f, -1.2f); lookHeight = 0.2f; fov = 48f; roll = 0f; break;
                case CamShot.Wide: offset = new Vector3(3.4f, 2.0f, 0.6f); lookHeight = 1.3f; fov = 50f; roll = 0f; break;
                case CamShot.GroundImpact: offset = new Vector3(2.0f, 0.35f, 0.6f); lookHeight = 0.5f; fov = 42f; roll = -6f; break;
                case CamShot.Front: offset = new Vector3(0.7f, 1.4f, 2.6f); lookHeight = 1.2f; fov = 46f; roll = 0f; break;
                default: offset = new Vector3(2.5f, 1.3f, 0.3f); lookHeight = 1.1f; fov = 45f; roll = 0f; break;
            }
        }

        /// <summary>
        /// Close-up camera for the current beat: frames hero and victim together, drifts slightly during the
        /// motion, and slowly pushes in while a prompt is waiting.
        /// </summary>
        void UpdateWrestleCamera(WrestleStep st, float t, bool waiting)
        {
            if (cam == null || wrestleVictim == null || !wrestleMove.cinematic) return;
            Vector3 victimPoint = wrestleMove.carry ? wrestleVictim.transform.position + Vector3.up * 0.9f
                                                    : wrestleVictim.transform.position + Vector3.up * 0.3f;
            Vector3 focus = Vector3.Lerp(transform.position + Vector3.up * 1.1f, victimPoint, 0.5f);

            ShotLayout(st.shot, out var offset, out float lookHeight, out float fov, out float roll);
            offset.x *= wrestleSide;
            roll *= wrestleSide;
            float push = waiting ? Mathf.Lerp(1f, 0.8f, Mathf.Clamp01(QteProgress)) : 1f;
            Quaternion frame = wrestleRotation * Quaternion.Euler(0f, waiting ? 0f : (t - 0.5f) * 10f * wrestleSide, 0f);
            Vector3 flat = frame * new Vector3(offset.x, 0f, offset.z) * push;
            var camPos = new Vector3(focus.x + flat.x, wrestleOrigin.y + offset.y, focus.z + flat.z);
            var look = new Vector3(focus.x, wrestleOrigin.y + lookHeight, focus.z);

            // Keep both whole bodies in frame: hero from feet to head, the rowdy within ~0.9 m of his hips.
            Vector3 heroFeet = transform.position, heroHead = transform.position + Vector3.up * 1.85f;
            Vector3 center = (heroFeet + heroHead + victimPoint * 2f) * 0.25f;
            float radius = Mathf.Max(Vector3.Distance(center, heroFeet), Vector3.Distance(center, heroHead),
                Vector3.Distance(center, victimPoint) + 0.9f);
            cam.SetCinematicFraming(camPos, look, fov * Mathf.Lerp(0.9f, 1f, push), roll, center, radius * Mathf.Lerp(0.85f, 1f, push));
        }

        void UpdateQte(WrestleStep st)
        {
            float p = QteProgress;
            if (GM != null) GM.SlowMo(0.3f, 0.05f);   // the world crawls while the prompt is up

            bool pressedRight = (st.qte == QteKey.Light && GameInput.LightPressed)
                             || (st.qte == QteKey.Heavy && GameInput.HeavyPressed)
                             || (st.qte == QteKey.Jump && GameInput.JumpPressed);
            bool pressedWrong = !pressedRight &&
                ((st.qte != QteKey.Light && GameInput.LightPressed) || (st.qte != QteKey.Heavy && GameInput.HeavyPressed)
                 || (st.qte != QteKey.Jump && GameInput.JumpPressed) || GameInput.GrabPressed || GameInput.BlockPressed || GameInput.DodgePressed);

            if (pressedRight)
            {
                bool perfect = Mathf.Abs(p - QtePerfectAt) <= QtePerfectWindow;
                ResolveQte(perfect ? 2 : 1);
            }
            else if (pressedWrong || p >= 1f)
            {
                ResolveQte(0);
            }
        }

        /// <summary>2 = perfect, 1 = good, 0 = miss. The move carries on either way.</summary>
        void ResolveQte(int grade)
        {
            qteActive = false;
            qteDone++;
            lightBufferedUntil = heavyBufferedUntil = 0f;
            QteResultTime = Time.unscaledTime;
            if (grade == 2)
            {
                qtePerfects++;
                QteResultText = "PERFECT!";
                AddGethu(4f);
                if (GM != null) GM.HitStop(0.04f);
            }
            else if (grade == 1)
            {
                QteResultText = "GOOD";
            }
            else
            {
                qteMisses++;
                QteResultText = "MISS";
            }
            BeginStepMotion(wrestleMove.steps[wrestleStep]);
        }

        void FinishWrestleStep(WrestleStep st)
        {
            var victim = wrestleVictim;
            if (st.impact)
            {
                WrestleImpact(st);
                return;
            }
            if (wrestleMove.throwAtEnd && wrestleStep == wrestleMove.steps.Count - 1 && victim != null)
            {
                // Let go at the end of the swing: he flies off and the ragdoll takes over.
                Vector3 dir = wrestleRotation * Vector3.forward;
                Visual.SetGrip(null, false);
                victim.Launch(this, dir, EnemyFighter.LaunchKind.Throw);
                victim.ReceiveHit(new HitInfo { attacker = this, damage = wrestleMove.throwDamage, direction = dir, hitsDowned = true });
                RegisterHits(1, 8f);
                if (GM != null) { GM.Shake(0.2f); GM.AddPopup("THROW!", victim.Center + Vector3.up, new Color(1f, 0.85f, 0.4f), 0.8f); }
                return;
            }
            if (st.damage <= 0f || victim == null) return;

            // A blow while holding him (never the killing one: that's for the slam).
            float dmg = Mathf.Min(st.damage * WrestleDamageMultiplier, victim.Health - 1f);
            if (dmg > 0f)
                victim.ReceiveHit(new HitInfo { attacker = this, damage = dmg, direction = transform.forward, unblockable = true, hitsDowned = true });
            victim.Visual.Flinch(1f);
            FX.HitSpark(victim.Center, false);
            RegisterHits(1, 2f);
            if (GM != null) { GM.HitStop(0.05f); GM.Shake(0.12f); }
        }

        void WrestleImpact(WrestleStep st)
        {
            var victim = wrestleVictim;
            var move = wrestleMove;
            Vector3 fwd = wrestleRotation * Vector3.forward;
            Vector3 dir = Flat(victim.transform.position - transform.position);
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : fwd;
            Vector3 impactPoint = victim.transform.position;
            impactPoint.y = wrestleOrigin.y;

            float damage = st.damage * WrestleDamageMultiplier;
            if (st.keepHold && move.carry)
            {
                // First slam of a double slam: he hits the ground but you don't let go.
                victim.ReceiveHit(new HitInfo { attacker = this, damage = damage, direction = dir, unblockable = true, hitsDowned = true });
                victim.Visual.Flinch(1f);
                RegisterHits(1, 0f);
                FX.Dust(impactPoint, 14);
                if (GM != null)
                {
                    GM.HitStop(0.1f);
                    GM.Shake(0.4f, 0.25f);
                    GM.AddPopup("DHAB!", impactPoint + Vector3.up * 1.2f, new Color(1f, 0.8f, 0.3f), 1.1f);
                }
                if (cam != null) cam.ZoomPunch(6f);
                return;
            }

            wrestleReleased = true;
            if (move.carry && victim.Visual.CanRagdoll)
            {
                // He goes limp from exactly the pose he hit the ground in, and physics takes it from there.
                victim.ReleaseFromGrab(dir * 1.5f + Vector3.down * 3f, knockDown: true);
            }
            else if (move.carry)
            {
                victim.Visual.ClearCarry();
                victim.HoldAt(impactPoint, wrestleVictimRotation);
                victim.ReleaseFromGrab(dir * 1.5f, knockDown: true);
            }
            else
            {
                victim.PinDown(1.6f);
                Visual.ReleaseGripSlowly(0.35f);
            }
            victim.ReceiveHit(new HitInfo
            {
                attacker = this, damage = damage, direction = dir, knockback = 1f,
                heavy = true, unblockable = true, hitsDowned = true,
            });
            RegisterHits(1, 0f);
            NotifyMoveLanded(move.name);

            if (move.prop != null)
            {
                move.prop.Break(fwd);
                if (victim.Visual.IsRagdoll) victim.Visual.StartRagdoll(fwd * 3f + Vector3.down);
            }
            else if (move.wall)
            {
                Vector3 wallPoint = impactPoint + fwd * 0.4f + Vector3.up * 1.1f;
                FX.Dust(wallPoint, 18);
                FX.Burst(wallPoint, 12, 4f, 0.08f, new Color(0.6f, 0.6f, 0.55f), Color.black, 0.5f, -8f);
            }
            else
            {
                FX.Dust(impactPoint, move.shockwave ? 30 : 16);
            }

            if (move.shockwave)
            {
                // Knocks down everyone around, except the man you just drove into the road.
                int hits = 0;
                foreach (var f in All.ToArray())
                {
                    if (f == null || f == victim || f.team == team || f.IsDead) continue;
                    Vector3 to = Flat(f.transform.position - impactPoint);
                    if (to.magnitude > 3.4f) continue;
                    var r = f.ReceiveHit(new HitInfo
                    {
                        attacker = this, damage = 15f, direction = to.sqrMagnitude > 0.01f ? to.normalized : fwd,
                        knockback = 6f, launch = 4f, heavy = true, unblockable = true, hitsDowned = true,
                    });
                    if (r == HitResult.Hit) hits++;
                }
                RegisterHits(hits, 0f);
            }

            if (GM != null)
            {
                GM.HitStop(0.14f);
                GM.Shake(move.shockwave ? 0.7f : 0.5f, 0.35f);
                bool flawless = qtePerfects > 0 && qtePerfects == move.QteCount;
                GM.AddPopup(flawless ? move.name + " — FLAWLESS!" : move.name + "!", impactPoint + Vector3.up * 1.6f,
                    new Color(1f, 0.75f, 0.15f), flawless ? 1.7f : 1.5f);
                if (flawless) AddGethu(10f);
            }
            if (cam != null) cam.ZoomPunch(9f);
            if (GM != null) GM.SlowMo(0.25f, 0.45f);   // savour the landing
        }

        void EndWrestle()
        {
            var victim = wrestleVictim;
            if (victim != null && !victim.IsDead && victim.IsGrabbed && victim.Visual.CanRagdoll)
            {
                victim.ReleaseFromGrab(Vector3.zero, knockDown: true);
            }
            else if (victim != null && !victim.IsDead && victim.IsGrabbed)
            {
                victim.Visual.ClearCarry();
                Vector3 p = victim.transform.position;
                p.y = wrestleOrigin.y;
                victim.HoldAt(p, victim.transform.rotation);
                victim.ReleaseFromGrab(Vector3.zero, knockDown: true);
            }
            wrestleMove = null;
            wrestleVictim = null;
            qteActive = false;
            Visual.ReleaseGripSlowly(0.45f);   // let go and straighten up gradually after the slam
            Visual.ClearCarry();
            lightBufferedUntil = heavyBufferedUntil = 0f;
            if (cam != null) cam.EndCinematic(0.6f);

            Vector3 pos = transform.position;
            pos.y = wrestleOrigin.y;
            Motor.SetFrozen(false);
            Motor.Teleport(pos);
            if (State == PlayerState.Wrestle)
            {
                SetState(PlayerState.Stagger);   // short recovery after landing the move
                stateTimer = 0.05f;
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace MadrasGethu
{
    /// <summary>Which button a quick-time prompt asks for.</summary>
    public enum QteKey { None, Light, Heavy, Jump }

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
        float roomAhead, roomBehind;
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

        WrestleMove BodySlam()
        {
            return new WrestleMove { name = "BODY SLAM", id = MoveId.BodySlam, cost = GethuBar }
                .Step(With(S(QteKey.Light, "LIFT", 0.45f, FighterPose.Lift), new Vector3(0f, 1.75f, 0.2f), -90f, 0.1f))
                .Step(Impact(With(S(QteKey.Heavy, "SLAM", 0.28f, FighterPose.Slam), new Vector3(0f, 0.2f, 1.1f), -90f, 0.35f), 30f));
        }

        WrestleMove DhobiPachad()
        {
            return new WrestleMove { name = "DHOBI PACHAD", id = MoveId.DhobiPachad, cost = GethuBar }
                .Step(With(S(QteKey.Light, "OVER THE SHOULDER", 0.35f, FighterPose.Lift), new Vector3(0f, 1.6f, 0.25f), -70f, 0f))
                .Step(Impact(With(S(QteKey.Heavy, "BEAT HIM DOWN", 0.42f, FighterPose.Throw), new Vector3(0f, 0.2f, -1.2f), -270f, 0.9f), 32f));
        }

        WrestleMove GermanSuplex()
        {
            var m = new WrestleMove { name = "GERMAN SUPLEX", id = MoveId.GermanSuplex, cost = GethuBar, victimFacesAway = true };
            for (int i = 0; i < 3; i++)
            {
                var shot = With(S(QteKey.Light, "BODY SHOT", 0.22f, FighterPose.Knee), new Vector3(0f, 0.9f, 0.5f), 0f, 0f);
                shot.damage = 5f;
                m.Step(shot);
            }
            return m.Step(Impact(With(S(QteKey.Heavy, "SUPLEX", 0.5f, FighterPose.Suplex), new Vector3(0f, 0.25f, -0.9f), -180f, 1.1f), 34f));
        }

        WrestleMove Powerbomb()
        {
            var jump = With(S(QteKey.Jump, "HIGHER", 0.3f, FighterPose.Jump), new Vector3(0f, 2.6f, 0.3f), -125f, 0f);
            jump.hero = new Vector3(0f, 0.5f, 0f);
            var drive = Impact(With(S(QteKey.Heavy, "DRIVE HIM DOWN", 0.22f, FighterPose.Slam), new Vector3(0f, 0.2f, 1.05f), -90f, 0f), 45f);
            drive.hero = new Vector3(0f, 0f, 0.2f);
            return new WrestleMove { name = "POWERBOMB", id = MoveId.Powerbomb, cost = maxGethu, shockwave = true }
                .Step(With(S(QteKey.Light, "LIFT", 0.45f, FighterPose.Lift), new Vector3(0f, 2.0f, 0.25f), -110f, 0.1f))
                .Step(jump)
                .Step(drive);
        }

        WrestleMove Spear()
        {
            var run = With(S(QteKey.None, null, 0.28f, FighterPose.Spear), new Vector3(0f, 1.0f, 1.7f), -30f, 0f);
            run.hero = new Vector3(0f, 0f, 1.0f);
            var hit = Impact(With(S(QteKey.Heavy, "SPEAR", 0.2f, FighterPose.Spear), new Vector3(0f, 0.25f, 2.8f), -90f, 0f), 28f);
            hit.hero = new Vector3(0f, 0f, 1.9f);
            return new WrestleMove { name = "SPEAR", id = MoveId.Spear, cost = GethuBar }.Step(run).Step(hit);
        }

        WrestleMove ElbowDrop(Vector3 victimLocal)
        {
            float z = Mathf.Max(0.6f, victimLocal.z);
            var jump = S(QteKey.Jump, "JUMP", 0.4f, FighterPose.Jump);
            jump.hero = new Vector3(victimLocal.x, 1.2f, z - 0.6f);
            var drop = Impact(S(QteKey.Light, "ELBOW", 0.18f, FighterPose.ElbowDrop), 26f);
            drop.hero = new Vector3(victimLocal.x, 0f, z - 0.5f);
            return new WrestleMove { name = "ELBOW DROP", id = MoveId.ElbowDrop, cost = GethuBar, carry = false }.Step(jump).Step(drop);
        }

        WrestleMove Spinebuster(float wallDistance)
        {
            float d = Mathf.Max(1.0f, wallDistance);
            var drive = Impact(With(S(QteKey.Heavy, "INTO THE WALL", 0.3f, FighterPose.Spear), new Vector3(0f, 1.1f, d - 0.35f), -75f, 0f), 36f);
            drive.hero = new Vector3(0f, 0f, Mathf.Max(0f, d - 1.0f));
            return new WrestleMove { name = "SPINEBUSTER", id = MoveId.Spinebuster, cost = GethuBar, wall = true }
                .Step(With(S(QteKey.Light, "LIFT", 0.35f, FighterPose.Lift), new Vector3(0f, 1.5f, 0.45f), -50f, 0f))
                .Step(drive);
        }

        static WrestleStep With(WrestleStep s, Vector3 hip, float pitch, float arc)
        {
            s.hip = hip;
            s.pitch = pitch;
            s.arc = arc;
            return s;
        }

        static WrestleStep Impact(WrestleStep s, float damage)
        {
            s.impact = true;
            s.damage = damage;
            return s;
        }

        // ------------------------------------------------------------------ choosing a finisher

        /// <summary>The wrestling finisher Q would do right now while holding someone (null = none).</summary>
        WrestleMove PickGrabFinisher(Vector3 aim, Vector3 move)
        {
            if (grabbed == null) return null;
            if (MoveUnlocks.Has(MoveId.Spinebuster) && !grabFromBehind && FindWall(grabbed.Center, aim, 2.6f, out var wallHit))
                return Spinebuster(Flat(wallHit.point - transform.position).magnitude);
            if (!grabFromBehind && Gethu >= maxGethu && MoveUnlocks.Has(MoveId.Powerbomb)) return Powerbomb();
            if (grabFromBehind && MoveUnlocks.Has(MoveId.GermanSuplex)) return GermanSuplex();
            if (!grabFromBehind)
            {
                // Dragging him around: swing him over your shoulder. Standing still: lift and slam.
                if (move.sqrMagnitude > 0.25f && MoveUnlocks.Has(MoveId.DhobiPachad)) return DhobiPachad();
                if (MoveUnlocks.Has(MoveId.BodySlam)) return BodySlam();
            }
            return MoveUnlocks.Has(MoveId.Powerbomb) ? Powerbomb() : null;
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

            var downed = DownedNearby();
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
                target.EnterGrabbed(false);
                Gethu -= GethuBar;
                StartWrestle(Spear(), target);
                return true;
            }
            return false;
        }

        EnemyFighter DownedNearby()
        {
            var f = CombatUtil.FindTarget(this, MoveInputWorld().sqrMagnitude > 0.01f ? MoveInputWorld() : transform.forward, 2.4f, includeDowned: true) as EnemyFighter;
            return f != null && f.IsDowned && !f.IsDead ? f : null;
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
                if (MoveUnlocks.Has(MoveId.ElbowDrop))
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
            if (!Physics.SphereCast(from, 0.25f, aim, out hit, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
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
            var hits = Physics.SphereCastAll(Center, 0.3f, direction, max, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
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
            roomAhead = Room(fwd, 4f);
            roomBehind = Room(-fwd, 3f);

            curHip = Quaternion.Inverse(wrestleRotation) * (victim.transform.position + Vector3.up * 0.9f - wrestleOrigin);
            curHero = Vector3.zero;
            curPitch = 0f;
            qtePerfects = qteMisses = qteDone = 0;
            qteActive = false;

            if (!move.carry) victim.PinDown(3f);   // he stays down until the elbow lands
            Motor.SetFrozen(true);
            Visual.SetGrip(null, false);
            SetState(PlayerState.Wrestle);
            lightIndex = 0;
            lightBufferedUntil = heavyBufferedUntil = 0f;
            if (GM != null)
            {
                GM.AddPopup(move.name, Center + Vector3.up * 1.3f, new Color(1f, 0.75f, 0.15f), 1.4f);
                GM.SlowMo(0.4f, 0.3f);
            }
            if (cam != null) cam.FovKick(6f);
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
            wrestleStepTimer = 0f;
            if (st.qte != QteKey.None)
            {
                qteActive = true;
                qteStart = Time.unscaledTime;
                if (wrestleMove.carry) Visual.Play(FighterPose.Grab, 999f);   // keep hold of him while waiting
            }
            else
            {
                BeginStepMotion(st);
            }
        }

        void BeginStepMotion(WrestleStep st)
        {
            Visual.Play(st.heroPose, st.duration + 0.15f, st.duration + 0.1f);
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
                UpdateQte(st);
                return;
            }

            wrestleStepTimer += Time.deltaTime;
            float t = Mathf.Clamp01(wrestleStepTimer / Mathf.Max(0.01f, st.duration));
            ApplyWrestlePose(st, t);
            if (t < 1f) return;

            curHip = st.hip ?? stepHipFrom;
            curHero = st.hero ?? stepHeroFrom;
            curPitch = st.pitch ?? stepPitchFrom;
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
            transform.position = wrestleOrigin + wrestleRotation * hero;
            transform.rotation = wrestleRotation;

            if (!wrestleMove.carry) return;
            Vector3 hip = Vector3.Lerp(stepHipFrom, st.hip ?? stepHipFrom, e) + Vector3.up * st.arc * lift;
            hip.z = Mathf.Clamp(hip.z, -(roomBehind - 0.3f), roomAhead - 0.3f);
            float pitch = Mathf.Lerp(stepPitchFrom, st.pitch ?? stepPitchFrom, e);
            wrestleVictim.HoldAt(wrestleOrigin + wrestleRotation * hip - Vector3.up * 0.9f, wrestleVictimRotation);
            wrestleVictim.Visual.SetCarry(Quaternion.Euler(pitch, 0f, 0f));
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
            Color c;
            if (grade == 2)
            {
                qtePerfects++;
                QteResultText = "PERFECT!";
                c = new Color(0.4f, 1f, 0.5f);
                AddGethu(4f);
                if (GM != null) GM.HitStop(0.04f);
            }
            else if (grade == 1)
            {
                QteResultText = "GOOD";
                c = new Color(1f, 0.9f, 0.5f);
            }
            else
            {
                qteMisses++;
                QteResultText = "MISS";
                c = new Color(1f, 0.45f, 0.35f);
            }
            if (GM != null) GM.AddPopup(QteResultText, Center + Vector3.up * 1.0f, c, 0.9f);
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
            if (move.carry)
            {
                victim.Visual.ClearCarry();
                victim.HoldAt(impactPoint, wrestleVictimRotation);
                victim.ReleaseFromGrab(dir * 1.5f, knockDown: true);
            }
            else
            {
                victim.PinDown(1.6f);
            }
            victim.ReceiveHit(new HitInfo
            {
                attacker = this, damage = damage, direction = dir, knockback = 1f,
                heavy = true, unblockable = true, hitsDowned = true,
            });
            RegisterHits(1, 0f);

            if (move.wall)
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
                var shock = new AttackData("Shockwave", FighterPose.Slam, 15f)
                    { range = 3.4f, arc = 360f, knockback = 7f, launch = 5f, heavy = true, unblockable = true, hitsDowned = true };
                var r = CombatUtil.Strike(this, shock, 1f);
                RegisterHits(r.hits, 0f);
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
            if (cam != null) cam.FovKick(8f);
        }

        void EndWrestle()
        {
            var victim = wrestleVictim;
            if (victim != null && !victim.IsDead && victim.IsGrabbed)
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
            lightBufferedUntil = heavyBufferedUntil = 0f;

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

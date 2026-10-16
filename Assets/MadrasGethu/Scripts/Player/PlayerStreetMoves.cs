using UnityEngine;

namespace MadrasGethu
{
    /// <summary>
    /// Street moves added on top of the boxing combos:
    ///   dodge, RMB ............. Spinning Heel Kick
    ///   jump, RMB .............. Superman Punch
    ///   S + LMB, then LMB ...... Kaal Odaippu: low kick drops him to a knee, knee to the face
    ///   parry (F), then RMB .... Liver Shot: slip inside, hook to the ribs, he folds over
    ///   sprint + E ............. Rugby Tackle, then LMB punches from the mount, RMB finishes
    /// Holding him:
    ///   E with another rowdy close .... Mutta Mothal (head clash)
    ///   hold RMB ....................... Arm Twist (in PlayerGrabCombos.cs)
    ///   LMB then F ..................... Ear Clap (in PlayerGrabCombos.cs)
    ///   S + F .......................... Trip & Stomp
    ///   SHIFT + E ...................... Bowling Throw
    /// The finisher-only moves (Giant Swing, Choke-Out, Thai Clinch Knees, Kadal Throw, Ice Box Slam) are in PlayerWrestling.cs.
    /// </summary>
    public partial class PlayerFighter
    {
        AttackData spinHeelKick, supermanPunch, lowKick, kneeToFace, liverShot;

        EnemyFighter lastParried;
        float lastParryTime = -10f;
        EnemyFighter lowKicked;
        float lowKickTime = -10f;

        void BuildStreetMoves()
        {
            spinHeelKick = new AttackData("Spinning Heel Kick", FighterPose.SpinKick, 20f)
                { announce = true, startup = 0.3f, recovery = 0.55f, range = 2.1f, arc = 360f, lunge = 0.6f, knockback = 6f, launch = 3.5f, heavy = true, gethuGain = 12f, hitStop = 0.12f, shake = 0.35f };
            supermanPunch = new AttackData("Superman Punch", FighterPose.SupermanPunch, 18f)
                { announce = true, startup = 0.22f, recovery = 0.5f, range = 1.9f, arc = 90f, lunge = 2.6f, knockback = 9f, launch = 2f, heavy = true, gethuGain = 11f, hitStop = 0.12f, shake = 0.3f };
            // Under the guard: it can't be blocked, but on its own it only buckles the knee.
            lowKick = new AttackData("Low Kick", FighterPose.LowKick, 9f)
                { announce = true, startup = 0.2f, recovery = 0.4f, range = 1.8f, arc = 100f, lunge = 0.8f, knockback = 0.5f, unblockable = true, gethuGain = 7f, hitStop = 0.08f, shake = 0.15f };
            kneeToFace = new AttackData("Kaal Odaippu", FighterPose.Knee, 18f)
                { announce = true, startup = 0.18f, recovery = 0.5f, range = 1.6f, lunge = 0.6f, knockback = 5f, launch = 3.5f, heavy = true, unblockable = true, gethuGain = 12f, hitStop = 0.12f, shake = 0.35f };
            liverShot = new AttackData("Liver Shot", FighterPose.BodyBlow, 16f)
                { announce = true, startup = 0.14f, recovery = 0.45f, range = 1.8f, arc = 100f, lunge = 0.9f, knockback = 1f, heavy = true, unblockable = true, gethuGain = 12f, hitStop = 0.12f, shake = 0.25f };
        }

        /// <summary>Reactions to the street moves once they connect.</summary>
        void OnStreetMoveLanded(AttackData attack, Fighter first)
        {
            var enemy = first as EnemyFighter;
            if (enemy == null || enemy.IsDead) return;
            if (attack == lowKick)
            {
                enemy.KneelDown(2.2f);
                lowKicked = enemy;
                lowKickTime = Time.time;
            }
            else if (attack == liverShot)
            {
                enemy.DoubleOver(3f);
            }
        }

        /// <summary>S (stepping away from him) + LMB: the low kick.</summary>
        Fighter LowKickTarget()
        {
            if (!MoveUnlocks.Has(MoveId.KaalOdaippu) || HeldWeapon != null) return null;
            Vector3 input = MoveInputWorld();
            if (input.sqrMagnitude < 0.25f) return null;
            var focus = CombatUtil.FindTarget(this, Vector3.zero, 2.6f);
            if (focus == null) return null;
            Vector3 to = Flat(focus.transform.position - transform.position);
            return Vector3.Dot(input.normalized, to.normalized) < -0.5f ? focus : null;
        }

        /// <summary>The rowdy you just dropped to one knee (used up when the knee is thrown).</summary>
        EnemyFighter KneelingTarget()
        {
            var k = lowKicked;
            if (k == null || k.IsDead || !k.IsDazed || Time.time - lowKickTime > 2.3f) return null;
            if (Flat(k.transform.position - transform.position).magnitude > 2.4f) return null;
            lowKicked = null;
            return k;
        }

        /// <summary>The rowdy you just parried, if RMB comes quickly enough (used up when the shot is thrown).</summary>
        EnemyFighter LiverShotTarget()
        {
            var p = lastParried;
            if (p == null || !MoveUnlocks.Has(MoveId.LiverShot) || p.IsDead || p.IsDowned || Time.time - lastParryTime > 1.0f) return null;
            if (Flat(p.transform.position - transform.position).magnitude > 2.8f) return null;
            lastParried = null;
            return p;
        }

        // ------------------------------------------------------------------ rugby tackle and the mount

        /// <summary>Sprint + E at a rowdy a few metres ahead: take him down and get on top.</summary>
        bool TryStartTackle()
        {
            if (!MoveUnlocks.Has(MoveId.RugbyTackle) || HeldWeapon != null) return false;
            if (!GameInput.Sprint || Motor.MoveVelocity.magnitude < runSpeed * 0.8f) return false;
            var target = CombatUtil.FindTarget(this, transform.forward, 5f) as EnemyFighter;
            if (target == null || !target.CanBeGrabbed) return false;
            float gap = Flat(target.transform.position - transform.position).magnitude;
            if (gap < 1.0f) return false;
            Motor.FaceDirection(target.transform.position - transform.position, instant: true);
            target.EnterGrabbed(false);
            StartWrestle(Tackle(gap), target);
            return true;
        }

        EnemyFighter mountVictim;
        float mountGroundY;
        int mountPunches;
        float mountNextPunch, mountPunchLands = -1f, mountLastInput, mountFinishAt = -1f;
        float mountTime, mountStartPitch, mountPunchLean;
        Vector3 mountFrom;
        bool mountLeft;
        const int MountMaxPunches = 10;
        const float MountSettle = 0.3f;   // getting from the tackle onto his hips

        /// <summary>After the tackle: he lies limp on his back (ragdoll), you straddle his hips.</summary>
        void StartMount(EnemyFighter victim)
        {
            mountVictim = victim;
            mountGroundY = wrestleOrigin.y;
            mountPunches = 0;
            mountNextPunch = Time.time + MountSettle;
            mountPunchLands = -1f;
            mountFinishAt = -1f;
            mountLastInput = Time.time;
            mountTime = 0f;
            mountStartPitch = curBodyPitch;
            mountFrom = transform.position;
            mountPunchLean = 0f;
            wrestleMove = null;
            wrestleVictim = null;
            qteActive = false;
            lightBufferedUntil = heavyBufferedUntil = 0f;
            victim.PinDown(2f);
            SetState(PlayerState.Mount);
        }

        Vector3 MountHips(EnemyFighter v)
        {
            Vector3 p = v.Visual.IsRagdoll ? v.Visual.RagdollCenter : v.transform.position + Vector3.up * 0.25f;
            return p;
        }

        void UpdateMount()
        {
            var victim = mountVictim;
            if (victim == null || victim.IsDead)
            {
                EndMount();
                return;
            }
            mountTime += Time.deltaTime;
            victim.PinDown(1.5f);   // he stays down while you're on him

            // Straddle his hips, facing his head; settle from the tackle (lying on him) up onto your knees.
            Vector3 hips = MountHips(victim);
            Vector3 head = victim.Visual.RagdollHead;
            Vector3 toHead = Flat(head - hips);
            if (toHead.sqrMagnitude < 0.01f) toHead = transform.forward;
            Vector3 seat = new Vector3(hips.x, mountGroundY, hips.z) - toHead.normalized * 0.05f;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(mountTime / MountSettle));
            Motor.Teleport(Vector3.Lerp(mountFrom, seat, k));
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toHead.normalized), k);
            float pitch = Mathf.Lerp(mountStartPitch, 0f, k);
            if (Mathf.Abs(pitch) > 0.5f) Visual.SetCarry(Quaternion.Euler(pitch, 0f, 0f));
            else Visual.ClearCarry();

            // Kneeling over him; every punch leans the body down into it.
            mountPunchLean = Mathf.MoveTowards(mountPunchLean, 0f, Time.deltaTime * 90f);
            Visual.SetGrapple(victim, GripKind.Waist, -0.62f, 30f + mountPunchLean);
            Visual.FreeHands(0.2f);
            MountCamera(hips);

            if (mountPunchLands >= 0f && Time.time >= mountPunchLands)
            {
                mountPunchLands = -1f;
                float dmg = Mathf.Min(5f * (MassMode ? massModeDamageMultiplier : 1f), Mathf.Max(0f, victim.Health - 1f));
                victim.ReceiveHit(new HitInfo { attacker = this, damage = dmg, direction = transform.forward, unblockable = true, hitsDowned = true });
                // His head snaps away from the fist.
                Vector3 side = transform.right * (mountLeft ? 1f : -1f);
                victim.Visual.RagdollHeadJolt(side * 2.2f + Vector3.down * 1.2f);
                FX.HitSpark(head, false);
                RegisterHits(1, 3f);
                if (GM != null) { GM.HitStop(0.04f); GM.Shake(0.12f); }
            }

            if (mountFinishAt >= 0f)
            {
                if (Time.time < mountFinishAt) return;
                float dmg = 20f * (MassMode ? massModeDamageMultiplier : 1f);
                victim.ReceiveHit(new HitInfo { attacker = this, damage = dmg, direction = transform.forward, heavy = true, unblockable = true, hitsDowned = true });
                victim.Visual.RagdollHeadJolt(Vector3.down * 3f);
                FX.HitSpark(head, true);
                FX.Dust(head, 10);
                RegisterHits(1, 10f);
                NotifyMoveLanded("Rugby Tackle");
                if (GM != null)
                {
                    GM.HitStop(0.12f);
                    GM.Shake(0.4f, 0.25f);
                    GM.AddPopup("GROUND AND POUND!", head + Vector3.up * 1.2f, new Color(1f, 0.75f, 0.15f), 1.3f);
                }
                if (cam != null) cam.ZoomPunch(6f);
                EndMount();
                return;
            }

            Prompts.Add(new PromptItem { key = "LMB", label = "Punch" });
            Prompts.Add(new PromptItem { key = "RMB", label = "Finish" });

            if (GameInput.LightPressed && Time.time >= mountNextPunch && mountPunches < MountMaxPunches)
            {
                float t = Visual.PlayTimed(FighterPose.GroundPunch, 0.14f, 0.2f, 2.6f, 0.2f);
                mountPunchLands = Time.time + t;
                mountNextPunch = Time.time + Mathf.Max(0.22f, t + 0.08f);
                mountPunches++;
                mountLeft = !mountLeft;
                mountPunchLean = 18f;
                mountLastInput = Time.time;
            }
            bool finish = GameInput.HeavyPressed || (mountPunches >= MountMaxPunches && mountPunchLands < 0f);
            if (finish && mountPunchLands < 0f && mountTime >= MountSettle)
            {
                // Both fists together, straight down.
                mountFinishAt = Time.time + Visual.PlayTimed(FighterPose.Slam, 0.3f, 0.4f, 1.6f, 0.4f);
                mountPunchLean = 30f;
                return;
            }
            lightBufferedUntil = heavyBufferedUntil = 0f;
            if (Time.time - mountLastInput > 3f) EndMount();   // nothing more to give: get up
        }

        void MountCamera(Vector3 hips)
        {
            if (cam == null) return;
            Vector3 fwd = transform.forward;
            Vector3 side = Vector3.Cross(Vector3.up, fwd);
            if (Vector3.Dot(cam.transform.position - transform.position, side) < 0f) side = -side;
            Vector3 mid = Vector3.Lerp(transform.position + Vector3.up * 0.8f, hips, 0.5f) + fwd * 0.2f;
            cam.SetCinematicFraming(mid + side * 2.6f + Vector3.up * 0.9f - fwd * 0.4f, mid, 46f, 0f, mid, 1.4f);
        }

        void EndMount()
        {
            var victim = mountVictim;
            mountVictim = null;
            if (victim != null && !victim.IsDead)
            {
                if (victim.IsGrabbed)   // no ragdoll on this body: put him down lying
                {
                    victim.Visual.ClearCarry();
                    Vector3 p = victim.transform.position;
                    p.y = mountGroundY;
                    victim.HoldAt(p, victim.transform.rotation);
                    victim.ReleaseFromGrab(Vector3.down, knockDown: true);
                }
                victim.PinDown(1.2f);
            }
            Visual.ReleaseGripSlowly(0.4f);
            Visual.ClearCarry();
            if (cam != null) cam.EndCinematic(0.4f);
            Vector3 pos = transform.position;
            pos.y = mountGroundY;
            Motor.SetFrozen(false);
            Motor.Teleport(pos);
            SetState(PlayerState.Stagger);
            stateTimer = 0.05f;
        }

        // ------------------------------------------------------------------ Mutta Mothal (head clash)

        EnemyFighter clashVictim, clashOther;
        Vector3 clashDir, clashVictimStart, clashPoint, clashHeroStart, clashHeroEnd;
        bool clashed;
        const float ClashTime = 0.38f;

        /// <summary>Another rowdy close enough to bang heads with the one you're holding.</summary>
        EnemyFighter HeadClashPartner()
        {
            if (grabbed == null || !MoveUnlocks.Has(MoveId.MuttaMothal)) return null;
            EnemyFighter best = null;
            float bestDist = 3.2f;
            foreach (var f in All)
            {
                if (!(f is EnemyFighter e) || e == grabbed || e.IsDead || e.IsDowned || e.IsGrabbed || e.team == team) continue;
                float d = Flat(e.transform.position - grabbed.transform.position).magnitude;
                if (d < bestDist && Flat(e.transform.position - transform.position).magnitude < 3.8f)
                {
                    bestDist = d;
                    best = e;
                }
            }
            return best;
        }

        /// <summary>E while holding him: head clash with a rowdy nearby, or Shift + E: bowling throw.</summary>
        bool TryGrabEMove(Vector3 aim)
        {
            var partner = HeadClashPartner();
            if (partner != null)
            {
                StartHeadClash(partner);
                return true;
            }
            if (GameInput.Sprint && MoveUnlocks.Has(MoveId.BowlingThrow))
            {
                StartGrabFinisher(BowlingThrow(), aim);
                return true;
            }
            return false;
        }

        /// <summary>F while holding him: LMB just before = ear clap, S held = trip and stomp.</summary>
        bool TryGrabFMove()
        {
            if (codeFirst == 'L' && Time.time - codeTime < 0.3f && MoveUnlocks.Has(MoveId.EarClap))
            {
                codeFirst = '\0';
                StartStrikeCombo(EarClapCombo());
                return true;
            }
            if (GameInput.Move.y < -0.5f && MoveUnlocks.Has(MoveId.TripStomp))
            {
                Vector3 toHim = Flat(grabbed.transform.position - transform.position);
                StartGrabFinisher(TripStomp(), toHim.sqrMagnitude > 0.01f ? toHim.normalized : transform.forward);
                return true;
            }
            return false;
        }

        void StartHeadClash(EnemyFighter other)
        {
            clashVictim = grabbed;
            clashOther = other;
            LetGo();
            other.EnterGrabbed(false);   // caught flat-footed: he doesn't get out of the way
            clashDir = Flat(other.transform.position - clashVictim.transform.position);
            clashDir = clashDir.sqrMagnitude > 0.01f ? clashDir.normalized : transform.forward;
            clashVictimStart = clashVictim.transform.position;
            clashPoint = other.transform.position - clashDir * 0.5f;
            clashHeroStart = transform.position;
            clashHeroEnd = clashPoint - clashDir * 0.6f;
            clashed = false;
            Motor.SetDesiredVelocity(Vector3.zero, instant: true);   // no drag speed left over from walking him
            Motor.FaceDirection(clashDir, instant: true);
            other.HoldAt(other.transform.position, Quaternion.LookRotation(-clashDir));
            Visual.SetGrip(clashVictim, false);
            Visual.FreeRightHand(ClashTime + 0.2f);
            Visual.Play(FighterPose.Throw, ClashTime + 0.3f, ClashTime + 0.2f);
            SetState(PlayerState.HeadClash);
        }

        void UpdateHeadClash()
        {
            var v = clashVictim;
            var o = clashOther;
            if (v == null || o == null)
            {
                EndHeadClash();
                return;
            }

            if (!clashed)
            {
                float t = Mathf.Clamp01(stateTimer / ClashTime);
                float e = t * t;   // accelerates into the hit
                v.HoldAt(Vector3.Lerp(clashVictimStart, clashPoint, e), Quaternion.LookRotation(clashDir));
                v.Visual.SetCarry(Quaternion.Euler(25f * e, 0f, 0f));   // head first
                o.Visual.SetCarry(Quaternion.Euler(10f * e, 0f, 0f));
                Motor.Teleport(Vector3.Lerp(clashHeroStart, clashHeroEnd, e));
                if (t < 1f) return;

                clashed = true;
                Vector3 contact = Vector3.Lerp(v.Center, o.Center, 0.5f) + Vector3.up * 0.55f;
                float mult = MassMode ? massModeDamageMultiplier : 1f;
                Visual.SetGrip(null, false);
                v.Visual.ClearCarry();
                o.Visual.ClearCarry();
                v.ReceiveHit(new HitInfo { attacker = this, damage = 22f * mult, direction = clashDir, heavy = true, unblockable = true });
                o.ReceiveHit(new HitInfo { attacker = this, damage = 22f * mult, direction = clashDir, heavy = true, unblockable = true });
                if (!v.IsDead) v.ReleaseFromGrab(-clashDir * 3f + Vector3.up * 2f, knockDown: true);
                if (!o.IsDead) o.ReleaseFromGrab(clashDir * 3f + Vector3.up * 2f, knockDown: true);
                FX.HitSpark(contact, true);
                FX.Burst(contact, 18, 5f, 0.06f, new Color(1f, 0.95f, 0.5f), new Color(3f, 2.6f, 1f), 0.5f, 0f);
                RegisterHits(2, 14f);
                NotifyMoveLanded("Mutta Mothal");
                if (GM != null)
                {
                    GM.HitStop(0.12f);
                    GM.Shake(0.45f, 0.3f);
                    GM.SlowMo(0.3f, 0.35f);
                    GM.AddPopup("MUTTA MOTHAL!", contact + Vector3.up * 0.8f, new Color(1f, 0.75f, 0.15f), 1.4f);
                }
                if (cam != null) cam.ZoomPunch(6f);
                return;
            }
            if (stateTimer > ClashTime + 0.35f) EndHeadClash();
        }

        void EndHeadClash()
        {
            // If one of them got away before the clash (killed by someone else), let go of the other.
            if (!clashed)
            {
                if (clashVictim != null && clashVictim.IsGrabbed) { clashVictim.Visual.ClearCarry(); clashVictim.ReleaseFromGrab(Vector3.zero, knockDown: false); }
                if (clashOther != null && clashOther.IsGrabbed) { clashOther.Visual.ClearCarry(); clashOther.ReleaseFromGrab(Vector3.zero, knockDown: false); }
            }
            clashVictim = clashOther = null;
            Visual.SetGrip(null, false);
            SetState(PlayerState.Free);
        }

        // ------------------------------------------------------------------ helpers for the finishers

        /// <summary>Giant swing: his body knocks down anyone it swings into.</summary>
        void SweepWithBody()
        {
            var st = wrestleMove.steps[wrestleStep];
            if (qteActive || st.yaw == null || Mathf.Approximately(st.yaw.Value, stepYawFrom) || wrestleVictim == null) return;
            float spinSign = Mathf.Sign(st.yaw.Value - stepYawFrom);
            Vector3 toBody = Flat(wrestleVictim.transform.position - transform.position);
            if (toBody.sqrMagnitude < 0.01f) return;
            foreach (var f in All.ToArray())
            {
                if (f == null || f == wrestleVictim || f.team == team || f.IsDead || f.IsDowned || swept.Contains(f)) continue;
                Vector3 toF = Flat(f.transform.position - transform.position);
                if (toF.magnitude > 2.9f || Vector3.Angle(toBody, toF) > 40f) continue;
                swept.Add(f);
                Vector3 tangent = Vector3.Cross(Vector3.up, toBody.normalized) * spinSign;
                Vector3 dir = (tangent + toF.normalized * 0.6f).normalized;
                var r = f.ReceiveHit(new HitInfo { attacker = this, damage = 15f, direction = dir, knockback = 7f, launch = 4f, heavy = true, unblockable = true });
                if (r != HitResult.Hit) continue;
                FX.HitSpark(f.Center, true);
                RegisterHits(1, 4f);
                if (GM != null)
                {
                    GM.HitStop(0.05f);
                    GM.Shake(0.2f);
                    GM.AddPopup("SWEPT!", f.Center + Vector3.up, new Color(1f, 0.8f, 0.2f), 0.8f);
                }
            }
        }

        /// <summary>Kadal Throw: he hits the harbour water.</summary>
        void SeaSplash(EnemyFighter victim)
        {
            Vector3 p = victim.transform.position + Vector3.up * 0.9f;
            p.y = wrestleOrigin.y - 1.25f;
            FX.Burst(p, 40, 6.5f, 0.09f, new Color(0.75f, 0.9f, 1f), new Color(0.6f, 0.8f, 1f), 0.9f, -12f);
            FX.Burst(p, 20, 3f, 0.16f, Color.white, Color.white * 0.6f, 1.2f, -6f);
            wrestleReleased = true;
            Visual.ReleaseGripSlowly(0.3f);
            victim.Visual.ClearCarry();
            victim.ThrownIntoSea(this);
            RegisterHits(1, 0f);
            NotifyMoveLanded(wrestleMove.name);
            if (GM != null)
            {
                GM.Shake(0.35f, 0.25f);
                GM.SlowMo(0.3f, 0.5f);
                GM.AddPopup("SPLASH!", p + Vector3.up * 1.6f, new Color(0.6f, 0.9f, 1f), 1.4f);
            }
            if (cam != null) cam.ZoomPunch(6f);
        }
    }
}

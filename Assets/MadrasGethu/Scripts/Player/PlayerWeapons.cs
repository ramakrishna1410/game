using UnityEngine;

namespace MadrasGethu
{
    /// <summary>
    /// Weapon movesets. Each weapon fights its own way:
    ///   Lathi (Silambam) .. LMB x3 Silambam Strikes · RMB thrust + overhead · hold RMB Silambam Sutru spin · Q Silambam Finish
    ///   Iron Pipe ......... LMB x2 heavy swings · RMB Knee Breaker · hold RMB Golf Swing · Q Pipe Choke
    ///   Plank ............. LMB wide sweep (several rowdies) · RMB head smash (it splinters) · Q Plank Break over his back
    ///   Bottle ............ LMB / RMB smash on the head (dazes) · Q collar + bottle finisher
    ///   Brick ............. LMB brick punch · RMB aimed throw (headshot dazes) · Q Brick Drop on a rowdy on the ground
    ///   Plastic Chair ..... LMB swing · RMB smash (it breaks) · Q Chair Break over his back
    ///   Cycle Chain ....... LMB whip (long reach) · RMB wrap round his neck and pull him into your grab
    ///   Fish .............. LMB / RMB Meen Adi slap (RMB spins him off his feet)
    ///   Tyre .............. LMB / RMB drop it over his shoulders: arms trapped
    /// Plus: F with a lathi / pipe / plank blocks even unblockable swings; E with a weapon grabs one-handed
    /// and LMB / RMB hits him with it while you hold him.
    /// </summary>
    public partial class PlayerFighter
    {
        int weaponChain;
        float lastWeaponAttack = -10f;
        float weaponChargeTime;
        bool weaponChargeWindup;
        bool attackCrouched;
        System.Func<bool> weaponMoveUpdate;   // runs in PlayerState.WeaponMove; returns true when done
        float weaponGrabHitAt = -1f;
        EnemyFighter chainVictim;             // being pulled in by the chain (let go if the pull is cut short)

        /// <summary>SetState hook: a chain pull cut short (you got hit) lets him go instead of leaving him held.</summary>
        void CancelChainPull(PlayerState next)
        {
            if (chainVictim == null || next == PlayerState.Grab) { chainVictim = null; return; }
            if (chainVictim.IsGrabbed && grabbed != chainVictim) chainVictim.ReleaseFromGrab(Vector3.zero, false);
            chainVictim = null;
        }

        AttackData W(string name, FighterPose pose, float damage)
        {
            var w = HeldWeapon;
            return new AttackData(name, pose, damage)
            {
                startup = 0.22f, recovery = 0.38f, range = w != null ? w.reach : 2f, arc = 120f, lunge = 0.8f,
                knockback = 4f, gethuGain = 7f, hitStop = 0.08f, shake = 0.18f,
            };
        }

        /// <summary>LMB / RMB with a weapon in hand. Returns true if something started.</summary>
        bool StartWeaponAttack(bool light, bool heavy)
        {
            var w = HeldWeapon;
            if (w == null) return false;
            bool chain = State == PlayerState.Attack && Time.time - lastWeaponAttack < 0.9f;
            if (!chain || heavy) weaponChain = 0;
            lastWeaponAttack = Time.time;

            switch (w.type)
            {
                case WeaponType.Lathi:
                    if (heavy) return BeginWeaponCharge();
                    {
                        int i = weaponChain % 3;
                        weaponChain++;
                        var a = W(i < 2 ? "Lathi Strike" : "Silambam Strikes", FighterPose.WeaponSwing, i < 2 ? 11f : 16f);
                        a.startup = i < 2 ? 0.16f : 0.2f; a.recovery = 0.28f; a.range = 2.4f; a.reaction = i == 2 ? FighterPose.Uppercut : FighterPose.Hook;
                        if (i == 2) { a.knockback = 6f; a.launch = 2f; a.heavy = true; a.announce = true; a.shake = 0.3f; }
                        StartAttack(a, null);
                    }
                    return true;

                case WeaponType.IronPipe:
                    if (heavy) return BeginWeaponCharge();
                    {
                        int i = weaponChain % 2;
                        weaponChain++;
                        var a = W(i == 0 ? "Pipe Swing" : "Pipe Crusher", FighterPose.WeaponSwing, i == 0 ? 17f : 22f);
                        a.startup = 0.3f; a.recovery = 0.45f; a.reaction = FighterPose.Hook; a.knockback = 5f;
                        if (i == 1) { a.launch = 3f; a.knockback = 7f; a.heavy = true; a.announce = true; a.shake = 0.35f; }
                        StartAttack(a, null);
                    }
                    return true;

                case WeaponType.Plank:
                    if (heavy)
                    {
                        var s = W("Plank Smash", FighterPose.WeaponSmash, 24f);
                        s.startup = 0.32f; s.recovery = 0.55f; s.launch = 3f; s.knockback = 5f; s.heavy = true; s.announce = true; s.breakWeapon = true; s.shake = 0.35f;
                        StartAttack(s, null);
                    }
                    else
                    {
                        var a = W("Plank Sweep", FighterPose.WeaponSwing, 13f);
                        a.arc = 170f; a.range = 2.3f; a.knockback = 5f; a.reaction = FighterPose.Hook;
                        StartAttack(a, null);
                    }
                    return true;

                case WeaponType.Bottle:
                {
                    var a = W("Bottle Smash", FighterPose.WeaponSmash, 20f);
                    a.startup = 0.24f; a.dazes = true; a.heavy = true; a.announce = true; a.breakWeapon = true; a.reaction = FighterPose.Uppercut; a.shake = 0.3f;
                    StartAttack(a, null);
                    return true;
                }

                case WeaponType.Brick:
                    if (heavy) { ThrowWeapon(); return true; }   // aimed throw: a headshot dazes him
                    {
                        var a = W("Brick Punch", FighterPose.Cross, 15f);
                        a.startup = 0.16f; a.range = 1.5f; a.reaction = FighterPose.Cross; a.knockback = 3f;
                        StartAttack(a, null);
                    }
                    return true;

                case WeaponType.Chair:
                    if (heavy)
                    {
                        var s = W("Chair Smash", FighterPose.WeaponSmash, 24f);
                        s.startup = 0.32f; s.recovery = 0.55f; s.launch = 3f; s.knockback = 4f; s.heavy = true; s.announce = true; s.breakWeapon = true;
                        s.reaction = FighterPose.BodyBlow; s.shake = 0.35f;
                        StartAttack(s, null);
                    }
                    else
                    {
                        var a = W("Chair Swing", FighterPose.WeaponSwing, 12f);
                        a.reaction = FighterPose.Hook;
                        StartAttack(a, null);
                    }
                    return true;

                case WeaponType.Chain:
                    if (heavy && TryChainWrap()) return true;
                    {
                        var a = W("Chain Whip", FighterPose.WeaponSwing, 10f);
                        a.range = 3.3f; a.arc = 70f; a.startup = 0.2f; a.recovery = 0.3f; a.lunge = 0.3f; a.reaction = FighterPose.Hook;
                        StartAttack(a, null);
                    }
                    return true;

                case WeaponType.Fish:
                {
                    var a = W("Meen Adi", FighterPose.WeaponSwing, heavy ? 16f : 12f);
                    a.reaction = FighterPose.Hook; a.announce = true;
                    if (heavy) { a.knockback = 9f; a.launch = 2f; a.heavy = true; a.startup = 0.28f; a.shake = 0.3f; }
                    StartAttack(a, null);
                    return true;
                }

                case WeaponType.Tyre:
                    if (TryTyreTrap()) return true;
                    if (GM != null) GM.AddPopup("Get close to drop it over him", Center + Vector3.up, Color.gray, 0.7f);
                    return true;
            }
            StartAttack(w.MakeAttack(heavy), null);
            return true;
        }

        // ------------------------------------------------------------------ hold RMB (lathi spin, pipe golf swing)

        bool BeginWeaponCharge()
        {
            weaponChargeTime = 0f;
            weaponChargeWindup = false;
            SetState(PlayerState.WeaponCharge);
            return true;
        }

        void UpdateWeaponCharge()
        {
            var w = HeldWeapon;
            if (w == null) { SetState(PlayerState.Free); return; }
            weaponChargeTime += Time.deltaTime;
            bool held = GameInput.HeavyHeld;
            Motor.SetDesiredVelocity(Vector3.zero);
            var focus = CombatUtil.FindTarget(this, Vector3.zero, combatLockRange);
            if (focus != null) Motor.FaceDirection(focus.transform.position - transform.position);

            if (w.type == WeaponType.Lathi)
            {
                if (!held && weaponChargeTime < 0.25f)
                {
                    // Tap: thrust to the stomach, then the overhead crack.
                    var thrust = W("Lathi Thrust", FighterPose.Cross, 9f);
                    thrust.startup = 0.15f; thrust.recovery = 0.3f; thrust.range = 2.4f; thrust.reaction = FighterPose.BodyBlow; thrust.knockback = 1.5f;
                    var over = W("Lathi Overhead", FighterPose.WeaponSmash, 20f);
                    over.startup = 0.24f; over.recovery = 0.5f; over.range = 2.4f; over.launch = 3.5f; over.knockback = 5f; over.heavy = true; over.announce = true; over.shake = 0.35f;
                    thrust.next = over;
                    StartAttack(thrust, null);
                }
                else if (held && weaponChargeTime >= 0.3f && MoveUnlocks.Has(MoveId.SilambamSutru))
                {
                    var spin = W("Silambam Sutru", FighterPose.WeaponSwing, 16f);
                    spin.startup = 0.45f; spin.recovery = 0.4f; spin.range = 2.6f; spin.arc = 360f; spin.spin = 360f; spin.lunge = 0f;
                    spin.knockback = 7f; spin.launch = 3f; spin.heavy = true; spin.unblockable = true; spin.announce = true; spin.shake = 0.35f;
                    StartAttack(spin, null);
                }
                else if (!held) SetState(PlayerState.Free);
                return;
            }

            if (w.type == WeaponType.IronPipe)
            {
                if (!held && weaponChargeTime < 0.3f)
                {
                    var knee = W("Knee Breaker", FighterPose.WeaponSwing, 12f);
                    knee.startup = 0.25f; knee.reaction = FighterPose.LowKick; knee.unblockable = true; knee.announce = true; knee.knockback = 0.5f;
                    knee.onHit = e => { e.KneelDown(2.5f); lowKicked = e; lowKickTime = Time.time; };
                    StartAttack(knee, null);
                    return;
                }
                if (held && weaponChargeTime < 1.2f)
                {
                    if (!weaponChargeWindup && weaponChargeTime >= 0.3f)
                    {
                        weaponChargeWindup = true;
                        Visual.Play(FighterPose.Windup, 1.0f);
                    }
                    if (weaponChargeTime >= 0.3f)
                    {
                        Prompts.Add(new PromptItem { key = "RMB", label = weaponChargeTime >= 0.6f ? "Release!" : "Hold..." });
                        if (weaponChargeTime >= 0.6f && Mathf.Repeat(weaponChargeTime, 0.25f) < Time.deltaTime)
                            FX.Burst(Center + transform.right * 0.3f, 4, 2f, 0.05f, new Color(1f, 0.8f, 0.3f), new Color(2f, 1.5f, 0.3f), 0.25f, 0f);
                    }
                    return;
                }
                if (!MoveUnlocks.Has(MoveId.GolfSwing)) { SetState(PlayerState.Free); return; }
                // Release (or held to the max): the golf swing. Much worse if he's already down on one knee.
                var golf = W("Golf Swing", FighterPose.Uppercut, weaponChargeTime >= 0.6f ? 28f : 20f);
                golf.startup = 0.18f; golf.recovery = 0.55f; golf.reaction = FighterPose.Uppercut; golf.launch = 7f; golf.knockback = 6f;
                golf.heavy = true; golf.unblockable = true; golf.announce = true; golf.hitStop = 0.14f; golf.shake = 0.45f;
                var target = focus as EnemyFighter;
                if (target != null && target.IsDazed) golf.damage *= 1.4f;
                if (cam != null) cam.FovKick(5f);
                StartAttack(golf, target);
                return;
            }
            SetState(PlayerState.Free);
        }

        // ------------------------------------------------------------------ chain wrap, tyre trap

        bool TryChainWrap()
        {
            if (!MoveUnlocks.Has(MoveId.ChainWrap)) return false;
            var target = CombatUtil.FindTarget(this, transform.forward, 3.6f) as EnemyFighter;
            if (target == null || target.IsDowned || target.IsGrabbed) return false;
            Motor.FaceDirection(target.transform.position - transform.position, instant: true);
            Visual.Play(FighterPose.WeaponSwing, 0.45f, 0.45f);
            GameAudio.Play(Sfx.Whoosh, Center, 0.7f);
            bool canPull = target.CanBeGrabbed;
            Vector3 from = target.transform.position;
            Vector3 to = transform.position + Flat(target.transform.position - transform.position).normalized * 0.6f;
            float t = 0f;
            bool wrapped = false;
            weaponMoveUpdate = () =>
            {
                t += Time.deltaTime;
                Motor.SetDesiredVelocity(Vector3.zero);
                if (!wrapped && t >= 0.2f)
                {
                    wrapped = true;
                    GameAudio.Play(Sfx.Grab, target.Center + Vector3.up * 0.5f, 1f);
                    if (!canPull)
                    {
                        // Too big to drag in: the chain still bites.
                        target.ReceiveHit(new HitInfo { attacker = this, damage = 10f, direction = transform.forward, knockback = 2f, pose = FighterPose.Hook });
                        return true;
                    }
                    target.EnterGrabbed(false);
                    chainVictim = target;
                    NotifyMoveLanded("Chain Wrap");
                    if (GM != null) GM.AddPopup("GOT YOU!", target.Center + Vector3.up, new Color(1f, 0.8f, 0.3f), 0.9f);
                }
                if (!wrapped) return false;
                float k = Mathf.Clamp01((t - 0.2f) / 0.35f);
                if (target == null || target.IsDead) return true;
                target.HoldAt(Vector3.Lerp(from, to, k * k), Quaternion.LookRotation(Flat(transform.position - from).normalized));
                if (k < 1f) return false;
                BeginGrab(target, false);   // straight into your grab, chain still in hand
                return false;               // BeginGrab has changed the state already
            };
            SetState(PlayerState.WeaponMove);
            return true;
        }

        bool TryTyreTrap()
        {
            if (!MoveUnlocks.Has(MoveId.TyreTrap)) return false;
            var target = CombatUtil.FindTarget(this, transform.forward, 2.1f) as EnemyFighter;
            if (target == null || target.IsDowned || target.IsGrabbed) return false;
            Motor.FaceDirection(target.transform.position - transform.position, instant: true);
            Visual.Play(FighterPose.WeaponSmash, 0.5f, 0.5f);
            float t = 0f;
            weaponMoveUpdate = () =>
            {
                t += Time.deltaTime;
                Motor.SetDesiredVelocity(Vector3.zero);
                if (t < 0.32f) return false;
                var w = HeldWeapon;
                HeldWeapon = null;
                if (w != null && target != null && !target.IsDead)
                {
                    target.TyreTrap(4f, w);
                    GameAudio.Play(Sfx.BodyFall, target.Center + Vector3.up * 0.4f, 0.8f, 1.3f);
                    NotifyMoveLanded("Tyre Trap");
                    RegisterHits(1, 10f);
                    if (GM != null)
                    {
                        GM.AddPopup("TYRE TRAP!", target.Center + Vector3.up * 1.1f, new Color(1f, 0.75f, 0.15f), 1.2f);
                        GM.HitStop(0.06f);
                    }
                }
                else if (w != null) w.Drop();
                return true;
            };
            SetState(PlayerState.WeaponMove);
            return true;
        }

        void UpdateWeaponMove()
        {
            if (weaponMoveUpdate == null || weaponMoveUpdate())
            {
                weaponMoveUpdate = null;
                if (State == PlayerState.WeaponMove) SetState(PlayerState.Free);
            }
        }

        // ------------------------------------------------------------------ weapon finishers (Q)

        /// <summary>Q with a weapon in hand. Returns true if it was used (or refused for lack of Gethu).</summary>
        bool TryWeaponFinisher()
        {
            var w = HeldWeapon;
            if (w == null) return false;

            if (w.type == WeaponType.Brick)
            {
                var down = DownedNearby(lyingOnly: true);
                if (down == null || !MoveUnlocks.Has(MoveId.BrickDrop)) return false;
                if (!PayFinisher()) return true;
                var drop = W("Brick Drop", FighterPose.WeaponSmash, 25f);
                drop.startup = 0.35f; drop.recovery = 0.5f; drop.range = 1.9f; drop.arc = 140f; drop.hitsDowned = true; drop.crouch = true;
                drop.heavy = true; drop.announce = true; drop.breakWeapon = true; drop.knockback = 0.5f; drop.shake = 0.4f;
                StartAttack(drop, down);
                return true;
            }

            var target = CombatUtil.FindTarget(this, MoveInputWorld().sqrMagnitude > 0.01f ? MoveInputWorld() : transform.forward, 2.3f) as EnemyFighter;
            if (target == null || target.IsDowned || target.IsGrabbed) return false;
            if (!target.CanBeGrabbed)
            {
                if (GM != null) GM.AddPopup("Stagger him first", target.Center + Vector3.up, new Color(1f, 0.6f, 0.3f), 0.7f);
                return true;
            }

            WrestleMove move = null;
            switch (w.type)
            {
                case WeaponType.Lathi: if (MoveUnlocks.Has(MoveId.SilambamFinish)) move = SilambamFinish(); break;
                case WeaponType.IronPipe: if (MoveUnlocks.Has(MoveId.PipeChoke)) { grabFromBehind = false; move = ChokeOut("PIPE CHOKE", MoveId.PipeChoke, GripKind.PipeChoke); } break;
                case WeaponType.Plank: if (MoveUnlocks.Has(MoveId.PlankBreak)) move = BreakOverBack("PLANK BREAK", MoveId.PlankBreak); break;
                case WeaponType.Chair: if (MoveUnlocks.Has(MoveId.PlankBreak)) move = BreakOverBack("CHAIR BREAK", MoveId.PlankBreak); break;
                case WeaponType.Bottle:
                    if (!MoveUnlocks.Has(MoveId.BottleFinisher)) return false;
                    if (!PayFinisher()) return true;
                    Motor.FaceDirection(target.transform.position - transform.position, instant: true);
                    target.EnterGrabbed(false);
                    grabbed = target;
                    grabFromBehind = false;
                    StartStrikeCombo(BottleCombo());
                    return true;
            }
            if (move == null) return false;
            if (!PayFinisher()) return true;
            Motor.FaceDirection(target.transform.position - transform.position, instant: true);
            target.EnterGrabbed(false);
            StartWrestle(move, target);
            return true;
        }

        bool PayFinisher()
        {
            if (Gethu < GethuBar)
            {
                if (GM != null) GM.AddPopup("Need 1 Gethu bar", Center + Vector3.up, Color.gray, 0.6f);
                return false;
            }
            Gethu -= GethuBar;
            return true;
        }

        // Silambam Finish: sweep his legs with the stick (he's off his feet), spin it, crack it down as he lands.
        WrestleMove SilambamFinish()
        {
            var sweep = Pose(With(S(QteKey.None, null, 0.3f, FighterPose.WeaponSwing), new Vector3(0f, 1.0f, 1.0f), -90f, 0.3f), -0.35f, 30f);
            sweep.anim = true;
            sweep.shot = CamShot.Side;
            var air = Pose(With(S(QteKey.None, null, 0.16f, FighterPose.Grab), new Vector3(0f, 1.05f, 1.0f), -92f, 0f), -0.05f, -8f);
            air.shot = CamShot.LowHero;
            var crack = Pose(Impact(With(S(QteKey.Heavy, "CRACK", 0.25f, FighterPose.WeaponSmash), new Vector3(0f, 0.3f, 1.0f), -90f, 0f), 30f), -0.4f, 40f);
            crack.anim = true;
            crack.shot = CamShot.GroundImpact;
            var after = Pose(S(QteKey.None, null, 0.35f, FighterPose.Grab), -0.1f, 10f);
            after.shot = CamShot.Side;
            return new WrestleMove { name = "SILAMBAM FINISH", id = MoveId.SilambamFinish, cost = 0f, noHands = true }
                .Step(sweep).Step(air).Step(crack).Step(after);
        }

        // Plank / chair break: bend him over, then smash it across his back. It snaps; he goes down on his face.
        WrestleMove BreakOverBack(string name, MoveId id)
        {
            var bend = Pose(With(S(QteKey.None, null, 0.35f, FighterPose.Grab), new Vector3(0f, 0.95f, 0.85f), 0f, 0f), 0f, -10f);
            bend.victimLean = 60f;
            bend.victimDrop = -0.1f;
            bend.shot = CamShot.Side;
            var smash = Pose(Impact(S(QteKey.Heavy, "SMASH", 0.3f, FighterPose.WeaponSmash), 28f), -0.3f, 35f);
            smash.anim = true;
            smash.victimLean = 85f;
            smash.shot = CamShot.GroundImpact;
            var after = Pose(S(QteKey.None, null, 0.4f, FighterPose.Grab), -0.1f, 10f);
            after.shot = CamShot.Side;
            return new WrestleMove
            {
                name = name, id = id, cost = 0f, noHands = true, breakWeapon = true,
                releasePush = new Vector3(0f, -2.5f, 0.4f),
            }.Step(bend).Step(smash).Step(after);
        }

        // Bottle finisher: collar in the left hand, bottle on his head.
        StrikeCombo BottleCombo()
        {
            var c = new StrikeCombo { name = "BOTTLE FINISHER", id = MoveId.BottleFinisher, cinematic = true };
            var b = c.Add(Beat(0.5f, FighterPose.WeaponSmash, 30f, null));
            b.holdCollar = true; b.lead = 0.4f; b.victimZ = 0.62f; b.breakWeapon = true;
            b.knockout = new Vector3(0f, 0.4f, 0.8f);
            return c;
        }

        // ------------------------------------------------------------------ grab with a weapon in hand

        /// <summary>Holding him one-handed: LMB / RMB hits him with the weapon.</summary>
        void UpdateWeaponGrabStrike()
        {
            var w = HeldWeapon;
            Visual.FreeRightHand(0.2f);   // the weapon hand stays off his collar
            if (weaponGrabHitAt >= 0f && Time.time >= weaponGrabHitAt)
            {
                weaponGrabHitAt = -1f;
                if (grabbed != null && w != null)
                {
                    float mult = MassMode ? massModeDamageMultiplier : 1f;
                    grabbed.ReceiveHit(new HitInfo { attacker = this, damage = w.damage * mult, direction = transform.forward, unblockable = true, pose = FighterPose.Hook });
                    grabbed.Visual.Play(FighterPose.Flinch, 0.3f, 0.35f);
                    FX.HitSpark(grabbed.Center + Vector3.up * 0.4f, true);
                    RegisterHits(1, 6f);
                    NotifyMoveLanded("Weapon Grab Strike");
                    if (GM != null) { GM.HitStop(0.07f); GM.Shake(0.18f); }
                    if (w.RegisterHit()) HeldWeapon = null;
                    if (grabbed != null && grabbed.IsDead) ReleaseGrab(Vector3.zero, false);
                }
            }
            if (weaponGrabHitAt >= 0f || grabActionCooldown > 0f) return;
            if (GameInput.LightPressed || GameInput.HeavyPressed)
            {
                float t = Visual.PlayTimed(FighterPose.WeaponSwing, 0.2f, 0.3f);
                weaponGrabHitAt = Time.time + t;
                grabActionCooldown = t + 0.25f;
                GameAudio.Play(Sfx.Whoosh, Center, 0.5f);
            }
            lightBufferedUntil = heavyBufferedUntil = 0f;
        }

        /// <summary>Block with a stick or pipe: it stops even the unblockable swings (not a charge).</summary>
        bool WeaponBlocks(HitInfo hit)
        {
            var w = HeldWeapon;
            if (w == null || !MoveUnlocks.Has(MoveId.WeaponBlock)) return false;
            if (w.type != WeaponType.Lathi && w.type != WeaponType.IronPipe && w.type != WeaponType.Plank) return false;
            return !(hit.attacker is EnemyFighter e && e.IsCharging);
        }

        /// <summary>Key hints for the weapon in hand.</summary>
        void AddWeaponPrompts()
        {
            var w = HeldWeapon;
            string l = "Swing", r = "Smash", q = null;
            switch (w.type)
            {
                case WeaponType.Lathi: l = "Silambam"; r = "Thrust · hold: Spin"; q = "Silambam Finish"; break;
                case WeaponType.IronPipe: l = "Heavy swing"; r = "Knee breaker · hold: Golf"; q = "Pipe Choke"; break;
                case WeaponType.Plank: l = "Sweep"; r = "Smash"; q = "Plank Break"; break;
                case WeaponType.Bottle: l = "Smash"; r = "Smash"; q = "Bottle Finisher"; break;
                case WeaponType.Brick: l = "Punch"; r = "Throw"; q = "Brick Drop (him down)"; break;
                case WeaponType.Chair: l = "Swing"; r = "Smash"; q = "Chair Break"; break;
                case WeaponType.Chain: l = "Whip"; r = "Wrap + pull"; break;
                case WeaponType.Fish: l = "Meen Adi"; r = "Big slap"; break;
                case WeaponType.Tyre: l = "Tyre trap"; r = "Tyre trap"; break;
            }
            Prompts.Add(new PromptItem { key = "LMB", label = l });
            Prompts.Add(new PromptItem { key = "RMB", label = r });
            if (q != null) Prompts.Add(new PromptItem { key = "Q", label = q, dim = Gethu < GethuBar });
            Prompts.Add(new PromptItem { key = "E", label = "Grab / throw " + w.displayName });
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MadrasGethu
{
    /// <summary>
    /// Chapter 1 — காசிமேடு காலை / Kasimedu Morning (script: docs/script/01_Kasimedu_Kaalai.md,
    /// lines: Resources/Story/ch01.txt). Kutty's auto brings Maaran home at dawn, Ilango at the gate,
    /// a walk through the market, Settu's boys at Amma's kadai (tutorial fight), Amma's kitchen,
    /// Settu's phone call, and night on the jetty.
    /// </summary>
    public class Chapter1 : StoryDirector
    {
        public static readonly Vector3 HeroStart = new Vector3(0.7f, 0.1f, -11.6f);

        protected override string ChapterFile => "ch01";

        // The lane runs along Z: godowns at -X, the harbour water at +X. Amma's kadai is the tea stall.
        static readonly Vector3 Kadai = new Vector3(-6.2f, 0f, 6f);
        static readonly Vector3 SelviMark = new Vector3(-7.05f, 0.1f, 6f);
        static readonly Vector3 PalluMark = new Vector3(-4.95f, 0.1f, 6.1f);
        static readonly Vector3 RowdyAMark = new Vector3(-4.1f, 0.1f, 4.5f);
        static readonly Vector3 RowdyBMark = new Vector3(-3.9f, 0.1f, 7.6f);
        static readonly Vector3 HeroConfront = new Vector3(-2.1f, 0.1f, 3.6f);
        static readonly Vector3 AutoStop = new Vector3(1.8f, 0f, -13f);
        static readonly Vector3 AutoParked = new Vector3(3.0f, 0f, -1.5f);
        static readonly Vector3 PhotoPoint = new Vector3(-7.56f, 1.6f, 6.9f);

        Transform root, auto, jeep;
        StoryActor kutty, selvi, ilango, kayal;
        readonly List<(StoryActor who, string scene)> chats = new List<(StoryActor, string)>();
        EnemyFighter pallu;
        readonly List<EnemyFighter> fighters = new List<EnemyFighter>();
        GameObject tray;
        bool autoDriving, rideCamera;
        bool heavyUsed;

        protected override void Update()
        {
            base.Update();
            if (!inCutscene && GameInput.HeavyPressed) heavyUsed = true;
            if (rideCamera && auto != null)
                cam.CutsceneShot(auto.position + new Vector3(-2.9f, 1.5f, 3.6f), auto.position + Vector3.up * 1.15f, 46f, false, 3f);
        }

        protected override IEnumerator Run()
        {
            ui.Fade = 1f;
            root = new GameObject("Chapter 1").transform;
            KasimeduArenaBuilder.SetTimeOfDay(true);
            StoryProps.DressKasimedu(root, Kadai);
            var arena = GameObject.Find("Kasimedu Harbour");
            StoryProps.OpenGateRoad(root, arena != null ? arena.transform : null);
            SetUpCast();

            BeginCutscene();
            Show(hero, false);   // riding in the back of the auto
            yield return Card("காசிமேடு காலை", "CHAPTER 1  ·  KASIMEDU MORNING", "Kasimedu fishing harbour, North Chennai  ·  6:10 AM", 3.5f);

            // ---- 1.1 the auto ride
            Music("gaana");
            StartCoroutine(Drive(auto, AutoStop, 2.6f));
            Shot(new Vector3(6.5f, 4.2f, -64f), auto.position + Vector3.up, 46f);
            yield return FadeTo(0f, 1.4f);
            yield return Wait(2.6f);
            rideCamera = true;
            yield return Scene("1.1_auto");
            rideCamera = false;
            while (autoDriving) yield return null;   // a skip drops the auto at the gate
            Music(null);

            // ---- the gate: Inspector Ilango
            yield return Scene("1.1_gate");
            StartCoroutine(ParkAuto());
            EndCutscene();

            // ---- 1.2 walk to Amma's kadai
            yield return WalkToKadai();

            // ---- 1.3 the shakedown
            BeginCutscene();
            yield return Scene("1.3_shakedown");
            EndCutscene();

            // ---- 1.4 the fight
            yield return Fight();

            // ---- after: Pallu runs (if he's still in one piece)
            BeginCutscene();
            if (pallu != null && !pallu.IsDead && pallu.IsBegging)
            {
                pallu.cutsceneControlled = true;
                Vector3 front = pallu.transform.position + Flat(hero.transform.position - pallu.transform.position).normalized * 1.5f;
                yield return Walk(hero, front, 1.4f);
                hero.Face(pallu.transform);
                yield return Scene("1.4_end");
            }
            yield return FadeTo(1f, 1.2f);

            // ---- 1.5 Amma's kitchen
            CleanUpFight();
            yield return Kitchen();

            // ---- 1.6 Settu's call
            yield return FadeTo(1f, 1f);
            yield return Call();

            // ---- 1.7 the jetty at night
            yield return FadeTo(1f, 1.4f);
            yield return Jetty();

            yield return FadeTo(1f, 2f);
            EndCutscene();
            ui.Fade = 1f;
            yield return Card("அத்தியாயம் 1 முடிந்தது", "END OF CHAPTER 1", "Next: Chapter 2 — Vaathiyaar (The Coach)", 6f);
            ui.Banner = "MADRAS GETHU";
            ui.BannerSmall = "Thanks for playing Chapter 1   ·   Enter: play it again";
            while (!GameInput.RestartPressed) yield return null;
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        // ------------------------------------------------------------------ cast and props

        void SetUpCast()
        {
            auto = StoryProps.Auto(root, new Vector3(1.8f, 0f, -82f), 0f);
            jeep = StoryProps.Jeep(root, new Vector3(4.6f, 0f, -9.3f), 180f);
            ilango = Npc("ilango", new Vector3(3.2f, 0.1f, -10.4f), AutoStop);
            kutty = Npc("kutty", AutoStop + new Vector3(0f, 0.1f, 0f), AutoStop + Vector3.forward);
            Show(kutty, false);
            if (kutty.Motor != null) kutty.Motor.SetFrozen(true);
            hero.Teleport(HeroStart);

            // People on the walk (E to talk)
            chats.Add((Npc("muniyandi", new Vector3(5.4f, 0.1f, -15.5f), new Vector3(0f, 0f, -15.5f)), "1.2_muniyandi"));
            chats.Add((Npc("paati", new Vector3(4.6f, 0.1f, -6.3f), new Vector3(0f, 0f, -6f)), "1.2_paati"));
            chats.Add((Npc("boy", new Vector3(-1.4f, 0.1f, -5.2f), new Vector3(1f, 0f, -8f)), "1.2_boy"));

            // Amma's kadai and Settu's boys, already there when you arrive
            selvi = Npc("selvi", SelviMark, PalluMark);
            pallu = SpawnRowdy("pallu", PalluMark, SelviMark, true);
            SpawnRowdy(null, RowdyAMark, Kadai, true);
            SpawnRowdy(null, RowdyBMark, Kadai, true);

            // A stack of steel idli plates on the counter (Pallu sweeps them off)
            tray = Palette.Primitive(PrimitiveType.Cylinder, root, Kadai + new Vector3(0.45f, 1.14f, -0.35f), new Vector3(0.45f, 0.03f, 0.45f),
                new Color(0.78f, 0.8f, 0.82f), name: "Idli Plates");
        }

        EnemyFighter SpawnRowdy(string castId, Vector3 at, Vector3 lookAt, bool waiting)
        {
            var e = castId != null ? CharacterFactory.CreateNamedEnemy(EnemyKind.Rowdy, castId, at) : CharacterFactory.CreateEnemy(EnemyKind.Rowdy, at);
            e.cutsceneControlled = waiting;
            var actor = e.GetComponent<StoryActor>();
            if (actor == null) { actor = e.gameObject.AddComponent<StoryActor>(); actor.castId = "rowdy"; }
            actor.FaceNow(lookAt);
            if (castId == "pallu") { e.keepAlive = true; pallu = e; }
            fighters.Add(e);
            return e;
        }

        IEnumerator Drive(Transform t, Vector3 to, float speed)
        {
            autoDriving = true;
            while (t != null && Vector3.Distance(t.position, to) > 0.05f)
            {
                if (skipping) { t.position = to; t.rotation = Quaternion.LookRotation(Vector3.forward); break; }
                Vector3 d = to - t.position;
                float step = Mathf.Min(speed, 0.4f + d.magnitude * 0.9f) * Time.deltaTime;
                t.position = Vector3.MoveTowards(t.position, to, step);
                if (Flat(d).sqrMagnitude > 0.01f)
                    t.rotation = Quaternion.RotateTowards(t.rotation, Quaternion.LookRotation(Flat(d)), 90f * Time.deltaTime);
                yield return null;
            }
            autoDriving = false;
        }

        IEnumerator ParkAuto()
        {
            yield return Walk(kutty, AutoStop + new Vector3(-0.7f, 0.1f, 0.6f), 1.4f);
            Show(kutty, false);
            if (kutty.Motor != null) kutty.Motor.SetFrozen(true);
            yield return Drive(auto, new Vector3(2.4f, 0f, -8f), 1.6f);
            yield return Drive(auto, AutoParked, 1.2f);
        }

        // ------------------------------------------------------------------ markers

        protected override IEnumerator Marker(string name, string arg)
        {
            switch (name)
            {
                case "billboard":
                    rideCamera = false;
                    // Low, looking up at Kumaran's banner on the arch as the auto comes under it.
                    Shot(new Vector3(1.2f, 1.1f, -27.5f), new Vector3(-0.15f, 6.2f, -22f), 56f);
                    yield return Wait(2.2f);
                    StartCoroutine(BackToRideCamera(4.5f));
                    break;

                case "step_out":
                    rideCamera = false;
                    hero.Place(HeroStart, ilango.transform.position);
                    Show(hero, true);
                    if (kutty.Motor != null) kutty.Motor.SetFrozen(false);
                    kutty.Place(AutoStop + new Vector3(-0.9f, 0.1f, -0.9f), hero.transform.position);
                    Show(kutty, true);
                    Shot(new Vector3(-1.6f, 1.55f, -14.6f), new Vector3(1.6f, 1.3f, -10.8f), 42f);
                    yield return Walk(ilango, new Vector3(1.9f, 0.1f, -9.9f), 1.2f);
                    ilango.Face(hero);
                    AutoShots();
                    break;

                case "jeep_leaves":
                    yield return Walk(ilango, new Vector3(3.5f, 0.1f, -8.4f), 1.6f);
                    Show(ilango, false);
                    StartCoroutine(JeepAway());
                    hero.Face(kutty);
                    kutty.Face(hero);
                    yield return Wait(0.6f);
                    break;

                case "tray":
                    pallu.GetComponent<StoryActor>().Gesture(FighterPose.Push, 1f);
                    yield return Wait(0.35f);
                    // A flat box collider so the plates slide and clatter instead of rolling like a ball.
                    Destroy(tray.GetComponent<Collider>());
                    tray.AddComponent<BoxCollider>();
                    var rb = tray.AddComponent<Rigidbody>();
                    rb.AddForce(new Vector3(2.2f, 1.2f, -0.6f), ForceMode.VelocityChange);
                    rb.AddTorque(new Vector3(4f, 0f, 6f), ForceMode.VelocityChange);
                    GameAudio.Play(Sfx.Glass, tray.transform.position, 0.8f, 1.6f);
                    if (selvi.Visual != null) selvi.Visual.Flinch();
                    yield return Wait(0.9f);
                    Shot(PalluMark + new Vector3(0.4f, 1.6f, -1.3f), HeroConfront + Vector3.up * 1.5f, 38f);
                    hero.Teleport(new Vector3(-1.2f, 0.1f, -1.5f));
                    yield return Walk(hero, HeroConfront, 1.6f);
                    AutoShots();
                    break;

                case "turn":
                    foreach (var f in fighters)
                        if (f != null) f.GetComponent<StoryActor>().Face(hero);
                    yield return Wait(0.5f);
                    break;

                case "pallu_runs":
                    pallu.cutsceneControlled = false;
                    pallu.LetGo();
                    yield return Wait(1.6f);
                    break;

                case "kayal_enters":
                    Show(kayal, true);
                    yield return Walk(kayal, new Vector3(-4.0f, 0.1f, 7.3f), 1.4f);
                    kayal.Face(hero);
                    hero.Face(kayal);
                    break;

                case "photo":
                    Shot(new Vector3(-5.3f, 1.65f, 7.7f), PhotoPoint, 30f);
                    yield return Wait(2f);
                    Shot(new Vector3(-5.1f, 1.6f, 4.7f), SelviMark + Vector3.up * 1.45f, 32f, false, 0.8f);
                    break;

                case "phone_rings":
                    Ring(hero.transform.position);
                    yield return Wait(1.8f);
                    hero.Hold("OnPhone", true);
                    Shot(hero.Head + hero.transform.forward * 1.3f + hero.transform.right * 0.35f, hero.Head - Vector3.up * 0.05f, 34f);
                    yield return Wait(0.6f);
                    break;

                case "hang_up":
                    yield return Wait(0.8f);
                    hero.Hold("OnPhone", false);
                    Shot(hero.Head + hero.transform.forward * 2.2f + Vector3.up * 0.3f, hero.Head, 30f, false, 0.6f);
                    yield return Wait(2f);
                    break;

                case "kutty_walks":
                    yield return Walk(kutty, new Vector3(6.1f, 0.1f, 5.4f), 1.1f);
                    kutty.Face(hero);
                    AutoShots();
                    break;

                case "neon":
                    hero.Face(new Vector3(34f, 0f, 10f));
                    Shot(hero.Head + new Vector3(-1.3f, 0.2f, -1.0f), new Vector3(34f, 5.5f, 10f), 30f);
                    yield return Wait(2.8f);
                    break;

                case "stand":
                    Shot(new Vector3(11f, 1.2f, 2.5f), hero.Head - Vector3.up * 0.2f, 38f, false, 0.4f);
                    yield return Wait(3f);
                    break;
            }
        }

        IEnumerator BackToRideCamera(float after)
        {
            yield return Wait(after);
            if (autoDriving && inCutscene) rideCamera = true;
        }

        IEnumerator JeepAway()
        {
            var to = new Vector3(4.6f, 0f, -48f);
            while (jeep != null && Vector3.Distance(jeep.position, to) > 0.1f)
            {
                jeep.position = Vector3.MoveTowards(jeep.position, to, 6f * Time.deltaTime);
                yield return null;
            }
            if (jeep != null) Destroy(jeep.gameObject);
            if (ilango != null) Destroy(ilango.gameObject);
        }

        // ------------------------------------------------------------------ 1.2 the walk

        IEnumerator WalkToKadai()
        {
            Objective("அம்மா கடைக்கு போ", "Go to Amma's idli kadai");
            var done = new HashSet<string>();
            while (Flat(player.transform.position - Kadai).magnitude > 8.5f)
            {
                (StoryActor who, string scene) near = (null, null);
                float best = 2.4f;
                foreach (var c in chats)
                {
                    if (c.who == null || done.Contains(c.scene)) continue;
                    float d = Flat(c.who.transform.position - player.transform.position).magnitude;
                    if (d < best) { best = d; near = c; }
                }
                if (near.who != null)
                {
                    ui.TalkHint = "பேசு";
                    ui.TalkWorldPosition = near.who.Head + Vector3.up * 0.45f;
                    if (GameInput.GrabPressed)
                    {
                        done.Add(near.scene);
                        BeginCutscene(bars: false);
                        hero.Face(near.who);
                        near.who.Face(hero);
                        yield return Scene(near.scene);
                        EndCutscene();
                    }
                }
                else ui.TalkHint = null;
                yield return null;
            }
            ui.TalkHint = null;
            Objective(null, null);
        }

        // ------------------------------------------------------------------ 1.4 the fight

        struct Step
        {
            public string key, tamil, english;
            public System.Func<bool> done;
        }

        IEnumerator Fight()
        {
            GameAudio.StartMusic();
            Objective("செட்டு ஆளுங்கள அடி", "Beat Settu's boys");
            var steps = new List<Step>
            {
                new Step { key = "LMB", tamil = "அடி — இடது கிளிக்", english = "Strike — Left click (keep clicking for a combo)", done = () => player.State == PlayerFighter.PlayerState.Attack },
                new Step { key = "RMB", tamil = "கனமான அடி — வலது கிளிக்", english = "Heavy hit — Right click (breaks his guard)", done = () => heavyUsed },
                new Step { key = "F", tamil = "தடு — அழுத்திப் பிடி", english = "Block — hold F", done = () => player.State == PlayerFighter.PlayerState.Block },
                new Step { key = "F", tamil = "சிவப்பா மின்னுனா — தட்டு", english = "When a rowdy flashes RED — tap F to counter", done = () => player.State == PlayerFighter.PlayerState.Parry },
                new Step { key = "E", tamil = "பிடி", english = "Grab — E (then click to hit, E to throw)", done = () => player.Grabbed != null },
                new Step { key = "Q", tamil = "கெத்து — பிடிச்சுட்டு அழுத்து", english = "Gethu finisher — grab him, then Q", done = () => player.State == PlayerFighter.PlayerState.Wrestle || player.State == PlayerFighter.PlayerState.Finisher || player.MassMode },
            };
            int step = 0;
            float stepDoneAt = -1f;
            heavyUsed = false;

            ReleaseFighters();
            bool secondWave = false, kuttyCame = false, kadaiBark = false, boxerBark = false, flankBark = false;
            float secondWaveAt = 0f;
            Bark("1.4_barks", 0);
            FinisherSpot kadaiSpot = null;
            foreach (var s in FinisherSpot.All)
                if (s != null && Vector3.Distance(s.transform.position, Kadai) < 1f) kadaiSpot = s;

            while (true)
            {
                // Tutorial, one prompt at a time
                if (step < steps.Count)
                {
                    var st = steps[step];
                    if (stepDoneAt < 0f)
                    {
                        Tutorial(st.key, st.tamil, st.english);
                        if (st.done()) { stepDoneAt = Time.time; Tutorial("OK", "சூப்பர்!", "Super!"); }
                    }
                    else if (Time.time - stepDoneAt > 1.1f) { step++; stepDoneAt = -1f; }
                }
                else ClearTutorial();

                int dead = 0;
                foreach (var f in fighters) if (f == null || f.IsDead) dead++;

                if (!boxerBark && dead >= 1) { boxerBark = true; Bark("1.4_barks", 1); }
                if (!secondWave && dead >= 2)
                {
                    secondWave = true;
                    secondWaveAt = Time.time;
                    SpawnRowdy(null, new Vector3(0.5f, 0.1f, 21f), Kadai, false);
                    SpawnRowdy(null, new Vector3(-2.5f, 0.1f, 23f), Kadai, false);
                    foreach (var f in fighters) if (f != null && !gm.AliveEnemies.Contains(f) && !f.IsDead) gm.AliveEnemies.Add(f);
                    Bark("1.4_barks", 2);
                }
                if (secondWave && !kuttyCame && Time.time > secondWaveAt + 2.5f)
                {
                    kuttyCame = true;
                    StartCoroutine(KuttyArrives());
                }
                if (!kadaiBark && kadaiSpot != null && kadaiSpot.IsBroken) { kadaiBark = true; Bark("1.4_kadai_smashed", 0); }
                if (!flankBark && EnemyFighter.Flanker != null) { flankBark = true; Bark("1.4_barks", 3); }

                // Pallu: last one standing and hurt, he goes down on his knees.
                bool othersDown = true;
                foreach (var f in fighters) if (f != null && f != pallu && !f.IsDead) othersDown = false;
                if (secondWave && othersDown)
                {
                    if (pallu == null || pallu.IsDead) break;
                    if (pallu.IsBegging) break;
                    if (pallu.HealthFraction <= 0.5f && !pallu.IsDowned && !pallu.IsGrabbed && !pallu.Visual.IsRagdoll)
                        pallu.Surrender(true);
                }

                if (ConsumePlayerDied())
                {
                    ClearTutorial();
                    yield return WaitForRetry();
                    ResetFight();
                    secondWave = false;
                    boxerBark = false;
                    stepDoneAt = -1f;
                }
                yield return null;
            }

            ClearTutorial();
            Objective(null, null);
            GameAudio.StopMusic();
            yield return new WaitForSeconds(0.8f);
        }

        void ReleaseFighters()
        {
            foreach (var f in fighters)
            {
                if (f == null) continue;
                f.cutsceneControlled = false;
                if (!gm.AliveEnemies.Contains(f)) gm.AliveEnemies.Add(f);
            }
        }

        void ResetFight()
        {
            foreach (var f in Fighter.All.ToArray())
                if (f is EnemyFighter) Destroy(f.gameObject);
            fighters.Clear();
            gm.AliveEnemies.Clear();
            EnemyDirector.Reset();
            foreach (var spot in FinisherSpot.All) spot.Repair();
            player.Respawn(HeroConfront);
            pallu = SpawnRowdy("pallu", PalluMark, HeroConfront, false);
            SpawnRowdy(null, RowdyAMark, HeroConfront, false);
            SpawnRowdy(null, RowdyBMark, HeroConfront, false);
            ReleaseFighters();
        }

        IEnumerator KuttyArrives()
        {
            if (kutty.Motor != null) kutty.Motor.SetFrozen(false);
            kutty.Place(AutoParked + new Vector3(-1.1f, 0.1f, 0.4f), player.transform.position);
            Show(kutty, true);
            kutty.WalkTo(new Vector3(1.6f, 0.1f, 1.6f), 2.2f);
            yield return new WaitForSeconds(1.2f);
            Bark("1.4_kutty_arrives", 0);
            yield return new WaitForSeconds(1.5f);
            kutty.Face(player.transform);
        }

        void CleanUpFight()
        {
            foreach (var f in Fighter.All.ToArray())
                if (f is EnemyFighter) Destroy(f.gameObject);
            fighters.Clear();
            gm.AliveEnemies.Clear();
            EnemyDirector.Reset();
            foreach (var spot in FinisherSpot.All) spot.Repair();
            foreach (var w in Weapon.All.ToArray())
                if (w != null && w.Holder == null) Destroy(w.gameObject);
            KasimeduArenaBuilder.PlaceWeapons();
            if (tray != null) Destroy(tray);
            Show(kutty, false);
            if (kutty.Motor != null) kutty.Motor.SetFrozen(true);
            player.Heal(player.maxHealth);
        }

        // ------------------------------------------------------------------ 1.5 Amma's kitchen

        IEnumerator Kitchen()
        {
            player.EnterCutscene();
            selvi.Place(SelviMark, Kadai + Vector3.right * 2f);
            kayal = Npc("kayal", new Vector3(-2.6f, 0.1f, 12.5f), Kadai);
            Show(kayal, false);

            // On the bench if there's a sitting animation, standing at the counter if not.
            bool sit = player.Visual.HasAnimatorParameter("Sitting");
            if (sit)
            {
                player.Motor.SetFrozen(true);
                player.transform.position = new Vector3(-4.68f, 0f, 5.7f);
                hero.FaceNow(new Vector3(-8f, 0f, 5.7f));
                hero.Hold("Sitting", true);
            }
            else hero.Place(new Vector3(-4.85f, 0.1f, 5.6f), SelviMark);
            selvi.Face(hero);

            Shot(new Vector3(-2.9f, 1.45f, 7.9f), new Vector3(-6.2f, 1.15f, 5.8f), 40f);
            yield return FadeTo(0f, 1.2f);
            yield return Wait(1f);
            yield return Scene("1.5_kitchen");
            yield return Wait(1f);

            hero.Hold("Sitting", false);
            player.Motor.SetFrozen(false);
            if (kayal != null) Show(kayal, false);
        }

        // ------------------------------------------------------------------ 1.6 Settu's call

        IEnumerator Call()
        {
            AutoShots();
            hero.Place(new Vector3(-2.8f, 0.1f, 3.6f), new Vector3(4f, 0f, -2f));
            selvi.Place(SelviMark, Kadai + Vector3.right * 2f);
            Shot(new Vector3(-0.4f, 1.6f, 1.2f), hero.Head - Vector3.up * 0.1f, 38f);
            yield return FadeTo(0f, 1f);
            yield return Wait(0.8f);
            yield return Scene("1.6_call");
        }

        // ------------------------------------------------------------------ 1.7 the jetty

        IEnumerator Jetty()
        {
            KasimeduArenaBuilder.SetTimeOfDay(false);
            StoryProps.NeonSign(root);
            Show(selvi, false);
            hero.Place(new Vector3(6.25f, 0.1f, 4.5f), new Vector3(20f, 0f, 6f));
            if (kutty.Motor != null) kutty.Motor.SetFrozen(false);
            kutty.Place(new Vector3(1.2f, 0.1f, 8.5f), hero.transform.position);
            Show(kutty, true);
            Shot(new Vector3(10.5f, 1.7f, 0.6f), hero.Head - Vector3.up * 0.25f, 44f);
            yield return FadeTo(0f, 1.6f);
            yield return Wait(1.5f);
            yield return Scene("1.7_jetty");
        }
    }
}

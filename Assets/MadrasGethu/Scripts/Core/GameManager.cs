using System.Collections.Generic;
using UnityEngine;

namespace MadrasGethu
{
    /// <summary>
    /// Runs the fight: enemy waves, win/lose, restart, and global "game feel" effects
    /// (hit-stop, slow motion, screen shake, floating text).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public enum State { Intro, Fighting, BetweenWaves, Victory, GameOver, Practice }

        [System.Serializable]
        public class Wave
        {
            public string title;
            public string subtitle;
            public List<EnemyKind> enemies = new List<EnemyKind>();
        }

        public struct Popup
        {
            public string text;
            public Vector3 worldPosition;
            public Color color;
            public float startTime;
            public float size;
        }

        public static GameManager Instance { get; private set; }

        public List<Wave> waves = new List<Wave>();
        public List<Vector3> spawnPoints = new List<Vector3>();
        public Vector3 playerStart;

        [Header("Practice yard")]
        public bool practiceMode;
        public Vector3 dummySpawn;
        public EnemyKind dummyKind = EnemyKind.Rowdy;

        public bool PracticeMode => practiceMode;
        public EnemyFighter Dummy { get; private set; }
        /// <summary>Second dummy (Y): for moves that need two rowdies (head clash, bowling throw, giant swing).</summary>
        public EnemyFighter PartnerDummy { get; private set; }
        float dummyRespawnAt = -1f, partnerRespawnAt = -1f;
        /// <summary>K: the dummies fight back (so you can practise counters).</summary>
        public bool PracticeSparring { get; private set; }
        /// <summary>Moves landed in the practice yard (upper case, no "!").</summary>
        public readonly HashSet<string> PracticeLanded = new HashSet<string>();
        public string PracticeLastMove { get; private set; }
        public float PracticeLastHit { get; private set; }
        public float PracticeStringDamage { get; private set; }
        float lastDummyHitTime;
        readonly Dictionary<FinisherSpot, float> brokenSince = new Dictionary<FinisherSpot, float>();

        public State CurrentState { get; private set; }
        public int WaveIndex { get; private set; }
        public PlayerFighter Player { get; set; }
        public ThirdPersonCamera Cam { get; set; }
        public readonly List<EnemyFighter> AliveEnemies = new List<EnemyFighter>();
        public readonly List<Popup> Popups = new List<Popup>();

        public string BannerTitle { get; private set; }
        public string BannerSubtitle { get; private set; }
        public float BannerUntil { get; private set; }

        float stateTimer;
        readonly Queue<EnemyKind> pendingSpawns = new Queue<EnemyKind>();
        float spawnTimer;
        int spawnCursor;

        float hitStopUntil;
        float slowMoUntil;
        float slowMoScale = 1f;

        void Awake()
        {
            Instance = this;
            EnemyDirector.Reset();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        void Start()
        {
            if (practiceMode) BeginPractice();
            else BeginIntro();
        }

        // ------------------------------------------------------------------ practice yard

        void BeginPractice()
        {
            CurrentState = State.Practice;
            WaveIndex = -1;
            ShowBanner("PRACTICE YARD", "One dummy. He won't move, won't hit back, and won't go down for good.", 3f);
            if (Player != null)
            {
                Player.infiniteGethu = true;
                Player.invincible = true;
                Player.MoveLanded += OnPracticeMoveLanded;
            }
            SpawnDummy();
        }

        void SpawnDummy()
        {
            if (Dummy != null)
            {
                AliveEnemies.Remove(Dummy);
                Destroy(Dummy.gameObject);
            }
            EnemyDirector.Reset();
            Dummy = CharacterFactory.CreateEnemy(dummyKind, dummySpawn);
            Dummy.isDummy = true;
            Dummy.displayName = "Dummy (" + Dummy.Profile.displayName + ")";
            Dummy.gameObject.name = Dummy.displayName;
            Dummy.Damaged += OnDummyDamaged;
            Dummy.sparring = PracticeSparring;
            AliveEnemies.Add(Dummy);
        }

        static readonly Vector3 PartnerOffset = new Vector3(2.4f, 0f, 0.6f);

        void SpawnPartner()
        {
            RemovePartner();
            PartnerDummy = CharacterFactory.CreateEnemy(EnemyKind.Rowdy, dummySpawn + PartnerOffset);
            PartnerDummy.isDummy = true;
            PartnerDummy.displayName = "Dummy 2 (" + PartnerDummy.Profile.displayName + ")";
            PartnerDummy.gameObject.name = PartnerDummy.displayName;
            PartnerDummy.Damaged += OnDummyDamaged;
            PartnerDummy.sparring = PracticeSparring;
            AliveEnemies.Add(PartnerDummy);
        }

        void RemovePartner()
        {
            if (PartnerDummy == null) return;
            AliveEnemies.Remove(PartnerDummy);
            Destroy(PartnerDummy.gameObject);
            PartnerDummy = null;
        }

        /// <summary>Puts a practice dummy back on his mark after a moment (e.g. thrown into the sea).</summary>
        public void RespawnDummySoon(EnemyFighter dummy, float delay)
        {
            if (dummy == Dummy) dummyRespawnAt = Time.time + delay;
            else if (dummy == PartnerDummy) partnerRespawnAt = Time.time + delay;
        }

        void ResetPractice()
        {
            SpawnDummy();
            if (PartnerDummy != null) SpawnPartner();
            foreach (var spot in FinisherSpot.All) spot.Repair();
            brokenSince.Clear();
            foreach (var w in Weapon.All.ToArray())
                if (w != null && w.Holder == null) Destroy(w.gameObject);
            PracticeYardBuilder.PlaceWeapons();
        }

        /// <summary>Not while the hero has hold of the dummy (grab, finisher), or the hero would hold a deleted body.</summary>
        bool CanReplaceDummy =>
            Player == null || (Player.Grabbed == null && Player.State != PlayerFighter.PlayerState.Wrestle && Player.State != PlayerFighter.PlayerState.Finisher
                               && Player.State != PlayerFighter.PlayerState.Combo && Player.State != PlayerFighter.PlayerState.Mount
                               && Player.State != PlayerFighter.PlayerState.HeadClash && Player.State != PlayerFighter.PlayerState.WeaponMove
                               && Player.State != PlayerFighter.PlayerState.WeaponCharge);

        /// <summary>Knocked out of the yard, into the sea, or far off: true when he should go back on his mark.</summary>
        bool Lost(EnemyFighter d, Vector3 home, ref float respawnAt)
        {
            if (respawnAt > 0f && Time.time >= respawnAt) { respawnAt = -1f; return true; }
            if (d.transform.position.y < -3f) return true;
            return !d.IsGrabbed && !d.IsDowned &&
                   (d.transform.position.y < -2f || Vector3.ProjectOnPlane(d.transform.position - home, Vector3.up).magnitude > 14f);
        }

        void UpdatePractice()
        {
            if (GameInput.PracticeResetPressed && CanReplaceDummy) ResetPractice();
            if (GameInput.PracticeSwitchPressed && CanReplaceDummy)
            {
                dummyKind = dummyKind == EnemyKind.Rowdy ? EnemyKind.Thadiyan : dummyKind == EnemyKind.Thadiyan ? EnemyKind.Boss : EnemyKind.Rowdy;
                SpawnDummy();
                AddPopup(Dummy.displayName.ToUpper(), Dummy.Center + Vector3.up, new Color(1f, 0.85f, 0.4f), 1f);
            }
            if (GameInput.PracticeGethuPressed && Player != null)
            {
                Player.infiniteGethu = !Player.infiniteGethu;
                AddPopup(Player.infiniteGethu ? "INFINITE GETHU: ON" : "INFINITE GETHU: OFF", Player.Center + Vector3.up, new Color(1f, 0.8f, 0.2f), 0.9f);
            }

            if (GameInput.PracticeSparPressed)
            {
                PracticeSparring = !PracticeSparring;
                if (Dummy != null) Dummy.sparring = PracticeSparring;
                if (PartnerDummy != null) PartnerDummy.sparring = PracticeSparring;
                if (Player != null)
                    AddPopup(PracticeSparring ? "SPARRING: THEY FIGHT BACK" : "SPARRING OFF", Player.Center + Vector3.up, new Color(1f, 0.8f, 0.2f), 1f);
            }
            if (GameInput.PracticeWeaponPressed && Dummy != null && !Dummy.IsGrabbed)
            {
                // Hand him a lathi (and the second dummy a pipe) to practise the weapon steal.
                Dummy.EquipWeapon(Weapon.Create(WeaponType.Lathi, Dummy.transform.position));
                if (PartnerDummy != null && !PartnerDummy.IsGrabbed) PartnerDummy.EquipWeapon(Weapon.Create(WeaponType.IronPipe, PartnerDummy.transform.position));
                AddPopup("ARMED", Dummy.Center + Vector3.up, new Color(1f, 0.6f, 0.3f), 0.8f);
            }
            if (GameInput.PracticePartnerPressed && CanReplaceDummy)
            {
                if (PartnerDummy == null)
                {
                    SpawnPartner();
                    AddPopup("SECOND DUMMY", PartnerDummy.Center + Vector3.up, new Color(1f, 0.85f, 0.4f), 1f);
                }
                else RemovePartner();
            }

            if (Dummy == null) SpawnDummy();
            // Knocked out of the yard (or far off): put him back on his mark.
            if (CanReplaceDummy && Lost(Dummy, dummySpawn, ref dummyRespawnAt)) SpawnDummy();
            if (PartnerDummy != null && CanReplaceDummy && Lost(PartnerDummy, dummySpawn + PartnerOffset, ref partnerRespawnAt)) SpawnPartner();

            if (Time.time - lastDummyHitTime > 2.5f) PracticeStringDamage = 0f;

            // Smashed props come back after a few seconds so you can practise again.
            foreach (var spot in FinisherSpot.All)
            {
                if (spot == null || !spot.IsBroken) continue;
                if (!brokenSince.TryGetValue(spot, out float t)) brokenSince[spot] = Time.time;
                else if (Time.time - t > 5f) { spot.Repair(); brokenSince.Remove(spot); }
            }
        }

        void OnDummyDamaged(Fighter f, HitInfo hit, HitResult result)
        {
            if (Time.time - lastDummyHitTime > 2.5f) PracticeStringDamage = 0f;
            lastDummyHitTime = Time.time;
            PracticeLastHit = hit.damage;
            PracticeStringDamage += hit.damage;
        }

        void OnPracticeMoveLanded(string moveName)
        {
            string key = moveName.Trim().TrimEnd('!').Trim().ToUpperInvariant();
            PracticeLastMove = key;
            if (PracticeLanded.Add(key))
                AddPopup("NEW: " + key, (Dummy != null ? Dummy.Center : Player.Center) + Vector3.up * 1.6f, new Color(0.5f, 1f, 0.6f), 1f);
        }

        // ------------------------------------------------------------------ game flow

        void BeginIntro()
        {
            CurrentState = State.Intro;
            WaveIndex = -1;
            stateTimer = 3f;
            ShowBanner("MADRAS GETHU", "Kasimedu Fishing Harbour  ·  11:40 PM", 3f);
        }

        void StartNextWave()
        {
            WaveIndex++;
            if (WaveIndex >= waves.Count)
            {
                CurrentState = State.Victory;
                ShowBanner("KASIMEDU IS YOURS", "Press Enter / Start to fight again", 9999f);
                SlowMo(0.3f, 1.5f);
                return;
            }

            var wave = waves[WaveIndex];
            CurrentState = State.Fighting;
            surrenderOffered = false;
            lastAliveCount = 0;
            ShowBanner(wave.title, wave.subtitle, 2.5f);
            pendingSpawns.Clear();
            foreach (var kind in wave.enemies) pendingSpawns.Enqueue(kind);
            spawnTimer = 0.5f;
        }

        void Update()
        {
            UpdateTimeScale();
            CleanPopups();

            switch (CurrentState)
            {
                case State.Intro:
                    stateTimer -= Time.unscaledDeltaTime;
                    if (stateTimer <= 0f) StartNextWave();
                    break;

                case State.Fighting:
                    SpawnPending();
                    AliveEnemies.RemoveAll(e => e == null || e.IsDead);
                    CheckLastManStanding();
                    if (pendingSpawns.Count == 0 && AliveEnemies.Count == 0)
                    {
                        CurrentState = State.BetweenWaves;
                        stateTimer = 3f;
                        if (WaveIndex < waves.Count - 1)
                        {
                            ShowBanner("ADUTHU YAARU?", "Next! (Wave cleared: +40% health)", 2.5f);
                            if (Player != null) Player.Heal(Player.maxHealth * 0.4f);
                        }
                    }
                    break;

                case State.BetweenWaves:
                    stateTimer -= Time.unscaledDeltaTime;
                    if (stateTimer <= 0f) StartNextWave();
                    break;

                case State.Practice:
                    UpdatePractice();
                    break;

                case State.Victory:
                case State.GameOver:
                    if (GameInput.RestartPressed) Restart();
                    break;
            }
        }

        void SpawnPending()
        {
            if (pendingSpawns.Count == 0 || spawnPoints.Count == 0) return;
            spawnTimer -= Time.deltaTime;
            if (spawnTimer > 0f) return;
            spawnTimer = 0.7f;

            // Use the spawn point furthest from the player so enemies walk in from off-screen.
            Vector3 point = spawnPoints[spawnCursor % spawnPoints.Count];
            if (Player != null)
            {
                float best = -1f;
                for (int i = 0; i < spawnPoints.Count; i++)
                {
                    var p = spawnPoints[(spawnCursor + i) % spawnPoints.Count];
                    float d = (p - Player.transform.position).sqrMagnitude;
                    if (d > best) { best = d; point = p; }
                }
            }
            spawnCursor++;
            Vector3 jitter = new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1f, 1f));
            var enemy = CharacterFactory.CreateEnemy(pendingSpawns.Dequeue(), point + jitter);
            AliveEnemies.Add(enemy);
        }

        int lastAliveCount;
        bool surrenderOffered;

        /// <summary>When a rowdy's friends are all down, he may lose his nerve: beg or run.</summary>
        void CheckLastManStanding()
        {
            int count = AliveEnemies.Count;
            if (pendingSpawns.Count == 0 && count == 1 && lastAliveCount > 1 && !surrenderOffered)
            {
                surrenderOffered = true;
                var last = AliveEnemies[0];
                if (last != null && last.kind == EnemyKind.Rowdy && Random.value < 0.65f)
                    last.Surrender();
            }
            lastAliveCount = count;
        }

        /// <summary>A fleeing rowdy made it out of the street: he counts as beaten.</summary>
        public void EnemyEscaped(EnemyFighter enemy)
        {
            AliveEnemies.Remove(enemy);
            AddPopup("HE RAN AWAY", enemy.Center + Vector3.up, Color.gray, 0.8f);
            Destroy(enemy.gameObject);
        }

        /// <summary>The boss calls for backup.</summary>
        public void SpawnReinforcements(EnemyKind kind, int count)
        {
            if (CurrentState != State.Fighting) return;
            for (int i = 0; i < count; i++) pendingSpawns.Enqueue(kind);
            spawnTimer = 0.8f;
        }

        public void OnPlayerDied()
        {
            if (CurrentState == State.GameOver) return;
            CurrentState = State.GameOver;
            SlowMo(0.25f, 2f);
            ShowBanner("THOTHUTTA", "You lost the street.  Press Enter / Start to try again", 9999f);
        }

        void Restart()
        {
            foreach (var e in AliveEnemies)
                if (e != null) Destroy(e.gameObject);
            AliveEnemies.Clear();
            // Dead bodies are no longer in AliveEnemies but stay registered until destroyed.
            foreach (var f in Fighter.All)
                if (f is EnemyFighter) Destroy(f.gameObject);
            pendingSpawns.Clear();
            EnemyDirector.Reset();
            foreach (var spot in FinisherSpot.All)
                spot.Repair();
            foreach (var w in Weapon.All.ToArray())
                if (w != null && w.Holder == null) Destroy(w.gameObject);
            KasimeduArenaBuilder.PlaceWeapons();
            if (Player != null) Player.Respawn(playerStart);
            hitStopUntil = slowMoUntil = 0f;
            Time.timeScale = 1f;
            BeginIntro();
        }

        // ------------------------------------------------------------------ feel

        public void HitStop(float seconds)
        {
            hitStopUntil = Mathf.Max(hitStopUntil, Time.unscaledTime + seconds);
        }

        public void SlowMo(float scale, float seconds)
        {
            slowMoScale = scale;
            slowMoUntil = Mathf.Max(slowMoUntil, Time.unscaledTime + seconds);
        }

        public void Shake(float amount, float duration = 0.2f)
        {
            if (Cam != null) Cam.Shake(amount, duration);
        }

        void UpdateTimeScale()
        {
            float now = Time.unscaledTime;
            float scale = 1f;
            if (now < slowMoUntil) scale = slowMoScale;
            if (now < hitStopUntil) scale = 0.02f;
            Time.timeScale = scale;
        }

        public void ShowBanner(string title, string subtitle, float seconds)
        {
            BannerTitle = title;
            BannerSubtitle = subtitle;
            BannerUntil = Time.unscaledTime + seconds;
        }

        public void AddPopup(string text, Vector3 worldPosition, Color color, float size = 1f)
        {
            Popups.Add(new Popup
            {
                text = text,
                worldPosition = worldPosition,
                color = color,
                startTime = Time.unscaledTime,
                size = size,
            });
        }

        void CleanPopups()
        {
            Popups.RemoveAll(p => Time.unscaledTime - p.startTime > 1.2f);
        }
    }
}

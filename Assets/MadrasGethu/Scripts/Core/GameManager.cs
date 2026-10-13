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
        public enum State { Intro, Fighting, BetweenWaves, Victory, GameOver }

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
            BeginIntro();
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
                    if (pendingSpawns.Count == 0 && AliveEnemies.Count == 0)
                    {
                        CurrentState = State.BetweenWaves;
                        stateTimer = 3f;
                        if (WaveIndex < waves.Count - 1)
                        {
                            ShowBanner("ADUTHU YAARU?", "Next! (Wave cleared: +25% health)", 2.5f);
                            if (Player != null) Player.Heal(Player.maxHealth * 0.25f);
                        }
                    }
                    break;

                case State.BetweenWaves:
                    stateTimer -= Time.unscaledDeltaTime;
                    if (stateTimer <= 0f) StartNextWave();
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

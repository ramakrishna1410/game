using UnityEngine;

namespace MadrasGethu
{
    /// <summary>
    /// Put this on an empty GameObject in an empty scene and press Play: it builds the
    /// Kasimedu harbour arena, the player, the camera, the HUD and the enemy waves.
    /// (Menu: Madras Gethu > Create Kasimedu Arena Scene does this for you.)
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Tooltip("Optional: a rigged character with an Animator (e.g. from Mixamo). Leave empty for placeholder bodies.")]
        public GameObject playerModelPrefab;
        [Tooltip("Optional: model used for rowdies (and for the others if they have no model of their own).")]
        public GameObject enemyModelPrefab;
        [Tooltip("Optional: model for the Thadiyan bruiser.")]
        public GameObject thadiyanModelPrefab;
        [Tooltip("Optional: model for the boss, Kumaran Anna.")]
        public GameObject bossModelPrefab;
        [Tooltip("Animator Controller for all real characters (Art/Generated/FighterAnimator). Set by the Setup Characters tool.")]
        public RuntimeAnimatorController fighterAnimator;

        [Tooltip("Build the placeholder harbour. Turn off once you have your own level geometry in the scene.")]
        public bool buildArena = true;

        public Vector3 playerStart = new Vector3(0f, 0.1f, -6f);

        [Tooltip("Practice yard: one dummy that never moves, never attacks and never dies. No waves.")]
        public bool practiceMode;

        void Awake()
        {
            Time.timeScale = 1f;
            CombatUtil.ConfigurePhysicsLayers();
            CharacterFactory.PlayerModelPrefab = playerModelPrefab;
            CharacterFactory.EnemyModelPrefab = enemyModelPrefab;
            CharacterFactory.ThadiyanModelPrefab = thadiyanModelPrefab;
            CharacterFactory.BossModelPrefab = bossModelPrefab;
            CharacterFactory.FighterAnimator = fighterAnimator;

            if (practiceMode)
            {
                playerStart = PracticeYardBuilder.PlayerStart;
                if (buildArena) PracticeYardBuilder.Build(new GameObject("Practice Yard").transform);
            }
            else if (buildArena)
            {
                var arena = new GameObject("Kasimedu Harbour").transform;
                KasimeduArenaBuilder.Build(arena);
            }

            var cam = SetupCamera();
            GameAudio.StartMusic();

            var gmGo = new GameObject("Game Manager");
            var gm = gmGo.AddComponent<GameManager>();
            gm.playerStart = playerStart;
            gm.Cam = cam;
            if (practiceMode)
            {
                gm.practiceMode = true;
                gm.dummySpawn = PracticeYardBuilder.DummySpawn;
            }
            else
            {
                gm.spawnPoints.Add(new Vector3(0f, 0.1f, 28f));
                gm.spawnPoints.Add(new Vector3(-4f, 0.1f, 24f));
                gm.spawnPoints.Add(new Vector3(2f, 0.1f, -28f));
                gm.spawnPoints.Add(new Vector3(-3f, 0.1f, -24f));
                AddWaves(gm);
            }

            var player = CharacterFactory.CreatePlayer(playerStart, cam);
            gm.Player = player;
            cam.target = player.transform;

            gmGo.AddComponent<GameHUD>();
        }

        ThirdPersonCamera SetupCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                camera = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.06f, 0.1f);
            camera.fieldOfView = 58f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 250f;

            var tpc = camera.GetComponent<ThirdPersonCamera>();
            if (tpc == null) tpc = camera.gameObject.AddComponent<ThirdPersonCamera>();
            return tpc;
        }

        static void AddWaves(GameManager gm)
        {
            gm.waves.Add(new GameManager.Wave
            {
                title = "KASIMEDU ROWDIES",
                subtitle = "Settu's boys want to know why you came back.",
                enemies = { EnemyKind.Rowdy, EnemyKind.Rowdy, EnemyKind.Rowdy },
            });
            gm.waves.Add(new GameManager.Wave
            {
                title = "THADIYAN",
                subtitle = "Too big to grab. Break his guard with heavies, dodge or counter the red slam.",
                enemies = { EnemyKind.Rowdy, EnemyKind.Rowdy, EnemyKind.Thadiyan, EnemyKind.Rowdy, EnemyKind.Rowdy },
            });
            gm.waves.Add(new GameManager.Wave
            {
                title = "KUMARAN ANNA",
                subtitle = "The harbour boss. He runs Kasimedu. Tonight he answers to you.",
                enemies = { EnemyKind.Boss, EnemyKind.Rowdy, EnemyKind.Rowdy },
            });
        }
    }
}

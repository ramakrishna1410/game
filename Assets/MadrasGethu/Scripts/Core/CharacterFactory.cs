using UnityEngine;

namespace MadrasGethu
{
    /// <summary>Builds the player and enemies at runtime (CharacterController + motor + fighter + body).</summary>
    public static class CharacterFactory
    {
        /// <summary>Optional real character models (e.g. Mixamo). Leave null for the placeholder bodies.</summary>
        public static GameObject PlayerModelPrefab;
        public static GameObject EnemyModelPrefab;

        const int IgnoreRaycastLayer = 2;   // the camera's collision check skips fighters

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            PlayerModelPrefab = null;
            EnemyModelPrefab = null;
        }

        static GameObject CreateBody(string name, Vector3 position, float scale)
        {
            var go = new GameObject(name) { layer = IgnoreRaycastLayer };
            go.transform.position = position;

            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f * scale;
            cc.radius = 0.35f * scale;
            cc.center = new Vector3(0f, 0.9f * scale, 0f);
            cc.stepOffset = 0.35f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.05f;

            go.AddComponent<FighterMotor>();
            return go;
        }

        static FighterVisual AddVisual(GameObject owner, float scale, Color shirt, Color pants, Color accent, GameObject model)
        {
            var visualGo = new GameObject("Visual") { layer = IgnoreRaycastLayer };
            visualGo.transform.SetParent(owner.transform, false);
            visualGo.transform.localScale = Vector3.one * scale;
            var visual = visualGo.AddComponent<FighterVisual>();
            visual.shirtColor = shirt;
            visual.pantsColor = pants;
            visual.accentColor = accent;
            visual.modelPrefab = model;
            visual.Build();
            return visual;
        }

        public static PlayerFighter CreatePlayer(Vector3 position, ThirdPersonCamera cam)
        {
            var go = CreateBody("Player (Maaran)", position, 1f);
            var player = go.AddComponent<PlayerFighter>();
            player.ResetHealth(150f);
            player.SetCamera(cam);

            // Black shirt, blue-checked lungi, red towel around the waist: a North Chennai boxer.
            AddVisual(go, 1f,
                new Color(0.08f, 0.08f, 0.1f),
                new Color(0.2f, 0.3f, 0.6f),
                new Color(0.75f, 0.1f, 0.1f),
                PlayerModelPrefab);
            return player;
        }

        public static EnemyFighter CreateEnemy(EnemyKind kind, Vector3 position)
        {
            var go = CreateBody("Enemy", position, 1f);
            var enemy = go.AddComponent<EnemyFighter>();
            enemy.Configure(kind);
            var profile = enemy.Profile;
            go.name = profile.displayName;

            // Resize the controller for big guys.
            var cc = go.GetComponent<CharacterController>();
            cc.height = 1.8f * profile.scale;
            cc.radius = 0.35f * profile.scale;
            cc.center = new Vector3(0f, 0.9f * profile.scale, 0f);
            enemy.height = 1.8f * profile.scale;

            AddVisual(go, profile.scale, profile.shirt, profile.pants, profile.accent, EnemyModelPrefab);

            var gm = GameManager.Instance;
            if (gm != null && gm.Player != null)
            {
                Vector3 toPlayer = Vector3.ProjectOnPlane(gm.Player.transform.position - position, Vector3.up);
                if (toPlayer.sqrMagnitude > 0.01f) go.transform.rotation = Quaternion.LookRotation(toPlayer);
            }
            return enemy;
        }
    }
}

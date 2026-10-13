using UnityEngine;

namespace MadrasGethu
{
    /// <summary>
    /// Prototype HUD drawn with IMGUI (no Canvas or TextMeshPro setup needed):
    /// health and Gethu bars, combo counter, enemy bars, wave banners, floating text and a controls panel.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        static readonly Color Gold = new Color(1f, 0.78f, 0.2f);
        static readonly Color HealthRed = new Color(0.85f, 0.15f, 0.12f);
        static readonly Color HealthGreen = new Color(0.3f, 0.8f, 0.35f);
        static readonly Color Panel = new Color(0f, 0f, 0f, 0.55f);

        bool showHelp = true;
        float helpAutoHideAt;
        float displayedHealth = 1f;
        GUIStyle label, big, huge, small, center;
        float lastScale;

        GameManager GM => GameManager.Instance;

        void Start()
        {
            helpAutoHideAt = Time.unscaledTime + 12f;
        }

        void Update()
        {
            if (GameInput.HelpPressed) { showHelp = !showHelp; helpAutoHideAt = float.MaxValue; }
            if (Time.unscaledTime > helpAutoHideAt) { showHelp = false; helpAutoHideAt = float.MaxValue; }
        }

        float S => Screen.height / 1080f;

        void EnsureStyles()
        {
            if (label != null && Mathf.Approximately(lastScale, S)) return;
            lastScale = S;
            label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(22 * S), fontStyle = FontStyle.Bold };
            label.normal.textColor = Color.white;
            small = new GUIStyle(label) { fontSize = Mathf.RoundToInt(17 * S), fontStyle = FontStyle.Normal };
            big = new GUIStyle(label) { fontSize = Mathf.RoundToInt(44 * S), alignment = TextAnchor.MiddleCenter };
            huge = new GUIStyle(label) { fontSize = Mathf.RoundToInt(84 * S), alignment = TextAnchor.MiddleCenter };
            center = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
        }

        void OnGUI()
        {
            if (GM == null) return;
            EnsureStyles();

            DrawEnemyBars();
            DrawPopups();
            DrawPlayerPanel();
            DrawCombo();
            DrawWaveInfo();
            DrawBanner();
            DrawPrompt();
            if (showHelp) DrawHelp();

            if (Cursor.lockState != CursorLockMode.Locked)
                Shadowed(new Rect(0, Screen.height * 0.82f, Screen.width, 40 * S), "Click to fight  ·  Esc frees the mouse", center, Color.white);
        }

        // ------------------------------------------------------------------ player

        void DrawPlayerPanel()
        {
            var p = GM.Player;
            if (p == null) return;
            float x = 30 * S, y = 30 * S, w = 460 * S;

            Shadowed(new Rect(x, y, w, 30 * S), "MAARAN", label, Color.white);
            y += 32 * S;

            // Health: a red "recent damage" trail behind the live bar, like most brawlers.
            displayedHealth = Mathf.MoveTowards(displayedHealth, p.HealthFraction, Time.unscaledDeltaTime * 0.6f);
            if (displayedHealth < p.HealthFraction) displayedHealth = p.HealthFraction;
            Bar(new Rect(x, y, w, 22 * S), 1f, Panel);
            Bar(new Rect(x, y, w, 22 * S), displayedHealth, HealthRed);
            Bar(new Rect(x, y, w, 22 * S), p.HealthFraction, Color.Lerp(HealthRed, HealthGreen, p.HealthFraction));
            y += 30 * S;

            // Gethu meter in three segments; flashes when full.
            float gw = w * 0.85f;
            Bar(new Rect(x, y, gw, 16 * S), 1f, Panel);
            Color g = Gold;
            if (p.MassMode) g = Color.Lerp(Gold, Color.white, Mathf.PingPong(Time.unscaledTime * 6f, 1f));
            else if (p.GethuFraction >= 1f) g = Color.Lerp(Gold, new Color(1f, 0.4f, 0.1f), Mathf.PingPong(Time.unscaledTime * 3f, 1f));
            Bar(new Rect(x, y, gw, 16 * S), p.GethuFraction, g);
            for (int i = 1; i < 3; i++)
                Bar(new Rect(x + gw * i / 3f - 1, y, 2 * S, 16 * S), 1f, new Color(0f, 0f, 0f, 0.8f));
            string gethuLabel = p.MassMode ? "GETHU MODE!" : p.GethuFraction >= 1f ? "GETHU FULL · Q / RT" : "GETHU";
            Shadowed(new Rect(x + gw + 10 * S, y - 6 * S, 300 * S, 28 * S), gethuLabel, small, Gold);
        }

        void DrawCombo()
        {
            var p = GM.Player;
            if (p == null || p.ComboHits < 2) return;
            float pulse = 1f + Mathf.Clamp01(p.ComboTimer - 1.8f) * 2f;
            var style = new GUIStyle(big) { alignment = TextAnchor.MiddleRight, fontSize = Mathf.RoundToInt(big.fontSize * pulse) };
            Shadowed(new Rect(Screen.width - 430 * S, Screen.height * 0.32f, 400 * S, 60 * S), $"{p.ComboHits} HITS", style, Gold);
        }

        void DrawPrompt()
        {
            var p = GM.Player;
            if (p == null || string.IsNullOrEmpty(p.ContextPrompt)) return;
            var r = new Rect(Screen.width * 0.5f - 400 * S, Screen.height - 120 * S, 800 * S, 40 * S);
            Bar(r, 1f, Panel);
            Shadowed(r, p.ContextPrompt, center, Gold);
        }

        // ------------------------------------------------------------------ enemies

        void DrawEnemyBars()
        {
            var cam = Camera.main;
            if (cam == null) return;

            foreach (var e in GM.AliveEnemies)
            {
                if (e == null || e.IsDead) continue;
                if (e.IsBoss)
                {
                    DrawBossBar(e);
                    continue;
                }
                Vector3 sp = cam.WorldToScreenPoint(e.transform.position + Vector3.up * (e.height + 0.35f));
                if (sp.z <= 0f) continue;
                float w = 90 * S, h = 8 * S;
                var r = new Rect(sp.x - w / 2f, Screen.height - sp.y, w, h);
                Bar(r, 1f, Panel);
                Bar(r, e.HealthFraction, e.IsTelegraphing ? new Color(1f, 0.3f, 0.2f) : new Color(0.9f, 0.85f, 0.8f));
            }
        }

        void DrawBossBar(EnemyFighter boss)
        {
            float w = Screen.width * 0.5f;
            var r = new Rect((Screen.width - w) / 2f, Screen.height - 70 * S, w, 18 * S);
            Shadowed(new Rect(r.x, r.y - 34 * S, w, 30 * S), boss.displayName.ToUpper(), label, Color.white);
            Bar(r, 1f, Panel);
            Bar(r, boss.HealthFraction, HealthRed);
        }

        // ------------------------------------------------------------------ messages

        void DrawWaveInfo()
        {
            if (GM.WaveIndex < 0 || GM.WaveIndex >= GM.waves.Count) return;
            var r = new Rect(Screen.width - 430 * S, 30 * S, 400 * S, 30 * S);
            var right = new GUIStyle(small) { alignment = TextAnchor.UpperRight };
            Shadowed(r, $"Wave {GM.WaveIndex + 1}/{GM.waves.Count}   ·   Rowdies standing: {GM.AliveEnemies.Count}", right, Color.white);
        }

        void DrawBanner()
        {
            if (Time.unscaledTime > GM.BannerUntil || string.IsNullOrEmpty(GM.BannerTitle)) return;
            float remaining = GM.BannerUntil - Time.unscaledTime;
            float alpha = Mathf.Clamp01(remaining * 2f);
            float y = Screen.height * 0.3f;
            Bar(new Rect(0, y - 10 * S, Screen.width, 160 * S), 1f, new Color(0f, 0f, 0f, 0.45f * alpha));
            Shadowed(new Rect(0, y, Screen.width, 100 * S), GM.BannerTitle, huge, new Color(Gold.r, Gold.g, Gold.b, alpha));
            Shadowed(new Rect(0, y + 95 * S, Screen.width, 40 * S), GM.BannerSubtitle, center, new Color(1f, 1f, 1f, alpha));
        }

        void DrawPopups()
        {
            var cam = Camera.main;
            if (cam == null) return;
            foreach (var p in GM.Popups)
            {
                float age = Time.unscaledTime - p.startTime;
                Vector3 sp = cam.WorldToScreenPoint(p.worldPosition + Vector3.up * age * 0.8f);
                if (sp.z <= 0f) continue;
                var style = new GUIStyle(center) { fontSize = Mathf.RoundToInt(30 * S * p.size) };
                Color c = p.color;
                c.a = Mathf.Clamp01(1.2f - age);
                Shadowed(new Rect(sp.x - 300 * S, Screen.height - sp.y - 25 * S, 600 * S, 50 * S), p.text, style, c);
            }
        }

        void DrawHelp()
        {
            string[] lines =
            {
                "CONTROLS                      (H to hide)",
                "WASD / Left stick ........ move    (Shift / LB sprint)",
                "Mouse / Right stick ...... camera",
                "Left click / X ........... jab-cross-hook-body combo",
                "Right click / Y .......... heavy ender (changes with combo step)",
                "Right click near downed .. stomp",
                "F / RB (hold) ............ block",
                "F / RB when enemy flashes RED ... COUNTER",
                "Space / A ................ dodge",
                "E / B .................... grab  →  click: knee · E/RMB: throw",
                "Q / RT ................... Gethu: finisher while grabbing near",
                "                           tea kadai / crates / auto, or Gethu Mode",
            };
            float w = 640 * S, lh = 24 * S;
            var r = new Rect(30 * S, Screen.height - (lines.Length * lh + 40 * S), w, lines.Length * lh + 20 * S);
            Bar(r, 1f, Panel);
            for (int i = 0; i < lines.Length; i++)
                Shadowed(new Rect(r.x + 14 * S, r.y + 10 * S + i * lh, w, lh), lines[i], small, i == 0 ? Gold : Color.white);
        }

        // ------------------------------------------------------------------ drawing helpers

        static void Bar(Rect r, float fill, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fill), r.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        static void Shadowed(Rect r, string text, GUIStyle style, Color color)
        {
            var old = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, color.a * 0.8f);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
            style.normal.textColor = old;
        }
    }
}

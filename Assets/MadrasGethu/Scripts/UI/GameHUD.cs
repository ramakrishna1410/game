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
        bool showChecklist = true;
        float helpAutoHideAt;
        float displayedHealth = 1f;
        GUIStyle label, big, huge, small, center, keyText;
        float lastScale;

        GameManager GM => GameManager.Instance;

        void Start()
        {
            helpAutoHideAt = Time.unscaledTime + 12f;
            if (GM != null && GM.PracticeMode) { showHelp = false; helpAutoHideAt = float.MaxValue; }
        }

        void Update()
        {
            if (GameInput.HelpPressed) { showHelp = !showHelp; helpAutoHideAt = float.MaxValue; }
            if (GameInput.PracticeListPressed) showChecklist = !showChecklist;
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
            keyText = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(19 * S) };
        }

        void OnGUI()
        {
            if (GM == null) return;
            EnsureStyles();

            DrawFinisherMarkers();
            DrawEnemyBars();
            DrawPopups();
            DrawPlayerPanel();
            DrawCombo();
            DrawWaveInfo();
            if (GM.PracticeMode) DrawPractice();
            DrawBanner();
            DrawPrompt();
            DrawQte();
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
            // Weapon in hand and how many hits it has left.
            if (p.HeldWeapon != null)
            {
                var held = p.HeldWeapon;
                string pips = new string('■', Mathf.Max(0, held.HitsLeft)) + new string('□', Mathf.Max(0, held.durability - held.HitsLeft));
                Shadowed(new Rect(x, y + 22 * S, 520 * S, 26 * S),
                    $"{held.displayName.ToUpper()}  {pips}   (E: throw)", small, new Color(1f, 0.85f, 0.5f));
            }

            int bars = Mathf.FloorToInt(p.Gethu / p.GethuBar + 0.001f);
            string gethuLabel = p.MassMode ? "GETHU MODE!" : p.GethuFraction >= 1f ? "GETHU FULL · Q" : bars > 0 ? $"GETHU · {bars} bar{(bars > 1 ? "s" : "")}" : "GETHU";
            Shadowed(new Rect(x + gw + 10 * S, y - 6 * S, 300 * S, 28 * S), gethuLabel, small, Gold);
        }

        void DrawCombo()
        {
            var p = GM.Player;
            if (p == null || p.ComboHits < 2) return;
            float pulse = 1f + Mathf.Clamp01(p.ComboTimer - 1.8f) * 2f;
            var style = new GUIStyle(big) { alignment = TextAnchor.MiddleRight, fontSize = Mathf.RoundToInt(big.fontSize * pulse) };
            Shadowed(new Rect(Screen.width - 430 * S, Screen.height * 0.32f, 400 * S, 60 * S), $"{p.ComboHits} HITS", style, Gold);
            if (p.ComboGrade > 0)
            {
                var gradeStyle = new GUIStyle(label) { alignment = TextAnchor.MiddleRight, fontSize = Mathf.RoundToInt((26 + 4 * p.ComboGrade) * S) };
                Color gc = p.ComboGrade >= 4 ? Color.Lerp(Gold, new Color(1f, 0.3f, 0.2f), Mathf.PingPong(Time.unscaledTime * 4f, 1f))
                         : p.ComboGrade == 3 ? new Color(1f, 0.55f, 0.15f) : Gold;
                Shadowed(new Rect(Screen.width - 430 * S, Screen.height * 0.32f + 58 * S, 400 * S, 40 * S),
                    $"{p.ComboGradeName}   Gethu x{p.GethuMultiplier:0.00}", gradeStyle, gc);
            }
        }

        void DrawPrompt()
        {
            var p = GM.Player;
            if (p == null || p.QteActive) return;

            // Grabbed by a rowdy: mash meter.
            if (p.IsHeld)
            {
                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 20f);
                var meter = new Rect(Screen.width * 0.5f - 200 * S, Screen.height * 0.62f, 400 * S, 24 * S);
                float k = 64 * S * pulse;
                HudIcons.DrawKey(new Rect(Screen.width * 0.5f - k / 2f, meter.y - k - 46 * S, k, k), "E", Gold, keyText);
                Shadowed(new Rect(0, meter.y - 44 * S, Screen.width, 40 * S), "TAP FAST TO BREAK FREE!", center, new Color(1f, 0.85f, 0.2f));
                Bar(meter, 1f, Panel);
                Bar(meter, p.BreakFreeProgress, new Color(0.4f, 1f, 0.4f));
                return;
            }
            if (p.Prompts.Count == 0) return;

            // A row of key caps with what each does, centred at the bottom of the screen.
            float h = 40 * S, gap = 26 * S;
            var labelStyle = new GUIStyle(label) { alignment = TextAnchor.MiddleLeft, fontSize = Mathf.RoundToInt(20 * S) };
            float total = 0f;
            foreach (var item in p.Prompts)
                total += HudIcons.KeyWidth(item.key, h) + 8 * S + labelStyle.CalcSize(new GUIContent(item.label)).x + gap;
            total -= gap;
            float x = (Screen.width - total) / 2f, y = Screen.height - 130 * S;
            Bar(new Rect(x - 16 * S, y - 8 * S, total + 32 * S, h + 16 * S), 1f, new Color(0f, 0f, 0f, 0.4f));
            foreach (var item in p.Prompts)
            {
                float a = item.dim ? 0.45f : 1f;
                float kw = HudIcons.KeyWidth(item.key, h);
                HudIcons.DrawKey(new Rect(x, y, kw, h), item.key, item.key == "Q" ? new Color(1f, 0.6f, 0.15f) : Gold, keyText, a);
                x += kw + 8 * S;
                float lw = labelStyle.CalcSize(new GUIContent(item.label)).x;
                Shadowed(new Rect(x, y, lw + 4 * S, h), item.label, labelStyle, new Color(1f, 1f, 1f, a));
                x += lw + gap;
            }
        }

        /// <summary>Quick-time prompt for wrestling finishers: a ring closes in on the key; press as it meets the gold circle.</summary>
        void DrawQte()
        {
            var p = GM.Player;
            if (p == null) return;
            float cx = Screen.width * 0.5f, cy = Screen.height * 0.74f;   // low, so the close-up stays visible

            if (p.QteActive)
            {
                float prog = p.QteProgress;
                float baseR = 54 * S;
                float t = PlayerFighter.QtePerfectAt;
                float scale = prog <= t ? Mathf.Lerp(2.6f, 1f, prog / t) : Mathf.Lerp(1f, 0.6f, (prog - t) / (1f - t));
                bool inWindow = Mathf.Abs(prog - t) <= 0.13f;

                HudIcons.Draw(new Rect(cx - baseR * 1.25f, cy - baseR * 1.25f, baseR * 2.5f, baseR * 2.5f), HudIcons.Disc, new Color(0f, 0f, 0f, 0.45f));
                HudIcons.Draw(new Rect(cx - baseR, cy - baseR, baseR * 2f, baseR * 2f), HudIcons.Ring, Gold);
                float r = baseR * scale;
                HudIcons.Draw(new Rect(cx - r, cy - r, r * 2f, r * 2f), HudIcons.Ring,
                    inWindow ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 1f, 1f, 0.9f));

                string key = p.QteKeyNow == QteKey.Light ? "LMB" : p.QteKeyNow == QteKey.Heavy ? "RMB" : "SPACE";
                float kh = 58 * S;
                float kw = HudIcons.KeyWidth(key, kh);
                HudIcons.DrawKey(new Rect(cx - kw / 2f, cy - kh / 2f, kw, kh), key, Gold, keyText);

                Shadowed(new Rect(0, cy + baseR * 1.3f, Screen.width, 40 * S), p.QteLabelNow, center, Color.white);
                Shadowed(new Rect(0, cy - baseR * 1.3f - 80 * S, Screen.width, 50 * S), p.WrestleName,
                    new GUIStyle(big) { fontSize = Mathf.RoundToInt(36 * S) }, Gold);
                if (p.QteTotal > 1)
                    Shadowed(new Rect(0, cy + baseR * 1.3f + 34 * S, Screen.width, 30 * S), $"{p.QteNumber} / {p.QteTotal}", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter }, new Color(1f, 1f, 1f, 0.7f));
            }

            float since = Time.unscaledTime - p.QteResultTime;
            if (since < 0.6f && !string.IsNullOrEmpty(p.QteResultText))
            {
                Color c = p.QteResultText == "MISS" ? new Color(1f, 0.45f, 0.35f) : p.QteResultText == "GOOD" ? new Color(1f, 0.9f, 0.5f) : new Color(0.4f, 1f, 0.5f);
                c.a = Mathf.Clamp01((0.6f - since) * 3f);
                Shadowed(new Rect(0, cy - 150 * S - since * 60 * S, Screen.width, 60 * S), p.QteResultText,
                    new GUIStyle(big) { fontSize = Mathf.RoundToInt(48 * S) }, c);
            }
        }

        // ------------------------------------------------------------------ enemies

        EnemyFighter grabHintTarget;

        /// <summary>The rowdy the hero is facing, close enough to grab (only one gets the hand icon).</summary>
        EnemyFighter FindGrabHintTarget()
        {
            var p = GM.Player;
            if (p == null || p.IsDead || p.Grabbed != null || p.HeldWeapon != null) return null;
            if (p.State != PlayerFighter.PlayerState.Free && p.State != PlayerFighter.PlayerState.Attack && p.State != PlayerFighter.PlayerState.Block)
                return null;
            EnemyFighter best = null;
            float bestDist = 3.2f;
            foreach (var e in GM.AliveEnemies)
            {
                if (e == null || e.IsDead || e.IsDowned || e.IsBegging) continue;
                Vector3 to = e.transform.position - p.transform.position;
                to.y = 0f;
                float d = to.magnitude;
                if (d < bestDist && Vector3.Dot(p.transform.forward, to.normalized) > 0.3f) { bestDist = d; best = e; }
            }
            return best;
        }

        /// <summary>
        /// Hand icon over the rowdy in front: white = grabbable (E), flashing yellow = dazed (grab now!),
        /// crossed out = too heavy until staggered. A skull means a wrestling finisher is ready on him.
        /// </summary>
        void DrawGrabHint(EnemyFighter e, Vector3 sp)
        {
            var p = GM.Player;
            float size = 34 * S;
            float x = sp.x + 56 * S, y = Screen.height - sp.y - size * 0.6f;
            bool can = e.CanBeGrabbed;
            if (e.IsDazed) size *= 1f + 0.15f * Mathf.Sin(Time.unscaledTime * 12f);
            var r = new Rect(x, y, size, size);
            Color handColor = !can ? new Color(0.7f, 0.7f, 0.7f, 0.7f)
                            : e.IsDazed ? Color.Lerp(Gold, Color.white, Mathf.PingPong(Time.unscaledTime * 5f, 1f))
                            : new Color(1f, 1f, 1f, 0.9f);
            HudIcons.Draw(new Rect(r.x + 2, r.y + 2, r.width, r.height), HudIcons.Hand, new Color(0f, 0f, 0f, 0.6f));
            HudIcons.Draw(r, HudIcons.Hand, handColor);
            if (!can)
            {
                // Red slash: too heavy to grab until you stagger him.
                var old = GUI.matrix;
                GUIUtility.RotateAroundPivot(-45f, r.center);
                Bar(new Rect(r.center.x - size * 0.6f, r.center.y - 2.5f * S, size * 1.2f, 5 * S), 1f, new Color(1f, 0.25f, 0.2f, 0.95f));
                GUI.matrix = old;
                return;
            }
            float k = 22 * S;
            HudIcons.DrawKey(new Rect(r.xMax + 4 * S, r.center.y - k / 2f, k, k), "E", Gold, new GUIStyle(keyText) { fontSize = Mathf.RoundToInt(14 * S) });
            if (e.IsDazed && p != null && p.Gethu >= p.GethuBar)
            {
                var skullRect = new Rect(r.xMax + 30 * S, r.y, size, size);
                HudIcons.Draw(skullRect, HudIcons.Skull, new Color(1f, 0.6f, 0.15f, 0.95f));
            }
        }

        void DrawEnemyBars()
        {
            var cam = Camera.main;
            if (cam == null) return;
            grabHintTarget = FindGrabHintTarget();

            foreach (var e in GM.AliveEnemies)
            {
                if (e == null || e.IsDead) continue;
                float top = e.Visual != null ? e.Visual.HeadTopY : e.transform.position.y + e.height;
                Vector3 sp = cam.WorldToScreenPoint(new Vector3(e.transform.position.x, top + 0.25f, e.transform.position.z));
                if (sp.z <= 0f) continue;

                // Warning above a rowdy about to attack: what to press depends on the colour.
                var tele = e.CurrentTelegraph;
                if (tele != TelegraphColor.None)
                {
                    float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 18f);
                    float size = 46 * S * pulse;
                    var box = new Rect(sp.x - size / 2f, Screen.height - sp.y - size - 14 * S, size, size);
                    Color boxColor = tele == TelegraphColor.Red ? new Color(0.1f, 0.7f, 1f, 0.9f)
                                   : tele == TelegraphColor.Orange ? new Color(1f, 0.5f, 0.1f, 0.9f)
                                   : new Color(1f, 0.85f, 0.15f, 0.9f);
                    string key = tele == TelegraphColor.Red ? "F" : "C";
                    string what = tele == TelegraphColor.Red ? "PARRY" : tele == TelegraphColor.Orange ? "DODGE" : "DODGE GRAB";
                    HudIcons.DrawKey(box, key, boxColor, new GUIStyle(keyText) { fontSize = Mathf.RoundToInt(22 * S * pulse) });
                    Shadowed(new Rect(sp.x - 70 * S, box.yMax, 140 * S, 20 * S), what, new GUIStyle(small) { alignment = TextAnchor.MiddleCenter }, boxColor);
                }
                if (e.IsBegging)
                {
                    Shadowed(new Rect(sp.x - 220 * S, Screen.height - sp.y - 40 * S, 440 * S, 26 * S),
                        "E: let him go   ·   Hit him: finish him", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold },
                        new Color(1f, 0.9f, 0.6f));
                }

                if (e == grabHintTarget) DrawGrabHint(e, sp);
                else if (e.IsDowned && GM.Player != null && GM.Player.Gethu >= GM.Player.GethuBar && GM.Player.Grabbed == null)
                {
                    Vector3 to = e.transform.position - GM.Player.transform.position;
                    to.y = 0f;
                    if (to.magnitude < 2.6f)
                    {
                        Vector3 low = cam.WorldToScreenPoint(e.transform.position + Vector3.up * 0.9f);
                        if (low.z > 0f)
                        {
                            float sz = 30 * S * (1f + 0.1f * Mathf.Sin(Time.unscaledTime * 8f));
                            HudIcons.Draw(new Rect(low.x - sz / 2f, Screen.height - low.y - sz, sz, sz), HudIcons.Skull, new Color(1f, 0.6f, 0.15f, 0.95f));
                        }
                    }
                }

                if (e.IsBoss)
                {
                    DrawBossBar(e);
                    continue;
                }
                float w = 90 * S, h = 8 * S;
                var r = new Rect(sp.x - w / 2f, Screen.height - sp.y, w, h);
                Bar(r, 1f, Panel);
                Bar(r, e.HealthFraction, e.IsTelegraphing ? new Color(1f, 0.3f, 0.2f) : new Color(0.9f, 0.85f, 0.8f));
            }
        }

        /// <summary>Labels over props that can be used for environment attacks.</summary>
        void DrawFinisherMarkers()
        {
            var cam = Camera.main;
            var p = GM.Player;
            if (cam == null || p == null || p.IsDead) return;
            var style = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };

            foreach (var spot in FinisherSpot.All)
            {
                if (spot == null || spot.IsBroken) continue;
                Vector3 flat = spot.transform.position - p.transform.position;
                flat.y = 0f;
                float dist = flat.magnitude;
                if (dist > 12f) continue;
                Vector3 sp = cam.WorldToScreenPoint(spot.SlamPoint + Vector3.up * 1.6f);
                if (sp.z <= 0f) continue;

                bool enough = p.Gethu >= spot.gethuCost;
                bool ready = p.Grabbed != null && dist <= spot.useRadius && enough;
                string text;
                Color color;
                if (ready) { text = "Q  —  " + spot.finisherName; color = new Color(1f, 0.8f, 0.2f); }
                else if (p.Grabbed != null) { text = enough ? "▼ drag him here" : $"▼ {spot.promptName}: throw him in (E)"; color = new Color(1f, 0.85f, 0.4f); }
                else { text = $"▼ {spot.promptName.ToUpper()}"; color = enough ? new Color(1f, 0.85f, 0.4f) : new Color(0.85f, 0.85f, 0.85f, 0.8f); }

                float alpha = Mathf.Clamp01((12f - dist) / 4f);
                color.a *= alpha;
                var st = ready ? new GUIStyle(style) { fontSize = Mathf.RoundToInt(24 * S) } : style;
                Shadowed(new Rect(sp.x - 220 * S, Screen.height - sp.y - 14 * S, 440 * S, 28 * S), text, st, color);
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

        // ------------------------------------------------------------------ practice yard

        static readonly string[,] PracticeMoves =
        {
            { "COMBOS", "" },
            { "Haymaker", "RMB" },
            { "Charged Haymaker", "hold RMB" },
            { "Kasimedu Kick", "LMB RMB" },
            { "Shoulder Barge", "LMB RMB RMB" },
            { "Kasimedu Haymaker", "LMB RMB RMB RMB" },
            { "Uppercut", "LMB LMB RMB" },
            { "Air Hammer", "LMB LMB RMB RMB" },
            { "Spinning Backfist", "LMB x3 RMB" },
            { "Royapuram Rush Finisher", "LMB x4 RMB" },
            { "Double Hammer", "heavy, RMB" },
            { "Leg Sweep", "back + RMB" },
            { "Shoulder Rush", "forward + RMB (far)" },
            { "Running Knee", "sprint + LMB" },
            { "Clothesline", "sprint + RMB" },
            { "Back Elbow", "LMB, him behind you" },
            { "Pivot Backfist", "dodge, LMB" },
            { "Flying Kick", "jump, LMB" },
            { "Stomp", "RMB on the ground" },
            { "Wall Bounce", "knock him into a wall" },
            { "Wall Slam", "grab, E at the wall" },
            { "STREET MOVES", "" },
            { "Spinning Heel Kick", "dodge, RMB" },
            { "Superman Punch", "jump, RMB" },
            { "Kaal Odaippu", "S+LMB, then LMB" },
            { "Rugby Tackle", "sprint + E, LMB.., RMB" },
            { "GRAB COMBOS", "" },
            { "Royapuram Rush", "grab, LMB LMB LMB" },
            { "Machine Gun Kuthu", "grab, tap LMB fast, RMB" },
            { "Kasimedu Kick Storm", "grab, RMB RMB" },
            { "Thalaivar Special", "grab, LMB RMB" },
            { "Jallikattu", "grab, RMB LMB" },
            { "Gethu Mass Combo", "grab, LMB+RMB (full)" },
            { "COUNTERS", "" },
            { "Counter Punch", "perfect parry (F)" },
            { "Kai Odaippu", "perfect parry, LMB" },
            { "Counter Throw", "perfect parry, E" },
            { "Counter Finisher", "perfect parry, Q" },
            { "Weapon Steal", "perfect parry a weapon" },
            { "Grab Reversal", "E as he grabs (yellow)" },
            { "Escape Strike", "break free when held" },
            { "Matador", "dodge a charge, RMB" },
            { "Double Counter", "parry two at once" },
            { "HOLDING HIM", "" },
            { "Mutta Mothal", "grab, E by another rowdy" },
            { "Arm Twist", "grab, hold RMB" },
            { "Ear Clap", "grab, LMB then F" },
            { "Trip & Stomp", "grab, S+F" },
            { "Bowling Throw", "grab, SHIFT+E" },
            { "WRESTLING", "" },
            { "Body Slam", "grab, Q" },
            { "Spin Drop", "grab, S+Q (shoulders, spin)" },
            { "German Suplex", "grab from behind, Q" },
            { "Spinebuster", "grab at a wall, W+Q" },
            { "Powerbomb", "grab, SHIFT+Q (full)" },
            { "Spear", "sprint at him, Q" },
            { "Elbow Drop", "him on the ground, Q" },
            { "Choke-Out", "grab, A+Q" },
            { "Thai Clinch Knees", "grab, W+Q (no wall)" },
            { "Giant Swing", "grab, D+Q (full)" },
            { "PROPS", "" },
            { "Tea Kadai Smash", "grab at the kadai, Q" },
            { "Meen Petti Slam", "grab at the crates, Q" },
            { "Auto Smash", "grab at the auto, Q" },
            { "Kadal Throw", "grab at the jetty edge, Q" },
            { "Ice Box Slam", "grab at the ice box, Q" },
        };

        /// <summary>Practice yard: a checklist of every move (ticks when it lands), damage numbers and the yard keys.</summary>
        void DrawPractice()
        {
            float x = Screen.width - 440 * S, y = 30 * S, w = 410 * S;
            var right = new GUIStyle(small) { alignment = TextAnchor.UpperRight };
            Shadowed(new Rect(x, y, w, 26 * S), "PRACTICE YARD", new GUIStyle(label) { alignment = TextAnchor.UpperRight }, Gold);
            y += 30 * S;
            var p = GM.Player;
            string dummyName = GM.Dummy != null ? GM.Dummy.Profile.displayName : "-";
            Shadowed(new Rect(x, y, w, 22 * S), $"R reset dummy  ·  T dummy: {dummyName}  ·  G infinite Gethu: {(p != null && p.infiniteGethu ? "ON" : "OFF")}", right, Color.white);
            y += 22 * S;
            Shadowed(new Rect(x, y, w, 22 * S), (GM.PartnerDummy != null ? "Y remove 2nd dummy  ·  " : "Y add a 2nd dummy  ·  ")
                + (showChecklist ? "L hide move list" : "L show move list"), right, new Color(1f, 1f, 1f, 0.7f));
            y += 22 * S;
            Shadowed(new Rect(x, y, w, 22 * S), "K sparring (they fight back): " + (GM.PracticeSparring ? "ON" : "OFF") + "  ·  J give them weapons",
                right, GM.PracticeSparring ? new Color(1f, 0.6f, 0.4f) : new Color(1f, 1f, 1f, 0.7f));
            y += 26 * S;
            if (GM.PracticeStringDamage > 0f)
            {
                Shadowed(new Rect(x, y, w, 24 * S), $"Last hit {GM.PracticeLastHit:0}   ·   String damage {GM.PracticeStringDamage:0}", right, new Color(1f, 0.85f, 0.5f));
                y += 26 * S;
            }
            if (!string.IsNullOrEmpty(GM.PracticeLastMove))
            {
                Shadowed(new Rect(x, y, w, 24 * S), "Last move: " + GM.PracticeLastMove, right, new Color(0.6f, 1f, 0.7f));
                y += 26 * S;
            }

            if (showHelp || !showChecklist) return;   // the controls panel is showing instead, or it's folded away

            // Checklist on the left, under the health bars.
            int rows = PracticeMoves.GetLength(0);
            int done = 0, total = 0;
            for (int i = 0; i < rows; i++)
                if (PracticeMoves[i, 1] != "") { total++; if (GM.PracticeLanded.Contains(PracticeMoves[i, 0].ToUpperInvariant())) done++; }

            float lh = 19 * S, cx = 30 * S, cy = 150 * S, cw = 420 * S, gap = 24 * S;
            var rowStyle = new GUIStyle(small) { fontSize = Mathf.RoundToInt(15 * S) };
            var hintStyle = new GUIStyle(rowStyle) { alignment = TextAnchor.UpperRight };
            // Long list: two columns so it fits on the screen.
            int perColumn = Mathf.Max(8, Mathf.FloorToInt((Screen.height - cy - 30 * S) / lh) - 1);
            int columns = rows > perColumn ? 2 : 1;
            int rowsPerColumn = columns == 2 ? Mathf.CeilToInt(rows / 2f) : rows;
            Bar(new Rect(cx - 8 * S, cy - 8 * S, columns * cw + (columns - 1) * gap + 16 * S, (rowsPerColumn + 1) * lh + 16 * S), 1f, new Color(0f, 0f, 0f, 0.4f));
            Shadowed(new Rect(cx, cy, cw, lh), $"MOVES LANDED  {done} / {total}", new GUIStyle(rowStyle) { fontStyle = FontStyle.Bold }, Gold);
            for (int i = 0; i < rows; i++)
            {
                int col = i / rowsPerColumn;
                float colX = cx + col * (cw + gap);
                float ry = cy + (i - col * rowsPerColumn + 1) * lh;
                string name = PracticeMoves[i, 0], hint = PracticeMoves[i, 1];
                if (hint == "")
                {
                    Shadowed(new Rect(colX, ry, cw, lh), name, new GUIStyle(rowStyle) { fontStyle = FontStyle.Bold }, new Color(1f, 0.7f, 0.3f));
                    continue;
                }
                bool landed = GM.PracticeLanded.Contains(name.ToUpperInvariant());
                Color c = landed ? new Color(0.55f, 1f, 0.6f) : new Color(1f, 1f, 1f, 0.75f);
                float dot = 11 * S;
                HudIcons.Draw(new Rect(colX, ry + (lh - dot) / 2f, dot, dot), landed ? HudIcons.Disc : HudIcons.Ring, c);
                Shadowed(new Rect(colX + dot + 6 * S, ry, cw, lh), name, rowStyle, c);
                Shadowed(new Rect(colX, ry, cw, lh), hint, hintStyle, new Color(1f, 1f, 1f, landed ? 0.45f : 0.6f));
            }
        }

        void DrawHelp()
        {
            string[] lines =
            {
                "CONTROLS (keyboard + mouse)                   H to hide",
                "WASD move · Shift sprint · Space jump · C / Ctrl dodge (+direction)",
                "LMB punch · RMB heavy · hold RMB: CHARGED HAYMAKER (dazes, breaks guard)",
                "F block · tap F when the blue F shows = PARRY (late = PERFECT)",
                "ORANGE = unblockable, YELLOW = grab: dodge (C). Grabbed? tap E fast",
                "E grab collar → LMB/RMB combos · Shift drag fast · E throw · F shove",
                "Q (costs Gethu) · while holding: WRESTLING FINISHER · near a prop: smash",
                "",
                "COMBOS",
                "RMB ............... Haymaker            LMB RMB ........... Kasimedu Kick",
                "LMB RMB RMB RMB ... Kick > Shoulder Barge > Kasimedu Haymaker",
                "LMB LMB RMB RMB ... Uppercut > Air Hammer",
                "LMB x3 RMB ........ Spinning Backfist   LMB x4 RMB ........ Royapuram Rush",
                "back + RMB ........ Leg Sweep           forward + RMB (far) Shoulder Rush",
                "sprint + LMB ...... Running Knee        sprint + RMB ...... Clothesline",
                "LMB, rowdy behind . Back Elbow          dodge > LMB ....... Pivot Backfist",
                "heavy > RMB ....... Double Hammer       jump > LMB ........ Flying Kick",
                "Knocked into a wall: WALL BOUNCE (he staggers back dazed)",
                "dodge > RMB ....... Spinning Heel Kick  jump > RMB ........ Superman Punch",
                "S + LMB > LMB ..... Low kick > Kaal Odaippu (knee to the face)",
                "sprint + E ........ Rugby Tackle (LMB, RMB)",
                "",
                "COUNTERS (perfect parry = tap F as the blue F shows, then:)",
                "nothing ........... Counter Punch       LMB ............... Kai Odaippu (arm break)",
                "E ................. Counter Throw       Q (1 bar) ......... Counter Finisher",
                "parry a weapon .... Weapon Steal        E as he grabs ..... Grab Reversal",
                "held: tap E ....... Escape Strike       dodge charge > RMB  Matador",
                "two punch at once . Double Counter",
                "",
                "HOLDING HIM",
                "E, rowdy close .... Mutta Mothal        hold RMB .......... Arm Twist",
                "LMB then F ........ Ear Clap            S + F ............. Trip & Stomp",
                "SHIFT + E ......... Bowling Throw",
                "",
                "WRESTLING (Q, 1 Gethu bar; follow the ring prompts)",
                "hold, Q .............. Body Slam      hold, S+Q ......... Spin Drop",
                "hold from behind, Q .. German Suplex  hold at a wall, W+Q Spinebuster",
                "hold, SHIFT+Q (full) .. Powerbomb      sprint at a rowdy . Spear",
                "rowdy on the ground ... Elbow Drop     hold, A+Q ......... Choke-Out",
                "hold, W+Q (no wall) .. Thai Clinch    hold, D+Q (full) .. Giant Swing",
                "at the jetty edge, Q .. Kadal Throw    at an ice box, Q .. Ice Box Slam",
                "combos: NALLA > SEMMA > MASS > THALAIVAA",
            };
            float w = 760 * S, lh = 22 * S;
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

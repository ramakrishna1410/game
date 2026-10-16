using UnityEngine;

namespace MadrasGethu
{
    public enum SubtitleMode { TamilEnglish, TamilTanglish, English, Tanglish }

    /// <summary>
    /// Story screen: subtitles (Tamil script with English or Tanglish underneath), cinema bars, fades,
    /// chapter title cards, the objective, tutorial prompts and "E: talk".
    /// </summary>
    public class StoryUI : MonoBehaviour
    {
        const string ModeKey = "MadrasGethu.Subtitles";

        static int cachedMode = -1;

        public static SubtitleMode Mode
        {
            get
            {
                if (cachedMode < 0)
                {
                    try { cachedMode = Mathf.Clamp(PlayerPrefs.GetInt(ModeKey, 0), 0, 3); }
                    catch { cachedMode = 0; }
                }
                return (SubtitleMode)cachedMode;
            }
            set
            {
                cachedMode = (int)value;
                try { PlayerPrefs.SetInt(ModeKey, cachedMode); PlayerPrefs.Save(); }
                catch { }
            }
        }

        // The Tamil font has no English letters, so these are written in English.
        static readonly string[] ModeNames =
        {
            "Tamil + English", "Tamil + Tanglish", "English only", "Tanglish only",
        };

        // What's on screen (set by StoryDirector)
        public StoryEntry Line;
        public float LineShownAt;
        public bool ShowNextHint;
        public StoryEntry Bark;
        public float BarkUntil;
        public string ObjectiveTamil, ObjectiveEnglish;
        public string TutorialKey, TutorialTamil, TutorialEnglish;
        public float TutorialShownAt;
        public string TalkHint;            // "E" prompt over someone you can talk to
        public Vector3 TalkWorldPosition;
        public bool Letterbox;
        public float Fade;                 // 0 clear .. 1 black
        public string CardTamil, CardEnglish, CardSmall;
        public float CardAlpha;
        public string Banner, BannerSmall;
        public bool ShowSkipHint;

        float bars;
        float modeToastUntil;
        Font tamil, tamilBold;
        GUIStyle tamilLine, tamilName, latinLine, latinName, small, key, cardTamil, cardLatin, objective;
        float lastScale;

        float S => Screen.height / 1080f;

        void Start()
        {
            tamil = Resources.Load<Font>("Fonts/NotoSansTamil-Regular");
            tamilBold = Resources.Load<Font>("Fonts/NotoSansTamil-Bold");
            if (tamil == null) Debug.LogWarning("[Madras Gethu] Tamil font missing: copy Assets/MadrasGethu/Resources/Fonts too.");
        }

        void Update()
        {
            bars = Mathf.MoveTowards(bars, Letterbox ? 1f : 0f, Time.unscaledDeltaTime * 2.5f);
            if (GameInput.SubtitleModePressed)
            {
                Mode = (SubtitleMode)(((int)Mode + 1) % 4);
                modeToastUntil = Time.unscaledTime + 2f;
            }
        }

        void EnsureStyles()
        {
            if (tamilLine != null && Mathf.Approximately(lastScale, S)) return;
            lastScale = S;
            tamilLine = new GUIStyle(GUI.skin.label) { font = tamil, fontSize = Mathf.RoundToInt(34 * S), alignment = TextAnchor.UpperCenter, wordWrap = true };
            tamilLine.normal.textColor = Color.white;
            tamilName = new GUIStyle(tamilLine) { font = tamilBold != null ? tamilBold : tamil, fontSize = Mathf.RoundToInt(25 * S), wordWrap = false };
            latinLine = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(25 * S), alignment = TextAnchor.UpperCenter, wordWrap = true, fontStyle = FontStyle.Italic };
            latinLine.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            latinName = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(20 * S), fontStyle = FontStyle.Bold };
            small = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(17 * S) };
            small.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
            key = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(22 * S), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            key.normal.textColor = Color.black;
            cardTamil = new GUIStyle(tamilLine) { font = tamilBold != null ? tamilBold : tamil, fontSize = Mathf.RoundToInt(78 * S), alignment = TextAnchor.MiddleCenter };
            cardLatin = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(34 * S), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            objective = new GUIStyle(GUI.skin.label) { font = tamil, fontSize = Mathf.RoundToInt(22 * S), alignment = TextAnchor.UpperRight };
        }

        void OnGUI()
        {
            EnsureStyles();
            GUI.depth = -10;
            float w = Screen.width, h = Screen.height;

            // Cinema bars
            if (bars > 0f)
            {
                float bh = h * 0.11f * Mathf.SmoothStep(0f, 1f, bars);
                Rect(new Rect(0, 0, w, bh), Color.black);
                Rect(new Rect(0, h - bh, w, bh), Color.black);
            }

            if (!string.IsNullOrEmpty(ObjectiveEnglish) && !Letterbox) DrawObjective(w);
            if (!string.IsNullOrEmpty(TutorialEnglish) && !Letterbox) DrawTutorial(w, h);
            if (!string.IsNullOrEmpty(TalkHint) && !Letterbox) DrawTalkHint();

            if (Line != null) DrawSubtitle(Line, w, h, true);
            else if (Bark != null && Time.unscaledTime < BarkUntil) DrawSubtitle(Bark, w, h, false);

            if (ShowSkipHint && Letterbox)
                GUI.Label(new Rect(w - 360 * S, h - 34 * S, 340 * S, 30 * S), "Space: next   ·   Tab: skip   ·   F2: subtitles", small);

            if (Fade > 0.001f) Rect(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, Fade));

            if (CardAlpha > 0.001f) DrawCard(w, h);
            if (!string.IsNullOrEmpty(Banner)) DrawBanner(w, h);

            if (Time.unscaledTime < modeToastUntil)
            {
                var st = new GUIStyle(latinName) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(26 * S) };
                Shadowed(new Rect(0, h * 0.15f, w, 40 * S), "Subtitles: " + ModeNames[(int)Mode], st, Color.white);
            }
        }

        void DrawSubtitle(StoryEntry e, float w, float h, bool full)
        {
            var who = Cast.Get(e.speaker);
            var mode = Mode;
            bool showTamil = mode == SubtitleMode.TamilEnglish || mode == SubtitleMode.TamilTanglish;
            string second = mode == SubtitleMode.TamilEnglish || mode == SubtitleMode.English ? e.english : e.tanglish;

            float boxW = Mathf.Min(w * 0.72f, 1300 * S);
            float x = (w - boxW) * 0.5f;
            float bottom = h - (full ? h * 0.11f + 14 * S : 120 * S);
            var mainStyle = showTamil ? tamilLine : new GUIStyle(latinLine) { fontStyle = FontStyle.Normal, fontSize = Mathf.RoundToInt(30 * S) };
            string main = showTamil ? e.tamil : second;
            if (!full) mainStyle = new GUIStyle(mainStyle) { fontSize = Mathf.RoundToInt(mainStyle.fontSize * 0.8f) };
            float mainH = mainStyle.CalcHeight(new GUIContent(main), boxW);
            float secondH = showTamil ? latinLine.CalcHeight(new GUIContent(second), boxW) : 0f;
            float nameH = 32 * S;
            float total = nameH + mainH + secondH + 10 * S;
            float y = bottom - total;

            if (full) Rect(new Rect(x - 20 * S, y - 10 * S, boxW + 40 * S, total + 20 * S), new Color(0f, 0f, 0f, 0.45f));

            // Speaker: Tamil name in its colour, English name after it.
            if (showTamil)
            {
                var tn = new GUIContent(who.tamil);
                var ln = new GUIContent("  " + who.english.ToUpperInvariant() + (e.offScreen ? "  (phone)" : ""));
                float tw = tamilName.CalcSize(tn).x, lw = latinName.CalcSize(ln).x;
                float nx = (w - tw - lw) * 0.5f;
                Shadowed(new Rect(nx, y, tw + 4, nameH), who.tamil, new GUIStyle(tamilName) { alignment = TextAnchor.UpperLeft }, who.nameColor);
                Shadowed(new Rect(nx + tw, y + 4 * S, lw + 4, nameH), ln.text, latinName, who.nameColor);
            }
            else
            {
                var st = new GUIStyle(latinName) { alignment = TextAnchor.UpperCenter };
                Shadowed(new Rect(0, y + 4 * S, w, nameH), who.english.ToUpperInvariant() + (e.offScreen ? "  (phone)" : ""), st, who.nameColor);
            }
            y += nameH;
            Shadowed(new Rect(x, y, boxW, mainH), main, mainStyle, Color.white);
            y += mainH;
            if (showTamil) Shadowed(new Rect(x, y, boxW, secondH), second, latinLine, new Color(0.85f, 0.85f, 0.85f));

            if (full && ShowNextHint && Time.unscaledTime - LineShownAt > 0.6f)
            {
                float a = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
                GUI.color = new Color(1f, 1f, 1f, a);
                GUI.Label(new Rect(x + boxW - 30 * S, bottom - 26 * S, 30 * S, 26 * S), ">>", new GUIStyle(small) { fontStyle = FontStyle.Bold });
                GUI.color = Color.white;
            }
        }

        void DrawObjective(float w)
        {
            float x = w - 640 * S, y = 30 * S;
            var head = new GUIStyle(small) { alignment = TextAnchor.UpperRight, fontStyle = FontStyle.Bold };
            Shadowed(new Rect(x, y, 610 * S, 24 * S), "OBJECTIVE", head, new Color(1f, 0.78f, 0.2f));
            y += 24 * S;
            if (Mode != SubtitleMode.English && Mode != SubtitleMode.Tanglish && !string.IsNullOrEmpty(ObjectiveTamil))
            {
                Shadowed(new Rect(x, y, 610 * S, 34 * S), ObjectiveTamil, objective, Color.white);
                y += 32 * S;
            }
            var en = new GUIStyle(small) { alignment = TextAnchor.UpperRight, fontSize = Mathf.RoundToInt(19 * S) };
            Shadowed(new Rect(x, y, 610 * S, 28 * S), ObjectiveEnglish, en, new Color(0.9f, 0.9f, 0.9f));
        }

        void DrawTutorial(float w, float h)
        {
            float pop = Mathf.Clamp01((Time.unscaledTime - TutorialShownAt) * 4f);
            float bw = 760 * S, bh = 92 * S;
            float x = (w - bw) * 0.5f, y = h * 0.66f - (1f - pop) * 20 * S;
            Rect(new Rect(x, y, bw, bh), new Color(0f, 0f, 0f, 0.6f * pop));
            Rect(new Rect(x, y, 6 * S, bh), new Color(1f, 0.78f, 0.2f, pop));

            float kw = Mathf.Max(64 * S, key.CalcSize(new GUIContent(TutorialKey)).x + 24 * S);
            Rect(new Rect(x + 24 * S, y + (bh - 48 * S) * 0.5f, kw, 48 * S), new Color(1f, 0.85f, 0.35f, pop));
            GUI.Label(new Rect(x + 24 * S, y + (bh - 48 * S) * 0.5f, kw, 48 * S), TutorialKey, key);

            float tx = x + 24 * S + kw + 20 * S, tw = bw - (tx - x) - 16 * S;
            bool tamilOn = Mode != SubtitleMode.English && Mode != SubtitleMode.Tanglish;
            if (tamilOn)
            {
                var ts = new GUIStyle(tamilName) { alignment = TextAnchor.UpperLeft, fontSize = Mathf.RoundToInt(26 * S) };
                Shadowed(new Rect(tx, y + 10 * S, tw, 38 * S), TutorialTamil, ts, Color.white);
            }
            var es = new GUIStyle(small) { fontSize = Mathf.RoundToInt(21 * S), fontStyle = FontStyle.Bold };
            Shadowed(new Rect(tx, y + (tamilOn ? 50 : 32) * S, tw, 30 * S), TutorialEnglish, es, new Color(0.95f, 0.95f, 0.95f));
        }

        void DrawTalkHint()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(TalkWorldPosition);
            if (sp.z < 0f) return;
            float x = sp.x, y = Screen.height - sp.y;
            Rect(new Rect(x - 18 * S, y - 18 * S, 36 * S, 36 * S), new Color(1f, 0.85f, 0.35f));
            GUI.Label(new Rect(x - 18 * S, y - 18 * S, 36 * S, 36 * S), "E", key);
            bool tamilOn = Mode != SubtitleMode.English && Mode != SubtitleMode.Tanglish;
            float tx = x + 24 * S;
            if (tamilOn)
            {
                var st = new GUIStyle(tamilName) { alignment = TextAnchor.MiddleLeft, fontSize = Mathf.RoundToInt(22 * S) };
                Shadowed(new Rect(tx, y - 18 * S, 200 * S, 36 * S), TalkHint, st, Color.white);
                tx += st.CalcSize(new GUIContent(TalkHint)).x + 10 * S;
            }
            var ls = new GUIStyle(latinName) { alignment = TextAnchor.MiddleLeft, fontSize = Mathf.RoundToInt(20 * S) };
            Shadowed(new Rect(tx, y - 18 * S, 200 * S, 36 * S), "Talk", ls, Color.white);
        }

        void DrawCard(float w, float h)
        {
            Color c = new Color(1f, 1f, 1f, CardAlpha);
            bool tamilOn = Mode != SubtitleMode.English && Mode != SubtitleMode.Tanglish;
            float y = h * 0.38f;
            if (tamilOn && !string.IsNullOrEmpty(CardTamil))
            {
                Shadowed(new Rect(0, y, w, 110 * S), CardTamil, cardTamil, new Color(1f, 0.82f, 0.35f, CardAlpha));
                y += 110 * S;
            }
            Shadowed(new Rect(0, y, w, 50 * S), CardEnglish, cardLatin, c);
            if (!string.IsNullOrEmpty(CardSmall))
                Shadowed(new Rect(0, y + 54 * S, w, 34 * S), CardSmall, new GUIStyle(cardLatin) { fontSize = Mathf.RoundToInt(22 * S), fontStyle = FontStyle.Normal }, new Color(0.85f, 0.85f, 0.85f, CardAlpha));
        }

        void DrawBanner(float w, float h)
        {
            Rect(new Rect(0, h * 0.4f, w, 150 * S), new Color(0f, 0f, 0f, 0.6f));
            Shadowed(new Rect(0, h * 0.4f + 14 * S, w, 70 * S), Banner, new GUIStyle(cardLatin) { fontSize = Mathf.RoundToInt(56 * S) }, new Color(0.95f, 0.25f, 0.2f));
            if (!string.IsNullOrEmpty(BannerSmall))
                Shadowed(new Rect(0, h * 0.4f + 90 * S, w, 40 * S), BannerSmall, new GUIStyle(cardLatin) { fontSize = Mathf.RoundToInt(24 * S), fontStyle = FontStyle.Normal }, Color.white);
        }

        static void Rect(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static void Shadowed(Rect r, string text, GUIStyle style, Color color)
        {
            var old = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, color.a * 0.85f);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
            style.normal.textColor = old;
        }
    }
}

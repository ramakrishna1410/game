using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace MadrasGethu
{
    /// <summary>
    /// One-screen check of which Unity text path draws Tamil correctly, before the dialogue
    /// system is built on one of them. Shows the same three lines four ways:
    ///   A  the old HUD path (IMGUI / OnGUI)
    ///   B  UI Toolkit, standard text generator
    ///   C  UI Toolkit, advanced text generator (HarfBuzz shaping)
    ///   D  reference image rendered outside Unity with proper shaping (what it should look like)
    /// Menu: Madras Gethu > Tamil Text Test.
    /// </summary>
    public class TamilTextTest : MonoBehaviour
    {
        public static readonly string[] Lines =
        {
            "மாறன், செல்வி, கயல், குமரன் அண்ணன்",
            "இந்த கடை இட்லி சாப்பிட்டு வளர்ந்தவன் டா.",
            "கொ கோ கௌ க்ஷ ஸ்ரீ ஷ்ண க்ஷ்",
        };

        const int FontSize = 34;
        const float LineGap = 56f;

        Font font;
        Texture2D reference;
        GUIStyle tamilStyle, headStyle;
        string toolkitStatus = "", advancedStatus = "";
        UIDocument doc;

        float RowHeight => Screen.height / 4f;

        void Start()
        {
            if (Camera.main == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                var cam = go.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.1f, 0.11f, 0.16f);
            }

            font = Resources.Load<Font>("Fonts/NotoSansTamil-Regular");
            reference = Resources.Load<Texture2D>("Fonts/TamilReference");
            BuildToolkitRows();
        }

        void BuildToolkitRows()
        {
            if (font == null) { toolkitStatus = "font missing: copy Resources/Fonts too"; return; }
            try
            {
                var fontAsset = UnityEngine.TextCore.Text.FontAsset.CreateFontAsset(font);
                var panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.scaleMode = PanelScaleMode.ConstantPixelSize;
                panel.scale = 1f;
                doc = gameObject.AddComponent<UIDocument>();
                doc.panelSettings = panel;

                var root = doc.rootVisualElement;
                for (int i = 0; i < Lines.Length; i++)
                {
                    root.Add(MakeLabel(Lines[i], fontAsset, RowHeight * 1f + 40f + i * LineGap));
                    var adv = MakeLabel(Lines[i], fontAsset, RowHeight * 2f + 40f + i * LineGap);
                    advancedStatus = UseAdvancedGenerator(adv);
                    root.Add(adv);
                }
                toolkitStatus = "ok";
            }
            catch (Exception e)
            {
                toolkitStatus = "failed: " + e.Message;
                Debug.LogException(e);
            }
        }

        static Label MakeLabel(string text, UnityEngine.TextCore.Text.FontAsset fontAsset, float top)
        {
            var l = new Label(text);
            l.style.position = Position.Absolute;
            l.style.left = 24f;
            l.style.top = top;
            l.style.fontSize = FontSize;
            l.style.color = Color.white;
            l.style.unityFontDefinition = FontDefinition.FromSDFFont(fontAsset);
            return l;
        }

        // style.unityTextGenerator only exists in newer Unity 6 versions, so set it by reflection.
        static string UseAdvancedGenerator(VisualElement e)
        {
            var prop = typeof(IStyle).GetProperty("unityTextGenerator");
            if (prop == null) return "this Unity has no advanced text generator";
            try
            {
                var styleType = prop.PropertyType;
                var enumType = styleType.GetGenericArguments()[0];
                var value = Activator.CreateInstance(styleType, Enum.Parse(enumType, "Advanced"));
                prop.SetValue(e.style, value);
                return "advanced generator on";
            }
            catch (Exception ex)
            {
                return "could not switch on: " + ex.Message;
            }
        }

        void OnGUI()
        {
            if (headStyle == null)
            {
                headStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
                headStyle.normal.textColor = new Color(1f, 0.75f, 0.3f);
                tamilStyle = new GUIStyle(GUI.skin.label) { fontSize = FontSize, font = font };
                tamilStyle.normal.textColor = Color.white;
            }

            float h = RowHeight;
            GUI.Label(new Rect(24, 6, 1200, 30), "A   Old HUD text (IMGUI)" + (font == null ? "  (font missing!)" : ""), headStyle);
            for (int i = 0; i < Lines.Length; i++)
                GUI.Label(new Rect(24, 40 + i * LineGap, 1400, LineGap), Lines[i], tamilStyle);

            GUI.Label(new Rect(24, h + 6, 1200, 30), "B   UI Toolkit, standard   [" + toolkitStatus + "]", headStyle);
            GUI.Label(new Rect(24, h * 2 + 6, 1200, 30), "C   UI Toolkit, advanced   [" + advancedStatus + "]", headStyle);
            GUI.Label(new Rect(24, h * 3 + 6, 1200, 30), "D   Correct Tamil (reference picture)", headStyle);
            if (reference != null)
                GUI.DrawTexture(new Rect(12, h * 3 + 32, reference.width, reference.height), reference);

            GUI.Label(new Rect(Screen.width - 520, Screen.height - 34, 510, 30),
                "Which of A / B / C matches D? Screenshot this.", headStyle);
        }
    }
}

using UnityEditor;
using UnityEngine;

namespace MadrasGethu.EditorTools
{
    /// <summary>A scrollable window for tool reports (Unity's popup dialogs cut long text short).</summary>
    public class ReportWindow : EditorWindow
    {
        string text;
        Vector2 scroll;

        public static void Show(string title, string report)
        {
            var window = GetWindow<ReportWindow>(true, "Madras Gethu: " + title);
            window.text = report;
            window.minSize = new Vector2(560f, 420f);
            window.Show();
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            var style = new GUIStyle(EditorStyles.label) { wordWrap = true, richText = false, fontSize = 13 };
            EditorGUILayout.SelectableLabel(text ?? "", style, GUILayout.ExpandHeight(true),
                GUILayout.MinHeight(style.CalcHeight(new GUIContent(text ?? ""), position.width - 30f)));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy to clipboard", GUILayout.Height(28f)))
                EditorGUIUtility.systemCopyBuffer = text;
            if (GUILayout.Button("Close", GUILayout.Height(28f)))
                Close();
            EditorGUILayout.EndHorizontal();
        }
    }
}

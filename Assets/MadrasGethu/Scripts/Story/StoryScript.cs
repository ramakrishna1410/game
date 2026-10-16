using System.Collections.Generic;
using UnityEngine;

namespace MadrasGethu
{
    /// <summary>One entry of a scene: a spoken line, or a marker where the game does something.</summary>
    public class StoryEntry
    {
        public string speaker;      // cast id; null for a marker
        public bool offScreen;      // "settu~": heard, not seen (phone, voice)
        public string[] gestures;   // talk / point / wave / none ...
        public string tamil, tanglish, english;
        public string marker;       // "@tray" -> "tray"
        public string markerArg;    // "@pause 1.5" -> "1.5"
        public int lineNumber;      // 1-based count of spoken lines in the scene (for voice files)

        public bool IsMarker => marker != null;
        public bool Has(string gesture) => gestures != null && System.Array.IndexOf(gestures, gesture) >= 0;
    }

    /// <summary>
    /// Reads a chapter's dialogue from Resources/Story/&lt;name&gt;.txt (see the header of ch01.txt for the format).
    /// Keeping the words in a text file means they can be corrected without touching code.
    /// </summary>
    public class StoryScript
    {
        public readonly string chapter;
        readonly Dictionary<string, List<StoryEntry>> scenes = new Dictionary<string, List<StoryEntry>>();

        StoryScript(string chapter) { this.chapter = chapter; }

        public static StoryScript Load(string chapter)
        {
            var script = new StoryScript(chapter);
            var asset = Resources.Load<TextAsset>("Story/" + chapter);
            if (asset == null)
            {
                Debug.LogWarning("[Madras Gethu] Story file missing: Resources/Story/" + chapter + ".txt (copy the Resources folder too).");
                return script;
            }
            script.Parse(asset.text);
            return script;
        }

        void Parse(string text)
        {
            List<StoryEntry> current = null;
            int spoken = 0;
            var lines = text.Replace("\r", "").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i].Trim();
                if (raw.Length == 0 || raw.StartsWith("#")) continue;

                if (raw.StartsWith("=="))
                {
                    current = new List<StoryEntry>();
                    spoken = 0;
                    scenes[raw.Substring(2).Trim()] = current;
                    continue;
                }
                if (current == null) continue;

                if (raw.StartsWith("@"))
                {
                    string body = raw.Substring(1).Trim();
                    int space = body.IndexOf(' ');
                    current.Add(new StoryEntry
                    {
                        marker = space < 0 ? body : body.Substring(0, space),
                        markerArg = space < 0 ? null : body.Substring(space + 1).Trim(),
                    });
                    continue;
                }

                var cols = raw.Split('|');
                if (cols.Length < 5)
                {
                    Debug.LogWarning($"[Madras Gethu] {chapter}.txt line {i + 1}: needs 5 columns (speaker | gesture | Tamil | Tanglish | English).");
                    continue;
                }
                string speaker = cols[0].Trim().ToLowerInvariant();
                bool off = speaker.EndsWith("~");
                if (off) speaker = speaker.TrimEnd('~');
                spoken++;
                current.Add(new StoryEntry
                {
                    speaker = speaker,
                    offScreen = off,
                    gestures = cols[1].Trim().ToLowerInvariant().Split(','),
                    tamil = cols[2].Trim(),
                    tanglish = cols[3].Trim(),
                    english = string.Join("|", cols, 4, cols.Length - 4).Trim(),
                    lineNumber = spoken,
                });
            }
        }

        public List<StoryEntry> Scene(string id)
        {
            if (scenes.TryGetValue(id, out var s)) return s;
            Debug.LogWarning($"[Madras Gethu] Scene '{id}' not found in {chapter}.txt");
            return new List<StoryEntry>();
        }

        public bool HasScene(string id) => scenes.ContainsKey(id);
    }
}

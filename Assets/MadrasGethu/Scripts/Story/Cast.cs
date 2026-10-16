using System.Collections.Generic;
using UnityEngine;

namespace MadrasGethu
{
    /// <summary>Who's who in the story: names in Tamil and English, subtitle colour and placeholder look.</summary>
    public class CastMember
    {
        public string id;
        public string tamil;
        public string english;
        public Color nameColor;
        public Color shirt, pants, accent;
        public bool female;
        public bool moustache = true;
        public float scale = 1f;
    }

    public static class Cast
    {
        static readonly Dictionary<string, CastMember> members = new Dictionary<string, CastMember>();

        static Cast()
        {
            Add("maaran", "மாறன்", "Maaran", new Color(1f, 0.82f, 0.35f), new Color(0.08f, 0.08f, 0.1f), new Color(0.2f, 0.3f, 0.6f), new Color(0.75f, 0.1f, 0.1f));
            Add("kutty", "குட்டி", "Kutty", new Color(0.55f, 0.9f, 1f), new Color(0.85f, 0.75f, 0.3f), new Color(0.15f, 0.15f, 0.2f), new Color(0.9f, 0.9f, 0.9f), scale: 0.95f);
            Add("selvi", "செல்வி", "Selvi", new Color(1f, 0.6f, 0.75f), new Color(0.65f, 0.15f, 0.3f), new Color(0.65f, 0.15f, 0.3f), new Color(0.95f, 0.75f, 0.2f), female: true, scale: 0.92f);
            Add("kayal", "கயல்", "Kayal", new Color(0.75f, 1f, 0.7f), new Color(0.95f, 0.95f, 1f), new Color(0.2f, 0.45f, 0.75f), new Color(0.2f, 0.45f, 0.75f), female: true, scale: 0.92f);
            Add("ilango", "இளங்கோ", "Inspector Ilango", new Color(0.85f, 0.8f, 0.6f), new Color(0.75f, 0.62f, 0.4f), new Color(0.75f, 0.62f, 0.4f), new Color(0.3f, 0.2f, 0.1f));
            Add("pallu", "பல்லு", "Pallu", new Color(1f, 0.45f, 0.4f), new Color(0.75f, 0.2f, 0.2f), new Color(0.15f, 0.15f, 0.15f), new Color(0.95f, 0.95f, 0.95f));
            Add("settu", "செட்டு", "Settu", new Color(1f, 0.35f, 0.3f), new Color(0.95f, 0.95f, 0.95f), new Color(0.95f, 0.95f, 0.95f), new Color(0.85f, 0.65f, 0.1f));
            Add("muniyandi", "முனியாண்டி", "Muniyandi", new Color(0.8f, 0.85f, 0.95f), new Color(0.6f, 0.55f, 0.45f), new Color(0.3f, 0.45f, 0.4f), new Color(0.9f, 0.9f, 0.85f));
            Add("paati", "பாட்டி", "Paati", new Color(0.95f, 0.8f, 0.9f), new Color(0.3f, 0.5f, 0.35f), new Color(0.3f, 0.5f, 0.35f), new Color(0.8f, 0.3f, 0.2f), female: true, scale: 0.85f);
            Add("boy", "சிறுவன்", "Boy", new Color(0.9f, 0.95f, 0.6f), new Color(0.9f, 0.5f, 0.1f), new Color(0.2f, 0.25f, 0.4f), new Color(0.9f, 0.9f, 0.9f), scale: 0.7f, moustache: false);
            Add("rowdy", "ரௌடி", "Rowdy", new Color(1f, 0.5f, 0.45f), Color.gray, Color.gray, Color.black);
            Add("announcer", "அறிவிப்பாளர்", "Announcer", new Color(0.9f, 0.9f, 0.9f), Color.gray, Color.gray, Color.black);
        }

        static void Add(string id, string tamil, string english, Color name, Color shirt, Color pants, Color accent,
                        bool female = false, float scale = 1f, bool moustache = true)
        {
            members[id] = new CastMember
            {
                id = id, tamil = tamil, english = english, nameColor = name,
                shirt = shirt, pants = pants, accent = accent,
                female = female, moustache = moustache && !female, scale = scale,
            };
        }

        public static CastMember Get(string id)
        {
            if (id != null && members.TryGetValue(id, out var m)) return m;
            return new CastMember { id = id, tamil = id, english = id, nameColor = Color.white, shirt = Color.gray, pants = Color.gray, accent = Color.black };
        }
    }
}

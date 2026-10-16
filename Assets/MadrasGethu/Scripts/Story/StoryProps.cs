using UnityEngine;

namespace MadrasGethu
{
    /// <summary>
    /// Story set dressing for the Kasimedu lane: Kutty's auto, Ilango's jeep, Kumaran's billboard,
    /// the protest wall, Amma's shop sign, Velu's photo and the boxing club's neon across the water.
    /// Pictures with Tamil text come from Resources/Story/Art (drawn by tools/story_art.py).
    /// </summary>
    public static class StoryProps
    {
        static Texture2D Art(string name) => Resources.Load<Texture2D>("Story/Art/" + name);

        /// <summary>A flat picture facing the given direction (e.g. Vector3.right faces the road from the godown side).</summary>
        public static GameObject Picture(Transform root, string art, Vector3 position, Vector3 facing, Vector2 size, Color glow = default)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Picture " + art;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.LookRotation(-facing);   // a quad shows its face towards -forward
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var tex = Art(art);
            go.GetComponent<Renderer>().sharedMaterial = tex != null ? Palette.Textured(tex, glow) : Palette.Get(new Color(0.5f, 0.5f, 0.5f));
            return go;
        }

        /// <summary>Kutty's auto: drives in the opening. Returns its root (the front points along +Z).</summary>
        public static Transform Auto(Transform root, Vector3 position, float yaw)
        {
            var t = new GameObject("Kutty's Auto").transform;
            t.SetParent(root, false);
            t.position = position;
            t.rotation = Quaternion.Euler(0f, yaw, 0f);
            Color yellow = new Color(0.98f, 0.8f, 0.1f);
            Color black = new Color(0.07f, 0.07f, 0.07f);
            Color green = new Color(0.1f, 0.45f, 0.2f);
            Palette.Primitive(PrimitiveType.Cube, t, new Vector3(0f, 0.75f, -0.2f), new Vector3(1.3f, 0.9f, 1.6f), green, collider: false, name: "Body");
            Palette.Primitive(PrimitiveType.Cube, t, new Vector3(0f, 0.7f, 0.9f), new Vector3(0.8f, 0.8f, 0.7f), green, collider: false, name: "Nose");
            Palette.Primitive(PrimitiveType.Cube, t, new Vector3(0f, 1.75f, -0.1f), new Vector3(1.35f, 0.08f, 2.2f), yellow, collider: false, name: "Canopy");
            Palette.Primitive(PrimitiveType.Cube, t, new Vector3(0f, 1.4f, 1.15f), new Vector3(0.8f, 0.5f, 0.04f), new Color(0.4f, 0.55f, 0.6f), collider: false, name: "Windshield");
            Palette.Primitive(PrimitiveType.Cylinder, t, new Vector3(0f, 0.25f, 1.25f), new Vector3(0.45f, 0.06f, 0.45f), black, collider: false, name: "Front Wheel")
                .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            for (int s = -1; s <= 1; s += 2)
                Palette.Primitive(PrimitiveType.Cylinder, t, new Vector3(s * 0.62f, 0.25f, -0.6f), new Vector3(0.45f, 0.06f, 0.45f), black, collider: false, name: "Rear Wheel")
                    .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            // "Amman thunai / KUTTY" painted on the back hood
            var back = Picture(t, "auto_back", Vector3.zero, Vector3.back, new Vector2(1.3f, 0.65f));
            back.transform.localPosition = new Vector3(0f, 1.42f, -1.0f);
            back.transform.localRotation = Quaternion.LookRotation(Vector3.forward);
            return t;
        }

        /// <summary>Inspector Ilango's police jeep (white, with a light bar).</summary>
        public static Transform Jeep(Transform root, Vector3 position, float yaw)
        {
            var t = new GameObject("Police Jeep").transform;
            t.SetParent(root, false);
            t.position = position;
            t.rotation = Quaternion.Euler(0f, yaw, 0f);
            Color white = new Color(0.9f, 0.9f, 0.88f);
            Color glass = new Color(0.15f, 0.2f, 0.25f);
            Color black = new Color(0.07f, 0.07f, 0.07f);
            Palette.Primitive(PrimitiveType.Cube, t, new Vector3(0f, 0.85f, 0f), new Vector3(1.8f, 0.9f, 4.2f), white, collider: false, name: "Body");
            Palette.Primitive(PrimitiveType.Cube, t, new Vector3(0f, 1.6f, -0.4f), new Vector3(1.7f, 0.7f, 2.6f), white, collider: false, name: "Cabin");
            Palette.Primitive(PrimitiveType.Cube, t, new Vector3(0f, 1.62f, 0.92f), new Vector3(1.6f, 0.55f, 0.04f), glass, collider: false, name: "Windscreen");
            Palette.Primitive(PrimitiveType.Cube, t, new Vector3(0f, 0.9f, 0f), new Vector3(1.82f, 0.18f, 4.22f), new Color(0.15f, 0.25f, 0.65f), collider: false, name: "Blue Band");
            Palette.Primitive(PrimitiveType.Cube, t, new Vector3(-0.35f, 2.02f, -0.2f), new Vector3(0.6f, 0.12f, 0.25f), new Color(0.9f, 0.1f, 0.1f), collider: false, name: "Light Red");
            Palette.Primitive(PrimitiveType.Cube, t, new Vector3(0.35f, 2.02f, -0.2f), new Vector3(0.6f, 0.12f, 0.25f), new Color(0.1f, 0.3f, 0.95f), collider: false, name: "Light Blue");
            foreach (var p in new[] { new Vector3(-0.85f, 0.38f, 1.4f), new Vector3(0.85f, 0.38f, 1.4f), new Vector3(-0.85f, 0.38f, -1.4f), new Vector3(0.85f, 0.38f, -1.4f) })
                Palette.Primitive(PrimitiveType.Cylinder, t, p, new Vector3(0.7f, 0.12f, 0.7f), black, collider: false, name: "Wheel")
                    .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            return t;
        }

        /// <summary>Everything that stays put in Chapter 1's lane.</summary>
        public static void DressKasimedu(Transform root, Vector3 kadai)
        {
            // Amma's sign and Velu's photo belong to the kadai, so they go when it's smashed in a fight.
            Transform shop = root;
            foreach (var spot in FinisherSpot.All)
                if (spot != null && spot.intactVisual != null && Vector3.Distance(spot.transform.position, kadai) < 1f)
                    shop = spot.intactVisual.transform;

            // Kumaran's banner on a party arch across the harbour road, facing the gate end of the lane.
            var arch = new GameObject("Kumaran Arch").transform;
            arch.SetParent(root, false);
            arch.position = new Vector3(0f, 0f, -8f);
            Color steel = new Color(0.3f, 0.3f, 0.32f);
            foreach (float x in new[] { -7.2f, 6.9f })
                Palette.Primitive(PrimitiveType.Cylinder, arch, new Vector3(x, 4.5f, 0.1f), new Vector3(0.2f, 4.5f, 0.2f), steel, collider: false, name: "Pole");
            Palette.Primitive(PrimitiveType.Cube, arch, new Vector3(-0.15f, 8.9f, 0.1f), new Vector3(14.3f, 0.2f, 0.2f), steel, collider: false, name: "Beam");
            Picture(arch, "billboard_kumaran", new Vector3(-0.15f, 6.8f, -8f), Vector3.back, new Vector2(8f, 4f));

            // "Our shore is ours" on a godown front, half whitewashed.
            Picture(root, "wall_slogan", new Vector3(-7.96f, 2.1f, -3.5f), Vector3.right, new Vector2(4.4f, 1.65f));

            // Amma's shop: name board on the tarp, and Velu's garlanded photo at the back.
            Picture(shop, "kadai_sign", kadai + new Vector3(0.95f, 2.15f, 0f), Vector3.right, new Vector2(2.6f, 0.5f));
            var photoBoard = Palette.Primitive(PrimitiveType.Cube, shop, Vector3.zero, new Vector3(0.06f, 1.5f, 1.2f), new Color(0.35f, 0.25f, 0.18f), collider: false, name: "Back Board");
            photoBoard.transform.position = kadai + new Vector3(-1.4f, 1.35f, 0.9f);
            Picture(shop, "velu_photo", kadai + new Vector3(-1.36f, 1.6f, 0.9f), Vector3.right, new Vector2(0.42f, 0.52f));
        }

        /// <summary>
        /// Opens up the gate end of the lane for the drive in: the road carries on into the morning haze
        /// (with a few buildings either side), and the police barricades at that end are taken away.
        /// </summary>
        public static void OpenGateRoad(Transform root, Transform arena)
        {
            Palette.Primitive(PrimitiveType.Cube, root, new Vector3(0f, -0.5f, -71f), new Vector3(16f, 1f, 68f), new Color(0.16f, 0.16f, 0.18f), name: "Harbour Road");
            Palette.Primitive(PrimitiveType.Cube, root, new Vector3(-6.5f, 0.06f, -71f), new Vector3(3f, 0.12f, 68f), new Color(0.35f, 0.33f, 0.31f), collider: false, name: "Footpath");
            Random.InitState(77);
            for (float z = -40f; z > -104f; z -= Random.Range(7f, 11f))
            {
                float hgt = Random.Range(4f, 9f);
                Palette.Primitive(PrimitiveType.Cube, root, new Vector3(-12f, hgt * 0.5f, z), new Vector3(8f, hgt, Random.Range(6f, 9.5f)),
                    Color.HSVToRGB(Random.value, 0.35f, 0.7f), collider: false, name: "Building");
            }
            Random.InitState(System.Environment.TickCount);
            if (arena == null) return;
            foreach (var t in arena.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("Barricade") && t.position.z < 0f) t.gameObject.SetActive(false);
        }

        /// <summary>The boxing club's neon sign across the harbour, seen from the jetty at night.</summary>
        public static GameObject NeonSign(Transform root)
        {
            var go = Picture(root, "neon_boxing", new Vector3(34f, 6f, 10f), Vector3.left, new Vector2(12f, 1.5f), new Color(1.6f, 1.6f, 1.6f));
            go.AddComponent<NeonFlicker>();
            var light = new GameObject("Neon Glow").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.transform.localPosition = new Vector3(0f, 0f, -0.5f);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.25f, 0.25f);
            light.range = 18f;
            light.intensity = 2f;
            return go;
        }
    }

    /// <summary>Old neon: mostly on, with the odd stutter.</summary>
    public class NeonFlicker : MonoBehaviour
    {
        Renderer rend;
        Light glow;
        float nextFlicker;

        void Start()
        {
            rend = GetComponent<Renderer>();
            glow = GetComponentInChildren<Light>();
            nextFlicker = Time.time + Random.Range(1.5f, 4f);
        }

        void Update()
        {
            bool off = Time.time >= nextFlicker && Time.time < nextFlicker + 0.12f;
            if (Time.time > nextFlicker + 0.12f) nextFlicker = Time.time + Random.Range(1.5f, 5f);
            if (rend != null) rend.enabled = !off;
            if (glow != null) glow.enabled = !off;
        }
    }
}

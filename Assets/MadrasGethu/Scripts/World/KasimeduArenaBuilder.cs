using UnityEngine;

namespace MadrasGethu
{
    /// <summary>
    /// Builds a night-time stretch of Kasimedu fishing harbour (North Chennai) out of placeholder blocks:
    /// a road between old godowns and the harbour edge, fishing boats, a tea kadai, fish crates,
    /// a parked auto and sodium street lamps. Replace pieces with real models as art arrives.
    ///
    /// The lane runs along Z. Godowns are on the left (-X), the harbour water on the right (+X).
    /// </summary>
    public static class KasimeduArenaBuilder
    {
        public const float HalfLength = 32f;

        static readonly Color Asphalt = new Color(0.16f, 0.16f, 0.18f);
        static readonly Color Footpath = new Color(0.35f, 0.33f, 0.31f);
        static readonly Color Concrete = new Color(0.45f, 0.44f, 0.42f);
        static readonly Color Water = new Color(0.04f, 0.12f, 0.16f);
        static readonly Color SodiumLight = new Color(1f, 0.68f, 0.35f);

        // Faded Chennai wall colours
        static readonly Color[] WallColors =
        {
            new Color(0.25f, 0.6f, 0.58f), new Color(0.85f, 0.55f, 0.6f), new Color(0.9f, 0.78f, 0.35f),
            new Color(0.55f, 0.7f, 0.3f), new Color(0.6f, 0.62f, 0.75f), new Color(0.85f, 0.45f, 0.25f),
        };

        public static void Build(Transform root)
        {
            Random.InitState(1947);
            SetupNightLighting(root);
            BuildGround(root);
            BuildGodowns(root);
            BuildHarbour(root);
            BuildStreetLamps(root);
            BuildProps(root);
            BuildBoundaries(root);
            Random.InitState(System.Environment.TickCount);   // fixed layout, but unpredictable fights
        }

        // ------------------------------------------------------------------ lighting

        static void SetupNightLighting(Transform root)
        {
            var moonGo = new GameObject("Moonlight");
            moonGo.transform.SetParent(root, false);
            moonGo.transform.rotation = Quaternion.Euler(38f, -35f, 0f);
            var moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.55f, 0.65f, 0.95f);
            moon.intensity = 0.8f;
            moon.shadows = LightShadows.Soft;

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.27f, 0.28f, 0.36f);

            // Soft cool back light so fighters stand out from the road (rim light).
            var rimGo = new GameObject("Rim Light");
            rimGo.transform.SetParent(root, false);
            rimGo.transform.rotation = Quaternion.Euler(25f, 150f, 0f);
            var rim = rimGo.AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.color = new Color(0.6f, 0.7f, 1f);
            rim.intensity = 0.45f;
            rim.shadows = LightShadows.None;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.05f, 0.06f, 0.1f);
            RenderSettings.fogDensity = 0.018f;
        }

        // ------------------------------------------------------------------ ground

        static void BuildGround(Transform root)
        {
            Palette.Primitive(PrimitiveType.Cube, root, new Vector3(0f, -0.5f, 0f), new Vector3(16f, 1f, HalfLength * 2f + 10f), Asphalt, name: "Road");
            Palette.Primitive(PrimitiveType.Cube, root, new Vector3(-6.5f, 0.06f, 0f), new Vector3(3f, 0.12f, HalfLength * 2f + 10f), Footpath, name: "Footpath");

            // Faded lane markings
            for (float z = -HalfLength; z < HalfLength; z += 6f)
                Palette.Primitive(PrimitiveType.Cube, root, new Vector3(0.5f, 0.005f, z), new Vector3(0.15f, 0.01f, 2.5f),
                    new Color(0.6f, 0.58f, 0.5f), collider: false, name: "Marking");

            // Puddles that catch the lamp light
            for (int i = 0; i < 8; i++)
            {
                var p = new Vector3(Random.Range(-4f, 5f), 0.004f, Random.Range(-HalfLength, HalfLength));
                Palette.Primitive(PrimitiveType.Cylinder, root, p, new Vector3(Random.Range(1f, 2.5f), 0.002f, Random.Range(0.8f, 2f)),
                    new Color(0.08f, 0.09f, 0.12f), collider: false, name: "Puddle");
            }
        }

        // ------------------------------------------------------------------ godowns (left side)

        static void BuildGodowns(Transform root)
        {
            float z = -HalfLength - 4f;
            int i = 0;
            while (z < HalfLength + 4f)
            {
                float width = Random.Range(7f, 12f);
                float height = Random.Range(4.5f, 9f);
                Color wall = WallColors[i % WallColors.Length];
                var building = new GameObject("Godown " + i).transform;
                building.SetParent(root, false);
                building.localPosition = new Vector3(-12f, 0f, z + width * 0.5f);

                Palette.Primitive(PrimitiveType.Cube, building, new Vector3(0f, height * 0.5f, 0f), new Vector3(8f, height, width - 0.2f), wall * 0.85f, name: "Wall");
                // Rolling shutter
                Palette.Primitive(PrimitiveType.Cube, building, new Vector3(4.02f, 1.6f, 0f), new Vector3(0.05f, 3.2f, Mathf.Min(4f, width - 2f)),
                    new Color(0.35f, 0.37f, 0.38f), collider: false, name: "Shutter");
                // Painted shop-name board in bright colours
                Color board = Color.HSVToRGB(Random.value, 0.75f, 0.85f);
                Palette.Primitive(PrimitiveType.Cube, building, new Vector3(4.03f, 3.7f, 0f), new Vector3(0.05f, 0.8f, width - 1.2f),
                    board, collider: false, name: "Name Board");
                // Weathering stripe at the bottom
                Palette.Primitive(PrimitiveType.Cube, building, new Vector3(4.01f, 0.3f, 0f), new Vector3(0.04f, 0.6f, width - 0.2f),
                    wall * 0.45f, collider: false, name: "Grime");
                // Cinema poster / party wall painting
                if (Random.value < 0.6f)
                {
                    var poster = new Vector3(4.04f, Random.Range(2f, 3f), Random.Range(-width * 0.3f, width * 0.3f));
                    Palette.Primitive(PrimitiveType.Cube, building, poster, new Vector3(0.03f, 1.4f, 1f),
                        Color.HSVToRGB(Random.value, 0.9f, 0.9f), collider: false, name: "Poster");
                }
                // Water tank on the roof, a Chennai rooftop staple
                if (Random.value < 0.5f)
                    Palette.Primitive(PrimitiveType.Cylinder, building, new Vector3(Random.Range(-2f, 2f), height + 0.6f, 0f),
                        new Vector3(1.2f, 0.6f, 1.2f), new Color(0.1f, 0.1f, 0.1f), collider: false, name: "Sintex Tank");

                z += width;
                i++;
            }
        }

        // ------------------------------------------------------------------ harbour (right side)

        static void BuildHarbour(Transform root)
        {
            Palette.Primitive(PrimitiveType.Cube, root, new Vector3(7.5f, -0.4f, 0f), new Vector3(1f, 1f, HalfLength * 2f + 10f), Concrete, name: "Dock Edge");
            var water = Palette.Primitive(PrimitiveType.Cube, root, new Vector3(30f, -1.4f, 0f), new Vector3(44f, 0.2f, 140f), Water, collider: false, name: "Harbour Water");
            water.GetComponent<Renderer>().sharedMaterial = Palette.Get(Water, new Color(0.01f, 0.03f, 0.05f));

            // Bollards along the edge
            for (float z = -HalfLength + 2f; z < HalfLength; z += 5f)
                Palette.Primitive(PrimitiveType.Cylinder, root, new Vector3(7.4f, 0.35f, z), new Vector3(0.35f, 0.35f, 0.35f),
                    new Color(0.2f, 0.2f, 0.22f), name: "Bollard");

            // Fishing boats (painted wooden hulls) moored in the harbour
            Color[] hulls =
            {
                new Color(0.15f, 0.35f, 0.7f), new Color(0.75f, 0.2f, 0.15f), new Color(0.95f, 0.75f, 0.2f),
                new Color(0.2f, 0.55f, 0.35f), new Color(0.9f, 0.9f, 0.9f),
            };
            for (int i = 0; i < 9; i++)
            {
                var boat = new GameObject("Fishing Boat " + i).transform;
                boat.SetParent(root, false);
                boat.localPosition = new Vector3(Random.Range(10f, 22f), -1.1f, -HalfLength + i * 8f + Random.Range(-1f, 1f));
                boat.localRotation = Quaternion.Euler(0f, Random.Range(-15f, 15f), Random.Range(-3f, 3f));
                Color hull = hulls[i % hulls.Length];
                Palette.Primitive(PrimitiveType.Cube, boat, new Vector3(0f, 0.5f, 0f), new Vector3(2.4f, 1f, 8f), hull, collider: false, name: "Hull");
                Palette.Primitive(PrimitiveType.Cube, boat, new Vector3(0f, 1.02f, 0f), new Vector3(2.5f, 0.08f, 8.1f), Color.white * 0.9f, collider: false, name: "Gunwale");
                Palette.Primitive(PrimitiveType.Cube, boat, new Vector3(0f, 1.6f, -2f), new Vector3(1.8f, 1.2f, 2f), new Color(0.3f, 0.2f, 0.12f), collider: false, name: "Cabin");
                Palette.Primitive(PrimitiveType.Cylinder, boat, new Vector3(0f, 3f, 1f), new Vector3(0.1f, 2.2f, 0.1f), new Color(0.3f, 0.25f, 0.2f), collider: false, name: "Mast");
                Palette.Primitive(PrimitiveType.Cube, boat, new Vector3(0f, 5f, 1.4f), new Vector3(0.02f, 0.5f, 0.8f),
                    Color.HSVToRGB(Random.value, 0.9f, 0.9f), collider: false, name: "Flag");
                if (Random.value < 0.5f)
                {
                    var lamp = new GameObject("Boat Lamp").AddComponent<Light>();
                    lamp.transform.SetParent(boat, false);
                    lamp.transform.localPosition = new Vector3(0f, 2.6f, -2f);
                    lamp.type = LightType.Point;
                    lamp.color = new Color(0.6f, 1f, 0.7f);
                    lamp.range = 6f;
                    lamp.intensity = 1.2f;
                }
            }
        }

        // ------------------------------------------------------------------ lamps

        static void BuildStreetLamps(Transform root)
        {
            for (float z = -HalfLength + 4f; z < HalfLength; z += 13f)
            {
                var lampRoot = new GameObject("Street Lamp").transform;
                lampRoot.SetParent(root, false);
                lampRoot.localPosition = new Vector3(-5.6f, 0f, z);
                Palette.Primitive(PrimitiveType.Cylinder, lampRoot, new Vector3(0f, 3f, 0f), new Vector3(0.15f, 3f, 0.15f), new Color(0.25f, 0.27f, 0.25f), name: "Pole");
                Palette.Primitive(PrimitiveType.Cube, lampRoot, new Vector3(0.9f, 5.9f, 0f), new Vector3(1.8f, 0.1f, 0.1f), new Color(0.25f, 0.27f, 0.25f), collider: false, name: "Arm");
                var head = Palette.Primitive(PrimitiveType.Cube, lampRoot, new Vector3(1.7f, 5.8f, 0f), new Vector3(0.6f, 0.15f, 0.3f), SodiumLight, collider: false, name: "Lamp Head");
                head.GetComponent<Renderer>().sharedMaterial = Palette.Get(SodiumLight, SodiumLight * 4f);

                var light = new GameObject("Light").AddComponent<Light>();
                light.transform.SetParent(lampRoot, false);
                light.transform.localPosition = new Vector3(1.7f, 5.4f, 0f);
                light.type = LightType.Point;
                light.color = SodiumLight;
                light.range = 16f;
                light.intensity = 2.6f;
                light.shadows = LightShadows.None;
            }
        }

        // ------------------------------------------------------------------ props & finisher spots

        static void BuildProps(Transform root)
        {
            BuildTeaKadai(root, new Vector3(-6.2f, 0f, 6f));
            BuildFishCrates(root, new Vector3(5.8f, 0f, -8f), finisher: true);
            BuildFishCrates(root, new Vector3(6f, 0f, 14f), finisher: true);
            BuildFishCrates(root, new Vector3(-6.4f, 0f, -18f), finisher: true);
            BuildAuto(root, new Vector3(-3.2f, 0f, -2f), 20f);

            // Ice boxes and nets
            for (int i = 0; i < 5; i++)
            {
                var p = new Vector3(Random.Range(4.5f, 6.5f), 0.35f, Random.Range(-HalfLength + 3f, HalfLength - 3f));
                if (Mathf.Abs(p.z + 8f) < 3f || Mathf.Abs(p.z - 14f) < 3f) continue;
                Palette.Primitive(PrimitiveType.Cube, root, p, new Vector3(1.2f, 0.7f, 0.9f), new Color(0.85f, 0.85f, 0.8f), name: "Ice Box");
            }
            for (int i = 0; i < 4; i++)
            {
                var p = new Vector3(Random.Range(5f, 6.8f), 0.08f, Random.Range(-HalfLength + 3f, HalfLength - 3f));
                Palette.Primitive(PrimitiveType.Cube, root, p, new Vector3(1.8f, 0.15f, 2.4f), new Color(0.12f, 0.3f, 0.2f), collider: false, name: "Fishing Net");
            }
        }

        static FinisherSpot MakeSpot(Transform root, string name, Vector3 position, out Transform visual)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            visual = new GameObject("Intact").transform;
            visual.SetParent(go.transform, false);
            var spot = go.AddComponent<FinisherSpot>();
            spot.intactVisual = visual.gameObject;
            return spot;
        }

        static void BuildTeaKadai(Transform root, Vector3 position)
        {
            var spot = MakeSpot(root, "Tea Kadai", position, out var v);
            spot.finisherName = "TEA KADAI SMASH!";
            spot.bodyRadius = 1.2f;
            spot.promptName = "tea kadai";
            spot.damage = 60f;
            spot.debrisColor = new Color(0.55f, 0.35f, 0.2f);

            Color wood = new Color(0.45f, 0.3f, 0.18f);
            Palette.Primitive(PrimitiveType.Cube, v, new Vector3(0f, 0.55f, 0f), new Vector3(1.4f, 1.1f, 2.6f), wood, name: "Counter");
            Palette.Primitive(PrimitiveType.Cube, v, new Vector3(0.72f, 0.75f, 0f), new Vector3(0.04f, 0.5f, 2.4f), new Color(0.9f, 0.85f, 0.2f), collider: false, name: "Painted Front");
            // Blue tarp roof on poles
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Palette.Primitive(PrimitiveType.Cylinder, v, new Vector3(sx * 0.65f, 1.2f, sz * 1.25f), new Vector3(0.06f, 1.2f, 0.06f), wood, collider: false, name: "Pole");
            Palette.Primitive(PrimitiveType.Cube, v, new Vector3(0.2f, 2.4f, 0f), new Vector3(2f, 0.05f, 3f), new Color(0.15f, 0.35f, 0.75f), name: "Tarp");   // has collision so the camera stops at it
            // Brass tea kettle, glass jars of biscuits, a small tube light
            Palette.Primitive(PrimitiveType.Cylinder, v, new Vector3(0f, 1.3f, -0.6f), new Vector3(0.45f, 0.2f, 0.45f), new Color(0.8f, 0.6f, 0.2f), collider: false, name: "Tea Kettle");
            for (int i = 0; i < 4; i++)
                Palette.Primitive(PrimitiveType.Cylinder, v, new Vector3(0.3f, 1.25f, 0.2f + i * 0.28f), new Vector3(0.2f, 0.15f, 0.2f),
                    new Color(0.7f, 0.85f, 0.9f, 1f), collider: false, name: "Biscuit Jar");
            var tube = Palette.Primitive(PrimitiveType.Cube, v, new Vector3(0.5f, 2.2f, 0f), new Vector3(0.05f, 0.05f, 1.2f), Color.white, collider: false, name: "Tube Light");
            tube.GetComponent<Renderer>().sharedMaterial = Palette.Get(Color.white, new Color(2f, 2.2f, 2.2f));
            var light = new GameObject("Kadai Light").AddComponent<Light>();
            light.transform.SetParent(v, false);
            light.transform.localPosition = new Vector3(1f, 2f, 0f);
            light.type = LightType.Point;
            light.color = new Color(0.85f, 1f, 0.95f);
            light.range = 7f;
            light.intensity = 1.5f;
            // Bench
            Palette.Primitive(PrimitiveType.Cube, v, new Vector3(1.5f, 0.25f, 0f), new Vector3(0.4f, 0.5f, 2f), wood * 0.8f, name: "Bench");
        }

        static void BuildFishCrates(Transform root, Vector3 position, bool finisher)
        {
            Transform parent;
            if (finisher)
            {
                var spot = MakeSpot(root, "Fish Crates", position, out parent);
                spot.finisherName = "MEEN PETTI SLAM!";
                spot.promptName = "fish crates";
                spot.damage = 50f;
                spot.gethuCost = 25f;
                spot.debrisColor = new Color(0.2f, 0.4f, 0.8f);
            }
            else
            {
                parent = new GameObject("Fish Crates").transform;
                parent.SetParent(root, false);
                parent.localPosition = position;
            }

            Color[] crateColors = { new Color(0.15f, 0.35f, 0.8f), new Color(0.8f, 0.2f, 0.15f), new Color(0.95f, 0.75f, 0.15f) };
            for (int x = 0; x < 2; x++)
                for (int z = 0; z < 2; z++)
                {
                    int stack = Random.Range(1, 4);
                    for (int y = 0; y < stack; y++)
                        Palette.Primitive(PrimitiveType.Cube, parent, new Vector3(x * 0.75f - 0.37f, 0.22f + y * 0.44f, z * 1.05f - 0.52f),
                            new Vector3(0.7f, 0.42f, 1f), crateColors[Random.Range(0, crateColors.Length)], name: "Crate");
                }
        }

        static void BuildAuto(Transform root, Vector3 position, float yaw)
        {
            var spot = MakeSpot(root, "Parked Auto", position, out var v);
            spot.finisherName = "AUTO SMASH!";
            spot.promptName = "auto";
            spot.damage = 70f;
            spot.gethuCost = 40f;
            spot.useRadius = 2.8f;
            spot.bodyRadius = 1.3f;
            spot.debrisColor = new Color(0.95f, 0.8f, 0.1f);
            v.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Color yellow = new Color(0.98f, 0.8f, 0.1f);
            Color black = new Color(0.07f, 0.07f, 0.07f);
            Color green = new Color(0.1f, 0.45f, 0.2f);
            Palette.Primitive(PrimitiveType.Cube, v, new Vector3(0f, 0.75f, -0.2f), new Vector3(1.3f, 0.9f, 1.6f), green, name: "Body");
            Palette.Primitive(PrimitiveType.Cube, v, new Vector3(0f, 0.7f, 0.9f), new Vector3(0.8f, 0.8f, 0.7f), green, name: "Nose");
            Palette.Primitive(PrimitiveType.Cube, v, new Vector3(0f, 1.75f, -0.1f), new Vector3(1.35f, 0.08f, 2.2f), yellow, collider: false, name: "Canopy");
            Palette.Primitive(PrimitiveType.Cube, v, new Vector3(0f, 1.45f, -0.95f), new Vector3(1.3f, 0.6f, 0.08f), black, collider: false, name: "Back Hood");
            Palette.Primitive(PrimitiveType.Cube, v, new Vector3(0f, 1.4f, 1.15f), new Vector3(0.8f, 0.5f, 0.04f), new Color(0.4f, 0.55f, 0.6f), collider: false, name: "Windshield");
            Palette.Primitive(PrimitiveType.Cylinder, v, new Vector3(0f, 0.25f, 1.25f), new Vector3(0.45f, 0.06f, 0.45f), black, collider: false, name: "Front Wheel")
                .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            for (int s = -1; s <= 1; s += 2)
                Palette.Primitive(PrimitiveType.Cylinder, v, new Vector3(s * 0.62f, 0.25f, -0.6f), new Vector3(0.45f, 0.06f, 0.45f), black, collider: false, name: "Rear Wheel")
                    .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        // ------------------------------------------------------------------ invisible walls

        static void BuildBoundaries(Transform root)
        {
            Wall(root, new Vector3(7.6f, 2f, 0f), new Vector3(0.4f, 4f, HalfLength * 2f + 10f));   // harbour edge
            Wall(root, new Vector3(-8.1f, 2f, 0f), new Vector3(0.4f, 4f, HalfLength * 2f + 10f));  // godown fronts
            Wall(root, new Vector3(0f, 2f, HalfLength + 2f), new Vector3(16f, 4f, 0.4f));
            Wall(root, new Vector3(0f, 2f, -HalfLength - 2f), new Vector3(16f, 4f, 0.4f));

            // Visible barricades at both ends of the lane (police barricade style)
            for (int end = -1; end <= 1; end += 2)
                for (float x = -6f; x <= 6f; x += 2.2f)
                {
                    float z = end * (HalfLength + 1.6f);
                    Palette.Primitive(PrimitiveType.Cube, root, new Vector3(x, 0.55f, z), new Vector3(2f, 1.1f, 0.15f),
                        new Color(0.9f, 0.9f, 0.85f), collider: false, name: "Barricade");
                    Palette.Primitive(PrimitiveType.Cube, root, new Vector3(x, 0.75f, z - end * 0.01f), new Vector3(2.02f, 0.25f, 0.16f),
                        new Color(0.8f, 0.1f, 0.1f), collider: false, name: "Barricade Stripe");
                }
        }

        static void Wall(Transform root, Vector3 position, Vector3 size)
        {
            var go = new GameObject("Invisible Wall") { layer = 2 };   // Ignore Raycast: blocks fighters, not the camera
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
        }
    }
}

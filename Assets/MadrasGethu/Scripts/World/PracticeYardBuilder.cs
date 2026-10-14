using UnityEngine;

namespace MadrasGethu
{
    /// <summary>
    /// Practice yard behind the Royapuram boxing club: a walled concrete courtyard with one practice dummy,
    /// a brick wall for wall slams and spinebusters, a tea kadai, fish crates and an auto for prop finishers,
    /// and a rack of street weapons. Everything is placeholder geometry built in code.
    /// </summary>
    public static class PracticeYardBuilder
    {
        public const float Half = 11f;
        public static readonly Vector3 PlayerStart = new Vector3(0f, 0.1f, -2f);
        public static readonly Vector3 DummySpawn = new Vector3(0f, 0.1f, 2.5f);

        static readonly Color Concrete = new Color(0.42f, 0.41f, 0.39f);
        static readonly Color Brick = new Color(0.55f, 0.27f, 0.2f);
        static readonly Color Mortar = new Color(0.62f, 0.58f, 0.52f);
        static readonly Color PaintRed = new Color(0.75f, 0.12f, 0.1f);
        static readonly Color TubeLight = new Color(0.9f, 1f, 0.95f);

        public static void Build(Transform root)
        {
            Random.InitState(7);
            KasimeduArenaBuilder.SetupNightLighting(root);
            RenderSettings.ambientLight = new Color(0.36f, 0.36f, 0.42f);   // brighter than the harbour: easy to see moves
            RenderSettings.fogDensity = 0.008f;

            BuildGround(root);
            BuildWalls(root);
            BuildEquipment(root);
            BuildLights(root);

            KasimeduArenaBuilder.BuildTeaKadai(root, new Vector3(-8.2f, 0f, -3f));
            KasimeduArenaBuilder.BuildFishCrates(root, new Vector3(7.5f, 0f, 5f), finisher: true);
            KasimeduArenaBuilder.BuildAuto(root, new Vector3(6.5f, 0f, -6f), 200f);

            PlaceWeapons();
            Random.InitState(System.Environment.TickCount);
        }

        static void BuildGround(Transform root)
        {
            Palette.Primitive(PrimitiveType.Cube, root, new Vector3(0f, -0.5f, 0f), new Vector3(Half * 2f + 2f, 1f, Half * 2f + 2f), Concrete, name: "Yard Floor");

            // Painted sparring square
            const float sq = 4f, line = 0.1f;
            for (int s = -1; s <= 1; s += 2)
            {
                Palette.Primitive(PrimitiveType.Cube, root, new Vector3(0f, 0.005f, s * sq), new Vector3(sq * 2f + line, 0.01f, line), PaintRed, collider: false, name: "Square Line");
                Palette.Primitive(PrimitiveType.Cube, root, new Vector3(s * sq, 0.005f, 0f), new Vector3(line, 0.01f, sq * 2f + line), PaintRed, collider: false, name: "Square Line");
            }

            // Where the dummy stands
            Palette.Primitive(PrimitiveType.Cylinder, root, new Vector3(DummySpawn.x, 0.006f, DummySpawn.z), new Vector3(1.3f, 0.004f, 1.3f),
                new Color(0.95f, 0.75f, 0.15f), collider: false, name: "Dummy Mark");
            Palette.Primitive(PrimitiveType.Cylinder, root, new Vector3(DummySpawn.x, 0.008f, DummySpawn.z), new Vector3(1.1f, 0.004f, 1.1f),
                Concrete * 0.9f, collider: false, name: "Dummy Mark Inner");

            // Oil stains and cracks
            for (int i = 0; i < 6; i++)
            {
                var p = new Vector3(Random.Range(-9f, 9f), 0.004f, Random.Range(-9f, 9f));
                Palette.Primitive(PrimitiveType.Cylinder, root, p, new Vector3(Random.Range(0.6f, 1.6f), 0.002f, Random.Range(0.5f, 1.3f)),
                    Concrete * 0.75f, collider: false, name: "Stain");
            }
        }

        static void BuildWalls(Transform root)
        {
            // Solid brick walls (north and west): real collision, so wall slams, wall bounces and spinebusters work.
            BrickWall(root, new Vector3(0f, 1.6f, Half + 0.25f), new Vector3(Half * 2f + 1f, 3.2f, 0.5f), true);
            BrickWall(root, new Vector3(-Half - 0.25f, 1.6f, 0f), new Vector3(0.5f, 3.2f, Half * 2f + 1f), false);

            // Faded posters on the north wall
            Color[] posters = { new Color(0.9f, 0.75f, 0.2f), new Color(0.2f, 0.5f, 0.8f), new Color(0.85f, 0.3f, 0.35f) };
            for (int i = 0; i < 3; i++)
                Palette.Primitive(PrimitiveType.Cube, root, new Vector3(-4f + i * 4f, 1.9f, Half - 0.02f), new Vector3(1.2f, 1.6f, 0.02f),
                    posters[i], collider: false, name: "Poster");
            // Club banner
            Palette.Primitive(PrimitiveType.Cube, root, new Vector3(0f, 2.9f, Half - 0.03f), new Vector3(8f, 0.5f, 0.02f), PaintRed, collider: false, name: "Club Banner");

            // Open sides (east and south): a low railing you can see over, invisible walls keep everyone in.
            Color rail = new Color(0.3f, 0.32f, 0.3f);
            for (int side = 0; side < 2; side++)
            {
                bool east = side == 0;
                for (float t = -Half; t <= Half; t += 2.2f)
                {
                    var post = east ? new Vector3(Half, 0.55f, t) : new Vector3(t, 0.55f, -Half);
                    Palette.Primitive(PrimitiveType.Cylinder, root, post, new Vector3(0.08f, 0.55f, 0.08f), rail, collider: false, name: "Rail Post");
                }
                var bar = east ? new Vector3(Half, 1.05f, 0f) : new Vector3(0f, 1.05f, -Half);
                var barSize = east ? new Vector3(0.06f, 0.06f, Half * 2f) : new Vector3(Half * 2f, 0.06f, 0.06f);
                Palette.Primitive(PrimitiveType.Cube, root, bar, barSize, rail, collider: false, name: "Rail");
                InvisibleWall(root, east ? new Vector3(Half + 0.3f, 2f, 0f) : new Vector3(0f, 2f, -Half - 0.3f),
                    east ? new Vector3(0.4f, 4f, Half * 2f + 2f) : new Vector3(Half * 2f + 2f, 4f, 0.4f));
            }
        }

        static void BrickWall(Transform root, Vector3 position, Vector3 size, bool alongX)
        {
            Palette.Primitive(PrimitiveType.Cube, root, position, size, Brick, name: "Brick Wall");
            // Mortar lines so it reads as brick from the camera
            for (float y = 0.3f; y < size.y; y += 0.3f)
            {
                var p = new Vector3(position.x, y, position.z);
                Vector3 s = alongX ? new Vector3(size.x, 0.02f, size.z + 0.02f) : new Vector3(size.x + 0.02f, 0.02f, size.z);
                Palette.Primitive(PrimitiveType.Cube, root, p, s, Mortar, collider: false, name: "Mortar");
            }
        }

        static void InvisibleWall(Transform root, Vector3 position, Vector3 size)
        {
            var go = new GameObject("Invisible Wall") { layer = 2 };   // blocks fighters, not the camera
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            go.AddComponent<BoxCollider>().size = size;
        }

        static void BuildEquipment(Transform root)
        {
            Color bag = new Color(0.35f, 0.12f, 0.1f);
            Color rope = new Color(0.2f, 0.18f, 0.15f);
            // Heavy bags hanging from a beam along the north wall (at the ends, leaving the middle free for wall slams)
            Palette.Primitive(PrimitiveType.Cube, root, new Vector3(0f, 3.1f, Half - 0.6f), new Vector3(Half * 2f, 0.15f, 0.15f), rope, collider: false, name: "Beam");
            foreach (float x in new[] { -8f, -6.5f, 6.5f, 8f })
            {
                Palette.Primitive(PrimitiveType.Cylinder, root, new Vector3(x, 1.35f, Half - 0.6f), new Vector3(0.45f, 0.6f, 0.45f), bag, collider: false, name: "Heavy Bag");
                Palette.Primitive(PrimitiveType.Cylinder, root, new Vector3(x, 2.5f, Half - 0.6f), new Vector3(0.03f, 0.55f, 0.03f), rope, collider: false, name: "Chain");
            }

            // Tyre stack and a bench
            Color tyre = new Color(0.08f, 0.08f, 0.08f);
            for (int i = 0; i < 4; i++)
                Palette.Primitive(PrimitiveType.Cylinder, root, new Vector3(-9.2f, 0.1f + i * 0.2f, 8.5f), new Vector3(0.9f, 0.1f, 0.9f), tyre, name: "Tyre");
            Palette.Primitive(PrimitiveType.Cube, root, new Vector3(0f, 0.25f, -9.6f), new Vector3(3f, 0.5f, 0.5f), new Color(0.4f, 0.28f, 0.16f), name: "Bench");

            // Weapon rack
            Palette.Primitive(PrimitiveType.Cube, root, new Vector3(-5f, 0.05f, -9.3f), new Vector3(5.5f, 0.1f, 1.2f), new Color(0.3f, 0.22f, 0.14f), collider: false, name: "Weapon Mat");
        }

        static void BuildLights(Transform root)
        {
            // Big floodlight over the yard plus tube lights on the walls.
            var flood = new GameObject("Floodlight").AddComponent<Light>();
            flood.transform.SetParent(root, false);
            flood.transform.localPosition = new Vector3(0f, 8f, 0f);
            flood.type = LightType.Point;
            flood.color = new Color(1f, 0.95f, 0.85f);
            flood.range = 24f;
            flood.intensity = 2.2f;
            flood.shadows = LightShadows.Soft;

            Vector3[] tubes = { new Vector3(-4f, 2.6f, Half - 0.05f), new Vector3(4f, 2.6f, Half - 0.05f), new Vector3(-Half + 0.05f, 2.6f, 0f) };
            foreach (var p in tubes)
            {
                bool onWest = p.x < -Half + 1f;
                var tube = Palette.Primitive(PrimitiveType.Cube, root, p, onWest ? new Vector3(0.05f, 0.05f, 1.4f) : new Vector3(1.4f, 0.05f, 0.05f),
                    TubeLight, collider: false, name: "Tube Light");
                tube.GetComponent<Renderer>().sharedMaterial = Palette.Get(TubeLight, TubeLight * 2.2f);
                var light = new GameObject("Tube Glow").AddComponent<Light>();
                light.transform.SetParent(root, false);
                light.transform.localPosition = p + (onWest ? Vector3.right : Vector3.back) * 0.8f;
                light.type = LightType.Point;
                light.color = TubeLight;
                light.range = 9f;
                light.intensity = 1.2f;
            }
        }

        /// <summary>A rack of street weapons at the south end of the yard.</summary>
        public static void PlaceWeapons()
        {
            Weapon.Create(WeaponType.Lathi, new Vector3(-7.2f, 0f, -9.2f));
            Weapon.Create(WeaponType.IronPipe, new Vector3(-6.2f, 0f, -9.4f));
            Weapon.Create(WeaponType.Plank, new Vector3(-5.1f, 0f, -9.1f));
            Weapon.Create(WeaponType.Bottle, new Vector3(-4.1f, 0f, -9.4f));
            Weapon.Create(WeaponType.Bottle, new Vector3(-3.4f, 0f, -9.1f));
            Weapon.Create(WeaponType.Brick, new Vector3(-2.6f, 0f, -9.3f));
        }
    }
}

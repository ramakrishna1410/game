using UnityEngine;

namespace MadrasGethu
{
    /// <summary>Cheap placeholder effects made of tiny cubes. Swap for real particle systems later.</summary>
    public static class FX
    {
        static readonly Color SparkColor = new Color(1f, 0.85f, 0.4f);
        static readonly Color HeavySparkColor = new Color(1f, 0.45f, 0.15f);
        static readonly Color BlockColor = new Color(0.55f, 0.8f, 1f);
        static readonly Color DustColor = new Color(0.45f, 0.42f, 0.38f);

        public static void HitSpark(Vector3 position, bool heavy)
        {
            Color c = heavy ? HeavySparkColor : SparkColor;
            Burst(position, heavy ? 14 : 8, heavy ? 6f : 4f, 0.07f, c, c * 3f, 0.25f, 0f);
        }

        public static void BlockSpark(Vector3 position)
        {
            Burst(position, 6, 3f, 0.05f, BlockColor, BlockColor * 2f, 0.2f, 0f);
        }

        public static void Dust(Vector3 position, int count = 10)
        {
            Burst(position + Vector3.up * 0.1f, count, 2.5f, 0.15f, DustColor, Color.black, 0.6f, -2f);
        }

        public static void Burst(Vector3 position, int count, float speed, float size, Color color, Color emission,
            float life, float gravity)
        {
            var mat = Palette.Get(color, emission);
            for (int i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(go.GetComponent<Collider>());
                go.name = "FX";
                go.layer = 2; // Ignore Raycast, so the camera never bumps into effects
                go.transform.position = position;
                go.transform.localScale = Vector3.one * size;
                go.transform.rotation = Random.rotation;
                go.GetComponent<Renderer>().sharedMaterial = mat;
                var p = go.AddComponent<FXParticle>();
                p.velocity = Random.onUnitSphere * speed * Random.Range(0.5f, 1f);
                p.gravity = gravity;
                p.life = life * Random.Range(0.7f, 1.2f);
            }
        }
    }
}

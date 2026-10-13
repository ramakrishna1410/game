using System.Collections.Generic;
using UnityEngine;

namespace MadrasGethu
{
    /// <summary>
    /// Creates and caches simple coloured materials for placeholder geometry.
    /// It copies the default primitive material, so it works in both URP and the Built-in pipeline.
    /// </summary>
    public static class Palette
    {
        static Material baseMaterial;
        static readonly Dictionary<(Color, Color), Material> cache = new Dictionary<(Color, Color), Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            baseMaterial = null;
            cache.Clear();
        }

        public static Material Get(Color color) => Get(color, Color.black);

        public static Material Get(Color color, Color emission)
        {
            if (cache.TryGetValue((color, emission), out var mat) && mat != null)
                return mat;

            if (baseMaterial == null)
            {
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                baseMaterial = probe.GetComponent<Renderer>().sharedMaterial;
                Object.Destroy(probe);
            }

            mat = new Material(baseMaterial) { color = color };
            if (emission != Color.black)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            cache[(color, emission)] = mat;
            return mat;
        }

        /// <summary>Creates a primitive with a coloured material. Pass collider=false for purely visual parts.</summary>
        public static GameObject Primitive(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale,
            Color color, bool collider = true, string name = null)
        {
            var go = GameObject.CreatePrimitive(type);
            if (name != null) go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = Get(color);
            if (!collider)
            {
                var col = go.GetComponent<Collider>();
                col.enabled = false;
                Object.Destroy(col);
            }
            return go;
        }

        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        }
    }
}

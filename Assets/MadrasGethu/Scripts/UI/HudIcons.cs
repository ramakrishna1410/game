using UnityEngine;

namespace MadrasGethu
{
    /// <summary>
    /// HUD icons drawn in code (no image files needed): key caps, mouse buttons, the grab hand, a skull,
    /// and rings for the QTE timing circle. White shapes on transparency, tinted when drawn.
    /// </summary>
    public static class HudIcons
    {
        const int Size = 128;

        static Texture2D keyFill, keyBorder, mouseBody, mouseLeft, mouseRight, hand, skull, ring, disc;

        public static Texture2D KeyFill => keyFill != null ? keyFill : (keyFill = Make(KeyFillShape));
        public static Texture2D KeyBorder => keyBorder != null ? keyBorder : (keyBorder = Make(KeyBorderShape));
        public static Texture2D MouseBody => mouseBody != null ? mouseBody : (mouseBody = Make(MouseBodyShape));
        public static Texture2D MouseLeft => mouseLeft != null ? mouseLeft : (mouseLeft = Make(MouseLeftShape));
        public static Texture2D MouseRight => mouseRight != null ? mouseRight : (mouseRight = Make(MouseRightShape));
        public static Texture2D Hand => hand != null ? hand : (hand = Make(HandShape));
        public static Texture2D Skull => skull != null ? skull : (skull = Make(SkullShape));
        public static Texture2D Ring => ring != null ? ring : (ring = Make(RingShape));
        public static Texture2D Disc => disc != null ? disc : (disc = Make(DiscShape));

        delegate float Shape(float x, float y);   // signed distance in 0..1 space (y down); < 0 inside

        static Texture2D Make(Shape shape)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[Size * Size];
            float aa = 1.5f / Size;
            for (int py = 0; py < Size; py++)
            {
                for (int px = 0; px < Size; px++)
                {
                    float x = (px + 0.5f) / Size;
                    float y = 1f - (py + 0.5f) / Size;   // textures start at the bottom row
                    float d = shape(x, y);
                    byte a = (byte)(255f * Mathf.Clamp01(0.5f - d / aa));
                    pixels[py * Size + px] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }

        // ------------------------------------------------------------------ distance helpers

        static float Circle(float x, float y, float cx, float cy, float r) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;

        static float RoundBox(float x, float y, float cx, float cy, float hw, float hh, float r)
        {
            float qx = Mathf.Abs(x - cx) - hw + r;
            float qy = Mathf.Abs(y - cy) - hh + r;
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        static float Capsule(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            float pax = x - ax, pay = y - ay, bax = bx - ax, bay = by - ay;
            float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            float dx = pax - bax * h, dy = pay - bay * h;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }

        static float Union(float a, float b) => Mathf.Min(a, b);
        static float Cut(float a, float b) => Mathf.Max(a, -b);

        // ------------------------------------------------------------------ shapes

        static float KeyFillShape(float x, float y) => RoundBox(x, y, 0.5f, 0.5f, 0.46f, 0.46f, 0.16f);
        static float KeyBorderShape(float x, float y) => Mathf.Abs(KeyFillShape(x, y) + 0.035f) - 0.035f;

        static float MouseOutline(float x, float y) => Capsule(x, y, 0.5f, 0.36f, 0.5f, 0.64f, 0.3f);
        static float MouseBodyShape(float x, float y)
        {
            float outline = Mathf.Abs(MouseOutline(x, y) + 0.03f) - 0.03f;
            float split = Union(RoundBox(x, y, 0.5f, 0.24f, 0.012f, 0.18f, 0.01f), RoundBox(x, y, 0.5f, 0.42f, 0.3f, 0.012f, 0.01f));
            return Union(outline, Mathf.Max(split, MouseOutline(x, y)));
        }
        static float MouseButton(float x, float y, bool left)
        {
            float inner = MouseOutline(x, y) + 0.07f;
            float quad = left ? RoundBox(x, y, 0.32f, 0.2f, 0.16f, 0.2f, 0f) : RoundBox(x, y, 0.68f, 0.2f, 0.16f, 0.2f, 0f);
            return Mathf.Max(inner, Mathf.Max(quad, -(Mathf.Abs(x - 0.5f) - 0.03f)));
        }
        static float MouseLeftShape(float x, float y) => MouseButton(x, y, true);
        static float MouseRightShape(float x, float y) => MouseButton(x, y, false);

        static float HandShape(float x, float y)
        {
            float palm = RoundBox(x, y, 0.52f, 0.66f, 0.21f, 0.2f, 0.08f);
            float f1 = Capsule(x, y, 0.37f, 0.52f, 0.36f, 0.22f, 0.055f);
            float f2 = Capsule(x, y, 0.48f, 0.5f, 0.48f, 0.12f, 0.055f);
            float f3 = Capsule(x, y, 0.59f, 0.5f, 0.6f, 0.15f, 0.055f);
            float f4 = Capsule(x, y, 0.69f, 0.55f, 0.71f, 0.27f, 0.05f);
            float thumb = Capsule(x, y, 0.35f, 0.72f, 0.18f, 0.5f, 0.06f);
            return Union(Union(Union(palm, f1), Union(f2, f3)), Union(f4, thumb));
        }

        static float SkullShape(float x, float y)
        {
            float head = Union(Circle(x, y, 0.5f, 0.42f, 0.33f), RoundBox(x, y, 0.5f, 0.74f, 0.2f, 0.14f, 0.06f));
            float eyes = Union(Circle(x, y, 0.37f, 0.47f, 0.085f), Circle(x, y, 0.63f, 0.47f, 0.085f));
            float nose = Capsule(x, y, 0.5f, 0.58f, 0.5f, 0.63f, 0.025f);
            float teeth = Union(RoundBox(x, y, 0.42f, 0.8f, 0.012f, 0.08f, 0f), Union(RoundBox(x, y, 0.5f, 0.8f, 0.012f, 0.08f, 0f), RoundBox(x, y, 0.58f, 0.8f, 0.012f, 0.08f, 0f)));
            return Cut(Cut(Cut(head, eyes), nose), teeth);
        }

        static float RingShape(float x, float y) => Mathf.Abs(Circle(x, y, 0.5f, 0.5f, 0.44f)) - 0.04f;
        static float DiscShape(float x, float y) => Circle(x, y, 0.5f, 0.5f, 0.47f);

        // ------------------------------------------------------------------ drawing

        public static void Draw(Rect r, Texture2D tex, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(r, tex, ScaleMode.StretchToFill, true);
            GUI.color = old;
        }

        public static bool IsMouse(string key) => key == "LMB" || key == "RMB";

        /// <summary>Width of a key cap of height h (wide keys like SPACE get longer caps).</summary>
        public static float KeyWidth(string key, float h) => IsMouse(key) ? h * 0.75f : key.Length <= 1 ? h : h * (0.45f + 0.28f * key.Length);

        /// <summary>A keyboard key cap (or a mouse with the button lit) with the key name on it.</summary>
        public static void DrawKey(Rect r, string key, Color accent, GUIStyle textStyle, float alpha = 1f)
        {
            if (IsMouse(key))
            {
                Draw(r, MouseBody, new Color(1f, 1f, 1f, 0.95f * alpha));
                Draw(r, key == "LMB" ? MouseLeft : MouseRight, new Color(accent.r, accent.g, accent.b, alpha));
                return;
            }
            Draw(r, KeyFill, new Color(0.08f, 0.08f, 0.1f, 0.85f * alpha));
            Draw(r, KeyBorder, new Color(accent.r, accent.g, accent.b, alpha));
            var old = textStyle.normal.textColor;
            textStyle.normal.textColor = new Color(1f, 1f, 1f, alpha);
            GUI.Label(r, key, textStyle);
            textStyle.normal.textColor = old;
        }
    }
}

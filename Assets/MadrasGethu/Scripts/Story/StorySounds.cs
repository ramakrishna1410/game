using UnityEngine;

namespace MadrasGethu
{
    /// <summary>
    /// Story sounds made in code, so scenes are never silent before real recordings exist:
    /// the auto's two-stroke engine, the harbour (waves on the jetty, crows at dawn) and a phone ring.
    /// Real files win: Resources/Audio/Ambience for the harbour.
    /// </summary>
    public static class StorySounds
    {
        const int Rate = 22050;
        static AudioClip engine, ring, morning, night;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { engine = ring = morning = night = null; }

        static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>One second of putt-putt (26 firings, so it loops cleanly). Pitch it up when driving.</summary>
        public static AudioClip AutoEngine()
        {
            if (engine != null) return engine;
            var rnd = new System.Random(5);
            var d = new float[Rate];
            const int firings = 26;
            float low = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float phase = (t * firings) % 1f;
                float env = Mathf.Exp(-phase * 9f);
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                low += (noise - low) * 0.12f;                     // muffled exhaust
                float thump = Mathf.Sin(2f * Mathf.PI * 62f * t) * Mathf.Exp(-phase * 5f);
                float rattle = noise * 0.18f * Mathf.Exp(-phase * 20f);
                d[i] = (low * 1.4f * env + thump * 0.55f + rattle) * 0.85f;
            }
            return engine = Make("Auto Engine", d);
        }

        /// <summary>Eight seconds of waves slapping the jetty, with crows calling in the morning version.</summary>
        public static AudioClip Harbour(bool morningCrows)
        {
            if (morningCrows && morning != null) return morning;
            if (!morningCrows && night != null) return night;
            var rnd = new System.Random(11);
            int n = Rate * 8, fade = Rate;
            var raw = new float[n + fade];
            float brown = 0f, lp = 0f, hiss = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float t = i / (float)Rate;
                float w = (float)(rnd.NextDouble() * 2.0 - 1.0);
                brown = Mathf.Clamp(brown + w * 0.02f, -1f, 1f) * 0.998f;
                lp += (brown - lp) * 0.08f;
                hiss += (w - hiss) * 0.35f;
                // Two slow swells per loop, each with a slap as the wave hits the concrete.
                float swell = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / 4f);
                float slap = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * t / 4f - 0.6f)), 12f);
                raw[i] = (lp * (0.6f + 0.8f * swell) * 1.6f + hiss * 0.04f * (0.3f + swell) + hiss * slap * 0.12f) * 0.45f;
            }
            if (morningCrows)
            {
                Crow(raw, 1.3f, 0.16f, 1f);
                Crow(raw, 1.75f, 0.16f, 0.96f);
                Crow(raw, 5.4f, 0.09f, 1.08f);   // a second one, further off
            }
            // Fold the tail over the start so the loop has no click.
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float a = i < fade ? i / (float)fade : 1f;
                d[i] = raw[i] * a + (i < fade ? raw[n + i] * (1f - a) : 0f);
            }
            var clip = Make(morningCrows ? "Harbour Morning" : "Harbour Night", d);
            if (morningCrows) morning = clip; else night = clip;
            return clip;
        }

        /// <summary>A crow's "kaa": a buzzy tone with its pitch falling, through a nasal band-pass.</summary>
        static void Crow(float[] buf, float at, float volume, float pitch)
        {
            int start = (int)(at * Rate), len = (int)(0.32f * Rate);
            float phase = 0f, y1 = 0f, y2 = 0f, x1 = 0f, x2 = 0f;
            // Band-pass around 1.3 kHz (biquad, Q ~ 2)
            float w0 = 2f * Mathf.PI * 1300f * pitch / Rate, alpha = Mathf.Sin(w0) / 4f;
            float b0 = alpha, b2 = -alpha, a0 = 1f + alpha, a1 = -2f * Mathf.Cos(w0), a2 = 1f - alpha;
            for (int i = 0; i < len && start + i < buf.Length; i++)
            {
                float k = i / (float)len;
                float f = (620f - 180f * k) * pitch;
                phase += f / Rate;
                float saw = 2f * (phase % 1f) - 1f;
                float x = saw * (1f + 0.3f * Mathf.Sin(2f * Mathf.PI * 38f * i / Rate));   // rasp
                float y = (b0 * x + b2 * x2 - a1 * y1 - a2 * y2) / a0;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                float env = Mathf.Sin(Mathf.PI * Mathf.Min(1f, k * 1.15f)) * (k < 0.08f ? k / 0.08f : 1f);
                buf[start + i] += Mathf.Clamp(y * env * volume * 3f, -0.5f, 0.5f);
            }
        }

        /// <summary>A phone ringing: the two-tone "tring tring".</summary>
        public static AudioClip Ring()
        {
            if (ring != null) return ring;
            var d = new float[Rate * 2];
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float c = t % 1f;
                bool on = c < 0.4f || (c > 0.5f && c < 0.9f);
                d[i] = on ? 0.09f * (Mathf.Sin(2f * Mathf.PI * 400f * t) + Mathf.Sin(2f * Mathf.PI * 450f * t)) : 0f;
            }
            return ring = Make("Ring", d);
        }
    }
}

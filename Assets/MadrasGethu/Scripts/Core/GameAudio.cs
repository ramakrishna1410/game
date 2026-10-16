using System.Collections.Generic;
using UnityEngine;

namespace MadrasGethu
{
    /// <summary>Every sound the game makes. Your own files go in Assets/MadrasGethu/Resources/Audio/&lt;name&gt;/.</summary>
    public enum Sfx
    {
        Auto,         // HitInfo default: picked from the blow (punch / heavy / kick)
        Silent,       // HitInfo: no impact sound (the move plays its own)
        PunchLight, PunchHeavy, Kick, BodyBlow, Block, Parry, Whoosh, Grab,
        BodyFall, Slam, BoneCrack, Smash, Glass, LidSlam, Splash,
        Grunt, Shout, Cheer,
    }

    /// <summary>
    /// Sound effects, music and ambience. For each sound it uses your files if there are any
    /// (Resources/Audio/PunchLight/*.wav etc., several files = random variations), otherwise a sound
    /// synthesised in code so the game is never silent. Voices (Grunt, Shout, Cheer) come only from files.
    /// Music: Resources/Audio/Music, ambience: Resources/Audio/Ambience (looped).
    /// </summary>
    public static class GameAudio
    {
        public static float SfxVolume = 1f;
        public static float MusicVolume = 0.35f;

        static readonly Dictionary<Sfx, AudioClip[]> clips = new Dictionary<Sfx, AudioClip[]>();
        static readonly Dictionary<Sfx, float> lastPlayed = new Dictionary<Sfx, float>();
        static GameObject root;
        static AudioSource[] pool;
        static int next;
        static AudioSource music, ambience;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            clips.Clear();
            lastPlayed.Clear();
            root = null;
            pool = null;
            music = ambience = null;
        }

        static void EnsurePool()
        {
            if (root != null) return;
            root = new GameObject("Game Audio");
            pool = new AudioSource[24];
            for (int i = 0; i < pool.Length; i++)
            {
                var go = new GameObject("Sfx " + i);
                go.transform.SetParent(root.transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0.55f;   // mostly 3D, but still clear when the camera pulls away
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 4f;
                src.maxDistance = 40f;
                src.dopplerLevel = 0f;
                pool[i] = src;
            }
        }

        static AudioClip[] Clips(Sfx id)
        {
            if (clips.TryGetValue(id, out var c)) return c;
            c = Resources.LoadAll<AudioClip>("Audio/" + id);
            if (c == null || c.Length == 0) c = SfxSynth.Make(id);
            clips[id] = c;
            return c;
        }

        /// <summary>Plays a sound at a point (random variation and a little pitch spread).</summary>
        public static void Play(Sfx id, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (id == Sfx.Auto || id == Sfx.Silent) return;
            // Don't stack the same sound many times in one instant (multi-hit sweeps).
            if (lastPlayed.TryGetValue(id, out float t) && Time.unscaledTime - t < 0.035f) return;
            var set = Clips(id);
            if (set == null || set.Length == 0) return;
            lastPlayed[id] = Time.unscaledTime;
            EnsurePool();
            var src = pool[next];
            next = (next + 1) % pool.Length;
            src.transform.position = position;
            src.clip = set[Random.Range(0, set.Length)];
            src.volume = Mathf.Clamp01(volume * SfxVolume);
            // Slow motion lowers the pitch a little, which makes big hits feel heavier.
            src.pitch = pitch * Random.Range(0.93f, 1.07f) * Mathf.Lerp(0.8f, 1f, Mathf.Clamp01(Time.timeScale));
            src.Play();
        }

        /// <summary>Is there a recorded (or synthesised) sound for this?</summary>
        public static bool Has(Sfx id)
        {
            var set = Clips(id);
            return set != null && set.Length > 0;
        }

        /// <summary>Background music and harbour ambience, if their files are there.</summary>
        public static void StartMusic() => StartMusic(true);

        /// <summary>withMusic = false: just the harbour ambience (story scenes start the fight music later).</summary>
        public static void StartMusic(bool withMusic)
        {
            EnsurePool();
            var tracks = withMusic ? Resources.LoadAll<AudioClip>("Audio/Music") : new AudioClip[0];
            if (tracks.Length > 0 && music == null)
            {
                music = root.AddComponent<AudioSource>();
                music.clip = tracks[Random.Range(0, tracks.Length)];
                music.loop = true;
                music.spatialBlend = 0f;
                music.volume = MusicVolume;
                music.Play();
            }
            var amb = Resources.LoadAll<AudioClip>("Audio/Ambience");
            if (amb.Length > 0 && ambience == null)
            {
                ambience = root.AddComponent<AudioSource>();
                ambience.clip = amb[Random.Range(0, amb.Length)];
                ambience.loop = true;
                ambience.spatialBlend = 0f;
                ambience.volume = MusicVolume * 0.7f;
                ambience.Play();
            }
        }

        public static void StopMusic()
        {
            if (music != null) { Object.Destroy(music); music = null; }
        }

        /// <summary>The impact sound for a blow, when the hit doesn't name one.</summary>
        public static Sfx ImpactFor(HitInfo hit)
        {
            if (hit.sound != Sfx.Auto) return hit.sound;
            switch (hit.pose)
            {
                case FighterPose.Kick: case FighterPose.FrontKick: case FighterPose.Roundhouse: case FighterPose.PushKick:
                case FighterPose.SpinKick: case FighterPose.LowKick: case FighterPose.FlyingKick: case FighterPose.LegSweep:
                case FighterPose.Stomp: case FighterPose.RowdyKick:
                    return Sfx.Kick;
                case FighterPose.BodyBlow: case FighterPose.Knee: case FighterPose.FlyingKnee:
                    return Sfx.BodyBlow;
            }
            return hit.heavy || hit.damage >= 14f ? Sfx.PunchHeavy : Sfx.PunchLight;
        }
    }

    /// <summary>Placeholder sound effects made in code (used until you add real recordings).</summary>
    static class SfxSynth
    {
        const int Rate = 44100;

        public static AudioClip[] Make(Sfx id)
        {
            var list = new List<AudioClip>();
            for (int v = 0; v < 3; v++)
            {
                var data = Build(id, new System.Random((int)id * 97 + v * 13));
                if (data == null) return new AudioClip[0];
                var clip = AudioClip.Create(id + "_" + v, data.Length, 1, Rate, false);
                clip.SetData(data, 0);
                list.Add(clip);
            }
            return list.ToArray();
        }

        static float[] Build(Sfx id, System.Random r)
        {
            float vary = 0.9f + (float)r.NextDouble() * 0.2f;
            switch (id)
            {
                case Sfx.PunchLight: return Mix(Thud(0.09f, 140f * vary, 70f, 0.7f), Noise(r, 0.03f, 0.6f, 3000f), 0f);
                case Sfx.PunchHeavy: return Mix(Thud(0.18f, 95f * vary, 45f, 1f), Noise(r, 0.05f, 0.7f, 2200f), 0f);
                case Sfx.Kick: return Mix(Thud(0.2f, 80f * vary, 40f, 1f), Noise(r, 0.06f, 0.6f, 1600f), 0f);
                case Sfx.BodyBlow: return Mix(Thud(0.16f, 70f * vary, 45f, 1f), Noise(r, 0.08f, 0.35f, 900f), 0f);
                case Sfx.Block: return Mix(Thud(0.07f, 220f * vary, 160f, 0.6f), Noise(r, 0.025f, 0.5f, 4000f), 0f);
                case Sfx.Parry: return Mix(Tone(0.25f, 900f * vary, 0.35f, 14f), Mix(Tone(0.2f, 1370f * vary, 0.2f, 18f), Noise(r, 0.02f, 0.6f, 6000f), 0f), 0f);
                case Sfx.Whoosh: return Sweep(r, 0.2f * vary, 0.4f);
                case Sfx.Grab: return Noise(r, 0.12f, 0.35f, 1500f, attack: 0.03f);
                case Sfx.BodyFall: return Mix(Thud(0.3f, 60f * vary, 35f, 1f), Noise(r, 0.2f, 0.4f, 500f), 0f);
                case Sfx.Slam: return Mix(Thud(0.5f, 55f * vary, 28f, 1f), Noise(r, 0.35f, 0.6f, 700f), 0f);
                case Sfx.BoneCrack: return Crackle(r, 4, 0.12f, 0.8f);
                case Sfx.Smash: return Mix(Crackle(r, 10, 0.35f, 0.7f), Thud(0.25f, 90f, 50f, 0.7f), 0f);
                case Sfx.Glass: return Mix(Chimes(r, 0.45f), Crackle(r, 6, 0.2f, 0.4f), 0f);
                case Sfx.LidSlam: return Mix(Thud(0.15f, 180f * vary, 120f, 0.9f), Noise(r, 0.06f, 0.6f, 2500f), 0f);
                case Sfx.Splash: return Noise(r, 0.9f, 0.8f, 1800f, attack: 0.02f, sweepDown: true);
                default: return null;   // voices: only from recorded files
            }
        }

        // A punchy low "thump": a sine that drops in pitch with a fast decay.
        static float[] Thud(float seconds, float startHz, float endHz, float amp)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float hz = Mathf.Lerp(startHz, endHz, Mathf.Sqrt(t));
                phase += 2 * Mathf.PI * hz / Rate;
                float env = Mathf.Exp(-t * 7f) * Mathf.Min(1f, i / 40f);
                d[i] = (float)System.Math.Tanh(Mathf.Sin((float)phase) * 2.2f) * amp * env;
            }
            return d;
        }

        // Filtered noise burst (one-pole low-pass at roughly 'cutoff').
        static float[] Noise(System.Random r, float seconds, float amp, float cutoff, float attack = 0.002f, bool sweepDown = false)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            float y = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float c = sweepDown ? Mathf.Lerp(cutoff, cutoff * 0.15f, t) : cutoff;
                float a = 1f - Mathf.Exp(-2f * Mathf.PI * c / Rate);
                y += a * ((float)r.NextDouble() * 2f - 1f - y);
                float env = Mathf.Min(1f, i / (attack * Rate + 1f)) * Mathf.Exp(-t * (sweepDown ? 3f : 6f));
                d[i] = y * amp * env * 2.5f;
            }
            return d;
        }

        static float[] Tone(float seconds, float hz, float amp, float decay)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                d[i] = Mathf.Sin(2 * Mathf.PI * hz * t) * amp * Mathf.Exp(-t * decay);
            }
            return d;
        }

        // Swish of air: band of noise that rises then falls.
        static float[] Sweep(System.Random r, float seconds, float amp)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            float y = 0f, y2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float c = Mathf.Lerp(400f, 2500f, Mathf.Sin(t * Mathf.PI));
                float a = 1f - Mathf.Exp(-2f * Mathf.PI * c / Rate);
                y += a * ((float)r.NextDouble() * 2f - 1f - y);
                y2 += 0.02f * (y - y2);
                d[i] = (y - y2) * amp * Mathf.Sin(t * Mathf.PI) * 3f;
            }
            return d;
        }

        // Sharp clicks spread over a short time (bones, splintering wood).
        static float[] Crackle(System.Random r, int count, float seconds, float amp)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            for (int k = 0; k < count; k++)
            {
                int start = (int)(r.NextDouble() * n * 0.7);
                int len = 200 + r.Next(500);
                float a = amp * (0.5f + (float)r.NextDouble() * 0.5f);
                for (int i = 0; i < len && start + i < n; i++)
                    d[start + i] += ((float)r.NextDouble() * 2f - 1f) * a * Mathf.Exp(-i / (len * 0.25f));
            }
            return d;
        }

        static float[] Chimes(System.Random r, float seconds)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            for (int k = 0; k < 6; k++)
            {
                float hz = 2000f + (float)r.NextDouble() * 4000f;
                float delay = (float)r.NextDouble() * 0.08f;
                for (int i = (int)(delay * Rate); i < n; i++)
                {
                    float t = i / (float)Rate - delay;
                    d[i] += Mathf.Sin(2 * Mathf.PI * hz * t) * 0.12f * Mathf.Exp(-t * 12f);
                }
            }
            return d;
        }

        static float[] Mix(float[] a, float[] b, float bDelay)
        {
            int off = (int)(bDelay * Rate);
            var d = new float[Mathf.Max(a.Length, b.Length + off)];
            for (int i = 0; i < a.Length; i++) d[i] += a[i];
            for (int i = 0; i < b.Length; i++) d[i + off] += b[i];
            for (int i = 0; i < d.Length; i++) d[i] = Mathf.Clamp(d[i], -1f, 1f);
            return d;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MadrasGethu
{
    /// <summary>
    /// When each move's fist (or foot, or head) actually connects in its animation clip, measured by the
    /// Setup Characters tool. The game speeds each animation up or down so that moment lines up exactly
    /// with the hit, so nobody reacts before the punch arrives.
    /// Saved at Art/Generated/Resources/MadrasGethuTimings.asset and loaded automatically.
    /// </summary>
    public class AnimationTimings : ScriptableObject
    {
        public const string ResourceName = "MadrasGethuTimings";

        [Serializable]
        public class Entry
        {
            public string controller;   // e.g. HeroAnimator
            public string move;         // trigger name, e.g. Jab
            public float length;        // clip length in seconds
            public float impact;        // seconds into the clip where the hit connects (0 = unknown)
        }

        public List<Entry> entries = new List<Entry>();

        static AnimationTimings cached;
        static bool loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            cached = null;
            loaded = false;
        }

        public static AnimationTimings Load()
        {
            if (!loaded)
            {
                loaded = true;
                cached = Resources.Load<AnimationTimings>(ResourceName);
            }
            return cached;
        }
    }
}

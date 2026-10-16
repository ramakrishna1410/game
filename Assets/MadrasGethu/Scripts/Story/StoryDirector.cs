using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MadrasGethu
{
    /// <summary>
    /// Runs a story chapter: cutscenes (lines from the chapter's text file, automatic over-the-shoulder
    /// camera, gestures, voice files), free walking, fights with retries, objectives and tutorial prompts.
    /// Each chapter is a subclass that writes its sequence in Run() and handles its @markers.
    /// </summary>
    public abstract class StoryDirector : MonoBehaviour
    {
        public static StoryDirector Instance { get; private set; }

        /// <summary>The game HUD stays hidden while a cutscene is playing.</summary>
        public static bool HidesHud => Instance != null && Instance.inCutscene;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        public static void Create(GameObject host, int chapter)
        {
            switch (chapter)
            {
                case 1: host.AddComponent<Chapter1>(); break;
                default: Debug.LogWarning("[Madras Gethu] Chapter " + chapter + " is not written yet."); break;
            }
        }

        public static Vector3 PlayerStartFor(int chapter) => chapter == 1 ? Chapter1.HeroStart : new Vector3(0f, 0.1f, -6f);

        protected abstract string ChapterFile { get; }
        protected abstract IEnumerator Run();
        /// <summary>A @marker line in the script was reached.</summary>
        protected virtual IEnumerator Marker(string name, string arg) { yield break; }

        protected StoryScript script;
        protected StoryUI ui;
        protected GameManager gm;
        protected PlayerFighter player;
        protected StoryActor hero;
        protected ThirdPersonCamera cam;

        protected bool inCutscene;
        protected bool skipping;
        protected bool manualShot;
        protected bool playerDied;
        AudioSource voice;

        protected virtual void Awake()
        {
            Instance = this;
            voice = gameObject.AddComponent<AudioSource>();
            voice.spatialBlend = 0f;
            voice.playOnAwake = false;
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        IEnumerator Start()
        {
            gm = GameManager.Instance;
            ui = gameObject.AddComponent<StoryUI>();
            ui.Fade = 1f;          // chapters open from black
            inCutscene = true;     // and with the HUD hidden until the chapter decides
            script = StoryScript.Load(ChapterFile);
            yield return null;   // let the GameManager and the hero finish setting up
            player = gm.Player;
            hero = player.GetComponent<StoryActor>();
            cam = gm.Cam;
            gm.PlayerDiedInStory += () => playerDied = true;
            yield return Run();
        }

        protected virtual void Update()
        {
            if (inCutscene && !skipping && GameInput.StorySkipPressed) skipping = true;
        }

        // ------------------------------------------------------------------ cutscenes

        protected void BeginCutscene(bool bars = true)
        {
            inCutscene = true;
            skipping = false;
            manualShot = false;
            ui.Letterbox = bars;
            ui.ShowSkipHint = true;
            ui.TalkHint = null;
            player.EnterCutscene();
        }

        protected void EndCutscene()
        {
            inCutscene = false;
            skipping = false;
            manualShot = false;
            ui.Letterbox = false;
            ui.Line = null;
            ui.ShowSkipHint = false;
            player.cutsceneControlled = false;
            cam.SnapBehind(player.transform);
            cam.EndCinematic(0.05f);
        }

        /// <summary>Plays a scene from the chapter file, line by line.</summary>
        protected IEnumerator Scene(string id)
        {
            var entries = script.Scene(id);
            StoryActor lastSpeaker = null;
            int shot = 0;
            foreach (var e in entries)
            {
                if (e.IsMarker)
                {
                    if (e.marker == "pause") { yield return Wait(ParseFloat(e.markerArg, 1f)); continue; }
                    yield return Marker(e.marker, e.markerArg);
                    continue;
                }
                if (skipping) continue;

                var speaker = e.offScreen ? null : StoryActor.Find(e.speaker);
                var listener = ListenerFor(speaker, lastSpeaker);
                if (speaker != null)
                {
                    if (!manualShot) AutoShot(speaker, listener, speaker != lastSpeaker, shot++);
                    if (listener != null)
                    {
                        if (speaker.Directed && !speaker.Walking) speaker.Face(listener);
                        if (listener.Directed && !listener.Walking) listener.Face(speaker);
                    }
                    if (e.Has("point")) speaker.Gesture(FighterPose.Point);
                    else if (e.Has("wave")) speaker.Gesture(FighterPose.Wave);
                    else if (e.Has("talk")) speaker.Gesture(FighterPose.Talk, 3f);
                }

                float duration = ReadTime(e);
                var clip = VoiceClip(id, e.lineNumber);
                if (clip != null)
                {
                    voice.clip = clip;
                    voice.Play();
                    duration = clip.length + 0.35f;
                }

                ui.Line = e;
                ui.LineShownAt = Time.unscaledTime;
                ui.ShowNextHint = true;
                yield return null;
                while (!skipping)
                {
                    float shown = Time.unscaledTime - ui.LineShownAt;
                    if (shown > 0.3f && GameInput.StoryNextPressed) break;
                    if (shown > duration) break;
                    yield return null;
                }
                voice.Stop();
                ui.Line = null;
                if (speaker != null) lastSpeaker = speaker;
                if (!skipping) yield return new WaitForSecondsRealtime(0.12f);
            }
        }

        /// <summary>A short line during play (shouts in a fight). Doesn't stop anything.</summary>
        protected void Bark(string sceneId, int index)
        {
            var entries = script.Scene(sceneId);
            int n = 0;
            foreach (var e in entries)
            {
                if (e.IsMarker) continue;
                if (n++ != index) continue;
                ui.Bark = e;
                ui.BarkUntil = Time.unscaledTime + Mathf.Min(3.2f, ReadTime(e) * 0.75f);
                var clip = VoiceClip(sceneId, e.lineNumber);
                if (clip != null) { voice.clip = clip; voice.Play(); }
                return;
            }
        }

        protected int SpokenLines(string sceneId)
        {
            int n = 0;
            foreach (var e in script.Scene(sceneId)) if (!e.IsMarker) n++;
            return n;
        }

        AudioClip VoiceClip(string sceneId, int line) => Resources.Load<AudioClip>($"Voice/{ChapterFile}/{sceneId}_{line}");

        static float ReadTime(StoryEntry e)
        {
            int len = Mathf.Max(e.english != null ? e.english.Length : 0, (e.tamil != null ? e.tamil.Length : 0) * 3 / 4);
            return Mathf.Clamp(1.6f + len * 0.06f, 2.4f, 9f);
        }

        StoryActor ListenerFor(StoryActor speaker, StoryActor lastSpeaker)
        {
            if (speaker == null) return null;
            if (lastSpeaker != null && lastSpeaker != speaker && Close(lastSpeaker, speaker)) return lastSpeaker;
            if (speaker != hero && hero != null && Close(hero, speaker)) return hero;
            StoryActor best = null;
            float bestD = 7f;
            foreach (var a in StoryActor.All)
            {
                if (a == null || a == speaker || !a.gameObject.activeInHierarchy || !Visible(a)) continue;
                float d = Vector3.Distance(a.transform.position, speaker.transform.position);
                if (d < bestD) { bestD = d; best = a; }
            }
            return best;
        }

        static bool Close(StoryActor a, StoryActor b) => Vector3.Distance(a.transform.position, b.transform.position) < 7f && Visible(a);

        static bool Visible(StoryActor a)
        {
            var r = a.GetComponentInChildren<Renderer>();
            return r != null && r.enabled;
        }

        /// <summary>
        /// Film grammar: a two-shot to open, then over the listener's shoulder onto the speaker,
        /// always from the same side of the line between them (so nobody seems to swap places).
        /// </summary>
        void AutoShot(StoryActor speaker, StoryActor listener, bool cut, int index)
        {
            Vector3 sHead = speaker.Head;
            if (listener == null)
            {
                Vector3 fwd = speaker.transform.forward;
                Vector3 side = Vector3.Cross(Vector3.up, fwd);
                cam.CutsceneShot(sHead + fwd * 2.1f + side * 0.5f + Vector3.up * 0.05f, sHead - Vector3.up * 0.05f, 36f, cut);
                return;
            }
            Vector3 lHead = listener.Head;
            Vector3 axis = Flat(sHead - lHead);
            float dist = axis.magnitude;
            axis = dist > 0.01f ? axis / dist : Vector3.forward;
            Vector3 lineSide = SideOfLine(speaker, listener);

            if (index == 0 || index % 5 == 4)
            {
                Vector3 mid = (sHead + lHead) * 0.5f;
                float back = Mathf.Max(2.6f, dist * 1.4f);
                cam.CutsceneShot(mid + lineSide * back + Vector3.up * 0.15f, mid - Vector3.up * 0.15f, 40f, cut);
                return;
            }
            Vector3 pos = lHead - axis * 0.75f + lineSide * 0.45f + Vector3.up * 0.1f;
            cam.CutsceneShot(pos, sHead - Vector3.up * 0.06f, 34f, cut, 1.5f);
        }

        /// <summary>Which side of the pair the camera stays on: towards the open road and the water.</summary>
        static Vector3 SideOfLine(StoryActor a, StoryActor b)
        {
            // Order the pair so the side doesn't flip when they swap who's talking.
            if (string.CompareOrdinal(a.castId ?? "", b.castId ?? "") > 0) { var t = a; a = b; b = t; }
            Vector3 axis = Flat(b.transform.position - a.transform.position).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, axis);
            if (side.x < -0.05f || (Mathf.Abs(side.x) <= 0.05f && side.z < 0f)) side = -side;
            return side;
        }

        /// <summary>A shot set by the chapter: holds until AutoShots() (lines stop moving the camera).</summary>
        protected void Shot(Vector3 position, Vector3 lookAt, float fov, bool cut = true, float sharpness = -1f)
        {
            manualShot = true;
            cam.CutsceneShot(position, lookAt, fov, cut, sharpness);
        }

        protected void AutoShots() => manualShot = false;

        protected static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        // ------------------------------------------------------------------ helpers

        protected IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end && !skipping) yield return null;
        }

        protected IEnumerator Walk(StoryActor actor, Vector3 point, float speed = 1.5f)
        {
            if (actor == null) yield break;
            if (skipping) { actor.Teleport(point); yield break; }
            actor.WalkTo(point, speed);
            float giveUp = Time.time + Vector3.Distance(actor.transform.position, point) / Mathf.Max(0.3f, speed) * 2f + 1f;
            while (actor.Walking && !skipping && Time.time < giveUp) yield return null;
            if (actor.Walking || skipping) { actor.WalkTo(actor.transform.position); actor.Teleport(point); }
        }

        protected IEnumerator FadeTo(float target, float seconds)
        {
            if (skipping) seconds = Mathf.Min(seconds, 0.15f);
            float start = ui.Fade;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                ui.Fade = Mathf.Lerp(start, target, Mathf.Clamp01(t / seconds));
                yield return null;
            }
            ui.Fade = target;
        }

        /// <summary>A title card over black (chapter name). Space skips it.</summary>
        protected IEnumerator Card(string tamil, string english, string small, float hold)
        {
            ui.CardTamil = tamil;
            ui.CardEnglish = english;
            ui.CardSmall = small;
            for (float t = 0f; t < 0.7f; t += Time.unscaledDeltaTime) { ui.CardAlpha = t / 0.7f; yield return null; }
            ui.CardAlpha = 1f;
            float end = Time.unscaledTime + hold;
            yield return null;
            while (Time.unscaledTime < end && !GameInput.StoryNextPressed && !GameInput.StorySkipPressed) yield return null;
            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime) { ui.CardAlpha = 1f - t / 0.6f; yield return null; }
            ui.CardAlpha = 0f;
        }

        protected void Objective(string tamil, string english)
        {
            ui.ObjectiveTamil = tamil;
            ui.ObjectiveEnglish = english;
        }

        protected void Tutorial(string key, string tamil, string english)
        {
            if (ui.TutorialEnglish != english) ui.TutorialShownAt = Time.unscaledTime;
            ui.TutorialKey = key;
            ui.TutorialTamil = tamil;
            ui.TutorialEnglish = english;
        }

        protected void ClearTutorial() { ui.TutorialEnglish = null; ui.TutorialTamil = null; }

        /// <summary>Shows or hides a character (riding inside the auto, stepping into the jeep...).</summary>
        protected static void Show(StoryActor actor, bool on)
        {
            if (actor == null) return;
            foreach (var r in actor.GetComponentsInChildren<Renderer>(true)) r.enabled = on;
        }

        protected StoryActor Npc(string id, Vector3 position, Vector3 lookAt)
        {
            var a = CharacterFactory.CreateNpc(id, position, lookAt);
            return a;
        }

        /// <summary>True (once) if the hero went down since the last check.</summary>
        protected bool ConsumePlayerDied()
        {
            bool d = playerDied;
            playerDied = false;
            return d;
        }

        protected IEnumerator WaitForRetry()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            ui.Banner = "THOTHUTTA!";
            ui.BannerSmall = "You lost the street.  Press Enter to try the fight again";
            while (!GameInput.RestartPressed && !GameInput.StoryNextPressed) yield return null;
            ui.Banner = null;
            ui.BannerSmall = null;
        }

        static float ParseFloat(string s, float fallback) =>
            float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : fallback;

        // ------------------------------------------------------------------ sound

        /// <summary>A phone ringing (made in code: the two-tone "tring tring").</summary>
        protected void Ring(Vector3 at) => AudioSource.PlayClipAtPoint(StorySounds.Ring(), at, 0.9f);

        /// <summary>
        /// Harbour sound bed: your files in Resources/Audio/Ambience if there are any (GameAudio plays
        /// those), otherwise waves on the jetty made in code, with crows in the morning.
        /// </summary>
        protected void Ambience(bool morning)
        {
            if (Resources.LoadAll<AudioClip>("Audio/Ambience").Length > 0) return;
            if (ambienceSource == null)
            {
                ambienceSource = gameObject.AddComponent<AudioSource>();
                ambienceSource.spatialBlend = 0f;
                ambienceSource.loop = true;
                ambienceSource.volume = 0.55f;
            }
            ambienceSource.clip = StorySounds.Harbour(morning);
            ambienceSource.Play();
        }
        AudioSource ambienceSource;

        /// <summary>Plays Resources/Audio/Story/&lt;cue&gt; if that file exists (gaana for the auto ride...).</summary>
        protected void Music(string cue)
        {
            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.spatialBlend = 0f;
                musicSource.loop = true;
            }
            var clip = string.IsNullOrEmpty(cue) ? null : Resources.Load<AudioClip>("Audio/Story/" + cue);
            if (clip == null) { musicSource.Stop(); return; }
            musicSource.clip = clip;
            musicSource.volume = GameAudio.MusicVolume;
            musicSource.Play();
        }
        AudioSource musicSource;
    }
}

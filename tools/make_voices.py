"""AI voices for the story dialogue (placeholders until real Tamil voice actors record).

Reads Assets/MadrasGethu/Resources/Story/<chapter>.txt and writes one file per spoken line to
Assets/MadrasGethu/Resources/Voice/<chapter>/<scene>_<line>.wav/.mp3, which the game plays automatically.
Each line's 6th column ("direction": angry, teasing, voice breaking...) tells the AI how to act it.

Engines (all have free use; internet needed):
  gemini      Google Gemini TTS: acts out the directions, 30 voices. Free API key: https://aistudio.google.com/apikey
              The free tier has a daily limit: if it stops part-way, run it again later and it carries on.
  elevenlabs  ElevenLabs v3: the most natural. Free plan 10,000 characters/month (Chapter 1 is ~2,300),
              non-commercial with attribution. Key: elevenlabs.io > Profile > API keys. Voices: tools/voices.json
  edge        Microsoft Edge voices: unlimited, no key, but flat ("computer-like").

    python make_voices.py "E:\\UnityProjects\\MadrasGethu" --engine gemini --key YOUR_KEY
    (add --force to remake lines that already have a voice file, e.g. after changing engine)
"""
import argparse
import asyncio
import base64
import json
import os
import re
import sys
import time
import urllib.error
import urllib.request
import wave

HERE = os.path.dirname(os.path.abspath(__file__))

# Who each character is, for the AI's acting.
CHARACTERS = {
    "maaran":    "Maaran, a 28-year-old ex-boxer from North Chennai; deep, calm, low voice; a man of few words",
    "kutty":     "Kutty, a cheerful, fast-talking 28-year-old auto driver from Kasimedu, always joking",
    "ilango":    "a 45-year-old police inspector; smooth, polite, controlled, quietly in charge",
    "settu":     "Settu, a 40-year-old harbour gangster; slow, gravelly, smug and menacing",
    "pallu":     "Pallu, a 25-year-old street rowdy; rough, loud, cocky",
    "rowdy":     "a young street rowdy; rough and loud",
    "muniyandi": "Muniyandi, an old fisherman in his seventies; hoarse and kind",
    "boy":       "a 10-year-old boy from the fishing village",
    "announcer": "a boxing ring announcer",
    "selvi":     "Selvi, a 52-year-old working-class mother who runs an idli shop; warm, tired, strong",
    "kayal":     "Kayal, a 22-year-old nursing student; bright, caring, quick",
    "paati":     "an old fish-selling grandmother in her seventies; loud and grumpy",
}

# Gemini prebuilt voices (https://ai.google.dev/gemini-api/docs/speech-generation#voices)
GEMINI_VOICES = {
    "maaran": "Charon", "kutty": "Puck", "ilango": "Schedar", "settu": "Algenib", "pallu": "Fenrir",
    "rowdy": "Orus", "muniyandi": "Enceladus", "boy": "Leda", "announcer": "Sadaltager",
    "selvi": "Sulafat", "kayal": "Aoede", "paati": "Gacrux",
}
GEMINI_MODELS = ["gemini-2.5-flash-preview-tts", "gemini-3.1-flash-tts-preview", "gemini-2.5-flash-tts"]

# Edge voices: voice, speed, pitch
EDGE_VOICES = {
    "maaran": ("ta-IN-ValluvarNeural", "-6%", "-6Hz"), "kutty": ("ta-LK-KumarNeural", "+10%", "+4Hz"),
    "ilango": ("ta-SG-AnbuNeural", "-8%", "-3Hz"), "settu": ("ta-MY-SuryaNeural", "-12%", "-14Hz"),
    "pallu": ("ta-MY-SuryaNeural", "+6%", "-2Hz"), "rowdy": ("ta-SG-AnbuNeural", "+8%", "-8Hz"),
    "muniyandi": ("ta-LK-KumarNeural", "-18%", "-12Hz"), "boy": ("ta-MY-SuryaNeural", "+8%", "+28Hz"),
    "announcer": ("ta-IN-ValluvarNeural", "+12%", "+2Hz"), "selvi": ("ta-IN-PallaviNeural", "-6%", "-8Hz"),
    "kayal": ("ta-LK-SaranyaNeural", "+4%", "+2Hz"), "paati": ("ta-MY-KaniNeural", "-18%", "-14Hz"),
}


class OutOfQuota(Exception):
    pass


def find_resources(path):
    for c in (os.path.join(path, "Assets", "MadrasGethu", "Resources"), os.path.join(path, "MadrasGethu", "Resources"),
              os.path.join(path, "Resources"), path):
        if os.path.isdir(os.path.join(c, "Story")):
            return c
    sys.exit("Couldn't find Resources/Story under " + path + ". Pass your Unity project folder.")


def parse(text):
    """Same rules as StoryScript.cs."""
    out, scene, n = [], None, 0
    for raw in text.replace("\r", "").split("\n"):
        raw = raw.strip()
        if not raw or raw.startswith("#"):
            continue
        if raw.startswith("=="):
            scene, n = raw[2:].strip(), 0
            continue
        if scene is None or raw.startswith("@"):
            continue
        cols = [c.strip() for c in raw.split("|")]
        if len(cols) < 5:
            continue
        n += 1
        out.append({"scene": scene, "n": n, "speaker": cols[0].lower().rstrip("~"), "phone": cols[0].endswith("~"),
                    "tamil": cols[2], "english": cols[4], "direction": cols[5] if len(cols) > 5 else ""})
    return out


def post_json(url, body, headers, timeout=120):
    req = urllib.request.Request(url, data=json.dumps(body).encode("utf-8"),
                                 headers={"Content-Type": "application/json", **headers}, method="POST")
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return r.read()


# ---------------------------------------------------------------------------------------------- gemini

def gemini_line(line, key, state):
    who = CHARACTERS.get(line["speaker"], "a Chennai local")
    how = line["direction"] or "naturally"
    phone = " He is speaking on a phone." if line["phone"] else ""
    prompt = (f"You are a Tamil film dubbing artist voicing {who}.{phone} "
              f"Speak natural, colloquial Chennai Tamil (Madras bashai) with real emotion, like a scene in a Tamil movie, "
              f"never like a news reader. (The line means: \"{line['english']}\".) Say it {how}:\n{line['tamil']}")
    body = {"contents": [{"parts": [{"text": prompt}]}],
            "generationConfig": {"responseModalities": ["AUDIO"],
                                 "speechConfig": {"voiceConfig": {"prebuiltVoiceConfig": {
                                     "voiceName": GEMINI_VOICES.get(line["speaker"], "Charon")}}}}}
    for attempt in range(6):
        model = state.setdefault("model", GEMINI_MODELS[0])
        url = f"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={key}"
        try:
            data = json.loads(post_json(url, body, {}))
            part = data["candidates"][0]["content"]["parts"][0]
            return base64.b64decode(part["inlineData"]["data"])
        except urllib.error.HTTPError as e:
            msg = e.read().decode("utf-8", "ignore")
            if e.code == 404 and model in GEMINI_MODELS and GEMINI_MODELS.index(model) + 1 < len(GEMINI_MODELS):
                state["model"] = GEMINI_MODELS[GEMINI_MODELS.index(model) + 1]
                print(f"    model {model} not available, trying {state['model']}")
                continue
            if e.code == 429:
                if "PerDay" in msg or "per day" in msg.lower():
                    raise OutOfQuota("Gemini's free daily limit is used up. Run this again tomorrow: it carries on where it stopped.")
                wait = 30
                m = re.search(r'"retryDelay":\s*"(\d+)', msg)
                if m:
                    wait = int(m.group(1)) + 2
                print(f"    busy (free tier rate limit), waiting {wait}s...")
                time.sleep(wait)
                continue
            if e.code in (400, 401, 403) and ("API key" in msg or "PERMISSION" in msg):
                raise OutOfQuota("Gemini rejected the API key: " + msg[:300])
            raise RuntimeError(f"HTTP {e.code}: {msg[:300]}")
        except (KeyError, IndexError):
            time.sleep(3)   # an empty answer now and then: try again
    raise RuntimeError("no audio after several tries")


def save_wav(pcm, path, rate=24000):
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(pcm)


# ---------------------------------------------------------------------------------------------- elevenlabs

def elevenlabs_voices():
    path = os.path.join(HERE, "voices.json")
    with open(path, encoding="utf-8") as f:
        return json.load(f)["elevenlabs"]


def elevenlabs_line(line, key, voices):
    voice = voices.get(line["speaker"]) or voices.get("default")
    # v3 audio tags: the first two words of the direction, e.g. "[overjoyed] [laughing]"
    tags = " ".join(f"[{t.strip()}]" for t in line["direction"].split(",")[:2] if t.strip())
    body = {"text": (tags + " " + line["tamil"]).strip(), "model_id": "eleven_v3",
            "voice_settings": {"stability": 0.4, "similarity_boost": 0.8}}
    url = f"https://api.elevenlabs.io/v1/text-to-speech/{voice}?output_format=mp3_44100_128"
    for attempt in range(4):
        try:
            return post_json(url, body, {"xi-api-key": key})
        except urllib.error.HTTPError as e:
            msg = e.read().decode("utf-8", "ignore")
            if e.code == 429:
                time.sleep(15)
                continue
            if e.code == 401 or "quota" in msg.lower():
                raise OutOfQuota("ElevenLabs: " + msg[:300])
            raise RuntimeError(f"HTTP {e.code}: {msg[:300]}")
    raise RuntimeError("ElevenLabs kept saying busy")


# ---------------------------------------------------------------------------------------------- edge

async def edge_line(line, path):
    import edge_tts
    voice, rate, pitch = EDGE_VOICES.get(line["speaker"], ("ta-IN-ValluvarNeural", "+0%", "+0Hz"))
    await edge_tts.Communicate(line["tamil"].replace("...", "… "), voice, rate=rate, pitch=pitch).save(path)


# ---------------------------------------------------------------------------------------------- main

async def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("project", help="your Unity project folder (or Assets/MadrasGethu)")
    ap.add_argument("--engine", choices=["gemini", "elevenlabs", "edge"], default="gemini")
    ap.add_argument("--key", help="API key (or set GEMINI_API_KEY / ELEVENLABS_API_KEY)")
    ap.add_argument("--force", action="store_true", help="remake lines that already have a voice file")
    ap.add_argument("--chapter", help="only this chapter file, e.g. ch01")
    ap.add_argument("--only", help="only lines whose file name starts with this, e.g. 1.3_shakedown")
    args = ap.parse_args()

    key = args.key or os.environ.get("GEMINI_API_KEY" if args.engine == "gemini" else "ELEVENLABS_API_KEY", "")
    if args.engine != "edge" and not key:
        sys.exit("This engine needs an API key: --key YOUR_KEY")
    voices = elevenlabs_voices() if args.engine == "elevenlabs" else None
    if args.engine == "edge":
        try:
            import edge_tts  # noqa: F401
        except ImportError:
            sys.exit("Install it first:  pip install edge-tts")

    res = find_resources(args.project)
    story = os.path.join(res, "Story")
    state, made, skipped, failed = {}, 0, 0, 0
    try:
        for name in sorted(os.listdir(story)):
            if not name.endswith(".txt") or (args.chapter and name[:-4] != args.chapter):
                continue
            chapter = name[:-4]
            with open(os.path.join(story, name), encoding="utf-8") as f:
                lines = parse(f.read())
            out_dir = os.path.join(res, "Voice", chapter)
            os.makedirs(out_dir, exist_ok=True)
            print(f"{chapter}: {len(lines)} lines  ({args.engine})")
            for line in lines:
                base = os.path.join(out_dir, f"{line['scene']}_{line['n']}")
                if args.only and not os.path.basename(base).startswith(args.only):
                    continue
                existing = [base + e for e in (".wav", ".mp3", ".ogg") if os.path.exists(base + e)]
                if existing and not args.force:
                    skipped += 1
                    continue
                for old in existing:          # one file per line, or Unity can't tell which to play
                    os.remove(old)
                    if os.path.exists(old + ".meta"):
                        os.remove(old + ".meta")
                try:
                    if args.engine == "gemini":
                        save_wav(gemini_line(line, key, state), base + ".wav")
                        time.sleep(1.5)        # be gentle with the free tier
                    elif args.engine == "elevenlabs":
                        with open(base + ".mp3", "wb") as f:
                            f.write(elevenlabs_line(line, key, voices))
                    else:
                        await edge_line(line, base + ".mp3")
                    made += 1
                    print(f"  {line['scene']}_{line['n']:<3} {line['speaker']:10s} {line['english']}")
                except OutOfQuota:
                    raise
                except Exception as e:
                    failed += 1
                    print(f"  {line['scene']}_{line['n']}  FAILED: {e}")
    except OutOfQuota as e:
        print("\n" + str(e))
    print(f"\nDone: {made} made, {skipped} already had a voice, {failed} failed.")
    print("Switch back to Unity: the files import by themselves. Play the chapter to hear them.")


if __name__ == "__main__":
    asyncio.run(main())

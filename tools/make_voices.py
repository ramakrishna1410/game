"""Placeholder Tamil voices for the story dialogue (until real voice actors record them).

Reads Assets/MadrasGethu/Resources/Story/<chapter>.txt and writes one MP3 per spoken line to
Assets/MadrasGethu/Resources/Voice/<chapter>/<scene>_<line>.mp3, which the game plays automatically.
Uses Microsoft's neural Tamil voices through the free `edge-tts` package (needs internet).

    pip install edge-tts
    python make_voices.py "E:\\UnityProjects\\MadrasGethu"            (all chapters)
    python make_voices.py "E:\\UnityProjects\\MadrasGethu" --force    (redo lines that already have a file)

Each character gets their own voice, speed and pitch (VOICES below). Lines you've already
recorded yourself are left alone unless you use --force.
"""
import argparse
import asyncio
import os
import sys

# Eight Tamil neural voices exist (India, Sri Lanka, Malaysia, Singapore); pitch and speed make the rest.
VOICES = {
    # id          voice                    speed   pitch
    "maaran":    ("ta-IN-ValluvarNeural", "-6%",  "-6Hz"),
    "kutty":     ("ta-LK-KumarNeural",    "+10%", "+4Hz"),
    "ilango":    ("ta-SG-AnbuNeural",     "-8%",  "-3Hz"),
    "settu":     ("ta-MY-SuryaNeural",    "-12%", "-14Hz"),
    "pallu":     ("ta-MY-SuryaNeural",    "+6%",  "-2Hz"),
    "rowdy":     ("ta-SG-AnbuNeural",     "+8%",  "-8Hz"),
    "muniyandi": ("ta-LK-KumarNeural",    "-18%", "-12Hz"),
    "boy":       ("ta-MY-SuryaNeural",    "+8%",  "+28Hz"),
    "announcer": ("ta-IN-ValluvarNeural", "+12%", "+2Hz"),
    "selvi":     ("ta-IN-PallaviNeural",  "-6%",  "-8Hz"),
    "kayal":     ("ta-LK-SaranyaNeural",  "+4%",  "+2Hz"),
    "paati":     ("ta-MY-KaniNeural",     "-18%", "-14Hz"),
}
DEFAULT = ("ta-IN-ValluvarNeural", "+0%", "+0Hz")


def find_story_folder(path):
    for candidate in (
        os.path.join(path, "Assets", "MadrasGethu", "Resources"),
        os.path.join(path, "MadrasGethu", "Resources"),
        os.path.join(path, "Resources"),
        path,
    ):
        if os.path.isdir(os.path.join(candidate, "Story")):
            return candidate
    sys.exit("Couldn't find Resources/Story under " + path + ". Pass your Unity project folder.")


def parse(text):
    """Same rules as StoryScript.cs: '== scene', '@marker', 'speaker | gesture | Tamil | Tanglish | English'."""
    lines, scene, n = [], None, 0
    for raw in text.replace("\r", "").split("\n"):
        raw = raw.strip()
        if not raw or raw.startswith("#"):
            continue
        if raw.startswith("=="):
            scene, n = raw[2:].strip(), 0
            continue
        if scene is None or raw.startswith("@"):
            continue
        cols = raw.split("|")
        if len(cols) < 5:
            continue
        n += 1
        speaker = cols[0].strip().lower().rstrip("~")
        lines.append((scene, n, speaker, cols[2].strip()))
    return lines


async def speak(text, speaker, out):
    import edge_tts
    voice, rate, pitch = VOICES.get(speaker, DEFAULT)
    # Ellipses read as long silences: keep them short.
    text = text.replace("...", "… ")
    await edge_tts.Communicate(text, voice, rate=rate, pitch=pitch).save(out)


async def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("project", help="your Unity project folder (or Assets/MadrasGethu)")
    ap.add_argument("--force", action="store_true", help="remake lines that already have a voice file")
    ap.add_argument("--chapter", help="only this chapter file, e.g. ch01")
    args = ap.parse_args()

    try:
        import edge_tts  # noqa: F401
    except ImportError:
        sys.exit("Install it first:  pip install edge-tts")

    res = find_story_folder(args.project)
    story = os.path.join(res, "Story")
    made = skipped = failed = 0
    for name in sorted(os.listdir(story)):
        if not name.endswith(".txt"):
            continue
        chapter = name[:-4]
        if args.chapter and chapter != args.chapter:
            continue
        with open(os.path.join(story, name), encoding="utf-8") as f:
            entries = parse(f.read())
        out_dir = os.path.join(res, "Voice", chapter)
        os.makedirs(out_dir, exist_ok=True)
        print(f"{chapter}: {len(entries)} lines")
        for scene, n, speaker, tamil in entries:
            base = os.path.join(out_dir, f"{scene}_{n}")
            if not args.force and any(os.path.exists(base + ext) for ext in (".mp3", ".ogg", ".wav")):
                skipped += 1
                continue
            try:
                await speak(tamil, speaker, base + ".mp3")
                made += 1
                print(f"  {scene}_{n}  {speaker:10s} {tamil}")
            except Exception as e:  # keep going: one bad line shouldn't stop the rest
                failed += 1
                print(f"  {scene}_{n}  FAILED: {e}")
    print(f"\nDone: {made} made, {skipped} already there, {failed} failed.")
    print("Back in Unity the files import by themselves; play the chapter to hear them.")


if __name__ == "__main__":
    asyncio.run(main())

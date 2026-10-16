# Reference voices for IndicF5 (offline AI voices)

IndicF5 copies **the voice and the acting** of a short recording. Record one clip per character and
the AI speaks all of that character's lines in that voice, with that energy.

## What to record

Each `.txt` file here holds one Tamil sentence. Read it **in character** and save the recording next to it
with the same name, as a **.wav** file: `maaran.txt` → `maaran.wav`.

| File | Who / how to say it |
|---|---|
| maaran | calm, low, a man of few words |
| maaran_angry | the same voice, angry (used for lines whose direction says "angry") |
| kutty | lively, fast, joking auto driver |
| ilango | smooth, polite police inspector |
| settu | slow, deep, menacing gangster |
| pallu | rough, loud, cocky rowdy |
| rowdy | any rough young rowdy |
| selvi | warm, tired mother |
| selvi_sad | the same voice, sad, close to tears |
| kayal | bright, caring young woman |
| muniyandi | old, hoarse fisherman |
| paati | loud, grumpy old woman |
| boy | excited young boy |

You can add more moods for anyone: `kutty_happy.wav` + `kutty_happy.txt`, `settu_menacing.wav`...
(moods understood: angry, sad, happy, scared, shout, whisper, laugh, tease, menacing, soft).
A mood clip is used when a line's direction in the dialogue file asks for that mood
(e.g. "voice breaking" or "hurt" picks `_sad`, "mocking" or "joking" picks `_tease`).

## How to record

- 5 to 10 seconds each, one clean take, no music or fan noise, phone or laptop mic held close.
- Say **exactly** what's in the .txt (or change the .txt to what you actually said).
- Free tool: **Audacity** (audacityteam.org): record, then *File → Export Audio → WAV*, mono.
  (Windows Sound Recorder saves .m4a: convert it to .wav in Audacity.)
- One person can voice several characters by changing their voice; friends and family make it better.

**Only record people who agree to it.** Don't clone actors, celebrities or anyone without their permission
(that's also in IndicF5's terms of use).

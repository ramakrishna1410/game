# Sound effects and music

The game plays sounds for every hit, block, parry, grab, swing, body fall, slam, bone crack, prop smash,
bottle break, ice box lid and splash. **Until you add your own files it uses placeholder sounds generated
in code**, so it's never silent. Real recordings sound far better: drop them in and they replace the
placeholders automatically (no setup step needed).

## Where to put files

`Assets/MadrasGethu/Resources/Audio/<Name>/` — one folder per sound. Put **2–4 variations** in each folder
(e.g. `punch1.wav`, `punch2.wav`, `punch3.wav`); the game picks one at random each time and varies the pitch
slightly. WAV, OGG or MP3 all work.

| Folder | What it is | Search for (Freesound.org / Kenney.nl / Pixabay) |
|---|---|---|
| `PunchLight` | jab / cross landing | "punch", "face punch", "hit flesh" |
| `PunchHeavy` | haymaker, hook, big punches | "heavy punch", "body hit hard" |
| `Kick` | kicks landing | "kick impact", "hit thud" |
| `BodyBlow` | body shots, knees | "stomach punch", "body blow" |
| `Block` | blow stopped by a guard | "block punch", "arm block" |
| `Parry` | parry / deflect | "swoosh hit", "parry", "whip crack" (short) |
| `Whoosh` | a swing through the air | "whoosh", "swing", "punch swoosh" |
| `Grab` | grabbing a collar | "cloth grab", "clothes rustle" |
| `BodyFall` | body hitting the road | "body fall", "body drop", "thud" |
| `Slam` | wrestling slam landing | "body slam", "heavy impact", "ground hit" |
| `BoneCrack` | big finishers | "bone crack", "bone break", "crunch" |
| `Smash` | tea kadai / crates / auto breaking | "wood break", "crate smash" |
| `Glass` | bottle breaking | "bottle smash", "glass break" |
| `LidSlam` | ice box lid | "plastic lid slam", "cooler lid" |
| `Splash` | Kadal Throw | "big splash", "body into water" |
| `Grunt` | rowdy getting hurt (voice) | "male grunt pain", "oof" — or record your own "Aah!", "Amma!" |
| `Shout` | rowdy shouting (voice) | record "Dei!", "Vaada!", "Adi da avana!" |
| `Cheer` | crowd cheer on big moves (optional) | "crowd cheer", "crowd oh" |

Voices (`Grunt`, `Shout`, `Cheer`) have no placeholder: they only play once you add files. Recording your
friends shouting the Madras Bashai lines from the game (`Scripts/Enemy/Shouts.cs`) works great.

## Music and ambience

- `Resources/Audio/Music/` — background track(s), looped. One is picked at random.
- `Resources/Audio/Ambience/` — harbour night ambience (waves, distant boats, crowd), looped under the music.

Volumes: `GameAudio.SfxVolume` (1.0) and `GameAudio.MusicVolume` (0.35) in `Scripts/Core/GameAudio.cs`.

Check each file's licence (CC0 is easiest; CC-BY needs a credit line).

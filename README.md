# Madras Gethu

A third-person street brawler set in **North Chennai**, in the spirit of *Sleeping Dogs* and *Yakuza*.
You play **Maaran**, a former Royapuram boxer who comes home to Kasimedu harbour and takes on the gang that now runs it.

This is the **first playable combat prototype**, built in **Unity 6 (URP)**. All characters and buildings are placeholder blocks, so you can play it right now with no art. Real models and animations come later (see [docs/MIXAMO_ANIMATIONS.md](docs/MIXAMO_ANIMATIONS.md)).

## What's in this prototype

- Third-person orbit camera that avoids walls, with screen shake, hit-stop and slow-motion
- **North Chennai boxing**: jab → cross → hook → body blow, with a different **heavy ender** at each step of the combo (haymaker, kick, uppercut launcher, spinning backfist, rush finisher)
- **Block** and Sleeping Dogs-style **counters**: tap block when an enemy flashes red
- **Dodge** with invincibility frames
- **Grab**: knee strikes, throws into other rowdies, and dragging enemies around
- **Environmental finishers**: drag a grabbed rowdy to the **tea kadai**, the **fish crates (meen petti)** or a **parked auto** and press Gethu
- **Gethu meter** (heat): fills as you fight stylishly. Spend it on finishers, or fill it completely for **Gethu Mode** (stronger hits, no flinching, half damage)
- **Combo specials**: shoulder barge, air hammer, charged haymaker, leg sweep, shoulder rush, clothesline, back elbow and **wall bounces**
- **Combo grades**: *Nalla → Semma → Mass → Thalaivaa*, each raising the Gethu you earn
- **Dazed** rowdies (charged haymaker, wall bounce, or worn down): they sway on their feet and can be grabbed, even the Thadiyan
- **Wrestling finishers with quick-time prompts**: Body Slam, Kasimedu Spin Drop, German Suplex, Powerbomb, Spear, Elbow Drop and Spinebuster. Press as the ring meets the gold circle for **PERFECT**; a miss never cancels the move
- **Key-cap button prompts**, a **hand icon** over grabbable rowdies (flashing when dazed, crossed out when too heavy) and a **skull** when a finisher is ready
- Moves are registered for a future **skill tree** (`MoveUnlocks`): everything is unlocked for now
- **Group AI**: rowdies circle you and take turns attacking, at most 2 at once
- 3 enemy types: **Rowdy**, **Thadiyan** (big bruiser, can't be grabbed, unblockable slam) and the boss **Kumaran Anna**
- 3 waves, ending in a boss fight, at a night-time Kasimedu fishing harbour (godowns, boats, sodium lamps)

## Setup on your laptop (Windows)

The prototype is light. It runs fine on integrated Intel Arc graphics with 16 GB of RAM.

1. **Install Unity Hub** from <https://unity.com/download>.
2. In Unity Hub → **Installs** → **Install Editor** → pick the latest **Unity 6 LTS** (6000.x).
   Include the module **Microsoft Visual Studio Community** (for editing C#). You can skip the Android, iOS and WebGL modules to save disk space.
3. Unity Hub → **Projects** → **New project** → choose the **Universal 3D** template → name it `MadrasGethu` → **Create**.
4. **Get this code into the project.** Pick one:
   - **Easy way:** on GitHub, open this repository, switch to the branch, click **Code → Download ZIP**, unzip it, and copy the `Assets/MadrasGethu` folder into your project's `Assets` folder.
   - **Git way** (lets you pull future updates): open a terminal in the project folder and run:
     ```bash
     git init
     git remote add origin https://github.com/ramakrishna1410/game.git
     git fetch origin
     git checkout -f claude/nice-johnson-n0dydi
     ```
5. Go back to Unity and wait for it to finish compiling. If it asks to **enable the new Input System backends**, click **Yes** (Unity restarts).
6. In the top menu, click **Madras Gethu → Create Kasimedu Arena Scene**.
7. Press **Play** ▶. Click inside the Game view to capture the mouse.

> Tip: keep the laptop plugged in and in performance mode while working in Unity.
> For glowing street lamps, add **GameObject → Volume → Global Volume**, create a new profile, and add a **Bloom** override.

## Practice Yard

A separate scene to learn the moves on **one dummy that never moves, never attacks and never dies**.

1. In Unity: **Madras Gethu → Create Practice Yard Scene** (it copies your characters from the arena scene).
2. Press **Play**.

| Key | What it does |
|---|---|
| R | Reset the dummy to his mark (also repairs props and puts the weapons back) |
| T | Switch dummy: Rowdy → Thadiyan → Boss |
| G | Infinite Gethu on/off (on by default, so you can practise every finisher) |
| L | Show/hide the move list |
| Y | Add / remove a **second dummy** (for Mutta Mothal, Bowling Throw, Giant Swing and the Double Counter) |
| K | **Sparring** on/off: the dummies fight back so you can practise counters (you still take no damage) |
| J | Give the dummies weapons (lathi / pipe) to practise the Weapon Steal |

The left panel lists every move with its input; each one ticks green the first time it lands. The top right shows the last hit, the damage of your current string and the last move. Smashed props rebuild themselves after a few seconds. The yard has a brick wall (wall slams, wall bounces, spinebuster), a tea kadai, fish crates, an auto, an ice box, a jetty edge onto the harbour (east side, for the Kadal Throw) and a weapon rack.

Use **Madras Gethu → Open Practice Yard Scene** later, or **Open Kasimedu Arena Scene** to go back to the waves.

## Sound

Every hit, block, parry, grab, swing, fall, slam, bone crack, prop smash, bottle, ice box lid and splash has a sound. Until you add recordings the game uses placeholder sounds made in code. Drop your own files into `Assets/MadrasGethu/Resources/Audio/<Name>/` (several per folder for variety) and they're used automatically. Music and harbour ambience go in `Audio/Music` and `Audio/Ambience`. See **[docs/AUDIO.md](docs/AUDIO.md)** for the folder names and what to search for on Freesound / Kenney.

Blows also get **hit reactions** on top of the animation: a hook snaps his head to the side, a straight punch snaps it back, an uppercut throws it up, a body shot folds him over.

## Story and Tamil dialogue

The story, cast and chapter plan are in **[docs/STORY.md](docs/STORY.md)**. Scene-by-scene scripts in Tamil, Tanglish and English are in [docs/script](docs/script).

**Tamil text test:** Tamil letters need shaping (vowel signs and joined letters), which not every Unity text system does. To check yours:
1. Copy `Assets/MadrasGethu/Resources/Fonts` as well as `Scripts` and `Editor`.
2. Click **Madras Gethu → Tamil Text Test**. It makes a test scene and presses Play.
3. The screen shows the same Tamil lines four ways (A, B, C) plus a correct reference picture (D). Take a screenshot.

The font is Noto Sans Tamil (free, SIL Open Font License; see `Resources/Fonts/NotoSansTamil-OFL.txt`).

## Ragdolls and hit timing

With real (Mixamo) characters:
- Knocked-down, thrown, slammed and defeated fighters go **limp and fall under physics** (ragdoll), slump against walls, and get up from however they landed (on their back or face down).
- **Hits land when the fist or foot arrives.** *Setup Characters* measures the contact frame of every attack animation, and the game times each animation to it. **Run Madras Gethu → Setup Characters (Mixamo) again** after updating, so the timings are measured.

## Finisher camera

Wrestling and prop finishers switch to a **close-up cinematic camera**: a low angle looking up at the lift, side-on for the throw, street level for the slam, overhead for the elbow drop. It pushes in slowly while a prompt waits, punches in on impact and holds on the landing in slow motion before easing back. To turn it off, untick **Cinematic Finishers** on the Main Camera's *Third Person Camera* while playing.

## Controls

Keyboard and mouse for now (gamepad prompts come later).

| Action | Keys |
|---|---|
| Move / Sprint | WASD / Shift |
| Camera | Mouse |
| Light attack (combo) | Left click |
| Heavy attack (combo ender, or **stomp** a downed enemy) | Right click |
| **Charged haymaker** (dazes, breaks guard) | hold Right click, release |
| Block (hold) | F |
| **Parry** (when the blue F shows) | F (tap) |
| Jump (then click for a **flying kick**) | Space |
| Dodge: sidestep / backstep when squared up, roll otherwise (direction + key) | C or Ctrl |
| Grab → combos / throw / shove / drag fast | E → LMB/RMB clicks / E / F / Shift |
| **Gethu**: wrestling finisher while grabbing, prop finisher near a prop, elbow drop on a downed rowdy, spear while sprinting, or Gethu Mode when full | Q |
| Quick-time prompts during a wrestling finisher | the key shown (Left click, Right click or Space) |
| Pick up a weapon (stand on it) / with a weapon: grab the rowdy in front one-handed, or throw it | E |
| Break free when a rowdy grabs you | tap E fast |
| Spare a begging rowdy (hit him to finish him) | E |
| Show/hide controls | H |
| Restart after win/lose | Enter |
| Free the mouse | Esc |

### Combos

| Input | Move |
|---|---|
| RMB | Haymaker (hold for a **Charged Haymaker**) |
| LMB RMB | Kasimedu Kick |
| LMB RMB RMB RMB | Kick → Shoulder Barge → Kasimedu Haymaker |
| LMB LMB RMB | Uppercut (launches) |
| LMB LMB RMB RMB | Uppercut → Air Hammer |
| LMB ×3 RMB / LMB ×4 RMB | Spinning Backfist / Royapuram Rush Finisher |
| any other heavy → RMB | Double Hammer |
| back + RMB | Leg Sweep (everyone around you) |
| forward + RMB from out of reach | Shoulder Rush |
| sprint + LMB / sprint + RMB | Running Knee / Clothesline |
| LMB with a rowdy right behind you | Back Elbow |
| dodge → LMB, jump → LMB | Pivot Backfist, Flying Kick |

### Street moves

| Input | Move |
|---|---|
| dodge → RMB | **Spinning Heel Kick**: spin out of the dodge, heel to the jaw, he drops |
| jump → RMB | **Superman Punch**: a hop forward into a flying cross |
| S (stepping away from him) + LMB, then LMB | **Kaal Odaippu**: low kick drops him to one knee, then a knee to the face |
| sprint + E at a rowdy | **Rugby Tackle**: take him down and sit on him; LMB rains punches, RMB finishes |

### Counters

A **perfect parry** (tap F just as the blue F shows) slows time; your next button picks the counter.

| Input | Counter |
|---|---|
| perfect parry, nothing | **Counter Punch** |
| perfect parry, **LMB** | **Kai Odaippu**: catch his arm, elbow into the joint, short punch; he can't attack for a while |
| perfect parry, **E** | **Counter Throw**: hip in, throw him over your back |
| perfect parry, **Q** (1 Gethu bar) | **Counter Finisher**: twist his wrist to his knees, headbutt (LMB on the ring), knee to the face |
| perfect parry against a lathi / pipe | **Weapon Steal**: yank it out of his hands and hit him with it; you keep it |
| **E** as his grab arrives (yellow) | **Grab Reversal**: knock his hands aside and grab him instead |
| tap **E** to break free when held | **Escape Strike**: elbow / headbutt / knee on the way out; he's left dazed |
| dodge (C) a Thadiyan charge, then **RMB** | **Matador**: trip him as he runs past, face-first |
| perfect parry with two punching at once | **Double Counter**: catch both fists, duck, their heads meet |

In the Practice Yard press **K** to make the dummies fight back (punches, grabs, holds, charges as Thadiyan) and **J** to give them a lathi / pipe. **Y** adds a second dummy for the double counter.

### Holding him (grab with E first)

| Input | Move |
|---|---|
| E with another rowdy close by | **Mutta Mothal**: smash his head into his friend's, both go down |
| hold RMB | **Arm Twist**: crank his arm, knee the elbow; he drops his weapon and can't attack for a while |
| LMB then F | **Ear Clap**: both palms on his ears; when you let go he reels about, dizzy |
| S + F | **Trip & Stomp**: hook his leg, drop him on his back, stamp on him |
| Shift + E | **Bowling Throw**: hurl him low along the road into a group |

### Grab combos (grab with E, then click, no QTE)

| Clicks after the grab | Combo |
|---|---|
| LMB LMB (LMB) | **Royapuram Rush**: two punches holding his collar; the third click lets go with an uppercut that lifts him off his feet |
| keep tapping LMB fast | **Machine Gun Kuthu**: rapid punches to the face holding the collar, then both fists into the body; RMB finishes (haymaker / uppercut) |
| RMB RMB | **Kasimedu Kick Storm**: shove, front kick to the stomach, roundhouse to the head |
| LMB RMB | **Thalaivar Special**: headbutt, knee to the gut, spinning backfist |
| RMB LMB | **Jallikattu**: push kick, run in, flying knee to the face |
| LMB + RMB together (full Gethu) | **Gethu Mass Combo**: jab, cross, body kick, hook, knee, dropkick |
| one click on its own | a single punch (LMB) or knee (RMB), and you keep hold of him |

Hold **Shift** while dragging someone to hurry him along (to a tea kadai or the auto). Throwing is **E** only.

### Wrestling finishers (Q, costs Gethu)

| When | Finisher | Prompts |
|---|---|---|
| Holding him, **Q** | Body Slam (scoop him across your chest, quarter turn, slam) | LMB scoop → RMB slam |
| Holding him, **S + Q** | Kasimedu Spin Drop (duck under, carry him across both shoulders, spin once, drop him face-first) | LMB → LMB → RMB |
| Holding him from behind, **Q** | German Suplex (waist lock, hip lifts, bridge back) | LMB ×3 hip lifts → RMB |
| Holding him facing a wall, **W + Q** | Spinebuster | LMB → RMB |
| Holding him, **Shift + Q** with full Gethu | Powerbomb (shockwave) | LMB → Space → RMB |
| Sprinting at a rowdy | Spear | RMB |
| Rowdy lying on the ground | Elbow Drop (leap, land on him elbow-first, roll off) | Space → LMB |
| Holding him, **A + Q** | Choke-Out (spin him round, forearm across his throat; he sinks to his knees and goes limp) | LMB ×4 squeezes |
| Holding him, **W + Q** (no wall in front) | Thai Clinch Knees (pull his head down, three knees to the face, elbow to the back of the neck) | LMB ×3 knees → RMB elbow |
| Holding him, **D + Q** with full Gethu | Giant Swing (his ankles, three spins; his body knocks down anyone close; let go) | LMB → Space → RMB |
| Holding him at the **jetty edge**, **Q** | Kadal Throw (lift him and heave him into the harbour) | LMB → RMB |
| Holding him at a fish market **ice box**, **Q** | Ice Box Slam (head in, slam the lid on him twice) | LMB → RMB → RMB |

Each costs one Gethu bar (the Powerbomb and Giant Swing cost the full meter). Near a tea kadai, fish crates or the auto, **Q** lifts him and smashes him into it.

During every lift Maaran's hands stay locked on the rowdy (IK) and his body crouches, drives up, arches or follows him down; the rowdy goes limp (ragdoll) on landing. **E** while holding swings him round by the collar and throws him. Every **PERFECT** adds damage and a little Gethu; all PERFECT = **FLAWLESS**.

### Weapons (pick one up with E)

Each weapon fights its own way. The **Q** finishers cost one Gethu bar.

| Weapon | LMB | RMB | Q |
|---|---|---|---|
| **Lathi** (Silambam) | Strike x3: the third is **Silambam Strikes** | tap: thrust to the gut, then **Lathi Overhead** · hold: **Silambam Sutru** (360° spin, hits everyone around, unblockable) | **Silambam Finish**: sweep him onto his back, twirl, crack him on the ground (RMB as it comes down = PERFECT) |
| **Iron Pipe** | Swing, then **Pipe Crusher** | tap: **Knee Breaker** (he drops to one knee) · hold and release: **Golf Swing** (more damage if he's dazed) | **Pipe Choke**: pipe across his throat in both hands, tap LMB, he sinks to his knees |
| **Plank** | **Plank Sweep** (wide, hits several) | **Plank Smash** (it splinters) | **Plank Break** across his back |
| **Bottle** | **Bottle Smash** (dazes; it breaks) | same | **Bottle Finisher**: collar and bottle |
| **Brick** | **Brick Punch** | throw it: **Brick Headshot** dazes | **Brick Drop** on a rowdy lying on the ground |
| **Plastic Chair** | Swing | **Chair Smash** (it breaks) | **Chair Break** across his back |
| **Cycle Chain** | **Chain Whip** (long reach) | **Chain Wrap**: round his neck and pull him into your grab | none |
| **Fish** | **Meen Adi** slap | big Meen Adi (spins him off his feet) | none |
| **Tyre** | **Tyre Trap**: drop it over his shoulders, arms pinned, dazed for 4 s | same | none |

Also with a weapon in hand:
- **F** with a lathi, pipe or plank is a **Weapon Block**: it stops even orange (unblockable) swings, but not a charge.
- **E** on a rowdy in front grabs him one-handed. LMB / RMB then hits him with the weapon (**Weapon Grab Strike**).

### Rowdy smarts

The rowdies fight back with their heads, but every trick has a warning and a way out:
- **They read you.** Throw the same string three times in a row and they block it ("HE'S READING YOU!"). Heavies, grabs, counters and orange moves still get through, and a heavy straight after a read block breaks his guard for extra damage. Mix it up and they're open again.
- **Grab escape.** A fresh rowdy may shove your grab off (25%, the boss 50%). A dazed or staggered rowdy, or one grabbed from behind, never escapes.
- **No free holds.** Hold someone for more than 1.5 s and a friend comes for you from behind with a red **!**. Throw him, use him (Mutta Mothal), start a finisher, or press **LMB** to let go and elbow the newcomer. Finishers and grab combos stay safe; the others close in and wait for them to end.
- **Flanking.** One rowdy at a time may circle behind you before attacking. A red **!** over him and a red arrow round Maaran show where he is; parry (F) or dodge (C).

**Enemy warning colours:** 🔵 the blue **F** box means parry; **orange** means unblockable, so dodge (C); **yellow** means a grab, so dodge, or tap E to break free if he catches you.

**How to fight:**
- Mix lights and heavies. The number of light hits before a heavy decides which heavy ender you get.
- Watch for the **red flash**. Counter it or dodge it.
- Grab rowdies, drag them to a **tea kadai, fish crates or auto**, and press **Q** for a finisher.
- **Thadiyan** can't be grabbed until you stagger him (heavies break his guard), and his overhead slam is **unblockable**.

## Project layout

```
Assets/MadrasGethu/
  Scripts/
    Core/          GameInput, GameManager (waves, hit-stop, slow-mo), GameBootstrap, CharacterFactory, Palette
    Combat/        Fighter base, FighterMotor (movement), FighterVisual (placeholder body + animation hooks),
                   AttackData/HitInfo, CombatUtil (hit detection, auto-targeting), FX
    Player/        PlayerFighter: combos, block/counter, dodge, grabs, finishers, Gethu meter
                   PlayerWrestling: wrestling finishers and QTE prompts · MoveUnlocks: skill-tree hooks
    Enemy/         EnemyFighter AI, EnemyProfile (stats per enemy type), EnemyDirector (attack turns)
    World/         KasimeduArenaBuilder (placeholder level), PracticeYardBuilder, FinisherSpot (tea kadai / crates / auto)
    CameraSystem/  ThirdPersonCamera
    UI/            GameHUD (health, Gethu, combo grades, button prompts, QTE ring), HudIcons (key caps, hand, skull)
  Editor/          "Madras Gethu" menu: creates the arena scene
docs/
  GAME_DESIGN.md        the full Chennai game concept and roadmap
  MIXAMO_ANIMATIONS.md  how to swap the placeholder bodies for real animated characters
```

## Tuning

- Combo moves and damage: `PlayerFighter.BuildDefaultMoves()`
- Wrestling finishers (timing, poses, damage, QTE keys): `PlayerWrestling.cs`
- Which moves are unlocked (for the future skill tree): `MoveUnlocks.cs`
- Enemy stats and moves: `EnemyProfile.cs`
- How many rowdies attack at once: `EnemyDirector.MaxAttackers`
- Waves: `GameBootstrap.AddWaves()`
- Arena layout: `KasimeduArenaBuilder.cs`

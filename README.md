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

## Controls

| Action | Keyboard / Mouse | Gamepad (Xbox) |
|---|---|---|
| Move / Sprint | WASD / Shift | Left stick / LB |
| Camera | Mouse | Right stick |
| Light attack (combo) | Left click | X |
| Heavy attack (combo ender, or **stomp** a downed enemy) | Right click | Y |
| Block (hold) | F | RB |
| **Counter** (when an enemy flashes red) | F (tap) | RB (tap) |
| Jump (then click for a **flying kick**) | Space | A |
| Dodge: sidestep / backstep when squared up, roll otherwise (direction + key) | C or Ctrl | LT |
| Grab → knee / throw | E → Left click / E or Right click | B → X / B or Y |
| **Gethu**: finisher while grabbing near a prop, or Gethu Mode when full | Q | RT |
| Pick up a weapon (stand on it) / throw the weapon you hold | E | B |
| Break free when a rowdy grabs you | tap E fast | tap B fast |
| Spare a begging rowdy (hit him to finish him) | E | B |
| Show/hide controls | H | View/Back |
| Restart after win/lose | Enter | Start |
| Free the mouse | Esc | |

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
    Enemy/         EnemyFighter AI, EnemyProfile (stats per enemy type), EnemyDirector (attack turns)
    World/         KasimeduArenaBuilder (placeholder level), FinisherSpot (tea kadai / crates / auto)
    CameraSystem/  ThirdPersonCamera
    UI/            GameHUD (health, Gethu, combo counter, banners)
  Editor/          "Madras Gethu" menu: creates the arena scene
docs/
  GAME_DESIGN.md        the full Chennai game concept and roadmap
  MIXAMO_ANIMATIONS.md  how to swap the placeholder bodies for real animated characters
```

## Tuning

- Combo moves and damage: `PlayerFighter.BuildDefaultMoves()`
- Enemy stats and moves: `EnemyProfile.cs`
- How many rowdies attack at once: `EnemyDirector.MaxAttackers`
- Waves: `GameBootstrap.AddWaves()`
- Arena layout: `KasimeduArenaBuilder.cs`

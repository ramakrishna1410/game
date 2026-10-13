# Real characters and animations (Mixamo)

The game ships with block-body placeholders. This guide replaces them with semi-realistic Mixamo characters and fighting animations.
You download the files and **one menu click** does the rest: **Madras Gethu → Setup Characters (Mixamo)**.

## 1. Sign in to Mixamo
<https://www.mixamo.com>: it's free, using an Adobe account (Google sign-in works).

## 2. Download the characters ("With Skin")
**Characters** tab → pick a character → **Download** with:
- Format: **FBX for Unity (.fbx)**
- Pose: **T-pose**

| Save as | Who | Suggestion |
|---|---|---|
| `Hero.fbx` | Maaran (the player) | a fit, realistic male in casual clothes |
| `Rowdy.fbx` | the rowdies | a different realistic male (street clothes / thug) |
| `Thadiyan.fbx` *(optional)* | the big bruiser | a heavy-built male. The game scales him up 25% |
| `Boss.fbx` *(optional)* | Kumaran Anna | someone older or better dressed |

Put them in: `Assets/MadrasGethu/Art/Characters/`
(The menu **Madras Gethu → Open Art Folder** creates and opens this folder for you.)

## 3. Download the animations ("Without Skin")
Keep the **Hero** character selected (top right), go to the **Animations** tab, search for each one, click it, then **Download** with:
- Format: **FBX for Unity (.fbx)**
- Skin: **Without Skin**
- If an **"In Place"** checkbox appears (walking, running, some kicks), **tick it**

Save each file with the name in the first column into `Assets/MadrasGethu/Art/Animations/`.

### Must have (5)
| Save as | Search Mixamo for |
|---|---|
| `Idle.fbx` | Idle |
| `Walk.fbx` | Walking (In Place) |
| `Run.fbx` | Running (In Place) |
| `Guard.fbx` | Boxing Idle *(or "Fight Idle")* |
| `Jab.fbx` | Jab *(or "Lead Jab")* |

### Fight-stance footwork (recommended: makes circling and waiting look real)
| Save as | Search Mixamo for |
|---|---|
| `WalkBack.fbx` | Walking Backwards *(or "Boxing Step Backward")* |
| `StrafeLeft.fbx` | Left Strafe Walking *(or "Boxing Step Left")* |
| `StrafeRight.fbx` | Right Strafe Walking *(or "Boxing Step Right")* |
| `Taunt.fbx` | Taunt *(waiting rowdies use it now and then)* |

### Combat (recommended)
| Save as | Search Mixamo for |
|---|---|
| `Cross.fbx` | Cross Punch |
| `Hook.fbx` | Hook Punch |
| `BodyBlow.fbx` | Body Jab Cross *(or any body punch)* |
| `Haymaker.fbx` | Punching *(a big swinging punch)* |
| `Uppercut.fbx` | Uppercut |
| `Kick.fbx` | Mma Kick *(or Front Kick)* |
| `Block.fbx` | Center Block *(or Body Block)* |
| `Dodge.fbx` | Dodging *(or a side step)* |
| `Flinch.fbx` | Head Hit *(or "Hit Reaction")* |
| `Knockdown.fbx` | Knocked Down *(or "Falling Back")* |
| `GetUp.fbx` | Getting Up *(or "Stand Up")* |
| `Dead.fbx` | Dying |

### Grab, shove and parry (recommended)
| Save as | Search Mixamo for | Stand-in if missing |
|---|---|---|
| `Grab.fbx` | Grab / Holding (hero holding someone) | Guard |
| `Held.fbx` | Struggling (rowdy being held) | Flinch |
| `Push.fbx` | Push / Shove | Haymaker |
| `Headbutt.fbx` | Headbutt | Cross |
| `Parry.fbx` | Outward Block / Center Block | Block |
| `StaggerBack.fbx` | Stumble Backwards | Flinch |

The hero's hands are placed on the rowdy's collar by IK whatever the Grab animation does, so any calm holding pose works.

### Jump and dodges (recommended)
| Save as | Search Mixamo for | Stand-in if missing |
|---|---|---|
| `Jump.fbx` | Jump *(tick In Place)* | none |
| `FlyingKick.fbx` | Flying Kick / Drop Kick | Kick |
| `DodgeLeft.fbx` | Dodging Left / side step left | Dodge |
| `DodgeRight.fbx` | Dodging Right / side step right | Dodge |
| `DodgeBack.fbx` | Dodging Back / back step / hop back | Dodge |
| `Roll.fbx` | Stand To Roll / Quick Roll | Dodge |

### Extras (optional; the tool uses stand-ins if missing)
| Save as | Search Mixamo for | Stand-in if missing |
|---|---|---|
| `Backfist.fbx` | Spinning backfist / any spin attack | Hook |
| `Stomp.fbx` | Stomping | Kick |
| `Knee.fbx` | Knee strike | Kick |
| `Counter.fbx` | any fast counter punch | Cross |
| `Throw.fbx` | Throw | Haymaker |
| `Slam.fbx` | Overhead slam / two-hand smash | Haymaker |
| `Grab.fbx` | Grab / holding | Guard |
| `Windup.fbx` | Taunt / charge-up (rowdy wind-up) | none |

> Exact Mixamo names vary. Pick whatever looks closest. Only the **file name** matters.
> The tool also recognises Mixamo's own names (e.g. `Boxing Idle.fbx`, `Hook Punch.fbx`), but renaming to the first column is safest.

## 4. Run the tool
1. Make sure the arena scene exists (**Madras Gethu → Create Kasimedu Arena Scene**, which you've already done).
2. Click **Madras Gethu → Setup Characters (Mixamo)**.
3. Wait for the progress bar. A report shows which animations were found, which use stand-ins, and which are missing.
4. Press **Play**.

What the tool does:
- Sets every FBX to **Humanoid** (so all animations work on all characters), and makes the animations play in place.
- Extracts the textures so skin and clothes show up.
- Builds `Art/Generated/FighterAnimator.controller`: walk/run blend trees, a fight stance, every attack and reaction, block/hold/held states, and knockdown/get-up/death.
- Creates `Hero.prefab`, `Rowdy.prefab`, … in `Art/Generated/` and plugs them into **Game Bootstrap** in the arena scene.

You can run it again any time you add or replace files.

## Tuning
- **Animation too slow or fast?** Open `Art/Generated/FighterAnimator.controller`, click the state (e.g. *Jab*) and change **Speed**. Lights default to about 1.7x, because Mixamo punches are slower than game punches.
  *(Re-running the tool rebuilds the controller and resets these values. Change the defaults in `CharacterSetupTool.cs` to keep them.)*
- **Hit lands before the fist does?** Raise that move's `startup` in `PlayerFighter.BuildDefaultMoves()`.
- **Character floating or sunk?** Select the prefab in `Art/Generated/` and adjust the child model's Y position.

## Notes
- The red "attack coming" flash and white hit flash tint the real models too.
- Models get no colliders. The game's CharacterController handles collisions.
- Mixamo characters are Western-looking. For a Tamil look (lungi, veshti, banian, moustache), the next step is customising them in **Blender**, or using **Character Creator** (paid) and exporting to Unity. The same tool works on those FBX files.

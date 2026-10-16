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

**Story cast (optional, for Story: Chapter 1).** Any other character file is a story character, matched by its file name:

| Save as | Who | Suggestion |
|---|---|---|
| `Kutty.fbx` | Maaran's friend, an auto driver | a slim, cheerful male in a shirt |
| `Selvi.fbx` | Maaran's mother, runs the idli shop | an older woman (no saree on Mixamo yet; pick the most modest, everyday outfit) |
| `Kayal.fbx` | his sister, a nursing student | a young woman in casual clothes |
| `Ilango.fbx` | the police inspector | a male in a uniform-like khaki or plain shirt |
| `Pallu.fbx` | the rowdy who leads the shakedown | a rough-looking male, different from Rowdy |
| `Muniyandi.fbx`, `Paati.fbx`, `Boy.fbx` | harbour people you can talk to | an old man, an old woman, a boy |

Anyone without a file uses a placeholder body in their colours (the men use the Rowdy model).

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
| `GetUp.fbx` | Getting Up *(starts lying on the back)* |
| `GetUpFront.fbx` | Stand Up / Getting Up *(pick one that starts lying **face down**; preview it in Mixamo)* |
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

### Combo specials and wrestling finishers (optional; stand-ins are used if missing)
Put these in `Art/Animations/` (or `Art/Animations/Hero/`). The rowdy's body is lifted, flipped and slammed by the game itself, so the hero's animation only has to look like he's lifting or driving down.

| Save as | Search Mixamo for | Stand-in if missing |
|---|---|---|
| `ShoulderBarge.fbx` | Shoulder Hit / Shoulder Bash | Push |
| `LegSweep.fbx` | Leg Sweep | Kick |
| `Clothesline.fbx` | Clothesline / Lariat (or a wide arm swing) | Haymaker |
| `BackElbow.fbx` | Elbow Strike / Elbow Punch | Backfist |
| `Lift.fbx` | Lifting (Box Lifting) | PickUp |
| `Suplex.fbx` | Back Flip / Bridge (arching backwards) | Throw |
| `Spear.fbx` | Football Tackle | Charge |
| `ElbowDrop.fbx` | Elbow Drop / Jump Down | Stomp |

### Grab combo kicks (strongly recommended)
The grab combos use these. Without them they borrow `Kick.fbx`, which looks wrong if your Kick is a jumping kick.
Exact Mixamo names change; search the words below and pick one that stays on the ground (except the flying knee). Tick **In Place** when offered.

| Save as | Search Mixamo for | Used in |
|---|---|---|
| `FrontKick.fbx` | "Front Kick" / "Snap Kick" | Kasimedu Kick Storm, Gethu Mass Combo |
| `Roundhouse.fbx` | "Roundhouse Kick" / "Mma Kick" (high kick) | Kasimedu Kick Storm |
| `PushKick.fbx` | "Push Kick" / a front kick that shoves | Jallikattu |
| `FlyingKnee.fbx` | "Flying Knee" / "Jumping Knee" | Jallikattu |
| `Knee.fbx` | "Knee Strike" (standing) | Gethu Mass Combo (the combo knees are also done in code) |
| `Backfist.fbx` | "Spinning Backfist" / any backhand (the game adds the spin) | Thalaivar Special |
| `Uppercut.fbx`, `Haymaker.fbx`, `Hook.fbx`, `BodyBlow.fbx` | see the Combat table above | Royapuram Rush, Machine Gun Kuthu |
| `FlyingKick.fbx` | "Drop Kick" / "Flying Kick" | Gethu Mass Combo dropkick |

### Street moves (recommended)
Without these the moves borrow the animation in the last column.

| Save as | Search Mixamo for | Used in | Borrows |
|---|---|---|---|
| `SpinKick.fbx` | "Spinning Kick" / "Hurricane Kick" / "Spin Kick" | Spinning Heel Kick | Roundhouse, Kick |
| `SupermanPunch.fbx` | "Jumping Punch" / "Superman Punch" | Superman Punch | Haymaker |
| `LowKick.fbx` | "Low Kick" / "Leg Kick" (to the shin or knee) | Kaal Odaippu | Kick |
| `Tackle.fbx` | "Tackle" / "Takedown" / "Rugby Tackle" | Rugby Tackle | Spear |
| `GroundPunch.fbx` | "Punching Down" / "Ground Punch" (punching someone on the floor) | Rugby Tackle mount punches | Cross |

For enemies (into `Art/Animations/Rowdy/`, or `Art/Animations/` for everyone):

| Save as | Search Mixamo for | Used for | Borrows |
|---|---|---|---|
| `Kneel.fbx` | "Kneeling Down" / "Hurt Kneel" / "Falling To Knee" | dropped to one knee by the low kick | Beg |
| `GutHit.fbx` | "Stomach Hit" / "Gut Punch" reaction / "Kidney Hit" | folded over by the liver shot | Flinch |

Choke-Out, Thai Clinch Knees, Giant Swing, Kadal Throw, Ice Box Slam, Arm Twist and Ear Clap need no new files: the hands, posture and the rowdy's body are placed in code like the other wrestling moves.

For enemies, into `Art/Animations/Rowdy/`:

| Save as | Search Mixamo for | Used for |
|---|---|---|
| `Dazed.fbx` | Dizzy Idle / Stunned | swaying, seeing stars (open to grabs and finishers) |

> Exact Mixamo names vary. Pick whatever looks closest. Only the **file name** matters.
> The tool also recognises Mixamo's own names (e.g. `Boxing Idle.fbx`, `Hook Punch.fbx`), but renaming to the first column is safest.

## Enemy-only animations (separate folders)
Enemies now fight differently from the hero. The folders are:

```
Art/Animations/            shared by everyone (Idle, Walk, Run, Guard, Flinch, Knockdown, GetUp, Dead, ...)
Art/Animations/Hero/       hero-only versions (optional; overrides the shared files for the hero)
Art/Animations/Rowdy/      enemy moves (the Thadiyan and the boss also use these)
Art/Animations/Thadiyan/   bruiser-only overrides
Art/Animations/Boss/       boss-only overrides
```

Download these with a **Rowdy** character selected in Mixamo (Without Skin) into `Art/Animations/Rowdy/`:

| Save as | Search Mixamo for | Used for |
|---|---|---|
| `RowdySwing.fbx` | Punching (a sloppy, wide swing) | wild swing |
| `RowdyKick.fbx` | Kick / front kick | stomach kick |
| `RowdyShove.fbx` | Push | shove |
| `RowdyGrab.fbx` | Grab / reaching | grabbing the hero |
| `Taunt.fbx` | Taunt | waiting rowdies |
| `ThrowObject.fbx` | Throw (overhand) | throwing bottles and bricks |
| `PickUp.fbx` | Picking Up | picking up a weapon |
| `WeaponSwing.fbx` | Sword Slash / bat swing | lathi / pipe swing |
| `WeaponSmash.fbx` | Great Sword Slash / overhead | weapon overhead smash |
| `Beg.fbx` | Kneeling / pleading | last rowdy begging for mercy |
| `Guard.fbx` | Drunk Idle / loose Fight Idle | a street stance instead of the hero's boxing guard |

Into `Art/Animations/Thadiyan/` (Mixamo's **Mutant** set suits him):

| Save as | Search Mixamo for |
|---|---|
| `Charge.fbx` | Mutant Run / tackle (In Place) |
| `GroundPound.fbx` | Mutant Jump Attack |
| `BearHug.fbx` | Mutant Swiping / grab |
| `Roar.fbx` | Mutant Roaring |
| `RowdySwing.fbx` | Mutant Punch |

Into `Art/Animations/Boss/`: a boxing **Jab**, **Hook**, **Kick**, `Roar.fbx` (Yelling) and `WeaponSwing.fbx` for his iron pipe.

Into `Art/Animations/Hero/` (optional): `WeaponSwing.fbx`, `WeaponSmash.fbx`, `PickUp.fbx`, `ThrowObject.fbx` if you want the hero to handle weapons differently from the rowdies.

Anything missing falls back to a similar shared move, so the game always works.

### Story cutscenes (recommended for Story mode; put them in `Art/Animations/`)

| Save as | Mixamo search | Used for |
|---|---|---|
| `Talking.fbx` | "Talking" | hand gestures while someone speaks |
| `Pointing.fbx` | "Pointing" | "point" lines (Pallu, Kutty, Kayal) |
| `Waving.fbx` | "Waving" | Kutty waving from the auto |
| `Sitting Idle.fbx` | "Sitting Idle" | Maaran on the bench in Amma's kitchen |
| `Talking On Phone.fbx` | "Talking On Phone" | Settu's phone call (arms only, he can still turn) |
| `Sad Idle.fbx` *(optional)* | "Sad Idle" | for later chapters |

Without them the characters just stand and talk; without *Sitting Idle* Maaran stands at the counter.

## 4. Run the tool
1. Make sure the arena scene exists (**Madras Gethu → Create Kasimedu Arena Scene**, which you've already done).
2. Click **Madras Gethu → Setup Characters (Mixamo)**.
3. Wait for the progress bar. A report shows which animations were found, which use stand-ins, and which are missing.
4. Press **Play**.

What the tool does:
- Sets every FBX to **Humanoid** (so all animations work on all characters), and makes the animations play in place.
- Extracts the textures so skin and clothes show up.
- Builds `Art/Generated/FighterAnimator.controller`: walk/run blend trees, a fight stance, every attack and reaction, block/hold/held states, and knockdown/get-up/death.
- Creates `Hero.prefab`, `Rowdy.prefab`, … in `Art/Generated/` and plugs them into **Game Bootstrap** in the arena scene (and the Practice Yard and Chapter 1 scenes, if they exist). Story cast files (Kutty, Selvi, ...) go into its **Cast Models** list.

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

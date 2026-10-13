# Replacing the placeholder bodies with real animated characters

The prototype draws block-bodies and animates them in code. When you're ready for real fighters, you can use **Mixamo** (free, from Adobe) for characters and animations.

## 1. Download from Mixamo
1. Go to <https://www.mixamo.com> and sign in with a free Adobe account.
2. **Characters** tab: pick a character, or upload your own (e.g. a Chennai rowdy made in Blender or Character Creator).
3. **Animations** tab: search for and download each of these (exact names vary, so pick the closest match). Use format **FBX for Unity**, and choose **With Skin** for the first one and **Without Skin** for the rest:
   - `Boxing Idle` (Guard), `Idle`, `Running`
   - `Jab`, `Cross Punch`, `Hook Punch`, `Body Jab Cross` (BodyBlow)
   - `Punching` or a big swing (Haymaker), `Uppercut`, `Mma Kick` (Kick), `Spinning Backfist` (Backfist), `Stomping` (Stomp)
   - `Block`, `Dodging` (Dodge), `Reaction`/`Hit To Body` (Flinch)
   - `Knocked Down` (Knockdown), `Getting Up` (GetUp), `Dying` (Dead)
   - `Grab`, `Knee`, `Throw`, `Counter` (any quick punch)

## 2. Import into Unity
1. Drag the FBX files into `Assets/MadrasGethu/Art/Characters/`.
2. Select each FBX → **Rig** tab → **Animation Type: Humanoid** → Apply.
   (For the animation-only files, set **Avatar Definition: Copy From Other Avatar** and choose the character's avatar.)
3. Select each animation → **Animation** tab → tick **Loop Time** for Idle, Boxing Idle and Running only.

## 3. Animator Controller
Create **Assets → Create → Animator Controller** named `FighterAnimator`, then add these **parameters**:

| Parameter | Type | Set by the code when… |
|---|---|---|
| `Speed` | Float (0–1) | moving |
| `Guard` | Bool | enemies are close (fighting stance) |
| `Jab`, `Cross`, `Hook`, `BodyBlow`, `Haymaker`, `Uppercut`, `Backfist`, `Kick`, `Stomp` | Trigger | attacking |
| `Grab`, `Knee`, `Throw`, `Slam`, `Counter`, `Dodge`, `Block` | Trigger | the matching action |
| `Flinch`, `Knockdown`, `GetUp`, `Dead`, `Windup` | Trigger | getting hit / enemy telegraph |

The trigger names match the `FighterPose` enum in `Scripts/Combat/CombatTypes.cs` exactly. You only need to add the ones you have animations for; missing triggers are skipped.

Then set up the states:
- A **Locomotion** blend tree on `Speed` (Idle → Running), with a **Guard** version that uses Boxing Idle.
- From **Any State**, a transition to each attack/reaction state on its trigger (no exit time, ~0.05s blend), with each state returning to Locomotion when it finishes.
- **Untick "Apply Root Motion"** on the Animator. The code moves the characters.

## 4. Hook it up
1. Drag the character FBX into a scene, add an **Animator** with `FighterAnimator`, and save it as a prefab (e.g. `Maaran.prefab`). Make sure its feet are at the prefab's origin and it faces +Z.
2. Select the **Game Bootstrap** object in the Kasimedu Arena scene:
   - **Player Model Prefab** → your hero prefab
   - **Enemy Model Prefab** → a rowdy prefab
3. Press Play. The placeholder blocks are replaced by your models, and every move fires its Animator trigger.

Notes:
- Combat timing is still controlled in code (`startup`/`recovery` in `AttackData`). If an animation's hit frame doesn't line up, either tweak `startup` or speed up the animation state.
- The models should not have colliders. The CharacterController handles collisions.
- The red attack-warning flash and the white hit flash only tint the placeholder bodies for now. With real models, the enemy's health bar still turns red during the wind-up. Adding a material tint or outline for models is a later task.

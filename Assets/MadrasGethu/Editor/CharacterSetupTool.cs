using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MadrasGethu.EditorTools
{
    /// <summary>
    /// Madras Gethu > Setup Characters
    ///
    /// Turns Mixamo downloads into ready-to-fight characters in one click:
    ///   Assets/MadrasGethu/Art/Characters/Hero.fbx, Rowdy.fbx, Thadiyan.fbx, Boss.fbx   (With Skin)
    ///   Assets/MadrasGethu/Art/Animations/Idle.fbx, Jab.fbx, ...                           (Without Skin)
    ///
    /// It sets every FBX to Humanoid, makes the animations play in place, builds the Animator
    /// Controller the combat code expects, creates a prefab per character and plugs them into
    /// the Game Bootstrap of the Kasimedu Arena scene. Missing animations fall back to similar ones.
    /// </summary>
    public static class CharacterSetupTool
    {
        const string ArtRoot = "Assets/MadrasGethu/Art";
        const string CharactersFolder = ArtRoot + "/Characters";
        const string AnimationsFolder = ArtRoot + "/Animations";
        const string GeneratedFolder = ArtRoot + "/Generated";
        const string ControllerPath = GeneratedFolder + "/FighterAnimator.controller";
        const string ArenaScenePath = "Assets/MadrasGethu/Scenes/KasimeduArena.unity";

        enum SlotKind { Locomotion, Trigger, HoldBool, Final }

        class Slot
        {
            public string name;
            public SlotKind kind;
            public string[] aliases;
            public string[] fallbacks;
            public float speed = 1f;
            public string boolParameter;   // for HoldBool slots
            public AnimationClip clip;     // the clip actually used (own file or fallback)
            public bool ownFile;
        }

        static Slot L(string n, params string[] aliases) =>
            new Slot { name = n, kind = SlotKind.Locomotion, aliases = aliases, fallbacks = new string[0] };

        static Slot T(string n, float speed, string[] aliases, params string[] fallbacks) =>
            new Slot { name = n, kind = SlotKind.Trigger, speed = speed, aliases = aliases, fallbacks = fallbacks };

        static List<Slot> CreateSlots()
        {
            return new List<Slot>
            {
                L("Idle", "idle", "breathingidle", "standingidle", "happyidle"),
                L("Walk", "walk", "walking"),
                L("Run", "run", "running", "jog", "jogging", "fastrun"),
                new Slot { name = "Guard", kind = SlotKind.Locomotion, aliases = new[] { "guard", "boxingidle", "fightidle", "fightingidle", "bouncingfightidle" }, fallbacks = new[] { "Idle" } },

                T("Jab", 1.7f, new[] { "jab", "leadjab", "jabpunch" }, "Cross", "Hook"),
                T("Cross", 1.7f, new[] { "cross", "crosspunch", "punch", "punching" }, "Jab", "Hook"),
                T("Hook", 1.6f, new[] { "hook", "hookpunch", "leadhook" }, "Cross", "Jab"),
                T("BodyBlow", 1.6f, new[] { "bodyblow", "bodyjab", "bodyjabcross", "bodypunch" }, "Hook", "Cross"),
                T("Haymaker", 1.3f, new[] { "haymaker", "bigpunch", "punchcombo" }, "Hook", "Cross"),
                T("Uppercut", 1.4f, new[] { "uppercut" }, "Hook", "Cross"),
                T("Backfist", 1.4f, new[] { "backfist", "spinningbackfist", "spinbackfist" }, "Hook", "Haymaker"),
                T("Kick", 1.4f, new[] { "kick", "mmakick", "frontkick", "roundhousekick", "legkick" }),
                T("Stomp", 1.3f, new[] { "stomp", "stomping" }, "Kick"),
                T("Knee", 1.5f, new[] { "knee", "kneestrike", "kneekick" }, "Kick"),
                T("Throw", 1.3f, new[] { "throw", "throwing", "toss" }, "Haymaker", "Hook"),
                T("Slam", 1.3f, new[] { "slam", "overheadslam", "doublehandslam" }, "Haymaker", "Uppercut"),
                T("Counter", 1.6f, new[] { "counter", "counterpunch" }, "Cross", "Haymaker"),
                T("Dodge", 1.4f, new[] { "dodge", "dodging", "sidestep", "roll" }),
                T("Flinch", 1.3f, new[] { "flinch", "hitreaction", "reaction", "headhit", "hit", "takinghit" }),
                T("Windup", 1f, new[] { "windup", "taunt", "chargeup" }),
                T("GetUp", 1.3f, new[] { "getup", "gettingup", "standup", "standingup" }),

                new Slot { name = "Block", kind = SlotKind.HoldBool, boolParameter = "Blocking", aliases = new[] { "block", "blocking", "centerblock", "bodyblock" }, fallbacks = new[] { "Guard" } },
                new Slot { name = "Grab", kind = SlotKind.HoldBool, boolParameter = "Holding", aliases = new[] { "grab", "grabbing", "hold", "holding" }, fallbacks = new[] { "Guard" } },
                new Slot { name = "Held", kind = SlotKind.HoldBool, boolParameter = "Held", aliases = new[] { "held", "beingheld", "choked", "struggle" }, fallbacks = new[] { "Flinch" } },

                new Slot { name = "Knockdown", kind = SlotKind.Final, aliases = new[] { "knockdown", "knockeddown", "fallingback", "falldown" }, fallbacks = new[] { "Dead" } },
                new Slot { name = "Dead", kind = SlotKind.Final, aliases = new[] { "dead", "dying", "death", "fallingbackdeath" }, fallbacks = new[] { "Knockdown" } },
            };
        }

        static readonly string[] CharacterNames = { "Hero", "Rowdy", "Thadiyan", "Boss" };

        // ------------------------------------------------------------------ menu

        [MenuItem("Madras Gethu/Setup Characters (Mixamo)", priority = 20)]
        public static void SetupCharacters()
        {
            EnsureFolder(ArtRoot);
            EnsureFolder(CharactersFolder);
            EnsureFolder(AnimationsFolder);
            EnsureFolder(GeneratedFolder);

            var characterFiles = FindFbx(CharactersFolder);
            if (characterFiles.Count == 0)
            {
                EditorUtility.DisplayDialog("Madras Gethu",
                    "No characters found.\n\nPut your Mixamo character FBX files (downloaded \"With Skin\") in\n" +
                    CharactersFolder + "\nnamed Hero.fbx, Rowdy.fbx (and optionally Thadiyan.fbx, Boss.fbx),\n" +
                    "and the animations in\n" + AnimationsFolder + "\n\nThen run this again.", "OK");
                return;
            }

            string heroPath = characterFiles.FirstOrDefault(p => Normalize(Path.GetFileNameWithoutExtension(p)) == "hero")
                              ?? characterFiles[0];

            try
            {
                // 1. Characters
                EditorUtility.DisplayProgressBar("Madras Gethu", "Setting up characters...", 0.1f);
                var avatars = new Dictionary<string, Avatar>();
                foreach (var path in characterFiles)
                {
                    var avatar = SetupCharacterModel(path);
                    if (avatar == null)
                    {
                        EditorUtility.ClearProgressBar();
                        EditorUtility.DisplayDialog("Madras Gethu",
                            "Could not create a Humanoid avatar for\n" + path +
                            "\n\nMake sure it is a Mixamo character downloaded as \"FBX for Unity\" With Skin.", "OK");
                        return;
                    }
                    avatars[path] = avatar;
                }
                var heroAvatar = avatars[heroPath];

                // 2. Animations
                var slots = CreateSlots();
                var animationFiles = FindFbx(AnimationsFolder);
                var unmatched = new List<string>();
                for (int i = 0; i < animationFiles.Count; i++)
                {
                    string path = animationFiles[i];
                    EditorUtility.DisplayProgressBar("Madras Gethu", "Importing " + Path.GetFileName(path),
                        0.2f + 0.5f * i / Mathf.Max(1, animationFiles.Count));
                    var slot = MatchSlot(slots, Path.GetFileNameWithoutExtension(path));
                    if (slot == null || slot.ownFile)
                    {
                        unmatched.Add(Path.GetFileName(path));
                        continue;
                    }
                    slot.clip = SetupAnimation(path, slot, heroAvatar);
                    slot.ownFile = slot.clip != null;
                }
                ResolveFallbacks(slots);

                var idle = slots.First(s => s.name == "Idle");
                if (idle.clip == null)
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("Madras Gethu",
                        "An Idle animation is required.\n\nDownload Mixamo \"Idle\" (Without Skin) and save it as\n" +
                        AnimationsFolder + "/Idle.fbx", "OK");
                    return;
                }

                // 3. Animator Controller
                EditorUtility.DisplayProgressBar("Madras Gethu", "Building Animator Controller...", 0.75f);
                var controller = BuildController(slots);

                // 4. Prefabs
                EditorUtility.DisplayProgressBar("Madras Gethu", "Creating prefabs...", 0.85f);
                var prefabs = new Dictionary<string, GameObject>();
                foreach (var path in characterFiles)
                {
                    string name = Path.GetFileNameWithoutExtension(path);
                    prefabs[Normalize(name)] = CreatePrefab(path, name, controller);
                }

                // 5. Plug into the arena scene
                EditorUtility.DisplayProgressBar("Madras Gethu", "Updating the arena scene...", 0.95f);
                string sceneResult = AssignToBootstrap(prefabs, heroPath, controller);

                EditorUtility.ClearProgressBar();
                ShowReport(slots, unmatched, characterFiles, sceneResult);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Madras Gethu: setup failed",
                    "Something went wrong:\n\n" + e.Message + "\n\nThe full error is in the Console. Send a screenshot of it.", "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Madras Gethu/Open Art Folder", priority = 21)]
        public static void OpenArtFolder()
        {
            EnsureFolder(ArtRoot);
            EnsureFolder(CharactersFolder);
            EnsureFolder(AnimationsFolder);
            EditorUtility.RevealInFinder(CharactersFolder);
        }

        [MenuItem("Madras Gethu/Check Character Setup", priority = 22)]
        public static void CheckSetup()
        {
            var sb = new StringBuilder();
            var characters = FindFbx(CharactersFolder);
            sb.AppendLine("CHARACTERS (" + CharactersFolder + ")");
            if (characters.Count == 0) sb.AppendLine("  ✗ none found");
            foreach (var path in characters)
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var mats = model != null
                    ? model.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToList()
                    : new List<Material>();
                int textured = mats.Count(m => GetTex(m, "_BaseMap") != null || GetTex(m, "_MainTex") != null);
                bool human = avatar != null && avatar.isValid && avatar.isHuman;
                sb.AppendLine($"  {(human ? "✓" : "✗")} {Path.GetFileName(path)}: rig={importer?.animationType}, humanoid avatar={(human ? "yes" : "NO")}, " +
                              $"materials with texture={textured}/{mats.Count}");
            }

            var slots = CreateSlots();
            var anims = FindFbx(AnimationsFolder);
            sb.AppendLine();
            sb.AppendLine("ANIMATIONS (" + AnimationsFolder + "): " + anims.Count + " files");
            foreach (var path in anims)
            {
                var slot = MatchSlot(slots, Path.GetFileNameWithoutExtension(path));
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                bool hasClip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Any(c => !c.name.StartsWith("__preview__"));
                sb.AppendLine($"  {(slot != null && hasClip ? "✓" : "✗")} {Path.GetFileName(path)} → {(slot != null ? slot.name : "NOT RECOGNISED (rename it)")}, rig={importer?.animationType}");
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            sb.AppendLine();
            sb.AppendLine($"ANIMATOR CONTROLLER: {(controller != null ? "✓ " + ControllerPath : "✗ missing (run Setup Characters)")}");

            sb.AppendLine();
            sb.AppendLine("ARENA SCENE");
            GameBootstrap bootstrap = null;
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                bootstrap = root.GetComponentInChildren<GameBootstrap>(true);
                if (bootstrap != null) break;
            }
            if (bootstrap == null)
            {
                sb.AppendLine("  ✗ Game Bootstrap not found in the open scene (open " + ArenaScenePath + ")");
            }
            else
            {
                PrefabLine(sb, "Player model", bootstrap.playerModelPrefab);
                PrefabLine(sb, "Enemy model", bootstrap.enemyModelPrefab);
                PrefabLine(sb, "Thadiyan model", bootstrap.thadiyanModelPrefab);
                PrefabLine(sb, "Boss model", bootstrap.bossModelPrefab);
                sb.AppendLine($"  {(bootstrap.fighterAnimator != null ? "✓" : "✗")} Fighter Animator: {(bootstrap.fighterAnimator != null ? bootstrap.fighterAnimator.name : "(empty)")}");
            }

            Debug.Log("[Madras Gethu] Character setup check\n" + sb);
            EditorUtility.DisplayDialog("Madras Gethu: character check", sb.ToString(), "OK");
        }

        static void PrefabLine(StringBuilder sb, string label, GameObject prefab)
        {
            if (prefab == null) { sb.AppendLine($"  - {label}: (empty)"); return; }
            var anim = prefab.GetComponentInChildren<Animator>(true);
            bool ok = anim != null && anim.runtimeAnimatorController != null;
            sb.AppendLine($"  {(ok ? "✓" : "✗")} {label}: {prefab.name}{(ok ? "" : " (no Animator Controller on this prefab)")}");
        }

        // ------------------------------------------------------------------ import settings

        static Avatar SetupCharacterModel(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return null;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.SaveAndReimport();

            SetupMaterials(path, importer);

            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            return avatar != null && avatar.isValid && avatar.isHuman ? avatar : null;
        }

        // ------------------------------------------------------------------ materials (colours)

        /// <summary>
        /// Makes skin and clothes show up: extracts textures and materials from the FBX into editable files,
        /// switches them to the project's lit shader (URP) and plugs in colour/normal textures that are missing.
        /// </summary>
        static void SetupMaterials(string modelPath, ModelImporter importer)
        {
            string modelName = Path.GetFileNameWithoutExtension(modelPath);
            string textureFolder = GeneratedFolder + "/Textures/" + modelName;
            string materialFolder = GeneratedFolder + "/Materials/" + modelName;
            EnsureFolder(textureFolder);
            EnsureFolder(materialFolder);

            // 1. Textures embedded in the FBX (Mixamo, Tripo) -> real texture files.
            if (AssetDatabase.FindAssets("t:Texture2D", new[] { textureFolder }).Length == 0)
            {
                importer.ExtractTextures(textureFolder);
                AssetDatabase.Refresh();
            }

            // 2. Materials embedded in the FBX -> editable .mat files.
            bool extracted = false;
            foreach (var mat in AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>())
            {
                string matPath = materialFolder + "/" + SafeFileName(mat.name) + ".mat";
                if (File.Exists(matPath)) continue;
                if (string.IsNullOrEmpty(AssetDatabase.ExtractAsset(mat, matPath)))
                    extracted = true;
            }
            if (extracted)
            {
                AssetDatabase.WriteImportSettingsIfDirty(modelPath);
                AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
            }

            // 3. Make every material the model uses render with its textures.
            var textures = new List<Texture2D>();
            foreach (var folder in new[] { textureFolder, Path.GetDirectoryName(modelPath).Replace('\\', '/') })
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
                    if (tex != null && !textures.Contains(tex)) textures.Add(tex);
                }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) return;
            var materials = model.GetComponentsInChildren<Renderer>(true)
                .SelectMany(r => r.sharedMaterials)
                .Where(m => m != null)
                .Distinct()
                .ToList();
            foreach (var mat in materials)
                FixMaterial(mat, textures);
            AssetDatabase.SaveAssets();
        }

        static void FixMaterial(Material mat, List<Texture2D> textures)
        {
            // Only materials saved as their own files can be edited (embedded ones are read-only).
            if (!AssetDatabase.GetAssetPath(mat).EndsWith(".mat")) return;

            Texture main = GetTex(mat, "_BaseMap") ?? GetTex(mat, "_MainTex");
            Texture normal = GetTex(mat, "_BumpMap");

            bool urp = GraphicsSettings.currentRenderPipeline != null;
            var lit = Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard");
            if (lit != null && mat.shader != lit) mat.shader = lit;

            if (main == null) main = BestTexture(textures, mat.name, new[] { "basecolor", "diffuse", "albedo", "color", "diff", "col" }, new[] { "normal", "rough", "metal", "spec", "occlusion", "_ao", "gloss", "height" });
            if (main == null && textures.Count == 1) main = textures[0];
            if (normal == null) normal = BestTexture(textures, mat.name, new[] { "normal", "nrm", "_nor" }, new string[0]);

            if (main != null)
            {
                SetTex(mat, "_BaseMap", main);
                SetTex(mat, "_MainTex", main);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            }
            if (normal != null)
            {
                var ti = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal)) as TextureImporter;
                if (ti != null && ti.textureType != TextureImporterType.NormalMap)
                {
                    ti.textureType = TextureImporterType.NormalMap;
                    ti.SaveAndReimport();
                }
                SetTex(mat, "_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
            // Skin and cloth are not shiny; imported FBX materials often are.
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.25f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.25f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);
        }

        static Texture GetTex(Material mat, string property) =>
            mat.HasProperty(property) ? mat.GetTexture(property) : null;

        static void SetTex(Material mat, string property, Texture tex)
        {
            if (mat.HasProperty(property)) mat.SetTexture(property, tex);
        }

        /// <summary>Picks the texture whose name best matches the material name and the wanted map type.</summary>
        static Texture2D BestTexture(List<Texture2D> textures, string materialName, string[] wanted, string[] unwanted)
        {
            string mat = Normalize(materialName);
            Texture2D best = null;
            int bestScore = 0;
            foreach (var tex in textures)
            {
                string n = Normalize(tex.name);
                if (unwanted.Any(u => n.Contains(Normalize(u)))) continue;
                int score = 0;
                if (wanted.Any(w => n.Contains(Normalize(w)))) score += 10;
                else continue;
                if (mat.Length > 0 && (n.Contains(mat) || mat.Contains(n))) score += 5;
                if (score > bestScore) { bestScore = score; best = tex; }
            }
            return best;
        }

        static string SafeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Replace(':', '_');
        }

        static AnimationClip SetupAnimation(string path, Slot slot, Avatar sourceAvatar)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return null;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = sourceAvatar;
            importer.importAnimation = true;
            importer.SaveAndReimport();   // refresh the default clip list for the humanoid rig

            var clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return null;

            var clip = clips[0];
            clip.name = slot.name;
            clip.loopTime = slot.kind == SlotKind.Locomotion;
            // Play in place: the game code moves the characters, the animation only moves the body.
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;
            clip.lockRootPositionXZ = true;
            clip.keepOriginalPositionXZ = true;
            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();

            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => c.name == slot.name);
        }

        static void ResolveFallbacks(List<Slot> slots)
        {
            // Two passes so fallbacks of fallbacks resolve (e.g. Held -> Flinch -> nothing).
            for (int pass = 0; pass < 2; pass++)
                foreach (var slot in slots)
                {
                    if (slot.clip != null || slot.fallbacks == null) continue;
                    foreach (var fb in slot.fallbacks)
                    {
                        var other = slots.FirstOrDefault(s => s.name == fb);
                        if (other != null && other.clip != null)
                        {
                            slot.clip = other.clip;
                            break;
                        }
                    }
                }
        }

        // ------------------------------------------------------------------ animator controller

        static AnimatorController BuildController(List<Slot> slots)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
                AssetDatabase.DeleteAsset(ControllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Guard", AnimatorControllerParameterType.Bool);
            foreach (var s in slots)
            {
                if (s.kind == SlotKind.Trigger || s.kind == SlotKind.Final)
                    controller.AddParameter(s.name, AnimatorControllerParameterType.Trigger);
                if (s.kind == SlotKind.HoldBool)
                    controller.AddParameter(s.boolParameter, AnimatorControllerParameterType.Bool);
            }

            var sm = controller.layers[0].stateMachine;
            // Locomotion (relaxed) and fighting stance, both blended by Speed.
            var locomotion = controller.CreateBlendTreeInController("Locomotion", out var relaxedTree, 0);
            FillLocomotionTree(relaxedTree, ClipFor(slots, "Idle"), ClipFor(slots, "Walk"), ClipFor(slots, "Run"));
            var stance = controller.CreateBlendTreeInController("Fight Stance", out var stanceTree, 0);
            FillLocomotionTree(stanceTree, ClipFor(slots, "Guard"), ClipFor(slots, "Walk"), ClipFor(slots, "Run"));
            sm.defaultState = locomotion;

            var toStance = locomotion.AddTransition(stance);
            toStance.AddCondition(AnimatorConditionMode.If, 0f, "Guard");
            SetQuick(toStance, 0.2f);
            var toRelaxed = stance.AddTransition(locomotion);
            toRelaxed.AddCondition(AnimatorConditionMode.IfNot, 0f, "Guard");
            SetQuick(toRelaxed, 0.3f);

            // Death and knockdown first, so they win over everything else.
            AnimatorState knockdown = null;
            foreach (var name in new[] { "Dead", "Knockdown" })
            {
                var slot = slots.First(s => s.name == name);
                if (slot.clip == null) continue;
                var state = sm.AddState(name);
                state.motion = slot.clip;
                var t = sm.AddAnyStateTransition(state);
                t.AddCondition(AnimatorConditionMode.If, 0f, name);
                t.canTransitionToSelf = false;
                SetQuick(t, 0.08f);
                if (name == "Knockdown") knockdown = state;
                // No exit: they hold the last frame (lying on the ground).
            }

            // Hold states driven by bools: block, holding someone, being held.
            foreach (var slot in slots.Where(s => s.kind == SlotKind.HoldBool && s.clip != null))
            {
                var state = sm.AddState(slot.name);
                state.motion = slot.clip;
                var enter = sm.AddAnyStateTransition(state);
                enter.AddCondition(AnimatorConditionMode.If, 0f, slot.boolParameter);
                enter.canTransitionToSelf = false;
                SetQuick(enter, 0.1f);
                AddExitByCondition(state, stance, locomotion, slot.boolParameter);
            }

            // One-shot actions on triggers.
            foreach (var slot in slots.Where(s => s.kind == SlotKind.Trigger && s.clip != null))
            {
                var state = sm.AddState(slot.name);
                state.motion = slot.clip;
                state.speed = slot.speed;
                var enter = sm.AddAnyStateTransition(state);
                enter.AddCondition(AnimatorConditionMode.If, 0f, slot.name);
                enter.canTransitionToSelf = true;
                SetQuick(enter, 0.06f);
                AddExitByTime(state, stance, locomotion);
            }

            // Without a GetUp animation, a knocked-down fighter pops back up on the trigger.
            if (knockdown != null && slots.First(s => s.name == "GetUp").clip == null)
            {
                var up1 = knockdown.AddTransition(stance);
                up1.AddCondition(AnimatorConditionMode.If, 0f, "GetUp");
                up1.AddCondition(AnimatorConditionMode.If, 0f, "Guard");
                SetQuick(up1, 0.25f);
                var up2 = knockdown.AddTransition(locomotion);
                up2.AddCondition(AnimatorConditionMode.If, 0f, "GetUp");
                up2.AddCondition(AnimatorConditionMode.IfNot, 0f, "Guard");
                SetQuick(up2, 0.25f);
            }

            LayoutStates(sm);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        static AnimationClip ClipFor(List<Slot> slots, string name) => slots.First(s => s.name == name).clip;

        static void FillLocomotionTree(BlendTree tree, AnimationClip idle, AnimationClip walk, AnimationClip run)
        {
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            if (walk != null) tree.AddChild(walk, 0.35f);
            if (run != null) tree.AddChild(run, 0.8f);
            else if (walk != null) tree.AddChild(walk, 0.8f);
        }

        static void SetQuick(AnimatorStateTransition t, float duration)
        {
            t.hasExitTime = false;
            t.duration = duration;
            t.hasFixedDuration = true;
        }

        static void AddExitByTime(AnimatorState state, AnimatorState stance, AnimatorState relaxed)
        {
            var a = state.AddTransition(stance);
            a.AddCondition(AnimatorConditionMode.If, 0f, "Guard");
            a.hasExitTime = true;
            a.exitTime = 0.85f;
            a.duration = 0.15f;
            a.hasFixedDuration = true;

            var b = state.AddTransition(relaxed);
            b.AddCondition(AnimatorConditionMode.IfNot, 0f, "Guard");
            b.hasExitTime = true;
            b.exitTime = 0.85f;
            b.duration = 0.2f;
            b.hasFixedDuration = true;
        }

        static void AddExitByCondition(AnimatorState state, AnimatorState stance, AnimatorState relaxed, string boolParameter)
        {
            var a = state.AddTransition(stance);
            a.AddCondition(AnimatorConditionMode.IfNot, 0f, boolParameter);
            a.AddCondition(AnimatorConditionMode.If, 0f, "Guard");
            SetQuick(a, 0.15f);

            var b = state.AddTransition(relaxed);
            b.AddCondition(AnimatorConditionMode.IfNot, 0f, boolParameter);
            b.AddCondition(AnimatorConditionMode.IfNot, 0f, "Guard");
            SetQuick(b, 0.2f);
        }

        static void LayoutStates(AnimatorStateMachine sm)
        {
            var states = sm.states;
            for (int i = 0; i < states.Length; i++)
            {
                var child = states[i];
                child.position = i < 2
                    ? new Vector3(300f + i * 260f, 0f, 0f)
                    : new Vector3(40f + ((i - 2) % 5) * 220f, 120f + ((i - 2) / 5) * 70f, 0f);
                states[i] = child;
            }
            sm.states = states;
            sm.anyStatePosition = new Vector3(40f, 0f, 0f);
        }

        // ------------------------------------------------------------------ prefabs and scene

        static GameObject CreatePrefab(string modelPath, string name, AnimatorController controller)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var instance = Object.Instantiate(model);
            instance.name = name;
            try
            {
                var animator = instance.GetComponent<Animator>();
                if (animator == null) animator = instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (animator.avatar == null)
                    animator.avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();

                foreach (var col in instance.GetComponentsInChildren<Collider>())
                    Object.DestroyImmediate(col);

                string prefabPath = GeneratedFolder + "/" + name + ".prefab";
                return PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        static string AssignToBootstrap(Dictionary<string, GameObject> prefabs, string heroPath, AnimatorController controller)
        {
            if (!File.Exists(ArenaScenePath))
                return "The arena scene does not exist yet. Run 'Madras Gethu > Create Kasimedu Arena Scene', then run this tool again.";
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return "Scene not updated (you cancelled saving the open scene). Assign the prefabs on Game Bootstrap by hand.";

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ArenaScenePath)
                scene = EditorSceneManager.OpenScene(ArenaScenePath, OpenSceneMode.Single);

            GameBootstrap bootstrap = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                bootstrap = root.GetComponentInChildren<GameBootstrap>(true);
                if (bootstrap != null) break;
            }
            if (bootstrap == null)
                return "No Game Bootstrap found in the arena scene. Assign the prefabs by hand.";

            var hero = Lookup(prefabs, Normalize(Path.GetFileNameWithoutExtension(heroPath)));
            var rowdy = Lookup(prefabs, "rowdy");

            Undo.RecordObject(bootstrap, "Assign character models");
            bootstrap.playerModelPrefab = hero;
            bootstrap.enemyModelPrefab = rowdy != null ? rowdy : hero;
            bootstrap.thadiyanModelPrefab = Lookup(prefabs, "thadiyan");
            bootstrap.bossModelPrefab = Lookup(prefabs, "boss");
            bootstrap.fighterAnimator = controller;
            EditorUtility.SetDirty(bootstrap);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            return rowdy != null
                ? "Kasimedu Arena scene updated with your characters."
                : "Kasimedu Arena scene updated. No Rowdy.fbx found, so enemies use the Hero model for now.";
        }

        static GameObject Lookup(Dictionary<string, GameObject> prefabs, string key) =>
            prefabs.TryGetValue(key, out var prefab) ? prefab : null;

        static void ShowReport(List<Slot> slots, List<string> unmatched, List<string> characters, string sceneResult)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Characters: " + string.Join(", ", characters.Select(Path.GetFileNameWithoutExtension)));
            sb.AppendLine();
            sb.AppendLine("Animations found: " + string.Join(", ", slots.Where(s => s.ownFile).Select(s => s.name)));
            var borrowed = slots.Where(s => !s.ownFile && s.clip != null).Select(s => s.name + "→" + s.clip.name).ToList();
            if (borrowed.Count > 0) sb.AppendLine("Using stand-ins: " + string.Join(", ", borrowed));
            var missing = slots.Where(s => s.clip == null).Select(s => s.name).ToList();
            if (missing.Count > 0) sb.AppendLine("Missing (optional): " + string.Join(", ", missing));
            if (unmatched.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Not recognised (rename them to a slot name, e.g. Jab.fbx): " + string.Join(", ", unmatched));
            }
            sb.AppendLine();
            sb.AppendLine(sceneResult);
            sb.AppendLine();
            sb.AppendLine("Press Play to fight!");

            Debug.Log("[Madras Gethu] Character setup\n" + sb);
            EditorUtility.DisplayDialog("Madras Gethu: characters ready", sb.ToString(), "Semma!");
        }

        // ------------------------------------------------------------------ helpers

        static List<string> FindFbx(string folder)
        {
            if (!Directory.Exists(folder)) return new List<string>();
            return Directory.GetFiles(folder, "*.fbx", SearchOption.TopDirectoryOnly)
                .Select(p => p.Replace('\\', '/'))
                .OrderBy(p => p)
                .ToList();
        }

        static Slot MatchSlot(List<Slot> slots, string fileName)
        {
            string key = Normalize(fileName.Contains("@") ? fileName.Substring(fileName.LastIndexOf('@') + 1) : fileName);
            foreach (var s in slots)
                if (Normalize(s.name) == key || s.aliases.Contains(key))
                    return s;
            return null;
        }

        static string Normalize(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s.ToLowerInvariant())
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            return sb.ToString();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}

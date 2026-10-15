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
        const string ControllerPath = GeneratedFolder + "/HeroAnimator.controller";
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
                new Slot { name = "WalkBack", kind = SlotKind.Locomotion, aliases = new[] { "walkback", "walkingbackwards", "walkbackwards", "backwardwalk", "backwardswalk" }, fallbacks = new[] { "Walk" } },
                new Slot { name = "StrafeLeft", kind = SlotKind.Locomotion, aliases = new[] { "strafeleft", "leftstrafe", "leftstrafewalking", "leftstrafewalk", "walkleft", "walkstrafeleft" }, fallbacks = new[] { "Walk" } },
                new Slot { name = "StrafeRight", kind = SlotKind.Locomotion, aliases = new[] { "straferight", "rightstrafe", "rightstrafewalking", "rightstrafewalk", "walkright", "walkstraferight" }, fallbacks = new[] { "Walk" } },
                new Slot { name = "Guard", kind = SlotKind.Locomotion, aliases = new[] { "guard", "boxingidle", "fightidle", "fightingidle", "bouncingfightidle" }, fallbacks = new[] { "Idle" } },

                T("Jab", 1.7f, new[] { "jab", "leadjab", "jabpunch" }, "Cross", "Hook"),
                T("Cross", 1.7f, new[] { "cross", "crosspunch", "punch", "punching" }, "Jab", "Hook"),
                T("Hook", 1.6f, new[] { "hook", "hookpunch", "leadhook" }, "Cross", "Jab"),
                T("BodyBlow", 1.6f, new[] { "bodyblow", "bodyjab", "bodyjabcross", "bodypunch" }, "Hook", "Cross"),
                T("Haymaker", 1.3f, new[] { "haymaker", "bigpunch", "punchcombo" }, "Hook", "Cross"),
                T("Uppercut", 1.4f, new[] { "uppercut" }, "Hook", "Cross"),
                T("Backfist", 1.4f, new[] { "backfist", "spinningbackfist", "spinbackfist" }, "Hook", "Haymaker"),
                T("Kick", 1.4f, new[] { "kick", "mmakick", "legkick" }, "FrontKick", "Roundhouse"),
                T("Stomp", 1.3f, new[] { "stomp", "stomping" }, "Kick"),
                T("Knee", 1.5f, new[] { "knee", "kneestrike", "kneekick" }, "Kick"),
                T("Throw", 1.3f, new[] { "throw", "judothrow", "grabthrow" }, "Haymaker", "Hook"),
                T("Slam", 1.3f, new[] { "slam", "overheadslam", "doublehandslam" }, "Haymaker", "Uppercut"),
                T("Counter", 1.6f, new[] { "counter", "counterpunch" }, "Cross", "Haymaker"),
                T("Dodge", 1.4f, new[] { "dodge", "dodging", "sidestep" }),
                T("DodgeLeft", 1f, new[] { "dodgeleft", "leftdodge", "dodgingleft", "sidestepleft", "stepleft" }, "Dodge"),
                T("DodgeRight", 1f, new[] { "dodgeright", "rightdodge", "dodgingright", "sidestepright", "stepright" }, "Dodge"),
                T("DodgeBack", 1f, new[] { "dodgeback", "backdodge", "dodgingback", "backstep", "stepback", "hopback" }, "Dodge"),
                T("Roll", 1f, new[] { "roll", "forwardroll", "quickroll", "standtoroll", "diveroll" }, "Dodge"),
                T("Parry", 1f, new[] { "parry", "outwardblock", "deflect", "blockdeflect" }, "Block", "Guard"),
                T("StaggerBack", 1f, new[] { "staggerback", "stumblebackwards", "stumbleback", "stumble", "stagger", "hitreactionback" }, "Flinch"),
                T("Push", 1f, new[] { "push", "shove", "pushing", "twohandpush" }, "Haymaker", "Cross"),
                T("Headbutt", 1f, new[] { "headbutt", "headbutting" }, "Cross", "Jab"),
                T("Jump", 1f, new[] { "jump", "jumping", "standingjump", "jumpinplace" }),
                T("FlyingKick", 1f, new[] { "flyingkick", "jumpkick", "dropkick", "jumpattack" }, "Kick"),
                T("Flinch", 1.3f, new[] { "flinch", "hitreaction", "reaction", "headhit", "hit", "takinghit" }),
                T("Windup", 1f, new[] { "windup", "chargeup" }),
                T("Taunt", 1f, new[] { "taunt", "taunting", "comeon", "challenge" }),
                T("GetUp", 1.3f, new[] { "getup", "gettingup", "standup", "standingup", "getupback", "gettingupback" }),
                T("GetUpFront", 1.3f, new[] { "getupfront", "gettingupfront", "standupfront", "getupfromfront", "getupprone", "pronetostand" }, "GetUp"),

                // Enemy-only moves (put them in Animations/Rowdy, /Thadiyan or /Boss)
                T("RowdySwing", 1f, new[] { "rowdyswing", "wildswing", "sloppypunch", "streetpunch" }, "Haymaker", "Cross"),
                T("RowdyKick", 1f, new[] { "rowdykick", "stomachkick" }, "Kick"),
                T("RowdyShove", 1f, new[] { "rowdyshove", "shoving", "twohandshove" }, "Push", "Haymaker"),
                T("RowdyGrab", 1f, new[] { "rowdygrab", "grabattempt", "reachgrab" }, "Grab", "Push"),
                T("BearHug", 1f, new[] { "bearhug", "hug", "crush" }, "RowdyGrab", "Grab"),
                T("Charge", 1f, new[] { "charge", "shouldercharge", "tackle", "mutantrun", "runningtackle" }, "Run"),
                T("GroundPound", 1f, new[] { "groundpound", "mutantjumpattack", "groundslam", "doubleaxe" }, "Slam", "Haymaker"),
                T("Roar", 1f, new[] { "roar", "mutantroaring", "angry", "rage", "yelling" }, "Taunt"),
                // Weapons and objects
                T("PickUp", 1f, new[] { "pickup", "pickingup", "pickupobject", "lootpickup" }),
                T("WeaponSwing", 1f, new[] { "weaponswing", "swordslash", "batswing", "stickswing", "onehandswing" }, "Haymaker", "Hook"),
                T("WeaponSmash", 1f, new[] { "weaponsmash", "overheadsmash", "greatswordslash", "twohandsmash" }, "Slam", "Haymaker"),
                T("ThrowObject", 1f, new[] { "throwobject", "throwing", "grenadethrow", "toss" }, "Throw", "Haymaker"),
                // Hero combo specials and wrestling finishers (put them in Animations/ or Animations/Hero)
                T("ShoulderBarge", 1.2f, new[] { "shoulderbarge", "shoulderhit", "shoulderbash" }, "Push", "Haymaker"),
                T("LegSweep", 1.2f, new[] { "legsweep", "sweep", "sweepfall", "spinsweep" }, "Kick", "Stomp"),
                T("Clothesline", 1.1f, new[] { "clothesline", "lariat", "armswing" }, "Haymaker", "Hook"),
                T("BackElbow", 1.3f, new[] { "backelbow", "elbowstrike", "elbow", "elbowpunch" }, "Backfist", "Hook"),
                T("Lift", 1f, new[] { "lift", "lifting", "liftup", "boxlifting", "liftingheavy" }, "PickUp", "Throw"),
                T("Suplex", 1f, new[] { "suplex", "germansuplex", "backflip", "bridge" }, "Throw", "Slam"),
                T("Spear", 1f, new[] { "spear", "footballtackle", "speartackle" }, "Charge", "Push"),
                // Grab strike combos
                T("FrontKick", 1.2f, new[] { "frontkick", "frontsnapkick", "snapkick", "straightkick" }, "Kick"),
                T("Roundhouse", 1.2f, new[] { "roundhouse", "roundhousekick", "highkick", "headkick" }, "Kick"),
                T("PushKick", 1.1f, new[] { "pushkick", "teep", "frontpushkick", "stompkick" }, "FrontKick", "Kick"),
                T("FlyingKnee", 1f, new[] { "flyingknee", "jumpingknee", "jumpknee", "flyingkneestrike" }, "Knee", "FlyingKick"),
                T("ElbowDrop", 1f, new[] { "elbowdrop", "legdrop", "jumpdown" }, "Stomp", "Slam"),

                new Slot { name = "Dazed", kind = SlotKind.HoldBool, boolParameter = "Dazed", aliases = new[] { "dazed", "dizzy", "dizzyidle", "stunned" }, fallbacks = new[] { "StaggerBack", "Flinch" } },
                new Slot { name = "Beg", kind = SlotKind.HoldBool, boolParameter = "Begging", aliases = new[] { "beg", "begging", "pleading", "kneelbeg", "kneeling" }, fallbacks = new[] { "Flinch" } },
                new Slot { name = "Block", kind = SlotKind.HoldBool, boolParameter = "Blocking", aliases = new[] { "block", "blocking", "centerblock", "bodyblock" }, fallbacks = new[] { "Guard" } },
                new Slot { name = "Grab", kind = SlotKind.HoldBool, boolParameter = "Holding", aliases = new[] { "grab", "grabbing", "hold", "holding" }, fallbacks = new[] { "Guard" } },
                new Slot { name = "Held", kind = SlotKind.HoldBool, boolParameter = "Held", aliases = new[] { "held", "beingheld", "choked", "struggle", "struggling", "grabbedreaction" }, fallbacks = new[] { "Flinch" } },

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
            EnsureFolder(AnimationsFolder + "/Hero");
            EnsureFolder(AnimationsFolder + "/Rowdy");
            EnsureFolder(AnimationsFolder + "/Thadiyan");
            EnsureFolder(AnimationsFolder + "/Boss");
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

                // 2+3. Animations and one Animator per character type.
                //   Animations/          shared moves (everyone)
                //   Animations/Hero/     hero-only moves (override the shared ones)
                //   Animations/Rowdy/    enemy moves (Thadiyan and Boss inherit these)
                //   Animations/Thadiyan/, Animations/Boss/
                var importCache = new Dictionary<string, AnimationClip>();
                var unmatched = new List<string>();
                var controllers = new Dictionary<string, AnimatorController>();
                var slotsByType = new Dictionary<string, List<Slot>>();
                var report = new StringBuilder();
                bool anyEnemy = characterFiles.Any(p => Normalize(Path.GetFileNameWithoutExtension(p)) != "hero");

                foreach (var type in CharacterNames)
                {
                    bool needed = type == "Hero" || characterFiles.Any(p => Normalize(Path.GetFileNameWithoutExtension(p)) == Normalize(type))
                                  || (type == "Rowdy" && anyEnemy);
                    if (!needed) continue;

                    EditorUtility.DisplayProgressBar("Madras Gethu", "Animations for " + type + "...", 0.2f + 0.15f * controllers.Count);
                    var slots = CreateSlots();
                    foreach (var folder in AnimationFolderChain(type))
                    {
                        foreach (var path in FindFbx(folder))
                        {
                            var slot = MatchSlot(slots, Path.GetFileNameWithoutExtension(path));
                            if (slot == null)
                            {
                                if (!unmatched.Contains(Path.GetFileName(path))) unmatched.Add(Path.GetFileName(path));
                                continue;
                            }
                            if (!importCache.TryGetValue(path, out var clip))
                            {
                                clip = SetupAnimation(path, slot, heroAvatar);
                                importCache[path] = clip;
                            }
                            if (clip != null) { slot.clip = clip; slot.ownFile = true; }   // later folders override earlier ones
                        }
                    }
                    ResolveFallbacks(slots);

                    if (slots.First(sl => sl.name == "Idle").clip == null)
                    {
                        EditorUtility.ClearProgressBar();
                        EditorUtility.DisplayDialog("Madras Gethu",
                            "An Idle animation is required.\n\nDownload Mixamo \"Idle\" (Without Skin) and save it as\n" +
                            AnimationsFolder + "/Idle.fbx", "OK");
                        return;
                    }

                    controllers[type] = BuildController(slots, GeneratedFolder + "/" + type + "Animator.controller");
                    slotsByType[type] = slots;
                    AppendSlotReport(report, type, slots);
                }

                // 3b. Measure when each punch/kick actually connects, so hits land with the animation.
                EditorUtility.DisplayProgressBar("Madras Gethu", "Measuring contact frames...", 0.8f);
                int timed = WriteTimings(slotsByType, controllers, heroPath);
                report.AppendLine("Contact frames measured for " + timed + " moves (hits now land when the fist/foot arrives).");

                // 4. Prefabs, each with its own type's Animator
                EditorUtility.DisplayProgressBar("Madras Gethu", "Creating prefabs...", 0.85f);
                var prefabs = new Dictionary<string, GameObject>();
                foreach (var path in characterFiles)
                {
                    string name = Path.GetFileNameWithoutExtension(path);
                    string type = CharacterNames.FirstOrDefault(t => Normalize(t) == Normalize(name)) ?? (path == heroPath ? "Hero" : "Rowdy");
                    var controller = controllers.TryGetValue(type, out var c) ? c
                                   : controllers.TryGetValue("Rowdy", out var r) && type != "Hero" ? r
                                   : controllers["Hero"];
                    prefabs[Normalize(name)] = CreatePrefab(path, name, controller);
                }

                // 5. Plug into the arena scene
                EditorUtility.DisplayProgressBar("Madras Gethu", "Updating the arena scene...", 0.95f);
                string sceneResult = AssignToBootstrap(prefabs, heroPath, controllers["Hero"]);

                EditorUtility.ClearProgressBar();
                ShowReport(report.ToString(), unmatched, characterFiles, sceneResult);
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
            ReportWindow.Show("Character check", sb.ToString());
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
            // The game code moves the characters. Forward/sideways travel in the clip must NOT be baked into
            // the pose (the body would drift ahead and snap back every loop); with root motion off it is
            // simply discarded, so the clip plays in place. Rotation and height stay baked into the pose.
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;
            clip.lockRootPositionXZ = false;
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

        static AnimatorController BuildController(List<Slot> slots, string controllerPath)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
                AssetDatabase.DeleteAsset(controllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
            // Speed of one-shot moves, set by the game each time so the punch lands when the hit registers.
            controller.AddParameter("ActionSpeed", AnimatorControllerParameterType.Float);
            var parameters = controller.parameters;
            foreach (var p in parameters)
                if (p.name == "ActionSpeed") p.defaultFloat = 1.2f;   // never 0, or moves would freeze
            controller.parameters = parameters;
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
            FillStanceTree(stanceTree, slots);
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
            // (Holding, being held and blocking only move the arms: they go on the upper-body layer below,
            // so the legs keep walking while you drag someone or step with your guard up.)
            foreach (var slot in slots.Where(s => s.kind == SlotKind.HoldBool && s.clip != null && !UpperBodyHolds.Contains(s.name)))
            {
                var state = sm.AddState(slot.name);
                state.motion = slot.clip;
                // Entered only from standing / fight stance, never from Any State: otherwise the held pose
                // would cancel a punch, knee or flinch on its first frame while the bool is still on.
                // One-shot moves end back in the stance, and from there the hold state resumes.
                foreach (var from in new[] { locomotion, stance })
                {
                    var enter = from.AddTransition(state);
                    enter.AddCondition(AnimatorConditionMode.If, 0f, slot.boolParameter);
                    SetQuick(enter, 0.1f);
                }
                AddExitByCondition(state, stance, locomotion, slot.boolParameter);
            }

            // One-shot actions on triggers.
            foreach (var slot in slots.Where(s => s.kind == SlotKind.Trigger && s.clip != null))
            {
                var state = sm.AddState(slot.name);
                state.motion = slot.clip;
                state.speed = 1f;
                state.speedParameter = "ActionSpeed";
                state.speedParameterActive = true;
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

            // Upper-body layer: arms and head only, over whatever the legs are doing.
            controller.AddLayer("Upper Body");
            var upper = controller.layers[1].stateMachine;
            var empty = upper.AddState("Empty");
            upper.defaultState = empty;
            foreach (var slot in slots.Where(s => s.kind == SlotKind.HoldBool && s.clip != null && UpperBodyHolds.Contains(s.name)))
            {
                var state = upper.AddState(slot.name);
                state.motion = slot.clip;
                var enter = empty.AddTransition(state);
                enter.AddCondition(AnimatorConditionMode.If, 0f, slot.boolParameter);
                SetQuick(enter, 0.12f);
                var exit = state.AddTransition(empty);
                exit.AddCondition(AnimatorConditionMode.IfNot, 0f, slot.boolParameter);
                SetQuick(exit, 0.12f);
            }

            // Hand IK runs on layers with IK Pass enabled: the base layer (crouch, lean, planted feet) and the
            // upper-body layer (hands on the collar / body), which is applied last.
            var layers = controller.layers;
            layers[0].iKPass = true;
            layers[1].iKPass = true;
            layers[1].defaultWeight = 1f;
            layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
            layers[1].avatarMask = UpperBodyMask(Path.GetDirectoryName(controllerPath).Replace('\\', '/'));
            controller.layers = layers;

            LayoutStates(sm);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        static AnimationClip ClipFor(List<Slot> slots, string name) => slots.First(s => s.name == name).clip;

        /// <summary>
        /// Fighting stance: guard idle in the middle, stepping forward/back/sideways around it
        /// (MoveX = right, MoveZ = forward, in the character's own space; 0.5 = walk, 1 = run).
        /// </summary>
        static void FillStanceTree(BlendTree tree, List<Slot> slots)
        {
            tree.blendType = BlendTreeType.FreeformDirectional2D;
            tree.blendParameter = "MoveX";
            tree.blendParameterY = "MoveZ";
            tree.AddChild(ClipFor(slots, "Guard"), Vector2.zero);
            var walk = ClipFor(slots, "Walk");
            if (walk != null) tree.AddChild(walk, new Vector2(0f, 0.5f));
            var back = ClipFor(slots, "WalkBack");
            if (back != null) tree.AddChild(back, new Vector2(0f, -0.5f));
            var left = ClipFor(slots, "StrafeLeft");
            if (left != null) tree.AddChild(left, new Vector2(-0.5f, 0f));
            var right = ClipFor(slots, "StrafeRight");
            if (right != null) tree.AddChild(right, new Vector2(0.5f, 0f));
            var run = ClipFor(slots, "Run");
            if (run != null) tree.AddChild(run, new Vector2(0f, 1f));
        }

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

        static readonly string[] UpperBodyHolds = { "Grab", "Held", "Block" };

        /// <summary>Arms, hands and head only (no hips, spine or legs). Saved next to the controllers.</summary>
        static AvatarMask UpperBodyMask(string folder)
        {
            string path = folder + "/UpperBody.mask";
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, path);
            }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var part = (AvatarMaskBodyPart)i;
                bool on = part == AvatarMaskBodyPart.Head || part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm
                          || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers
                          || part == AvatarMaskBodyPart.LeftHandIK || part == AvatarMaskBodyPart.RightHandIK;
                mask.SetHumanoidBodyPartActive(part, on);
            }
            EditorUtility.SetDirty(mask);
            return mask;
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

            var rowdy = Lookup(prefabs, "rowdy");
            string message;
            if (!AssignInScene(ArenaScenePath, prefabs, heroPath, controller))
                message = "No Game Bootstrap found in the arena scene. Assign the prefabs by hand.";
            else
                message = rowdy != null
                    ? "Kasimedu Arena scene updated with your characters."
                    : "Kasimedu Arena scene updated. No Rowdy.fbx found, so enemies use the Hero model for now.";

            // The practice yard uses the same characters.
            if (File.Exists(MadrasGethuMenu.PracticeScenePath))
            {
                if (AssignInScene(MadrasGethuMenu.PracticeScenePath, prefabs, heroPath, controller))
                    message += " Practice Yard updated too.";
                EditorSceneManager.OpenScene(ArenaScenePath, OpenSceneMode.Single);
            }
            return message;
        }

        static bool AssignInScene(string scenePath, Dictionary<string, GameObject> prefabs, string heroPath, AnimatorController controller)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != scenePath)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            GameBootstrap bootstrap = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                bootstrap = root.GetComponentInChildren<GameBootstrap>(true);
                if (bootstrap != null) break;
            }
            if (bootstrap == null) return false;

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
            return true;
        }

        // ------------------------------------------------------------------ contact frames

        enum Reach { None, Hands, Feet, Knees, HandUp, SmashHands, SmashFeet, Head }

        static Reach ReachFor(string move)
        {
            switch (move)
            {
                case "Jab": case "Cross": case "Hook": case "BodyBlow": case "Haymaker": case "Backfist": case "Counter":
                case "Push": case "Throw": case "ThrowObject": case "RowdySwing": case "RowdyShove": case "RowdyGrab":
                case "BearHug": case "WeaponSwing": case "Clothesline": case "BackElbow": case "ShoulderBarge": case "Spear":
                    return Reach.Hands;
                case "Kick": case "RowdyKick": case "FlyingKick": case "LegSweep":
                case "FrontKick": case "Roundhouse": case "PushKick":
                    return Reach.Feet;
                case "Knee": case "FlyingKnee": return Reach.Knees;
                case "Uppercut": return Reach.HandUp;
                case "Slam": case "GroundPound": case "WeaponSmash": case "ElbowDrop": return Reach.SmashHands;
                case "Stomp": return Reach.SmashFeet;
                case "Headbutt": return Reach.Head;
                default: return Reach.None;
            }
        }

        /// <summary>
        /// Plays every attack clip on a copy of the hero and finds the frame where the fist, foot, knee or head
        /// is furthest out (or, for slams and stomps, hits the bottom after the raise). Saved to
        /// Art/Generated/Resources so the game can time each animation to its hit.
        /// </summary>
        static int WriteTimings(Dictionary<string, List<Slot>> slotsByType, Dictionary<string, AnimatorController> controllers, string modelPath)
        {
            EnsureFolder(GeneratedFolder + "/Resources");
            string assetPath = GeneratedFolder + "/Resources/" + AnimationTimings.ResourceName + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<AnimationTimings>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<AnimationTimings>();
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            asset.entries.Clear();

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) return 0;
            var go = (GameObject)Object.Instantiate(model);
            go.hideFlags = HideFlags.HideAndDontSave;
            int count = 0;
            var cache = new Dictionary<string, float>();
            try
            {
                var animator = go.GetComponent<Animator>();
                if (animator == null) animator = go.AddComponent<Animator>();
                if (animator.avatar == null)
                    animator.avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
                if (animator.avatar == null || !animator.avatar.isHuman) return 0;

                AnimationMode.StartAnimationMode();
                foreach (var pair in slotsByType)
                {
                    if (!controllers.TryGetValue(pair.Key, out var controller)) continue;
                    foreach (var slot in pair.Value)
                    {
                        if (slot.clip == null || (slot.kind != SlotKind.Trigger && slot.kind != SlotKind.Final)) continue;
                        var reach = ReachFor(slot.name);
                        float impact = 0f;
                        if (reach != Reach.None)
                        {
                            string key = AssetDatabase.GetAssetPath(slot.clip) + "/" + slot.clip.name + "/" + reach;
                            if (!cache.TryGetValue(key, out impact))
                            {
                                impact = MeasureImpact(go, animator, slot.clip, reach);
                                cache[key] = impact;
                            }
                            if (impact > 0f) count++;
                        }
                        asset.entries.Add(new AnimationTimings.Entry
                        {
                            controller = controller.name, move = slot.name, length = slot.clip.length, impact = impact,
                        });
                    }
                }
            }
            finally
            {
                if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                Object.DestroyImmediate(go);
            }
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return count;
        }

        static float MeasureImpact(GameObject go, Animator animator, AnimationClip clip, Reach reach)
        {
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform a, b;
            switch (reach)
            {
                case Reach.Feet: case Reach.SmashFeet:
                    a = animator.GetBoneTransform(HumanBodyBones.LeftFoot); b = animator.GetBoneTransform(HumanBodyBones.RightFoot); break;
                case Reach.Knees:
                    a = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg); b = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg); break;
                case Reach.Head:
                    a = b = animator.GetBoneTransform(HumanBodyBones.Head); break;
                default:
                    a = animator.GetBoneTransform(HumanBodyBones.LeftHand); b = animator.GetBoneTransform(HumanBodyBones.RightHand); break;
            }
            if (hips == null || a == null || b == null) return 0f;

            const int samples = 60;
            var times = new float[samples];
            var values = new float[samples];
            float length = clip.length;
            for (int i = 0; i < samples; i++)
            {
                float t = length * (0.05f + 0.85f * i / (samples - 1));
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(go, clip, t);
                AnimationMode.EndSampling();
                times[i] = t;
                Vector3 da = a.position - hips.position, db = b.position - hips.position;
                switch (reach)
                {
                    case Reach.HandUp: case Reach.SmashHands: case Reach.SmashFeet:
                        values[i] = Mathf.Max(da.y, db.y); break;
                    default:
                        da.y = 0f; db.y = 0f;
                        values[i] = Mathf.Max(da.magnitude, db.magnitude); break;
                }
            }

            int best = 0;
            for (int i = 1; i < samples; i++) if (values[i] > values[best]) best = i;
            if (reach == Reach.SmashHands || reach == Reach.SmashFeet)
            {
                // Raised highest at 'best'; the impact is the lowest point after that.
                int low = best;
                for (int i = best + 1; i < samples; i++) if (values[i] < values[low]) low = i;
                best = low;
            }
            return times[best];
        }

        static GameObject Lookup(Dictionary<string, GameObject> prefabs, string key) =>
            prefabs.TryGetValue(key, out var prefab) ? prefab : null;

        static IEnumerable<string> AnimationFolderChain(string type)
        {
            yield return AnimationsFolder;
            if (type == "Hero") { yield return AnimationsFolder + "/Hero"; yield break; }
            yield return AnimationsFolder + "/Rowdy";
            if (type != "Rowdy") yield return AnimationsFolder + "/" + type;
        }

        static void AppendSlotReport(StringBuilder sb, string type, List<Slot> slots)
        {
            sb.AppendLine(type.ToUpper());
            sb.AppendLine("  Own animations: " + string.Join(", ", slots.Where(x => x.ownFile).Select(x => x.name)));
            var borrowed = slots.Where(x => !x.ownFile && x.clip != null).Select(x => x.name + "→" + x.clip.name).ToList();
            if (borrowed.Count > 0) sb.AppendLine("  Stand-ins: " + string.Join(", ", borrowed));
            var missing = slots.Where(x => x.clip == null).Select(x => x.name).ToList();
            if (missing.Count > 0) sb.AppendLine("  Missing (optional): " + string.Join(", ", missing));
            sb.AppendLine();
        }

        static void ShowReport(string slotReport, List<string> unmatched, List<string> characters, string sceneResult)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Characters: " + string.Join(", ", characters.Select(Path.GetFileNameWithoutExtension)));
            sb.AppendLine();
            sb.Append(slotReport);
            if (unmatched.Count > 0)
            {
                sb.AppendLine("Not recognised (rename them to a slot name, e.g. Jab.fbx): " + string.Join(", ", unmatched));
                sb.AppendLine();
            }
            sb.AppendLine(sceneResult);
            sb.AppendLine();
            sb.AppendLine("Press Play to fight!");

            Debug.Log("[Madras Gethu] Character setup\n" + sb);
            ReportWindow.Show("Characters ready. Semma!", sb.ToString());
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

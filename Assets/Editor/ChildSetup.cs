using System.Collections.Generic;
using System.IO;
using ChildGame.Hub;
using ChildGame.MiniGames;
using ChildGame.MiniGames.Caro;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ChildGame.EditorTools
{
    /// <summary>
    /// Headless project setup and builds (see tools/*.sh):
    /// <c>Unity.exe -batchmode -quit -projectPath src -executeMethod ChildGame.EditorTools.ChildSetup.Configure</c>
    /// </summary>
    public static class ChildSetup
    {
        public const string SceneFolder = "Assets/Child/Scenes";
        public const string HubScenePath = SceneFolder + "/" + SceneFlow.HubScene + ".unity";
        public const string PlaceholderScene = "MG_Placeholder";
        private const string MiniGameFolder = "Assets/Child/Resources/MiniGames";

        // ⚠ Development identity, following the owner's other projects. Not the store identity (the title is undecided).
        private const string CompanyName = "thestone"; // studio name (owner 2026-10-09)
        private const string ProductName = "Childhood Days"; // official name (owner 2026-10-08); Vietnamese: "Những ngày thơ ấu"
        private const string BundleId = "com.thachnguyen.childhood";

        // Release version: raise VersionCode by one for every upload to Google Play or App Store Connect (it is also the
        // iOS build number).
        public const string VersionName = "1.0.0";
        public const int VersionCode = 2;
        public const string IosMinimumVersion = "15.0";
        private const int TargetSdk = 36; // Google Play's required target API level (2026)

        [MenuItem("Child/Developer/Configure Project and Rebuild Scenes")]
        public static void Configure()
        {
            ImportTmpEssentials();
            HandArtBaker.Bake();
            ImportOwnerArt();
            ConfigurePlayer();
            ConfigureIcon();
            IncludeTmpShaders();
            Directory.CreateDirectory(SceneFolder);
            BuildHubScene();
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(HubScenePath, true) };
            foreach (var m in MiniGames)
            {
                var def = Define(m);
                scenes.Add(new EditorBuildSettingsScene(BuildMiniGameScene(m, def), true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[CHILD-SETUP] Done.");
        }

        /// <summary>
        /// The owner's art (Resources/Art/Hub): imported as full-quality sprites (no mipmaps, high-quality compression,
        /// up to 2048 px), pivot at the bottom centre.
        /// </summary>
        private static void ImportOwnerArt()
        {
            foreach (var path in new[] { "Assets/Child/Resources/Art/Hub/yard.png" })
            {
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                importer.SetTextureSettings(settings);
                importer.spritePixelsPerUnit = 768f / 10.8f; // the picture is 10.8 units tall (the screen height) at scale 1
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }


        /// <summary>
        /// The app icon (owner 2026-10-08: Art/Icon/child_source.png): a centred square crop for the standard and round
        /// icons; for Android's adaptive icon the whole picture is shrunk into the safe zone over its own sky colour, so
        /// the launcher's mask never cuts the kite or the child.
        /// </summary>
        private static void ConfigureIcon()
        {
            const string source = "Assets/Child/Art/Icon/child_source.png";
            if (!File.Exists(source)) return;
            var src = new Texture2D(2, 2);
            src.LoadImage(File.ReadAllBytes(source));
            var side = Mathf.Min(src.width, src.height);
            var square = new Texture2D(side, side, TextureFormat.RGBA32, false);
            square.SetPixels(src.GetPixels((src.width - side) / 2, (src.height - side) / 2, side, side));
            square.Apply();
            const int adaptive = 432;
            var sky = src.GetPixel(src.width / 2, src.height - 4);
            var back = new Texture2D(adaptive, adaptive, TextureFormat.RGBA32, false);
            var fill = new Color[adaptive * adaptive];
            for (var i = 0; i < fill.Length; i++) fill[i] = sky;
            back.SetPixels(fill);
            back.Apply();
            var front = new Texture2D(adaptive, adaptive, TextureFormat.RGBA32, false);
            const int inner = 288; // the adaptive icon's safe zone (66 of 108 dp)
            var clear = new Color[adaptive * adaptive];
            front.SetPixels(clear);
            for (var y = 0; y < inner; y++)
            for (var x = 0; x < inner; x++)
                front.SetPixel((adaptive - inner) / 2 + x, (adaptive - inner) / 2 + y, square.GetPixelBilinear((x + 0.5f) / inner, (y + 0.5f) / inner));
            front.Apply();
            var paths = new[] { "Assets/Child/Art/Icon/app_icon.png", "Assets/Child/Art/Icon/app_icon_back.png", "Assets/Child/Art/Icon/app_icon_front.png" };
            File.WriteAllBytes(paths[0], square.EncodeToPNG());
            File.WriteAllBytes(paths[1], back.EncodeToPNG());
            File.WriteAllBytes(paths[2], front.EncodeToPNG());
            AssetDatabase.Refresh();
            foreach (var p in paths)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(p);
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(paths[0]);
            var iconBack = AssetDatabase.LoadAssetAtPath<Texture2D>(paths[1]);
            var iconFront = AssetDatabase.LoadAssetAtPath<Texture2D>(paths[2]);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            foreach (var kind in new[] { UnityEditor.Android.AndroidPlatformIconKind.Legacy, UnityEditor.Android.AndroidPlatformIconKind.Round })
            {
                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                foreach (var i in icons) i.SetTexture(icon);
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
            }
            var adaptiveIcons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, UnityEditor.Android.AndroidPlatformIconKind.Adaptive);
            foreach (var i in adaptiveIcons)
            {
                i.SetTexture(iconBack, 0);
                i.SetTexture(iconFront, 1);
            }
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, UnityEditor.Android.AndroidPlatformIconKind.Adaptive, adaptiveIcons);

            // iOS: one opaque 1024 × 1024 icon (the App Store rejects an icon with an alpha channel) for every slot,
            // including the App Store marketing icon. ⚠ Upscaled from the owner's 558 × 512 picture.
            const int iosSize = 1024;
            var ios = new Texture2D(iosSize, iosSize, TextureFormat.RGB24, false);
            var paper = new Color32(251, 245, 232, 255);
            for (var y = 0; y < iosSize; y++)
            for (var x = 0; x < iosSize; x++)
            {
                var c = square.GetPixelBilinear((x + 0.5f) / iosSize, (y + 0.5f) / iosSize);
                ios.SetPixel(x, y, Color.Lerp(paper, new Color(c.r, c.g, c.b, 1f), c.a));
            }
            ios.Apply();
            const string iosPath = "Assets/Child/Art/Icon/app_icon_ios.png";
            File.WriteAllBytes(iosPath, ios.EncodeToPNG());
            AssetDatabase.ImportAsset(iosPath);
            var iosImporter = (TextureImporter)AssetImporter.GetAtPath(iosPath);
            iosImporter.textureType = TextureImporterType.Default;
            iosImporter.mipmapEnabled = false;
            iosImporter.maxTextureSize = 1024;
            iosImporter.textureCompression = TextureImporterCompression.Uncompressed;
            iosImporter.alphaSource = TextureImporterAlphaSource.None;
            iosImporter.SaveAndReimport();
            var iosIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(iosPath);
            foreach (var kind in new[] { IconKind.Application, IconKind.Store, IconKind.Settings, IconKind.Notification, IconKind.Spotlight })
            {
                var slots = PlayerSettings.GetIconSizes(NamedBuildTarget.iOS, kind).Length;
                var list = new Texture2D[slots];
                for (var i = 0; i < slots; i++) list[i] = iosIcon;
                if (slots > 0) PlayerSettings.SetIcons(NamedBuildTarget.iOS, list, kind);
            }
            Debug.Log("[CHILD-SETUP] App icon set.");
        }
        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, BundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);

            // iOS (docs/7-ios-release.md): iOS 15 is both Unity 6.3's and current Xcode's minimum; the build number follows
            // VersionCode. The microphone text is required because the Tắm sông code references the microphone (it is only
            // used on Android).
            PlayerSettings.iOS.targetOSVersionString = IosMinimumVersion;
            PlayerSettings.iOS.buildNumber = VersionCode.ToString();
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.iOS.appleDeveloperTeamID = "T55W9XVM6P"; // owner 2026-10-12: signing team, preselected in Xcode
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.microphoneUsageDescription = "Childhood Days listens for loud sounds in River Swim only to tell when the dragonfly flies away. Nothing is recorded.";
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);

            // Landscape (side views fit most childhood activities); either way up.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)TargetSdk;
            PlayerSettings.bundleVersion = VersionName;
            PlayerSettings.Android.bundleVersionCode = VersionCode;

            // Web: one gzip build that works on any static host (decompression fallback for hosts without the
            // Content-Encoding header), the game's own page (Assets/WebGLTemplates/Childhood) and cached data.
            PlayerSettings.WebGL.template = "PROJECT:Childhood";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Minimal);

            // Windows builds are only for development captures.
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
        }

        /// <summary>TMP settings and shaders live in "TMP Essential Resources" inside the uGUI package.</summary>
        private static void ImportTmpEssentials()
        {
            if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro")) return;
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_Text).Assembly);
            var package = Path.Combine(info.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
            AssetDatabase.ImportPackage(package, false);
            AssetDatabase.Refresh();
            Debug.Log("[CHILD-SETUP] Imported TMP Essential Resources.");
        }

        /// <summary>Fonts are created at runtime (<see cref="UI.Ui.Bold"/>), so their shaders must always ship.</summary>
        private static void IncludeTmpShaders()
        {
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0];
            var so = new SerializedObject(settings);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            foreach (var name in new[] { "TextMeshPro/Mobile/Distance Field", "TextMeshPro/Distance Field" })
            {
                var shader = Shader.Find(name);
                if (shader == null)
                {
                    Debug.LogWarning($"[CHILD-SETUP] Shader not found: {name}");
                    continue;
                }
                var present = false;
                for (var i = 0; i < list.arraySize; i++) present |= list.GetArrayElementAtIndex(i).objectReferenceValue == shader;
                if (present) continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>One entry per mini game: its definition fields and the MiniGame component its scene gets.</summary>
        private struct MiniGameSpec
        {
            public string id, scene, key;
            public string[] story;
            public int order;
            public Vector2 hub;
            public Color color;
            public System.Type game;
            public Color background;
            public bool devOnly, special;
            public int dailyLimit;
            public string unlockItem;
            public string[] win, lose, complete;
            public StorySegment[] extras;
            public int paper;
            public bool sandals;
        }

        // Hub layout (placeholder): two rows of four blocks; row 1 at y 1.4, row 2 at y -2.0.
        private static readonly MiniGameSpec[] MiniGames =
        {
            new MiniGameSpec
            {
                id = "ve", scene = "MG01_VeTranh", key = "mg.ve", order = 1,
                story = new[] { "mg.ve.story.1", "mg.ve.story.2" }, complete = new[] { "mg.ve.complete.1" },
                hub = new Vector2(6.4f, -2.0f), color = new Color32(255, 240, 160, 255), game = typeof(MiniGames.VeTranh.VeTranhGame),
                background = new Color32(196, 150, 100, 255), unlockItem = "crayons", paper = 1,
            },
            new MiniGameSpec
            {
                id = "caro", scene = "MG02_Caro", key = "mg.caro", order = 2,
                story = new[] { "mg.caro.story.1", "mg.caro.story.2", "mg.caro.story.3", "mg.caro.story.4", "mg.caro.story.5", "mg.caro.story.6" },
                win = new[] { "mg.caro.win.1" }, lose = new[] { "mg.caro.lose.1" }, complete = new[] { "mg.caro.complete.1" },
                hub = new Vector2(-6.4f, 1.4f), color = new Color32(200, 225, 255, 255), game = typeof(CaroGame),
                background = new Color32(176, 122, 76, 255), paper = 1,
            },
            new MiniGameSpec
            {
                id = "dieu", scene = "MG03_ThaDieu", key = "mg.dieu", order = 3,
                story = new[] { "mg.dieu.story.1", "mg.dieu.story.2", "mg.dieu.story.3" },
                win = new[] { "mg.dieu.win.1", "mg.dieu.win.2", "mg.dieu.end.1", "mg.dieu.end.2" },
                lose = new[] { "mg.dieu.lose.1", "mg.dieu.lose.2", "mg.dieu.end.1", "mg.dieu.end.2" },
                hub = new Vector2(-2.1f, 1.4f), color = new Color32(255, 190, 170, 255), game = typeof(MiniGames.ThaDieu.ThaDieuGame),
                background = new Color32(255, 214, 160, 255), unlockItem = "kite",
            },
            new MiniGameSpec
            {
                id = "xoai", scene = "MG04_TromXoai", key = "mg.xoai", order = 4,
                story = new[] { "mg.xoai.story.1", "mg.xoai.story.2", "mg.xoai.story.3", "mg.xoai.story.4", "mg.xoai.story.5" },
                hub = new Vector2(2.1f, 1.4f), color = new Color32(255, 214, 120, 255), game = typeof(MiniGames.TromXoai.TromXoaiGame),
                background = new Color32(160, 212, 240, 255), sandals = true,
                extras = new[]
                {
                    new StorySegment { titleKey = "story.part.xoai.talk", keys = ChildGame.MiniGames.TromXoai.TromXoaiGame.SandalTalk, seenFlag = "sandaltalk" },
                    new StorySegment { titleKey = "story.part.xoai.loot", keys = ChildGame.MiniGames.TromXoai.TromXoaiGame.LootEnding },
                    new StorySegment { titleKey = "story.part.xoai.stuck", keys = ChildGame.MiniGames.TromXoai.TromXoaiGame.StuckEnding },
                    new StorySegment { titleKey = "story.part.xoai.fight", keys = ChildGame.MiniGames.TromXoai.TromXoaiGame.FightStory, seenFlag = StoryBook.Id(ChildGame.MiniGames.TromXoai.TromXoaiGame.FightEnding) },
                },
            },
            new MiniGameSpec
            {
                id = "oanquan", scene = "MG05_OAnQuan", key = "mg.oaq", order = 5,
                story = new[] { "mg.oaq.story.0", "mg.oaq.story.1" }, complete = new[] { "mg.oaq.complete.1" },
                hub = new Vector2(6.4f, 1.4f), color = new Color32(225, 200, 170, 255), game = typeof(MiniGames.OAnQuan.OAnQuanGame),
                background = new Color32(196, 160, 112, 255),
            },
            new MiniGameSpec
            {
                id = "mocau", scene = "MG06_DuaMoCau", key = "mg.mocau", order = 6,
                story = new[] { "mg.mocau.story.1", "mg.mocau.story.2", "mg.mocau.story.3" }, complete = new[] { "mg.mocau.complete.1" },
                hub = new Vector2(-6.4f, -2.0f), color = new Color32(230, 205, 150, 255), game = typeof(MiniGames.MoCau.MoCauGame),
                background = new Color32(118, 178, 84, 255),
            },
            new MiniGameSpec
            {
                id = "chuon", scene = "MG07_ChuonChuon", key = "mg.chuon", story = new string[0], order = 7,
                complete = new[] { "mg.chuon.complete.1" },
                extras = new[] { new StorySegment { titleKey = "story.part.chuon.learn", keys = ChildGame.MiniGames.ChuonChuon.ChuonChuonGame.LearnStory, seenFlag = "swim" } },
                hub = new Vector2(-2.1f, -2.0f), color = new Color32(170, 215, 240, 255), game = typeof(MiniGames.ChuonChuon.ChuonChuonGame),
                background = new Color32(176, 216, 240, 255),
            },
            new MiniGameSpec
            {
                id = "toc", scene = "SP01_NhoToc", key = "mg.toc", order = 101,
                story = new[] { "mg.toc.story.1", "mg.toc.story.2", "mg.toc.story.3" }, win = new[] { "mg.toc.win.1", "mg.toc.win.2" },
                hub = new Vector2(2.1f, -2.0f), color = new Color32(235, 225, 210, 255), game = typeof(MiniGames.NhoToc.NhoTocGame),
                background = new Color32(214, 190, 140, 255), special = true, dailyLimit = 3,
            },
            // ⚠ Test-only flow check: hidden from the hub except in capture runs.
            new MiniGameSpec
            {
                id = "placeholder", scene = PlaceholderScene, key = "mg.placeholder",
                story = new[] { "mg.placeholder.story.1", "mg.placeholder.story.2" }, order = 999,
                hub = new Vector2(0f, -4.3f), color = new Color32(235, 235, 235, 255), game = typeof(PlaceholderTapGame),
                background = new Color32(255, 240, 210, 255), devOnly = true,
            },
        };

        /// <summary>Creates or updates the definition asset. Fields are Claude-owned and rewritten every run.</summary>
        private static MiniGameDefinition Define(MiniGameSpec m)
        {
            Directory.CreateDirectory(MiniGameFolder);
            var path = $"{MiniGameFolder}/{m.id}.asset";
            var def = AssetDatabase.LoadAssetAtPath<MiniGameDefinition>(path);
            var created = def == null;
            if (created) def = ScriptableObject.CreateInstance<MiniGameDefinition>();
            def.id = m.id;
            def.sceneName = m.scene;
            def.titleKey = m.key + ".title";
            def.instructionsKey = m.key + ".instructions";
            def.storyKeys = m.story;
            def.winKeys = m.win ?? new string[0];
            def.loseKeys = m.lose ?? new string[0];
            def.completeKeys = m.complete ?? new string[0];
            def.extraStories = new List<StorySegment>(m.extras ?? new StorySegment[0]);
            def.order = m.order;
            def.hubPosition = m.hub;
            def.hubColor = m.color;
            def.devOnly = m.devOnly;
            def.hasDifficulty = !m.special;
            def.dailyLimit = m.dailyLimit;
            def.unlockItem = m.unlockItem ?? "";
            def.paperPerPlay = m.paper;
            def.needsSandals = m.sandals;
            if (created) AssetDatabase.CreateAsset(def, path);
            else EditorUtility.SetDirty(def);
            return def;
        }

        private static void BuildHubScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddCamera(new Color32(150, 214, 245, 255));
            AddGlobalLight();
            new GameObject("Hub").AddComponent<HubController>();
            EditorSceneManager.SaveScene(scene, HubScenePath);
        }

        private static string BuildMiniGameScene(MiniGameSpec m, MiniGameDefinition def)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddCamera(m.background);
            AddGlobalLight();
            var session = new GameObject("Session").AddComponent<MiniGameSession>();
            var so = new SerializedObject(session);
            so.FindProperty("fallbackDefinition").objectReferenceValue = def;
            so.ApplyModifiedPropertiesWithoutUndo();
            new GameObject("Game").AddComponent(m.game);
            var path = $"{SceneFolder}/{m.scene}.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        /// <summary>Orthographic 2D camera: 10.8 world units tall = 1080 px at 100 px per unit.</summary>
        private static void AddCamera(Color background)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5.4f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.transform.position = new Vector3(0, 0, -10);
            go.AddComponent<UniversalAdditionalCameraData>();
            go.AddComponent<AudioListener>();
        }

        private static void AddGlobalLight()
        {
            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
        }

        // ---- Builds ----

        /// <summary>
        /// Resolves the Android libraries (LevelPlay's mediation SDK, from Assets/LevelPlay/Editor/*Dependencies.xml) into
        /// the Gradle template before an Android build. The resolver is called by reflection: it is a plugin DLL.
        /// </summary>
        private static void ResolveAndroidDependencies()
        {
            System.Type type = null;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                type ??= asm.GetType("GooglePlayServices.PlayServicesResolver");
            var resolve = type?.GetMethod("ResolveSync", new[] { typeof(bool) });
            if (resolve == null)
            {
                Debug.LogError("[CHILD-BUILD] Android dependency resolver not found.");
                return;
            }
            var ok = (bool)resolve.Invoke(null, new object[] { true });
            Debug.Log($"[CHILD-BUILD] Android dependencies resolved: {ok}");
        }

        /// <summary>A debug-signed development APK at <c>build/child-dev.apk</c>.</summary>
        public static void BuildApk()
        {
            PlayerSettings.Android.useCustomKeystore = false;
            EditorUserBuildSettings.buildAppBundle = false;
            ResolveAndroidDependencies();
            Build("../build/child-dev.apk", BuildTarget.Android, BuildOptions.Development);
        }

        /// <summary>
        /// The Google Play release: a signed Android App Bundle at <c>build/release/childhood-VERSION.aab</c>. The upload
        /// key lives outside the repository (keys/ beside it, see docs/4-release.md); its passwords come from
        /// keys/keystore.properties.
        /// </summary>
        public static void BuildAab()
        {
            var keys = Path.GetFullPath("../../child-keys");
            var props = new Dictionary<string, string>();
            foreach (var line in File.ReadAllLines(Path.Combine(keys, "keystore.properties")))
            {
                var i = line.IndexOf('=');
                if (i > 0) props[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
            }
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = Path.Combine(keys, props["storeFile"]);
            PlayerSettings.Android.keystorePass = props["storePassword"];
            PlayerSettings.Android.keyaliasName = props["keyAlias"];
            PlayerSettings.Android.keyaliasPass = props["keyPassword"];
            EditorUserBuildSettings.buildAppBundle = true;
            ResolveAndroidDependencies();
            // Symbols for Play Console crash reports.
            UnityEditor.Android.UserBuildSettings.DebugSymbols.level = Unity.Android.Types.DebugSymbolLevel.SymbolTable;
            Build($"../build/release/childhood-{VersionName}.aab", BuildTarget.Android, BuildOptions.None);
            PlayerSettings.Android.useCustomKeystore = false;
            EditorUserBuildSettings.buildAppBundle = false;
        }

        /// <summary>
        /// The iOS release as an Xcode project at <c>build/ios</c>, to finish on a Mac (docs/7-ios-release.md). IosExport
        /// fixes the Podfile and writes mac-setup.sh.
        /// </summary>
        public static void BuildIos() => Build("../build/ios", BuildTarget.iOS, BuildOptions.None);

        /// <summary>The web release at <c>build/web</c>: upload the whole folder to any static host.</summary>
        public static void BuildWeb() => Build("../build/web", BuildTarget.WebGL, BuildOptions.None);

        /// <summary>
        /// The CrazyGames build at <c>build/crazygames</c> (docs/5-crazygames.md): the CRAZYGAMES define turns on their SDK
        /// (ads, gameplay events, account save), with their own page and Brotli files (smaller download; the decompression
        /// fallback makes them load on any host). The plain web settings are put back afterwards.
        /// </summary>
        public static void BuildCrazyGames()
        {
            PlayerSettings.WebGL.template = "PROJECT:CrazyGames";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            Build("../build/crazygames", BuildTarget.WebGL, BuildOptions.None, new[] { "CRAZYGAMES" });
            PlayerSettings.WebGL.template = "PROJECT:Childhood";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        }
        /// <summary>A Windows development build at <c>build/windows/Child.exe</c>, used for screenshots.</summary>
        public static void BuildWindows() => Build("../build/windows/Child.exe", BuildTarget.StandaloneWindows64, BuildOptions.Development);

        /// <summary>A Windows build without the "Development Build" mark, for store screenshots (tools/store-shots.sh).</summary>
        public static void BuildWindowsClean() => Build("../build/windows-clean/Child.exe", BuildTarget.StandaloneWindows64, BuildOptions.None);

        private static void Build(string relativePath, BuildTarget target, BuildOptions options, string[] defines = null)
        {
            var path = Path.GetFullPath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            var scenes = new string[EditorBuildSettings.scenes.Length];
            for (var i = 0; i < scenes.Length; i++) scenes[i] = EditorBuildSettings.scenes[i].path;
            if (scenes.Length == 0)
            {
                Debug.LogError("[CHILD-BUILD] No scenes; run ChildSetup.Configure first.");
                EditorApplication.Exit(1);
                return;
            }
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = target,
                options = options,
                extraScriptingDefines = defines,
            });
            Debug.Log($"[CHILD-BUILD] {report.summary.result}: {path}");
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}

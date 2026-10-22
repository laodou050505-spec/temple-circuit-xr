using System.IO;
using System.Linq;
using ByteDance.PICO.XR;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;

namespace TempleCircuitEditor
{
    public static class TemplePicoProjectSetup
    {
        const string SettingsKey = "ByteDance.PICO.XR.Settings";
        const string LoaderType = "ByteDance.PICO.XR.PXR_Loader";
        const string AndroidAppIconAssetPath = "Assets/Game/UI/AppIcon/TempleCircuitAppIcon.png";
        const int ReferenceWidth = 1920;
        const int ReferenceHeight = 1080;

        // EN: all menu paths use English product slug; bundle ID and productName updated for EN edition
        [MenuItem("Tools/IncenseCrossing/PICO/Configure Android & PICO XR")]
        public static void ConfigurePico()
        {
            PlayerSettings.companyName = "TempleCircuit";
            // EN: productName updated from "香火争渡" to "Incense Crossing"
            PlayerSettings.productName = "Incense Crossing";
            // EN: bundle ID updated to English edition identifier
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.templecircuit.monopoly.en");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3, GraphicsDeviceType.Vulkan });
            PlayerSettings.colorSpace = ColorSpace.Linear;
            ConfigureDisplay();
            ApplyAndroidAppIcon();

            EnsurePicoLoader();
            var settings = PXR_Settings.GetSettings();
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PXR_Settings>();
                EditorBuildSettings.AddConfigObject(SettingsKey, settings, true);
            }
            settings.appMode = PXR_Settings.AppMode.XR;
            settings.stereoRenderingModeAndroid = PXR_Settings.StereoRenderingModeAndroid.Multiview;
            settings.optimizeBufferDiscards = true;
            EditorUtility.SetDirty(settings);

            var projectConfig = PXR_ProjectSetting.GetProjectConfig();
            projectConfig.videoSeeThrough = true;
            projectConfig.mrSafeguard = true;
            projectConfig.sceneCapture = false;
            projectConfig.enableRecommendMSAA = true;
            projectConfig.recommendMSAA = true;
            projectConfig.portalFirstSelected = 1;
            EditorUtility.SetDirty(projectConfig);
            PXR_ProjectSetting.SaveAssets();

            EnsurePicoRenderPipeline();
            EnsureSceneInBuild();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Android, BuildTarget.Android);
            Debug.Log("PICO configured (EN): official PXR loader, XR mode, video passthrough, MR Safeguard, Multiview, OpenGLES3/Vulkan, Android API 29+, ARM64, IL2CPP, Activity and Input System-only project setting.");
        }

        static void EnsurePicoLoader()
        {
            var perTarget = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget")
                .Select(guid => AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault();
            if (perTarget == null)
            {
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, "Assets/XRGeneralSettingsPerBuildTarget.asset");
            }

            var general = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            if (general == null)
            {
                general = ScriptableObject.CreateInstance<XRGeneralSettings>();
                general.name = "PICO Android XR Settings";
                AssetDatabase.AddObjectToAsset(general, perTarget);
                perTarget.SetSettingsForBuildTarget(BuildTargetGroup.Android, general);
            }
            if (general.Manager == null)
            {
                var manager = ScriptableObject.CreateInstance<XRManagerSettings>();
                manager.name = "PICO XR Manager";
                AssetDatabase.AddObjectToAsset(manager, perTarget);
                general.Manager = manager;
            }

            foreach (var loader in general.Manager.activeLoaders.ToArray())
                XRPackageMetadataStore.RemoveLoader(general.Manager, loader.GetType().FullName, BuildTargetGroup.Android);
            if (!XRPackageMetadataStore.AssignLoader(general.Manager, LoaderType, BuildTargetGroup.Android) &&
                !XRPackageMetadataStore.AssignLoader(general.Manager, "PXR_Loader", BuildTargetGroup.Android))
                throw new BuildFailedException("Could not assign the official PICO PXR_Loader.");

            EditorUtility.SetDirty(general.Manager);
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(perTarget);
        }

        static void EnsureSceneInBuild()
        {
            const string scenePath = "Assets/Scenes/SampleScene.unity";
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(scene => scene.path != scenePath))
                scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void ApplyAndroidAppIcon()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(AndroidAppIconAssetPath);
            if (icon == null)
                throw new BuildFailedException("Android/PICO app icon is missing: " + AndroidAppIconAssetPath);

            var kinds = PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android);
            var assignedSlots = 0;
            foreach (var kind in kinds)
            {
                var slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                foreach (var slot in slots)
                {
                    for (var layer = 0; layer < slot.maxLayerCount; layer++)
                        slot.SetTexture(icon, layer);
                    assignedSlots++;
                }
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, slots);
            }

            Debug.Log($"PICO app icon assigned from {AndroidAppIconAssetPath} to {assignedSlots} Android icon slots.");
        }

        [MenuItem("Tools/IncenseCrossing/PICO/Verify Project Configuration")]
        public static void VerifyPico()
        {
            var errors = new System.Collections.Generic.List<string>();
            var perTarget = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            var hasLoader = perTarget != null && perTarget.AssignedSettings != null && perTarget.AssignedSettings.activeLoaders.Any(loader => loader.GetType().FullName == LoaderType);
            if (!hasLoader) errors.Add("PXR_Loader is not assigned for Android.");
            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP) errors.Add("Android scripting backend is not IL2CPP.");
            if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64) errors.Add("Android architecture is not ARM64-only.");
            if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel29) errors.Add("Minimum Android API is below 29.");
            if (PlayerSettings.Android.applicationEntry != AndroidApplicationEntry.Activity) errors.Add("Android entry point is not Activity.");
            var settings = PXR_Settings.GetSettings();
            if (settings == null || settings.stereoRenderingModeAndroid != PXR_Settings.StereoRenderingModeAndroid.Multiview) errors.Add("PICO Multiview is not enabled.");
            var projectConfig = PXR_ProjectSetting.GetProjectConfig();
            if (projectConfig == null || !projectConfig.videoSeeThrough) errors.Add("PICO video passthrough is not enabled.");
            if (projectConfig == null || !projectConfig.mrSafeguard) errors.Add("PICO MR Safeguard is not enabled.");
            var mobilePipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            var serializedPipeline = mobilePipeline == null ? null : new SerializedObject(mobilePipeline);
            var alphaOutput = serializedPipeline?.FindProperty("m_AllowPostProcessAlphaOutput");
            if (alphaOutput == null || !alphaOutput.boolValue)
                errors.Add("Mobile URP post-processing does not preserve the transparent VST alpha channel.");
            var supportsHdr = serializedPipeline?.FindProperty("m_SupportsHDR");
            if (supportsHdr == null || supportsHdr.boolValue)
                errors.Add("Mobile URP HDR must stay disabled for the transparent MR eye target.");
            var shadowDistance = serializedPipeline?.FindProperty("m_ShadowDistance");
            if (shadowDistance == null || shadowDistance.floatValue > 12.01f)
                errors.Add("Mobile URP shadow distance exceeds the tabletop rendering budget.");
            var additionalLightLimit = serializedPipeline?.FindProperty("m_AdditionalLightsPerObjectLimit");
            if (additionalLightLimit == null || additionalLightLimit.intValue > 2)
                errors.Add("Mobile URP additional-light limit exceeds the tabletop rendering budget.");
            var inputHandler = File.ReadAllText("ProjectSettings/ProjectSettings.asset").Contains("activeInputHandler: 1");
            if (!inputHandler) errors.Add("Input System is not the single active input backend.");

            if (errors.Count > 0)
                throw new BuildFailedException("PICO validation failed:\n- " + string.Join("\n- ", errors));
            Debug.Log("PICO validation passed (EN): loader, video passthrough, MR Safeguard, Multiview, API 29+, ARM64, IL2CPP, Activity and Input System-only are configured.");
        }

        [MenuItem("Tools/IncenseCrossing/Build/macOS Test Build")]
        public static void BuildMac()
        {
            ConfigureDisplay();
            EnsureDesktopRenderPipeline();
            EnsureSceneInBuild();
            Directory.CreateDirectory("Builds/EN");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = "Builds/EN/IncenseCrossing-EN-macOS.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development
            });
            if (result == null || result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new BuildFailedException("macOS build failed: " + (result == null ? "no build report" : result.summary.result.ToString()));
            Debug.Log("macOS test build (EN): Builds/EN/IncenseCrossing-EN-macOS.app");
        }

        [MenuItem("Tools/IncenseCrossing/Verify World Space & Design Start")]
        public static void VerifyWorldSpaceAndDesignStart()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            var game = Object.FindAnyObjectByType<TempleCircuit.TempleCircuitGame>();
            var rig = Object.FindAnyObjectByType<TempleCircuit.TempleXRTabletopRig>();
            if (game == null || rig == null || game.pathTiles == null || game.pathTiles.Length == 0)
                throw new BuildFailedException("World-space verification needs the Temple game, tabletop rig and board tiles.");
            if (rig.tabletopWidthMeters < 0.6f || rig.startPortalWidthMeters < 0.5f || rig.statusBoardWidthMeters < 0.3f)
                throw new BuildFailedException("World-space board or authored interaction surface dimensions are invalid.");
            rig.AlignGameplayStartWithStartTile();
            if (rig.designEyeOffsetFromTabletop.y <= 0.2f)
                throw new BuildFailedException("Design player start must stand above and outside the START edge.");
            if (game.dice == null || game.dice.GetComponent<Collider>() == null)
                throw new BuildFailedException("The world-space dice must have a collider for controller ray interaction.");
            var start = Object.FindAnyObjectByType<TempleCircuit.TempleStartScreen>();
            if (start == null)
                throw new BuildFailedException("Title-stage verification needs TempleStartScreen.");
            if (rig.titleDesignEyeOffsetFromTabletop.y <= 0.2f || rig.titleDesignLookAtOffsetFromTabletop.z >= -0.25f)
                throw new BuildFailedException("Title design start must face a freestanding title surface in front of the tabletop.");

            var center = Vector3.zero;
            foreach (var tile in game.pathTiles)
                center += tile.transform.position;
            center /= game.pathTiles.Length;
            var sourceUnitsPerMeter = Mathf.Max(0.0001f, EstimateBoardWidth(game) / rig.tabletopWidthMeters);
            var eye = center + rig.designEyeOffsetFromTabletop * sourceUnitsPerMeter;
            var landmark = center + rig.designLookAtOffsetFromTabletop * sourceUnitsPerMeter;
            var forward = landmark - eye;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
                throw new BuildFailedException("Design player start cannot coincide with the tabletop centre.");

            Debug.Log($"World-space verification passed (EN): {game.pathTiles.Length} board tiles and dice remain scene objects; " +
                      $"ray targets are collidable. Single design start is eye {eye:F3} facing board landmark {landmark:F3}; " +
                      "desktop camera, XR launch and recenter derive from TempleXRTabletopRig design start fields; runtime UI compensates for the tabletop root scale. " +
                      "Title stage uses its own authoritative title start and disables gameplay renderers, colliders, behaviours and audio until START.");
        }

        public static void CaptureDesktopDesignStart()
        {
            const string scenePath = "Assets/Scenes/SampleScene.unity";
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            EnsureDesktopRenderPipeline();
            var rig = Object.FindAnyObjectByType<TempleCircuit.TempleXRTabletopRig>();
            var game = Object.FindAnyObjectByType<TempleCircuit.TempleCircuitGame>();
            if (rig == null || game == null)
                throw new BuildFailedException("Desktop capture needs the Temple game and tabletop rig.");

            var camera = Camera.main;
            if (camera == null)
                throw new BuildFailedException("Desktop capture needs a Main Camera.");

            var boardWidth = EstimateBoardWidth(game);
            var boardCenter = Vector3.zero;
            foreach (var tile in game.pathTiles)
                boardCenter += tile.transform.position;
            boardCenter /= game.pathTiles.Length;
            var worldUnitsPerMeter = boardWidth / rig.tabletopWidthMeters;
            var eye = boardCenter + rig.designEyeOffsetFromTabletop * worldUnitsPerMeter;
            var landmark = boardCenter + rig.designLookAtOffsetFromTabletop * worldUnitsPerMeter;
            camera.transform.SetParent(null, true);
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.transform.position = eye;
            camera.transform.rotation = Quaternion.LookRotation(landmark - eye, Vector3.up);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 2000f;
            // EN: 52° desktop FOV as specified in handover document
            camera.fieldOfView = 52f;

            var outputDirectory = Path.GetFullPath("VisualQA/EN-20261022");
            Directory.CreateDirectory(outputDirectory);
            var outputPath = Path.Combine(outputDirectory, "Unity-GameView-DesignStart.png");
            var renderTexture = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32)
            {
                useMipMap = false,
                autoGenerateMips = false
            };
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            camera.targetTexture = renderTexture;
            var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest
            {
                destination = renderTexture
            };
            if (!UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(camera, request))
                throw new BuildFailedException("Desktop capture render request is not supported by the active URP pipeline.");
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = renderTexture;
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            image.Apply(false, false);
            File.WriteAllBytes(outputPath, image.EncodeToPNG());
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            Object.DestroyImmediate(renderTexture);
            Object.DestroyImmediate(image);
            Debug.Log($"Desktop design-start capture (EN): {outputPath}; eye {eye:F3}, landmark {landmark:F3}, yaw {rig.designFacingYawDegrees:F1}.");
        }

        static float EstimateBoardWidth(TempleCircuit.TempleCircuitGame game)
        {
            var minX = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var minZ = float.PositiveInfinity;
            var maxZ = float.NegativeInfinity;
            foreach (var tile in game.pathTiles)
            {
                if (tile == null)
                    continue;
                minX = Mathf.Min(minX, tile.transform.position.x);
                maxX = Mathf.Max(maxX, tile.transform.position.x);
                minZ = Mathf.Min(minZ, tile.transform.position.z);
                maxZ = Mathf.Max(maxZ, tile.transform.position.z);
            }
            return Mathf.Max(0.001f, Mathf.Max(maxX - minX, maxZ - minZ));
        }

        static void EnsureDesktopRenderPipeline()
        {
            var desktopPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if (desktopPipeline == null)
                throw new BuildFailedException("Desktop URP asset was not found.");
            GraphicsSettings.defaultRenderPipeline = desktopPipeline;
            QualitySettings.renderPipeline = desktopPipeline;
            EditorUtility.SetDirty(desktopPipeline);
        }

        [MenuItem("Tools/IncenseCrossing/PICO/Build PICO APK")]
        public static void BuildPicoApk()
        {
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                throw new BuildFailedException("Could not switch the active build target to Android.");
            EnsurePicoRenderPipeline();
            ConfigureDisplay();
            ApplyAndroidAppIcon();
            VerifyPico();
            VerifyWorldSpaceAndDesignStart();
            EnsureSceneInBuild();
            Directory.CreateDirectory("Builds/EN");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                // EN: updated output path for English edition APK
                locationPathName = "Builds/EN/IncenseCrossing-EN-PICO.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None
            });
            if (result == null || result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new BuildFailedException("PICO APK build failed (EN): " + (result == null ? "no build report" : result.summary.result.ToString()));
            Debug.Log("PICO release APK (EN): Builds/EN/IncenseCrossing-EN-PICO.apk");
        }

        static void EnsurePicoRenderPipeline()
        {
            var mobilePipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            if (mobilePipeline == null)
                throw new BuildFailedException("Mobile URP asset was not found.");
            var serializedPipeline = new SerializedObject(mobilePipeline);
            var alphaOutput = serializedPipeline.FindProperty("m_AllowPostProcessAlphaOutput");
            var supportsHdr = serializedPipeline.FindProperty("m_SupportsHDR");
            var shadowDistance = serializedPipeline.FindProperty("m_ShadowDistance");
            var additionalLightLimit = serializedPipeline.FindProperty("m_AdditionalLightsPerObjectLimit");
            if (alphaOutput == null || supportsHdr == null || shadowDistance == null || additionalLightLimit == null)
                throw new BuildFailedException("Required Mobile URP performance or alpha-output settings were not found.");

            alphaOutput.boolValue = true;
            supportsHdr.boolValue = false;
            shadowDistance.floatValue = 12f;
            additionalLightLimit.intValue = 2;
            serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
            GraphicsSettings.defaultRenderPipeline = mobilePipeline;
            QualitySettings.renderPipeline = mobilePipeline;
            EditorUtility.SetDirty(mobilePipeline);
        }

        static void ConfigureDisplay()
        {
            PlayerSettings.defaultScreenWidth = ReferenceWidth;
            PlayerSettings.defaultScreenHeight = ReferenceHeight;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = false;
        }
    }
}

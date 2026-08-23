using System.Collections.Generic;
using ByteDance.PICO.XR;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

namespace TempleCircuit
{
    [DefaultExecutionOrder(-1000)]
    public sealed class TempleXRTabletopRig : MonoBehaviour
    {
        static TempleXRTabletopRig activeRig;

        public TempleCircuitGame game;
        public TempleDiceInteractor dice;
        public TempleStartScreen startScreen;
        [Min(0.6f)] public float tabletopWidthMeters = 1.35f;
        public Vector3 tabletopCenter = new Vector3(0f, 0.84f, 1.05f);
        [Header("Authoritative gameplay design start (relative to tabletop centre)")]
        [Tooltip("The single authored gameplay eye position, in physical tabletop metres. Desktop, XR launch and recenter all derive from it.")]
        public Vector3 designEyeOffsetFromTabletop = new Vector3(0f, 0.68f, -1.18f);
        [Tooltip("The gameplay landmark the player faces. This is biased toward the START tile and the four pawns, not the board centre.")]
        public Vector3 designLookAtOffsetFromTabletop = new Vector3(0f, 0.02f, -0.50f);
        [Range(-180f, 180f)] public float designFacingYawDegrees;
        [Header("Authoritative title design start (relative to tabletop centre)")]
        [Tooltip("A separate title-stage eye position. It faces only the title surface; gameplay remains inactive.")]
        public Vector3 titleDesignEyeOffsetFromTabletop = new Vector3(0f, 0.78f, -1.1f);
        [Tooltip("Title-stage landmark. This is the freestanding title surface, not the board.")]
        public Vector3 titleDesignLookAtOffsetFromTabletop = new Vector3(0f, 0.78f, -0.45f);
        [Range(-180f, 180f)] public float titleDesignFacingYawDegrees;
        [Header("World-space primary surfaces (relative to tabletop centre)")]
        [Tooltip("A freestanding entry surface between the player and the table, never a camera child or board-back overlay.")]
        public Vector3 startPortalOffsetFromTabletop = new Vector3(0f, 0.78f, -0.45f);
        [Min(0.5f)] public float startPortalWidthMeters = 1.18f;
        [Tooltip("A world-fixed board above the tabletop. It may turn toward the player, but never follows the camera position.")]
        public Vector3 statusBoardOffsetFromTabletop = new Vector3(0f, 1.15f, 0.62f);
        [Min(0.3f)] public float statusBoardWidthMeters = 1.48f;
        [Min(1f)] public float rayLength = 5f;
        [Header("PICO visual background")]
        [Tooltip("The tabletop floats in the user's real environment. This must stay enabled for the PICO passthrough view.")]
        public bool useVideoSeeThrough = true;

        Camera headCamera;
        Transform leftController;
        Transform rightController;
        LineRenderer leftRay;
        LineRenderer rightRay;
        XRInputDevice leftDevice;
        XRInputDevice rightDevice;
        bool leftTriggerPrevious;
        bool rightTriggerPrevious;
        bool leftTriggerArmed;
        bool rightTriggerArmed;
        bool externalDiceHold;
        XRInputDevice holdingDevice;
        TempleWorldButton hoveredButton;
        Vector2 previousMouse;
        bool desktopOrbiting;
        bool desktopConfigured;
        bool xrConfigured;
        bool tabletopScaled;
        bool leftMenuPrevious;
        bool rightMenuPrevious;
        Vector3 desktopTarget;
        float desktopBoardRadius;
        float desktopYaw = -32f;
        float desktopPitch = 31f;
        float desktopDistance;
        float desktopTargetDistance;
        TempleMixedRealityPreview mixedRealityPreview;
        bool passthroughEnabled;
        bool passthroughStateApplied;
        bool passthroughEnablePending;
        bool cameraPermissionRequested;
        Coroutine passthroughEnableRoutine;
        PXR_ARCameraEffectManager arCameraEffectManager;
        readonly List<XRNodeState> nodeStates = new List<XRNodeState>();
        bool hasTrackedHeadPose;
        bool floorTrackingOriginApplied;
        bool designStartPending;
        int designStartEarliestFrame;
        Vector3 sourceBoardCenter;
        float sourceBoardWidth;
        bool boardGeometryCaptured;
        bool titleStage;

        bool XRActive => XRSettings.enabled && XRSettings.isDeviceActive;
        public bool IsTitleStage => titleStage;
        public Vector3 DesignPlayerEyeWorldPosition => GetWorldTablePoint(titleStage ? titleDesignEyeOffsetFromTabletop : designEyeOffsetFromTabletop);
        public Vector3 DesignPlayerLookAtWorldPosition => GetWorldTablePoint(titleStage ? titleDesignLookAtOffsetFromTabletop : designLookAtOffsetFromTabletop);
        public Quaternion DesignPlayerFacingRotation => Quaternion.Euler(0f, titleStage ? titleDesignFacingYawDegrees : designFacingYawDegrees, 0f);
        public float WorldUnitsPerTabletopMeter => tabletopScaled ? 1f : Mathf.Max(0.0001f, sourceBoardWidth / tabletopWidthMeters);

        void Awake()
        {
            if (activeRig != null && activeRig != this)
            {
                enabled = false;
                return;
            }
            activeRig = this;

            if (game == null)
                game = FindAnyObjectByType<TempleCircuitGame>();
            if (dice == null)
                dice = FindAnyObjectByType<TempleDiceInteractor>();
            if (startScreen == null)
                startScreen = FindAnyObjectByType<TempleStartScreen>();

            headCamera = Camera.main;
            if (headCamera == null)
            {
                var cameraObject = new GameObject("XR Head Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera";
                headCamera = cameraObject.GetComponent<Camera>();
            }

            mixedRealityPreview = GetComponent<TempleMixedRealityPreview>();
            if (mixedRealityPreview == null)
                mixedRealityPreview = gameObject.AddComponent<TempleMixedRealityPreview>();
            mixedRealityPreview.Initialize(headCamera);

            if (XRActive)
                EnsureXRMode();
        }

        void OnDisable()
        {
            mixedRealityPreview?.SetDesktopActive(false);
            SetPicoPassthrough(false);
        }

        void OnDestroy()
        {
            SetPicoPassthrough(false);
            if (activeRig == this)
                activeRig = null;
        }

        void Start()
        {
            if (XRActive)
                EnsureXRMode();
            else
                ConfigureDesktopCamera();
        }

        // World-space surfaces call this before they create their poses.
        public void PrepareWorldSpaceLayout()
        {
            CaptureBoardGeometry();
            if (XRActive)
                EnsureXRMode();
        }

        /// <summary>Changes the single current design start. Both desktop and XR derive from it.</summary>
        public void SetTitleStage(bool value)
        {
            titleStage = value;
            if (XRActive)
                ScheduleDesignStartSnap();
            else if (headCamera != null)
                ConfigureDesktopCamera();
        }

        void EnsureXRMode()
        {
            if (!tabletopScaled)
                ScaleSceneForTabletop();
            if (!xrConfigured)
                ConfigureXR();
        }

        void ConfigureXR()
        {
            // Startup and scene loading can happen before PICO publishes an
            // input subsystem. Keep retrying from UpdateXR until the floor
            // origin is actually accepted instead of leaving the scene at an
            // eye-level/unknown origin.
            floorTrackingOriginApplied = false;
            RequestFloorTrackingOrigin();

            headCamera.stereoTargetEye = StereoTargetEyeMask.Both;
            headCamera.transform.SetParent(transform, false);
            ConfigureCameraBackground();
            // PICO's own Video See-Through building block uses an alpha-clear,
            // non-HDR eye texture.  HDR can cause URP to resolve the clear
            // alpha as opaque black before the compositor receives it.
            headCamera.allowHDR = false;
            headCamera.allowMSAA = true;
            headCamera.nearClipPlane = 0.03f;
            headCamera.farClipPlane = 100f;
            mixedRealityPreview?.SetDesktopActive(false);
            // The manager must discover the actual stereo eye camera. Keep it
            // on that camera, matching the verified PICO MR presentation path,
            // rather than on this gameplay rig.
            var legacyRigManager = GetComponent<PXR_Manager>();
            if (legacyRigManager != null)
                Destroy(legacyRigManager);
            if (headCamera.GetComponent<PXR_Manager>() == null)
                headCamera.gameObject.AddComponent<PXR_Manager>();
            // PICO's VST compositor needs a real style data block in addition
            // to the transparent eye target.  Keep the official effect
            // manager on the stereo camera and initialise it with neutral
            // values: this creates the compositor data without tinting,
            // darkening, or otherwise changing the user's room.
            arCameraEffectManager = headCamera.GetComponent<PXR_ARCameraEffectManager>();
            if (arCameraEffectManager == null)
                arCameraEffectManager = headCamera.gameObject.AddComponent<PXR_ARCameraEffectManager>();
            arCameraEffectManager.enableCameraEffect = true;
            arCameraEffectManager.colortempValue = 0f;
            arCameraEffectManager.brightnessValue = 0f;
            arCameraEffectManager.saturationValue = 0f;
            arCameraEffectManager.contrastValue = 0f;

            RequestPassthroughCameraPermission();
            // The title panel is also a real-world-space object. Keep video
            // passthrough active for both title and gameplay; START only swaps
            // authored roots. Enable it after the eye layer's first frame, as
            // specified by PICO's minimal VST sample bootstrap.
            passthroughStateApplied = false;
            SetPicoPassthrough(true);
            if (leftController == null)
                leftController = CreateController("PICO Left Controller", new Color32(72, 162, 132, 255), out leftRay);
            if (rightController == null)
                rightController = CreateController("PICO Right Controller", new Color32(220, 181, 91, 255), out rightRay);
            // PICO Emulator may report a held trigger while it is starting. A ray
            // only becomes actionable after that trigger has been released once.
            leftTriggerPrevious = false;
            rightTriggerPrevious = false;
            leftTriggerArmed = false;
            rightTriggerArmed = false;
            xrConfigured = true;
            desktopConfigured = false;
            // A PICO emulator may not publish InputTracking data on its first
            // rendered frames. Place the floor-origin rig at the authored
            // start immediately with a nominal eye height, then refine it as
            // soon as a real 6DoF head pose arrives. This never moves the
            // tabletop, dice, pawns, or world-space panels.
            ApplyDesignStartWithAvailableHeadPose(false);
            ScheduleDesignStartSnap();
        }

        Transform CreateController(string name, Color color, out LineRenderer ray)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            ray = go.AddComponent<LineRenderer>();
            ray.useWorldSpace = true;
            ray.positionCount = 2;
            ray.startWidth = 0.007f;
            ray.endWidth = 0.0025f;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            ray.material = new Material(shader) { color = color };
            ray.startColor = color;
            ray.endColor = new Color(color.r, color.g, color.b, 0.12f);
            return go.transform;
        }

        void ScaleSceneForTabletop()
        {
            if (game == null || game.pathTiles == null || game.pathTiles.Length == 0)
                return;
            CaptureBoardGeometry();
            if (sourceBoardWidth < 0.01f)
                return;

            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var tabletop = new GameObject("PICO_Tabletop_RuntimeScale").transform;
            tabletop.position = sourceBoardCenter;
            foreach (var root in roots)
            {
                if (root == gameObject || root.GetComponent<Camera>() != null || root.GetComponentInChildren<Camera>(true) != null || root.GetComponent<UnityEngine.EventSystems.EventSystem>() != null)
                    continue;
                root.transform.SetParent(tabletop, true);
            }
            tabletop.localScale = Vector3.one * (tabletopWidthMeters / sourceBoardWidth);
            tabletop.position = tabletopCenter;
            tabletopScaled = true;
            // TempleDiceInteractor records its support in world coordinates.
            // Re-find that support after the authored scene is converted into
            // tabletop metres; retaining the source-scene support coordinate
            // was the reason the die could restore below the board in XR.
            dice?.SnapToSupportSurface();
            dice?.CaptureRestPose();
        }

        void CaptureBoardGeometry()
        {
            if (boardGeometryCaptured || game == null || game.pathTiles == null || game.pathTiles.Length == 0)
                return;

            var minX = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var minZ = float.PositiveInfinity;
            var maxZ = float.NegativeInfinity;
            var averageY = 0f;
            var count = 0;
            foreach (var tile in game.pathTiles)
            {
                if (tile == null)
                    continue;
                var position = tile.transform.position;
                minX = Mathf.Min(minX, position.x);
                maxX = Mathf.Max(maxX, position.x);
                minZ = Mathf.Min(minZ, position.z);
                maxZ = Mathf.Max(maxZ, position.z);
                averageY += position.y;
                count++;
            }

            if (count == 0)
                return;
            sourceBoardCenter = new Vector3((minX + maxX) * 0.5f, averageY / count, (minZ + maxZ) * 0.5f);
            sourceBoardWidth = Mathf.Max(maxX - minX, maxZ - minZ);
            boardGeometryCaptured = sourceBoardWidth >= 0.01f;
        }

        /// <summary>Maps authored tabletop metres into either the source or runtime-scaled world.</summary>
        public Vector3 GetWorldTablePoint(Vector3 tabletopOffsetMeters)
        {
            CaptureBoardGeometry();
            var center = tabletopScaled ? tabletopCenter : sourceBoardCenter;
            // Offsets are authored in the board's world axes. Facing yaw only controls the
            // player, never rotates the board or relocates its fixed interaction surfaces.
            return center + tabletopOffsetMeters * WorldUnitsPerTabletopMeter;
        }

        void Update()
        {
            if (XRActive)
            {
                EnsureXRMode();
                SetPicoPassthrough(true);
                UpdateXR();
            }
            else
            {
                SetPicoPassthrough(false);
                if (!desktopConfigured)
                    ConfigureDesktopCamera();
                // The desktop room preview is optional and must never become an
                // overlay between the editor camera and the world-space game.
                mixedRealityPreview?.SetDesktopActive(false);
                UpdateDesktopCamera();
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused && XRActive)
                SetPicoPassthrough(true);
            else if (paused)
                SetPicoPassthrough(false);
        }

        void SetPicoPassthrough(bool enabledState)
        {
            var shouldEnable = enabledState && useVideoSeeThrough;
            if (passthroughStateApplied && passthroughEnabled == shouldEnable &&
                (!shouldEnable || !passthroughEnablePending))
                return;
            passthroughEnabled = shouldEnable;
            if (!shouldEnable)
            {
                if (passthroughEnableRoutine != null)
                    StopCoroutine(passthroughEnableRoutine);
                passthroughEnableRoutine = null;
                passthroughEnablePending = false;
                passthroughStateApplied = true;
                if (XRActive)
                    PXR_Manager.EnableVideoSeeThrough = false;
                ConfigureCameraBackground();
                return;
            }

            if (!XRActive)
                return;

            // Video See-Through is camera-backed on PICO. Declaring CAMERA in
            // the manifest is not enough on current Android/PICO firmware: the
            // user must grant it at first launch before the compositor is asked
            // for a passthrough layer. Otherwise it correctly falls back to a
            // black opaque background.
            if (!HasPassthroughCameraPermission())
            {
                RequestPassthroughCameraPermission();
                return;
            }

            if (passthroughEnablePending)
                return;
            if (passthroughEnableRoutine != null)
                StopCoroutine(passthroughEnableRoutine);
            passthroughEnableRoutine = StartCoroutine(EnablePicoPassthroughAfterEyeLayerReady());
        }

        bool HasPassthroughCameraPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return Permission.HasUserAuthorizedPermission(Permission.Camera);
#else
            return true;
#endif
        }

        void RequestPassthroughCameraPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (HasPassthroughCameraPermission())
                return;
            if (cameraPermissionRequested)
                return;

            cameraPermissionRequested = true;
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += permission =>
            {
                if (permission != Permission.Camera)
                    return;
                cameraPermissionRequested = false;
                passthroughStateApplied = false;
                Debug.Log("Temple MR camera permission granted; enabling real-world passthrough.", this);
                SetPicoPassthrough(true);
            };
            callbacks.PermissionDenied += permission =>
            {
                if (permission == Permission.Camera)
                    Debug.LogWarning("Temple MR needs camera permission to show the real-world background.", this);
            };
            callbacks.PermissionDeniedAndDontAskAgain += permission =>
            {
                if (permission == Permission.Camera)
                    Debug.LogWarning("Temple MR camera permission was blocked. Enable Camera for 香火争渡 in PICO settings to use passthrough.", this);
            };
            Permission.RequestUserPermission(Permission.Camera, callbacks);
#endif
        }

        System.Collections.IEnumerator EnablePicoPassthroughAfterEyeLayerReady()
        {
            passthroughEnablePending = true;
            // PICO's own basic VST bootstrap enables video see-through after a
            // rendered frame. Calling it during XR loader setup can leave the
            // emulator with an opaque black eye texture instead of a composed
            // transparent MR eye layer.
            yield return null;
            yield return new WaitForEndOfFrame();

            if (this != null && enabled && XRActive && passthroughEnabled && useVideoSeeThrough)
            {
                PXR_Manager.EnableVideoSeeThrough = true;
                // This is the no-filter initialization call made by PICO's
                // official Video See-Through building block. Then submit the
                // complete official neutral style data.  Merely enabling the
                // effect can leave PICO Emulator 0.13 without an effect-data
                // allocation (which presents as an opaque black background).
                // No LUT, colour map or virtual backdrop is applied to the
                // real view.
                var effectResult = PXR_MixedReality.EnableVideoSeeThroughEffect(true);
                var colorTemperatureResult = PXR_MixedReality.SetVideoSeeThroughEffect(PxrLayerEffect.Colortemp, 0f, 0f);
                var brightnessResult = PXR_MixedReality.SetVideoSeeThroughEffect(PxrLayerEffect.Brightness, 0f, 0f);
                var saturationResult = PXR_MixedReality.SetVideoSeeThroughEffect(PxrLayerEffect.Saturation, 0f, 0f);
                var contrastResult = PXR_MixedReality.SetVideoSeeThroughEffect(PxrLayerEffect.Contrast, 0f, 0f);
                passthroughStateApplied = true;
                ConfigureCameraBackground();
                Debug.Log($"Temple PICO VST enabled after XR eye layer initialization " +
                          $"(enable {effectResult}; neutral style CT {colorTemperatureResult}, " +
                          $"brightness {brightnessResult}, saturation {saturationResult}, " +
                          $"contrast {contrastResult}).", this);
            }

            passthroughEnablePending = false;
            passthroughEnableRoutine = null;
        }

        void UpdateXR()
        {
            RequestFloorTrackingOrigin();
            leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            ReadTrackedHeadPose();
            if (designStartPending && hasTrackedHeadPose && Time.frameCount >= designStartEarliestFrame)
                ApplyDesignStartWithAvailableHeadPose(true);
            ApplyPose(leftDevice, leftController);
            ApplyPose(rightDevice, rightController);
            UpdateController(leftDevice, leftController, leftRay, ref leftTriggerPrevious, ref leftTriggerArmed);
            UpdateController(rightDevice, rightController, rightRay, ref rightTriggerPrevious, ref rightTriggerArmed);

            var leftMenu = leftDevice.TryGetFeatureValue(XRCommonUsages.menuButton, out var leftMenuValue) && leftMenuValue;
            var rightMenu = rightDevice.TryGetFeatureValue(XRCommonUsages.menuButton, out var rightMenuValue) && rightMenuValue;
            if ((leftMenu && !leftMenuPrevious) || (rightMenu && !rightMenuPrevious))
                Recenter();
            leftMenuPrevious = leftMenu;
            rightMenuPrevious = rightMenu;
        }

        void ReadTrackedHeadPose()
        {
            hasTrackedHeadPose = false;
            InputTracking.GetNodeStates(nodeStates);
            foreach (var state in nodeStates)
            {
                if (state.nodeType != XRNode.Head ||
                    !state.TryGetPosition(out var position) ||
                    !state.TryGetRotation(out var rotation))
                    continue;

                headCamera.transform.localPosition = position;
                headCamera.transform.localRotation = rotation;
                hasTrackedHeadPose = true;
                break;
            }
        }

        void RequestFloorTrackingOrigin()
        {
            if (floorTrackingOriginApplied)
                return;

            var inputSubsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(inputSubsystems);
            foreach (var subsystem in inputSubsystems)
            {
                if (!subsystem.running ||
                    (subsystem.GetSupportedTrackingOriginModes() & TrackingOriginModeFlags.Floor) == 0)
                    continue;

                floorTrackingOriginApplied = subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
                if (floorTrackingOriginApplied)
                {
                    Debug.Log("Temple XR tracking origin confirmed: Floor.", this);
                    return;
                }
            }
        }

        void ApplyPose(XRInputDevice device, Transform target)
        {
            if (target == null || !device.isValid)
                return;
            if (device.TryGetFeatureValue(XRCommonUsages.devicePosition, out var position))
                target.localPosition = position;
            if (device.TryGetFeatureValue(XRCommonUsages.deviceRotation, out var rotation))
                target.localRotation = rotation;
        }

        void UpdateController(XRInputDevice device, Transform controller, LineRenderer ray, ref bool triggerPrevious, ref bool triggerArmed)
        {
            if (controller == null || ray == null)
                return;
            var origin = controller.position;
            var direction = controller.forward;
            var end = origin + direction * rayLength;
            TempleWorldButton button = null;
            TempleDiceInteractor pointedDice = null;
            TempleStartSurface startSurface = null;
            var hitPoint = end;
            if (Physics.Raycast(origin, direction, out var hit, rayLength))
            {
                end = hit.point;
                hitPoint = hit.point;
                button = hit.transform.GetComponentInParent<TempleWorldButton>();
                pointedDice = hit.transform.GetComponentInParent<TempleDiceInteractor>();
                startSurface = hit.transform.GetComponentInParent<TempleStartSurface>();
            }
            ray.SetPosition(0, origin);
            ray.SetPosition(1, end);

            if (startScreen != null && !startScreen.GameStarted)
                startScreen.SetWorldHover(hitPoint, startSurface != null);

            if (button != hoveredButton)
            {
                hoveredButton?.SetHover(false);
                hoveredButton = button;
                hoveredButton?.SetHover(true);
            }

            var pressed = device.TryGetFeatureValue(XRCommonUsages.triggerButton, out var trigger) && trigger;
            // Do not let an emulator's initial controller state select START on
            // boot. Releasing the trigger once intentionally arms world input.
            if (!pressed)
                triggerArmed = true;
            if (pressed && !triggerPrevious && triggerArmed)
            {
                if (startSurface != null && startScreen != null && !startScreen.GameStarted)
                {
                    startSurface.Trigger(hitPoint);
                    device.SendHapticImpulse(0u, 0.22f, 0.06f);
                }
                else if (pointedDice != null && pointedDice == dice && dice.BeginHold())
                {
                    externalDiceHold = true;
                    holdingDevice = device;
                    device.SendHapticImpulse(0u, 0.28f, 0.08f);
                }
                else if (button != null)
                {
                    button.Trigger();
                    device.SendHapticImpulse(0u, 0.18f, 0.045f);
                }
            }

            if (externalDiceHold && pressed && device.name == holdingDevice.name && Time.frameCount % 7 == 0)
                device.SendHapticImpulse(0u, Mathf.Lerp(0.16f, 0.5f, Mathf.PingPong(Time.time * 0.8f, 1f)), 0.06f);

            if (!pressed && triggerPrevious && externalDiceHold && device.name == holdingDevice.name)
            {
                externalDiceHold = false;
                dice.EndHold();
                device.SendHapticImpulse(0u, 0.62f, 0.12f);
            }
            triggerPrevious = pressed;
        }

        void ConfigureDesktopCamera()
        {
            if (game == null)
                game = FindAnyObjectByType<TempleCircuitGame>();
            if (game == null || game.pathTiles == null || game.pathTiles.Length == 0)
                return;
            CaptureBoardGeometry();
            if (sourceBoardWidth < 0.01f)
                return;
            headCamera.transform.SetParent(null, true);
            headCamera.stereoTargetEye = StereoTargetEyeMask.None;
            desktopTarget = DesignPlayerLookAtWorldPosition;
            var designOffset = DesignPlayerEyeWorldPosition - desktopTarget;
            desktopBoardRadius = sourceBoardWidth * 0.5f;
            desktopDistance = Mathf.Max(0.01f, designOffset.magnitude);
            desktopTargetDistance = desktopDistance;
            desktopYaw = Mathf.Atan2(designOffset.x, -designOffset.z) * Mathf.Rad2Deg;
            desktopPitch = Mathf.Asin(Mathf.Clamp(designOffset.y / desktopDistance, -1f, 1f)) * Mathf.Rad2Deg;
            ApplyDesktopCamera();
            headCamera.nearClipPlane = 0.05f;
            headCamera.farClipPlane = 2000f;
            headCamera.fieldOfView = 52f;
            ConfigureCameraBackground();
            mixedRealityPreview?.SetDesktopActive(false);
            desktopConfigured = true;
            xrConfigured = false;
        }

        void UpdateDesktopCamera()
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                    TriggerDesktopWorldTarget(mouse.position.ReadValue());
                if (mouse.rightButton.wasPressedThisFrame)
                {
                    desktopOrbiting = true;
                    previousMouse = mouse.position.ReadValue();
                }
                if (mouse.rightButton.wasReleasedThisFrame)
                    desktopOrbiting = false;
                if (desktopOrbiting)
                {
                    var current = mouse.position.ReadValue();
                    var delta = current - previousMouse;
                    previousMouse = current;
                    desktopYaw += delta.x * 0.12f;
                    desktopPitch = Mathf.Clamp(desktopPitch - delta.y * 0.1f, 18f, 52f);
                }
                var scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    desktopTargetDistance *= Mathf.Exp(-scroll * 0.0012f);
                    desktopTargetDistance = Mathf.Clamp(desktopTargetDistance, desktopBoardRadius * 1.35f, desktopBoardRadius * 3.4f);
                }
            }

            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                ConfigureDesktopCamera();

            desktopDistance = Mathf.Lerp(desktopDistance, desktopTargetDistance, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
            ApplyDesktopCamera();
        }

        void TriggerDesktopWorldTarget(Vector2 screenPosition)
        {
            if (headCamera == null)
                return;
            var ray = headCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out var hit, 2000f))
                return;
            var startSurface = hit.transform.GetComponentInParent<TempleStartSurface>();
            if (startSurface != null && startScreen != null && !startScreen.GameStarted)
            {
                startSurface.Trigger(hit.point);
                return;
            }
            var button = hit.transform.GetComponentInParent<TempleWorldButton>();
            if (button != null)
            {
                button.Trigger();
                return;
            }
            var pointedDice = hit.transform.GetComponentInParent<TempleDiceInteractor>();
            if (pointedDice != null && pointedDice == dice)
            {
                dice.BeginHold();
                return;
            }

            if (startScreen != null && !startScreen.GameStarted)
                startScreen.TriggerAtWorldPoint(hit.point);
        }

        void ApplyDesktopCamera()
        {
            var yawRotation = Quaternion.Euler(0f, desktopYaw, 0f);
            var horizontal = Mathf.Cos(desktopPitch * Mathf.Deg2Rad) * desktopDistance;
            var offset = yawRotation * (Vector3.back * horizontal);
            offset.y = Mathf.Sin(desktopPitch * Mathf.Deg2Rad) * desktopDistance;
            headCamera.transform.position = desktopTarget + offset;
            headCamera.transform.rotation = Quaternion.LookRotation(desktopTarget - headCamera.transform.position, Vector3.up);
        }

        public void Recenter()
        {
            if (XRActive)
            {
                // Resetting the authored world must relocate only the XR rig.
                // Calling the platform recenter here changes the tracking
                // origin a second time, which was the source of the tabletop
                // appearing overhead while its independently anchored UI was
                // left below the view in the simulator.
                ApplyDesignStartWithAvailableHeadPose(hasTrackedHeadPose);
                ScheduleDesignStartSnap();
            }
            else
                ConfigureDesktopCamera();
        }

        void ScheduleDesignStartSnap()
        {
            // The emulator publishes the XR camera pose after scene startup.
            // Solving the rig against an untracked zero pose places the player
            // above or below the table as soon as the real pose arrives.
            designStartPending = true;
            designStartEarliestFrame = Time.frameCount + 2;
        }

        void ApplyDesignStartWithAvailableHeadPose(bool usingTrackedPose)
        {
            if (headCamera == null)
                return;

            // Preserve the local 6DoF headset pose whenever it is available.
            // If it is not available yet, use the authoritative design eye
            // height so both the first rendered frame and later tracked frame
            // resolve to the same world-space tabletop landmark.
            var desiredEye = DesignPlayerEyeWorldPosition;
            var nominalLocalEye = new Vector3(0f, Mathf.Max(0.01f, desiredEye.y), 0f);
            var localEye = usingTrackedPose ? headCamera.transform.localPosition : nominalLocalEye;
            var localRotation = usingTrackedPose ? headCamera.transform.localRotation : Quaternion.identity;
            if (!usingTrackedPose)
            {
                headCamera.transform.localPosition = localEye;
                headCamera.transform.localRotation = localRotation;
            }

            var localForward = localRotation * Vector3.forward;
            localForward.y = 0f;
            var localYaw = localForward.sqrMagnitude < 0.0001f
                ? 0f
                : Mathf.Atan2(localForward.x, localForward.z) * Mathf.Rad2Deg;
            var currentYaw = titleStage ? titleDesignFacingYawDegrees : designFacingYawDegrees;
            transform.rotation = Quaternion.Euler(0f, currentYaw - localYaw, 0f);
            transform.position = desiredEye - transform.rotation * localEye;
            designStartPending = false;
            Debug.Log($"Temple {(titleStage ? "title" : "gameplay")} design start applied " +
                      $"({(usingTrackedPose ? "tracked" : "nominal")} eye): local eye {localEye:F3}, " +
                      $"world eye {headCamera.transform.position:F3}, target eye {desiredEye:F3}, " +
                      $"yaw {currentYaw:F1}, board {GetWorldTablePoint(Vector3.zero):F3}.", this);
        }

        public void EnterGame()
        {
            if (XRActive)
            {
                SetPicoPassthrough(true);
                // START changes from title to gameplay landmark in the middle
                // of an XR frame. Apply immediately, then refine after PICO
                // publishes the next tracked head pose.
                ApplyDesignStartWithAvailableHeadPose(hasTrackedHeadPose);
                ScheduleDesignStartSnap();
            }
            else
            {
                mixedRealityPreview?.SetDesktopActive(false);
                ConfigureDesktopCamera();
            }
        }

        void ConfigureCameraBackground()
        {
            if (headCamera == null)
                return;

            // No virtual room and no camera-bound backdrop: the board, cards and
            // rays are the only game objects. The PICO compositor supplies the
            // user's physical surroundings behind them.
            headCamera.clearFlags = CameraClearFlags.SolidColor;
            headCamera.backgroundColor = Color.clear;
        }
    }
}

using System.Collections.Generic;
using ByteDance.PICO.XR;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
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
        public Vector3 designEyeOffsetFromTabletop = new Vector3(0f, 0.78f, -1.1f);
        [Tooltip("The gameplay landmark the player faces. Zero is the board centre.")]
        public Vector3 designLookAtOffsetFromTabletop = Vector3.zero;
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
        [Min(0.3f)] public float statusBoardWidthMeters = 0.92f;
        [Min(1f)] public float rayLength = 5f;

        Camera headCamera;
        Transform leftController;
        Transform rightController;
        LineRenderer leftRay;
        LineRenderer rightRay;
        XRInputDevice leftDevice;
        XRInputDevice rightDevice;
        bool leftTriggerPrevious;
        bool rightTriggerPrevious;
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
        Vector3 sourceBoardCenter;
        float sourceBoardWidth;
        bool boardGeometryCaptured;
        Coroutine designStartRoutine;
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
            var inputSubsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(inputSubsystems);
            foreach (var subsystem in inputSubsystems)
            {
                subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
                subsystem.TryRecenter();
            }

            headCamera.stereoTargetEye = StereoTargetEyeMask.Both;
            headCamera.transform.SetParent(transform, false);
            headCamera.clearFlags = CameraClearFlags.SolidColor;
            headCamera.backgroundColor = Color.clear;
            headCamera.allowHDR = false;
            headCamera.nearClipPlane = 0.03f;
            headCamera.farClipPlane = 100f;
            mixedRealityPreview?.SetDesktopActive(false);
            SetPicoPassthrough(startScreen == null || startScreen.GameStarted);
            if (leftController == null)
                leftController = CreateController("PICO Left Controller", new Color32(72, 162, 132, 255), out leftRay);
            if (rightController == null)
                rightController = CreateController("PICO Right Controller", new Color32(220, 181, 91, 255), out rightRay);
            xrConfigured = true;
            desktopConfigured = false;
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
                SetPicoPassthrough(startScreen == null || startScreen.GameStarted);
                UpdateXR();
            }
            else
            {
                SetPicoPassthrough(false);
                if (!desktopConfigured)
                    ConfigureDesktopCamera();
                mixedRealityPreview?.SetDesktopActive(startScreen == null || startScreen.GameStarted);
                UpdateDesktopCamera();
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused && XRActive)
                SetPicoPassthrough(startScreen == null || startScreen.GameStarted);
            else if (paused)
                SetPicoPassthrough(false);
        }

        void SetPicoPassthrough(bool enabledState)
        {
            if (passthroughEnabled == enabledState)
                return;
            passthroughEnabled = enabledState;
            if (!XRActive && enabledState)
                return;

            PXR_Manager.EnableVideoSeeThrough = enabledState;
        }

        void UpdateXR()
        {
            leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            var headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            ApplyPose(headDevice, headCamera.transform);
            ApplyPose(leftDevice, leftController);
            ApplyPose(rightDevice, rightController);
            UpdateController(leftDevice, leftController, leftRay, ref leftTriggerPrevious);
            UpdateController(rightDevice, rightController, rightRay, ref rightTriggerPrevious);

            var leftMenu = leftDevice.TryGetFeatureValue(XRCommonUsages.menuButton, out var leftMenuValue) && leftMenuValue;
            var rightMenu = rightDevice.TryGetFeatureValue(XRCommonUsages.menuButton, out var rightMenuValue) && rightMenuValue;
            if ((leftMenu && !leftMenuPrevious) || (rightMenu && !rightMenuPrevious))
                Recenter();
            leftMenuPrevious = leftMenu;
            rightMenuPrevious = rightMenu;
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

        void UpdateController(XRInputDevice device, Transform controller, LineRenderer ray, ref bool triggerPrevious)
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
            if (pressed && !triggerPrevious)
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
            mixedRealityPreview?.SetDesktopActive(startScreen == null || startScreen.GameStarted);
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
            var inputSubsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(inputSubsystems);
            foreach (var subsystem in inputSubsystems)
                subsystem.TryRecenter();
            if (XRActive)
                ScheduleDesignStartSnap();
            else
                ConfigureDesktopCamera();
        }

        void ScheduleDesignStartSnap()
        {
            if (designStartRoutine != null)
                StopCoroutine(designStartRoutine);
            designStartRoutine = StartCoroutine(SnapToDesignStartAfterTracking());
        }

        System.Collections.IEnumerator SnapToDesignStartAfterTracking()
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            ResetRigRootToDesignStart();
            designStartRoutine = null;
        }

        void ResetRigRootToDesignStart()
        {
            if (headCamera == null)
                return;

            // Preserve local 6DoF tracking offset; solve root so the eye lands on the authored start.
            var localForward = headCamera.transform.localRotation * Vector3.forward;
            localForward.y = 0f;
            var localYaw = localForward.sqrMagnitude < 0.0001f
                ? 0f
                : Mathf.Atan2(localForward.x, localForward.z) * Mathf.Rad2Deg;
            var currentYaw = titleStage ? titleDesignFacingYawDegrees : designFacingYawDegrees;
            transform.rotation = Quaternion.Euler(0f, currentYaw - localYaw, 0f);
            transform.position = DesignPlayerEyeWorldPosition - transform.rotation * headCamera.transform.localPosition;
            Debug.Log($"Temple {(titleStage ? "title" : "gameplay")} design start applied: eye {DesignPlayerEyeWorldPosition:F3}, yaw {currentYaw:F1}, board {GetWorldTablePoint(Vector3.zero):F3}.", this);
        }

        public void EnterGame()
        {
            if (XRActive)
                SetPicoPassthrough(true);
            else
                mixedRealityPreview?.SetDesktopActive(true);
        }
    }
}

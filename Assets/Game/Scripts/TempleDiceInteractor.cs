using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TempleCircuit
{
    public sealed class TempleDiceInteractor : MonoBehaviour
    {
        public TempleCircuitGame game;
        public Transform centerAnchor;
        [Min(0.05f)] public float minimumHoldSeconds = 0.45f;
        [Min(0.1f)] public float rollSeconds = 0.85f;
        [Min(0f)] public float surfaceClearance = 0.002f;

        Vector3 restWorldPosition;
        Quaternion restWorldRotation;
        Vector3 fixedLocalScale;
        float lockedSupportTop;
        Vector2 lockedSupportCenter;
        bool supportLocked;
        float holdStartedAt;
        bool holding;
        bool rolling;
        Camera inputCamera;
        float localDiceHeight = 1f;
        Renderer[] visualRenderers = Array.Empty<Renderer>();
        bool visualRenderersCached;

        public bool IsHolding => holding;
        public bool IsRolling => rolling;

        void Awake()
        {
            SnapToSupportSurface();
            CaptureRestPose();
            inputCamera = Camera.main;
#if UNITY_EDITOR
            ValidateSettledCenters();
#endif
        }

        public void CaptureRestPose()
        {
            restWorldRotation = transform.rotation;
            fixedLocalScale = transform.localScale;
            AlignVisualCenter();
            GroundToLockedSupport();
            restWorldPosition = transform.position;
            if (TryGetRendererBounds(out var bounds))
            {
                var parentScale = transform.parent == null ? 1f : Mathf.Max(0.0001f, transform.parent.lossyScale.y);
                localDiceHeight = Mathf.Max(0.1f, bounds.size.y / parentScale);
            }
        }

        public bool SnapToSupportSurface()
        {
            if (!TryGetRendererBounds(out var diceBounds))
                return false;

            // The central temple anchor is authored once in the saved scene.
            // Do not infer its support from the whole board when it is
            // available: decorative floor renderers can win that search and
            // leave the die below the board after XR runtime scaling.
            if (centerAnchor != null && TryGetCentralSupportBounds(out var centralSupport))
            {
                lockedSupportTop = centralSupport.max.y;
                lockedSupportCenter = new Vector2(centerAnchor.position.x, centerAnchor.position.z);
                supportLocked = true;
                AlignVisualCenter();
                GroundToLockedSupport();
                restWorldPosition = transform.position;
                Debug.Log($"Temple dice anchored on the central shrine at {transform.position:F3}; shrine top {lockedSupportTop:F3}.", this);
                return true;
            }

            var supportTop = float.NegativeInfinity;
            Renderer supportRenderer = null;
            var center = diceBounds.center;
            var padding = Mathf.Min(diceBounds.extents.x, diceBounds.extents.z) * 0.18f;
            foreach (var renderer in FindObjectsByType<Renderer>())
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.transform.IsChildOf(transform))
                    continue;
                var bounds = renderer.bounds;
                if (bounds.size.sqrMagnitude < 0.0001f)
                    continue;
                if (center.x < bounds.min.x - padding || center.x > bounds.max.x + padding || center.z < bounds.min.z - padding || center.z > bounds.max.z + padding)
                    continue;
                // Only the surface already below the dice is a valid support.
                // This avoids snapping through the central shrine to a roof,
                // fence or decorative renderer which happens to overlap it.
                if (bounds.max.y > center.y + diceBounds.extents.y * 0.12f)
                    continue;
                if (bounds.max.y <= supportTop)
                    continue;
                supportTop = bounds.max.y;
                supportRenderer = renderer;
            }

            if (supportRenderer == null || float.IsNegativeInfinity(supportTop))
                return false;
            lockedSupportTop = supportTop;
            var supportBounds = supportRenderer.bounds;
            lockedSupportCenter = new Vector2(supportBounds.center.x, supportBounds.center.z);
            supportLocked = true;
            AlignVisualCenter();
            GroundToLockedSupport();
            restWorldPosition = transform.position;
            return true;
        }

        bool TryGetCentralSupportBounds(out Bounds bounds)
        {
            // The anchor is a child of the central-shrine prefab root. Use its
            // visible geometry so the dice consistently grounds on the shrine.
            var supportRoot = centerAnchor == null ? null : centerAnchor.parent;
            if (supportRoot == null)
            {
                bounds = default;
                return false;
            }

            var renderers = supportRoot.GetComponentsInChildren<Renderer>(true);
            var found = false;
            bounds = default;
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.transform.IsChildOf(transform))
                    continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                    bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }

        void GroundToLockedSupport(float lift = 0f)
        {
            if (!supportLocked || !TryGetRendererBounds(out var bounds))
                return;
            transform.position += Vector3.up * (lockedSupportTop + surfaceClearance + lift - bounds.min.y);
        }

        void RestoreRestPose(Quaternion rotation)
        {
            transform.position = restWorldPosition;
            transform.rotation = rotation;
            transform.localScale = fixedLocalScale;
            AlignVisualCenter();
            GroundToLockedSupport();
            restWorldPosition = transform.position;
        }

        void AlignVisualCenter()
        {
            if (!TryGetRendererBounds(out var bounds))
                return;
            var target = GetCenterTarget(bounds.center);
            transform.position += new Vector3(target.x - bounds.center.x, 0f, target.y - bounds.center.z);
        }

        Vector2 GetCenterTarget(Vector3 fallback)
        {
            if (centerAnchor != null)
                return new Vector2(centerAnchor.position.x, centerAnchor.position.z);
            if (supportLocked)
                return lockedSupportCenter;
            if (game != null)
                return new Vector2(game.transform.position.x, game.transform.position.z);
            return new Vector2(fallback.x, fallback.z);
        }

        void ApplyLockedPose(Quaternion rotation, float lift)
        {
            transform.position = restWorldPosition;
            transform.rotation = rotation;
            transform.localScale = fixedLocalScale;
            AlignVisualCenter();
            GroundToLockedSupport(lift);
        }

        bool TryGetRendererBounds(out Bounds bounds)
        {
            if (!visualRenderersCached)
            {
                visualRenderers = GetComponentsInChildren<Renderer>();
                visualRenderersCached = true;
            }

            var found = false;
            bounds = default;
            foreach (var renderer in visualRenderers)
            {
                if (renderer == null)
                    continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                    bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }

        void OnTransformChildrenChanged()
        {
            visualRenderersCached = false;
        }

        void Update()
        {
            // Input System-only desktop fallback. PICO controllers call BeginHold/EndHold through the XR rig.
            var mouse = Mouse.current;
            if (mouse != null && !holding && !rolling && mouse.leftButton.wasPressedThisFrame && game != null && game.CanHoldDice)
            {
                inputCamera = Camera.main;
                if (inputCamera != null)
                {
                    var ray = inputCamera.ScreenPointToRay(mouse.position.ReadValue());
                    if (Physics.Raycast(ray, out var hit, 1000f) && hit.transform.GetComponentInParent<TempleDiceInteractor>() == this)
                        BeginHold();
                }
            }

            if (holding && mouse != null && mouse.leftButton.wasReleasedThisFrame)
                EndHold();

            if (!holding)
                return;

            var elapsed = Time.unscaledTime - holdStartedAt;
            var strength = Mathf.Clamp01(elapsed / 1.8f);
            var jitter = Mathf.Lerp(0.025f, 0.11f, strength);
            var t = Time.unscaledTime * Mathf.Lerp(9f, 19f, strength);
            var holdRotation = restWorldRotation * Quaternion.Euler(
                Mathf.Sin(t) * Mathf.Lerp(5f, 18f, strength),
                Mathf.Cos(t * 0.81f) * Mathf.Lerp(8f, 28f, strength),
                Mathf.Sin(t * 1.21f) * Mathf.Lerp(4f, 16f, strength));
            var verticalJitter = Mathf.Abs(Mathf.Sin(t * 1.33f)) * jitter * 0.6f;
            ApplyLockedPose(holdRotation, verticalJitter);
            game?.ReportDiceCharge(strength);
        }

        public bool BeginHold()
        {
            if (holding || rolling || game == null || !game.CanHoldDice)
                return false;
            holding = true;
            holdStartedAt = Time.unscaledTime;
            game.NotifyDiceHoldStarted();
            return true;
        }

        public void EndHold()
        {
            if (!holding)
                return;
            holding = false;
            RestoreRestPose(restWorldRotation);
            var duration = Mathf.Max(minimumHoldSeconds, Time.unscaledTime - holdStartedAt);
            game.OnDiceHoldReleased(duration);
        }

        public void CancelHold()
        {
            holding = false;
            StopAllCoroutines();
            rolling = false;
            RestoreRestPose(restWorldRotation);
        }

        public void Roll(int value, float power, Action<int> completed)
        {
            if (rolling)
                return;
            game?.audioManager?.PlayDiceRoll();
            StartCoroutine(RollRoutine(Mathf.Clamp(value, 1, 6), Mathf.Clamp01(power), completed));
        }

        IEnumerator RollRoutine(int value, float power, Action<int> completed)
        {
            rolling = true;
            holding = false;
            var startRotation = transform.rotation;
            var faceRotation = restWorldRotation * FaceRotation(value);
            var targetRotation = Quaternion.AngleAxis(90f * UnityEngine.Random.Range(0, 4), Vector3.up) * faceRotation;
            var duration = Mathf.Lerp(rollSeconds * 0.8f, rollSeconds * 1.25f, power);
            var spin = new Vector3(
                UnityEngine.Random.Range(520f, 820f),
                UnityEngine.Random.Range(620f, 980f),
                UnityEngine.Random.Range(460f, 760f));

            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var p = Mathf.Clamp01(elapsed / duration);
                var settle = 1f - Mathf.SmoothStep(0f, 1f, p);
                // The die remains seated on the central shrine: it should have
                // a clear roll arc, not spring above the temple or its nearby
                // action panel.  This is intentionally much lower than the
                // original desktop-sized throw.
                var arc = Mathf.Sin(p * Mathf.PI) * localDiceHeight * Mathf.Lerp(0.10f, 0.18f, power);
                var animatedRotation = Quaternion.Slerp(startRotation, targetRotation, Mathf.SmoothStep(0.58f, 1f, p)) *
                    Quaternion.Euler(spin * (p * settle * 0.35f));
                ApplyLockedPose(animatedRotation, arc);
                yield return null;
            }

            RestoreRestPose(targetRotation);
            rolling = false;
            game?.audioManager?.PlayDiceLand();
            completed?.Invoke(value);
        }

        static Quaternion FaceRotation(int value)
        {
            return Quaternion.FromToRotation(FaceLocalNormal(value), Vector3.up);
        }

        static Vector3 FaceLocalNormal(int value)
        {
            // Read from the imported mesh's pip geometry, not from a standard-die assumption.
            switch (value)
            {
                case 1: return Vector3.forward;
                case 2: return Vector3.down;
                case 3: return Vector3.back;
                case 4: return Vector3.up;
                case 5: return Vector3.right;
                default: return Vector3.left;
            }
        }

#if UNITY_EDITOR
        void ValidateSettledCenters()
        {
            if (!supportLocked || game == null || !TryGetRendererBounds(out _))
                return;

            var savedPosition = transform.position;
            var savedRotation = transform.rotation;
            var savedScale = transform.localScale;
            var savedRestPosition = restWorldPosition;
            var savedRestRotation = restWorldRotation;
            var target = GetCenterTarget(savedPosition);
            var maximumCenterError = 0f;
            var maximumGroundError = 0f;
            var maximumFaceAngle = 0f;

            for (var value = 1; value <= 6; value++)
            {
                for (var yawStep = 0; yawStep < 4; yawStep++)
                {
                    var faceRotation = savedRestRotation * FaceRotation(value);
                    var rotation = Quaternion.AngleAxis(yawStep * 90f, Vector3.up) * faceRotation;
                    RestoreRestPose(rotation);
                    if (!TryGetRendererBounds(out var bounds))
                        continue;
                    maximumCenterError = Mathf.Max(maximumCenterError, Vector2.Distance(
                        new Vector2(bounds.center.x, bounds.center.z),
                        target));
                    maximumGroundError = Mathf.Max(maximumGroundError, Mathf.Abs(bounds.min.y - lockedSupportTop - surfaceClearance));
                    maximumFaceAngle = Mathf.Max(maximumFaceAngle, Vector3.Angle(rotation * FaceLocalNormal(value), Vector3.up));
                }
            }

            transform.position = savedPosition;
            transform.rotation = savedRotation;
            transform.localScale = savedScale;
            restWorldPosition = savedRestPosition;
            restWorldRotation = savedRestRotation;

            const float tolerance = 0.001f;
            if (maximumCenterError > tolerance || maximumGroundError > tolerance || maximumFaceAngle > 0.01f)
            {
                Debug.LogError(
                    $"Dice validation failed: center error {maximumCenterError:F6}, ground error {maximumGroundError:F6}, face angle {maximumFaceAngle:F4}.", this);
                return;
            }
            Debug.Log($"Dice center and face validation passed for all 24 settled orientations (max center error {maximumCenterError:F6}, face angle {maximumFaceAngle:F4}).", this);
        }
#endif
    }
}

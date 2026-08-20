using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;

namespace TempleCircuit
{
    [DefaultExecutionOrder(-2000)]
    public sealed class TempleStartScreen : MonoBehaviour
    {
        const float ReferenceWidth = 1920f;
        const float ReferenceHeight = 1080f;
        const float FadeSeconds = 0.28f;

        static readonly Vector2 StartMin = new Vector2(0.13f, 0.345f);
        static readonly Vector2 StartMax = new Vector2(0.397f, 0.466f);
        static readonly Vector2 ExitMin = new Vector2(0.168f, 0.21f);
        static readonly Vector2 ExitMax = new Vector2(0.358f, 0.312f);

        public Texture2D coverTexture;
        public TempleCircuitGame game;
        public TempleXRTabletopRig rig;

        GameObject presentationRoot;
        RectTransform coverRect;
        CanvasGroup canvasGroup;
        TempleStartButtonFeedback startButtonFeedback;
        TempleStartButtonFeedback exitButtonFeedback;
        bool xrPresentation;
        readonly List<RendererState> gameplayRenderers = new List<RendererState>();
        readonly List<ColliderState> gameplayColliders = new List<ColliderState>();
        readonly List<BehaviourState> gameplayBehaviours = new List<BehaviourState>();
        readonly List<AudioState> gameplayAudio = new List<AudioState>();
        readonly List<LightState> gameplayLights = new List<LightState>();
        bool stageStateCaptured;

        public bool GameStarted { get; private set; }

        void Awake()
        {
            GameStarted = false;
            if (game == null)
                game = GetComponent<TempleCircuitGame>() ?? FindAnyObjectByType<TempleCircuitGame>();
            if (rig == null)
                rig = FindAnyObjectByType<TempleXRTabletopRig>();
            if (rig != null)
            {
                rig.startScreen = this;
                rig.SetTitleStage(true);
            }
            SetGameplayStageActive(false);
        }

        void Start()
        {
            if (coverTexture == null)
                coverTexture = Resources.Load<Texture2D>("TempleStartCover");
            if (coverTexture == null)
            {
                Debug.LogError("The 16:9 Temple start cover is missing. Starting the game without the cover.", this);
                BeginGame(false);
                return;
            }

            xrPresentation = XRSettings.enabled && XRSettings.isDeviceActive;
            BuildPresentation();
        }

        void OnDestroy()
        {
            if (presentationRoot != null)
                Destroy(presentationRoot);
        }

        void BuildPresentation()
        {
            rig?.PrepareWorldSpaceLayout();
            var camera = Camera.main;
            presentationRoot = new GameObject(
                "TempleTitleRoot_WorldSpace",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));

            var canvas = presentationRoot.GetComponent<Canvas>();
            var scaler = presentationRoot.GetComponent<CanvasScaler>();
            canvasGroup = presentationRoot.GetComponent<CanvasGroup>();
            canvas.sortingOrder = 1000;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var canvasRect = presentationRoot.GetComponent<RectTransform>();
            if (rig != null)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                canvasRect.sizeDelta = new Vector2(ReferenceWidth, ReferenceHeight);
                canvasRect.position = rig.GetWorldTablePoint(rig.startPortalOffsetFromTabletop);
                var facePlayer = rig.DesignPlayerEyeWorldPosition - canvasRect.position;
                facePlayer.y = 0f;
                canvasRect.rotation = facePlayer.sqrMagnitude < 0.001f
                    ? rig.DesignPlayerFacingRotation
                    // World-space Canvas is viewed from its back in this authored layout.
                    // Reverse the surface normal so the title art, Chinese text and START/EXIT
                    // buttons are readable instead of horizontally mirrored.
                    : Quaternion.LookRotation(-facePlayer.normalized, Vector3.up);
                canvasRect.localScale = Vector3.one *
                    (rig.startPortalWidthMeters * rig.WorldUnitsPerTabletopMeter / ReferenceWidth);
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasRect.anchorMin = Vector2.zero;
                canvasRect.anchorMax = Vector2.one;
                canvasRect.offsetMin = Vector2.zero;
                canvasRect.offsetMax = Vector2.zero;
            }

            var backdrop = CreateImage("Backdrop", canvasRect, Color.black);
            Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = false;

            var coverFrame = new GameObject("CoverFrame_16x9", typeof(RectTransform), typeof(AspectRatioFitter));
            coverRect = coverFrame.GetComponent<RectTransform>();
            coverRect.SetParent(canvasRect, false);
            coverRect.anchorMin = Vector2.zero;
            coverRect.anchorMax = Vector2.one;
            coverRect.offsetMin = Vector2.zero;
            coverRect.offsetMax = Vector2.zero;
            var fitter = coverFrame.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 16f / 9f;

            var cover = new GameObject("CoverArtwork", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            var coverTransform = cover.GetComponent<RectTransform>();
            coverTransform.SetParent(coverRect, false);
            Stretch(coverTransform);
            var artwork = cover.GetComponent<RawImage>();
            artwork.texture = coverTexture;
            artwork.color = Color.white;

            startButtonFeedback = CreateButtonArea("StartButton", coverRect, StartMin, StartMax, BeginGameFromButton, new Color32(226, 183, 69, 255));
            exitButtonFeedback = CreateButtonArea("ExitButton", coverRect, ExitMin, ExitMax, QuitGame, new Color32(72, 162, 132, 255));

            if (rig != null)
            {
                var collider = cover.AddComponent<BoxCollider>();
                collider.size = new Vector3(ReferenceWidth, ReferenceHeight, 12f);
                var surface = cover.AddComponent<TempleStartSurface>();
                surface.owner = this;
            }
        }

        void SetGameplayStageActive(bool active)
        {
            if (!stageStateCaptured)
                CaptureGameplayStageState();

            foreach (var state in gameplayRenderers)
                if (state.component != null) state.component.enabled = active && state.enabled;
            foreach (var state in gameplayColliders)
                if (state.component != null) state.component.enabled = active && state.enabled;
            foreach (var state in gameplayBehaviours)
                if (state.component != null) state.component.enabled = active && state.enabled;
            foreach (var state in gameplayAudio)
                if (state.component != null) state.component.enabled = active && state.enabled;
            foreach (var state in gameplayLights)
                if (state.component != null) state.component.enabled = active && state.enabled;

            Debug.Log($"Temple {(active ? "gameplay" : "title")} stage: " +
                      $"{gameplayRenderers.Count} renderers, {gameplayColliders.Count} colliders, " +
                      $"{gameplayBehaviours.Count} gameplay behaviours and {gameplayAudio.Count} audio sources " +
                      (active ? "restored after START." : "disabled before the opaque title surface is shown."), this);
        }

        void CaptureGameplayStageState()
        {
            stageStateCaptured = true;
            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
                if (!IsTitleSafe(renderer.transform)) gameplayRenderers.Add(new RendererState(renderer));
            foreach (var collider in FindObjectsByType<Collider>(FindObjectsInactive.Exclude))
                if (!IsTitleSafe(collider.transform)) gameplayColliders.Add(new ColliderState(collider));
            foreach (var behaviour in FindObjectsByType<Behaviour>(FindObjectsInactive.Exclude))
                if (!IsTitleSafe(behaviour.transform) && behaviour != this) gameplayBehaviours.Add(new BehaviourState(behaviour));
            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude))
                if (!IsTitleSafe(source.transform)) gameplayAudio.Add(new AudioState(source));
            foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                if (!IsTitleSafe(light.transform)) gameplayLights.Add(new LightState(light));
        }

        bool IsTitleSafe(Transform candidate)
        {
            if (candidate == null)
                return true;
            if (rig != null && candidate.IsChildOf(rig.transform))
                return true;
            if (candidate.GetComponent<Camera>() != null || candidate.GetComponentInParent<Camera>() != null)
                return true;
            return candidate.GetComponent<UnityEngine.EventSystems.EventSystem>() != null ||
                   candidate.GetComponentInParent<UnityEngine.EventSystems.EventSystem>() != null;
        }

        readonly struct RendererState
        {
            public readonly Renderer component;
            public readonly bool enabled;
            public RendererState(Renderer value) { component = value; enabled = value.enabled; }
        }

        readonly struct ColliderState
        {
            public readonly Collider component;
            public readonly bool enabled;
            public ColliderState(Collider value) { component = value; enabled = value.enabled; }
        }

        readonly struct BehaviourState
        {
            public readonly Behaviour component;
            public readonly bool enabled;
            public BehaviourState(Behaviour value) { component = value; enabled = value.enabled; }
        }

        readonly struct AudioState
        {
            public readonly AudioSource component;
            public readonly bool enabled;
            public AudioState(AudioSource value) { component = value; enabled = value.enabled; }
        }

        readonly struct LightState
        {
            public readonly Light component;
            public readonly bool enabled;
            public LightState(Light value) { component = value; enabled = value.enabled; }
        }

        void BeginGameFromButton()
        {
            BeginGame(true);
        }

        public void TriggerAtWorldPoint(Vector3 worldPoint)
        {
            if (GameStarted || coverRect == null)
                return;

            var local = coverRect.InverseTransformPoint(worldPoint);
            var rect = coverRect.rect;
            var normalized = new Vector2(
                Mathf.InverseLerp(rect.xMin, rect.xMax, local.x),
                Mathf.InverseLerp(rect.yMin, rect.yMax, local.y));
            if (Inside(normalized, ExitMin, ExitMax))
                QuitGame();
            else if (Inside(normalized, StartMin, StartMax))
                BeginGame(true);
        }

        public void SetWorldHover(Vector3 worldPoint, bool isHovering)
        {
            if (GameStarted || coverRect == null)
                return;

            var local = coverRect.InverseTransformPoint(worldPoint);
            var rect = coverRect.rect;
            var normalized = new Vector2(
                Mathf.InverseLerp(rect.xMin, rect.xMax, local.x),
                Mathf.InverseLerp(rect.yMin, rect.yMax, local.y));
            startButtonFeedback?.SetWorldHover(isHovering && Inside(normalized, StartMin, StartMax));
            exitButtonFeedback?.SetWorldHover(isHovering && Inside(normalized, ExitMin, ExitMax));
        }

        void BeginGame(bool playClick)
        {
            if (GameStarted)
                return;

            GameStarted = true;
            if (playClick)
                game?.audioManager?.PlayUiClick();
            SetGameplayStageActive(true);
            rig?.SetTitleStage(false);
            rig?.EnterGame();

            if (canvasGroup == null)
            {
                if (presentationRoot != null)
                    Destroy(presentationRoot);
                return;
            }
            StartCoroutine(FadeOut());
        }

        IEnumerator FadeOut()
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            var elapsed = 0f;
            while (elapsed < FadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / FadeSeconds);
                canvasGroup.alpha = 1f - t * t * (3f - 2f * t);
                yield return null;
            }
            Destroy(presentationRoot);
            presentationRoot = null;
        }

        void QuitGame()
        {
            game?.audioManager?.PlayUiClick();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        static TempleStartButtonFeedback CreateButtonArea(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction action, Color accent)
        {
            var image = CreateImage(name, parent, Color.clear);
            var rect = image.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(action);
            var feedback = image.gameObject.AddComponent<TempleStartButtonFeedback>();
            feedback.Configure(image, accent);
            return feedback;
        }

        static Image CreateImage(string name, RectTransform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static bool Inside(Vector2 point, Vector2 min, Vector2 max)
        {
            return point.x >= min.x && point.x <= max.x && point.y >= min.y && point.y <= max.y;
        }
    }

    public sealed class TempleStartSurface : MonoBehaviour
    {
        public TempleStartScreen owner;

        public void Trigger(Vector3 worldPoint)
        {
            owner?.TriggerAtWorldPoint(worldPoint);
        }

        public void SetHover(Vector3 worldPoint, bool isHovering)
        {
            owner?.SetWorldHover(worldPoint, isHovering);
        }
    }

    [RequireComponent(typeof(RectTransform))]
    public sealed class TempleStartButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        Image overlay;
        RectTransform rect;
        Color accent;
        bool pointerHover;
        bool worldHover;
        bool pressed;
        Vector3 initialScale;

        public void Configure(Image target, Color color)
        {
            overlay = target;
            rect = target.rectTransform;
            accent = color;
            initialScale = rect.localScale;
            ApplyVisual(true);
        }

        public void SetWorldHover(bool value)
        {
            worldHover = value;
            ApplyVisual(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerHover = true;
            ApplyVisual(false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerHover = false;
            pressed = false;
            ApplyVisual(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pressed = true;
            ApplyVisual(false);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressed = false;
            ApplyVisual(false);
        }

        void ApplyVisual(bool immediate)
        {
            if (overlay == null || rect == null)
                return;

            var hovering = pointerHover || worldHover;
            var targetAlpha = pressed ? 0.31f : hovering ? 0.16f : 0f;
            overlay.color = new Color(accent.r, accent.g, accent.b, targetAlpha);
            var targetScale = initialScale * (pressed ? 0.97f : hovering ? 1.035f : 1f);
            if (immediate)
                rect.localScale = targetScale;
            else
                rect.localScale = Vector3.Lerp(rect.localScale, targetScale, 1f - Mathf.Exp(-22f * Time.unscaledDeltaTime));
        }

        void LateUpdate()
        {
            ApplyVisual(false);
        }
    }
}

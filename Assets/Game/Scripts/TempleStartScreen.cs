using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;

namespace TempleCircuit
{
    public enum TempleStartButtonShape
    {
        Start,
        Exit
    }

    [DefaultExecutionOrder(-2000)]
    public sealed class TempleStartScreen : MonoBehaviour
    {
        const float ReferenceWidth = 1920f;
        const float ReferenceHeight = 1080f;
        const float CoverEdgeFeather = 0.105f;
        const float CoverDepartureSeconds = 0.16f;
        const float GameplayRevealSeconds = 0.62f;
        const float SceneSwapCoverAlpha = 0.92f;

        // Pixel-measured from the authored 1920 x 1080 cover. These bounds
        // contain the visible gold outlines without adding outside padding.
        static readonly Vector2 StartMin = new Vector2(249f / ReferenceWidth, 374f / ReferenceHeight);
        static readonly Vector2 StartMax = new Vector2(760f / ReferenceWidth, 504f / ReferenceHeight);
        static readonly Vector2 ExitMin = new Vector2(313f / ReferenceWidth, 227f / ReferenceHeight);
        static readonly Vector2 ExitMax = new Vector2(696f / ReferenceWidth, 338f / ReferenceHeight);

        public Texture2D coverTexture;
        public TempleCircuitGame game;
        public TempleXRTabletopRig rig;

        GameObject presentationRoot;
        RectTransform presentationRect;
        RectTransform coverRect;
        BoxCollider coverCollider;
        CanvasGroup canvasGroup;
        Material coverFeatherMaterial;
        TempleStartButtonFeedback startButtonFeedback;
        TempleStartButtonFeedback exitButtonFeedback;
        bool titleActionPending;
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
            if (coverFeatherMaterial != null)
                Destroy(coverFeatherMaterial);
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
            presentationRect = canvasRect;
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

            // The cover artwork itself is the complete, opaque title stage.
            // Do not add a second black world-space rectangle behind it: in MR
            // that would mask the real room outside the 16:9 cover and make the
            // simulator look like a conventional black-background VR game.

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
            var featherShader = Resources.Load<Shader>("TempleStartCoverFeather");
            if (featherShader != null)
            {
                coverFeatherMaterial = new Material(featherShader)
                {
                    name = "Temple Start Cover Edge Feather"
                };
                coverFeatherMaterial.SetFloat("_Feather", CoverEdgeFeather);
                coverFeatherMaterial.SetFloat("_Aspect", ReferenceWidth / ReferenceHeight);
                artwork.material = coverFeatherMaterial;
            }
            else
            {
                Debug.LogWarning("Temple start cover feather shader is missing; using the unfeathered cover.", this);
            }

            startButtonFeedback = CreateButtonArea("StartButton", coverRect, StartMin, StartMax, TempleStartButtonShape.Start, BeginGameFromButton, new Color32(226, 183, 69, 255));
            exitButtonFeedback = CreateButtonArea("ExitButton", coverRect, ExitMin, ExitMax, TempleStartButtonShape.Exit, QuitGameFromButton, new Color32(72, 162, 132, 255));

            if (rig != null)
            {
                coverCollider = cover.AddComponent<BoxCollider>();
                coverCollider.size = new Vector3(ReferenceWidth, ReferenceHeight, 12f);
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
            QueueTitleAction(true, startButtonFeedback);
        }

        void QuitGameFromButton()
        {
            QueueTitleAction(false, exitButtonFeedback);
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
            if (InsideButtonShape(normalized, ExitMin, ExitMax, TempleStartButtonShape.Exit))
                QuitGameFromButton();
            else if (InsideButtonShape(normalized, StartMin, StartMax, TempleStartButtonShape.Start))
                BeginGameFromButton();
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
            startButtonFeedback?.SetWorldHover(isHovering && InsideButtonShape(normalized, StartMin, StartMax, TempleStartButtonShape.Start));
            exitButtonFeedback?.SetWorldHover(isHovering && InsideButtonShape(normalized, ExitMin, ExitMax, TempleStartButtonShape.Exit));
        }

        void BeginGame(bool playClick)
        {
            if (GameStarted)
                return;

            GameStarted = true;
            titleActionPending = true;
            if (playClick)
                game?.audioManager?.PlayUiClick();

            if (canvasGroup == null || presentationRoot == null)
            {
                if (presentationRoot != null)
                    Destroy(presentationRoot);
                presentationRoot = null;
                ActivateGameplayStage();
                return;
            }
            StartCoroutine(TransitionToGameplay());
        }

        void QueueTitleAction(bool beginGame, TempleStartButtonFeedback feedback)
        {
            if (GameStarted || titleActionPending)
                return;
            titleActionPending = true;
            feedback?.PulsePressed(0.16f);
            game?.audioManager?.PlayUiClick();
            StartCoroutine(CompleteTitleAction(beginGame));
        }

        IEnumerator CompleteTitleAction(bool beginGame)
        {
            // Keep the pressed colour visible for a handful of XR frames before
            // the title is destroyed or the application exits.
            yield return new WaitForSecondsRealtime(0.13f);
            if (beginGame)
            {
                titleActionPending = false;
                BeginGame(false);
            }
            else
                QuitGame(false);
        }

        IEnumerator TransitionToGameplay()
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            // Let the title gently release before the viewpoint changes. The
            // cover is still nearly opaque at the swap, so the authored title
            // and gameplay starts never present as a hard camera cut.
            yield return FadeCover(1f, SceneSwapCoverAlpha, CoverDepartureSeconds);

            AttachCoverToViewForTransition();
            if (coverCollider != null)
                coverCollider.enabled = false;
            ActivateGameplayStage();

            // The title now remains visually registered to the view for this
            // short exit only. As it dissolves, the world-space board becomes
            // progressively visible beneath it while passthrough stays stable.
            yield return FadeCover(SceneSwapCoverAlpha, 0f, GameplayRevealSeconds);

            canvasGroup.alpha = 0f;
            Destroy(presentationRoot);
            presentationRoot = null;
            presentationRect = null;
            coverRect = null;
            coverCollider = null;
            Debug.Log("Temple START transition complete: cover faded out and gameplay revealed.", this);
        }

        IEnumerator FadeCover(float from, float to, float seconds)
        {
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, seconds));
                var eased = t * t * (3f - 2f * t);
                canvasGroup.alpha = Mathf.LerpUnclamped(from, to, eased);
                yield return null;
            }
            canvasGroup.alpha = to;
        }

        void AttachCoverToViewForTransition()
        {
            var camera = Camera.main;
            if (presentationRect == null || camera == null)
                return;

            // This is intentionally limited to the sub-second exit. Before
            // START the cover remains a fixed world-space surface; attaching
            // it here preserves its apparent pose while the rig moves to the
            // gameplay-side design start.
            presentationRect.SetParent(camera.transform, true);
        }

        void ActivateGameplayStage()
        {
            SetGameplayStageActive(true);
            rig?.SetTitleStage(false);
            rig?.EnterGame();
        }

        void QuitGame(bool playClick = true)
        {
            if (playClick)
                game?.audioManager?.PlayUiClick();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        static TempleStartButtonFeedback CreateButtonArea(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, TempleStartButtonShape shape, UnityEngine.Events.UnityAction action, Color accent)
        {
            var image = CreateImage(name, parent, Color.clear);
            var rect = image.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var feedback = image.gameObject.AddComponent<TempleStartButtonFeedback>();
            feedback.Configure(image, accent, shape, action);
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

        static bool InsideButtonShape(Vector2 point, Vector2 min, Vector2 max, TempleStartButtonShape shape)
        {
            if (!Inside(point, min, max))
                return false;
            var normalized = new Vector2(
                Mathf.InverseLerp(min.x, max.x, point.x),
                Mathf.InverseLerp(min.y, max.y, point.y));
            return TempleStartButtonFeedback.ContainsShape(normalized, shape);
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
    public sealed class TempleStartButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        const int ShapeTextureWidth = 256;
        const int ShapeTextureHeight = 96;
        const int ShapeSupersampling = 4;

        // Authored cover-outline samples, normalized to each button's measured
        // bounds and expressed bottom-up to match RectTransform coordinates.
        // Separate outlines are required because START and EXIT have visibly
        // different hand-painted silhouettes.
        static readonly Vector2[] StartOutline =
        {
            StartPoint(284f, 578f), StartPoint(725f, 578f),
            StartPoint(731f, 580f), StartPoint(737f, 584f),
            StartPoint(741f, 590f), StartPoint(744f, 600f),
            StartPoint(747f, 610f), StartPoint(753f, 620f),
            StartPoint(756f, 630f), StartPoint(759f, 640f),
            StartPoint(757f, 650f), StartPoint(754f, 660f),
            StartPoint(749f, 670f), StartPoint(744f, 680f),
            StartPoint(742f, 690f), StartPoint(735f, 700f),
            StartPoint(731f, 702f), StartPoint(725f, 704f),
            StartPoint(284f, 704f), StartPoint(278f, 702f),
            StartPoint(274f, 700f), StartPoint(267f, 690f),
            StartPoint(265f, 680f), StartPoint(262f, 670f),
            StartPoint(255f, 660f), StartPoint(252f, 650f),
            StartPoint(250f, 640f), StartPoint(253f, 630f),
            StartPoint(256f, 620f), StartPoint(262f, 610f),
            StartPoint(265f, 600f), StartPoint(267f, 590f),
            StartPoint(271f, 584f), StartPoint(277f, 580f)
        };

        static readonly Vector2[] ExitOutline =
        {
            ExitPoint(341f, 744f), ExitPoint(666f, 744f),
            ExitPoint(673f, 746f), ExitPoint(677f, 750f),
            ExitPoint(681f, 758f), ExitPoint(684f, 770f),
            ExitPoint(689f, 782f), ExitPoint(693f, 794f),
            ExitPoint(693f, 802f), ExitPoint(691f, 810f),
            ExitPoint(687f, 818f), ExitPoint(683f, 828f),
            ExitPoint(681f, 840f), ExitPoint(677f, 846f),
            ExitPoint(674f, 848f), ExitPoint(670f, 850f),
            ExitPoint(660f, 852f), ExitPoint(346f, 852f),
            ExitPoint(337f, 850f), ExitPoint(333f, 848f),
            ExitPoint(331f, 846f), ExitPoint(327f, 840f),
            ExitPoint(325f, 828f), ExitPoint(321f, 818f),
            ExitPoint(317f, 810f), ExitPoint(315f, 802f),
            ExitPoint(315f, 794f), ExitPoint(318f, 782f),
            ExitPoint(324f, 770f), ExitPoint(326f, 758f),
            ExitPoint(330f, 750f), ExitPoint(335f, 746f)
        };

        static Vector2 StartPoint(float sourceX, float sourceY)
        {
            return new Vector2(
                (sourceX - 249f) / (760f - 249f),
                (706f - sourceY) / (706f - 576f));
        }

        static Vector2 ExitPoint(float sourceX, float sourceY)
        {
            return new Vector2(
                (sourceX - 313f) / (696f - 313f),
                (853f - sourceY) / (853f - 742f));
        }

        static Sprite startShapeSprite;
        static Sprite exitShapeSprite;
        Image overlay;
        RectTransform rect;
        Color accent;
        TempleStartButtonShape shape;
        UnityEngine.Events.UnityAction clickAction;
        bool pointerHover;
        bool worldHover;
        bool pressed;
        float forcedPressedUntil;
        Vector3 initialScale;

        public void Configure(Image target, Color color, TempleStartButtonShape buttonShape, UnityEngine.Events.UnityAction action)
        {
            overlay = target;
            rect = target.rectTransform;
            accent = color;
            shape = buttonShape;
            clickAction = action;
            initialScale = rect.localScale;
            overlay.sprite = GetShapeSprite(shape);
            overlay.type = Image.Type.Simple;
            overlay.preserveAspect = false;
            overlay.useSpriteMesh = true;
            overlay.alphaHitTestMinimumThreshold = 0.1f;
            ApplyVisual(true);
        }

        public void SetWorldHover(bool value)
        {
            worldHover = value;
            ApplyVisual(false);
        }

        public void PulsePressed(float seconds)
        {
            forcedPressedUntil = Mathf.Max(forcedPressedUntil, Time.unscaledTime + Mathf.Max(0f, seconds));
            ApplyVisual(true);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerHover = IsPointerInsideShape(eventData);
            ApplyVisual(false);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            pointerHover = IsPointerInsideShape(eventData);
            if (!pointerHover)
                pressed = false;
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
            pointerHover = IsPointerInsideShape(eventData);
            pressed = pointerHover;
            ApplyVisual(false);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressed = false;
            ApplyVisual(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (IsPointerInsideShape(eventData))
                clickAction?.Invoke();
        }

        void ApplyVisual(bool immediate)
        {
            if (overlay == null || rect == null)
                return;

            var hovering = pointerHover || worldHover;
            var visuallyPressed = pressed || Time.unscaledTime < forcedPressedUntil;
            var targetAlpha = visuallyPressed ? 0.55f : hovering ? 0.30f : 0f;
            overlay.color = new Color(accent.r, accent.g, accent.b, targetAlpha);
            // The authored button is an irregular painted surface. Feedback is
            // colour-only and never grows beyond or shrinks inside that exact
            // surface, so the source artwork remains perfectly registered.
            rect.localScale = initialScale;
        }

        void LateUpdate()
        {
            ApplyVisual(false);
        }

        bool IsPointerInsideShape(PointerEventData eventData)
        {
            if (rect == null || eventData == null ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out var local))
                return false;
            var bounds = rect.rect;
            var normalized = new Vector2(
                Mathf.InverseLerp(bounds.xMin, bounds.xMax, local.x),
                Mathf.InverseLerp(bounds.yMin, bounds.yMax, local.y));
            return ContainsShape(normalized, shape);
        }

        public static bool ContainsShape(Vector2 point, TempleStartButtonShape shape)
        {
            if (point.x < 0f || point.x > 1f || point.y < 0f || point.y > 1f)
                return false;
            return PointInPolygon(point, shape == TempleStartButtonShape.Start ? StartOutline : ExitOutline);
        }

        static bool PointInPolygon(Vector2 point, Vector2[] outline)
        {
            var inside = false;
            for (int current = 0, previous = outline.Length - 1; current < outline.Length; previous = current++)
            {
                var a = outline[current];
                var b = outline[previous];
                if ((a.y > point.y) != (b.y > point.y) &&
                    point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        static Sprite GetShapeSprite(TempleStartButtonShape shape)
        {
            var cached = shape == TempleStartButtonShape.Start ? startShapeSprite : exitShapeSprite;
            if (cached != null)
                return cached;

            var texture = new Texture2D(ShapeTextureWidth, ShapeTextureHeight, TextureFormat.RGBA32, false, true)
            {
                name = $"Temple{shape}Button_IrregularFeedbackMask",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[ShapeTextureWidth * ShapeTextureHeight];
            var samples = ShapeSupersampling * ShapeSupersampling;
            for (var y = 0; y < ShapeTextureHeight; y++)
            {
                for (var x = 0; x < ShapeTextureWidth; x++)
                {
                    var coverage = 0;
                    for (var sy = 0; sy < ShapeSupersampling; sy++)
                    {
                        for (var sx = 0; sx < ShapeSupersampling; sx++)
                        {
                            var point = new Vector2(
                                (x + (sx + 0.5f) / ShapeSupersampling) / ShapeTextureWidth,
                                (y + (sy + 0.5f) / ShapeSupersampling) / ShapeTextureHeight);
                            if (ContainsShape(point, shape))
                                coverage++;
                        }
                    }
                    pixels[y * ShapeTextureWidth + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * coverage / samples));
                }
            }
            texture.SetPixels32(pixels);
            // Keep the runtime texture readable because Image uses its alpha
            // channel for pixel-accurate desktop pointer hit testing.
            texture.Apply(false, false);
            cached = Sprite.Create(
                texture,
                new Rect(0f, 0f, ShapeTextureWidth, ShapeTextureHeight),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            cached.name = $"Temple{shape}Button_IrregularFeedbackMask";
            cached.hideFlags = HideFlags.HideAndDontSave;
            if (shape == TempleStartButtonShape.Start)
                startShapeSprite = cached;
            else
                exitShapeSprite = cached;
            return cached;
        }
    }
}

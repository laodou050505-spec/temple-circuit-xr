using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace TempleCircuit
{
    [DisallowMultipleComponent]
    public sealed class TempleMixedRealityPreview : MonoBehaviour
    {
        const float CameraStartupTimeout = 5f;

        Camera previewCamera;
        GameObject backgroundObject;
        Mesh backgroundMesh;
        MeshRenderer backgroundRenderer;
        Material backgroundMaterial;
        Texture2D roomTexture;
        WebCamTexture webCamTexture;
        Coroutine cameraAttempt;
        bool desktopActive;
        bool preferCamera = true;
        bool usingCamera;
        bool selectedCameraIsFrontFacing;
        int attemptVersion;
        int lastVideoWidth;
        int lastVideoHeight;
        int lastVideoRotation;
        bool lastVideoMirrored;
        float lastCameraAspect;
        float lastCameraFov;

        public bool UsingCamera => usingCamera;

        public void Initialize(Camera targetCamera)
        {
            previewCamera = targetCamera;
            if (previewCamera != null)
                previewCamera.depthTextureMode |= DepthTextureMode.Depth;
        }

        public void SetDesktopActive(bool active)
        {
            if (desktopActive == active)
                return;

            desktopActive = active;
            if (!active)
            {
                StopCameraAttempt();
                StopWebCam();
                if (backgroundObject != null)
                    backgroundObject.SetActive(false);
                return;
            }

            EnsureBackground();
            backgroundObject.SetActive(true);
            ShowRoomPreview();
            if (preferCamera)
                StartCameraAttempt();
        }

        void Update()
        {
            if (!desktopActive)
                return;

            if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
            {
                if (usingCamera || cameraAttempt != null || preferCamera)
                {
                    preferCamera = false;
                    StopCameraAttempt();
                    StopWebCam();
                    ShowRoomPreview();
                }
                else
                {
                    preferCamera = true;
                    StartCameraAttempt();
                }
            }

            UpdateBackgroundGeometry();
            if (usingCamera && webCamTexture != null && webCamTexture.didUpdateThisFrame)
                UpdateWebCamPresentation();
        }

        void EnsureBackground()
        {
            if (backgroundObject != null || previewCamera == null)
                return;

            backgroundObject = new GameObject("Desktop_MR_RealSpace_Background");
            backgroundObject.hideFlags = HideFlags.DontSave;
            backgroundObject.transform.SetParent(previewCamera.transform, false);

            var meshFilter = backgroundObject.AddComponent<MeshFilter>();
            backgroundRenderer = backgroundObject.AddComponent<MeshRenderer>();
            backgroundRenderer.shadowCastingMode = ShadowCastingMode.Off;
            backgroundRenderer.receiveShadows = false;
            backgroundRenderer.lightProbeUsage = LightProbeUsage.Off;
            backgroundRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            backgroundMesh = new Mesh { name = "Desktop MR Background Mesh", hideFlags = HideFlags.DontSave };
            backgroundMesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f)
            };
            backgroundMesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            backgroundMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            backgroundMesh.RecalculateBounds();
            meshFilter.sharedMesh = backgroundMesh;

            var shader = Resources.Load<Shader>("TempleCameraBackground") ?? Shader.Find("TempleCircuit/CameraBackground");
            if (shader == null)
            {
                Debug.LogError("Desktop MR preview shader is missing.");
                backgroundObject.SetActive(false);
                return;
            }

            backgroundMaterial = new Material(shader)
            {
                name = "Desktop MR Background Material",
                hideFlags = HideFlags.DontSave,
                renderQueue = (int)RenderQueue.Transparent + 100
            };
            backgroundRenderer.sharedMaterial = backgroundMaterial;
            UpdateBackgroundGeometry();
        }

        void UpdateBackgroundGeometry()
        {
            if (backgroundObject == null || previewCamera == null)
                return;

            var distance = Mathf.Max(previewCamera.nearClipPlane + 0.025f, 0.08f);
            var height = 2f * distance * Mathf.Tan(previewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var width = height * Mathf.Max(0.1f, previewCamera.aspect);
            backgroundObject.transform.localPosition = Vector3.forward * distance;
            backgroundObject.transform.localRotation = Quaternion.identity;
            backgroundObject.transform.localScale = new Vector3(width * 1.01f, height * 1.01f, 1f);

            if (!Mathf.Approximately(lastCameraAspect, previewCamera.aspect) || !Mathf.Approximately(lastCameraFov, previewCamera.fieldOfView))
            {
                lastCameraAspect = previewCamera.aspect;
                lastCameraFov = previewCamera.fieldOfView;
                UpdateTextureTransform();
            }
        }

        void StartCameraAttempt()
        {
            if (!desktopActive || cameraAttempt != null || usingCamera)
                return;
            cameraAttempt = StartCoroutine(TryStartCamera(++attemptVersion));
        }

        IEnumerator TryStartCamera(int version)
        {
            ShowRoomPreview();

            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
                yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);

            if (version != attemptVersion || !desktopActive)
            {
                cameraAttempt = null;
                yield break;
            }

            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                preferCamera = false;
                cameraAttempt = null;
                Debug.LogWarning("Camera access was not granted. Desktop MR is using the room simulation.");
                yield break;
            }

            var devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                preferCamera = false;
                cameraAttempt = null;
                Debug.LogWarning("No desktop camera was found. Desktop MR is using the room simulation.");
                yield break;
            }

            var selected = devices[0];
            for (var i = 0; i < devices.Length; i++)
            {
                if (!devices[i].isFrontFacing)
                {
                    selected = devices[i];
                    break;
                }
            }

            selectedCameraIsFrontFacing = selected.isFrontFacing;
            webCamTexture = new WebCamTexture(selected.name, 1280, 720, 30);
            webCamTexture.Play();

            var timeoutAt = Time.realtimeSinceStartup + CameraStartupTimeout;
            while (version == attemptVersion && desktopActive && Time.realtimeSinceStartup < timeoutAt)
            {
                if (webCamTexture.didUpdateThisFrame && webCamTexture.width > 64 && webCamTexture.height > 64)
                    break;
                yield return null;
            }

            cameraAttempt = null;
            if (version != attemptVersion || !desktopActive)
                yield break;

            if (webCamTexture == null || !webCamTexture.isPlaying || webCamTexture.width <= 64 || webCamTexture.height <= 64)
            {
                preferCamera = false;
                StopWebCam();
                ShowRoomPreview();
                Debug.LogWarning("The desktop camera did not produce frames. Desktop MR is using the room simulation.");
                yield break;
            }

            usingCamera = true;
            backgroundMaterial.SetTexture("_MainTex", webCamTexture);
            UpdateWebCamPresentation();
            Debug.Log($"Desktop MR camera preview active: {selected.name} ({webCamTexture.width}x{webCamTexture.height}).");
        }

        void UpdateWebCamPresentation()
        {
            if (webCamTexture == null || backgroundMaterial == null)
                return;

            if (lastVideoWidth == webCamTexture.width &&
                lastVideoHeight == webCamTexture.height &&
                lastVideoRotation == webCamTexture.videoRotationAngle &&
                lastVideoMirrored == webCamTexture.videoVerticallyMirrored)
                return;

            lastVideoWidth = webCamTexture.width;
            lastVideoHeight = webCamTexture.height;
            lastVideoRotation = webCamTexture.videoRotationAngle;
            lastVideoMirrored = webCamTexture.videoVerticallyMirrored;
            UpdateTextureTransform();
        }

        void UpdateTextureTransform()
        {
            if (backgroundMaterial == null || previewCamera == null)
                return;

            var sourceWidth = usingCamera && webCamTexture != null ? Mathf.Max(1, webCamTexture.width) : 16;
            var sourceHeight = usingCamera && webCamTexture != null ? Mathf.Max(1, webCamTexture.height) : 9;
            var rotation = usingCamera && webCamTexture != null ? webCamTexture.videoRotationAngle : 0;
            var rotated = rotation == 90 || rotation == 270;
            var sourceAspect = rotated ? sourceHeight / (float)sourceWidth : sourceWidth / (float)sourceHeight;
            var targetAspect = Mathf.Max(0.1f, previewCamera.aspect);
            var crop = Vector2.one;
            if (sourceAspect > targetAspect)
                crop.x = targetAspect / sourceAspect;
            else
                crop.y = sourceAspect / targetAspect;

            var radians = -rotation * Mathf.Deg2Rad;
            backgroundMaterial.SetVector("_CropScale", new Vector4(crop.x, crop.y, 0f, 0f));
            backgroundMaterial.SetVector("_UvRotation", new Vector4(Mathf.Cos(radians), Mathf.Sin(radians), 0f, 0f));
            backgroundMaterial.SetVector("_Mirror", new Vector4(
                usingCamera && selectedCameraIsFrontFacing ? -1f : 1f,
                usingCamera && webCamTexture != null && webCamTexture.videoVerticallyMirrored ? -1f : 1f,
                0f,
                0f));
        }

        void ShowRoomPreview()
        {
            if (backgroundMaterial == null)
                return;

            usingCamera = false;
            if (roomTexture == null)
                roomTexture = CreateRoomTexture();
            backgroundMaterial.SetTexture("_MainTex", roomTexture);
            lastVideoWidth = 0;
            lastVideoHeight = 0;
            lastVideoRotation = 0;
            lastVideoMirrored = false;
            UpdateTextureTransform();
        }

        static Texture2D CreateRoomTexture()
        {
            const int width = 960;
            const int height = 540;
            var pixels = new Color32[width * height];
            var wall = new Color32(179, 184, 177, 255);
            var floor = new Color32(117, 105, 91, 255);
            var window = new Color32(134, 177, 192, 255);
            var frame = new Color32(225, 221, 206, 255);
            var shelf = new Color32(74, 76, 70, 255);
            var table = new Color32(104, 84, 65, 255);

            for (var y = 0; y < height; y++)
            {
                var v = y / (float)(height - 1);
                for (var x = 0; x < width; x++)
                {
                    var u = x / (float)(width - 1);
                    Color32 color;
                    if (v > 0.34f)
                    {
                        var shade = (byte)Mathf.Clamp(220f + 22f * (u - 0.5f) - 28f * (v - 0.34f), 0f, 255f);
                        color = new Color32(
                            (byte)(wall.r * shade / 255),
                            (byte)(wall.g * shade / 255),
                            (byte)(wall.b * shade / 255), 255);
                    }
                    else
                    {
                        var shade = (byte)Mathf.Clamp(168f + 76f * v, 0f, 255f);
                        color = new Color32(
                            (byte)(floor.r * shade / 255),
                            (byte)(floor.g * shade / 255),
                            (byte)(floor.b * shade / 255), 255);
                        var perspectiveLine = Mathf.Abs(Mathf.Repeat((u - 0.5f) / Mathf.Max(0.08f, 0.43f - v) * 0.075f + 0.5f, 0.1f) - 0.05f);
                        if (perspectiveLine < 0.003f)
                            color = new Color32(88, 80, 70, 255);
                    }

                    if (u > 0.10f && u < 0.34f && v > 0.54f && v < 0.86f)
                    {
                        color = window;
                        if (Mathf.Abs(u - 0.22f) < 0.006f || Mathf.Abs(v - 0.70f) < 0.009f ||
                            u < 0.116f || u > 0.324f || v < 0.558f || v > 0.842f)
                            color = frame;
                    }

                    if (u > 0.77f && u < 0.91f && v > 0.40f && v < 0.78f)
                    {
                        color = shelf;
                        if (Mathf.Abs(Mathf.Repeat(v - 0.41f, 0.09f)) < 0.012f)
                            color = new Color32(132, 113, 88, 255);
                    }

                    var tableHalfWidth = Mathf.Lerp(0.46f, 0.34f, Mathf.InverseLerp(0.04f, 0.30f, v));
                    if (v > 0.04f && v < 0.30f && Mathf.Abs(u - 0.5f) < tableHalfWidth)
                    {
                        var tableLight = (byte)Mathf.Clamp(190f + v * 95f, 0f, 255f);
                        color = new Color32(
                            (byte)(table.r * tableLight / 255),
                            (byte)(table.g * tableLight / 255),
                            (byte)(table.b * tableLight / 255), 255);
                    }

                    pixels[y * width + x] = color;
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
            {
                name = "Desktop MR Room Simulation",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        void StopCameraAttempt()
        {
            attemptVersion++;
            if (cameraAttempt != null)
            {
                StopCoroutine(cameraAttempt);
                cameraAttempt = null;
            }
        }

        void StopWebCam()
        {
            usingCamera = false;
            if (webCamTexture == null)
                return;
            if (webCamTexture.isPlaying)
                webCamTexture.Stop();
            Destroy(webCamTexture);
            webCamTexture = null;
        }

        void OnDestroy()
        {
            StopCameraAttempt();
            StopWebCam();
            if (backgroundMaterial != null)
                Destroy(backgroundMaterial);
            if (backgroundMesh != null)
                Destroy(backgroundMesh);
            if (roomTexture != null)
                Destroy(roomTexture);
            if (backgroundObject != null)
                Destroy(backgroundObject);
        }
    }
}

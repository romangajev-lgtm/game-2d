using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AIInterrogation
{
    public class SceneAtmosphereController : MonoBehaviour
    {
        private const float PixelsPerUnit = 100f;

        [SerializeField] private string calmSceneResourcePath = "Evidence/room_1";
        [SerializeField] private string angrySceneResourcePath = "Evidence/room_1";
        [SerializeField] private string tableSlamSceneResourcePath = "Evidence/room_1_table_slam";
        [SerializeField] private string fallbackCalmSceneResourcePath = "Art/interrogator_calm";
        [SerializeField] private string fallbackAngrySceneResourcePath = "Art/interrogator_angry";
        [SerializeField] private bool cleanSceneImage = true;

        private Camera sceneCamera;
        private SpriteRenderer interrogatorRenderer;
        private Sprite calmSprite;
        private Sprite angrySprite;
        private Sprite tableSlamSprite;
        private RawImage spotlightOverlay;
        private RawImage grainOverlay;
        private RectTransform overlayRoot;
        private Vector3 baseSpritePosition;
        private Vector3 animationOffset;
        private Coroutine tableSlamRoutine;
        private InterrogatorMood desiredMood;
        private bool temporaryMoodActive;
        private float shakeTime;
        private float grainTick;
        private Texture2D grainTexture;

        public void Initialize()
        {
            sceneCamera = Camera.main;
            if (sceneCamera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                sceneCamera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            sceneCamera.orthographic = true;
            sceneCamera.orthographicSize = 4.5f;
            sceneCamera.transform.position = new Vector3(0f, 0f, -10f);
            sceneCamera.backgroundColor = new Color(0.005f, 0.004f, 0.003f, 1f);
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;

            CreateInterrogatorSprite();
            if (!cleanSceneImage)
            {
                CreateOverlayCanvas();
            }
        }

        public void SetMood(InterrogatorMood mood, bool pulse)
        {
            if (interrogatorRenderer == null)
            {
                return;
            }

            desiredMood = mood;
            if (!temporaryMoodActive)
            {
                ApplyMood(desiredMood);
            }

            if (!cleanSceneImage && pulse && mood == InterrogatorMood.Angry)
            {
                shakeTime = 0.28f;
            }
        }

        public void TriggerTableSlam(float returnDelay)
        {
            if (interrogatorRenderer == null)
            {
                return;
            }

            if (tableSlamRoutine != null)
            {
                StopCoroutine(tableSlamRoutine);
            }

            tableSlamRoutine = StartCoroutine(TableSlamRoutine(Mathf.Max(0.1f, returnDelay)));
        }

        public void ForceMood(InterrogatorMood mood)
        {
            if (tableSlamRoutine != null)
            {
                StopCoroutine(tableSlamRoutine);
                tableSlamRoutine = null;
            }

            desiredMood = mood;
            temporaryMoodActive = false;
            animationOffset = Vector3.zero;
            shakeTime = 0f;
            ApplyMood(desiredMood);
            if (interrogatorRenderer != null)
            {
                interrogatorRenderer.transform.position = baseSpritePosition;
            }
        }

        private void Update()
        {
            if (interrogatorRenderer == null)
            {
                return;
            }

            if (cleanSceneImage)
            {
                interrogatorRenderer.color = Color.white;
                interrogatorRenderer.transform.position = baseSpritePosition;
                FitSpriteToCamera();
                return;
            }

            var flicker = 0.93f + Mathf.PerlinNoise(Time.time * 11.7f, 0.13f) * 0.09f;
            interrogatorRenderer.color = new Color(1.00f * flicker, 0.88f * flicker, 0.64f * flicker, 1f);

            if (spotlightOverlay != null)
            {
                var color = spotlightOverlay.color;
                color.a = 0.20f + Mathf.PerlinNoise(Time.time * 8f, 2.1f) * 0.06f;
                spotlightOverlay.color = color;
            }

            UpdateShake();
            UpdateGrain();
            FitSpriteToCamera();
        }

        private void CreateInterrogatorSprite()
        {
            var calmTexture = LoadSceneTexture(calmSceneResourcePath, fallbackCalmSceneResourcePath);
            var angryTexture = LoadSceneTexture(angrySceneResourcePath, fallbackAngrySceneResourcePath);
            var tableSlamTexture = LoadSceneTexture(tableSlamSceneResourcePath, fallbackAngrySceneResourcePath);
            if (calmTexture == null || angryTexture == null)
            {
                Debug.LogError("Interrogation scene images are missing from Assets/Resources.");
                return;
            }

            calmSprite = CreateSprite(calmTexture);
            angrySprite = CreateSprite(angryTexture);
            tableSlamSprite = tableSlamTexture != null ? CreateSprite(tableSlamTexture) : angrySprite;

            var spriteObject = new GameObject("Interrogator Fullscreen Sprite");
            interrogatorRenderer = spriteObject.AddComponent<SpriteRenderer>();
            interrogatorRenderer.sprite = calmSprite;
            if (!cleanSceneImage)
            {
                interrogatorRenderer.sharedMaterial = RuntimeMaterialFactory.SpriteMaterial;
            }

            interrogatorRenderer.color = Color.white;
            interrogatorRenderer.sortingOrder = 0;
            desiredMood = InterrogatorMood.Calm;
            baseSpritePosition = Vector3.zero;
            spriteObject.transform.position = baseSpritePosition;
            FitSpriteToCamera();
        }

        private static Texture2D LoadSceneTexture(string primaryPath, string fallbackPath)
        {
            var texture = Resources.Load<Texture2D>(primaryPath);
            if (texture != null)
            {
                return texture;
            }

            Debug.LogWarning($"Scene texture missing at Resources/{primaryPath}. Falling back to Resources/{fallbackPath}.");
            return Resources.Load<Texture2D>(fallbackPath);
        }

        private static Sprite CreateSprite(Texture2D texture)
        {
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        }

        private void FitSpriteToCamera()
        {
            if (interrogatorRenderer == null || sceneCamera == null || interrogatorRenderer.sprite == null)
            {
                return;
            }

            var spriteSize = interrogatorRenderer.sprite.bounds.size;
            var cameraHeight = sceneCamera.orthographicSize * 2f;
            var cameraWidth = cameraHeight * Mathf.Max(0.01f, sceneCamera.aspect);
            var scale = cleanSceneImage
                ? Mathf.Min(cameraWidth / spriteSize.x, cameraHeight / spriteSize.y)
                : Mathf.Max(cameraWidth / spriteSize.x, cameraHeight / spriteSize.y);
            interrogatorRenderer.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void CreateOverlayCanvas()
        {
            var canvasObject = new GameObject("CRT Atmosphere Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
            overlayRoot = canvasObject.GetComponent<RectTransform>();

            spotlightOverlay = CreateRawOverlay("Hard Lamp Spot", TextureFactory.CreateSpotlightTexture(256, 144), new Color(1f, 0.76f, 0.35f, 0.18f));
            CreateLampBar();
            CreateRawOverlay("Vignette", TextureFactory.CreateVignetteTexture(256, 144), new Color(0f, 0f, 0f, 0.58f));
            CreateRawOverlay("CRT Scanlines", TextureFactory.CreateScanlineTexture(), new Color(0f, 0f, 0f, 0.18f), new Rect(0f, 0f, 1f, 180f));
            grainTexture = TextureFactory.CreateGrainTexture(256, 144);
            grainOverlay = CreateRawOverlay("Film Grain", grainTexture, new Color(1f, 1f, 1f, 0.035f));
        }

        private RawImage CreateRawOverlay(string name, Texture texture, Color color, Rect? uv = null)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(overlayRoot, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.StretchToParent();
            var raw = obj.AddComponent<RawImage>();
            raw.texture = texture;
            raw.color = color;
            RuntimeMaterialFactory.ApplyTo(raw);
            raw.raycastTarget = false;
            if (uv.HasValue)
            {
                raw.uvRect = uv.Value;
            }

            return raw;
        }

        private void CreateLampBar()
        {
            var obj = new GameObject("Overhead Lamp");
            obj.transform.SetParent(overlayRoot, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.43f, 0.88f);
            rect.anchorMax = new Vector2(0.57f, 0.92f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = obj.AddComponent<Image>();
            image.sprite = TextureFactory.CreateSolidSprite(new Color(0.88f, 0.68f, 0.36f, 1f));
            image.color = new Color(0.95f, 0.72f, 0.36f, 0.28f);
            RuntimeMaterialFactory.ApplyTo(image);
            image.raycastTarget = false;
        }

        private void UpdateShake()
        {
            if (shakeTime <= 0f)
            {
                interrogatorRenderer.transform.position = baseSpritePosition + animationOffset;
                return;
            }

            shakeTime -= Time.deltaTime;
            var amount = Mathf.Lerp(0f, 0.035f, shakeTime / 0.28f);
            var offset = new Vector3(Random.Range(-amount, amount), Random.Range(-amount * 0.5f, amount * 0.5f), 0f);
            interrogatorRenderer.transform.position = baseSpritePosition + animationOffset + offset;
        }

        private IEnumerator TableSlamRoutine(float returnDelay)
        {
            temporaryMoodActive = true;
            interrogatorRenderer.sprite = tableSlamSprite != null ? tableSlamSprite : angrySprite;

            if (cleanSceneImage)
            {
                yield return new WaitForSeconds(returnDelay);
                temporaryMoodActive = false;
                ApplyMood(desiredMood);
                tableSlamRoutine = null;
                yield break;
            }

            shakeTime = 0.55f;

            const float windupDuration = 0.10f;
            var elapsed = 0f;
            while (elapsed < windupDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / windupDuration);
                animationOffset = Vector3.Lerp(Vector3.zero, new Vector3(0f, -0.045f, 0f), t);
                yield return null;
            }

            const float impactDuration = 0.14f;
            elapsed = 0f;
            while (elapsed < impactDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / impactDuration);
                animationOffset = Vector3.Lerp(new Vector3(0f, -0.045f, 0f), Vector3.zero, t);
                yield return null;
            }

            animationOffset = Vector3.zero;
            yield return new WaitForSeconds(returnDelay);

            temporaryMoodActive = false;
            ApplyMood(desiredMood);
            tableSlamRoutine = null;
        }

        private void ApplyMood(InterrogatorMood mood)
        {
            if (interrogatorRenderer != null)
            {
                interrogatorRenderer.sprite = mood == InterrogatorMood.Angry ? angrySprite : calmSprite;
            }
        }

        private void UpdateGrain()
        {
            if (grainTexture == null || grainOverlay == null)
            {
                return;
            }

            grainTick += Time.deltaTime;
            if (grainTick < 0.08f)
            {
                return;
            }

            grainTick = 0f;
            TextureFactory.RefreshGrainTexture(grainTexture);
        }
    }

    public static class TextureFactory
    {
        public static Sprite CreateSolidSprite(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        public static Texture2D CreateVignetteTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var uv = new Vector2((x + 0.5f) / width, (y + 0.5f) / height);
                    var d = Vector2.Distance(uv, new Vector2(0.5f, 0.52f));
                    var alpha = Mathf.SmoothStep(0.06f, 1f, Mathf.InverseLerp(0.28f, 0.72f, d));
                    texture.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
                }
            }

            texture.Apply();
            return texture;
        }

        public static Texture2D CreateSpotlightTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var uv = new Vector2((x + 0.5f) / width, (y + 0.5f) / height);
                    var center = new Vector2(0.5f, 0.56f);
                    var dx = (uv.x - center.x) / 0.34f;
                    var dy = (uv.y - center.y) / 0.46f;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    var alpha = 1f - Mathf.SmoothStep(0.0f, 1.0f, distance);
                    texture.SetPixel(x, y, new Color(1f, 0.78f, 0.42f, alpha));
                }
            }

            texture.Apply();
            return texture;
        }

        public static Texture2D CreateScanlineTexture()
        {
            var texture = new Texture2D(1, 4, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point
            };
            texture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.55f));
            texture.SetPixel(0, 1, Color.clear);
            texture.SetPixel(0, 2, Color.clear);
            texture.SetPixel(0, 3, Color.clear);
            texture.Apply();
            return texture;
        }

        public static Texture2D CreateGrainTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point
            };
            RefreshGrainTexture(texture);
            return texture;
        }

        public static void RefreshGrainTexture(Texture2D texture)
        {
            for (var y = 0; y < texture.height; y++)
            {
                for (var x = 0; x < texture.width; x++)
                {
                    var value = Random.Range(0.35f, 1f);
                    texture.SetPixel(x, y, new Color(value, value, value, 1f));
                }
            }

            texture.Apply(false);
        }
    }
}

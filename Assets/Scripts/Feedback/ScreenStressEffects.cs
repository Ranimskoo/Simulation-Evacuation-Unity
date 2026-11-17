using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using IgnisEscape.Adaptive;
using IgnisEscape.Core;

namespace IgnisEscape.Feedback
{
    /// <summary>
    /// Efek layar hasil keputusan AI (state resmi dari Python):
    /// - Tenang    : layar normal & jernih.
    /// - Ragu-Ragu : vignette mulai menyempit + sedikit buram + sedikit bergetar.
    /// - Panik     : vignette kuat (tunnel vision) + lebih buram + lebih bergetar + layar lebih gelap.
    /// Getaran hanya diterapkan pada Main Camera (anak CameraPivot), sedangkan TelemetryExtractor
    /// membaca CameraPivot sehingga getaran TIDAK mengotori bacaan omega (camera_jitter).
    /// </summary>
    public class ScreenStressEffects : MonoBehaviour
    {
        [Header("Referensi")]
        [SerializeField] private AdaptiveInterventionController adaptiveController;
        [SerializeField] private Transform shakeTarget;
        [SerializeField] private Image darkOverlay;
        [SerializeField] private Image radialVignetteOverlay;
        [SerializeField] private Image blurHazeOverlay;

        [Header("Transisi")]
        [SerializeField] private float transitionSpeed = 1.8f; // level per detik (0=Tenang, 1=Ragu, 2=Panik)

        [Header("Buram (Gaussian DoF + Haze) - Tenang / Ragu / Panik")]
        [SerializeField] private Vector3 blurStart = new Vector3(50f, 0.05f, 0.01f);
        [SerializeField] private Vector3 blurEnd = new Vector3(60f, 1.4f, 0.30f);
        [SerializeField] private Vector3 blurRadius = new Vector3(0.5f, 1.35f, 1.5f);
        [SerializeField] private Vector3 blurOverlayAlpha = new Vector3(0f, 0.25f, 0.50f);
        [SerializeField] private Vector3 chromaticIntensity = new Vector3(0f, 0.48f, 1.0f);
        [SerializeField] private Vector3 lensDistortionIntensity = new Vector3(0f, -0.15f, -0.34f);

        [Header("Getaran - Tenang / Ragu / Panik")]
        [SerializeField] private Vector3 shakePosition = new Vector3(0f, 0.026f, 0.075f);
        [SerializeField] private Vector3 shakeRotation = new Vector3(0f, 1.85f, 4.8f);
        [SerializeField] private Vector3 shakeFrequency = new Vector3(6f, 9.5f, 17.5f);

        [Header("Vignette & Gelap - Tenang / Ragu / Panik")]
        [SerializeField] private Vector3 vignetteOverlayAlpha = new Vector3(0f, 0.74f, 0.96f);
        [SerializeField] private Vector3 vignetteScale = new Vector3(1.35f, 1.10f, 0.92f);
        [SerializeField] private Vector3 urpVignetteIntensity = new Vector3(0f, 0.52f, 0.78f);
        [SerializeField] private Vector3 overlayAlpha = new Vector3(0f, 0.10f, 0.42f);
        [SerializeField] private Vector3 postExposure = new Vector3(0f, -0.30f, -1.25f);

        public float CurrentLevel { get; private set; }

        private Volume _volume;
        private VolumeProfile _profile;
        private DepthOfField _dof;
        private Vignette _vignette;
        private ColorAdjustments _color;
        private ChromaticAberration _chromatic;
        private LensDistortion _distortion;

        private Vector3 _baseLocalPos;
        private Quaternion _baseLocalRot;
        private float _seed;
        private Texture2D _vignetteTex;
        private Texture2D _blurTex;
        private Sprite _vignetteSprite;
        private Sprite _blurSprite;

        private void Awake()
        {
            if (adaptiveController == null) adaptiveController = FindAnyObjectByType<AdaptiveInterventionController>();
            if (shakeTarget == null && Camera.main != null) shakeTarget = Camera.main.transform;

            if (shakeTarget != null)
            {
                _baseLocalPos = shakeTarget.localPosition;
                _baseLocalRot = shakeTarget.localRotation;
                var cam = shakeTarget.GetComponent<Camera>();
                if (cam != null)
                {
                    var camData = cam.GetUniversalAdditionalCameraData();
                    camData.renderPostProcessing = true;
                    camData.requiresDepthTexture = true;
                }
            }

            if (darkOverlay != null)
            {
                darkOverlay.raycastTarget = false;
                darkOverlay.color = new Color(0f, 0f, 0f, 0f);
            }

            EnsureProceduralOverlays();
            _seed = Random.value * 100f;
            CreateVolume();
        }

        private void EnsureProceduralOverlays()
        {
            Transform canvasParent = darkOverlay != null ? darkOverlay.transform.parent : null;
            if (canvasParent == null)
            {
                GameObject existingCanvas = GameObject.Find("ScreenOverlayCanvas");
                if (existingCanvas != null) canvasParent = existingCanvas.transform;
            }
            if (canvasParent == null) return;

            _vignetteSprite = CreateRadialVignetteSprite(256, out _vignetteTex);
            _blurSprite = CreateSoftBlurHazeSprite(128, out _blurTex);

            if (blurHazeOverlay == null)
            {
                GameObject blurGo = new GameObject("StressBlurHazeOverlay");
                blurGo.transform.SetParent(canvasParent, false);
                blurGo.transform.SetAsFirstSibling();
                RectTransform rt = blurGo.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = new Vector2(80f, 80f);
                blurHazeOverlay = blurGo.AddComponent<Image>();
            }
            blurHazeOverlay.sprite = _blurSprite;
            blurHazeOverlay.type = Image.Type.Simple;
            blurHazeOverlay.raycastTarget = false;
            blurHazeOverlay.color = new Color(0.78f, 0.82f, 0.85f, 0f);

            if (radialVignetteOverlay == null)
            {
                GameObject vigGo = new GameObject("StressRadialVignetteOverlay");
                vigGo.transform.SetParent(canvasParent, false);
                vigGo.transform.SetSiblingIndex(1);
                RectTransform rt = vigGo.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
                radialVignetteOverlay = vigGo.AddComponent<Image>();
            }
            radialVignetteOverlay.sprite = _vignetteSprite;
            radialVignetteOverlay.type = Image.Type.Simple;
            radialVignetteOverlay.raycastTarget = false;
            radialVignetteOverlay.color = new Color(0.03f, 0.01f, 0.01f, 0f);
        }

        private void CreateVolume()
        {
            GameObject volObj = new GameObject("StressPostFX_Volume");
            volObj.transform.SetParent(transform, false);
            _volume = volObj.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 100f;

            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _volume.sharedProfile = _profile;

            _dof = _profile.Add<DepthOfField>(false);
            _dof.mode.Override(DepthOfFieldMode.Gaussian);
            _dof.gaussianStart.Override(blurStart.x);
            _dof.gaussianEnd.Override(blurEnd.x);
            _dof.gaussianMaxRadius.Override(blurRadius.x);
            _dof.highQualitySampling.Override(true);
            _dof.active = false;

            _vignette = _profile.Add<Vignette>(false);
            _vignette.intensity.Override(0f);
            _vignette.smoothness.Override(0.55f);
            _vignette.rounded.Override(false);
            _vignette.color.Override(new Color(0.04f, 0.01f, 0.01f, 1f));
            _vignette.active = true;

            _color = _profile.Add<ColorAdjustments>(false);
            _color.postExposure.Override(0f);
            _color.active = true;

            _chromatic = _profile.Add<ChromaticAberration>(false);
            _chromatic.intensity.Override(0f);
            _chromatic.active = true;

            _distortion = _profile.Add<LensDistortion>(false);
            _distortion.intensity.Override(0f);
            _distortion.active = true;
        }

        private void Update()
        {
            float target = 0f;
            if (adaptiveController != null)
            {
                switch (adaptiveController.CurrentState)
                {
                    case BehaviorState.RaguRagu: target = 1f; break;
                    case BehaviorState.Panik: target = 2f; break;
                }
            }
            CurrentLevel = Mathf.MoveTowards(CurrentLevel, target, transitionSpeed * Time.deltaTime);

            float pulseFreq = CurrentLevel > 1.2f ? 3.2f : 1.4f;
            float pulse = Mathf.Sin(Time.time * pulseFreq * Mathf.PI * 2f) * (0.055f * Mathf.Clamp01(CurrentLevel));

            // 1. Buram (URP DoF + Chromatic + LensDistortion + UI Blur Haze)
            if (_dof != null)
            {
                _dof.active = CurrentLevel > 0.02f;
                _dof.gaussianStart.value = Eval(blurStart);
                _dof.gaussianEnd.value = Mathf.Max(Eval(blurEnd), _dof.gaussianStart.value + 0.08f);
                _dof.gaussianMaxRadius.value = Mathf.Clamp(Eval(blurRadius), 0.5f, 1.5f);
            }
            if (_chromatic != null)
            {
                _chromatic.intensity.value = Mathf.Clamp01(Eval(chromaticIntensity));
            }
            if (_distortion != null)
            {
                float baseDist = Eval(lensDistortionIntensity);
                float dizzyWave = Mathf.Sin(Time.time * 2.3f) * 0.06f * Mathf.Clamp01(CurrentLevel);
                _distortion.intensity.value = Mathf.Clamp(baseDist + dizzyWave, -0.6f, 0.6f);
            }
            if (blurHazeOverlay != null)
            {
                Color bc = blurHazeOverlay.color;
                bc.a = Mathf.Clamp01(Eval(blurOverlayAlpha) + pulse * 0.5f);
                blurHazeOverlay.color = bc;

                float jitter = 14f * Mathf.Clamp01(CurrentLevel * 0.6f);
                blurHazeOverlay.rectTransform.anchoredPosition = new Vector2(
                    Mathf.Sin(Time.time * 11f) * jitter,
                    Mathf.Cos(Time.time * 9f) * jitter
                );
            }

            // 2. Vignette & Gelap (UI Radial Vignette + Dark Overlay + URP Vignette + Exposure)
            if (radialVignetteOverlay != null)
            {
                Color vc = CurrentLevel > 1.15f
                    ? new Color(0.08f, 0.01f, 0.01f, 1f)
                    : new Color(0.02f, 0.02f, 0.03f, 1f);
                vc.a = Mathf.Clamp01(Eval(vignetteOverlayAlpha) + pulse);
                radialVignetteOverlay.color = vc;

                float sc = Mathf.Max(0.85f, Eval(vignetteScale) - pulse * 0.35f);
                radialVignetteOverlay.rectTransform.localScale = new Vector3(sc, sc, 1f);
            }
            if (_vignette != null) _vignette.intensity.value = Mathf.Clamp01(Eval(urpVignetteIntensity) + pulse);
            if (_color != null) _color.postExposure.value = Eval(postExposure);
            if (darkOverlay != null)
            {
                Color c = darkOverlay.color;
                c.a = Mathf.Clamp01(Eval(overlayAlpha) + Mathf.Max(0f, pulse * 0.5f));
                darkOverlay.color = c;
            }

            // 3. Getaran Kamera (hanya pada Main Camera, terisolasi dari CameraPivot)
            if (shakeTarget != null)
            {
                float ampPos = Eval(shakePosition);
                float ampRot = Eval(shakeRotation);
                float t = Time.time * Eval(shakeFrequency);

                float nx = Mathf.PerlinNoise(_seed, t) * 2f - 1f;
                float ny = Mathf.PerlinNoise(_seed + 11.3f, t) * 2f - 1f;
                float rx = Mathf.PerlinNoise(_seed + 23.7f, t) * 2f - 1f;
                float ry = Mathf.PerlinNoise(_seed + 37.1f, t) * 2f - 1f;
                float rz = Mathf.PerlinNoise(_seed + 51.9f, t) * 2f - 1f;

                shakeTarget.localPosition = _baseLocalPos + new Vector3(nx, ny, 0f) * ampPos;
                shakeTarget.localRotation = _baseLocalRot * Quaternion.Euler(rx * ampRot, ry * ampRot, rz * ampRot * 0.65f);
            }
        }

        private float Eval(Vector3 values)
        {
            return CurrentLevel <= 1f
                ? Mathf.Lerp(values.x, values.y, CurrentLevel)
                : Mathf.Lerp(values.y, values.z, CurrentLevel - 1f);
        }

        private static Sprite CreateRadialVignetteSprite(int size, out Texture2D tex)
        {
            tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Proc_RadialVignette",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color[] pixels = new Color[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                float dy = (y - half) / half;
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    // Jarak elips dari pusat layar (0 di tengah, ~1 di tepi, ~1.41 di sudut)
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    // Tengah (dist < 0.25) bening; tepi luar (dist >= 0.88) pekat
                    float t = Mathf.InverseLerp(0.24f, 0.90f, dist);
                    float alpha = Mathf.SmoothStep(0f, 1f, t);
                    alpha = Mathf.Pow(alpha, 1.35f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateSoftBlurHazeSprite(int size, out Texture2D tex)
        {
            tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Proc_BlurHaze",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color[] pixels = new Color[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                float dy = (y - half) / half;
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float ring = 0.45f + 0.55f * Mathf.InverseLerp(0.10f, 0.85f, dist);
                    float noise = 0.85f + 0.15f * Mathf.PerlinNoise(x * 0.12f, y * 0.12f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, ring * noise);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private void OnDisable()
        {
            if (shakeTarget != null)
            {
                shakeTarget.localPosition = _baseLocalPos;
                shakeTarget.localRotation = _baseLocalRot;
            }
        }

        private void OnDestroy()
        {
            if (_profile != null) Destroy(_profile);
            if (_vignetteTex != null) Destroy(_vignetteTex);
            if (_blurTex != null) Destroy(_blurTex);
        }
    }
}

using UnityEngine;
using UnityEngine.UI;
using IgnisEscape.Core;
using IgnisEscape.Player;

namespace IgnisEscape.Feedback
{
    /// <summary>
    /// Komponen Pseudo-Haptic (Bagian 7B):
    /// - Saat menyentuh rintangan/puing: damage flash vignette merah sesaat,
    ///   bunyi benturan frekuensi rendah prosedural, dan perlambatan kecepatan gerak sesaat.
    /// - Saat status Panik: breathing ring toska #00A99D + peredaman sensitivitas kamera.
    /// </summary>
    public class PseudoHapticController : MonoBehaviour
    {
        [Header("Referensi Pemain & Audio")]
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private CameraDampener cameraDampener;
        [SerializeField] private AudioSource impactAudioSource;

        [Header("UI Overlay")]
        [SerializeField] private Image damageVignetteImage;
        [SerializeField] private RectTransform breathingRingTransform;
        [SerializeField] private Image breathingRingImage;

        [Header("Parameter Pseudo-Haptic")]
        [SerializeField] private float vignettePeakAlpha = 0.48f;
        [SerializeField] private float vignetteFadeSpeed = 2.8f;
        [SerializeField] private float slowDownMultiplier = 0.55f;
        [SerializeField] private float slowDownDuration = 0.85f;
        [SerializeField] private float breathingCycleHz = 0.25f; // 4 detik per siklus napas

        private AudioClip _proceduralImpactClip;
        private float _vignetteAlpha;
        private float _slowDownTimer;
        private bool _panicModeActive;
        private float _breathingRingAlpha;

        private void Awake()
        {
            if (playerHealth == null) playerHealth = FindFirstObjectByType<PlayerHealth>();
            if (playerController == null) playerController = FindFirstObjectByType<PlayerController>();
            if (cameraDampener == null) cameraDampener = FindFirstObjectByType<CameraDampener>();

            if (impactAudioSource == null)
            {
                impactAudioSource = gameObject.AddComponent<AudioSource>();
                impactAudioSource.playOnAwake = false;
                impactAudioSource.spatialBlend = 0f;
            }

            _proceduralImpactClip = ProceduralAudioGenerator.CreateLowFrequencyImpactClip();

            if (damageVignetteImage != null)
            {
                Color c = new Color(0.85f, 0.08f, 0.08f, 0f);
                damageVignetteImage.color = c;
                damageVignetteImage.raycastTarget = false;
            }

            if (breathingRingImage != null)
            {
                Color toska = SimulationConfig.PrimaryTosca;
                toska.a = 0f;
                breathingRingImage.color = toska;
                breathingRingImage.raycastTarget = false;
            }
        }

        private void OnEnable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnDamaged += HandlePlayerDamaged;
            }
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnDamaged -= HandlePlayerDamaged;
            }
        }

        private void Update()
        {
            // Fade vignette merah
            if (damageVignetteImage != null)
            {
                _vignetteAlpha = Mathf.MoveTowards(_vignetteAlpha, 0f, vignetteFadeSpeed * Time.deltaTime);
                Color c = damageVignetteImage.color;
                c.a = _vignetteAlpha;
                damageVignetteImage.color = c;
            }

            // Perlambatan gerak sesaat setelah benturan
            if (_slowDownTimer > 0f)
            {
                _slowDownTimer -= Time.deltaTime;
                if (playerController != null)
                {
                    playerController.SetSpeedMultiplier(_slowDownTimer > 0f ? slowDownMultiplier : 1f);
                }
            }

            // Animasi breathing guide ring toska #00A99D saat Panik (fade halus)
            float targetAlpha = _panicModeActive ? 0.85f : 0f;
            _breathingRingAlpha = Mathf.MoveTowards(_breathingRingAlpha, targetAlpha, 1.8f * Time.deltaTime);

            if (breathingRingImage != null)
            {
                Color c = SimulationConfig.PrimaryTosca;
                c.a = _breathingRingAlpha;
                breathingRingImage.color = c;
            }

            if (breathingRingTransform != null && _breathingRingAlpha > 0.01f)
            {
                float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * 2f * Mathf.PI * breathingCycleHz);
                float scale = Mathf.Lerp(0.75f, 1.25f, wave);
                breathingRingTransform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        public void SetPanicRegulasi(bool active)
        {
            _panicModeActive = active;
            if (cameraDampener != null)
            {
                cameraDampener.SetPanicDampening(active);
            }
        }

        public void TriggerImpactFeedback()
        {
            _vignetteAlpha = vignettePeakAlpha;
            _slowDownTimer = slowDownDuration;

            if (playerController != null)
            {
                playerController.SetSpeedMultiplier(slowDownMultiplier);
            }

            if (impactAudioSource != null && _proceduralImpactClip != null)
            {
                impactAudioSource.PlayOneShot(_proceduralImpactClip);
            }
        }

        private void HandlePlayerDamaged(float _, Vector3 __)
        {
            TriggerImpactFeedback();
        }
    }
}


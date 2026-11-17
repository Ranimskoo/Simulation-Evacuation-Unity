using UnityEngine;
using IgnisEscape.Core;
using IgnisEscape.Feedback;
using IgnisEscape.Network;
using IgnisEscape.Objects;

namespace IgnisEscape.Adaptive
{
    /// <summary>
    /// Pengendali Intervensi Adaptif (Bagian 4 spesifikasi).
    /// Hanya berubah saat Unity menerima state resmi dari Python, atau lewat fallback
    /// manual tombol R (reset ke Tenang) dan H (nyalakan/matikan marka jalur).
    /// </summary>
    public class AdaptiveInterventionController : MonoBehaviour
    {
        public const string PanicGuidanceMessage =
            "Jangan lari terburu-buru, tarik napas dan perhatikan langkahmu";

        [Header("Referensi Sistem")]
        [SerializeField] private UdpTelemetryClient udpClient;
        [SerializeField] private PseudoHapticController pseudoHaptic;
        [SerializeField] private FootprintGuide footprintGuide;
        [SerializeField] private Transform directionArrowTransform;
        [SerializeField] private Renderer directionArrowRenderer;
        [SerializeField] private FireExitDoor nearestFireExitDoor;
        [SerializeField] private AudioSource voiceAudioSource;
        [SerializeField] private AudioClip panicVoiceClip; // Diisi manual di Inspector

        [Header("Parameter Visual")]
        [SerializeField] private float calmFootprintIntensity = 0.15f;
        [SerializeField] private float hesitationFootprintIntensity = 2.4f;
        [SerializeField] private float arrowFadeSpeed = 2.0f;

        public BehaviorState CurrentState { get; private set; } = BehaviorState.Tenang;
        public float CurrentConfidence { get; private set; } = 1.0f;
        public bool IsPythonConnected => udpClient != null && udpClient.IsConnected;
        public string ActiveSubtitle { get; private set; } = string.Empty;

        private AudioClip _proceduralFallbackClip;
        private float _arrowAlpha;
        private MaterialPropertyBlock _arrowMpb;
        private float _subtitleTimer;

        private void Awake()
        {
            if (udpClient == null) udpClient = FindFirstObjectByType<UdpTelemetryClient>();
            if (pseudoHaptic == null) pseudoHaptic = FindFirstObjectByType<PseudoHapticController>();
            if (footprintGuide == null) footprintGuide = FindFirstObjectByType<FootprintGuide>();
            if (nearestFireExitDoor == null) nearestFireExitDoor = FindFirstObjectByType<FireExitDoor>();

            if (voiceAudioSource == null)
            {
                voiceAudioSource = gameObject.AddComponent<AudioSource>();
                voiceAudioSource.playOnAwake = false;
                voiceAudioSource.spatialBlend = 0f;
            }

            _proceduralFallbackClip = ProceduralAudioGenerator.CreateCalmBreathCueClip();
            _arrowMpb = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (udpClient != null)
            {
                udpClient.OnStateReceived += HandleOfficialStateReceived;
            }
        }

        private void OnDisable()
        {
            if (udpClient != null)
            {
                udpClient.OnStateReceived -= HandleOfficialStateReceived;
            }
        }

        private void Update()
        {
            // Fallback manual: R = reset ke Tenang, H = toggle marka jalur (Bagian 2 & 4)
            if (InputHelper.WasKeyPressedThisFrame(KeyCode.R))
            {
                ApplyState(BehaviorState.Tenang, 1.0f);
                Debug.Log("[AdaptiveIntervention] Manual reset (R) -> Tenang");
            }

            if (InputHelper.WasKeyPressedThisFrame(KeyCode.H))
            {
                if (footprintGuide != null)
                {
                    footprintGuide.ToggleManualDirect();
                }
                Debug.Log("[AdaptiveIntervention] Manual toggle marka jalur (H)");
            }

            UpdateDirectionArrow();

            if (_subtitleTimer > 0f)
            {
                _subtitleTimer -= Time.deltaTime;
                if (_subtitleTimer <= 0f)
                {
                    ActiveSubtitle = string.Empty;
                }
            }
        }

        private void HandleOfficialStateReceived(BehaviorState officialState, float confidence)
        {
            ApplyState(officialState, confidence);
        }

        public void ApplyState(BehaviorState newState, float confidence)
        {
            bool stateChanged = newState != CurrentState;
            CurrentState = newState;
            CurrentConfidence = confidence;

            switch (CurrentState)
            {
                case BehaviorState.Panik:
                    // Regulasi diri: breathing ring toska + peredaman sensitivitas + audio/subtitle
                    if (pseudoHaptic != null) pseudoHaptic.SetPanicRegulasi(true);
                    if (footprintGuide != null) footprintGuide.SetTargetIntensity(calmFootprintIntensity);
                    if (stateChanged) TriggerPanicAudioAndSubtitle();
                    break;

                case BehaviorState.RaguRagu:
                    // Scaffolding: naikkan kecerahan marka jejak kaki + panah ke pintu darurat
                    if (pseudoHaptic != null) pseudoHaptic.SetPanicRegulasi(false);
                    if (footprintGuide != null) footprintGuide.SetTargetIntensity(hesitationFootprintIntensity);
                    break;

                case BehaviorState.Tenang:
                default:
                    // Fading: bantuan visual tambahan diredupkan bertahap
                    if (pseudoHaptic != null) pseudoHaptic.SetPanicRegulasi(false);
                    if (footprintGuide != null) footprintGuide.SetTargetIntensity(calmFootprintIntensity);
                    break;
            }
        }

        private void TriggerPanicAudioAndSubtitle()
        {
            ActiveSubtitle = PanicGuidanceMessage;
            _subtitleTimer = 4.5f;

            AudioClip clipToPlay = panicVoiceClip != null ? panicVoiceClip : _proceduralFallbackClip;
            if (voiceAudioSource != null && clipToPlay != null)
            {
                voiceAudioSource.PlayOneShot(clipToPlay);
            }
        }

        private void UpdateDirectionArrow()
        {
            if (directionArrowTransform == null || directionArrowRenderer == null) return;

            float targetAlpha = (CurrentState == BehaviorState.RaguRagu) ? 1f : 0f;
            _arrowAlpha = Mathf.MoveTowards(_arrowAlpha, targetAlpha, arrowFadeSpeed * Time.deltaTime);

            bool visible = _arrowAlpha > 0.01f;
            directionArrowRenderer.enabled = visible;

            if (visible)
            {
                FireExitDoor targetDoor = FireExitDoor.FindNearestDoor(directionArrowTransform.position);
                if (targetDoor == null) targetDoor = nearestFireExitDoor;

                if (targetDoor != null)
                {
                    Vector3 toDoor = targetDoor.transform.position - directionArrowTransform.position;
                    toDoor.y = 0f;
                    if (toDoor.sqrMagnitude > 0.001f)
                    {
                        directionArrowTransform.rotation = Quaternion.LookRotation(toDoor.normalized, Vector3.up);
                    }
                }

                Color col = SimulationConfig.PrimaryTosca;
                col.a = _arrowAlpha;
                directionArrowRenderer.GetPropertyBlock(_arrowMpb);
                _arrowMpb.SetColor("_Color", col);
                _arrowMpb.SetColor("_EmissionColor", SimulationConfig.PrimaryTosca * (1.8f * _arrowAlpha));
                directionArrowRenderer.SetPropertyBlock(_arrowMpb);
            }
        }
    }
}


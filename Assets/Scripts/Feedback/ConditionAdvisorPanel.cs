using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IgnisEscape.Adaptive;
using IgnisEscape.Core;
using IgnisEscape.Telemetry;

namespace IgnisEscape.Feedback
{
    /// <summary>
    /// Panel HUD di kanan atas layar yang menampilkan:
    /// 1. Kondisi perilaku pemain saat ini (Tenang / Ragu-Ragu / Panik) dari AI Python.
    /// 2. Penyebab spesifik berdasarkan fitur telemetri aktif (lari terus, geser mouse cepat,
    ///    menabrak rintangan, atau bolak-balik arah).
    /// 3. Solusi konkret yang harus dilakukan pemain untuk kembali Tenang.
    /// </summary>
    public class ConditionAdvisorPanel : MonoBehaviour
    {
        [Header("Referensi Sistem")]
        [SerializeField] private AdaptiveInterventionController adaptiveController;
        [SerializeField] private TelemetryExtractor telemetryExtractor;

        [Header("Komponen UI")]
        [SerializeField] private Image headerBarImage;
        [SerializeField] private Image panelBackgroundImage;
        [SerializeField] private TextMeshProUGUI conditionTitleText;
        [SerializeField] private TextMeshProUGUI bodyAdvisorText;

        private static readonly Color CalmHeaderColor = new Color(0f, 169f / 255f, 157f / 255f, 0.95f); // #00A99D
        private static readonly Color HesitateHeaderColor = new Color(0.95f, 0.62f, 0.12f, 0.95f);       // Amber
        private static readonly Color PanicHeaderColor = new Color(0.88f, 0.18f, 0.18f, 0.96f);          // Merah

        private BehaviorState _lastDisplayedState = BehaviorState.Tenang;
        private string _activeCause = "Kecepatan jalan normal & pandangan kamera stabil.";
        private string _activeSolution = "• Pertahankan jalan tenang (WASD tanpa Shift) dan ikuti rambu EXIT menuju pintu darurat.";

        private void Awake()
        {
            if (adaptiveController == null) adaptiveController = FindAnyObjectByType<AdaptiveInterventionController>();
            if (telemetryExtractor == null) telemetryExtractor = FindAnyObjectByType<TelemetryExtractor>();
            ConfigureTextSizing();
        }

        private void ConfigureTextSizing()
        {
            if (conditionTitleText != null)
            {
                conditionTitleText.enableAutoSizing = true;
                conditionTitleText.fontSizeMin = 18f;
                conditionTitleText.fontSizeMax = 26f;
                conditionTitleText.alignment = TextAlignmentOptions.MidlineLeft;
                conditionTitleText.textWrappingMode = TextWrappingModes.NoWrap;
                conditionTitleText.overflowMode = TextOverflowModes.Ellipsis;
                RectTransform rt = conditionTitleText.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(14f, 4f);
                rt.offsetMax = new Vector2(-14f, -4f);
            }

            if (bodyAdvisorText != null)
            {
                bodyAdvisorText.enableAutoSizing = true;
                bodyAdvisorText.fontSizeMin = 15f;
                bodyAdvisorText.fontSizeMax = 22f;
                bodyAdvisorText.lineSpacing = 4f;
                bodyAdvisorText.alignment = TextAlignmentOptions.TopLeft;
                bodyAdvisorText.textWrappingMode = TextWrappingModes.Normal;
                bodyAdvisorText.overflowMode = TextOverflowModes.Truncate;
                RectTransform rt = bodyAdvisorText.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(10f, 8f);
                rt.offsetMax = new Vector2(-10f, -8f);
            }
        }

        private void Update()
        {
            BehaviorState state = adaptiveController != null ? adaptiveController.CurrentState : BehaviorState.Tenang;
            float conf = adaptiveController != null ? adaptiveController.CurrentConfidence : 1f;
            bool connected = adaptiveController != null && adaptiveController.IsPythonConnected;

            TelemetrySnapshot snap = telemetryExtractor != null ? telemetryExtractor.LatestSnapshot : default;
            UpdateDiagnosisText(state, snap);
            UpdateVisuals(state, conf, connected, snap);
        }

        private void UpdateDiagnosisText(BehaviorState state, TelemetrySnapshot snap)
        {
            float v = snap.avgSpeed;
            float omega = snap.cameraJitter;
            int c = snap.collisionCount;
            int r = snap.turnReversals;

            bool isSprinting = v > 2.05f;
            bool isHighJitter = omega >= 110f;
            bool isModerateJitter = omega >= 45f;
            bool hasCollisions = c >= 1;
            bool hasReversals = r >= 2;

            if (state == BehaviorState.Tenang)
            {
                _activeCause = c == 1
                    ? "Menyentuh 1 objek ringan, namun kecepatan & kamera tetap tenang."
                    : "Gerakan jalan terkendali & pandangan kamera stabil.";
                _activeSolution =
                    "• Pertahankan jalan normal (WASD tanpa tahan Shift).\n" +
                    "• Hindari rintangan dan ikuti rambu EXIT hijau menuju pintu darurat.";
            }
            else if (state == BehaviorState.RaguRagu)
            {
                if (hasCollisions && !isSprinting)
                {
                    _activeCause = $"Menabrak rintangan ({c}x) tanpa berlari (belum dikategorikan Panik).";
                    _activeSolution =
                        "• Perhatikan rintangan di depan! Tekan Spasi untuk melompat atau ambil jalur memutar.\n" +
                        "• Ikuti jejak kaki toska menyala di lantai tanpa perlu berlari.";
                }
                else if (isSprinting)
                {
                    _activeCause = $"Mulai berlari cepat ({v:F1} m/s) — berisiko memicu kondisi Panik!";
                    _activeSolution =
                        "• Segera LEPAS tombol Shift dan berjalan biasa (WASD).\n" +
                        "• Jaga pandangan tetap tenang agar layar tidak semakin buram.";
                }
                else if (isModerateJitter)
                {
                    _activeCause = $"Menggeser mouse/kamera cukup cepat ({omega:F0}°/s) karena ragu arah.";
                    _activeSolution =
                        "• Perlambat gerakan mouse dan fokuskan pandangan ke depan.\n" +
                        "• Ikuti panah toska di dekat kaki menuju Fire Exit terdekat.";
                }
                else if (hasReversals || v < 0.65f)
                {
                    _activeCause = "Berhenti / bolak-balik arah karena bingung memilih rute evakuasi.";
                    _activeSolution =
                        "• Ikuti panah toska di bawah kaki & jejak kaki menyala di lantai.\n" +
                        "• Tekan H jika ingin menyalakan/mematikan panduan jalur.";
                }
            }
            else if (state == BehaviorState.Panik)
            {
                if (isSprinting && (hasCollisions || isHighJitter || hasReversals))
                {
                    _activeCause = "Kombinasi berlari terus + kamera liar / menabrak rintangan beruntun!";
                    _activeSolution =
                        "1) LEPAS tombol Shift sekarang (berhenti berlari) & tenangkan mouse!\n" +
                        "2) Hindari/lompati rintangan (Spasi) dan ikuti cincin napas toska.\n" +
                        "3) Berhenti diam 2 detik dengan kamera stabil (atau tekan R) agar layar normal.";
                }
                else if (isSprinting)
                {
                    _activeCause = $"Berlari terus-menerus tanpa henti (v = {v:F1} m/s)!";
                    _activeSolution =
                        "1) LEPAS tombol Shift! Jangan berlari terus-menerus.\n" +
                        "2) Berjalan pelan atau berhenti 2 detik mengikuti ritme cincin napas toska di tengah layar (atau tekan R) untuk memulihkan fokus.";
                }
                else if (isHighJitter)
                {
                    _activeCause = $"Menggeser mouse/kamera terlalu cepat terus-menerus ({omega:F0}°/s)!";
                    _activeSolution =
                        "1) Hentikan gerakan mouse yang liar, tahan pandangan lurus.\n" +
                        "2) Diam sejenak selama 2 detik mengikuti cincin napas toska (atau tekan R) hingga buram & getaran hilang.";
                }
                else if (hasCollisions)
                {
                    _activeCause = $"Menabrak rintangan beruntun ({c}x) dalam kondisi tertekan!";
                    _activeSolution =
                        "1) Mundur sedikit dari rintangan, jangan tahan Shift.\n" +
                        "2) Gunakan Spasi untuk melompat atau teleport Waypoint hijau.\n" +
                        "3) Diam & tenangkan kamera 2 detik untuk menjernihkan pandangan.";
                }
                else if (_lastDisplayedState != BehaviorState.Panik)
                {
                    _activeCause = "Akumulasi kepanikan (lari terus / kamera cepat / benturan beruntun)!";
                    _activeSolution =
                        "1) Lepas tombol Shift (jangan lari) & tenangkan gerakan mouse.\n" +
                        "2) Berhenti sejenak selama 2 detik mengikuti cincin napas toska (atau tekan R) agar penglihatan kembali normal.";
                }
            }

            _lastDisplayedState = state;
        }

        private void UpdateVisuals(BehaviorState state, float conf, bool connected, TelemetrySnapshot snap)
        {
            if (headerBarImage != null)
            {
                switch (state)
                {
                    case BehaviorState.Tenang:
                        headerBarImage.color = CalmHeaderColor;
                        break;
                    case BehaviorState.RaguRagu:
                        headerBarImage.color = HesitateHeaderColor;
                        break;
                    case BehaviorState.Panik:
                        float pulse = 0.82f + 0.18f * Mathf.Sin(Time.time * 7f);
                        headerBarImage.color = new Color(PanicHeaderColor.r * pulse, PanicHeaderColor.g, PanicHeaderColor.b, 0.96f);
                        break;
                }
            }

            if (panelBackgroundImage != null)
            {
                panelBackgroundImage.color = state == BehaviorState.Panik
                    ? new Color(0.14f, 0.04f, 0.05f, 0.88f)
                    : new Color(0.04f, 0.08f, 0.10f, 0.84f);
            }

            if (conditionTitleText != null)
            {
                string stateLabel = state == BehaviorState.Tenang
                    ? "TENANG"
                    : (state == BehaviorState.RaguRagu ? "RAGU-RAGU" : "PANIK!");
                string modeTag = connected ? $"AI {conf * 100f:F0}%" : "MANUAL";
                conditionTitleText.text = $"KONDISI: {stateLabel}  [{modeTag}]";
            }

            if (bodyAdvisorText != null)
            {
                bodyAdvisorText.text =
                    $"<b>Penyebab:</b> {_activeCause}\n" +
                    $"<color=#7FFFD4><b>Solusi:</b></color>\n{_activeSolution}\n" +
                    $"<size=85%><color=#B0BEC5>[v={snap.avgSpeed:F1} m/s | mouse={snap.cameraJitter:F0}°/s | tabrak={snap.collisionCount} | balik={snap.turnReversals}]</color></size>";
            }
        }
    }
}


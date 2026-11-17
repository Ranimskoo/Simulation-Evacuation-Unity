using TMPro;
using UnityEngine;
using IgnisEscape.Adaptive;
using IgnisEscape.Core;
using IgnisEscape.Player;

namespace IgnisEscape.UI
{
    /// <summary>
    /// Panel status kesiapan evakuasi + indikator kondisi yang bersifat BODY-LOCKED
    /// pada pergelangan tangan kiri virtual (wrist monitor, BUKAN head-locked) (Bagian 8 & 4).
    /// </summary>
    public class WristStatusMonitor : MonoBehaviour
    {
        [Header("Referensi Data")]
        [SerializeField] private AdaptiveInterventionController adaptiveController;
        [SerializeField] private PlayerHealth playerHealth;

        [Header("Komponen Teks TMP")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI connectionText;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private TextMeshProUGUI subtitleText;

        private void Awake()
        {
            if (adaptiveController == null)
            {
                adaptiveController = FindFirstObjectByType<AdaptiveInterventionController>();
            }
            if (playerHealth == null)
            {
                playerHealth = FindFirstObjectByType<PlayerHealth>();
            }
        }

        private void Update()
        {
            if (adaptiveController != null)
            {
                if (statusText != null)
                {
                    var gm = FindFirstObjectByType<GameManager>();
                    string resultBanner = (gm != null && !string.IsNullOrEmpty(gm.MatchResultMessage))
                        ? $"\n<b>{gm.MatchResultMessage}</b>"
                        : string.Empty;

                    statusText.text =
                        $"STATUS: {adaptiveController.CurrentState.ToProtocolString()}\n" +
                        $"CONFIDENCE: {adaptiveController.CurrentConfidence:P0}{resultBanner}";
                }

                if (connectionText != null)
                {
                    bool connected = adaptiveController.IsPythonConnected;
                    connectionText.text = connected
                        ? "AI: Terhubung (UDP)"
                        : "Python terputus (Manual R/H)";
                    connectionText.color = connected
                        ? SimulationConfig.PrimaryTosca
                        : new Color(1f, 0.35f, 0.35f, 1f);
                }

                if (subtitleText != null)
                {
                    subtitleText.text = adaptiveController.ActiveSubtitle;
                }
            }

            if (playerHealth != null && healthText != null)
            {
                healthText.text = $"KETAHANAN: {playerHealth.CurrentHealth:F0}/{playerHealth.MaxHealth:F0}";
            }
        }
    }
}


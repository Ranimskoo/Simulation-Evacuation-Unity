using System.Collections;
using UnityEngine;
using IgnisEscape.Environment;
using IgnisEscape.Objects;
using IgnisEscape.Player;

namespace IgnisEscape.Core
{
    /// <summary>
    /// Mengelola alur permainan minimal (Bagian 5A):
    /// - Spawn di titik awal koridor, health awal 100.
    /// - Menang: melewati trigger di balik Fire Exit Door yang sudah Opened.
    /// - Kalah: health <= 0.
    /// - Menampilkan hasil singkat dan memulai ulang simulasi (ObstacleRandomizer diacak ulang).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private PlayerController playerController;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private ObstacleRandomizer obstacleRandomizer;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private float restartDelaySeconds = 2.5f;

        public string MatchResultMessage { get; private set; } = string.Empty;
        public bool IsRoundEnded { get; private set; }

        private void Awake()
        {
            if (playerController == null) playerController = FindFirstObjectByType<PlayerController>();
            if (playerHealth == null) playerHealth = FindFirstObjectByType<PlayerHealth>();
            if (obstacleRandomizer == null) obstacleRandomizer = FindFirstObjectByType<ObstacleRandomizer>();
        }

        private void OnEnable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnPlayerDepleted += HandlePlayerLost;
            }
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnPlayerDepleted -= HandlePlayerLost;
            }
        }

        public void NotifyPlayerEscaped()
        {
            if (IsRoundEnded) return;
            IsRoundEnded = true;
            MatchResultMessage = "EVAKUASI BERHASIL!";
            Debug.Log("[GameManager] Menang: Pemain berhasil keluar melalui Fire Exit Door.");
            StartCoroutine(RestartAfterDelay());
        }

        private void HandlePlayerLost()
        {
            if (IsRoundEnded) return;
            IsRoundEnded = true;
            MatchResultMessage = "EVAKUASI GAGAL (Ketahanan Habis)";
            Debug.Log("[GameManager] Kalah: Ketahanan pemain <= 0.");
            StartCoroutine(RestartAfterDelay());
        }

        private IEnumerator RestartAfterDelay()
        {
            yield return new WaitForSeconds(restartDelaySeconds);
            RestartSimulation();
        }

        public void RestartSimulation()
        {
            IsRoundEnded = false;
            MatchResultMessage = string.Empty;

            if (playerHealth != null) playerHealth.ResetHealth();

            Vector3 targetPos = spawnPoint != null ? spawnPoint.position : new Vector3(0f, 0.05f, 1.2f);
            if (playerController != null) playerController.TeleportTo(targetPos);

            if (obstacleRandomizer != null) obstacleRandomizer.RandomizePlacement();

            var doors = FireExitDoor.AllDoors;
            for (int i = 0; i < doors.Count; i++)
            {
                if (doors[i] != null) doors[i].ResetDoor();
            }
        }
    }
}


using TMPro;
using UnityEngine;
using IgnisEscape.Objects;

namespace IgnisEscape.UI
{
    /// <summary>
    /// Rambu jalur + persentase jarak ke pintu keluar yang bersifat WORLD-LOCKED
    /// di dinding koridor setiap interval 5 meter, selalu merujuk ke Fire Exit Door
    /// terdekat dari posisi pemain saat itu (Bagian 6 & 8).
    /// </summary>
    public class CorridorSignage : MonoBehaviour
    {
        [SerializeField] private TextMeshPro labelText;
        [SerializeField] private float maxRouteDistanceMeters = 75f;

        private Transform _playerTransform;
        private float _refreshTimer;

        private void Awake()
        {
            if (labelText == null)
            {
                labelText = GetComponentInChildren<TextMeshPro>();
            }
        }

        private void Start()
        {
            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null) _playerTransform = playerObj.transform;
            RefreshSignage();
        }

        private void Update()
        {
            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer <= 0f)
            {
                _refreshTimer = 0.25f;
                RefreshSignage();
            }
        }

        public void Configure(float maxDistance)
        {
            maxRouteDistanceMeters = Mathf.Max(1f, maxDistance);
            RefreshSignage();
        }

        public void RefreshSignage()
        {
            if (labelText == null) return;

            Vector3 refPos = _playerTransform != null ? _playerTransform.position : transform.position;
            FireExitDoor nearestDoor = FireExitDoor.FindNearestDoor(refPos);
            if (nearestDoor == null) return;

            float dist = Vector3.Distance(refPos, nearestDoor.transform.position);
            float progressPct = Mathf.Clamp01(1f - (dist / maxRouteDistanceMeters)) * 100f;
            labelText.text = $"{nearestDoor.DoorId} ->\n{dist:F1} m ({progressPct:F0}%)";
        }
    }
}

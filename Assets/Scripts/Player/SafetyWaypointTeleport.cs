using UnityEngine;
using IgnisEscape.Core;

namespace IgnisEscape.Player
{
    /// <summary>
    /// Mode XR & Desktop: Teleportasi Antar-Titik Pijakan Aman (Safety Waypoint Teleportation)
    /// agar proyek dapat dijalankan tanpa headset maupun dengan XR rig (Bagian 1b).
    /// </summary>
    public class SafetyWaypointTeleport : MonoBehaviour
    {
        [SerializeField] private Transform rayOrigin;
        [SerializeField] private float maxTeleportDistance = 12f;
        [SerializeField] private LayerMask waypointLayer = ~0;
        [SerializeField] private PlayerController playerController;

        private SafetyWaypoint _hoveredWaypoint;

        private void Awake()
        {
            if (rayOrigin == null && Camera.main != null)
            {
                rayOrigin = Camera.main.transform;
            }
            if (playerController == null)
            {
                playerController = GetComponent<PlayerController>();
            }
        }

        private void Update()
        {
            if (rayOrigin == null) return;

            SafetyWaypoint hitWaypoint = null;
            if (Physics.Raycast(rayOrigin.position, rayOrigin.forward, out RaycastHit hit, maxTeleportDistance, waypointLayer, QueryTriggerInteraction.Collide))
            {
                hitWaypoint = hit.collider.GetComponentInParent<SafetyWaypoint>();
            }

            if (_hoveredWaypoint != hitWaypoint)
            {
                if (_hoveredWaypoint != null && !_hoveredWaypoint.IsInitialWaypoint)
                {
                    _hoveredWaypoint.SetHighlighted(false);
                }
                _hoveredWaypoint = hitWaypoint;
                if (_hoveredWaypoint != null)
                {
                    _hoveredWaypoint.SetHighlighted(true);
                }
            }

            // Tekan E atau klik kanan/trigger untuk teleportasi ke titik pijakan aman
            if (_hoveredWaypoint != null &&
                (InputHelper.WasKeyPressedThisFrame(KeyCode.E) || InputHelper.WasLeftMousePressedThisFrame()))
            {
                TeleportToWaypoint(_hoveredWaypoint);
            }
        }

        public void TeleportToWaypoint(SafetyWaypoint waypoint)
        {
            if (waypoint == null) return;

            if (playerController != null)
            {
                playerController.TeleportTo(waypoint.TeleportPosition);
            }
            else
            {
                transform.position = waypoint.TeleportPosition;
            }
        }
    }
}


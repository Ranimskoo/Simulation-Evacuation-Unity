using UnityEngine;
using IgnisEscape.Core;

namespace IgnisEscape.Player
{
    /// <summary>
    /// Titik pijakan aman (Safety Waypoint) untuk mode Teleportasi XR maupun desktop
    /// (Bagian 1b & Bagian 4 awal permainan).
    /// </summary>
    public class SafetyWaypoint : MonoBehaviour
    {
        [SerializeField] private Renderer ringRenderer;
        [SerializeField] private bool isInitialWaypoint;

        private MaterialPropertyBlock _mpb;

        public bool IsInitialWaypoint => isInitialWaypoint;
        public Vector3 TeleportPosition => transform.position + Vector3.up * 0.05f;

        private void Awake()
        {
            if (ringRenderer == null)
            {
                ringRenderer = GetComponentInChildren<Renderer>();
            }
            _mpb = new MaterialPropertyBlock();
            SetHighlighted(isInitialWaypoint);
        }

        public void SetHighlighted(bool highlighted)
        {
            if (ringRenderer == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            Color col = SimulationConfig.PrimaryTosca;
            float emission = highlighted ? 2.2f : 0.6f;

            ringRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_Color", col);
            _mpb.SetColor("_EmissionColor", col * emission);
            ringRenderer.SetPropertyBlock(_mpb);
        }
    }
}


using System.Collections.Generic;
using UnityEngine;
using IgnisEscape.Core;

namespace IgnisEscape.Adaptive
{
    /// <summary>
    /// Mengelola marka jejak kaki toska (#00A99D) di lantai koridor:
    /// - Awal permainan: menyala berurutan menuju titik pijakan pertama (Bagian 4).
    /// - Ragu-Ragu: kecerahan dinaikkan (scaffolding).
    /// - Tenang: diredupkan bertahap (fading halus).
    /// - Tombol H: nyalakan/matikan marka secara langsung.
    /// </summary>
    public class FootprintGuide : MonoBehaviour
    {
        [SerializeField] private List<Renderer> footprintRenderers = new List<Renderer>();
        [SerializeField] private float startupSequenceDuration = 6f;
        [SerializeField] private float fadeSpeed = 1.5f;

        private MaterialPropertyBlock _mpb;
        private float _currentIntensity = 0.3f;
        private float _targetIntensity = 0.3f;
        private bool _forceManualVisible;
        private bool _manualToggleActive;
        private float _startupTimer;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (footprintRenderers.Count == 0)
            {
                foreach (var r in GetComponentsInChildren<Renderer>())
                {
                    footprintRenderers.Add(r);
                }
            }
        }

        private void Update()
        {
            _startupTimer += Time.deltaTime;
            _currentIntensity = Mathf.MoveTowards(_currentIntensity, _targetIntensity, fadeSpeed * Time.deltaTime);

            int count = footprintRenderers.Count;
            for (int i = 0; i < count; i++)
            {
                Renderer r = footprintRenderers[i];
                if (r == null) continue;

                float intensity = _currentIntensity;

                // Awal permainan: jejak kaki toska menyala berurutan tanpa instruksi lisan
                if (!_manualToggleActive && _startupTimer <= startupSequenceDuration && count > 0)
                {
                    float phase = (_startupTimer * 2.2f - i * 0.35f);
                    float pulse = Mathf.Clamp01(Mathf.Sin(phase));
                    intensity = Mathf.Max(intensity, 0.25f + pulse * 2.0f);
                }

                if (_manualToggleActive)
                {
                    intensity = _forceManualVisible ? 2.2f : 0f;
                }

                r.GetPropertyBlock(_mpb);
                Color col = SimulationConfig.PrimaryTosca;
                _mpb.SetColor("_Color", col);
                _mpb.SetColor("_EmissionColor", col * intensity);
                r.SetPropertyBlock(_mpb);
            }
        }

        public void SetTargetIntensity(float intensity)
        {
            _manualToggleActive = false;
            _targetIntensity = Mathf.Max(0f, intensity);
        }

        public void ToggleManualDirect()
        {
            _manualToggleActive = true;
            _forceManualVisible = !_forceManualVisible;
        }

        public void RegisterFootprint(Renderer r)
        {
            if (r != null && !footprintRenderers.Contains(r))
            {
                footprintRenderers.Add(r);
            }
        }
    }
}


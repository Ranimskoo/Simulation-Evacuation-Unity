using UnityEngine;

namespace IgnisEscape.Player
{
    /// <summary>
    /// Mengatur peredaman sensitivitas rotasi kamera (motion resistance) saat
    /// intervensi Panik aktif (Bagian 4 & 5A).
    /// </summary>
    public class CameraDampener : MonoBehaviour
    {
        [SerializeField] private float panicSensitivityMultiplier = 0.45f;
        [SerializeField] private float transitionSpeed = 5f;

        private float _targetMultiplier = 1f;
        public float CurrentMultiplier { get; private set; } = 1f;

        public void SetPanicDampening(bool active)
        {
            _targetMultiplier = active ? Mathf.Clamp(panicSensitivityMultiplier, 0.1f, 1f) : 1f;
        }

        private void Update()
        {
            CurrentMultiplier = Mathf.MoveTowards(
                CurrentMultiplier,
                _targetMultiplier,
                transitionSpeed * Time.deltaTime
            );
        }
    }
}


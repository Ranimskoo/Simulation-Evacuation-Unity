using UnityEngine;

namespace IgnisEscape.UI
{
    /// <summary>
    /// Memvalidasi ukuran tombol spasial VR/AR berdasarkan sudut pandang (Bagian 8):
    /// theta = 2 * arctan(w / (2d)) dengan w = 0,035 m dan d = 0,8 m (~2,51 derajat),
    /// serta mencatat peringatan apabila theta < 2,0 derajat.
    /// </summary>
    public class SpatialButtonValidator : MonoBehaviour
    {
        [SerializeField] private float buttonWidthMeters = 0.035f;
        [SerializeField] private float interactionDistanceMeters = 0.8f;
        [SerializeField] private float minimumAngleDegrees = 2.0f;

        public float ComputedAngleDegrees { get; private set; }
        public bool IsValid { get; private set; }

        private void Awake()
        {
            Validate();
        }

        public float Validate()
        {
            if (interactionDistanceMeters <= 0f)
            {
                Debug.LogWarning($"[SpatialButtonValidator] Jarak interaksi tidak valid pada {name}.");
                IsValid = false;
                return 0f;
            }

            float thetaRad = 2f * Mathf.Atan(buttonWidthMeters / (2f * interactionDistanceMeters));
            ComputedAngleDegrees = thetaRad * Mathf.Rad2Deg;
            IsValid = ComputedAngleDegrees >= minimumAngleDegrees;

            if (!IsValid)
            {
                Debug.LogWarning(
                    $"[SpatialButtonValidator] Tombol '{name}' terlalu kecil: theta = {ComputedAngleDegrees:F2} deg (< {minimumAngleDegrees:F1} deg)."
                );
            }

            return ComputedAngleDegrees;
        }
    }
}


using UnityEngine;

namespace IgnisEscape.Player
{
    /// <summary>
    /// Animasi prosedural sederhana tanpa Animator untuk tubuh pemain (Bagian 5A):
    /// ayunan lengan dan kaki berbasis sinus dengan amplitudo mengikuti kecepatan jalan.
    /// </summary>
    public class PlayerVisualAnimator : MonoBehaviour
    {
        [SerializeField] private PlayerController playerController;
        [SerializeField] private Transform leftArmPivot;
        [SerializeField] private Transform rightArmPivot;
        [SerializeField] private Transform leftLegPivot;
        [SerializeField] private Transform rightLegPivot;

        [Header("Parameter Ayunan")]
        [SerializeField] private float swingFrequency = 7.5f;
        [SerializeField] private float maxSwingAngle = 28f;
        [SerializeField] private float referenceMaxSpeed = 2.6f;

        private float _phase;

        private void Awake()
        {
            if (playerController == null)
            {
                playerController = GetComponentInParent<PlayerController>();
            }
        }

        private void Update()
        {
            float speed = playerController != null ? playerController.CurrentHorizontalSpeed : 0f;
            float normSpeed = Mathf.Clamp01(speed / Mathf.Max(0.1f, referenceMaxSpeed));

            if (normSpeed > 0.03f)
            {
                _phase += Time.deltaTime * swingFrequency * (0.6f + normSpeed * 0.8f);
            }
            else
            {
                _phase = Mathf.Lerp(_phase, Mathf.Round(_phase / Mathf.PI) * Mathf.PI, Time.deltaTime * 8f);
            }

            float angle = Mathf.Sin(_phase) * maxSwingAngle * normSpeed;

            if (leftArmPivot != null) leftArmPivot.localRotation = Quaternion.Euler(angle, 0f, 0f);
            if (rightArmPivot != null) rightArmPivot.localRotation = Quaternion.Euler(-angle, 0f, 0f);
            if (leftLegPivot != null) leftLegPivot.localRotation = Quaternion.Euler(-angle, 0f, 0f);
            if (rightLegPivot != null) rightLegPivot.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }
    }
}


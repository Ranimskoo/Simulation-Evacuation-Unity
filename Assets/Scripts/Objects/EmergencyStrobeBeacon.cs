using UnityEngine;

namespace IgnisEscape.Objects
{
    /// <summary>
    /// Lampu suar darurat (emergency strobe beacon) yang memutar Animation Clip
    /// looping dengan kurva STEPPED serta memiliki LODGroup 3 tingkat (Bagian 5 & 7D).
    /// </summary>
    public class EmergencyStrobeBeacon : MonoBehaviour
    {
        [SerializeField] private Animator strobeAnimator;
        [SerializeField] private Light strobeLight;

        public Light StrobeLight => strobeLight;

        private void Awake()
        {
            if (strobeAnimator == null)
            {
                strobeAnimator = GetComponent<Animator>();
            }
            if (strobeLight == null)
            {
                strobeLight = GetComponentInChildren<Light>();
            }
        }
    }
}


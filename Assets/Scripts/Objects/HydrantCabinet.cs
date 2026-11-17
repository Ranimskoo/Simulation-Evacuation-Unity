using UnityEngine;

namespace IgnisEscape.Objects
{
    /// <summary>
    /// Unit Kotak Hidran rakitan primitif (lemari dinding, gulungan selang, nozzle,
    /// katup putar) dengan LODGroup 3 tingkat (Bagian 5 & 7D).
    /// </summary>
    public class HydrantCabinet : MonoBehaviour
    {
        [SerializeField] private LODGroup lodGroup;

        public LODGroup LodGroup => lodGroup;

        private void Awake()
        {
            if (lodGroup == null)
            {
                lodGroup = GetComponent<LODGroup>();
            }
        }
    }
}


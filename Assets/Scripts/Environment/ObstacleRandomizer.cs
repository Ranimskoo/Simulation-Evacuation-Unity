using System;
using System.Collections.Generic;
using UnityEngine;

namespace IgnisEscape.Environment
{
    /// <summary>
    /// Menempatkan meja rintangan (ber-Collider) di SALAH SATU kandidat titik pada
    /// SETIAP zona koridor (minimal 8 titik kandidat total) secara acak setiap
    /// simulasi dimulai sehingga rute tidak dapat dihafal (Bagian 6 & 7C).
    /// </summary>
    public class ObstacleRandomizer : MonoBehaviour
    {
        [Serializable]
        public class CorridorObstacleZone
        {
            public string zoneName;
            public GameObject deskObstacleInstance;
            public List<Transform> candidatePoints = new List<Transform>();
            [HideInInspector] public int chosenIndex = -1;
        }

        [SerializeField] private List<CorridorObstacleZone> corridorZones = new List<CorridorObstacleZone>();

        public IReadOnlyList<CorridorObstacleZone> CorridorZones => corridorZones;

        private void Start()
        {
            RandomizePlacement();
        }

        public void RandomizePlacement()
        {
            if (corridorZones == null || corridorZones.Count == 0)
            {
                Debug.LogWarning("[ObstacleRandomizer] Zona kandidat rintangan belum diatur.");
                return;
            }

            for (int z = 0; z < corridorZones.Count; z++)
            {
                CorridorObstacleZone zone = corridorZones[z];
                if (zone == null || zone.deskObstacleInstance == null ||
                    zone.candidatePoints == null || zone.candidatePoints.Count == 0)
                {
                    continue;
                }

                zone.chosenIndex = UnityEngine.Random.Range(0, zone.candidatePoints.Count);
                Transform chosen = zone.candidatePoints[zone.chosenIndex];
                if (chosen != null)
                {
                    zone.deskObstacleInstance.transform.SetPositionAndRotation(chosen.position, chosen.rotation);
                    zone.deskObstacleInstance.SetActive(true);
                }
            }
        }
    }
}

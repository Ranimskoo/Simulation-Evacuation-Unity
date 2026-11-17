using System.Collections.Generic;
using UnityEngine;

namespace IgnisEscape.Environment
{
    /// <summary>
    /// Menjatuhkan potongan plafon gipsum dan balok (Rigidbody + BoxCollider) secara
    /// periodik pada beberapa BoundingBox (satu per segmen koridor dan satu di lobi)
    /// dengan batas puing aktif per segmen dan total demi performa (Bagian 6 & 7C).
    /// </summary>
    public class DebrisSpawner : MonoBehaviour
    {
        [Header("Daftar BoundingBox Langit-Langit (Lobi + Segmen Koridor)")]
        [SerializeField] private List<BoxCollider> ceilingBoundingBoxes = new List<BoxCollider>();

        [Header("Parameter Spawn & Batas Performa")]
        [SerializeField] private float spawnIntervalSeconds = 3.5f;
        [SerializeField] private int maxDebrisPerSegment = 3;
        [SerializeField] private int maxTotalActiveDebris = 12;
        [SerializeField] private Material gypsumMaterial;
        [SerializeField] private Material beamMaterial;

        private readonly Queue<GameObject> _totalActiveDebris = new Queue<GameObject>();
        private readonly Dictionary<int, Queue<GameObject>> _segmentQueues = new Dictionary<int, Queue<GameObject>>();
        private float _timer;

        private void Awake()
        {
            if (ceilingBoundingBoxes.Count == 0)
            {
                foreach (var box in GetComponentsInChildren<BoxCollider>())
                {
                    box.isTrigger = true;
                    ceilingBoundingBoxes.Add(box);
                }
            }
        }

        private void Update()
        {
            if (ceilingBoundingBoxes == null || ceilingBoundingBoxes.Count == 0) return;

            _timer += Time.deltaTime;
            if (_timer >= spawnIntervalSeconds)
            {
                _timer = 0f;
                SpawnRandomDebris();
            }
        }

        public void SpawnRandomDebris()
        {
            if (ceilingBoundingBoxes.Count == 0) return;

            int segIndex = Random.Range(0, ceilingBoundingBoxes.Count);
            BoxCollider chosenBox = ceilingBoundingBoxes[segIndex];
            if (chosenBox == null) return;

            if (!_segmentQueues.TryGetValue(segIndex, out var segQueue))
            {
                segQueue = new Queue<GameObject>();
                _segmentQueues[segIndex] = segQueue;
            }

            // Batasi puing aktif per segmen
            while (segQueue.Count >= maxDebrisPerSegment)
            {
                GameObject oldestSeg = segQueue.Dequeue();
                if (oldestSeg != null) Destroy(oldestSeg);
            }

            // Batasi puing aktif total seluruh gedung (maks ~6k segitiga)
            while (_totalActiveDebris.Count >= maxTotalActiveDebris)
            {
                GameObject oldestTotal = _totalActiveDebris.Dequeue();
                if (oldestTotal != null) Destroy(oldestTotal);
            }

            Bounds b = chosenBox.bounds;
            Vector3 spawnPos = new Vector3(
                Random.Range(b.min.x, b.max.x),
                Random.Range(b.min.y, b.max.y),
                Random.Range(b.min.z, b.max.z)
            );

            bool isBeam = Random.value > 0.55f;
            GameObject debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
            debris.name = isBeam ? "Debris_Beam" : "Debris_Gypsum";
            debris.tag = "Debris";
            debris.transform.position = spawnPos;
            debris.transform.rotation = Random.rotation;
            debris.transform.localScale = isBeam
                ? new Vector3(0.18f, 0.14f, 0.85f)
                : new Vector3(0.45f, 0.06f, 0.45f);

            var renderer = debris.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = isBeam ? beamMaterial : gypsumMaterial;
                if (mat != null) renderer.sharedMaterial = mat;
            }

            var rb = debris.AddComponent<Rigidbody>();
            rb.mass = isBeam ? 8f : 3f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            debris.AddComponent<FallingDebris>();
            segQueue.Enqueue(debris);
            _totalActiveDebris.Enqueue(debris);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace IgnisEscape.Optimization
{
    /// <summary>
    /// Menggabungkan mesh dinding modular per zona untuk mengurangi Draw Calls serta
    /// menonaktifkan sub-panel detail redundan saat mode optimasi aktif (Bagian 6 & 9).
    /// Menyediakan toggle debug sebelum/sesudah optimasi untuk presentasi.
    /// </summary>
    public class MeshCombiner : MonoBehaviour
    {
        private static readonly List<MeshCombiner> AllCombiners = new List<MeshCombiner>();

        [SerializeField] private bool combineOnStart = true;
        [SerializeField] private GameObject unoptimizedExtraDetailRoot;

        private readonly List<MeshFilter> _sourceFilters = new List<MeshFilter>();
        private GameObject _combinedObject;
        private bool _isOptimized;

        public bool IsOptimized => _isOptimized;
        public static IReadOnlyList<MeshCombiner> Instances => AllCombiners;

        public static void ToggleAllInScene()
        {
            bool targetState = AllCombiners.Count == 0 || !AllCombiners[0].IsOptimized;
            for (int i = 0; i < AllCombiners.Count; i++)
            {
                if (AllCombiners[i] != null)
                {
                    AllCombiners[i].SetOptimized(targetState);
                }
            }
        }

        private void OnEnable()
        {
            if (!AllCombiners.Contains(this)) AllCombiners.Add(this);
        }

        private void OnDisable()
        {
            AllCombiners.Remove(this);
        }

        private void Awake()
        {
            CacheSourceMeshes();
        }

        private void Start()
        {
            if (combineOnStart)
            {
                SetOptimized(true);
            }
        }

        private void CacheSourceMeshes()
        {
            _sourceFilters.Clear();
            foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.gameObject == _combinedObject) continue;
                if (unoptimizedExtraDetailRoot != null &&
                    mf.transform.IsChildOf(unoptimizedExtraDetailRoot.transform))
                {
                    continue;
                }
                if (mf.sharedMesh != null)
                {
                    _sourceFilters.Add(mf);
                }
            }
        }

        public void SetOptimized(bool optimized)
        {
            if (_sourceFilters.Count == 0) CacheSourceMeshes();

            if (optimized)
            {
                if (_combinedObject == null) BuildCombinedMesh();
                if (_combinedObject != null) _combinedObject.SetActive(true);
                SetSourceRenderersActive(false);
                if (unoptimizedExtraDetailRoot != null) unoptimizedExtraDetailRoot.SetActive(false);
                _isOptimized = true;
            }
            else
            {
                if (_combinedObject != null) _combinedObject.SetActive(false);
                SetSourceRenderersActive(true);
                if (unoptimizedExtraDetailRoot != null) unoptimizedExtraDetailRoot.SetActive(true);
                _isOptimized = false;
            }
        }

        public void ToggleOptimization()
        {
            SetOptimized(!_isOptimized);
        }

        private void SetSourceRenderersActive(bool active)
        {
            foreach (var mf in _sourceFilters)
            {
                if (mf == null) continue;
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = active;
            }
        }

        private void BuildCombinedMesh()
        {
            if (_sourceFilters.Count == 0) return;

            var combineList = new List<CombineInstance>();
            Material sharedMat = null;

            foreach (var mf in _sourceFilters)
            {
                if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null) continue;
                if (sharedMat == null) sharedMat = mr.sharedMaterial;

                combineList.Add(new CombineInstance
                {
                    mesh = mf.sharedMesh,
                    transform = transform.worldToLocalMatrix * mf.transform.localToWorldMatrix
                });
            }

            if (combineList.Count == 0) return;

            Mesh combinedMesh = new Mesh
            {
                name = $"{name}_CombinedMesh",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };
            combinedMesh.CombineMeshes(combineList.ToArray(), true, true);

            _combinedObject = new GameObject("CombinedZoneWallsMesh");
            _combinedObject.transform.SetParent(transform, false);
            _combinedObject.isStatic = true;

            var outFilter = _combinedObject.AddComponent<MeshFilter>();
            outFilter.sharedMesh = combinedMesh;

            var outRenderer = _combinedObject.AddComponent<MeshRenderer>();
            outRenderer.sharedMaterial = sharedMat;
        }
    }
}

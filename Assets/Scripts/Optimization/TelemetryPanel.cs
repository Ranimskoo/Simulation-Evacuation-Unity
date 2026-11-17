using TMPro;
using UnityEngine;
using IgnisEscape.Core;
using IgnisEscape.Telemetry;

namespace IgnisEscape.Optimization
{
    /// <summary>
    /// Panel pengukuran kinerja yang di-toggle tombol F1 (Bagian 9 spesifikasi):
    /// menampilkan ms/frame, FPS, jumlah segitiga adegan secara live dari mesh renderer
    /// aktif, serta status anggaran <= 120.000 segitiga dan toggle MeshCombiner (F2).
    /// </summary>
    public class TelemetryPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TextMeshProUGUI metricsText;
        [SerializeField] private MeshCombiner meshCombiner;
        [SerializeField] private DataCollector dataCollector;
        [SerializeField] private bool startVisible = true;

        private float _smoothedMs = 16.6f;
        private float _triangleRefreshTimer;
        private int _cachedTriangleCount;

        private void Awake()
        {
            if (meshCombiner == null) meshCombiner = FindFirstObjectByType<MeshCombiner>();
            if (dataCollector == null) dataCollector = FindFirstObjectByType<DataCollector>();
            if (panelRoot != null) panelRoot.SetActive(startVisible);
        }

        private void Update()
        {
            if (InputHelper.WasKeyPressedThisFrame(KeyCode.F1) && panelRoot != null)
            {
                panelRoot.SetActive(!panelRoot.activeSelf);
            }

            if (InputHelper.WasKeyPressedThisFrame(KeyCode.F2))
            {
                MeshCombiner.ToggleAllInScene();
                _triangleRefreshTimer = 0f; // Segera hitung ulang
            }

            if (panelRoot != null && !panelRoot.activeSelf) return;

            float currentMs = Time.unscaledDeltaTime * 1000f;
            _smoothedMs = Mathf.Lerp(_smoothedMs, currentMs, 0.1f);
            float fps = _smoothedMs > 0.01f ? 1000f / _smoothedMs : 0f;

            _triangleRefreshTimer -= Time.unscaledDeltaTime;
            if (_triangleRefreshTimer <= 0f)
            {
                _triangleRefreshTimer = 0.5f;
                _cachedTriangleCount = CountActiveSceneTriangles();
            }

            if (metricsText != null)
            {
                string optStatus = meshCombiner != null
                    ? (meshCombiner.IsOptimized ? "ON (MeshCombiner)" : "OFF (Modular)")
                    : "N/A";
                string budgetStatus = _cachedTriangleCount <= SimulationConfig.MaxTriangleBudget
                    ? "AMAN"
                    : "MELEBIHI ANGGARAN";
                string recStatus = dataCollector != null && dataCollector.IsRecording
                    ? $"REC [{dataCollector.ActiveLabel}] ({dataCollector.RecordedRowCount} baris)"
                    : "IDLE (1/2/3 label)";

                metricsText.text =
                    $"<b>KINERJA (F1)</b>\n" +
                    $"Frame Time : {_smoothedMs:F2} ms ({fps:F0} FPS)\n" +
                    $"Segitiga   : {_cachedTriangleCount:N0} / {SimulationConfig.MaxTriangleBudget:N0} ({budgetStatus})\n" +
                    $"Optimasi   : {optStatus} [F2]\n" +
                    $"Dataset    : {recStatus}";
            }
        }

        public static int CountActiveSceneTriangles()
        {
            int totalTris = 0;

            var meshFilters = FindObjectsByType<MeshFilter>();
            for (int i = 0; i < meshFilters.Length; i++)
            {
                var mf = meshFilters[i];
                if (!mf.gameObject.activeInHierarchy || mf.sharedMesh == null) continue;
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || !mr.enabled) continue;
                totalTris += GetSafeTriangleCount(mf.sharedMesh);
            }

            var skinned = FindObjectsByType<SkinnedMeshRenderer>();
            for (int i = 0; i < skinned.Length; i++)
            {
                var smr = skinned[i];
                if (!smr.gameObject.activeInHierarchy || !smr.enabled || smr.sharedMesh == null) continue;
                totalTris += GetSafeTriangleCount(smr.sharedMesh);
            }

            return totalTris;
        }

        private static int GetSafeTriangleCount(Mesh mesh)
        {
            if (mesh == null) return 0;
            if (mesh.isReadable)
            {
                return mesh.triangles.Length / 3;
            }

            int tris = 0;
            int subCount = mesh.subMeshCount;
            for (int s = 0; s < subCount; s++)
            {
                tris += (int)(mesh.GetIndexCount(s) / 3);
            }
            return tris > 0 ? tris : 12;
        }
    }
}


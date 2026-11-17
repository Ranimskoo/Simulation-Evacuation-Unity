using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using IgnisEscape.Core;

namespace IgnisEscape.Telemetry
{
    /// <summary>
    /// Merekam fitur telemetri setiap window ke evacuation_dataset.csv dengan kolom
    /// v, omega, C, R, label (Bagian 3 spesifikasi). Label diisi pemain lewat tombol 1/2/3.
    /// </summary>
    public class DataCollector : MonoBehaviour
    {
        [Header("Referensi")]
        [SerializeField] private TelemetryExtractor telemetryExtractor;

        [Header("Pengaturan Perekaman")]
        [SerializeField] private bool isRecording = false;
        [SerializeField] private BehaviorState activeLabel = BehaviorState.Tenang;
        [SerializeField] private string relativeCsvPath = "../python/data/evacuation_dataset.csv";

        public bool IsRecording => isRecording;
        public BehaviorState ActiveLabel => activeLabel;
        public int RecordedRowCount { get; private set; }
        public string ResolvedCsvPath { get; private set; }

        private StreamWriter _writer;

        private void Awake()
        {
            if (telemetryExtractor == null)
            {
                telemetryExtractor = FindFirstObjectByType<TelemetryExtractor>();
            }

            ResolvedCsvPath = Path.GetFullPath(Path.Combine(Application.dataPath, relativeCsvPath));
        }

        private void OnEnable()
        {
            if (telemetryExtractor != null)
            {
                telemetryExtractor.OnWindowSampled += HandleWindowSampled;
            }

            if (isRecording)
            {
                OpenWriter();
            }
        }

        private void OnDisable()
        {
            if (telemetryExtractor != null)
            {
                telemetryExtractor.OnWindowSampled -= HandleWindowSampled;
            }
            CloseWriter();
        }

        private void Update()
        {
            // Tombol 1/2/3 untuk menandai kelas perilaku saat playtest (Bagian 3)
            if (InputHelper.WasKeyPressedThisFrame(KeyCode.Alpha1))
            {
                activeLabel = BehaviorState.Tenang;
                Debug.Log("[DataCollector] Label aktif: 0 (Tenang)");
            }
            else if (InputHelper.WasKeyPressedThisFrame(KeyCode.Alpha2))
            {
                activeLabel = BehaviorState.RaguRagu;
                Debug.Log("[DataCollector] Label aktif: 1 (Ragu-Ragu)");
            }
            else if (InputHelper.WasKeyPressedThisFrame(KeyCode.Alpha3))
            {
                activeLabel = BehaviorState.Panik;
                Debug.Log("[DataCollector] Label aktif: 2 (Panik)");
            }
        }

        public void SetRecording(bool record)
        {
            if (isRecording == record) return;
            isRecording = record;
            if (isRecording)
            {
                OpenWriter();
            }
            else
            {
                CloseWriter();
            }
        }

        private void OpenWriter()
        {
            if (_writer != null) return;

            string dir = Path.GetDirectoryName(ResolvedCsvPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            bool fileExists = File.Exists(ResolvedCsvPath) && new FileInfo(ResolvedCsvPath).Length > 0;
            _writer = new StreamWriter(ResolvedCsvPath, append: true, Encoding.UTF8);
            if (!fileExists)
            {
                _writer.WriteLine("v,omega,C,R,label");
                _writer.Flush();
            }
        }

        private void CloseWriter()
        {
            if (_writer != null)
            {
                _writer.Flush();
                _writer.Close();
                _writer = null;
            }
        }

        private void HandleWindowSampled(TelemetrySnapshot snap)
        {
            if (!isRecording) return;
            if (_writer == null) OpenWriter();

            string line = string.Format(
                CultureInfo.InvariantCulture,
                "{0:F4},{1:F4},{2},{3},{4}",
                snap.avgSpeed,
                snap.cameraJitter,
                snap.collisionCount,
                snap.turnReversals,
                (int)activeLabel
            );
            _writer.WriteLine(line);
            _writer.Flush();
            RecordedRowCount++;
        }
    }
}


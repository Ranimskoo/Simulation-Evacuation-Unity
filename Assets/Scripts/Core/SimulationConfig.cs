using UnityEngine;

namespace IgnisEscape.Core
{
    /// <summary>
    /// Konstanta global proyek IgnisEscape sesuai kontrak spesifikasi (Bagian 1, 2, 8).
    /// </summary>
    public static class SimulationConfig
    {
        // Warna utama UI & seluruh elemen bantuan: toska #00A99D (Bagian 1 & 8)
        public static readonly Color PrimaryTosca = new Color(0f, 169f / 255f, 157f / 255f, 1f);
        public const string PrimaryToscaHex = "#00A99D";

        // Sliding window telemetri 0,5 detik (Bagian 2)
        public const float TelemetryWindowSeconds = 0.5f;

        // Konfigurasi default UDP lokal (Bagian 1)
        public const string DefaultHost = "127.0.0.1";
        public const int DefaultSendPort = 5005;
        public const int DefaultReceivePort = 5006;

        // Anggaran segitiga maksimum adegan akhir (Bagian 9)
        public const int MaxTriangleBudget = 120000;
    }
}


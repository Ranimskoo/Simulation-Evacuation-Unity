using System;

namespace IgnisEscape.Telemetry
{
    /// <summary>
    /// Data 4 fitur telemetri per sliding window 0,5 detik (Bagian 2 spesifikasi).
    /// </summary>
    [Serializable]
    public struct TelemetrySnapshot
    {
        public float avgSpeed;       // v: m/detik
        public float cameraJitter;   // omega: derajat/detik
        public int collisionCount;   // C: jumlah benturan collider
        public int turnReversals;    // R: jumlah pembalikan arah mendadak
        public float timestamp;
    }
}


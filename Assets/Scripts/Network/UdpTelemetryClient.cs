using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using IgnisEscape.Core;
using IgnisEscape.Telemetry;
using Debug = UnityEngine.Debug;

namespace IgnisEscape.Network
{
    /// <summary>
    /// Klien UDP lokal (127.0.0.1) untuk mengirim fitur ke Python (port 5005) dan
    /// menerima label status resmi + confidence dari Python (port 5006) (Bagian 1 & 2).
    /// </summary>
    public class UdpTelemetryClient : MonoBehaviour
    {
        [Serializable]
        private struct ServerResponsePayload
        {
            public string state;
            public float confidence;
        }

        [Header("Konfigurasi UDP")]
        [SerializeField] private string host = SimulationConfig.DefaultHost;
        [SerializeField] private int sendPort = SimulationConfig.DefaultSendPort;
        [SerializeField] private int receivePort = SimulationConfig.DefaultReceivePort;
        [SerializeField] private float connectionTimeoutSeconds = 2.5f;

        [Header("Referensi")]
        [SerializeField] private TelemetryExtractor telemetryExtractor;

        public event Action<BehaviorState, float> OnStateReceived;
        public event Action<bool> OnConnectionChanged;

        public bool IsConnected { get; private set; }
        public BehaviorState LastReceivedState { get; private set; } = BehaviorState.Tenang;
        public float LastConfidence { get; private set; }

        private UdpClient _sendClient;
        private UdpClient _receiveClient;
        private IPEndPoint _remoteEndPoint;
        private Thread _receiveThread;
        private volatile bool _running;

        private readonly ConcurrentQueue<ServerResponsePayload> _incomingQueue =
            new ConcurrentQueue<ServerResponsePayload>();

        private float _lastPacketReceivedTime = -100f;
        private float _firstPacketSentTime = -1f;
        private bool _hasSentAnyPacket;
        private bool _autoStartAttempted;
        private Process _spawnedPythonProcess;

        private void Awake()
        {
            if (telemetryExtractor == null)
            {
                telemetryExtractor = FindFirstObjectByType<TelemetryExtractor>();
            }
        }

        private void OnEnable()
        {
            StartNetworking();
            if (telemetryExtractor != null)
            {
                telemetryExtractor.OnWindowSampled += HandleWindowSampled;
            }
        }

        private void OnDisable()
        {
            if (telemetryExtractor != null)
            {
                telemetryExtractor.OnWindowSampled -= HandleWindowSampled;
            }
            StopNetworking();
        }

        private void Update()
        {
            // Proses pesan masuk di main thread Unity
            while (_incomingQueue.TryDequeue(out var payload))
            {
                _lastPacketReceivedTime = Time.unscaledTime;
                SetConnectionState(true);

                if (BehaviorStateExtensions.TryParseProtocolString(payload.state, out var parsedState))
                {
                    LastReceivedState = parsedState;
                    LastConfidence = payload.confidence;
                    OnStateReceived?.Invoke(parsedState, payload.confidence);
                }
            }

            // Deteksi timeout/terputus -> masuk mode manual (Bagian 2)
            if (_hasSentAnyPacket && IsConnected &&
                (Time.unscaledTime - _lastPacketReceivedTime) > connectionTimeoutSeconds)
            {
                SetConnectionState(false);
            }

            // Jika server Python belum berjalan saat Play dimulai, jalankan otomatis di latar belakang
            if (_hasSentAnyPacket && !IsConnected && !_autoStartAttempted &&
                _firstPacketSentTime > 0f && (Time.unscaledTime - _firstPacketSentTime) > 0.6f)
            {
                _autoStartAttempted = true;
                TryAutoStartPythonServer();
            }
        }

        private void StartNetworking()
        {
            try
            {
                _remoteEndPoint = new IPEndPoint(IPAddress.Parse(host), sendPort);
                _sendClient = new UdpClient();
                _receiveClient = new UdpClient(receivePort);
                _receiveClient.Client.ReceiveTimeout = 500;

                _running = true;
                _receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "IgnisEscape_UdpReceive"
                };
                _receiveThread.Start();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UdpTelemetryClient] Gagal membuka soket UDP: {ex.Message}");
                SetConnectionState(false);
            }
        }

        private void StopNetworking()
        {
            _running = false;

            try { _receiveClient?.Close(); } catch { }
            try { _sendClient?.Close(); } catch { }

            if (_receiveThread != null && _receiveThread.IsAlive)
            {
                _receiveThread.Join(300);
            }

            _receiveClient = null;
            _sendClient = null;
            _receiveThread = null;

            if (_spawnedPythonProcess != null)
            {
                try
                {
                    if (!_spawnedPythonProcess.HasExited)
                    {
                        _spawnedPythonProcess.Kill();
                    }
                    _spawnedPythonProcess.Dispose();
                }
                catch { }
                _spawnedPythonProcess = null;
            }
        }

        private void TryAutoStartPythonServer()
        {
            try
            {
                string scriptPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../python/server.py"));
                if (!File.Exists(scriptPath)) return;

                var psi = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{scriptPath}\"",
                    WorkingDirectory = Path.GetDirectoryName(scriptPath) ?? string.Empty,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                _spawnedPythonProcess = Process.Start(psi);
                Debug.Log("[UdpTelemetryClient] Server AI Python dijalankan otomatis di latar belakang.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UdpTelemetryClient] Gagal menjalankan server.py otomatis: {ex.Message}");
            }
        }

        private void ReceiveLoop()
        {
            IPEndPoint anyEp = new IPEndPoint(IPAddress.Any, 0);
            while (_running)
            {
                try
                {
                    byte[] bytes = _receiveClient.Receive(ref anyEp);
                    string json = Encoding.UTF8.GetString(bytes);
                    var payload = JsonUtility.FromJson<ServerResponsePayload>(json);
                    if (!string.IsNullOrEmpty(payload.state))
                    {
                        _incomingQueue.Enqueue(payload);
                    }
                }
                catch (SocketException)
                {
                    // Timeout periodik agar thread bisa berhenti bersih
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (_running)
                    {
                        Debug.LogWarning($"[UdpTelemetryClient] Kesalahan baca UDP: {ex.Message}");
                    }
                }
            }
        }

        private void HandleWindowSampled(TelemetrySnapshot snap)
        {
            if (_sendClient == null || _remoteEndPoint == null) return;

            // Format JSON persis sesuai kontrak Bagian 2: {"features": [v, omega, C, R]}
            string json = string.Format(
                CultureInfo.InvariantCulture,
                "{{\"features\": [{0:F4}, {1:F4}, {2}, {3}]}}",
                snap.avgSpeed,
                snap.cameraJitter,
                snap.collisionCount,
                snap.turnReversals
            );

            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                _sendClient.Send(bytes, bytes.Length, _remoteEndPoint);
                if (!_hasSentAnyPacket)
                {
                    _firstPacketSentTime = Time.unscaledTime;
                }
                _hasSentAnyPacket = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UdpTelemetryClient] Gagal kirim UDP: {ex.Message}");
                SetConnectionState(false);
            }
        }

        private void SetConnectionState(bool connected)
        {
            if (IsConnected == connected) return;
            IsConnected = connected;
            OnConnectionChanged?.Invoke(connected);
        }
    }
}


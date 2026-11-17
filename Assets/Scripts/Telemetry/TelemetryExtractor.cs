using System;
using System.Collections.Generic;
using UnityEngine;
using IgnisEscape.Core;
using IgnisEscape.Player;

namespace IgnisEscape.Telemetry
{
    /// <summary>
    /// Mengekstrak 4 fitur telemetri per sliding window 0,5 detik dari Player (Bagian 2 & 5A):
    /// - v: kecepatan horizontal CharacterController (frame teleportasi dikecualikan).
    /// - omega: laju perubahan sudut yaw + pitch kamera (derajat/detik).
    /// - C: benturan dengan objek bertag Wall, Desk, Debris (cooldown 0,2 dtk per objek).
    /// - R: pembalikan tanda vektor input gerak dengan ambang magnitudo minimum.
    /// </summary>
    public class TelemetryExtractor : MonoBehaviour
    {
        [Header("Referensi Player & Kamera")]
        [SerializeField] private PlayerController playerController;
        [SerializeField] private Transform cameraTransform;

        [Header("Parameter Window")]
        [SerializeField] private float windowDuration = SimulationConfig.TelemetryWindowSeconds;
        [SerializeField] private float minInputMagnitude = 0.2f;
        [SerializeField] private float perObjectCollisionCooldown = 0.2f;

        public event Action<TelemetrySnapshot> OnWindowSampled;

        public TelemetrySnapshot LatestSnapshot { get; private set; }

        private float _lastYaw;
        private float _lastPitch;
        private CameraDampener _cameraDampener;
        private Vector2 _lastValidInput;
        private bool _hasLastInput;

        private float _elapsed;
        private float _accumulatedSpeedTime;
        private float _continuousElapsed;
        private float _accumulatedCameraAngle;
        private int _collisionCount;
        private int _turnReversals;

        private readonly Dictionary<GameObject, float> _collisionCooldowns = new Dictionary<GameObject, float>();

        private void Awake()
        {
            if (playerController == null) playerController = GetComponent<PlayerController>();
            if (_cameraDampener == null) _cameraDampener = GetComponent<CameraDampener>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        }

        private void OnEnable()
        {
            ResetWindowAccumulators();
            CacheCameraAngles();
            _hasLastInput = false;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // 1. avg_speed (v): kecualikan frame teleportasi agar tidak terbaca sebagai lari
            if (playerController != null && !playerController.TeleportedThisFrame)
            {
                _accumulatedSpeedTime += playerController.CurrentHorizontalSpeed * dt;
                _continuousElapsed += dt;
            }

            // 2. camera_jitter (omega): laju perubahan sudut yaw + pitch kamera
            // Kompensasi faktor CameraDampener agar intervensi Panik tidak mengecilkan bacaan input mouse asli
            if (cameraTransform != null)
            {
                Vector3 euler = cameraTransform.eulerAngles;
                float deltaYaw = Mathf.Abs(Mathf.DeltaAngle(_lastYaw, euler.y));
                float deltaPitch = Mathf.Abs(Mathf.DeltaAngle(_lastPitch, euler.x));
                float damp = (_cameraDampener != null && _cameraDampener.CurrentMultiplier > 0.1f)
                    ? _cameraDampener.CurrentMultiplier
                    : 1f;
                _accumulatedCameraAngle += (deltaYaw + deltaPitch) / damp;
                _lastYaw = euler.y;
                _lastPitch = euler.x;
            }

            // 4. turn_reversals (R): pembalikan tanda vektor input gerak (maju<->mundur / kiri<->kanan)
            Vector2 moveInput = playerController != null
                ? playerController.CurrentMoveInput
                : InputHelper.GetMoveAxes();

            if (moveInput.magnitude >= minInputMagnitude)
            {
                if (_hasLastInput)
                {
                    bool reversedX = Mathf.Abs(moveInput.x) >= minInputMagnitude &&
                                     Mathf.Abs(_lastValidInput.x) >= minInputMagnitude &&
                                     Mathf.Sign(moveInput.x) != Mathf.Sign(_lastValidInput.x);
                    bool reversedY = Mathf.Abs(moveInput.y) >= minInputMagnitude &&
                                     Mathf.Abs(_lastValidInput.y) >= minInputMagnitude &&
                                     Mathf.Sign(moveInput.y) != Mathf.Sign(_lastValidInput.y);

                    if (reversedX || reversedY)
                    {
                        _turnReversals++;
                    }
                }
                _lastValidInput = moveInput;
                _hasLastInput = true;
            }

            _elapsed += dt;
            if (_elapsed >= windowDuration)
            {
                float safeTotal = Mathf.Max(_elapsed, 0.001f);
                float safeContinuous = Mathf.Max(_continuousElapsed, 0.001f);

                var snapshot = new TelemetrySnapshot
                {
                    avgSpeed = _continuousElapsed > 0.001f ? (_accumulatedSpeedTime / safeContinuous) : 0f,
                    cameraJitter = _accumulatedCameraAngle / safeTotal,
                    collisionCount = _collisionCount,
                    turnReversals = _turnReversals,
                    timestamp = Time.time
                };

                LatestSnapshot = snapshot;
                OnWindowSampled?.Invoke(snapshot);
                ResetWindowAccumulators();
            }
        }

        /// <summary>
        /// Menghitung benturan hanya dengan objek bertag Wall, Desk, atau Debris
        /// dengan cooldown 0,2 detik per objek (Bagian 5A).
        /// </summary>
        public void RegisterCollision(GameObject hitObject)
        {
            if (hitObject == null) return;
            if (!hitObject.CompareTag("Wall") &&
                !hitObject.CompareTag("Desk") &&
                !hitObject.CompareTag("Debris"))
            {
                return;
            }

            float now = Time.time;
            if (_collisionCooldowns.TryGetValue(hitObject, out float nextAllowed) && now < nextAllowed)
            {
                return;
            }

            _collisionCooldowns[hitObject] = now + perObjectCollisionCooldown;
            _collisionCount++;
        }

        private void CacheCameraAngles()
        {
            if (cameraTransform == null) return;
            Vector3 euler = cameraTransform.eulerAngles;
            _lastYaw = euler.y;
            _lastPitch = euler.x;
        }

        private void ResetWindowAccumulators()
        {
            _elapsed = 0f;
            _accumulatedSpeedTime = 0f;
            _continuousElapsed = 0f;
            _accumulatedCameraAngle = 0f;
            _collisionCount = 0;
            _turnReversals = 0;
        }
    }
}

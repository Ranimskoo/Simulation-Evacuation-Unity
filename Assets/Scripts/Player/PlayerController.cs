using UnityEngine;
using IgnisEscape.Core;
using IgnisEscape.Telemetry;

namespace IgnisEscape.Player
{
    /// <summary>
    /// Kendali pemain utama (Bagian 5A):
    /// - Mode desktop: WASD (jalan 1,7 m/s, Shift lari 2,6 m/s) + mouse-look dengan CameraDampener.
    /// - Mendukung pengali perlambatan PseudoHaptics, knockback meluruh ~0,3 detik,
    ///   serta penanda frame teleportasi agar dikecualikan dari perhitungan avg_speed.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Kecepatan & Kamera (Bagian 5A)")]
        [SerializeField] private float walkSpeed = 1.7f;
        [SerializeField] private float sprintSpeed = 2.6f;
        [SerializeField] private float jumpHeight = 1.15f;
        [SerializeField] private float mouseSensitivity = 0.15f;
        [SerializeField] private float gravity = -9.81f;
        [SerializeField] private float knockbackDecayDuration = 0.3f;
        [SerializeField] private Transform cameraPivot;

        [Header("Referensi")]
        [SerializeField] private CameraDampener cameraDampener;
        [SerializeField] private TelemetryExtractor telemetryExtractor;
        [SerializeField] private PlayerHealth playerHealth;

        private CharacterController _controller;
        private float _pitch;
        private float _verticalVelocity;
        private Vector3 _knockbackVelocity;
        private float _knockbackDecayRate;
        private bool _cursorLocked = true;
        private float _speedMultiplier = 1f;

        public Vector2 CurrentMoveInput { get; private set; }
        public float CurrentHorizontalSpeed { get; private set; }
        public bool TeleportedThisFrame { get; private set; }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (cameraPivot == null && Camera.main != null) cameraPivot = Camera.main.transform;
            if (cameraDampener == null) cameraDampener = GetComponent<CameraDampener>();
            if (telemetryExtractor == null) telemetryExtractor = GetComponent<TelemetryExtractor>();
            if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
        }

        private void OnEnable()
        {
            if (playerHealth != null) playerHealth.OnDamaged += HandleDamaged;
            SetCursorLock(true);
        }

        private void OnDisable()
        {
            if (playerHealth != null) playerHealth.OnDamaged -= HandleDamaged;
            SetCursorLock(false);
        }

        private void Update()
        {
            if (InputHelper.WasKeyPressedThisFrame(KeyCode.Escape))
            {
                SetCursorLock(!_cursorLocked);
            }

            HandleLook();
            HandleMovement();
        }

        private void LateUpdate()
        {
            // Reset flag teleport setelah TelemetryExtractor membaca di Update/LateUpdate
            TeleportedThisFrame = false;
        }

        public void SetSpeedMultiplier(float multiplier)
        {
            _speedMultiplier = Mathf.Clamp(multiplier, 0.2f, 1.5f);
        }

        public void TeleportTo(Vector3 targetPosition)
        {
            if (_controller == null) return;
            TeleportedThisFrame = true;
            _controller.enabled = false;
            transform.position = targetPosition;
            _controller.enabled = true;
            _verticalVelocity = 0f;
            _knockbackVelocity = Vector3.zero;
            CurrentHorizontalSpeed = 0f;
        }

        private void HandleLook()
        {
            if (!_cursorLocked || cameraPivot == null) return;

            Vector2 delta = InputHelper.GetMouseDelta();
            float damp = cameraDampener != null ? cameraDampener.CurrentMultiplier : 1f;
            float sens = mouseSensitivity * damp;

            transform.Rotate(Vector3.up, delta.x * sens);
            _pitch = Mathf.Clamp(_pitch - delta.y * sens, -80f, 80f);
            cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void HandleMovement()
        {
            CurrentMoveInput = InputHelper.GetMoveAxes();
            float baseSpeed = InputHelper.IsSprintHeld() ? sprintSpeed : walkSpeed;
            float targetSpeed = baseSpeed * _speedMultiplier;

            Vector3 moveDir = transform.right * CurrentMoveInput.x + transform.forward * CurrentMoveInput.y;
            if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

            if (_controller.isGrounded)
            {
                if (_verticalVelocity < 0f)
                {
                    _verticalVelocity = -2f;
                }
                if (InputHelper.WasKeyPressedThisFrame(KeyCode.Space))
                {
                    _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
            }
            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 horizontalVel = moveDir * targetSpeed + _knockbackVelocity;
            Vector3 velocity = horizontalVel;
            velocity.y = _verticalVelocity;

            Vector3 before = transform.position;
            _controller.Move(velocity * Time.deltaTime);
            Vector3 after = transform.position;

            if (!TeleportedThisFrame && Time.deltaTime > 0f)
            {
                Vector3 horizDelta = after - before;
                horizDelta.y = 0f;
                float actualSpeed = horizDelta.magnitude / Time.deltaTime;
                float intendedSpeed = moveDir.magnitude * baseSpeed;
                CurrentHorizontalSpeed = Mathf.Max(actualSpeed, intendedSpeed * 0.95f);
            }
            else
            {
                CurrentHorizontalSpeed = 0f;
            }

            if (_knockbackVelocity.sqrMagnitude > 0.0001f)
            {
                float step = _knockbackDecayRate * Time.deltaTime;
                _knockbackVelocity = Vector3.MoveTowards(_knockbackVelocity, Vector3.zero, step);
            }
            else
            {
                _knockbackVelocity = Vector3.zero;
            }
        }

        private void HandleDamaged(float _, Vector3 knockbackImpulse)
        {
            knockbackImpulse.y = 0f;
            _knockbackVelocity += knockbackImpulse;
            float safeDuration = Mathf.Max(0.05f, knockbackDecayDuration);
            _knockbackDecayRate = _knockbackVelocity.magnitude / safeDuration;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (telemetryExtractor != null && hit.normal.y <= 0.65f)
            {
                telemetryExtractor.RegisterCollision(hit.gameObject);
            }
        }

        private void SetCursorLock(bool locked)
        {
            _cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}


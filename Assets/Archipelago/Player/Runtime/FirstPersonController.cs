using System;
using Archipelago.Core;
using Archipelago.Session;
using MessagePipe;
using UnityEngine;
using Zenject;

namespace Archipelago.Player
{
    /// <summary>
    /// First-person character controller.
    /// Движение и взгляд читаются через InputReader (события Moved / Looked).
    /// Блокировка движения — по SessionStateChangedMessage (без изменений).
    ///
    /// PERF: Camera pitch clamped every frame — O(1), no allocations.
    /// THREAD: main thread only.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────

        [Header("Movement")]
        [SerializeField] private float _walkSpeed   = 4f;
        [SerializeField] private float _sprintSpeed = 7f;
        [SerializeField] private float _gravity     = -15f;

        [Header("Look")]
        [SerializeField] private Transform _cameraRoot;
        [SerializeField] private float     _mouseSensitivity = 1f;
        [SerializeField] private float     _pitchClamp       = 85f;

        // ── Injected ─────────────────────────────────────────────

        [Inject] private InputReader                             _inputReader;
        [Inject] private ISubscriber<SessionStateChangedMessage> _sessionSub;

        // ── Private ──────────────────────────────────────────────

        private CharacterController _cc;
        private IDisposable         _subscription;

        private Vector3 _velocity;
        private Vector2 _moveInput;
        private Vector2 _lookInput;
        private float   _pitch;
        private bool    _movementEnabled = true;
        private bool    _sprinting;

        // ── Unity Lifecycle ──────────────────────────────────────

        private void Awake()
        {
            // PERF: cache — never call GetComponent in Update
            _cc = GetComponent<CharacterController>();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }

        private void OnEnable()
        {
            _inputReader.Moved          += OnMoved;
            _inputReader.Looked         += OnLooked;
            _inputReader.SprintStarted  += OnSprintStarted;
            _inputReader.SprintCancelled += OnSprintCancelled;
        }

        private void OnDisable()
        {
            _inputReader.Moved          -= OnMoved;
            _inputReader.Looked         -= OnLooked;
            _inputReader.SprintStarted  -= OnSprintStarted;
            _inputReader.SprintCancelled -= OnSprintCancelled;
        }

        private void Start()
        {
            _subscription = _sessionSub.Subscribe(OnSessionStateChanged);
        }

        private void Update()
        {
            HandleLook();
            if (_movementEnabled) HandleMovement();
            ApplyGravity();
        }

        private void OnDestroy()
        {
            _subscription?.Dispose();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }

        // ── Input Handlers ────────────────────────────────────────

        private void OnMoved(Vector2 value)           => _moveInput = value;
        private void OnLooked(Vector2 value)          => _lookInput = value;
        private void OnSprintStarted()                => _sprinting = true;
        private void OnSprintCancelled()              => _sprinting = false;

        // ── Movement ─────────────────────────────────────────────

        private void HandleMovement()
        {
            float   speed = _sprinting ? _sprintSpeed : _walkSpeed;
            Vector3 dir   = transform.right   * _moveInput.x
                          + transform.forward * _moveInput.y;

            _cc.Move(dir * (speed * Time.deltaTime));
        }

        private void ApplyGravity()
        {
            if (_cc.isGrounded && _velocity.y < 0f)
                _velocity.y = -2f;

            _velocity.y += _gravity * Time.deltaTime;
            _cc.Move(_velocity * Time.deltaTime);
        }

        // ── Look ─────────────────────────────────────────────────

        private void HandleLook()
        {
            transform.Rotate(Vector3.up, _lookInput.x * _mouseSensitivity);

            _pitch -= _lookInput.y * _mouseSensitivity;
            _pitch  = Mathf.Clamp(_pitch, -_pitchClamp, _pitchClamp);

            if (_cameraRoot != null)
                _cameraRoot.localEulerAngles = new Vector3(_pitch, 0f, 0f);
        }

        // ── Session State ─────────────────────────────────────────

        private void OnSessionStateChanged(SessionStateChangedMessage msg)
        {
            _movementEnabled = msg.Next is SessionState.FreeRoam or SessionState.WakeUp;

            bool freeCursor = msg.Next is SessionState.Scanning or SessionState.MiniGame;
            Cursor.lockState = freeCursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible   = freeCursor;
        }
    }
}
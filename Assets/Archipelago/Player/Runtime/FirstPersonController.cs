using System;
using Archipelago.Core;
using MessagePipe;
using UnityEngine;
using Zenject;

namespace Archipelago.Player
{
    [DefaultExecutionOrder(-10)]
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

        private CharacterController  _cc;
        private PlayerFeelController _feelController;
        private IDisposable          _subscription;

        private Vector2 _moveInput;
        private Vector2 _lookInput;
        private float   _pitch;
        private float   _verticalVelocity;
        private bool    _sprinting;

        // Явный дефолт true — не ждём SessionStateChangedMessage при старте
        private bool _movementEnabled = true;
        private bool _injected;

        // Считаем скорость сами — не зависим от cc.velocity
        private Vector3 _trackedVelocity;

        public Vector3 Velocity     => _trackedVelocity;
        public bool    IsGrounded  => _cc.isGrounded;
        public float   SprintSpeed => _sprintSpeed;

        // ── Zenject ──────────────────────────────────────────────

        [Inject]
        private void Construct()
        {
            _injected = true;
            if (isActiveAndEnabled) SubscribeInput();
        }

        // ── Lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _cc             = GetComponent<CharacterController>();
            _feelController = GetComponent<PlayerFeelController>();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }

        private void OnEnable()
        {
            if (!_injected) return;
            SubscribeInput();
        }

        private void OnDisable()
        {
            if (!_injected) return;
            UnsubscribeInput();
        }

        private void Start()
        {
            _subscription = _sessionSub.Subscribe(OnSessionStateChanged);
        }

        private void OnDestroy()
        {
            _subscription?.Dispose();
            UnsubscribeInput();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }

        private void SubscribeInput()
        {
            _inputReader.Moved           += OnMoved;
            _inputReader.Looked          += OnLooked;
            _inputReader.SprintStarted   += OnSprintStarted;
            _inputReader.SprintCancelled += OnSprintCancelled;
        }

        private void UnsubscribeInput()
        {
            _inputReader.Moved           -= OnMoved;
            _inputReader.Looked          -= OnLooked;
            _inputReader.SprintStarted   -= OnSprintStarted;
            _inputReader.SprintCancelled -= OnSprintCancelled;
        }

        // ── Update ───────────────────────────────────────────────

        private void Update()
        {
            HandleLook();
            HandleMovement();
            HandleGravity();
        }

        // ── Input ────────────────────────────────────────────────

        private void OnMoved(Vector2 v)   => _moveInput = v;
        private void OnLooked(Vector2 v)  => _lookInput = v;
        private void OnSprintStarted()    => _sprinting = true;
        private void OnSprintCancelled()  => _sprinting = false;

        // ── Movement ─────────────────────────────────────────────

        private void HandleMovement()
        {
            Vector3 horizontal = Vector3.zero;

            if (_movementEnabled && _moveInput.sqrMagnitude > 0.001f)
            {
                float   speed = _sprinting ? _sprintSpeed : _walkSpeed;
                Vector3 dir   = transform.right   * _moveInput.x
                              + transform.forward * _moveInput.y;
                horizontal = dir.normalized * speed;
            }

            _cc.Move((horizontal + Vector3.up * _verticalVelocity) * Time.deltaTime);

            // Сохраняем желаемую скорость — не cc.velocity, которая может быть
            // нулём из-за коллизий или скин-виджа CharacterController
            _trackedVelocity = new Vector3(horizontal.x, _verticalVelocity, horizontal.z);
        }

        private void HandleGravity()
        {
            if (_cc.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;

            _verticalVelocity += _gravity * Time.deltaTime;
        }

        // ── Look ─────────────────────────────────────────────────

        private void HandleLook()
        {
            transform.Rotate(Vector3.up, _lookInput.x * _mouseSensitivity);

            _pitch -= _lookInput.y * _mouseSensitivity;
            _pitch  = Mathf.Clamp(_pitch, -_pitchClamp, _pitchClamp);

            if (_cameraRoot != null)
            {
                // Не трогаем Z — его пишет PlayerFeelController (tilt)
                var e = _cameraRoot.localEulerAngles;
                _cameraRoot.localEulerAngles = new Vector3(_pitch, e.y, e.z);
            }

            _feelController?.NotifyLookDelta(_lookInput);
        }

        // ── Session ──────────────────────────────────────────────

        private void OnSessionStateChanged(SessionStateChangedMessage msg)
        {
            _movementEnabled = msg.Next is SessionState.FreeRoam or SessionState.WakeUp;

            bool freeCursor = msg.Next is SessionState.Scanning or SessionState.MiniGame;
            Cursor.lockState = freeCursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible   = freeCursor;
        }

        // ── Public API ───────────────────────────────────────────

        public void SetCircleSearchCursor(bool visible)
        {
            Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible   = visible;
        }

        public void Teleport(Vector3 position)
        {
            _cc.enabled        = false;
            transform.position = position;
            _cc.enabled        = true;
        }
    }
}
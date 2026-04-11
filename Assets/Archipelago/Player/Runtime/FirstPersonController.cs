using System;
using Archipelago.Core;
using Archipelago.Session;
using MessagePipe;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace Archipelago.Player
{
    /// <summary>
    /// First-person character controller.
    /// Uses CharacterController + Input System (new).
    ///
    /// Automatically blocks/restores movement based on SessionState:
    ///   FreeRoam  → movement on, cursor locked
    ///   MiniGame / Scanning / Routine → movement off, cursor conditionally free
    ///
    /// PERF: Camera pitch clamped every frame — O(1), no allocations.
    /// Zenject: MonoBehaviour → use [Inject] field injection, not constructor.
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

        // ── Zenject Field Injection ───────────────────────────────

        [Inject] private ISubscriber<SessionStateChangedMessage> _sessionSub;

        // ── Private ──────────────────────────────────────────────

        private CharacterController        _cc;
        private ArchipelagoInputActions    _input;
        private IDisposable                _subscription;

        private Vector3 _velocity;
        private float   _pitch;
        private bool    _movementEnabled = true;

        // ── Unity Lifecycle ──────────────────────────────────────

        private void Awake()
        {
            // PERF: Cache — never call GetComponent in Update
            _cc = GetComponent<CharacterController>();

            _input = new ArchipelagoInputActions();
            _input.Enable();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }

        private void Start()
        {
            // Zenject injects _sessionSub before Start(), safe to subscribe here.
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
            _input?.Disable();
            _input?.Dispose();
            _subscription?.Dispose();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }

        // ── Movement ─────────────────────────────────────────────

        private void HandleMovement()
        {
            var   move2d   = _input.Player.Move.ReadValue<Vector2>();
            bool  sprinting = _input.Player.Sprint.IsPressed();
            float speed     = sprinting ? _sprintSpeed : _walkSpeed;

            Vector3 dir = transform.right   * move2d.x
                        + transform.forward * move2d.y;

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
            var lookDelta = _input.Player.Look.ReadValue<Vector2>();

            transform.Rotate(Vector3.up, lookDelta.x * _mouseSensitivity);

            _pitch -= lookDelta.y * _mouseSensitivity;
            _pitch  = Mathf.Clamp(_pitch, -_pitchClamp, _pitchClamp);

            if (_cameraRoot != null)
                _cameraRoot.localEulerAngles = new Vector3(_pitch, 0f, 0f);
        }

        // ── Session State Reaction ────────────────────────────────

        private void OnSessionStateChanged(SessionStateChangedMessage msg)
        {
            _movementEnabled = msg.Next is SessionState.FreeRoam or SessionState.WakeUp;

            bool freeCursor = msg.Next is SessionState.Scanning
                                       or SessionState.MiniGame;

            Cursor.lockState = freeCursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible   = freeCursor;
        }
    }
}
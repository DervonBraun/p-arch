using System;
using System.Collections;
using Archipelago.Core;
using MessagePipe;
using UnityEngine;
using Zenject;

namespace Archipelago.Player
{
    [DefaultExecutionOrder(10)]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerFeelController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────

        [Header("References")]
        [SerializeField] private Transform _cameraRoot;

        [Header("Head Bob")]
        [SerializeField] private float _bobFrequencyWalk   = 1.6f;
        [SerializeField] private float _bobFrequencySprint = 2.4f;
        [SerializeField] private float _bobAmplitudeY      = 0.05f;
        [SerializeField] private float _bobAmplitudeX      = 0.025f;
        [SerializeField] private float _bobSmoothing       = 10f;

        [Header("Camera Sway")]
        [SerializeField] private float _swayAmount    = 0.06f;
        [SerializeField] private float _swayMaxAmount = 0.10f;
        [SerializeField] private float _swaySmoothing = 5f;

        [Header("Tilt при стрейфе")]
        [SerializeField] private float _tiltAmount    = 2.5f;
        [SerializeField] private float _tiltSmoothing = 8f;

        [Header("Landing Impact")]
        [SerializeField] private float _impactAmplitude = 0.10f;
        [SerializeField] private float _impactDuration  = 0.20f;

        [Header("Footsteps")]
        [SerializeField] private float _stepIntervalWalk   = 0.50f;
        [SerializeField] private float _stepIntervalSprint = 0.32f;

        // ── Injected ──────────────────────────────────────────────

        [Inject] private ISubscriber<SessionStateChangedMessage> _sessionSub;

        // ── Private ───────────────────────────────────────────────

        private FirstPersonController _fpc;
        private FootstepPlayer        _footstepPlayer;
        private IDisposable           _sub;
        private bool                  _active = true;

        // Bob
        private float   _bobTimer;
        private Vector3 _bobTarget;
        private Vector3 _bobCurrent;

        // Sway
        private float _swayCurrent;

        // Tilt
        private float _tiltTarget;
        private float _tiltCurrent;

        // Landing
        private bool  _wasGrounded;
        private float _impactCurrent;

        // Footsteps
        private float _stepTimer;

        private Vector3 _baseLocalPos;

        // ── Lifecycle ─────────────────────────────────────────────

        private void Awake()
        {
            _fpc            = GetComponent<FirstPersonController>();
            _footstepPlayer = GetComponent<FootstepPlayer>();
        }

        private void Start()
        {
            if (_cameraRoot != null)
                _baseLocalPos = _cameraRoot.localPosition;

            _sub = _sessionSub.Subscribe(OnSessionChanged);
        }

        private void OnDestroy() => _sub?.Dispose();

        private void Update()
        {
            if (!_active || _cameraRoot == null || _fpc == null) return;

            Vector3 vel      = _fpc.Velocity;
            float   hSpeed   = new Vector3(vel.x, 0f, vel.z).magnitude;
            bool    grounded = _fpc.IsGrounded;
            bool    moving   = hSpeed > 0.1f && grounded;
            bool    sprinting = hSpeed > _fpc.SprintSpeed * 0.8f;

            TickBob(moving, sprinting);
            TickTilt(vel);
            TickLanding(grounded);
            TickFootsteps(moving, sprinting, grounded);
            Apply();
        }

        // ── Bob ───────────────────────────────────────────────────

        private void TickBob(bool moving, bool sprinting)
        {
            if (moving)
            {
                float freq = sprinting ? _bobFrequencySprint : _bobFrequencyWalk;
                _bobTimer += Time.deltaTime * freq * Mathf.PI * 2f;
                _bobTarget = new Vector3(
                    Mathf.Cos(_bobTimer * 0.5f) * _bobAmplitudeX,
                    Mathf.Sin(_bobTimer)         * _bobAmplitudeY,
                    0f);
            }
            else
            {
                _bobTimer  = 0f;
                _bobTarget = Vector3.zero;
            }

            _bobCurrent = Vector3.Lerp(_bobCurrent, _bobTarget,
                Time.deltaTime * _bobSmoothing);
        }

        // ── Tilt ──────────────────────────────────────────────────

        private void TickTilt(Vector3 worldVelocity)
        {
            float strafeSpeed = Vector3.Dot(worldVelocity, transform.right);
            _tiltTarget  = Mathf.Clamp(-strafeSpeed * 0.15f, -_tiltAmount, _tiltAmount);
            _tiltCurrent = Mathf.Lerp(_tiltCurrent, _tiltTarget,
                Time.deltaTime * _tiltSmoothing);
        }

        // ── Landing ───────────────────────────────────────────────

        private void TickLanding(bool grounded)
        {
            if (!_wasGrounded && grounded)
                StartCoroutine(LandingImpactRoutine());

            _wasGrounded   = grounded;
            _impactCurrent = Mathf.Lerp(_impactCurrent, 0f, Time.deltaTime * 12f);
        }

        private IEnumerator LandingImpactRoutine()
        {
            _impactCurrent = -_impactAmplitude;
            yield return new WaitForSeconds(_impactDuration * 0.3f);
        }

        // ── Footsteps ─────────────────────────────────────────────

        private void TickFootsteps(bool moving, bool sprinting, bool grounded)
        {
            if (!moving || !grounded) { _stepTimer = 0f; return; }

            _stepTimer += Time.deltaTime;
            float interval = sprinting ? _stepIntervalSprint : _stepIntervalWalk;

            if (_stepTimer >= interval)
            {
                _stepTimer -= interval;
                // Делегируем FootstepPlayer — он сам определит поверхность и
                // воспроизведёт через SteamAudioSource
                _footstepPlayer?.PlayStep();
            }
        }

        // ── Apply ─────────────────────────────────────────────────

        private void Apply()
        {
            _cameraRoot.localPosition = _baseLocalPos
                + _bobCurrent
                + new Vector3(0f, _impactCurrent, 0f);

            var e = _cameraRoot.localEulerAngles;
            _cameraRoot.localEulerAngles = new Vector3(e.x, e.y, _tiltCurrent);
        }

        // ── Sway (вызывается из FPC) ──────────────────────────────

        public void NotifyLookDelta(Vector2 lookDelta)
        {
            float swayTarget = Mathf.Clamp(
                -lookDelta.x * _swayAmount, -_swayMaxAmount, _swayMaxAmount);

            _swayCurrent = Mathf.Lerp(_swayCurrent, swayTarget,
                Time.deltaTime * _swaySmoothing);

            if (_cameraRoot != null && _active)
            {
                var p = _cameraRoot.localPosition;
                _cameraRoot.localPosition = new Vector3(p.x + _swayCurrent, p.y, p.z);
            }
        }

        // ── Session ───────────────────────────────────────────────

        private void OnSessionChanged(SessionStateChangedMessage msg)
        {
            _active = msg.Next is SessionState.FreeRoam or SessionState.WakeUp;

            if (!_active && _cameraRoot != null)
            {
                _bobCurrent    = Vector3.zero;
                _tiltCurrent   = 0f;
                _impactCurrent = 0f;
                _swayCurrent   = 0f;
                _cameraRoot.localPosition    = _baseLocalPos;
                _cameraRoot.localEulerAngles = new Vector3(
                    _cameraRoot.localEulerAngles.x, 0f, 0f);
            }
        }
    }
}
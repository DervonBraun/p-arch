using Archipelago.Core;
using Archipelago.Session;
using UnityEngine;
using Zenject;

namespace Archipelago.Scanner
{
    /// <summary>
    /// Raycast + scan progress. Attach на Player GameObject.
    ///
    /// Поток:
    ///   ПКМ hold  → _scanActive = true, начинаем raycast
    ///   Навёлся на ScannableObject → запускаем прогресс-таймер
    ///   Таймер заполнился → BeginSession()
    ///   Навёл в сторону / отпустил ПКМ → сбрасываем прогресс
    ///   Tab (через InputReader.OnOpenPanel) → OpenPanel() если есть отсканированный объект
    /// </summary>
    public sealed class ScannerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera _camera;

        [Header("Debug")]
        [SerializeField] private bool _drawDebugRay = true;

        [Inject] private InputReader    _inputReader;
        [Inject] private ScannerService _scannerService;
        [Inject] private SessionManager _sessionManager;
        [Inject] private ScannerConfig  _config;

        private ScannableObject _currentTarget;
        private ScannableObject _scannedTarget;   // последний успешно отсканированный
        private bool            _scanActive;      // ПКМ зажата
        private float           _scanProgress;    // 0..1

        // ── Unity Lifecycle ──────────────────────────────────────

        private void Awake()
        {
            if (_camera == null) _camera = Camera.main;
        }

        private void OnEnable()
        {
            _inputReader.ScanHeld      += HandleScanHeld;
            _inputReader.ScanReleased  += HandleScanReleased;
            _inputReader.PanelOpenRequested     += HandleOpenPanel;
        }

        private void OnDisable()
        {
            _inputReader.ScanHeld      -= HandleScanHeld;
            _inputReader.ScanReleased  -= HandleScanReleased;
            _inputReader.PanelOpenRequested     -= HandleOpenPanel;
        }

        private void Update()
        {
            if (!_scanActive) return;
            UpdateRaycast();
        }

        // ── Input Handlers ────────────────────────────────────────

        private void HandleScanHeld()
        {
            _scanActive = true;
        }

        private void HandleScanReleased()
        {
            _scanActive = false;
            ResetProgress();
        }

        private void HandleOpenPanel()
        {
            if (_scannedTarget == null) return;
            _sessionManager.EnterScanning();
        }

        // ── Raycast & Progress ────────────────────────────────────

        private void UpdateRaycast()
        {
            if (_camera == null) return;

            Vector3 origin    = _camera.transform.position;
            Vector3 direction = _camera.transform.forward;

            if (_drawDebugRay)
                Debug.DrawRay(origin, direction * _config.scanRaycastDistance, Color.cyan);

            bool hit = Physics.Raycast(
                origin, direction,
                out RaycastHit hitInfo,
                _config.scanRaycastDistance,
                _config.scanLayerMask);

            ScannableObject scannable = null;
            if (hit)
            {
                scannable = hitInfo.collider.GetComponentInParent<ScannableObject>()
                         ?? hitInfo.collider.GetComponent<ScannableObject>();
            }

            // Цель сменилась — сброс прогресса
            if (scannable != _currentTarget)
            {
                ResetProgress();
                _currentTarget = scannable;
            }

            // Тикаем прогресс
            if (_currentTarget != null)
                TickProgress();
        }

        private void TickProgress()
        {
            _scanProgress += Time.deltaTime / _config.scanDuration;

            // PERF: публиковать ScanProgressMessage каждый кадр не нужно —
            //       достаточно для UI прогресс-бара если он появится.
            // TODO: опубликовать ScanProgressMessage для прогресс-бара в HUD

            if (_scanProgress >= 1f)
                CompleteScan();
        }

        private void CompleteScan()
        {
            _scanProgress  = 1f;
            _scannedTarget = _currentTarget;

            Debug.Log($"[ScannerController] Scanned: {_scannedTarget.Data.objectId}");
            _scannerService.BeginSession(_scannedTarget.Data);

            // Сбрасываем чтобы повторный скан того же объекта работал
            ResetProgress();
        }

        private void ResetProgress()
        {
            _scanProgress  = 0f;
            _currentTarget = null;
        }

        // ── Public ────────────────────────────────────────────────

        public bool            IsScanActive  => _scanActive;
        public float           ScanProgress  => _scanProgress;
        public ScannableObject ScannedTarget => _scannedTarget;
    }
}
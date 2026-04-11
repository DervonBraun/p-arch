using Archipelago.Core;
using Archipelago.Session;
using MessagePipe;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace Archipelago.Scanner
{
    /// <summary>
    /// Управляет режимом сканирования:
    ///   - слушает нажатие OpenScanner (F / LB)
    ///   - делает raycast для поиска ScannableObject
    ///   - включает/выключает outline highlight на целевом объекте
    ///   - делегирует запрос ScannerService
    ///   - уведомляет SessionManager о входе/выходе из Scanning
    ///
    /// Attach на тот же GameObject что и FirstPersonController,
    /// либо на отдельный дочерний объект камеры.
    ///
    /// ВАЖНО: файл ArchipelagoInputActions.inputactions должен иметь
    /// включённую опцию "Generate C# Class" в инспекторе,
    /// иначе _input.Player.* не резолвится.
    /// Если C#-класс не генерируется — используем InputAction напрямую
    /// через InputSystem.FindAction (резервный путь ниже).
    /// </summary>
    public sealed class ScannerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera _camera;

        // ── Zenject ───────────────────────────────────────────────

        [Inject] private ScannerService  _scannerService;
        [Inject] private SessionManager  _sessionManager;
        [Inject] private ScannerConfig   _config;

        // ── Private ──────────────────────────────────────────────

        // Используем InputAction напрямую вместо сгенерированного класса —
        // это надёжнее: не зависит от того нажата ли кнопка Generate C# Class.
        private InputAction _openScannerAction;
        private ScannableObject _currentTarget;
        private bool            _scannerOpen;
        private MaterialPropertyBlock _mpb;

        // ── Unity Lifecycle ──────────────────────────────────────

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();

            if (_camera == null)
                _camera = Camera.main;

            // Читаем action по пути "Player/OpenScanner" из InputActionAsset.
            // Если InputActionAsset назначен через PlayerInput компонент — используем его.
            // Иначе создаём action вручную на клавишу F.
            var playerInput = GetComponentInParent<PlayerInput>();
            if (playerInput != null)
            {
                _openScannerAction = playerInput.actions.FindAction("Player/OpenScanner");
            }

            if (_openScannerAction == null)
            {
                // Fallback: прямой биндинг на F без InputActionAsset
                _openScannerAction = new InputAction(
                    name: "OpenScanner",
                    type: InputActionType.Button,
                    binding: "<Keyboard>/f");
                _openScannerAction.AddBinding("<Gamepad>/leftShoulder");
                _openScannerAction.Enable();
            }
        }

        private void Update()
        {
            if (_openScannerAction.WasPressedThisFrame())
                ToggleScanner();

            if (_scannerOpen)
                UpdateRaycast();
        }

        private void OnDestroy()
        {
            // Dispose только если мы сами создали action (fallback путь)
            if (_openScannerAction != null &&
                string.IsNullOrEmpty(_openScannerAction.actionMap?.name))
            {
                _openScannerAction.Disable();
                _openScannerAction.Dispose();
            }

            ClearOutline(_currentTarget);
        }

        // ── Scanner Toggle ────────────────────────────────────────

        private void ToggleScanner()
        {
            if (_scannerOpen) CloseScanner();
            else              OpenScanner();
        }

        private void OpenScanner()
        {
            _scannerOpen = true;
            _sessionManager.EnterScanning();
        }

        private void CloseScanner()
        {
            _scannerOpen = false;
            _scannerService.EndSession();
            _sessionManager.ExitScanning();
            ClearOutline(_currentTarget);
            _currentTarget = null;
        }

        // ── Raycast ───────────────────────────────────────────────

        private void UpdateRaycast()
        {
            var ray = new Ray(_camera.transform.position, _camera.transform.forward);

            if (Physics.Raycast(ray, out var hit, _config.scanRaycastDistance, _config.scanLayerMask))
            {
                var scannable = hit.collider.GetComponentInParent<ScannableObject>();

                if (scannable != _currentTarget)
                {
                    ClearOutline(_currentTarget);
                    _currentTarget = scannable;
                    ApplyOutline(_currentTarget);

                    if (_currentTarget != null && _currentTarget.Data != null)
                        _scannerService.BeginSession(_currentTarget.Data);
                }
            }
            else
            {
                if (_currentTarget != null)
                {
                    ClearOutline(_currentTarget);
                    _currentTarget = null;
                    _scannerService.EndSession();
                }
            }
        }

        // ── Outline Helpers ───────────────────────────────────────
        // LIMITATION: временный outline через MaterialPropertyBlock.
        // Полноценный HDRP CustomPass outline — в Этапе 8.

        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

        private void ApplyOutline(ScannableObject target)
        {
            if (target == null) return;
            foreach (var r in target.GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(_mpb);
                _mpb.SetFloat(OutlineWidthId, 0.02f);
                r.SetPropertyBlock(_mpb);
            }
        }

        private void ClearOutline(ScannableObject target)
        {
            if (target == null) return;
            foreach (var r in target.GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(_mpb);
                _mpb.SetFloat(OutlineWidthId, 0f);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
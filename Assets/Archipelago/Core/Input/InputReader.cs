using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Archipelago.Core
{
    /// <summary>
    /// Single access point for all input actions.
    /// ScriptableObject — инжектится через Zenject.
    ///
    /// События названы с суффиксом существительного (Moved, Scanned и т.д.),
    /// чтобы не конфликтовать с методами интерфейса IGameplayActions / IScannerActions
    /// (OnMove, OnScan и т.д.) — C# иначе путает event и метод.
    ///
    /// THREAD: main thread only.
    /// </summary>
    [CreateAssetMenu(fileName = "InputReader", menuName = "Archipelago/Input/InputReader")]
    public sealed class InputReader : ScriptableObject,
        ArchipelagoInputActions.IGameplayActions,
        ArchipelagoInputActions.IScannerActions
    {
        // ── Gameplay Events ──────────────────────────────────────

        public event Action<Vector2> Moved;
        public event Action<Vector2> Looked;
        public event Action          Interacted;
        public event Action          SprintStarted;
        public event Action          SprintCancelled;

        /// <summary>ПКМ зажата — режим сканирования активен.</summary>
        public event Action ScanHeld;

        /// <summary>ПКМ отпущена — режим сканирования выключен.</summary>
        public event Action ScanReleased;

        /// <summary>Tab в Gameplay map — открыть панель диалога.</summary>
        public event Action PanelOpenRequested;

        // ── Scanner Events ───────────────────────────────────────

        /// <summary>Enter в Scanner map — отправить запрос.</summary>
        public event Action SubmitRequested;

        /// <summary>Tab / Escape в Scanner map — закрыть панель.</summary>
        public event Action PanelCloseRequested;

        // ── State ────────────────────────────────────────────────

        /// <summary>True пока ПКМ зажата. InputRouter использует для блокировки Tab.</summary>
        public bool IsScanHeld { get; private set; }

        private ArchipelagoInputActions _actions;

        // ── Lifecycle ────────────────────────────────────────────

        private void OnEnable()
        {
            if (_actions == null)
            {
                _actions = new ArchipelagoInputActions();
                _actions.Gameplay.SetCallbacks(this);
                _actions.Scanner.SetCallbacks(this);
            }

            _actions.Gameplay.Enable();
            _actions.Scanner.Disable();
        }

        private void OnDisable()
        {
            _actions?.Gameplay.Disable();
            _actions?.Scanner.Disable();
        }

        // ── Map Switching ─────────────────────────────────────────

        public void EnableGameplayMap()
        {
            _actions.Scanner.Disable();
            _actions.Gameplay.Enable();
        }

        public void EnableScannerMap()
        {
            _actions.Gameplay.Disable();
            _actions.Scanner.Enable();
        }

        // ── IGameplayActions ──────────────────────────────────────

        void ArchipelagoInputActions.IGameplayActions.OnMove(InputAction.CallbackContext ctx)
            => Moved?.Invoke(ctx.ReadValue<Vector2>());

        void ArchipelagoInputActions.IGameplayActions.OnLook(InputAction.CallbackContext ctx)
            => Looked?.Invoke(ctx.ReadValue<Vector2>());

        void ArchipelagoInputActions.IGameplayActions.OnInteract(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) Interacted?.Invoke();
        }

        void ArchipelagoInputActions.IGameplayActions.OnSprint(InputAction.CallbackContext ctx)
        {
            if (ctx.started)  SprintStarted?.Invoke();
            if (ctx.canceled) SprintCancelled?.Invoke();
        }

        void ArchipelagoInputActions.IGameplayActions.OnScan(InputAction.CallbackContext ctx)
        {
            Debug.Log($"[InputReader] OnScan phase: {ctx.phase}");
            if (ctx.started)
            {
                IsScanHeld = true;
                ScanHeld?.Invoke();
            }
            else if (ctx.canceled)
            {
                IsScanHeld = false;
                ScanReleased?.Invoke();
            }
        }

        void ArchipelagoInputActions.IGameplayActions.OnOpenPanel(InputAction.CallbackContext ctx)
        {
            Debug.Log($"[InputReader] OnOpenPanel phase: {ctx.phase}, IsScanHeld: {IsScanHeld}");
            if (ctx.performed && !IsScanHeld)
                PanelOpenRequested?.Invoke();
        }

        // Заглушки для actions которые есть в asset но не используются в игровой логике.
        // Если понадобятся — добавить события по аналогии выше.
        void ArchipelagoInputActions.IGameplayActions.OnNext(InputAction.CallbackContext ctx) { }
        void ArchipelagoInputActions.IGameplayActions.OnPrevious(InputAction.CallbackContext ctx) { }

        // ── IScannerActions ───────────────────────────────────────

        void ArchipelagoInputActions.IScannerActions.OnSubmit(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) SubmitRequested?.Invoke();
        }

        void ArchipelagoInputActions.IScannerActions.OnClosePanel(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) PanelCloseRequested?.Invoke();
        }
    }
}
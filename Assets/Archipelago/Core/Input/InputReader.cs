using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Archipelago.Core
{
    /// <summary>
    /// Single access point for all input actions.
    /// ScriptableObject — инжектится через Zenject.
    ///
    /// Tab short press  → PanelOpenRequested  (or CircleSearchCloseRequested when overlay is open)
    /// Tab hold ≥0.4s   → CircleSearchOpenRequested
    /// While IsCircleSearchOpen: Moved and Looked events are suppressed.
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

        public event Action ScanHeld;
        public event Action ScanReleased;

        /// <summary>Tab short release in Gameplay map — open inventory/chat panel.</summary>
        public event Action PanelOpenRequested;

        /// <summary>Tab short release while CircleSearch overlay is open — close it.</summary>
        public event Action CircleSearchCloseRequested;

        /// <summary>Tab held ≥0.4s in Gameplay map — open CircleSearch overlay.</summary>
        public event Action CircleSearchOpenRequested;

        // ── Scanner Events ───────────────────────────────────────

        public event Action SubmitRequested;
        public event Action PanelCloseRequested;

        // ── State ────────────────────────────────────────────────

        /// <summary>True while RMB is held (legacy, kept for backward compat).</summary>
        public bool IsScanHeld { get; private set; }

        /// <summary>
        /// Set by CircleSearchController when the overlay is active.
        /// Suppresses Moved/Looked so the camera freezes.
        /// </summary>
        public bool IsCircleSearchOpen { get; set; }

        // Tracks whether CircleSearch.performed fired for the current Tab press,
        // so the subsequent OpenPanel.performed (release) can be suppressed.
        private bool _circleSearchFiredThisPress;

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
        {
            if (!IsCircleSearchOpen)
                Moved?.Invoke(ctx.ReadValue<Vector2>());
        }

        void ArchipelagoInputActions.IGameplayActions.OnLook(InputAction.CallbackContext ctx)
        {
            if (!IsCircleSearchOpen)
                Looked?.Invoke(ctx.ReadValue<Vector2>());
        }

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

            if (ctx.started)
            {
                _circleSearchFiredThisPress = false;
                return;
            }

            if (!ctx.performed) return;
            if (IsScanHeld) return;

            if (_circleSearchFiredThisPress)
            {
                _circleSearchFiredThisPress = false;
                return;
            }

            if (IsCircleSearchOpen)
                CircleSearchCloseRequested?.Invoke();
            else
            {
                PanelOpenRequested?.Invoke();
            }
        }

        void ArchipelagoInputActions.IGameplayActions.OnCircleSearch(InputAction.CallbackContext ctx)
        {
            // Hold interaction: performed fires when Tab has been held ≥ hold duration.
            if (ctx.performed && !IsCircleSearchOpen)
            {
                _circleSearchFiredThisPress = true;
                CircleSearchOpenRequested?.Invoke();
            }
        }

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

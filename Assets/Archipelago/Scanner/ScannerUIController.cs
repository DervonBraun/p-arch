using System;
using Archipelago.Core;
using MessagePipe;
using UnityEngine;
using UnityEngine.UIElements;
using Zenject;

namespace Archipelago.Scanner
{
    /// <summary>
    /// Управляет UI панелью сканера (UI Toolkit).
    /// Показывает ответы AI, поле ввода уточняющего вопроса, статус загрузки.
    ///
    /// Требует UIDocument на том же GameObject.
    /// UXML должен содержать элементы с именами указанными в константах ниже.
    ///
    /// НЕ знает про токены, raycast или SessionManager напрямую —
    /// только подписан на MessagePipe события.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ScannerUIController : MonoBehaviour
    {
        // ── UXML Element Names ────────────────────────────────────

        private const string PanelName      = "scanner-panel";
        private const string DialogTextName = "scanner-dialog-text";
        private const string InputFieldName = "scanner-input-field";
        private const string SubmitBtnName  = "scanner-submit-btn";
        private const string LoadingName    = "scanner-loading";
        private const string ObjectNameEl   = "scanner-object-name";

        // ── Zenject ───────────────────────────────────────────────

        [Inject] private ISubscriber<ScanRequestedMessage>  _requestSub;
        [Inject] private ISubscriber<ScanCompletedMessage>  _completedSub;
        [Inject] private ScannerService                     _scannerService;

        // ── UI Elements ───────────────────────────────────────────

        private VisualElement _panel;
        private Label         _dialogText;
        private TextField     _inputField;
        private Button        _submitBtn;
        private VisualElement _loadingIndicator;
        private Label         _objectNameLabel;

        // ── State ────────────────────────────────────────────────

        private IDisposable _subscriptions;

        // ── Unity Lifecycle ──────────────────────────────────────

        private void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            BindElements(root);
            SetupCallbacks();
            SetPanelVisible(false);

            var bag = DisposableBag.CreateBuilder();
            _requestSub  .Subscribe(OnScanRequested) .AddTo(bag);
            _completedSub.Subscribe(OnScanCompleted) .AddTo(bag);
            _subscriptions = bag.Build();
        }

        private void OnDestroy()
        {
            _subscriptions?.Dispose();
        }

        // ── Element Binding ───────────────────────────────────────

        private void BindElements(VisualElement root)
        {
            _panel            = root.Q(PanelName);
            _dialogText       = root.Q<Label>(DialogTextName);
            _inputField       = root.Q<TextField>(InputFieldName);
            _submitBtn        = root.Q<Button>(SubmitBtnName);
            _loadingIndicator = root.Q(LoadingName);
            _objectNameLabel  = root.Q<Label>(ObjectNameEl);

            if (_panel == null)
                Debug.LogError($"[ScannerUI] Element '{PanelName}' not found in UXML.");
        }

        private void SetupCallbacks()
        {
            _submitBtn?.RegisterCallback<ClickEvent>(_ => SubmitQuery());

            // Submit по Enter в текстовом поле
            _inputField?.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                    SubmitQuery();
            });
        }

        // ── MessagePipe Handlers ──────────────────────────────────

        private void OnScanRequested(ScanRequestedMessage msg)
        {
            SetPanelVisible(true);
            SetLoading(true);
            _submitBtn?.SetEnabled(false);

            if (_objectNameLabel != null)
                _objectNameLabel.text = msg.ObjectId;
        }

        private void OnScanCompleted(ScanCompletedMessage msg)
        {
            SetLoading(false);
            _submitBtn?.SetEnabled(true);

            if (_dialogText != null)
                _dialogText.text = msg.ResponseText;

            // Фокус на поле ввода для быстрого follow-up
            _inputField?.Focus();
        }

        // ── Input ─────────────────────────────────────────────────

        private void SubmitQuery()
        {
            var query = _inputField?.value?.Trim();
            if (string.IsNullOrEmpty(query)) return;
            if (_scannerService.IsScanning) return;

            _scannerService.SendFollowUp(query);

            if (_inputField != null)
                _inputField.value = string.Empty;
        }

        // ── Helpers ───────────────────────────────────────────────

        private void SetPanelVisible(bool visible)
        {
            if (_panel == null) return;
            _panel.style.display = visible
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        private void SetLoading(bool loading)
        {
            if (_loadingIndicator != null)
                _loadingIndicator.style.display = loading
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
        }
    }
}
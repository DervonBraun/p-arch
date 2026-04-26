using System;
using Archipelago.Core;
using Archipelago.Session;
using MessagePipe;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Archipelago.Scanner
{
    /// <summary>
    /// Scanner UI на uGUI Canvas + TextMeshPro.
    /// Закрытие панели — через InputReader.OnClosePanel (Tab / Escape в Scanner map).
    /// Enter для сабмита — через InputReader.OnSubmit (Scanner map).
    ///
    /// Иерархия Canvas:
    ///   ScannerCanvas (Canvas, CanvasScaler, GraphicRaycaster)
    ///   └── ScannerPanel (RectTransform, CanvasGroup)
    ///       ├── HeaderLabel      (TextMeshProUGUI)
    ///       ├── ObjectIdLabel    (TextMeshProUGUI)
    ///       ├── ResponseScroll   (ScrollRect)
    ///       │   └── Viewport/Content
    ///       │       └── ResponseText  (TextMeshProUGUI)
    ///       ├── LoadingLabel     (TextMeshProUGUI)
    ///       ├── InputRow         (HorizontalLayoutGroup)
    ///       │   ├── InputField   (TMP_InputField)
    ///       │   └── SubmitButton (Button → TextMeshProUGUI "→")
    ///       └── HintLabel        (TextMeshProUGUI)
    /// </summary>
    public sealed class ScannerUIController : MonoBehaviour
    {
        // ── Injected ─────────────────────────────────────────────

        [Inject] private ISubscriber<ScanRequestedMessage>       _requestSub;
        [Inject] private ISubscriber<ScanCompletedMessage>       _completedSub;
        [Inject] private ISubscriber<SessionStateChangedMessage> _sessionSub;
        [Inject] private ScannerService                          _scannerService;
        [Inject] private SessionManager                          _sessionManager;
        [Inject] private InputReader                             _inputReader;

        // ── Canvas references (assign in Inspector) ───────────────

        [Header("Root")]
        [SerializeField] private CanvasGroup _panelGroup;

        [Header("Labels")]
        [SerializeField] private TMP_Text _headerLabel;
        [SerializeField] private TMP_Text _objectIdLabel;
        [SerializeField] private TMP_Text _responseText;
        [SerializeField] private TMP_Text _loadingLabel;
        [SerializeField] private TMP_Text _hintLabel;

        [Header("Scroll")]
        [SerializeField] private ScrollRect _responseScroll;

        [Header("Input")]
        [SerializeField] private TMP_InputField _inputField;
        [SerializeField] private Button         _submitButton;

        // ── State ────────────────────────────────────────────────

        private bool        _loading;
        private IDisposable _subscriptions;

        // ── Unity Lifecycle ──────────────────────────────────────

        private void Start()
        {
            var bag = DisposableBag.CreateBuilder();
            _requestSub  .Subscribe(OnScanRequested)      .AddTo(bag);
            _completedSub.Subscribe(OnScanCompleted)      .AddTo(bag);
            _sessionSub  .Subscribe(OnSessionStateChanged).AddTo(bag);
            _subscriptions = bag.Build();

            _submitButton.onClick.AddListener(SubmitQuery);

            if (_hintLabel != null)
                _hintLabel.text = "[Tab/Esc] закрыть  |  [Enter] отправить";

            SetPanelVisible(false, immediate: true);
            SetResponseText("Наведи сканер на объект...");
        }

        private void OnEnable()
        {
            _inputReader.SubmitRequested     += SubmitQuery;
            _inputReader.PanelCloseRequested += HandleClosePanel;
        }

        private void OnDisable()
        {
            _inputReader.SubmitRequested     -= SubmitQuery;
            _inputReader.PanelCloseRequested -= HandleClosePanel;
        }

        private void OnDestroy()
        {
            _subscriptions?.Dispose();
            _submitButton.onClick.RemoveListener(SubmitQuery);
        }

        // ── Input Handlers ────────────────────────────────────────

        private void HandleClosePanel()
        {
            _sessionManager.ExitScanning();
        }

        // ── MessagePipe Handlers ──────────────────────────────────

        private void OnSessionStateChanged(SessionStateChangedMessage msg)
        {
            bool entering = msg.Next == SessionState.Scanning;
            SetPanelVisible(entering, immediate: false);

            if (!entering)
            {
                SetResponseText("Наведи сканер на объект...");
                if (_objectIdLabel != null) _objectIdLabel.text = "";
                if (_inputField    != null) _inputField.text    = "";
                _loading = false;
            }

            // Всегда обновляем interactable — и при открытии и при закрытии
            RefreshInteractable();
        }

        private void OnScanRequested(ScanRequestedMessage msg)
        {
            if (_objectIdLabel != null)
                _objectIdLabel.text = msg.ObjectId.ToUpper();

            SetResponseText("");
            _loading = true;
            RefreshInteractable();

            if (_loadingLabel != null) _loadingLabel.gameObject.SetActive(true);
        }

        private void OnScanCompleted(ScanCompletedMessage msg)
        {
            SetResponseText(msg.ResponseText);
            _loading = false;
            RefreshInteractable();

            if (_loadingLabel != null) _loadingLabel.gameObject.SetActive(false);

            // PERF: ForceUpdateCanvases вызываем только при получении ответа, не в Update.
            if (_responseScroll != null)
                Canvas.ForceUpdateCanvases();
        }

        // ── Submit ────────────────────────────────────────────────

        private void SubmitQuery()
        {
            if (_loading) return;
            if (_scannerService == null || _scannerService.IsScanning) return;

            var query = _inputField != null ? _inputField.text.Trim() : "";
            if (string.IsNullOrEmpty(query)) return;

            _scannerService.SendFollowUp(query);

            if (_inputField != null)
            {
                _inputField.text = "";
                _inputField.ActivateInputField();
            }
        }

        // ── Helpers ───────────────────────────────────────────────

        private void SetPanelVisible(bool visible, bool immediate)
        {
            if (_panelGroup == null) return;
            _panelGroup.gameObject.SetActive(visible);

            if (visible && _inputField != null)
                StartCoroutine(FocusNextFrame());
        }

        private System.Collections.IEnumerator FocusNextFrame()
        {
            yield return null;  // ждём один кадр
            _inputField.ActivateInputField();
            _inputField.Select();
        }

        private void SetResponseText(string text)
        {
            if (_responseText != null)
                _responseText.text = text;
        }

        private void RefreshInteractable()
        {
            bool canSubmit = !_loading &&
                             _scannerService != null &&
                             !_scannerService.IsScanning;

            Debug.Log($"[ScannerUI] RefreshInteractable: loading={_loading}, " +
                      $"serviceNull={_scannerService == null}, " +
                      $"isScanning={_scannerService?.IsScanning}, " +
                      $"canSubmit={canSubmit}");

            if (_submitButton != null) _submitButton.interactable = canSubmit;
            if (_inputField   != null) _inputField.interactable   = canSubmit;
        }
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using Archipelago.Core;
using Archipelago.Economy;
using Archipelago.Session;
using MessagePipe;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Archipelago.Scanner
{
    /// <summary>
    /// Scanner chat panel.
    ///
    /// Canvas hierarchy (assign all [SerializeField] refs in Inspector):
    ///   ScannerCanvas  (Canvas, Screen Space - Overlay)
    ///   └── ScannerPanel  (CanvasGroup)
    ///       ├── ChipsRow  (HorizontalLayoutGroup, ScrollRect optional)
    ///       │   └── [spawned chip GOs]
    ///       ├── ChatScroll  (ScrollRect)
    ///       │   └── Viewport → ChatContent  (VerticalLayoutGroup)
    ///       │       └── [spawned message rows]
    ///       ├── ReasoningBar  (hidden by default)
    ///       │   ├── BarBackground  (Image)
    ///       │   ├── BarFill        (Image, filled horizontal)
    ///       │   └── StageText      (TMP_Text)
    ///       └── InputRow
    ///           ├── CostLabel   (TMP_Text)
    ///           ├── InputField  (TMP_InputField)
    ///           └── SendButton  (Button)
    ///
    /// ZENJECT NOTE: OnEnable/OnDisable fire before [Inject] fields are assigned.
    /// All subscriptions are guarded by _injected flag and deferred to Construct().
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
        [Inject] private ScanCollection                          _collection;
        [Inject] private EconomyConfig                           _economyConfig;

        // ── Canvas References (assign in Inspector) ───────────────

        [Header("Root")]
        [SerializeField] private CanvasGroup _panelGroup;

        [Header("Object Chips")]
        [SerializeField] private RectTransform _chipsContent;
        [SerializeField] private Sprite        _defaultChipIcon;

        [Header("Chat")]
        [SerializeField] private ScrollRect    _chatScroll;
        [SerializeField] private RectTransform _chatContent;
        [SerializeField] private TMP_Text      _emptyLabel;     // "Добавьте объекты через CircleSearch"

        [Header("Reasoning Mode Progress Bar")]
        [SerializeField] private GameObject _reasoningBar;
        [SerializeField] private Image      _barFill;
        [SerializeField] private TMP_Text   _stageText;

        [Header("Input Row")]
        [SerializeField] private TMP_Text       _costLabel;
        [SerializeField] private TMP_InputField _inputField;
        [SerializeField] private Button         _sendButton;

        // ── State ────────────────────────────────────────────────

        private bool        _loading;
        private bool        _isReasoningMode;
        private float       _reasoningStartTime;
        private IDisposable _subscriptions;

        // Guard: OnEnable/OnDisable fire before Zenject [Inject] completes.
        // All event subscriptions are deferred until Construct() is called.
        private bool _injected;

        private readonly List<GameObject> _chipGOs    = new();
        private readonly List<GameObject> _messageGOs = new();

        private static readonly string[] ReasoningStages =
        {
            "Сбор данных...",
            "Анализ...",
            "Формирую ответ..."
        };

        // ── Zenject Inject Point ──────────────────────────────────

        /// <summary>
        /// Called by Zenject after all [Inject] fields are assigned.
        /// Safe entry point for any initialization that requires injected deps.
        /// </summary>
        [Inject]
        private void Construct()
        {
            _injected = true;
            if (isActiveAndEnabled)
                SubscribeInputReader();
        }

        // ── Unity Lifecycle ──────────────────────────────────────

        private void Start()
        {
            // _injected is guaranteed true by Start() — Zenject inject is always
            // complete before Start(). This assert catches misconfigured scenes.
            if (!_injected)
            {
                Debug.LogError("[ScannerUIController] Start() called before Zenject inject. " +
                               "Check SceneContext installer order.");
                return;
            }

            var bag = DisposableBag.CreateBuilder();
            _requestSub  .Subscribe(OnScanRequested)      .AddTo(bag);
            _completedSub.Subscribe(OnScanCompleted)      .AddTo(bag);
            _sessionSub  .Subscribe(OnSessionStateChanged).AddTo(bag);
            _subscriptions = bag.Build();

            _sendButton.onClick.AddListener(SubmitQuery);

            SetPanelVisible(false, immediate: true);
        }

        private void OnEnable()
        {
            if (!_injected) return;
            SubscribeInputReader();
        }

        private void OnDisable()
        {
            if (!_injected) return;
            UnsubscribeInputReader();
        }

        private void OnDestroy()
        {
            _subscriptions?.Dispose();

            if (_sendButton != null)
                _sendButton.onClick.RemoveListener(SubmitQuery);

            // Safe unsubscribe — _injected may be false if destroyed before inject.
            if (_injected)
                UnsubscribeInputReader();
        }

        private void Update()
        {
            // Guard: _panelGroup is [SerializeField] so it's set, but _scannerService
            // and _economyConfig are [Inject] — both must be ready before Update runs.
            if (!_injected) return;
            if (_panelGroup == null || !_panelGroup.gameObject.activeSelf) return;

            UpdateCostLabel();

            if (_loading && _isReasoningMode)
                UpdateReasoningBar();
        }

        // ── Input Subscription Helpers ────────────────────────────

        private void SubscribeInputReader()
        {
            _inputReader.PanelOpenRequested  += HandleOpenPanel;
            _inputReader.SubmitRequested     += SubmitQuery;
            _inputReader.PanelCloseRequested += HandleClosePanel;
        }

        private void UnsubscribeInputReader()
        {
            _inputReader.PanelOpenRequested  -= HandleOpenPanel;   // <-- добавить
            _inputReader.SubmitRequested     -= SubmitQuery;
            _inputReader.PanelCloseRequested -= HandleClosePanel;
        }

        private void HandleOpenPanel()
        {
            _sessionManager.EnterScanning();
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

            if (entering)
            {
                RebuildChips();
                ClearChat();
                if (_collection.Count == 0 && _emptyLabel != null)
                    _emptyLabel.gameObject.SetActive(true);
                StartCoroutine(FocusNextFrame());
            }
            else
            {
                ClearChips();
                ClearChat();
                _loading         = false;
                _isReasoningMode = false;
                SetReasoningBarVisible(false);
            }

            RefreshInteractable();
        }

        private void OnScanRequested(ScanRequestedMessage msg)
        {
            _loading = true;
            RefreshInteractable();

            AddMessageRow(msg.UserQuery, isUser: true);

            if (_emptyLabel != null) _emptyLabel.gameObject.SetActive(false);
            SetReasoningBarVisible(_isReasoningMode);
            if (_isReasoningMode) _reasoningStartTime = Time.realtimeSinceStartup;
        }

        private void OnScanCompleted(ScanCompletedMessage msg)
        {
            _loading         = false;
            _isReasoningMode = false;
            SetReasoningBarVisible(false);
            RefreshInteractable();

            AddMessageRow(msg.ResponseText, isUser: false);
            ScrollChatToBottom();
        }

        // ── Submit ────────────────────────────────────────────────

        private void SubmitQuery()
        {
            if (_loading || _scannerService.IsScanning) return;
            if (_collection.Count == 0) return;

            string query = _inputField != null ? _inputField.text.Trim() : "";
            if (string.IsNullOrEmpty(query)) return;

            _isReasoningMode = ScanCostCalculator.IsReasoningMode(query, _economyConfig);

            _scannerService.SendQuery(query);

            if (_inputField != null)
            {
                _inputField.text = "";
                _inputField.ActivateInputField();
            }
        }

        // ── Chips ─────────────────────────────────────────────────

        private void RebuildChips()
        {
            ClearChips();
            foreach (var entry in _collection.Entries)
                SpawnChip(entry);
        }

        private void SpawnChip(ScanCollectionEntry entry)
        {
            var go = new GameObject($"Chip_{entry.Data.objectId}");
            go.transform.SetParent(_chipsContent, worldPositionStays: false);

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth  = 80f;
            layout.preferredHeight = 80f;
            layout.flexibleWidth   = 0f;

            // Thumbnail image
            var imgGO = new GameObject("Thumb");
            imgGO.transform.SetParent(go.transform, false);
            var img = imgGO.AddComponent<Image>();
            img.sprite = entry.Thumbnail != null ? entry.Thumbnail : _defaultChipIcon;
            var imgRT = imgGO.GetComponent<RectTransform>();
            imgRT.anchorMin = Vector2.zero;
            imgRT.anchorMax = Vector2.one;
            imgRT.offsetMin = new Vector2(4, 20);
            imgRT.offsetMax = new Vector2(-4, -4);

            // Name label
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(go.transform, false);
            var label = labelGO.AddComponent<TextMeshProUGUI>();
            label.text      = entry.Data.displayName;
            label.fontSize  = 9f;
            label.alignment = TextAlignmentOptions.Bottom;
            label.color     = Color.white;
            var labelRT = labelGO.GetComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0, 0);
            labelRT.anchorMax = new Vector2(1, 0.3f);
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;

            // Remove button
            var btnGO = new GameObject("RemoveBtn");
            btnGO.transform.SetParent(go.transform, false);
            var btnImg = btnGO.AddComponent<Image>();
            btnImg.color = new Color(0.8f, 0.2f, 0.2f, 0.9f);
            var btn  = btnGO.AddComponent<Button>();
            var btnRT = btnGO.GetComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(1, 1);
            btnRT.anchorMax = new Vector2(1, 1);
            btnRT.pivot     = new Vector2(1, 1);
            btnRT.sizeDelta = new Vector2(16, 16);
            btnRT.anchoredPosition = Vector2.zero;

            var xGO = new GameObject("X");
            xGO.transform.SetParent(btnGO.transform, false);
            var xText = xGO.AddComponent<TextMeshProUGUI>();
            xText.text      = "×";
            xText.fontSize  = 12f;
            xText.alignment = TextAlignmentOptions.Center;
            var xRT = xGO.GetComponent<RectTransform>();
            xRT.anchorMin = Vector2.zero;
            xRT.anchorMax = Vector2.one;
            xRT.offsetMin = xRT.offsetMax = Vector2.zero;

            string objectId = entry.Data.objectId;
            btn.onClick.AddListener(() => RemoveChip(objectId));

            _chipGOs.Add(go);
        }

        private void RemoveChip(string objectId)
        {
            _collection.Remove(objectId);
            RebuildChips();
            RefreshInteractable();
        }

        private void ClearChips()
        {
            foreach (var go in _chipGOs)
                Destroy(go);
            _chipGOs.Clear();
        }

        // ── Chat Messages ─────────────────────────────────────────

        private void AddMessageRow(string text, bool isUser)
        {
            var go = new GameObject(isUser ? "UserMsg" : "AssistantMsg");
            go.transform.SetParent(_chatContent, worldPositionStays: false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = 14f;
            tmp.color     = isUser ? new Color(0.7f, 0.9f, 1f) : Color.white;
            tmp.alignment = isUser ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;

            var le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;

            var csf = go.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _messageGOs.Add(go);
        }

        private void ClearChat()
        {
            foreach (var go in _messageGOs)
                Destroy(go);
            _messageGOs.Clear();
        }

        private void ScrollChatToBottom()
        {
            Canvas.ForceUpdateCanvases();
            if (_chatScroll != null)
                _chatScroll.verticalNormalizedPosition = 0f;
        }

        // ── Reasoning Bar ─────────────────────────────────────────

        private void UpdateReasoningBar()
        {
            float elapsed = Time.realtimeSinceStartup - _reasoningStartTime;
            float t     = Mathf.Clamp01(elapsed / 20f);
            float eased = t < 0.8f ? t / 0.8f * 0.9f : 0.9f + (t - 0.8f) / 0.2f * 0.09f;

            if (_barFill   != null) _barFill.fillAmount = eased;
            if (_stageText != null)
            {
                int stage = eased < 0.33f ? 0 : eased < 0.66f ? 1 : 2;
                _stageText.text = ReasoningStages[stage];
            }
        }

        private void SetReasoningBarVisible(bool visible)
        {
            if (_reasoningBar != null) _reasoningBar.SetActive(visible);
            if (_barFill      != null) _barFill.fillAmount = 0f;
        }

        // ── Cost Label ────────────────────────────────────────────

        private void UpdateCostLabel()
        {
            // Guard all four deps — _scannerService and _economyConfig are [Inject],
            // original code only checked the [SerializeField] refs.
            if (_costLabel      == null) return;
            if (_inputField     == null) return;
            if (_scannerService == null) return;  // FIXED: was missing, caused NullRef line 367
            if (_economyConfig  == null) return;  // FIXED: was missing, caused NullRef line 368

            string query = _inputField.text;
            var cost = ScanCostCalculator.Calculate(
                _collection.Count,
                query,
                _scannerService.IsFirstRequest,
                _economyConfig);

            _costLabel.text = cost.Red > 0 || cost.Green > 0
                ? $"{cost.Red}  {cost.Green}"
                : "";
        }

        // ── Helpers ───────────────────────────────────────────────

        private void SetPanelVisible(bool visible, bool immediate)
        {
            if (_panelGroup == null) return;
            _panelGroup.gameObject.SetActive(visible);
        }

        private void RefreshInteractable()
        {
            bool canSend = !_loading && !_scannerService.IsScanning && _collection.Count > 0;
            if (_sendButton != null) _sendButton.interactable = canSend;
            if (_inputField != null) _inputField.interactable = canSend;
        }

        private IEnumerator FocusNextFrame()
        {
            yield return null;
            if (_inputField != null)
            {
                _inputField.ActivateInputField();
                _inputField.Select();
            }
        }
    }
}
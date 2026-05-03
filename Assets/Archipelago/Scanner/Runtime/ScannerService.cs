using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Archipelago.Core;
using Archipelago.Economy;
using Archipelago.PlayerProfile;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using Zenject;

namespace Archipelago.Scanner
{
    /// <summary>
    /// Orchestrates scanner queries.
    ///
    /// Этап 5 additions:
    ///   — Проверка FlagService.IsBlocked перед отправкой
    ///   — BuildProfileString() инжектируется в system[1]
    ///   — ApplyFlags() из JSON ответа сервера
    ///   — TrackStrangeScan() / TrackFollowUpQuery() для метрик
    /// </summary>
    public sealed class ScannerService : IInitializable, IDisposable
    {
        // ── State ─────────────────────────────────────────────────

        public bool IsScanning     { get; private set; }
        public bool IsFirstRequest => _session.IsFirstScan;

        private readonly ScanSession _session = new();
        private CancellationTokenSource _cts;
        private IDisposable              _sessionSub;

        // ── Dependencies ──────────────────────────────────────────

        private readonly GroqClient                              _groqClient;
        private readonly ScanCache                               _cache;
        private readonly ScannerConfig                           _config;
        private readonly ScanCollection                          _collection;
        private readonly EconomyConfig                           _economyConfig;
        private readonly TokenService                            _tokenService;
        private readonly PlayerProfileTracker                    _profileTracker;
        private readonly FlagService                             _flagService;
        private readonly IPublisher<ScanRequestedMessage>        _requestPub;
        private readonly IPublisher<ScanCompletedMessage>        _completedPub;
        private readonly ISubscriber<SessionStateChangedMessage> _stateSub;

        [Inject]
        public ScannerService(
            GroqClient                               groqClient,
            ScanCache                                cache,
            ScannerConfig                            config,
            EconomyConfig                            economyConfig,
            TokenService                             tokenService,
            PlayerProfileTracker                     profileTracker,
            FlagService                              flagService,
            ScanCollection                           collection,
            IPublisher<ScanRequestedMessage>         requestPub,
            IPublisher<ScanCompletedMessage>         completedPub,
            ISubscriber<SessionStateChangedMessage>  stateSub)
        {
            _groqClient     = groqClient;
            _cache          = cache;
            _config         = config;
            _economyConfig  = economyConfig;
            _tokenService   = tokenService;
            _profileTracker = profileTracker;
            _flagService    = flagService;
            _collection     = collection;
            _requestPub     = requestPub;
            _completedPub   = completedPub;
            _stateSub       = stateSub;
        }

        // ── IInitializable / IDisposable ─────────────────────────

        public void Initialize()
        {
            _sessionSub = _stateSub.Subscribe(OnSessionStateChanged);
        }

        public void Dispose()
        {
            _sessionSub?.Dispose();
            _cts?.Cancel();
            _cts?.Dispose();
        }

        // ── Public API ────────────────────────────────────────────

        public void SendQuery(string userQuery)
        {
            if (IsScanning)
            {
                Debug.LogWarning("[ScannerService] Request already in progress.");
                return;
            }

            // ── Блокировка (ABUSE >= 3) ───────────────────────────
            if (_flagService.IsBlocked)
            {
                var remaining = _flagService.BlockedRemaining;
                Debug.LogWarning($"[ScannerService] Scanner blocked. Remaining: {remaining.Minutes}m {remaining.Seconds}s");
                return;
            }

            if (_session.AttachedObjects.Count == 0)
            {
                Debug.LogWarning("[ScannerService] No objects attached to session.");
                return;
            }

            if (_cts == null || _cts.IsCancellationRequested)
                _cts = new CancellationTokenSource();

            SendQueryAsync(userQuery, _cts.Token).Forget();
        }

        // ── Session Lifecycle ─────────────────────────────────────

        private void OnSessionStateChanged(SessionStateChangedMessage msg)
        {
            if (msg.Next == SessionState.Scanning)
            {
                var objects = _collection.Entries.Select(e => e.Data).ToList();
                _session.Begin(objects);

                _cts?.Cancel();
                _cts?.Dispose();
                _cts = new CancellationTokenSource();
            }
            else if (msg.Previous == SessionState.Scanning)
            {
                _cts?.Cancel();
                _session.End();
                IsScanning = false;
            }
        }

        // ── Internal ─────────────────────────────────────────────

        private async UniTaskVoid SendQueryAsync(string query, CancellationToken ct)
        {
            var objects = _session.AttachedObjects;
            if (objects.Count == 0) return;

            // ── Cost check ────────────────────────────────────────
            var cost = ScanCostCalculator.Calculate(
                attachedObjectCount: objects.Count,
                questionText:        query,
                isFirstRequest:      _session.IsFirstScan,
                config:              _economyConfig);

            if (!_tokenService.CanAfford(TokenType.Red, cost.Red))
            {
                Debug.Log($"[ScannerService] Insufficient red: need {cost.Red}");
                return;
            }
            if (cost.Green > 0 && !_tokenService.CanAfford(TokenType.Green, cost.Green))
            {
                Debug.Log($"[ScannerService] Insufficient green: need {cost.Green}");
                return;
            }

            if (cost.Red   > 0) _tokenService.Spend(TokenType.Red,   cost.Red,   "scan_query");
            if (cost.Green > 0) _tokenService.Spend(TokenType.Green, cost.Green, "scan_init");
            // ─────────────────────────────────────────────────────

            // Трекинг уточняющего вопроса
            if (!_session.IsFirstScan)
                _profileTracker.TrackFollowUpQuery();

            IsScanning = true;
            _session.AddUserMessage(query);

            string compositeId = string.Join(",", objects.Select(o => o.objectId));
            _requestPub.Publish(new ScanRequestedMessage(compositeId, query));

            string responseText;
            bool   fromCache = false;

            try
            {
                var request = BuildRequest(objects);

                try
                {
                    var response = await _groqClient.SendScanRequestAsync(request, ct);
                    responseText = response.Response;

                    // Применяем флаги из ответа сервера
                    if (response.Flags != null && response.Flags.Length > 0)
                        _flagService.ApplyFlags(response.Flags);
                }
                catch (GroqClientException ex)
                {
                    Debug.LogWarning($"[ScannerService] Server error: {ex.Message}");
                    responseText = _config.GetRandomFallback();
                }
                catch (OperationCanceledException)
                {
                    IsScanning = false;
                    return;
                }

                _session.AddAssistantMessage(responseText);
                IsScanning = false;
                _completedPub.Publish(new ScanCompletedMessage(compositeId, responseText, fromCache));
            }
            catch (OperationCanceledException)
            {
                IsScanning = false;
            }
        }

        private GroqClient.ScanRequest BuildRequest(IReadOnlyList<ScannableObjectSO> objects)
        {
            // Пересчитываем флаги перед каждым запросом
            _flagService.RefreshBehaviorFlags();

            var messages = new List<GroqClient.MessageDto>();
            foreach (var msg in _session.GetHistory())
                messages.Add(new GroqClient.MessageDto { Role = msg.Role, Content = msg.Content });

            return new GroqClient.ScanRequest
            {
                ObjectIds     = objects.Select(o => o.objectId).ToList(),
                Messages      = messages,
                PlayerId      = _economyConfig.DevPlayerId,
                PlayerProfile = _profileTracker.BuildProfileString(),
            };
        }
    }
}

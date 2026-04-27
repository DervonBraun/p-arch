using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Archipelago.Core;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using Zenject;

namespace Archipelago.Scanner
{
    /// <summary>
    /// Orchestrates scanner queries.
    ///
    /// Lifecycle:
    ///   SessionStateChangedMessage(→ Scanning)  → OpenSession from current ScanCollection
    ///   SessionStateChangedMessage(Scanning → *) → CloseSession
    ///   SendQuery(text)                          → POST to Groq proxy, publish ScanCompleted
    ///
    /// No auto-query on session open. First query always originates from the player.
    /// Cost tracking (IsFirstRequest) is delegated to ScanSession.IsFirstScan.
    ///
    /// NOT responsible for UI or token deduction — other systems subscribe to messages.
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
        private readonly IPublisher<ScanRequestedMessage>        _requestPub;
        private readonly IPublisher<ScanCompletedMessage>        _completedPub;
        private readonly ISubscriber<SessionStateChangedMessage> _stateSub;

        private const string DevPlayerId = "dev_player_01";

        [Inject]
        public ScannerService(
            GroqClient                               groqClient,
            ScanCache                                cache,
            ScannerConfig                            config,
            ScanCollection                           collection,
            IPublisher<ScanRequestedMessage>         requestPub,
            IPublisher<ScanCompletedMessage>         completedPub,
            ISubscriber<SessionStateChangedMessage>  stateSub)
        {
            _groqClient   = groqClient;
            _cache        = cache;
            _config       = config;
            _collection   = collection;
            _requestPub   = requestPub;
            _completedPub = completedPub;
            _stateSub     = stateSub;
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

        /// <summary>
        /// Sends a player query against all currently attached objects.
        /// Fires ScanRequestedMessage immediately, ScanCompletedMessage on response.
        /// </summary>
        public void SendQuery(string userQuery)
        {
            if (IsScanning)
            {
                Debug.LogWarning("[ScannerService] Request already in progress.");
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
            var messages = new List<GroqClient.MessageDto>();
            foreach (var msg in _session.GetHistory())
                messages.Add(new GroqClient.MessageDto { Role = msg.Role, Content = msg.Content });

            return new GroqClient.ScanRequest
            {
                ObjectIds = objects.Select(o => o.objectId).ToList(),
                Messages  = messages,
                PlayerId  = DevPlayerId
            };
        }
    }
}

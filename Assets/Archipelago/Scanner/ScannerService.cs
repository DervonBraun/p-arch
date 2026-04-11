using System;
using System.Collections.Generic;
using System.Threading;
using Archipelago.Core;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using Zenject;

namespace Archipelago.Scanner
{
    /// <summary>
    /// Оркестратор сканирования. Знает про:
    ///   - текущую ScanSession (история диалога)
    ///   - ScanCache (оффлайн fallback)
    ///   - GroqClient (HTTP)
    ///   - промпт-сборку (три system-блока + история + вопрос)
    ///
    /// НЕ знает про:
    ///   - UI (публикует события, UI подписан)
    ///   - токены (TokenService подписан на ScanCompletedMessage)
    ///   - raycast (это ScannerController)
    /// </summary>
    public sealed class ScannerService : IInitializable, IDisposable
    {
        // ── State ────────────────────────────────────────────────

        public bool IsScanning { get; private set; }

        private readonly ScanSession _session = new();
        private CancellationTokenSource _cts;

        // ── Dependencies ─────────────────────────────────────────

        private readonly GroqClient                         _groqClient;
        private readonly ScanCache                          _cache;
        private readonly ScannerConfig                      _config;
        private readonly IPublisher<ScanRequestedMessage>   _requestPub;
        private readonly IPublisher<ScanCompletedMessage>   _completedPub;

        private const string DevPlayerId = "dev_player_01";

        [Inject]
        public ScannerService(
            GroqClient                       groqClient,
            ScanCache                        cache,
            ScannerConfig                    config,
            IPublisher<ScanRequestedMessage>  requestPub,
            IPublisher<ScanCompletedMessage>  completedPub)
        {
            _groqClient   = groqClient;
            _cache        = cache;
            _config       = config;
            _requestPub   = requestPub;
            _completedPub = completedPub;
        }

        // ── IInitializable / IDisposable ─────────────────────────

        public void Initialize() { }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        // ── Public API ───────────────────────────────────────────

        public void BeginSession(ScannableObjectSO obj)
        {
            _session.Begin(obj);
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            SendQueryAsync("Что это?", isPrimary: true, _cts.Token).Forget();
        }

        public void SendFollowUp(string userQuery)
        {
            if (_session.CurrentObject == null)
            {
                Debug.LogWarning("[ScannerService] SendFollowUp called without active session.");
                return;
            }

            if (IsScanning)
            {
                Debug.LogWarning("[ScannerService] Request already in progress.");
                return;
            }

            SendQueryAsync(userQuery, isPrimary: false, _cts.Token).Forget();
        }

        public void EndSession()
        {
            _cts?.Cancel();
            _session.End();
            IsScanning = false;
        }

        // ── Internal ─────────────────────────────────────────────

        private async UniTaskVoid SendQueryAsync(
            string query,
            bool isPrimary,
            CancellationToken ct)
        {
            var obj = _session.CurrentObject;
            if (obj == null) return;

            IsScanning = true;
            _session.AddUserMessage(query);
            _requestPub.Publish(new ScanRequestedMessage(obj.objectId, query));

            string responseText;
            bool   fromCache = false;

            try
            {
                if (isPrimary && _cache.HasEntry(obj.objectId))
                {
                    responseText = _cache.Get(obj.objectId);
                    fromCache    = true;
                    Debug.Log($"[ScannerService] Cache hit for '{obj.objectId}'");
                }
                else
                {
                    var request = BuildRequest(obj);

                    try
                    {
                        var response = await _groqClient.SendScanRequestAsync(request, ct);
                        responseText = response.Response;
                    }
                    catch (GroqClientException ex)
                    {
                        Debug.LogWarning($"[ScannerService] Server error: {ex.Message}");
                        responseText = isPrimary && _cache.HasEntry(obj.objectId)
                            ? _cache.Get(obj.objectId)
                            : _config.GetRandomFallback();
                        fromCache = isPrimary && _cache.HasEntry(obj.objectId);
                    }
                    catch (OperationCanceledException)
                    {
                        IsScanning = false;
                        return;
                    }

                    if (isPrimary)
                        _cache.Set(obj.objectId, responseText);
                }

                _session.AddAssistantMessage(responseText);

                // FIX: используем имя параметра из конструктора ScanCompletedMessage — fromCache
                _completedPub.Publish(new ScanCompletedMessage(obj.objectId, responseText, fromCache));
            }
            finally
            {
                IsScanning = false;
            }
        }

        private GroqClient.ScanRequest BuildRequest(ScannableObjectSO obj)
        {
            var messages = new List<GroqClient.MessageDto>();

            foreach (var msg in _session.GetHistory())
            {
                messages.Add(new GroqClient.MessageDto
                {
                    Role    = msg.Role,
                    Content = msg.Content
                });
            }

            return new GroqClient.ScanRequest
            {
                ObjectId = obj.objectId,
                Messages = messages,
                PlayerId = DevPlayerId
            };
        }
    }
}
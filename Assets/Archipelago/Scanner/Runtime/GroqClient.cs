using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Archipelago.Scanner
{
    /// <summary>
    /// HTTP клиент для Railway proxy → Groq API.
    /// НЕ ходит напрямую в Groq — API-ключ никогда не на клиенте.
    ///
    /// Все запросы асинхронные (UniTask). Retry с exponential backoff.
    /// При недоступности сервера бросает GroqClientException.
    ///
    /// THREAD: Вызывать только с main thread (UnityWebRequest ограничение).
    /// </summary>
    public sealed class GroqClient
    {
        // ── Request / Response DTOs ───────────────────────────────

        public sealed class ScanRequest
        {
            [JsonProperty("objectIds")]
            public List<string> ObjectIds { get; set; }

            [JsonProperty("messages")]
            public List<MessageDto> Messages { get; set; }

            [JsonProperty("playerId")]
            public string PlayerId { get; set; }
            
            [JsonProperty("playerProfile")]
            public string PlayerProfile { get; set; }
        }

        public sealed class MessageDto
        {
            [JsonProperty("role")]
            public string Role { get; set; }

            [JsonProperty("content")]
            public string Content { get; set; }
        }

        public sealed class ScanResponse
        {
            [JsonProperty("response")]
            public string Response { get; set; }

            [JsonProperty("blueTokensAwarded")]
            public int BlueTokensAwarded { get; set; }

            [JsonProperty("flags")]
            public string[] Flags { get; set; }   // <-- новый

            [JsonProperty("tone")]
            public string Tone { get; set; }      // <-- новый
        }

        // ── Private ──────────────────────────────────────────────

        private readonly ScannerConfig _config;
        private string ScanEndpoint => $"{_config.serverBaseUrl.TrimEnd('/')}/scan";

        // ── Constructor ──────────────────────────────────────────

        public GroqClient(ScannerConfig config)
        {
            _config = config;
        }

        // ── Public API ───────────────────────────────────────────

        /// <summary>
        /// Отправляет запрос на Railway proxy и возвращает ответ.
        /// Бросает <see cref="GroqClientException"/> при исчерпании retry.
        /// </summary>
        public async UniTask<ScanResponse> SendScanRequestAsync(
            ScanRequest request,
            System.Threading.CancellationToken ct = default)
        {
            var json    = JsonConvert.SerializeObject(request);
            var bodyRaw = Encoding.UTF8.GetBytes(json);

            int attempt = 0;
            while (true)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var response = await SendOnceAsync(bodyRaw, ct);
                    return response;
                }
                catch (GroqClientException ex) when (ex.IsRetryable && attempt < _config.maxRetries - 1)
                {
                    attempt++;
                    float delay = Mathf.Pow(2f, attempt); // 2s, 4s, 8s
                    Debug.LogWarning($"[GroqClient] Attempt {attempt} failed: {ex.Message}. " +
                                     $"Retrying in {delay}s...");
                    await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: ct);
                }
            }
        }

        // ── Internal ─────────────────────────────────────────────

        private async UniTask<ScanResponse> SendOnceAsync(
            byte[] bodyRaw,
            System.Threading.CancellationToken ct)
        {
            using var req = new UnityWebRequest(ScanEndpoint, "POST");
            req.uploadHandler   = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = Mathf.RoundToInt(_config.requestTimeoutSeconds);

            await req.SendWebRequest()
                     .WithCancellation(ct)
                     .SuppressCancellationThrow();

            ct.ThrowIfCancellationRequested();

            if (req.result == UnityWebRequest.Result.ConnectionError ||
                req.result == UnityWebRequest.Result.DataProcessingError)
            {
                throw new GroqClientException(req.error, isRetryable: true);
            }

            if (req.result == UnityWebRequest.Result.ProtocolError)
            {
                // 4xx — не ретраим (клиентская ошибка)
                // 5xx — ретраим (серверная ошибка)
                bool retryable = req.responseCode >= 500;
                throw new GroqClientException(
                    $"HTTP {req.responseCode}: {req.downloadHandler.text}",
                    isRetryable: retryable);
            }

            try
            {
                var result = JsonConvert.DeserializeObject<ScanResponse>(req.downloadHandler.text);
                if (result == null)
                    throw new GroqClientException("Empty response from server.", isRetryable: false);
                return result;
            }
            catch (JsonException ex)
            {
                throw new GroqClientException($"Failed to parse response: {ex.Message}", isRetryable: false);
            }
        }
    }

    /// <summary>Exception thrown by GroqClient on non-recoverable errors.</summary>
    public sealed class GroqClientException : Exception
    {
        public bool IsRetryable { get; }

        public GroqClientException(string message, bool isRetryable)
            : base(message)
        {
            IsRetryable = isRetryable;
        }
    }
}
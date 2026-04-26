using System;
using System.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Archipelago.Economy
{
    /// <summary>
    /// HTTP-клиент для Railway сервера.
    /// Все методы — UniTask, не блокируют main thread.
    /// Retry с exponential backoff на сетевые ошибки (не на 4xx).
    ///
    /// THREAD: вызывается только с main thread (UnityWebRequest требование).
    /// </summary>
    public sealed class ServerBridge
    {
        private readonly EconomyConfig _config;

        // ── Server response DTOs ──────────────────────────────────
        // LIMITATION: все DTO — отдельные классы, не иерархия.
        // Inheritance от sealed классов запрещена C# компилятором.

        public sealed class BalanceResponse
        {
            [JsonProperty("red")]   public int Red   { get; set; }
            [JsonProperty("green")] public int Green { get; set; }
            [JsonProperty("blue")]  public int Blue  { get; set; }

            public TokenBalance ToBalance() => new(Red, Green, Blue);
        }

        // FIXED: был : BalanceResponse — нельзя наследоваться от sealed.
        // Дублируем поля red/green/blue явно.
        public sealed class SpendEarnResponse
        {
            [JsonProperty("red")]    public int Red   { get; set; }
            [JsonProperty("green")]  public int Green { get; set; }
            [JsonProperty("blue")]   public int Blue  { get; set; }
            [JsonProperty("delta")]  public int Delta { get; set; }

            public TokenBalance ToBalance() => new(Red, Green, Blue);
        }

        public sealed class ErrorResponse
        {
            [JsonProperty("error")]    public string Error    { get; set; }
            [JsonProperty("type")]     public string Type     { get; set; }
            [JsonProperty("required")] public int    Required { get; set; }
            [JsonProperty("current")]  public int    Current  { get; set; }
        }

        // ── Result type ───────────────────────────────────────────

        public readonly struct BridgeResult<T>
        {
            public readonly bool          Success;
            public readonly T             Data;
            public readonly int           HttpStatus;
            public readonly ErrorResponse Error;

            private BridgeResult(bool ok, T data, int status, ErrorResponse err)
            {
                Success    = ok;
                Data       = data;
                HttpStatus = status;
                Error      = err;
            }

            public static BridgeResult<T> Ok(T data, int status = 200)
                => new(true, data, status, null);

            public static BridgeResult<T> Fail(ErrorResponse err, int status)
                => new(false, default, status, err);

            public static BridgeResult<T> NetworkFail()
                => new(false, default, 0, new ErrorResponse { Error = "Network error" });
        }

        // ── Constructor ───────────────────────────────────────────

        public ServerBridge(EconomyConfig config)
        {
            _config = config;
        }

        // ── Public API ────────────────────────────────────────────

        /// <summary>GET /tokens/{playerId} — получить баланс с сервера.</summary>
        public async UniTask<BridgeResult<BalanceResponse>> GetBalanceAsync(string playerId)
        {
            string url = $"{_config.ServerBaseUrl}/tokens/{Uri.EscapeDataString(playerId)}";
            return await SendWithRetryAsync<BalanceResponse>(url, "GET", null);
        }

        /// <summary>POST /tokens/{playerId}/earn — начислить токены.</summary>
        public async UniTask<BridgeResult<SpendEarnResponse>> EarnAsync(
            string playerId, TokenType type, int amount, string source)
        {
            string url  = $"{_config.ServerBaseUrl}/tokens/{Uri.EscapeDataString(playerId)}/earn";
            var    body = new { type = TokenTypeToString(type), amount, source };
            return await SendWithRetryAsync<SpendEarnResponse>(url, "POST", body);
        }

        /// <summary>
        /// POST /tokens/{playerId}/spend — списать токены.
        /// PERF: нет retry — 402 это бизнес-ошибка, не сетевой сбой.
        /// </summary>
        public async UniTask<BridgeResult<SpendEarnResponse>> SpendAsync(
            string playerId, TokenType type, int amount, string reason)
        {
            string url  = $"{_config.ServerBaseUrl}/tokens/{Uri.EscapeDataString(playerId)}/spend";
            var    body = new { type = TokenTypeToString(type), amount, reason };
            return await SendAsync<SpendEarnResponse>(url, "POST", body);
        }

        // ── Internal ──────────────────────────────────────────────

        private async UniTask<BridgeResult<T>> SendWithRetryAsync<T>(
            string url, string method, object body)
        {
            int   attempts = 0;
            float delay    = _config.RetryBaseDelaySeconds;

            while (true)
            {
                var result = await SendAsync<T>(url, method, body);

                if (result.Success || (result.HttpStatus >= 400 && result.HttpStatus < 500))
                    return result;

                attempts++;
                if (attempts > _config.RetryCount)
                    return BridgeResult<T>.NetworkFail();

                Debug.LogWarning(
                    $"[ServerBridge] Retry {attempts}/{_config.RetryCount} " +
                    $"after {delay:F1}s (status={result.HttpStatus})");

                await UniTask.Delay(TimeSpan.FromSeconds(delay));
                delay *= 2f;
            }
        }

        private async UniTask<BridgeResult<T>> SendAsync<T>(
            string url, string method, object body)
        {
            string jsonBody = body != null ? JsonConvert.SerializeObject(body) : null;

            using var req = new UnityWebRequest(url, method);
            req.timeout = Mathf.RoundToInt(_config.RequestTimeoutSeconds);

            if (jsonBody != null)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(bytes);
                req.uploadHandler.contentType = "application/json";
            }

            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Accept",       "application/json");

            try
            {
                await req.SendWebRequest().ToUniTask();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Debug.LogError($"[ServerBridge] Network exception {method} {url}: {ex.Message}");
                return BridgeResult<T>.NetworkFail();
            }

            int    status       = (int)req.responseCode;
            string responseText = req.downloadHandler?.text ?? "";

            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var data = JsonConvert.DeserializeObject<T>(responseText);
                    return BridgeResult<T>.Ok(data, status);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ServerBridge] JSON parse error: {ex.Message}\n{responseText}");
                    return BridgeResult<T>.NetworkFail();
                }
            }

            ErrorResponse errBody = null;
            try   { errBody = JsonConvert.DeserializeObject<ErrorResponse>(responseText); }
            catch { errBody = new ErrorResponse { Error = responseText }; }

            Debug.LogWarning($"[ServerBridge] {method} {url} → {status}: {errBody?.Error}");
            return BridgeResult<T>.Fail(errBody, status);
        }

        // ── Helpers ───────────────────────────────────────────────

        private static string TokenTypeToString(TokenType t) => t switch
        {
            TokenType.Red   => "red",
            TokenType.Green => "green",
            TokenType.Blue  => "blue",
            _               => throw new ArgumentOutOfRangeException(nameof(t)),
        };
    }
}
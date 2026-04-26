using System;
using Archipelago.Core;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;

namespace Archipelago.Economy
{
    /// <summary>
    /// Фасад для всех операций с токенами.
    /// Регистрируется в ServiceLocator как ITokenService.
    /// 
    /// Поток spend:
    ///   1. Optimistic update в TokenWallet (мгновенно)
    ///   2. Публикация TokensChangedMessage → UI обновляется сразу
    ///   3. HTTP запрос к серверу (async)
    ///   4. При успехе — ApplyServerBalance (reconcile)
    ///   5. При сбое — rollback + публикация TokensInsufficientMessage
    /// 
    /// THREAD: все public методы — main thread only.
    /// </summary>
    public sealed class TokenService : IDisposable
    {
        // ── Dependencies ──────────────────────────────────────────

        private readonly EconomyConfig                       _config;
        private readonly TokenWallet                         _wallet;
        private readonly ServerBridge                        _bridge;
        private readonly IPublisher<TokensChangedMessage>    _changedPub;
        private readonly IPublisher<TokensSyncedMessage>     _syncedPub;
        private readonly IPublisher<TokensInsufficientMessage> _insufficientPub;

        // ── State ─────────────────────────────────────────────────

        private bool _initialized;
        private bool _disposed;

        /// <summary>Текущий кэшированный баланс.</summary>
        public TokenBalance Balance => _wallet.Balance;

        /// <summary>true если wallet инициализирован (баланс загружен с сервера).</summary>
        public bool IsInitialized => _initialized;

        // ── Constructor ───────────────────────────────────────────

        public TokenService(
            EconomyConfig                         config,
            ServerBridge                          bridge,
            IPublisher<TokensChangedMessage>      changedPub,
            IPublisher<TokensSyncedMessage>       syncedPub,
            IPublisher<TokensInsufficientMessage> insufficientPub)
        {
            _config          = config;
            _bridge          = bridge;
            _changedPub      = changedPub;
            _syncedPub       = syncedPub;
            _insufficientPub = insufficientPub;
            _wallet          = new TokenWallet(_config.SyncQueueMaxSize);
        }

        // ── Initialization ────────────────────────────────────────

        /// <summary>
        /// Загружает баланс с сервера. Вызывается при старте игры из GameBootstrapper.
        /// </summary>
        public async UniTask InitializeAsync()
        {
            var result = await _bridge.GetBalanceAsync(_config.DevPlayerId);

            if (result.Success)
            {
                var balance = result.Data.ToBalance();
                _wallet.ApplyServerBalance(balance);
                _initialized = true;
                _syncedPub.Publish(new TokensSyncedMessage(balance));
                Debug.Log($"[TokenService] Initialized: {balance}");
            }
            else
            {
                // Offline start — начинаем с нулевого баланса,
                // синхронизируем когда появится связь
                _wallet.SetBalance(TokenBalance.Zero);
                _initialized = true;
                Debug.LogWarning("[TokenService] Offline start — balance is Zero");
            }
        }

        // ── Public API ────────────────────────────────────────────

        /// <summary>
        /// Проверяет достаточно ли токенов без списания.
        /// </summary>
        public bool CanAfford(TokenType type, int amount)
            => _wallet.Balance.Get(type) >= amount;

        /// <summary>
        /// Списывает токены. Optimistic update мгновенный.
        /// Серверная синхронизация — fire-and-forget с rollback при сбое.
        /// </summary>
        /// <returns>false если баланс недостаточен (локально).</returns>
        public bool Spend(TokenType type, int amount, string reason)
        {
            if (amount <= 0)
            {
                Debug.LogWarning($"[TokenService] Spend called with non-positive amount: {amount}");
                return false;
            }

            var old = _wallet.Balance;

            if (!_wallet.ApplyOptimistic(type, -amount, reason))
            {
                _insufficientPub.Publish(new TokensInsufficientMessage(
                    type, amount, old.Get(type)));
                return false;
            }

            PublishChanged(old, _wallet.Balance, type, -amount, reason);
            SyncSpendAsync(type, amount, reason, old).Forget();
            return true;
        }

        /// <summary>
        /// Начисляет токены. Optimistic update мгновенный.
        /// </summary>
        public void Earn(TokenType type, int amount, string source)
        {
            if (amount <= 0) return;

            var old = _wallet.Balance;
            _wallet.ApplyOptimistic(type, amount, source);
            PublishChanged(old, _wallet.Balance, type, amount, source);
            SyncEarnAsync(type, amount, source).Forget();
        }

        /// <summary>
        /// Принудительная синхронизация баланса с сервером.
        /// Вызывать при восстановлении сети.
        /// </summary>
        public async UniTask ForceSyncAsync()
        {
            var result = await _bridge.GetBalanceAsync(_config.DevPlayerId);
            if (!result.Success) return;

            var old     = _wallet.Balance;
            var balance = result.Data.ToBalance();
            _wallet.ApplyServerBalance(balance);

            // Публикуем изменение для каждого типа если расходится
            foreach (TokenType t in new[] { TokenType.Red, TokenType.Green, TokenType.Blue })
            {
                int delta = balance.Get(t) - old.Get(t);
                if (delta != 0)
                    PublishChanged(old, balance, t, delta, "server_sync");
            }

            _syncedPub.Publish(new TokensSyncedMessage(balance));
            Debug.Log($"[TokenService] Force sync complete: {balance}");
        }

        // ── Internal sync ─────────────────────────────────────────

        // PERF: fire-and-forget, не блокирует вызывающий код
        private async UniTaskVoid SyncSpendAsync(
            TokenType type, int amount, string reason, TokenBalance balanceBeforeOptimistic)
        {
            var result = await _bridge.SpendAsync(_config.DevPlayerId, type, amount, reason);

            if (result.Success)
            {
                // Сервер вернул authoritative баланс — reconcile
                _wallet.ApplyServerBalance(result.Data.ToBalance());
                _syncedPub.Publish(new TokensSyncedMessage(_wallet.Balance));
                return;
            }

            if (result.HttpStatus == 402)
            {
                // Сервер отклонил — rollback optimistic update
                Debug.LogWarning($"[TokenService] Server rejected spend {type} {amount}: insufficient");
                RollbackOptimistic(type, amount, reason);

                var err = result.Error;
                _insufficientPub.Publish(new TokensInsufficientMessage(
                    type,
                    err?.Required ?? amount,
                    err?.Current  ?? 0));
                return;
            }

            // Сетевая ошибка — операция в pending queue (уже добавлена в wallet)
            // При следующем ForceSyncAsync reconcile произойдёт
            Debug.LogWarning(
                $"[TokenService] Spend sync failed (status={result.HttpStatus}), queued for retry");
        }

        private async UniTaskVoid SyncEarnAsync(TokenType type, int amount, string source)
        {
            var result = await _bridge.EarnAsync(_config.DevPlayerId, type, amount, source);

            if (result.Success)
            {
                _wallet.ApplyServerBalance(result.Data.ToBalance());
                _syncedPub.Publish(new TokensSyncedMessage(_wallet.Balance));
            }
            else
            {
                Debug.LogWarning(
                    $"[TokenService] Earn sync failed (status={result.HttpStatus}), queued");
            }
        }

        private void RollbackOptimistic(TokenType type, int amount, string reason)
        {
            var old = _wallet.Balance;
            // Применяем обратную операцию без добавления в queue
            _wallet.SetBalance(_wallet.Balance.Apply(type, amount));
            PublishChanged(old, _wallet.Balance, type, amount, $"rollback:{reason}");
        }

        // ── Helpers ───────────────────────────────────────────────

        private void PublishChanged(
            TokenBalance old, TokenBalance next,
            TokenType type, int delta, string reason)
        {
            _changedPub.Publish(new TokensChangedMessage(old, next, type, delta, reason));
        }

        public void Dispose() => _disposed = true;
    }
}
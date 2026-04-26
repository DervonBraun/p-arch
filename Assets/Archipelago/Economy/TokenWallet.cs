using System;
using System.Collections.Generic;
using UnityEngine;

namespace Archipelago.Economy
{
    /// <summary>
    /// Локальный кэш баланса токенов.
    /// Canonical значение — на сервере. Wallet хранит optimistic копию
    /// и очередь несинхронизированных операций для offline-режима.
    /// 
    /// THREAD: все операции вызываются только с main thread.
    /// </summary>
    public sealed class TokenWallet
    {
        // ── State ─────────────────────────────────────────────────

        private TokenBalance _balance = TokenBalance.Zero;

        /// <summary>Текущий кэшированный баланс (optimistic).</summary>
        public TokenBalance Balance => _balance;

        // ── Pending sync queue ────────────────────────────────────

        public readonly struct PendingOp
        {
            public readonly TokenType Type;
            public readonly int       Delta;   // >0 earn, <0 spend
            public readonly string    Reason;
            public readonly DateTime  Timestamp;

            public PendingOp(TokenType type, int delta, string reason)
            {
                Type      = type;
                Delta     = delta;
                Reason    = reason;
                Timestamp = DateTime.UtcNow;
            }
        }

        private readonly Queue<PendingOp> _pendingOps;
        private readonly int              _maxQueueSize;

        public bool HasPendingOps => _pendingOps.Count > 0;
        public int  PendingCount  => _pendingOps.Count;

        public TokenWallet(int maxQueueSize = 64)
        {
            _maxQueueSize = maxQueueSize;
            _pendingOps   = new Queue<PendingOp>(maxQueueSize);
        }

        // ── Optimistic update ─────────────────────────────────────

        /// <summary>
        /// Применяет изменение локально (optimistic).
        /// Ставит операцию в очередь для последующей синхронизации.
        /// </summary>
        /// <returns>false если баланс уйдёт в минус (операция отклонена).</returns>
        public bool ApplyOptimistic(TokenType type, int delta, string reason)
        {
            int current = _balance.Get(type);
            int next    = current + delta;

            if (next < 0)
            {
                Debug.LogWarning(
                    $"[TokenWallet] Optimistic rejected: {type} {delta} " +
                    $"(current={current}, would be {next})");
                return false;
            }

            _balance = _balance.Apply(type, delta);

            if (_pendingOps.Count < _maxQueueSize)
            {
                _pendingOps.Enqueue(new PendingOp(type, delta, reason));
            }
            else
            {
                Debug.LogWarning(
                    $"[TokenWallet] Sync queue full ({_maxQueueSize}), dropping op: {type} {delta}");
            }

            return true;
        }

        /// <summary>
        /// Применяет authoritative баланс с сервера.
        /// Сбрасывает pending очередь (операции уже учтены сервером).
        /// </summary>
        public void ApplyServerBalance(TokenBalance serverBalance)
        {
            _balance = serverBalance;
            _pendingOps.Clear();
        }

        /// <summary>
        /// Принудительная установка баланса без сброса очереди.
        /// Используется при инициализации из кэша.
        /// </summary>
        public void SetBalance(TokenBalance balance)
        {
            _balance = balance;
        }

        // ── Pending queue access ──────────────────────────────────

        /// <summary>Снимает одну операцию из головы очереди.</summary>
        public bool TryDequeue(out PendingOp op) => _pendingOps.TryDequeue(out op);

        /// <summary>Возвращает операцию обратно в голову очереди при сбое.</summary>
        public void RequeueFront(PendingOp op)
        {
            // Queue не поддерживает prepend — пересобираем
            var tmp = new Queue<PendingOp>(_pendingOps.Count + 1);
            tmp.Enqueue(op);
            while (_pendingOps.TryDequeue(out var existing))
                tmp.Enqueue(existing);

            _pendingOps.Clear();
            while (tmp.TryDequeue(out var item))
                _pendingOps.Enqueue(item);
        }

        public override string ToString()
            => $"Wallet[{_balance}] pending={_pendingOps.Count}";
    }
}
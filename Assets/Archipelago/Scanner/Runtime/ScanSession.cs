using System.Collections.Generic;

namespace Archipelago.Scanner
{
    /// <summary>
    /// Хранит историю диалога одной сессии сканера.
    /// Создаётся при открытии панели, сбрасывается при закрытии.
    ///
    /// Сессия может содержать несколько объектов (из ScanCollection).
    /// Максимум MaxMessages пар user/assistant — старые вытесняются (sliding window).
    /// </summary>
    public sealed class ScanSession
    {
        public const int MaxMessages = 8;

        public readonly struct Message
        {
            public readonly string Role;     // "user" | "assistant"
            public readonly string Content;

            public Message(string role, string content)
            {
                Role    = role;
                Content = content;
            }
        }

        // ── State ────────────────────────────────────────────────

        public IReadOnlyList<ScannableObjectSO> AttachedObjects { get; private set; }
            = System.Array.Empty<ScannableObjectSO>();

        public bool IsFirstScan   { get; private set; } = true;
        public int  FollowUpCount { get; private set; }

        private readonly List<Message> _history = new(MaxMessages * 2);

        // ── Public API ───────────────────────────────────────────

        public void Begin(IReadOnlyList<ScannableObjectSO> objects)
        {
            AttachedObjects = objects ?? System.Array.Empty<ScannableObjectSO>();
            IsFirstScan     = true;
            FollowUpCount   = 0;
            _history.Clear();
        }

        public void End()
        {
            AttachedObjects = System.Array.Empty<ScannableObjectSO>();
            _history.Clear();
            IsFirstScan   = true;
            FollowUpCount = 0;
        }

        public void AddUserMessage(string content)
        {
            TrimIfNeeded();
            _history.Add(new Message("user", content));
            if (!IsFirstScan) FollowUpCount++;
            IsFirstScan = false;
        }

        public void AddAssistantMessage(string content)
            => _history.Add(new Message("assistant", content));

        public IReadOnlyList<Message> GetHistory() => _history;

        // ── Internal ─────────────────────────────────────────────

        private void TrimIfNeeded()
        {
            while (_history.Count >= MaxMessages * 2)
            {
                if (_history.Count >= 2)
                {
                    _history.RemoveAt(0);
                    _history.RemoveAt(0);
                }
                else break;
            }
        }
    }
}

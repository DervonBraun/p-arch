using System.Collections.Generic;

namespace Archipelago.Scanner
{
    /// <summary>
    /// Хранит историю диалога одной сессии сканирования.
    /// Создаётся при открытии сканера, уничтожается при закрытии.
    ///
    /// Максимум MaxMessages пар user/assistant.
    /// При превышении — вытесняется самое старое сообщение (sliding window).
    /// Первое сообщение (автоматический "что это?") не считается за follow-up.
    /// </summary>
    public sealed class ScanSession
    {
        public const int MaxMessages = 4;

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

        public ScannableObjectSO CurrentObject { get; private set; }
        public bool IsFirstScan               { get; private set; } = true;
        public int  FollowUpCount             { get; private set; }

        private readonly List<Message> _history = new(MaxMessages * 2);

        // ── Public API ───────────────────────────────────────────

        /// <summary>Инициализирует сессию для конкретного объекта.</summary>
        public void Begin(ScannableObjectSO obj)
        {
            CurrentObject = obj;
            IsFirstScan   = true;
            FollowUpCount = 0;
            _history.Clear();
        }

        /// <summary>Сбрасывает сессию при закрытии сканера.</summary>
        public void End()
        {
            CurrentObject = null;
            _history.Clear();
            IsFirstScan   = true;
            FollowUpCount = 0;
        }

        /// <summary>Добавляет сообщение пользователя в историю.</summary>
        public void AddUserMessage(string content)
        {
            TrimIfNeeded();
            _history.Add(new Message("user", content));

            if (!IsFirstScan) FollowUpCount++;
            IsFirstScan = false;
        }

        /// <summary>Добавляет ответ ассистента в историю.</summary>
        public void AddAssistantMessage(string content)
        {
            _history.Add(new Message("assistant", content));
        }

        /// <summary>Возвращает копию истории для сборки промпта.</summary>
        public IReadOnlyList<Message> GetHistory() => _history;

        // ── Internal ─────────────────────────────────────────────

        // Если история переполнена — убираем старейшую пару user+assistant
        private void TrimIfNeeded()
        {
            // _history содержит чередующиеся user/assistant пары
            // MaxMessages ограничивает количество пар
            while (_history.Count >= MaxMessages * 2)
            {
                // Удаляем первую пару (user + assistant)
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
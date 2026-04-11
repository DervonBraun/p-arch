// ============================================================
//  АРХИПЕЛАГ — MessagePipe Message Definitions
//
//  All inter-system communication goes through typed structs.
//  No system holds a direct reference to another.
//
//  Naming:  <Noun><PastTenseVerb>Message
//  Binding: Registered in ProjectInstaller via
//           container.BindMessageBroker<T>()
// ============================================================

namespace Archipelago.Core
{
    // ── Clock ────────────────────────────────────────────────

    /// <summary>
    /// Published every in-game tick by GameClock.
    /// Frequency = GameClock.TickInterval (default: every real frame).
    /// Do NOT use for per-frame logic — prefer Update() or UniTask loops.
    /// </summary>
    public readonly struct GameTickMessage
    {
        public readonly float TotalGameTime;   // accumulated in-game seconds
        public readonly float DeltaGameTime;   // in-game delta (not Time.deltaTime)
        public readonly int   DayIndex;        // 0-based

        public GameTickMessage(float total, float delta, int day)
        {
            TotalGameTime = total;
            DeltaGameTime = delta;
            DayIndex      = day;
        }
    }

    /// <summary>Published once when in-game midnight crosses to a new day.</summary>
    public readonly struct DayChangedMessage
    {
        public readonly int NewDayIndex;
        public DayChangedMessage(int day) => NewDayIndex = day;
    }

    // ── Session ──────────────────────────────────────────────

    /// <summary>Published on every FSM transition in SessionManager.</summary>
    public readonly struct SessionStateChangedMessage
    {
        public readonly SessionState Previous;
        public readonly SessionState Next;

        public SessionStateChangedMessage(SessionState prev, SessionState next)
        {
            Previous = prev;
            Next     = next;
        }
    }

    // ── Economy ──────────────────────────────────────────────

    /// <summary>
    /// Published after any confirmed token balance change.
    /// HUD subscribes to update counters.
    /// </summary>
    public readonly struct TokensChangedMessage
    {
        public readonly TokenType TokenType;
        public readonly int       Delta;       // positive = earned, negative = spent
        public readonly int       NewBalance;

        public TokensChangedMessage(TokenType type, int delta, int newBalance)
        {
            TokenType  = type;
            Delta      = delta;
            NewBalance = newBalance;
        }
    }

    // ── Effects ──────────────────────────────────────────────

    /// <summary>Published when an effect is applied or its stack count changes.</summary>
    public readonly struct EffectAppliedMessage
    {
        public readonly string EffectId;
        public readonly int    CurrentStacks;
        public readonly float  RemainingTime;

        public EffectAppliedMessage(string id, int stacks, float remaining)
        {
            EffectId      = id;
            CurrentStacks = stacks;
            RemainingTime = remaining;
        }
    }

    /// <summary>Published when an effect expires (timer hits zero).</summary>
    public readonly struct EffectExpiredMessage
    {
        public readonly string EffectId;
        public EffectExpiredMessage(string id) => EffectId = id;
    }

    // ── Routine ──────────────────────────────────────────────

    /// <summary>
    /// Published by RoutineManager on action completion.
    /// EffectService subscribes → applies effect.
    /// TokenService subscribes → awards blue tokens.
    /// </summary>
    public readonly struct RoutineCompletedMessage
    {
        public readonly string RoutineId;  // "eat" | "clean" | "water_garden"
        public readonly float  Quality;    // 0–1 reward scalar

        public RoutineCompletedMessage(string id, float quality)
        {
            RoutineId = id;
            Quality   = quality;
        }
    }

    // ── Scanner ──────────────────────────────────────────────

    /// <summary>Published when scanner fires a request (for UI loading indicator).</summary>
    public readonly struct ScanRequestedMessage
    {
        public readonly string ObjectId;
        public readonly string UserQuery;

        public ScanRequestedMessage(string objectId, string query)
        {
            ObjectId  = objectId;
            UserQuery = query;
        }
    }

    /// <summary>Published when Groq API (or cache) returns a completed response.</summary>
    public readonly struct ScanCompletedMessage
    {
        public readonly string ObjectId;
        public readonly string ResponseText;
        public readonly bool   IsFromCache;

        public ScanCompletedMessage(string objectId, string text, bool fromCache)
        {
            ObjectId     = objectId;
            ResponseText = text;
            IsFromCache  = fromCache;
        }
    }

    // ── Mini-Games ───────────────────────────────────────────

    /// <summary>Published by MiniGameManager when a game starts.</summary>
    public readonly struct MiniGameStartedMessage
    {
        public readonly string MiniGameId;
        public MiniGameStartedMessage(string id) => MiniGameId = id;
    }

    /// <summary>
    /// Published by MiniGameManager when a game ends.
    /// SessionManager subscribes → returns to FreeRoam.
    /// TokenService subscribes → awards red tokens scaled by Quality.
    /// </summary>
    public readonly struct MiniGameCompletedMessage
    {
        public readonly string MiniGameId;
        public readonly bool   Success;
        public readonly float  Quality;   // 0–1

        public MiniGameCompletedMessage(string id, bool success, float quality)
        {
            MiniGameId = id;
            Success    = success;
            Quality    = quality;
        }
    }

    // ── Save System ──────────────────────────────────────────

    /// <summary>Published after a save slot is written successfully.</summary>
    public readonly struct SaveCompletedMessage
    {
        public readonly int SlotIndex;
        public SaveCompletedMessage(int slot) => SlotIndex = slot;
    }

    /// <summary>Published when save is attempted but token balance is insufficient.</summary>
    public readonly struct SaveDeniedMessage
    {
        public readonly int Required;
        public readonly int Current;
        public SaveDeniedMessage(int required, int current) { Required = required; Current = current; }
    }
}

// ── Shared Enums ─────────────────────────────────────────────

namespace Archipelago.Core
{
    public enum TokenType
    {
        Red,    // Mini-games
        Green,  // Garden (passive)
        Blue    // Routine + Scanner — primary progress currency
    }

    public enum SessionState
    {
        None,
        WakeUp,
        FreeRoam,
        MiniGame,
        Scanning,
        Routine,
        CodeComplete,
        End
    }
}
// ============================================================
//  АРХИПЕЛАГ — MessagePipe Message Definitions + Shared Types
//
//  All inter-system communication goes through typed structs.
//  No system holds a direct reference to another.
//
//  Naming:  <Noun><PastTenseVerb>Message
//  Binding: Registered in SceneInstaller via
//           Container.BindMessageBroker<T>()
//
//  ВАЖНО: TokenType, TokenAmount, TokenBalance — канонические
//         определения здесь. TokenTypes.cs и EconomyMessages.cs
//         удалены. Все файлы используют Archipelago.Core.
// ============================================================

namespace Archipelago.Core
{
    // ── Shared Token Types ────────────────────────────────────

    /// <summary>
    /// Три типа валют. Строковые значения совпадают с именами полей на сервере.
    /// </summary>
    public enum TokenType
    {
        Red   = 0,  // Mini-games
        Green = 1,  // Garden (passive)
        Blue  = 2,  // Routine + Scanner — primary progress currency
    }

    /// <summary>
    /// Пара тип + количество. Используется в запросах spend/earn.
    /// </summary>
    public readonly struct TokenAmount
    {
        public readonly TokenType Type;
        public readonly int       Amount;

        public TokenAmount(TokenType type, int amount)
        {
            Type   = type;
            Amount = amount;
        }

        public override string ToString() => $"{Amount} {Type}";
    }

    /// <summary>
    /// Снимок баланса всех трёх валют. Value type — безопасно копировать.
    /// </summary>
    public readonly struct TokenBalance
    {
        public readonly int Red;
        public readonly int Green;
        public readonly int Blue;

        public TokenBalance(int red, int green, int blue)
        {
            Red   = red;
            Green = green;
            Blue  = blue;
        }

        public int Get(TokenType type) => type switch
        {
            TokenType.Red   => Red,
            TokenType.Green => Green,
            TokenType.Blue  => Blue,
            _               => 0,
        };

        public TokenBalance With(TokenType type, int newValue) => type switch
        {
            TokenType.Red   => new TokenBalance(newValue, Green, Blue),
            TokenType.Green => new TokenBalance(Red, newValue, Blue),
            TokenType.Blue  => new TokenBalance(Red, Green, newValue),
            _               => this,
        };

        public TokenBalance Apply(TokenType type, int delta)
            => With(type, Get(type) + delta);

        public static readonly TokenBalance Zero = new(0, 0, 0);

        public override string ToString() => $"R:{Red} G:{Green} B:{Blue}";
    }

    // ── Session State ─────────────────────────────────────────

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

    // ── Clock ────────────────────────────────────────────────

    /// <summary>
    /// Published every in-game tick by GameClock.
    /// Do NOT use for per-frame logic — prefer Update() or UniTask loops.
    /// </summary>
    public readonly struct GameTickMessage
    {
        public readonly float TotalGameTime;
        public readonly float DeltaGameTime;
        public readonly int   DayIndex;

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
    /// Публикуется TokenService при любом изменении баланса.
    /// UI подписывается и обновляет счётчики.
    /// </summary>
    public readonly struct TokensChangedMessage
    {
        public readonly TokenBalance OldBalance;
        public readonly TokenBalance NewBalance;
        public readonly TokenType    ChangedType;
        public readonly int          Delta;
        public readonly string       Reason;

        public TokensChangedMessage(
            TokenBalance old,
            TokenBalance next,
            TokenType    type,
            int          delta,
            string       reason)
        {
            OldBalance  = old;
            NewBalance  = next;
            ChangedType = type;
            Delta       = delta;
            Reason      = reason;
        }
    }

    /// <summary>Публикуется когда баланс успешно синхронизирован с сервером.</summary>
    public readonly struct TokensSyncedMessage
    {
        public readonly TokenBalance ServerBalance;
        public TokensSyncedMessage(TokenBalance balance) => ServerBalance = balance;
    }

    /// <summary>Публикуется когда spend отклонён (недостаточно токенов).</summary>
    public readonly struct TokensInsufficientMessage
    {
        public readonly TokenType Type;
        public readonly int       Required;
        public readonly int       Current;

        public TokensInsufficientMessage(TokenType type, int required, int current)
        {
            Type     = type;
            Required = required;
            Current  = current;
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
        public readonly string RoutineId;
        public readonly float  Quality;

        public RoutineCompletedMessage(string id, float quality)
        {
            RoutineId = id;
            Quality   = quality;
        }
    }

    // ── Scanner ──────────────────────────────────────────────

    /// <summary>
    /// Published by CircleSearchController when an object is captured (success=true)
    /// or when a draw gesture finds nothing (success=false).
    /// </summary>
    public readonly struct ObjectCapturedMessage
    {
        public readonly string ObjectId;
        public readonly string DisplayName;
        public readonly bool   Success;

        public ObjectCapturedMessage(string objectId, string displayName, bool success)
        {
            ObjectId    = objectId;
            DisplayName = displayName;
            Success     = success;
        }
    }

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
        public readonly float  Quality;

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
    public readonly struct EarnMultiplierChangedMessage
    {
        public readonly float  Multiplier;
        public readonly string Source;
        public EarnMultiplierChangedMessage(float multiplier, string source)
        { Multiplier = multiplier; Source = source; }
    }

    /// <summary>Публикуется CleanHandler при применении/истечении эффекта чистоты.</summary>
    public readonly struct CleanStateChangedMessage
    {
        public readonly bool IsClean;
        public CleanStateChangedMessage(bool isClean) => IsClean = isClean;
    }

    /// <summary>Публикуется GardenHandler при изменении множителя сада.</summary>
    public readonly struct GardenMultiplierChangedMessage
    {
        public readonly float  Multiplier;
        public readonly string Source;
        public GardenMultiplierChangedMessage(float multiplier, string source)
        { Multiplier = multiplier; Source = source; }
    }
    public readonly struct GardenStateChangedMessage
    {
        public readonly int  Accumulated;
        public readonly bool IsDecaying;    // true если близко к decay (80% таймера)
        public GardenStateChangedMessage(int accumulated, bool isDecaying)
        { Accumulated = accumulated; IsDecaying = isDecaying; }
    }


    public readonly struct CalibrationStateMessage
    {
        public readonly float NeedlePosition;  // [-1, 1]
        public readonly float PlayerPosition;  // [-1, 1]
        public readonly bool  InZone;
        public CalibrationStateMessage(float needle, float player, bool inZone)
        { NeedlePosition = needle; PlayerPosition = player; InZone = inZone; }
    }

    /// <summary>State tick от WireRepairGame → UI.</summary>
    public readonly struct WireRepairStateMessage
    {
        public readonly bool[]  Connected;
        public readonly float   TimeRemaining;
        public readonly int     SelectedIndex;  // -1 если ничего не выбрано
        public WireRepairStateMessage(bool[] connected, float time, int selected)
        { Connected = connected; TimeRemaining = time; SelectedIndex = selected; }
    }

    /// <summary>State tick от MonitoringGame → UI.</summary>
    public readonly struct MonitoringStateMessage
    {
        public readonly float[] Values;         // [-1, 1] для каждого параметра
        public readonly float[] OutOfZoneTime;  // секунд вне зоны подряд
        public readonly float   TimeRemaining;
        public MonitoringStateMessage(float[] values, float[] outTime, float time)
        { Values = values; OutOfZoneTime = outTime; TimeRemaining = time; }
    }
    public readonly struct RoomChangedMessage
    {
        /// <summary>
        /// Идентификатор комнаты. Возможные значения:
        /// "hub" | "garden" | "gallery" | "residential" | "generator" | "reservoir" | "street"
        /// </summary>
        public readonly string RoomId;
        public RoomChangedMessage(string roomId) => RoomId = roomId;
    }

    // ── Flags ─────────────────────────────────────────────────

    /// <summary>
    /// Публикуется FlagService при любом изменении флагов.
    /// ScannerUIController подписывается для показа предупреждений.
    /// </summary>
    public readonly struct FlagsUpdatedMessage
    {
        public readonly PlayerProfile.FlagProfile Flags;
        public FlagsUpdatedMessage(PlayerProfile.FlagProfile flags) => Flags = flags;
    }

    /// <summary>
    /// Публикуется FlagService когда сканер блокируется (ABUSE >= 3).
    /// ScannerUIController показывает таймер обратного отсчёта.
    /// </summary>
    public readonly struct ScannerBlockedMessage
    {
        public readonly System.DateTime BlockedUntil;
        public ScannerBlockedMessage(System.DateTime until) => BlockedUntil = until;
    }
    public readonly struct SaveLoadedMessage
    {
        public readonly int SlotIndex;
        public SaveLoadedMessage(int slot) => SlotIndex = slot;
    }
}
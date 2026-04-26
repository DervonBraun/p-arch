namespace Archipelago.Economy
{
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

    /// <summary>
    /// Публикуется когда баланс успешно синхронизирован с сервером.
    /// </summary>
    public readonly struct TokensSyncedMessage
    {
        public readonly TokenBalance ServerBalance;
        public TokensSyncedMessage(TokenBalance balance) => ServerBalance = balance;
    }

    /// <summary>
    /// Публикуется когда spend отклонён сервером (недостаточно токенов).
    /// </summary>
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
}
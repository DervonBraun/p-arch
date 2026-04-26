namespace Archipelago.Economy
{
    /// <summary>
    /// Три типа валют. Строковые значения совпадают с именами полей на сервере.
    /// </summary>
    public enum TokenType
    {
        Red   = 0,
        Green = 1,
        Blue  = 2,
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
}
namespace ToolSmiths.InventorySystem.Tests.EditMode.Probability
{
    /// <summary>Test-local enums — the point of the pure assembly is that these are all it needs.</summary>
    internal enum Coin
    {
        None = 0,   // default(Coin) — the fail bucket
        Copper = 1,
        Silver = 2,
        Gold = 3,
    }

    /// <summary>Mirrors ItemRarity's shape: a zero fail member, then rarer-as-value-rises.</summary>
    internal enum Tier
    {
        Nothing = 0,
        White = 5,
        Blue = 15,
        Yellow = 20,
        Orange = 30,
    }

    /// <summary>No zero member — exercises "the enum has no default bucket".</summary>
    internal enum NoFail
    {
        A = 1,
        B = 2,
        C = 3,
    }
}

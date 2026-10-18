namespace Eirora.Simulation.Economy
{
    /// <summary>
    /// Amounts of goods are whole numbers of millionths, so production per quarter hour stays exact enough
    /// without floating point (INV-D3).
    /// </summary>
    public static class Quantity
    {
        public const long PerUnit = 1_000_000;

        public static long Units(long whole) => whole * PerUnit;

        /// <summary>Whole units, rounded down.</summary>
        public static long Whole(long amount) => amount / PerUnit;
    }
}

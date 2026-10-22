using System;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// How the tuning types turn the range their <c>Sanitised()</c> states into a refusal: a value the
    /// sanitising would move is one the Encounter will not take.
    /// </summary>
    internal static class TuningRange
    {
        internal static void Require(string name, float value, float sanitised)
        {
            if (value != sanitised)
                throw new ArgumentOutOfRangeException(name, value, $"{name} is outside its range (nearest valid value: {sanitised}).");
        }
    }
}

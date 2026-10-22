namespace ToolSmiths.InventorySystem.Items
{
    /// <summary>
    /// A source of uniform rolls in <c>[0, 1)</c>. The generator takes randomness as a
    /// dependency - the same choice <c>ProbabilityTable</c> made when it moved
    /// <see cref="System.Random"/> out and took the roll as a parameter - so every roll the
    /// generator makes is a deterministic unit test.
    ///
    /// The runtime adapter (issue #7 / #8) wraps <c>UnityEngine.Random.value</c>; a test
    /// passes a seeded or scripted stand-in.
    /// </summary>
    public interface IRollSource
    {
        /// <summary>The next roll, uniform in <c>[0, 1)</c>.</summary>
        float Next();
    }
}

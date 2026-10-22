namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// How full the hero's bag is, for the bag-fill bar (issue #23). One fraction, not a container.
    /// The runtime adapter (<c>ContainerBagGauge</c>) measures occupied cells against the
    /// container's <c>Capacity</c>.
    /// </summary>
    public interface IBagGauge
    {
        /// <summary>Occupied capacity as a fraction in <c>[0, 1]</c> — 1 is a bag with no room left.</summary>
        float FillFraction { get; }
    }
}

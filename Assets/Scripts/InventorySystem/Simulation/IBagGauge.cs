namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// How full the hero's bag is, read live by the Encounter every tick so
    /// <see cref="HeroBehaviour.ShouldRecallForBagFull"/> can raise the bag-full auto-Recall
    /// (issue #23). One fraction, not a container: the bag-full trigger is the only thing the
    /// engine-free sim needs to know about storage, so this is all it is handed.
    ///
    /// The runtime adapter (<c>ContainerBagGauge</c>) measures occupied cells against the
    /// container's <c>Capacity</c>; a test hands over a settable fraction. An Encounter built
    /// without one simply never fires the bag-full trigger.
    /// </summary>
    public interface IBagGauge
    {
        /// <summary>Occupied capacity as a fraction in <c>[0, 1]</c> — 1 is a bag with no room left.</summary>
        float FillFraction { get; }
    }
}

using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// A map's Hero figure, which Travels between the map's anchors (ADR-0021). Shown, never simulated: nothing
    /// in the sim reads where it walks, and Pause does not stop it. Implemented by <see cref="HeroFigure"/>.
    /// </summary>
    public interface IHeroFigure
    {
        /// <summary>Whether a walk is under way. False once the figure has arrived, or was placed.</summary>
        bool IsWalking { get; }

        /// <summary>Whether a held Run's hero has gone into the Recall portal, so the Town map can come up.</summary>
        bool HasEnteredPortal { get; }

        /// <summary>Appear at <paramref name="anchor"/> and stand there, as the figure does at the Field Exit.</summary>
        void StandAt(RectTransform anchor);

        /// <summary>
        /// Walk from where the figure is drawn now to <paramref name="anchor"/>, replacing a walk under way. On
        /// arrival it stands there, or follows the fight when an Encounter is live.
        /// </summary>
        void TravelTo(RectTransform anchor);

        /// <summary>
        /// The Run just Sent comes out of a Town Portal: the figure appears at the fight instead of walking in to the
        /// Location from the Field Exit. Call it right after the Send.
        /// </summary>
        void ArriveByPortal();
    }
}

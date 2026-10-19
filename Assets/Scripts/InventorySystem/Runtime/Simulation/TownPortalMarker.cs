using ToolSmiths.InventorySystem.Services;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The Town Portal on the Town map (ADR-0022), beside the Waypoint: shown exactly while the hero has an open portal,
    /// with a Run held, a Run ended after it, or none at all. The hero owns the portal and no event says it closed
    /// (a resume clears it after the Run's own phase event), so it is read every frame.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class TownPortalMarker : MonoBehaviour
    {
        private Image _image;

        private Image Image => _image ? _image : _image = GetComponent<Image>();

        private void Update() => Image.enabled = Session.Instance.Hero.TownPortal != null;
    }
}

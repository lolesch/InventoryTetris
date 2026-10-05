using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Runtime.Provider;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// Mirrors the hero bag's fill onto an <see cref="Image"/>'s <see cref="Image.fillAmount"/> —
    /// the same <see cref="IBagGauge.FillFraction"/> the bag-full auto-Recall reads
    /// (<see cref="HeroBehaviour.ShouldRecallForBagFull"/>), so the bar and the trigger can never
    /// disagree. The image must use <see cref="Image.Type.Filled"/>; it is switched to it on
    /// <see cref="Reset"/>/<see cref="OnValidate"/> when still on a different type.
    ///
    /// Polled every <see cref="Update"/> rather than bound to the container's content event: a hero
    /// load replaces the bag, and a poll that reads the Hero each frame follows it with no rebinding
    /// (same reasoning as <see cref="EncounterStatsPanel"/>). The image is only written when the
    /// fraction changes.
    /// </summary>
    [RequireComponent(typeof(Image))]
    [DisallowMultipleComponent]
    public sealed class BagFillImage : MonoBehaviour
    {
        private Image _image;
        private CharacterInventory _bag;
        private IBagGauge _gauge;
        private float _shown = -1f;

        private void Awake() => _image = GetComponent<Image>();

        private void Reset() => EnsureFilled();

        private void OnValidate() => EnsureFilled();

        private void Update()
        {
            var bag = Session.Instance.Hero.Inventory;

            // The gauge is bound to one container; rebuild only if a hero load swaps it.
            if (!ReferenceEquals(bag, _bag))
            {
                _bag = bag;
                _gauge = new ContainerBagGauge(bag);
            }

            var fraction = _gauge.FillFraction;
            if (Mathf.Approximately(fraction, _shown)) return;

            _shown = fraction;
            _image.fillAmount = fraction;
        }

        private void EnsureFilled()
        {
            var image = GetComponent<Image>();
            if (image != null && image.type != Image.Type.Filled)
                image.type = Image.Type.Filled;
        }
    }
}

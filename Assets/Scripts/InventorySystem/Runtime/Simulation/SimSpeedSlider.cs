using System.Globalization;
using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>The sim-speed slider: continuous, with the log response curve of
    /// <see cref="HeroBehaviour.SliderToSimSpeed"/> applied for the readout (<c>1.0x</c>..<c>8.0x</c>)
    /// and for <see cref="SimSpeed"/>.</summary>
    public sealed class SimSpeedSlider : ValueSlider
    {
        public float SimSpeed => HeroBehaviour.SliderToSimSpeed(Value);

        public void SetSimSpeedWithoutNotify(float simSpeed) =>
            SetValueWithoutNotify(HeroBehaviour.SimSpeedToSlider(simSpeed));

        protected override float Map(float value) => HeroBehaviour.SliderToSimSpeed(value);

        protected override string Format(float value) => Map(value).ToString("0.0", CultureInfo.InvariantCulture) + "x";
    }
}

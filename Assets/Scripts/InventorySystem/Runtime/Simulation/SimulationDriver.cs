using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// Pumps the live Encounter from the frame loop (issue #43). Each <see cref="Update"/>, while
    /// a Run is <see cref="RunPhase.InField"/>, it banks <c>Time.deltaTime</c> scaled by
    /// <c>HeroBehaviour.SimSpeed</c> onto the sim's combat clock — and nothing else. It never
    /// touches <see cref="Time.timeScale"/> (ADR-0008): sim-speed accelerates the fight only, so
    /// UI tweens and panel animations keep real time.
    ///
    /// A downed hero is walked back to Town here too — <see cref="SimulationProvider.HandleHeroDeath"/>
    /// is a no-op until the sim reports the hero down, then closes the Run once. The provider
    /// attaches this component to its own GameObject, so it lives exactly as long as the provider.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public sealed class SimulationDriver : MonoBehaviour
    {
        private void Update()
        {
            var provider = SimulationProvider.Instance;
            if (provider == null) return;

            var run = provider.Run;
            if (run.Phase != RunPhase.InField) return;

            var simSpeed = Mathf.Max(0f, provider.Behaviour.SimSpeed);
            _ = run.Advance(Time.deltaTime * simSpeed);

            if (run.HeroIsDown)
                provider.HandleHeroDeath();
        }
    }
}

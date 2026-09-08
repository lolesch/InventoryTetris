using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Runtime.Character;
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
    /// Both ways out of the Field are walked back to Town here — a downed hero through
    /// <see cref="SimulationProvider.HandleHeroDeath"/>, and a <see cref="HeroBehaviour"/>
    /// auto-Recall trigger (issue #23) through <see cref="SimulationProvider.Recall"/>. Each is a
    /// no-op until the sim raises its signal, so the driver can poll both every frame. The
    /// provider attaches this component to its own GameObject, so it lives exactly as long as
    /// the provider.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public sealed class SimulationDriver : MonoBehaviour
    {
        private void Update()
        {
            var provider = SimulationProvider.Instance;
            if (provider == null) return;

            var simSpeed = Mathf.Max(0f, provider.Behaviour.SimSpeed);
            var dt = Time.deltaTime * simSpeed;

            // Regeneration belongs to the living hero at sim speed, in both Town and
            // Field (issue #45). Dead heroes do not regenerate.
            var hero = CharacterProvider.Instance?.Player;
            if (hero != null && !hero.IsDead)
                hero.Regenerate(dt);

            var run = provider.Run;
            if (run.Phase != RunPhase.InField) return;

            _ = run.Advance(dt);

            // Both exits are read after the tick, never inside it: the sim raises its signal and
            // stops, and the RunState transition happens out here. Death is tested first so a
            // tick that trips both is a Death — the penalty is not dodgeable by an auto-Recall
            // trigger racing it.
            if (run.HeroIsDown)
                provider.HandleHeroDeath();
            else if (run.RecallRequested)
                _ = provider.Recall();
        }
    }
}

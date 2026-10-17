using Submodules.Utility.Services;
using System;
using TMPro;
using ToolSmiths.InventorySystem.GUI.Components.Buttons;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Popups
{
    /// <summary>
    /// The Notice that opens after a Death (<see cref="DeathNotice"/>, spec <i>hero travel</i>): the Run's result in
    /// words and one button, Respawn, which is the selected button, so Enter and the Cancel key press it
    /// (<see cref="PopupFocus.OpenNotice(Submodules.Utility.UI.AbstractButton)"/>). Respawn is the only way on: it
    /// revives the hero at the Waypoint and the notice closes on <see cref="ISimulationService.Respawned"/>, however
    /// that was raised. It opens when the Death beat ends (<see cref="ISimulationService.DeathBeatEnded"/>), not when the
    /// Death settles. The component sits on an object that stays active; <c>screen</c> is what it opens.
    /// </summary>
    public sealed class DeathNoticePopup : MonoBehaviour
    {
        [SerializeField] private GameObject screen;
        [SerializeField] private TextMeshProUGUI message;
        [SerializeField] private ClickEventButton respawn;

        private IDisposable cancelLayer;
        private bool waiting;

        private void OnEnable()
        {
            respawn.Clicked += Respawn;

            // A scene object survives a Play session, so it starts closed whatever the last session left.
            screen.SetActive(false);

            // An enable in Edit Mode finds no services armed.
            if (!ServiceLocator.IsArmed)
                return;

            var simulation = SimulationService.Instance;
            simulation.RunSettled += OnRunSettled;
            simulation.DeathBeatEnded += OpenIfWaiting;
            simulation.Respawned += Close;
        }

        private void OnDisable()
        {
            respawn.Clicked -= Respawn;

            if (ServiceLocator.IsArmed)
            {
                var simulation = SimulationService.Instance;
                simulation.RunSettled -= OnRunSettled;
                simulation.DeathBeatEnded -= OpenIfWaiting;
                simulation.Respawned -= Close;
            }

            waiting = false;
            cancelLayer?.Dispose();
            cancelLayer = null;
        }

        // The Death is settled at once, but the notice waits for the Death beat to end: the figure falls first.
        private void OnRunSettled(RunResult result)
        {
            if (result.Outcome != RunOutcome.Died)
                return;

            var fell = Session.Instance.Hero.SelectedLocation;
            message.text = DeathNotice.TextFor(fell.DisplayName, result.XpLost, result.CurrencyFee);
            waiting = true;

            if (!SimulationService.Instance.DeathBeatPlaying)
                OpenIfWaiting();
        }

        // A beat cleared early by a hero load leaves the hero up: there is nothing to respawn from.
        private void OpenIfWaiting()
        {
            if (!waiting || !Session.Instance.Hero.IsDead)
                return;

            waiting = false;
            screen.SetActive(true);
            cancelLayer ??= PopupFocus.OpenNotice(respawn);
        }

        private static void Respawn() => SimulationService.Instance.Respawn();

        private void Close()
        {
            waiting = false;
            screen.SetActive(false);
            cancelLayer?.Dispose();
            cancelLayer = null;
        }
    }
}

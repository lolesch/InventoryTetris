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
    /// The Notice that opens once the Death beat ends, telling the player what the Death cost. Respawn is its one
    /// button and the only way on, so Enter and Cancel both press it. The component stays active; <c>screen</c> is
    /// what it opens.
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
            message.text = DeathNotice.TextFor(fell ? fell.DisplayName : null, result.XpLost, result.CurrencyFee);
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

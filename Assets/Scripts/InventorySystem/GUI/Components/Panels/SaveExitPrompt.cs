using Submodules.Utility.Services;
using Submodules.Utility.UI;
using System;
using TMPro;
using ToolSmiths.InventorySystem.GUI.Components.Buttons;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Panels
{
    /// <summary>
    /// The modal behind the Pause Menu's Save &amp; Exit (<see cref="SaveAndExit"/>). In Town the button goes
    /// straight through and this stays closed unless the write fails; in the Field it first asks, since leaving
    /// ends the Run. A failed write keeps the player in the game with the cause and offers Retry and, behind its
    /// own confirmation, Exit without saving. The component sits on the modal, which is shown by being enabled;
    /// Cancel (the Back button or the Cancel key, as a <see cref="CancelRank.Modal"/> layer) steps back one level.
    /// </summary>
    public sealed class SaveExitPrompt : MonoBehaviour
    {
        private enum Step
        {
            Confirm,
            Failed,
            Discard,
        }

        [SerializeField] private TextMeshProUGUI message;
        [SerializeField] private ClickEventButton primary;
        [SerializeField] private ClickEventButton secondary;
        [SerializeField] private ClickEventButton back;

        private IDisposable cancelLayer;
        private Step step;
        private string failure = string.Empty;

        /// <summary>Raised once the game is leaving for the Main Menu, saved or not, so the Pause Menu can stop taking clicks.</summary>
        public event Action Left;

        /// <summary>Starts a Save &amp; Exit: asks first when it ends a Run, else leaves at once.</summary>
        public void Begin()
        {
            if (SaveAndExit.NeedsConfirmation(ServiceLocator.Get<ISimulationService>()))
                Show(Step.Confirm);
            else
                Attempt();
        }

        private void OnEnable()
        {
            primary.Clicked += OnPrimary;
            secondary.Clicked += OnSecondary;
            back.Clicked += Back;
            cancelLayer = CancelChain.TryRegister(CancelRank.Modal, Cancel);
        }

        private void OnDisable()
        {
            primary.Clicked -= OnPrimary;
            secondary.Clicked -= OnSecondary;
            back.Clicked -= Back;
            cancelLayer?.Dispose();
            cancelLayer = null;
        }

        private void Show(Step next)
        {
            step = next;
            gameObject.SetActive(true);

            switch (next)
            {
                case Step.Confirm:
                    Set(SaveAndExit.FieldQuestion, primary: "Save & Exit", secondary: null, back: "Cancel");
                    break;
                case Step.Failed:
                    Set($"The hero was not saved.\n{failure}", primary: "Retry", secondary: "Exit without saving", back: "Stay");
                    break;
                default:
                    Set(SaveAndExit.DiscardQuestion, primary: null, secondary: "Exit without saving", back: "Back");
                    break;
            }
        }

        private void Set(string text, string primary, string secondary, string back)
        {
            message.text = text;
            Show(this.primary, primary);
            Show(this.secondary, secondary);
            Label(this.back, back);
        }

        // A button with no label for this step is hidden.
        private static void Show(ClickEventButton button, string label)
        {
            button.gameObject.SetActive(label != null);

            if (label != null)
                Label(button, label);
        }

        private static void Label(ClickEventButton button, string text) =>
            button.GetComponentInChildren<TextMeshProUGUI>(true).text = text;

        private void OnPrimary() => Attempt();

        private void OnSecondary()
        {
            if (step == Step.Discard)
                Exit();
            else
                Show(Step.Discard);
        }

        private void Back()
        {
            if (step == Step.Discard)
                Show(Step.Failed);
            else
                gameObject.SetActive(false);
        }

        private bool Cancel()
        {
            Back();
            return true;
        }

        private void Attempt()
        {
            var result = SaveAndExit.Leave(ServiceLocator.Get<ISimulationService>(), HeroSaveService.Instance, Exit);

            if (result.Wrote)
                return;

            failure = result.Reason;
            Show(Step.Failed);
        }

        private void Exit()
        {
            gameObject.SetActive(false);
            Left?.Invoke();
            SceneProvider.Instance.LoadScene(GameEntryGuard.MenuScene);
        }
    }
}

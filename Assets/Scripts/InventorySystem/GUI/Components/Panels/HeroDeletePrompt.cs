using Submodules.Utility.Services;
using System;
using TMPro;
using ToolSmiths.InventorySystem.GUI.Components.Buttons;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Panels
{
    /// <summary>
    /// The confirmation the Delete button opens: a modal over the Main Menu that names the selected hero and
    /// deletes nothing until Delete is confirmed (<see cref="MainMenuFlow.DeleteSelected"/>). It is shown by
    /// being enabled, so the component sits on the panel itself; Cancel (the button or the Cancel key, as a
    /// <see cref="CancelRank.Modal"/> layer) closes it and deletes nothing.
    /// </summary>
    public sealed class HeroDeletePrompt : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI question;
        [SerializeField] private ClickEventButton confirm;
        [SerializeField] private ClickEventButton cancel;

        private IDisposable cancelLayer;

        /// <summary>Shows the question for the selected hero.</summary>
        public void Open() => gameObject.SetActive(true);

        /// <summary>Hides the question. Nothing is deleted.</summary>
        public void Close() => gameObject.SetActive(false);

        private void OnEnable()
        {
            confirm.Clicked += Confirm;
            cancel.Clicked += Close;

            question.text = ServiceLocator.IsArmed && MainMenuFlow.TrySelected(HeroSaveService.Instance, out var hero)
                ? MainMenuFlow.DeleteQuestion(hero)
                : string.Empty;
            cancelLayer = CancelChain.TryRegister(CancelRank.Modal, Cancel);
        }

        private void OnDisable()
        {
            confirm.Clicked -= Confirm;
            cancel.Clicked -= Close;

            cancelLayer?.Dispose();
            cancelLayer = null;
        }

        private bool Cancel()
        {
            Close();
            return true;
        }

        private void Confirm()
        {
            try
            {
                _ = MainMenuFlow.DeleteSelected(HeroSaveService.Instance);
                Close();
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not delete the hero: {exception.Message}");
                question.text = "The hero could not be deleted: storage is blocked.";
            }
        }
    }
}

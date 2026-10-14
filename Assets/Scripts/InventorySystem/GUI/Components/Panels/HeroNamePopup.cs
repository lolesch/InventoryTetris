using Submodules.Utility.Services;
using System;
using TMPro;
using ToolSmiths.InventorySystem.GUI.Components.Buttons;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ToolSmiths.InventorySystem.GUI.Components.Panels
{
    /// <summary>
    /// The name entry the create slot opens: a popup over the hero slots that creates nothing until a valid
    /// name is confirmed (<see cref="MainMenuFlow.CreateHero"/>) and names the reason when it is not. It is
    /// shown by being enabled, so the component sits on the panel itself; Cancel (the button or the Cancel
    /// key, as a <see cref="CancelRank.Popup"/> layer) closes it and creates nothing.
    /// </summary>
    public sealed class HeroNamePopup : MonoBehaviour
    {
        [SerializeField] private TMP_InputField input;
        [SerializeField] private TextMeshProUGUI reason;
        [SerializeField] private ClickEventButton confirm;
        [SerializeField] private ClickEventButton cancel;

        private IDisposable cancelLayer;

        /// <summary>Shows the entry, empty.</summary>
        public void Open() => gameObject.SetActive(true);

        /// <summary>Hides the entry. Nothing is created.</summary>
        public void Close() => gameObject.SetActive(false);

        private void OnEnable()
        {
            confirm.Clicked += Confirm;
            cancel.Clicked += Close;
            input.onSubmit.AddListener(Submit);
            input.onValueChanged.AddListener(Edited);

            input.text = string.Empty;
            reason.text = string.Empty;
            cancelLayer = CancelChain.TryRegister(CancelRank.Popup, Cancel);
            Focus();
        }

        private void OnDisable()
        {
            confirm.Clicked -= Confirm;
            cancel.Clicked -= Close;
            input.onSubmit.RemoveListener(Submit);
            input.onValueChanged.RemoveListener(Edited);

            cancelLayer?.Dispose();
            cancelLayer = null;
        }

        private bool Cancel()
        {
            Close();
            return true;
        }

        private void Submit(string _) => Confirm();

        // A stale reason would sit beside text that no longer earns it.
        private void Edited(string _) => reason.text = string.Empty;

        private void Focus()
        {
            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(input.gameObject);

            input.ActivateInputField();
        }

        private void Confirm()
        {
            try
            {
                var check = MainMenuFlow.CreateHero(HeroSaveService.Instance, input.text);

                if (check.IsValid)
                {
                    Close();
                    return;
                }

                reason.text = check.Reason;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not create a hero: {exception.Message}");
                reason.text = "The hero could not be saved: storage is blocked or has no room.";
            }

            Focus();
        }
    }
}

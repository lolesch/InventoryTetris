using Submodules.Utility.Services;
using System;
using TMPro;
using ToolSmiths.InventorySystem.GUI.Components.Buttons;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Popups
{
    /// <summary>
    /// The game scene's one-time notice after a load that went wrong (<see cref="EntryNotice"/>): taken from the
    /// save service when the scene enables, and shown in a popup only if there is something to say. A damaged
    /// latest save and a partial load read as one message; the names of the packages left out stay behind Show.
    /// Dismiss (the button, or the Cancel key as a <see cref="CancelRank.Popup"/> layer) closes it. Taking the
    /// notice is what drops it, so a later scene change shows nothing. The component sits on an object that
    /// stays active; <c>screen</c> is what it opens.
    /// </summary>
    public sealed class EntryNoticePopup : MonoBehaviour
    {
        [SerializeField] private GameObject screen;
        [SerializeField] private TextMeshProUGUI message;
        [SerializeField] private TextMeshProUGUI names;
        [SerializeField] private ClickEventButton show;
        [SerializeField] private ClickEventButton dismiss;

        private IDisposable cancelLayer;
        private string namesText;

        private void OnEnable()
        {
            show.Clicked += ShowNames;
            dismiss.Clicked += Close;

            // A scene object survives a Play session, so it starts closed whatever the last session left.
            screen.SetActive(false);

            var notice = ServiceLocator.IsArmed ? HeroSaveService.Instance.TakeEntryNotice() : null;

            if (notice != null)
                Open(notice);
        }

        private void OnDisable()
        {
            show.Clicked -= ShowNames;
            dismiss.Clicked -= Close;

            cancelLayer?.Dispose();
            cancelLayer = null;
        }

        private void Open(EntryNotice notice)
        {
            message.text = notice.RestoredFromBackup && notice.UnplacedCount != 0
                ? $"{EntryNotice.BackupText}\n\n{notice.UnplacedText}"
                : notice.RestoredFromBackup ? EntryNotice.BackupText : notice.UnplacedText;
            namesText = notice.NamesText;

            names.gameObject.SetActive(false);
            show.gameObject.SetActive(notice.UnplacedCount != 0);
            screen.SetActive(true);
            cancelLayer ??= CancelChain.TryRegister(CancelRank.Popup, Cancel);
        }

        private void ShowNames()
        {
            names.text = namesText;
            names.gameObject.SetActive(true);
            show.gameObject.SetActive(false);
        }

        private void Close()
        {
            screen.SetActive(false);
            cancelLayer?.Dispose();
            cancelLayer = null;
        }

        private bool Cancel()
        {
            Close();
            return true;
        }
    }
}

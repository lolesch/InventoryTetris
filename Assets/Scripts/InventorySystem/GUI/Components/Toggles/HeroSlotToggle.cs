using Submodules.Utility.Services;
using Submodules.Utility.UI;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.GUI.Components.Panels;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Components.Toggles
{
    /// <summary>
    /// One of the hero slots: on means its hero is the Session's. The slot is this toggle's position
    /// among the <see cref="HeroSlotToggle"/>s of its <see cref="ToggleGroup"/>, and what it holds is
    /// <see cref="HeroSlots.At"/>: a saved hero, the create slot, or nothing. The group needs
    /// <c>UserCanClear</c> off, so choosing a slot switches the previous one off and clicking the
    /// active one leaves it on.
    ///
    /// <para>A slot with a hero shows the hero's template icon, and turning it on loads the hero
    /// (<see cref="MainMenuFlow.Select"/>); one that cannot be read shows <see cref="unreadableSprite"/>
    /// instead and is selected without being loaded, so the menu can say why and offer Delete. The create slot shows <see cref="createSprite"/>, and a
    /// <b>click</b> on it opens the <see cref="HeroNamePrompt"/>, which creates the hero once a name is
    /// confirmed - only a click: the group switching it on of its own accord (a reset to its first member)
    /// asks for nothing. Every slot after it is not interactable and shows no icon.</para>
    ///
    /// <para>The slots follow <see cref="IHeroSaveService.HeroesChanged"/> and read nothing else: whoever
    /// creates, deletes, renames or loads a hero does not need to know they exist. The list is read once
    /// per change and shared by every slot (<see cref="Heroes"/>), not once per slot. They act in Play
    /// Mode only: <c>Selectable</c> is <c>[ExecuteAlways]</c>, so an enable in the Editor must not
    /// rewrite the authored toggles.</para>
    /// </summary>
    public sealed class HeroSlotToggle : AbstractToggle
    {
        private static readonly List<HeroSlotToggle> Shown = new();

        private static IHeroSaveService watched;
        private static IReadOnlyList<HeroSummary> snapshot;
        private static int snapshotFrame = -1;
        private static string selectedId;

        private static readonly Color UnreadableTint = new(1f, 0.45f, 0.4f, 1f);

        [Space]
        [Tooltip("The Image the hero's template icon is shown on: a child, not the toggle's own background.")]
        [SerializeField] private Image icon;

        [Tooltip("The icon of the create slot: the first slot with no hero.")]
        [SerializeField] private Sprite createSprite;

        [Tooltip("The icon of a hero whose save cannot be read, in place of its template's.")]
        [SerializeField] private Sprite unreadableSprite;

        [Tooltip("The name entry a click on the create slot opens.")]
        [SerializeField] private HeroNamePrompt namePrompt;

        private string heroId;
        private bool creates;

        // Fires on every Play entry even with domain reload disabled, so nothing of the last session
        // (a toggle that was never disabled, a subscription on a service that is gone) survives into this one.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Shown.Clear();
            watched = null;
            snapshot = null;
            snapshotFrame = -1;
            selectedId = null;
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            if (!Application.isPlaying || !ServiceLocator.IsArmed)
                return;

            if (!Shown.Contains(this))
                Shown.Add(this);

            Watch();
            Apply(Heroes());
        }

        protected override void OnDisable()
        {
            _ = Shown.Remove(this);

            if (Shown.Count == 0)
                Unwatch();

            base.OnDisable();
        }

        protected override void OnClick()
        {
            if (creates)
                namePrompt.Open();
            else
                base.OnClick();
        }

        protected override void OnToggle()
        {
            if (!IsOn || heroId == null || !ServiceLocator.IsArmed)
                return;

            var saves = HeroSaveService.Instance;

            // A selection that happened raised HeroesChanged. One that did not (a Run is in the Field, or the
            // file is gone) left this slot pressed while the old hero is still the selected one.
            if (!MainMenuFlow.Select(saves, heroId) && saves.LastSelectedHeroId != heroId)
                Refresh();
        }

        private static void Watch()
        {
            if (watched != null)
                return;

            watched = HeroSaveService.Instance;
            watched.HeroesChanged += Refresh;
        }

        private static void Unwatch()
        {
            if (watched == null)
                return;

            watched.HeroesChanged -= Refresh;
            watched = null;
        }

        // The ordered list, read once a frame however many slots ask: a panel that enables six of them reads
        // the saves once. A change drops it (Refresh), so it is never stale across one.
        private static IReadOnlyList<HeroSummary> Heroes()
        {
            if (snapshot == null || snapshotFrame != Time.frameCount)
            {
                snapshot = HeroSlots.Ordered(HeroSaveService.Instance);
                selectedId = HeroSaveService.Instance.LastSelectedHeroId;
                snapshotFrame = Time.frameCount;
            }

            return snapshot;
        }

        private static void Refresh()
        {
            snapshot = null;
            var heroes = Heroes();

            foreach (var toggle in Shown.ToArray())
                toggle.Apply(heroes);
        }

        private void Apply(IReadOnlyList<HeroSummary> heroes)
        {
            var view = HeroSlots.At(heroes, Slot);

            heroId = view.Kind == HeroSlotKind.Hero ? view.Hero.Id : null;
            creates = view.Kind == HeroSlotKind.Create;
            interactable = view.IsUsable;

            if (icon)
            {
                var unreadable = view.Kind == HeroSlotKind.Hero && view.Hero.UnreadableReason != null;
                var template = view.Hero.Template;
                var sprite = creates ? createSprite : unreadable ? unreadableSprite : template ? template.Icon : null;

                icon.sprite = sprite;
                icon.enabled = sprite != null;
                icon.color = unreadable ? UnreadableTint : Color.white;
            }

            // SyncToggle, not SetToggle: this mirrors state the save service owns, so the group's
            // SystemCanClear governs it and the user-side UserCanClear does not refuse it. With no hero
            // selected that is what lets the group end up with none on.
            SyncToggle(heroId != null && selectedId == heroId);
        }

        // Counted among the slot toggles only, so a sibling that is not one does not shift the numbering.
        // A toggle with no parent has no siblings: it is slot 0.
        private int Slot
        {
            get
            {
                var parent = transform.parent;

                if (!parent)
                    return 0;

                var slot = 0;

                foreach (Transform sibling in parent)
                {
                    if (sibling == transform)
                        break;

                    if (sibling.GetComponent<HeroSlotToggle>())
                        slot++;
                }

                return slot;
            }
        }
    }
}

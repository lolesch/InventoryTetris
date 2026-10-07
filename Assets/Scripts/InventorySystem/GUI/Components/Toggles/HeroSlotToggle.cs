using Submodules.Utility.Services;
using Submodules.Utility.UI;
using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Components.Toggles
{
    /// <summary>
    /// One of the hero slots: on means its hero is the Session's. The slot is this toggle's position
    /// among the <see cref="HeroSlotToggle"/>s of its <see cref="ToggleGroup"/>, and what it holds is
    /// <see cref="HeroSlots.At"/>: a saved hero, the create slot, or nothing. The group needs
    /// <c>UserCanUntoggle</c> off, so choosing a slot switches the previous one off and clicking the
    /// active one leaves it on.
    ///
    /// <para>A slot with a hero shows the hero's template icon, and turning it on loads the hero; one
    /// that cannot be read is not interactable. The create slot shows <see cref="createSprite"/>, and a
    /// <b>click</b> on it creates and loads a hero - only a click: the group switching it on of its own
    /// accord (a reset to its first member) creates nothing. Every slot after it is not interactable and
    /// shows no icon.</para>
    ///
    /// <para>The slots follow <see cref="IHeroSaveService.HeroesChanged"/> and read nothing else: whoever
    /// creates, deletes, renames or loads a hero does not need to know they exist. The list is read once
    /// per change and shared by every slot (<see cref="Heroes"/>), not once per slot. They act in Play
    /// Mode only: <c>Selectable</c> is <c>[ExecuteAlways]</c>, so an enable in the Editor must not
    /// rewrite the authored toggles.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroSlotToggle : AbstractToggle
    {
        private static readonly List<HeroSlotToggle> Shown = new();

        private static IHeroSaveService watched;
        private static IReadOnlyList<HeroSummary> snapshot;
        private static int snapshotFrame = -1;

        [Tooltip("The Image the hero's template icon is shown on: a child, not the toggle's own background.")]
        [SerializeField] private Image icon;

        [Tooltip("The icon of the create slot: the first slot with no hero.")]
        [SerializeField] private Sprite createSprite;

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
                CreateHero();
            else
                base.OnClick();
        }

        protected override void OnToggle()
        {
            if (!IsOn || heroId == null || !ServiceLocator.IsArmed)
                return;

            var saves = HeroSaveService.Instance;

            if (saves.ActiveHeroId == heroId)
                return;

            // A load that happened raised HeroesChanged. One that did not (a Run is in the Field, or the
            // file is gone) left this slot pressed while the old hero is still the Session's.
            if (!saves.Load(heroId).Entered)
                Refresh();
        }

        // The service raises HeroesChanged for the create and for the load, so the slots are already showing
        // the new hero and pressing its slot. A failure before the write changed nothing and this slot was
        // never pressed, so there is nothing to put back.
        private static void CreateHero()
        {
            try
            {
                _ = HeroSaveService.Instance.CreateAndLoad();
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not create a hero: {exception.Message}");
            }
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
                snapshot = HeroSlots.Ordered(HeroSaveService.Instance.List());
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
                var template = view.Hero.Template;
                var sprite = creates ? createSprite : template ? template.Icon : null;

                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }

            // SyncToggle, not SetToggle: this mirrors state the save service owns, so the group's
            // GroupCanUntoggle governs it and the user-side UserCanUntoggle does not refuse it.
            SyncToggle(heroId != null && watched.ActiveHeroId == heroId);
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

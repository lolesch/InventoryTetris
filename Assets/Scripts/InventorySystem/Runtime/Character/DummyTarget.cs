using System;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Services;
using Submodules.Utility.Extensions;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;
using UnityEngine.Serialization;

namespace ToolSmiths.InventorySystem.Runtime.Character
{
    public sealed class DummyTarget : BaseCharacter
    {
        // The dummy's stats are authored on the component, not in a HeroData: it is a legacy
        // combat path that issue #117 deletes. Its Hero wraps these very instances, so what the
        // scene's displays are bound to stays what the hero changes. The old serialized names are
        // kept so the scene's values carry over.
        [SerializeField, FormerlySerializedAs("<IsInvincible>k__BackingField")] private bool isInvincible;
        [SerializeField, FormerlySerializedAs("<IsBlocking>k__BackingField")] private bool isBlocking;
        [SerializeField, FormerlySerializedAs("<SpendResource>k__BackingField")] private bool spendResource = true;

        [SerializeField, FormerlySerializedAs("<CharacterStats>k__BackingField")] private CharacterStat[] characterStats;
        [SerializeField, FormerlySerializedAs("<CharacterResources>k__BackingField")] private CharacterResource[] characterResources;

        [SerializeField, Range(1, 100), FormerlySerializedAs("<CharacterLevel>k__BackingField")] private uint characterLevel = 1;

        [SerializeField] private uint experience = 20; // TODO: derive from monsterLevel and combat rating?

        protected override Hero BuildHero() =>
            new(characterStats, characterResources, characterLevel)
            {
                IsInvincible = isInvincible,
                IsBlocking = isBlocking,
                SpendResource = spendResource,
            };

        private void OnValidate() => ResetStatsAndResources();

        [ContextMenu("ResetStatsAndResources")]
        private void ResetStatsAndResources()
        {
            var resourcesOnly = new StatName[] { StatName.Health, StatName.Resource, StatName.Shield, StatName.Experience };

            var statNames = Enum.GetValues(typeof(StatName)) as StatName[];
            var statsOnly = new System.Collections.Generic.List<StatName>(statNames);

            foreach (var resource in resourcesOnly)
                _ = statsOnly.Remove(resource);

            if (characterStats.Length != statsOnly.Count)
            {
                characterStats = new CharacterStat[statsOnly.Count];

                for (var i = 0; i < statsOnly.Count; i++)
                    characterStats[i] = new CharacterStat(statsOnly[i], 1);
            }

            if (characterResources.Length != resourcesOnly.Length)
            {
                characterResources = new CharacterResource[] {
                    new CharacterResource(StatName.Health, 100),
                    new CharacterResource(StatName.Resource, 60),
                    new CharacterResource(StatName.Shield, 0),
                    new CharacterResource(StatName.Experience, 280),
                };
            }
        }

        protected override void OnDeath()
        {
            Debug.LogWarning($"{name.ColoredComponent()} {"died!".Colored(Color.red)}", this);

            var player = CharacterProvider.Instance.Player;
            var loot = ItemService.Instance.RollLoot(
                magicFind: player.GetStatValue(StatName.IncreasedItemRarity),
                itemQuantity: player.GetStatValue(StatName.IncreasedItemQuantity));

            foreach (var package in loot)
                //rework to drop items on the floor
                _ = player.PickUpItemOrStash(package);

            // TODO: use event instead?
            player.GainExperience(experience, CharacterLevel);

            this.GetResource(StatName.Health).RefillCurrent();
            this.GetResource(StatName.Shield).RefillCurrent();
        }
    }
}

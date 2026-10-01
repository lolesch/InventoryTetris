using Submodules.Utility.Provider;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    public sealed class CharacterProvider : AbstractProvider<CharacterProvider>
    {
        [field: SerializeField] public LocalPlayer Player { get; private set; }
        [field: SerializeField] public DummyTarget Dummy { get; private set; }

        private void DealDamage(BaseCharacter dealer, BaseCharacter receiver, DamageType damageType) => dealer.DealDamageTo(receiver, damageType);

        public void PlayerDealsPhysicalDamageToDummy() => DealDamage(Player, Dummy, DamageType.PhysicalDamage);
        public void PlayerDealsMagicalDamageToDummy() => DealDamage(Player, Dummy, DamageType.MagicalDamage);

        public void DummyDealsPhysicalDamageToPlayer() => DealDamage(Dummy, Player, DamageType.PhysicalDamage);
        public void DummyDealsMagicalDamageToPlayer() => DealDamage(Dummy, Player, DamageType.MagicalDamage);
        public void KillPlayer() => Player.GetResource(StatName.Health).DepleteCurrent();
        public void KillDummy() => Dummy.GetResource(StatName.Health).DepleteCurrent();

        /// <summary>
        /// The Healer's on-transition side effect (issue #58): a full Health and Resource refill
        /// on every genuine entry into <see cref="InventoryContext.Healer"/>. Subscribed here, not
        /// from a scene component: the provider already owns <see cref="HealPlayer"/>, and the
        /// context is the only input the refill needs, so wiring it through a MonoBehaviour on a
        /// toggle or panel only added a scene object that could be misplaced or forgotten.
        ///
        /// <para>Reacts to the context event rather than the toggle's click:
        /// <see cref="InventoryContextState.Changed"/> only fires on an actual change, so a
        /// re-click the <c>ToggleGroup</c> turns into "switch off" never reaches here - the refill
        /// fires once per entry, never on the way out.</para>
        ///
        /// <para>The feedback is the resource globes filling; audio feedback is deferred.</para>
        ///
        /// <para>Subscribed in <c>OnEnable</c> rather than <c>Awake</c>: this project disables
        /// domain and scene reload, so an <c>Awake</c> subscription never comes back after
        /// <c>OnDisable</c> tears it down on the way out of the first Play entry.</para>
        /// </summary>
        private void OnEnable() =>
            _ = InventoryProvider.TrySubscribeContextChanged(OnContextChanged, out _);

        private void OnDisable() => InventoryProvider.UnsubscribeContextChanged(OnContextChanged);

        private void OnContextChanged(InventoryContext context)
        {
            if (context == InventoryContext.Healer)
                HealPlayer();
        }

        public void HealPlayer()
        {
            Player.GetResource(StatName.Health).RefillCurrent();
            Player.GetResource(StatName.Resource).RefillCurrent();
        }

        public void ToggleSpendingResource() => Player.SpendResource = !Player.SpendResource;
    }
}

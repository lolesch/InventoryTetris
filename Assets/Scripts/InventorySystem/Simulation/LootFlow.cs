using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// Turns each of an Encounter's kills into Loot (issue #24; spec "Loot flow"). Subscribed
    /// to <see cref="EncounterSimulation.EnemyDefeated"/>: rolls the kill's item Drops against
    /// a <see cref="RollContext"/> built from the Encounter's own Location and hero (its loot
    /// table, source level and live magic find), tests each against
    /// <see cref="HeroBehaviour.AdmitsItem"/>, and offers a pass to the bag — one that does not
    /// fit, or that fails the filter, stays on the ground as a <see cref="GroundDrops"/> entry.
    /// Separately rolls one coin Pile per kill and banks it to the wallet iff
    /// <see cref="HeroBehaviour.AdmitsCoin"/> passes.
    ///
    /// The roll itself is delegated entirely to its collaborators — <see cref="ItemGenerator"/>
    /// and <see cref="ICoinDropSource"/> each own their own randomness — so this class owns only
    /// the count-and-filter decision and the placement wiring ("the sim asks 'did it fit' and
    /// records the answer", spec "Loot flow"). <see cref="ClearGround"/> is not wired to
    /// anything here — a caller (a test today, <see cref="RunState.RunEnded"/> for real, issue
    /// #26) calls it when a Run ends, on both outcomes (CONTEXT.md "Drop").
    /// </summary>
    public sealed class LootFlow : ILootGround
    {
        private readonly EncounterSimulation _encounter;
        private readonly HeroBehaviour _behaviour;
        private readonly ItemGenerator _items;
        private readonly ICoinDropSource _coins;
        private readonly AbstractDimensionalContainer _bag;
        private readonly Wallet _wallet;
        private readonly List<ItemInstance> _groundDrops = new();

        public LootFlow(
            EncounterSimulation encounter,
            HeroBehaviour behaviour,
            ItemGenerator items,
            ICoinDropSource coins,
            AbstractDimensionalContainer bag,
            Wallet wallet)
        {
            _encounter = encounter ?? throw new ArgumentNullException(nameof(encounter));
            _behaviour = behaviour ?? throw new ArgumentNullException(nameof(behaviour));
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _coins = coins ?? throw new ArgumentNullException(nameof(coins));
            _bag = bag ?? throw new ArgumentNullException(nameof(bag));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));

            _encounter.EnemyDefeated += OnEnemyDefeated;
        }

        /// <summary>
        /// Item Drops still lying on the ground — failed the loot filter, or passed it but did
        /// not fit the bag. Cleared by <see cref="ClearGround"/>, never by anything else; a
        /// picked-up Drop is simply not added here in the first place.
        /// </summary>
        public IReadOnlyList<ItemInstance> GroundDrops => _groundDrops;

        /// <summary>
        /// Discards every Drop still on the ground — the Run-end rule (CONTEXT.md "Drop": "a
        /// Drop still on the ground when the Run ends is gone, on Recall or Death alike").
        /// </summary>
        public void ClearGround() => _groundDrops.Clear();

        /// <summary>Unsubscribes from the encounter's events so the LootFlow can be collected.</summary>
        public void Dispose() => _encounter.EnemyDefeated -= OnEnemyDefeated;

        /// <summary>
        /// Seats <paramref name="item"/> on the ground — the corpse-recovery seat (issue #22):
        /// a re-entry lays the Corpse's contents out, to the bag where they fit and here where
        /// they do not, so a full bag stranding a recovery looks the same as a full bag
        /// stranding a kill. Shares the one <see cref="GroundDrops"/> list a Run-end clears.
        /// </summary>
        public void PlaceOnGround(ItemInstance item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            _groundDrops.Add(item);
        }

        /// <summary>
        /// Raised with the coin Pile's base-unit total each time a passed-filter Pile banks to
        /// the Wallet. The Run tracks the take this way — <see cref="RunState.CurrencyBanked"/>,
        /// the base the Death fee reads — so the engine-side driver feeds it
        /// <see cref="RunState.BankCurrency"/> per kill.
        /// </summary>
        public event Action<long> CoinsBanked;

        private void OnEnemyDefeated(Enemy enemy)
        {
            RollItems(enemy);
            RollCoinPile();
        }

        private void RollItems(Enemy enemy)
        {
            var hero = _encounter.Hero;
            var location = _encounter.Profile;

            var count = DropCountFor(enemy, hero);
            if (count <= 0)
                return;

            var context = new RollContext(location.Table, location.SourceLevel, hero.MagicFind);

            // OnEnemyDefeated runs synchronously inside EncounterSimulation's tick (issue #20's
            // ResolveCast loop calls Defeat once per falling target, in a row) - an exhausted or
            // misconfigured Location loot table throwing here must not propagate and abort the
            // rest of that tick. A kill's combat resolution is not allowed to depend on
            // itemization content being well-formed; treat a roll failure as "no items this kill".
            IReadOnlyList<ItemInstance> drops;
            try
            {
                drops = _items.RollLoot(context, count);
            }
            catch (InvalidOperationException)
            {
                return;
            }

            for (var i = 0; i < drops.Count; i++)
            {
                var item = drops[i];
                if (_behaviour.AdmitsItem(item.Rarity))
                {
                    var package = new Package(_bag, item, 1u);
                    if (_bag.TryAddToContainer(ref package))
                        continue; // landed in the bag
                }

                _groundDrops.Add(item);
            }
        }

        private void RollCoinPile()
        {
            var (type, amount) = _coins.RollPile();
            if (amount == 0u || type == CurrencyType.NONE)
                return;

            if (!_behaviour.AdmitsCoin(type))
                return;

            _wallet.Deposit(CurrencyOf(type, amount));
            CoinsBanked?.Invoke(BaseUnitsOf(type, amount));
        }

        /// <summary>The Pile's value in iron base units (CONTEXT.md "Base Unit"), for the Run's take.</summary>
        private static long BaseUnitsOf(CurrencyType type, uint amount) => type switch
        {
            CurrencyType.Iron => amount,
            CurrencyType.Copper => checked((long)amount * Currency.ironToCopper),
            CurrencyType.Silver => checked((long)amount * Currency.ironToSilver),
            CurrencyType.Gold => checked((long)amount * Currency.ironToGold),
            _ => 0L,
        };

        /// <summary>The archetype's base roll count plus the hero's <c>IncreasedItemQuantity</c> bonus.</summary>
        private static int DropCountFor(Enemy enemy, IHeroCombatant hero)
        {
            var baseCount = EnemyArchetypes.Of(enemy.Archetype).LootRolls;
            var bonus = (int)(hero.IncreasedItemQuantity / 100f); // mirrors ItemProvider.AddBonusDrops
            return Math.Max(0, baseCount + bonus);
        }

        private static Currency CurrencyOf(CurrencyType type, uint amount) => type switch
        {
            CurrencyType.Iron => new Currency(amount, 0u, 0u, 0u),
            CurrencyType.Copper => new Currency(0u, amount, 0u, 0u),
            CurrencyType.Silver => new Currency(0u, 0u, amount, 0u),
            CurrencyType.Gold => new Currency(0u, 0u, 0u, amount),
            _ => default,
        };
    }
}

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
    /// table, source level and live magic find), and lays each on the ground as a
    /// <see cref="GroundDrops"/> entry (issue #63): the player takes it through
    /// <see cref="PickUpFromGround"/> - the acquisition entry point (<see cref="IItemReceiver"/> —
    /// auto-equip, else the bag). With the debug switch <see cref="HeroBehaviour.AutoPickup"/> on,
    /// a Drop that passes <see cref="HeroBehaviour.AdmitsItem"/> is offered to that entry point
    /// on the spot instead, and only one that fails the filter or finds no room stays down.
    /// Separately rolls one coin Pile per kill and banks it to the wallet iff
    /// <see cref="HeroBehaviour.AdmitsCoin"/> passes.
    ///
    /// The roll itself is delegated entirely to its collaborators — <see cref="ItemGenerator"/>
    /// and <see cref="ICoinDropSource"/> each own their own randomness — so this class owns only
    /// the count-and-filter decision and the placement wiring ("the sim asks 'did it fit' and
    /// records the answer", spec "Loot flow"). <see cref="ClearGround"/> is not wired to
    /// anything here — a caller (a test today, <see cref="RunState.RunEnded"/> for real, issue
    /// #26) calls it when a Run ends, on both outcomes (GLOSSARY.md "Drop").
    /// </summary>
    public sealed class LootFlow : ILootGround
    {
        private readonly EncounterSimulation _encounter;
        private readonly HeroBehaviour _behaviour;
        private readonly ItemGenerator _items;
        private readonly ICoinDropSource _coins;
        private readonly IItemReceiver _player;
        private readonly Wallet _wallet;
        private readonly List<ItemInstance> _groundDrops = new();

        public LootFlow(
            EncounterSimulation encounter,
            HeroBehaviour behaviour,
            ItemGenerator items,
            ICoinDropSource coins,
            IItemReceiver player,
            Wallet wallet)
        {
            _encounter = encounter ?? throw new ArgumentNullException(nameof(encounter));
            _behaviour = behaviour ?? throw new ArgumentNullException(nameof(behaviour));
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _coins = coins ?? throw new ArgumentNullException(nameof(coins));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));

            _encounter.EnemyDefeated += OnEnemyDefeated;
        }

        /// <summary>
        /// Item Drops still lying on the ground - every kill's, until the player picks them up (or,
        /// with <see cref="HeroBehaviour.AutoPickup"/> on, the ones the filter or the bag turned away).
        /// Cleared by <see cref="ClearGround"/>; a Drop the player takes later leaves
        /// one at a time through <see cref="PickUpFromGround"/>. A Drop that was picked up on the
        /// spot is simply not added here in the first place.
        /// </summary>
        public IReadOnlyList<ItemInstance> GroundDrops => _groundDrops;

        /// <summary>
        /// Discards every Drop still on the ground — the Run-end rule (GLOSSARY.md "Drop": "a
        /// Drop still on the ground when the Run ends is gone, on Recall or Death alike").
        /// </summary>
        public void ClearGround()
        {
            if (_groundDrops.Count == 0)
                return;

            _groundDrops.Clear();
            GroundChanged?.Invoke();
        }

        /// <summary>
        /// Raised after <see cref="GroundDrops"/> gained or lost an entry - a kill grounding a Drop,
        /// a Quick Move to the ground, a pick-up, or the Run-end clear. Carries
        /// nothing: the Ground Items List (issue #63) re-reads the list, because
        /// <see cref="ItemInstance"/> is value-equal and an event naming one could not say which
        /// of two equal Drops it meant.
        /// </summary>
        public event Action GroundChanged;

        /// <summary>
        /// The player picks <paramref name="item"/> up off the ground, through the same acquisition
        /// entry point a kill's Drop goes through - auto-equip, else the bag. A pick-up that finds
        /// no room (or throws, surfaced through <see cref="PlacementFailed"/>) leaves the Drop
        /// where it lay. The Drop is found by reference, not by value: two equal swords on the
        /// ground are two slots, and the one clicked is the one that goes.
        /// </summary>
        /// <returns>Whether the player took it. False also for an item no longer on the ground.</returns>
        public bool PickUpFromGround(ItemInstance item)
        {
            var index = IndexOnGround(item);
            if (index < 0 || !TryPlace(item))
                return false;

            // TryPlace runs engine-side code that may itself have touched the ground; look again.
            index = IndexOnGround(item);
            if (0 <= index)
                _groundDrops.RemoveAt(index);

            GroundChanged?.Invoke();
            return true;
        }

        private int IndexOnGround(ItemInstance item)
        {
            for (var i = 0; i < _groundDrops.Count; i++)
                if (ReferenceEquals(_groundDrops[i], item))
                    return i;

            return -1;
        }

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
            GroundChanged?.Invoke();
        }

        /// <summary>
        /// Raised with the coin Pile's base-unit total each time a passed-filter Pile banks to
        /// the Wallet. The Run tracks the take this way — <see cref="RunState.CurrencyBanked"/>,
        /// the base the Death fee reads — so the engine-side driver feeds it
        /// <see cref="RunState.BankCurrency"/> per kill.
        /// </summary>
        public event Action<long> CoinsBanked;

        /// <summary>
        /// Raised when the player's pick-up threw for a Drop; the Drop stays on the ground. The
        /// engine-side driver logs it - this class stays free of engine calls.
        /// </summary>
        public event Action<ItemInstance, Exception> PlacementFailed;

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

            var grounded = false;
            for (var i = 0; i < drops.Count; i++)
            {
                var item = drops[i];

                // Picking up is the player's click (issue #63) unless the debug switch hands it back.
                if (_behaviour.AutoPickup && _behaviour.AdmitsItem(item.Rarity) && TryPlace(item))
                    continue; // equipped, or landed in the bag

                _groundDrops.Add(item);
                grounded = true;
            }

            // Once per kill, after the list is whole: a listener repaints one list, not one per Drop.
            if (grounded)
                GroundChanged?.Invoke();
        }

        /// <summary>
        /// Offers <paramref name="item"/> to the player. An equip applies stats and refreshes the
        /// character sheet, all of it engine-side code - and on an auto-pick-up it runs inside the
        /// same tick as the kill - so a throw there gets the roll's treatment above: the kill still
        /// resolves and the item stays on the ground, with the failure surfaced through
        /// <see cref="PlacementFailed"/> rather than swallowed. (A throw after a partial equip can
        /// leave the item both equipped and on the ground - the Run-end clear drops the copy;
        /// losing it would be worse.)
        /// </summary>
        private bool TryPlace(ItemInstance item)
        {
            try
            {
                return _player.PickUpItem(item, 1u);
            }
            catch (Exception exception)
            {
                PlacementFailed?.Invoke(item, exception);
                return false;
            }
        }

        private void RollCoinPile()
        {
            var (type, amount) = _coins.RollPile();
            if (amount == 0u || type == CurrencyType.NONE)
                return;

            if (!_behaviour.AdmitsCoin(type))
                return;

            _wallet.Deposit(Currency.Of(type, amount));
            CoinsBanked?.Invoke(checked((long)amount * Currency.ValueOf(type))); // the Pile's value in iron base units (GLOSSARY.md "Base Unit")
        }

        /// <summary>The archetype's base roll count plus the hero's <c>IncreasedItemQuantity</c> bonus.</summary>
        private static int DropCountFor(Enemy enemy, IHeroCombatant hero)
        {
            var baseCount = EnemyArchetypes.Of(enemy.Archetype).LootRolls;
            var bonus = (int)(hero.IncreasedItemQuantity / 100f); // mirrors ItemService.RollLoot's bonus drops
            return Math.Max(0, baseCount + bonus);
        }
    }
}

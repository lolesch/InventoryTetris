using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// Turns each of an Encounter's kills into Loot (issue #24; spec "Loot flow"). Subscribed
    /// to <see cref="EncounterSimulation.EnemyDefeated"/>: rolls the kill's item Drops against
    /// a <see cref="RollContext"/> built from the Encounter's own Location and hero (its loot
    /// table, source level and live magic find), and lays each on the <see cref="Ground"/>
    /// (issues #63, #216; the oldest make room when it is full): the player takes it through
    /// <see cref="PickUpFromGround"/> - the acquisition entry point (<see cref="IItemReceiver"/> —
    /// auto-equip, else the bag). With the debug switch <see cref="HeroBehaviour.AutoPickup"/> on,
    /// a Drop that passes <see cref="HeroBehaviour.AdmitsItem"/> is offered to that entry point
    /// on the spot instead, and only one that fails the filter or finds no room stays down.
    /// Separately rolls one coin Pile per kill and treats it the same way (issue #218): with auto-pickup on
    /// and <see cref="HeroBehaviour.AdmitsCoin"/> passing, the wallet takes what it can; the rest lies on
    /// the ground as a coin stack, which banks to the wallet when it is evicted or clicked.
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
        private readonly AbstractDimensionalContainer[] _pickUpContainers;

        public LootFlow(
            EncounterSimulation encounter,
            HeroBehaviour behaviour,
            ItemGenerator items,
            ICoinDropSource coins,
            IItemReceiver player,
            Wallet wallet,
            GroundContainer ground,
            params AbstractDimensionalContainer[] receivingContainers)
        {
            _encounter = encounter ?? throw new ArgumentNullException(nameof(encounter));
            _behaviour = behaviour ?? throw new ArgumentNullException(nameof(behaviour));
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _coins = coins ?? throw new ArgumentNullException(nameof(coins));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            Ground = ground ?? throw new ArgumentNullException(nameof(ground));

            // What a pick-up rolls back together: the ground it leaves and every container the player's
            // entry point may add to, so a stack that only partly fits leaves no half of it in the bag.
            _pickUpContainers = new AbstractDimensionalContainer[(receivingContainers?.Length ?? 0) + 1];
            _pickUpContainers[0] = ground;
            receivingContainers?.CopyTo(_pickUpContainers, 1);

            _encounter.EnemyDefeated += OnEnemyDefeated;
        }

        /// <summary>
        /// The ground itself: a stash-sized grid holding what lies there, where it landed, until the
        /// player picks it up or newer loot evicts it. Wiped by <see cref="ClearGround"/>.
        /// </summary>
        public GroundContainer Ground { get; }

        /// <summary>
        /// The Packages lying on the ground, oldest first - the Ground Items List's view of
        /// <see cref="Ground"/>. A stack is one entry. Builds a new list on every access, so read it once.
        /// </summary>
        public IReadOnlyList<Package> GroundDrops => Ground.PackagesOldestFirst();

        /// <summary>
        /// Discards every Drop still on the ground - the Run-end rule (GLOSSARY.md "Drop": "a
        /// Drop still on the ground when the Run ends is gone, on Recall or Death alike").
        /// </summary>
        public void ClearGround()
        {
            if (Ground.StoredPackages.Count == 0)
                return;

            Ground.RemoveAll();
            GroundChanged?.Invoke();
        }

        /// <summary>
        /// Raised after <see cref="Ground"/> changed - a kill grounding a Drop, a discard (which may
        /// evict the oldest), a pick-up, or the Run-end clear. Carries nothing: the Ground Items List
        /// re-reads the container, because <see cref="ItemInstance"/> is value-equal and an event
        /// naming one could not say which of two equal Drops it meant.
        /// </summary>
        public event Action GroundChanged;

        /// <summary>
        /// The player picks the Package of <paramref name="item"/> up off the ground, through the same
        /// acquisition entry point a kill's Drop goes through - auto-equip, else the bag. A pick-up that
        /// finds no room for all of it (or throws, surfaced through <see cref="PlacementFailed"/>) leaves
        /// the Package where it lay and the receiving containers as they were. The Package is found by
        /// reference, not by value: two equal swords on the ground are two slots, and the one clicked is
        /// the one that goes.
        /// </summary>
        /// <returns>Whether the player took it. False also for an item no longer on the ground.</returns>
        public bool PickUpFromGround(ItemInstance item)
        {
            if (!TryFindOnGround(item, out var cell, out var stored))
                return false;

            if (TryCoinOf(stored, out var coin))
            {
                // Coins bank rather than go through the entry point; what the Wallet cannot take stays.
                var taken = Bank(coin, stored.Amount);
                if (taken == 0u)
                    return false;

                _ = Ground.RemoveAtPosition(cell, new Package(Ground, stored.Item, taken));
                GroundChanged?.Invoke();
                return true;
            }

            using var transaction = new ItemTransaction(_pickUpContainers);

            _ = Ground.RemoveAtPosition(cell, stored); // reduces its own copy; `stored` stays whole

            if (!TryPlace(stored.Item, stored.Amount))
                return false; // dispose rolls back - the Package stays at its cell

            transaction.Commit();
            GroundChanged?.Invoke();
            return true;
        }

        private bool TryFindOnGround(ItemInstance item, out Vector2Int cell, out Package stored)
        {
            foreach (var entry in Ground.StoredPackages)
                if (ReferenceEquals(entry.Value.Item, item))
                {
                    cell = entry.Key;
                    stored = entry.Value;
                    return true;
                }

            cell = default;
            stored = default;
            return false;
        }

        /// <summary>Unsubscribes from the encounter's events so the LootFlow can be collected.</summary>
        public void Dispose() => _encounter.EnemyDefeated -= OnEnemyDefeated;

        /// <summary>
        /// Lays <paramref name="package"/> on the ground - a Quick Move or a drop on the floor slot -
        /// evicting the oldest Packages when it is full. A stack lands as one Package.
        /// </summary>
        /// <returns>False, with the ground untouched, for a Package larger than the whole ground.</returns>
        public bool PlaceOnGround(Package package)
        {
            if (!Land(package))
                return false;

            GroundChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Raised with the base-unit total of the coins each time some bank to the Wallet - a
        /// picked-up Pile, an evicted stack, a clicked stack. The Run tracks the take this way — <see cref="RunState.CurrencyBanked"/>,
        /// the base the Death fee reads — so the engine-side driver feeds it
        /// <see cref="RunState.BankCurrency"/> per kill.
        /// </summary>
        public event Action<long> CoinsBanked;

        /// <summary>
        /// Raised when the player's pick-up threw for a Drop (it stays on the ground), or when a new Drop
        /// or coin Pile could not land at all - too large for the ground, or a coin the Wallet cannot
        /// mint (the item is then null). The engine-side driver logs it - this class stays free of engine calls.
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

                grounded |= LandNew(item, 1u);
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
        private bool TryPlace(ItemInstance item, uint amount = 1u)
        {
            try
            {
                return _player.PickUpItem(item, amount);
            }
            catch (Exception exception)
            {
                PlacementFailed?.Invoke(item, exception);
                return false;
            }
        }

        /// <summary>
        /// A coin Pile follows the item rule: with auto-pickup on and the filter admitting the
        /// denomination the Wallet takes what it can, and whatever is left lands on the ground as a
        /// stack of that denomination's coin item.
        /// </summary>
        private void RollCoinPile()
        {
            var (type, amount) = _coins.RollPile();
            if (amount == 0u || type == CurrencyType.NONE)
                return;

            if (_behaviour.AutoPickup && _behaviour.AdmitsCoin(type))
                amount -= Bank(type, amount);

            if (0u < amount && LandNew(_wallet.MintCoin(type), amount))
                GroundChanged?.Invoke();
        }

        /// <summary>Lays <paramref name="package"/> on the ground; a coin stack pushed out banks first.</summary>
        private bool Land(Package package) => Ground.TryLand(package, BankEvicted);

        /// <summary>
        /// Lands a fresh Drop, or says through <see cref="PlacementFailed"/> that it could not: a null
        /// <paramref name="item"/> is a coin the Wallet cannot mint, anything else is larger than the ground.
        /// </summary>
        private bool LandNew(ItemInstance item, uint amount)
        {
            if (item != null && Land(new Package(null, item, amount)))
                return true;

            PlacementFailed?.Invoke(item, new InvalidOperationException(item == null
                ? "A Drop could not land on the ground: the Wallet cannot mint the coin."
                : $"A Drop could not land on the ground: {item.DefinitionId} does not fit it."));
            return false;
        }

        private void BankEvicted(Package evicted)
        {
            if (TryCoinOf(evicted, out var type))
                _ = Bank(type, evicted.Amount);
        }

        private bool TryCoinOf(Package package, out CurrencyType type)
        {
            var definition = Ground.ViewOf(package.Item).Definition;
            type = definition.CurrencyType;
            return definition.Category == ItemCategory.Currency;
        }

        /// <summary>
        /// Deposits <paramref name="amount"/> coins to the Wallet as far as it fits and reports the Run's
        /// take; the Wallet drops what does not fit, so the count taken is read off its balance.
        /// </summary>
        /// <returns>How many coins the Wallet took.</returns>
        private uint Bank(CurrencyType type, uint amount)
        {
            var value = Currency.ValueOf(type);
            var before = _wallet.Balance.Total;

            _wallet.Deposit(Currency.Of(type, amount));

            var taken = (_wallet.Balance.Total - before) / value;
            if (0u < taken)
                CoinsBanked?.Invoke(checked((long)taken * value)); // the Pile's value in iron base units (GLOSSARY.md "Base Unit")

            return taken;
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

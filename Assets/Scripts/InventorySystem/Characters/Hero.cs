using System;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Character
{
    /// <summary>
    /// The hero as a plain object: its stats and resources, level and XP, regeneration, the
    /// damage it deals and receives, the comparison of two modifiers, and the add/remove of an
    /// equipped item's stats. Built from a <see cref="HeroData"/> template, with no
    /// <c>GameObject</c>, so every one of those is testable on its own (issue #110, ADR-0015).
    ///
    /// Holds no view: the stat panel binds to the stats' change events instead of the model
    /// reaching into a UI pool, and no scene component wraps it: the Session holds it.
    /// Levelling up heals the hero itself, not through a locator.
    ///
    /// <see cref="IStatReceiver"/> is the seat the container core takes it by, and
    /// <see cref="IItemReceiver"/> the one loot, a Buy and the Corpse recovery put items through.
    ///
    /// What the hero owns besides its stats - Equipment, Inventory, Stash, Wallet and the
    /// Behaviour Profile - arrives through <see cref="Outfit"/>, after construction, because the
    /// Equipment takes the hero as its stat receiver and so cannot exist before it. The one place
    /// that order is written down is <c>SessionBuilder</c>; a hero that was never outfitted (the
    /// legacy dummy) holds none of it and says so when asked.
    /// </summary>
    public sealed class Hero : IStatReceiver, IItemReceiver
    {
        private readonly CharacterStat[] _stats;
        private readonly CharacterResource[] _resources;
        private readonly IReadOnlyList<CharacterStat> _statsView;
        private readonly IReadOnlyList<CharacterResource> _resourcesView;

        // Seconds each resource has been empty, tracked across calls so Regenerate stays
        // a pure function of elapsed time - see ResourceRegen.Step.
        private float _healthSecondsEmpty;
        private float _resourceSecondsEmpty;
        private float _shieldSecondsEmpty;

        private CharacterEquipment _equipment;
        private CharacterInventory _inventory;
        private CharacterInventory _stash;
        private Wallet _wallet;
        private HeroBehaviour _behaviour;

        /// <summary>A new hero from a template: every stat and resource at its base value, and full but for the XP.</summary>
        public Hero(HeroData data)
            : this(BuildStats(data), BuildResources(data), data.Level) => Template = data;

        /// <summary>
        /// A hero over stats that already exist - the legacy scene character whose stats are
        /// serialized on the component keeps its instances, so what its displays are bound to
        /// stays what the hero changes. Health, Resource and Shield start full, the XP empty.
        /// </summary>
        public Hero(IEnumerable<CharacterStat> stats, IEnumerable<CharacterResource> resources, uint level = 1u)
        {
            _stats = stats.ToArray();
            _resources = resources.ToArray();
            _statsView = Array.AsReadOnly(_stats);
            _resourcesView = Array.AsReadOnly(_resources);
            Level = level;

            // The hero owns its stats for its whole life, so it never needs to let go of them.
            foreach (var stat in _resources.Cast<CharacterStat>().Concat(_stats))
                stat.TotalHasChanged += _ => StatsChanged?.Invoke();

            RequireResource(StatName.Health).RefillCurrent();
            RequireResource(StatName.Resource).RefillCurrent();
            RequireResource(StatName.Shield).RefillCurrent();

            //TODO: design Experience
            RequireResource(StatName.Experience).DepleteCurrent();
        }

        /// <summary>Every stat that is not a resource.</summary>
        public IReadOnlyList<CharacterStat> Stats => _statsView;

        /// <summary>Health, Resource, Shield and Experience.</summary>
        public IReadOnlyList<CharacterResource> Resources => _resourcesView;

        public uint Level { get; private set; }

        /// <summary>The template this hero was built from: its icon and class name. <c>null</c> for the legacy hero over existing stats.</summary>
        public HeroData Template { get; }

        public bool IsInvincible { get; set; }
        public bool IsBlocking { get; set; }

        /// <summary>Whether casting costs Resource. Off, an attack is free.</summary>
        public bool SpendResource { get; set; } = true;

        public bool IsDead => GetResource(StatName.Health).IsDepleted;

        /// <summary>A hit this hero dealt: the type and the damage output, before the target mitigated it.</summary>
        public event Action<DamageType, float> DamageDealt;

        /// <summary>
        /// A hit this hero took: the type, what the Shield absorbed and what the Health lost, both
        /// after mitigation. Raised before the Health is taken off, so a listener that reacts to
        /// the death sees the hit that caused it first.
        /// </summary>
        public event Action<DamageType, float, float> DamageReceived;

        /// <summary>
        /// The total of any stat or resource changed: gear equipped or removed, a level-up's next
        /// threshold. What a view of the hero's numbers binds to (issue #111) - the hero knows no
        /// view. A resource's current value moving (a hit, a regeneration tick) is not a change of
        /// its total.
        /// </summary>
        public event Action StatsChanged;

        // --- what the hero owns -------------------------------------------------------------------

        /// <summary>What the hero wears. Throws if the hero was never <see cref="Outfit"/>ted.</summary>
        public CharacterEquipment Equipment => _equipment ?? throw NotOutfitted();

        /// <summary>The bag. Throws if the hero was never <see cref="Outfit"/>ted.</summary>
        public CharacterInventory Inventory => _inventory ?? throw NotOutfitted();

        /// <summary>The Stash, per hero for the MVP (ADR-0014). Throws if the hero was never <see cref="Outfit"/>ted.</summary>
        public CharacterInventory Stash => _stash ?? throw NotOutfitted();

        /// <summary>The spendable money, backed by <see cref="Inventory"/>'s coin cells. Throws if the hero was never <see cref="Outfit"/>ted.</summary>
        public Wallet Wallet => _wallet ?? throw NotOutfitted();

        /// <summary>The six sliders the player sets on the hero (Behaviour Profile). Throws if the hero was never <see cref="Outfit"/>ted.</summary>
        public HeroBehaviour Behaviour => _behaviour ?? throw NotOutfitted();

        /// <summary>
        /// The one Corpse a Death can leave behind (ADR-0009), the hero's own: it saves with it, and a
        /// hero that is replaced takes it along. Empty until a Death buries the bag in it; the
        /// settlement is the only thing that writes it.
        /// </summary>
        public Corpse Corpse { get; } = new();

        /// <summary>The Location a Send will (or last did) go to. <c>null</c> until the hero was first sent.</summary>
        public LocationConfig SelectedLocation { get; set; }

        /// <summary>Whether <see cref="Outfit"/> has run.</summary>
        public bool IsOutfitted => _equipment != null;

        /// <summary>
        /// Hands the hero what it owns, once. Called by the builder after the hero exists, because
        /// the <paramref name="equipment"/> was built with the hero as its stat receiver.
        /// </summary>
        public void Outfit(CharacterEquipment equipment, CharacterInventory inventory, CharacterInventory stash,
            Wallet wallet, HeroBehaviour behaviour)
        {
            if (IsOutfitted)
                throw new InvalidOperationException("This Hero is already outfitted; build a new one instead of outfitting it twice.");

            _equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _stash = stash ?? throw new ArgumentNullException(nameof(stash));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _behaviour = behaviour ?? throw new ArgumentNullException(nameof(behaviour));
        }

        /// <summary>
        /// The placement every acquisition shares: auto-equip into an empty slot, else the
        /// Inventory, and nothing behind that. <c>false</c> means no room, so the caller decides
        /// what that costs (<see cref="ItemAcquisition"/>).
        /// </summary>
        public bool PickUpItem(ItemInstance item, uint amount)
        {
            var package = new Package(null, item, amount);
            return ItemAcquisition.TryPlace(ref package, Equipment, Inventory);
        }

        private static InvalidOperationException NotOutfitted() =>
            new("This Hero has no Equipment, Inventory, Stash, Wallet or Behaviour Profile: it was never outfitted by the builder.");

        // --- stats --------------------------------------------------------------------------------

        /// <summary>The stat or resource called <paramref name="stat"/>, or <c>null</c> if the hero has none.</summary>
        public CharacterStat GetStat(StatName stat)
        {
            // Resources first, matching the old reverse-iterated Union(stats, resources).
            for (var i = _resources.Length; i-- > 0;)
                if (_resources[i].Stat == stat)
                    return _resources[i];

            for (var i = _stats.Length; i-- > 0;)
                if (_stats[i].Stat == stat)
                    return _stats[i];

            return null;
        }

        public CharacterResource GetResource(StatName resource)
        {
            for (var i = _resources.Length; i-- > 0;)
                if (_resources[i].Stat == resource)
                    return _resources[i];

            return null;
        }

        /// <summary>The stat's total, or 0 for a stat the template never authored, so a sparse template
        /// costs a bonus rather than a NullReferenceException on every tick.</summary>
        public float GetStatValue(StatName stat) => GetStat(stat)?.TotalValue ?? 0f;

        public void AddItemStats(IReadOnlyList<CharacterStatModifier> stats)
        {
            foreach (var itemStat in stats)
                GetStat(itemStat.Stat)?.AddModifier(itemStat.Modifier);
        }

        public void RemoveItemStats(IReadOnlyList<CharacterStatModifier> stats)
        {
            foreach (var itemStat in stats)
                if (GetStat(itemStat.Stat)?.TryRemoveModifier(itemStat.Modifier) != true)
                    Debug.LogWarning($"could not remove {itemStat.Stat} modifier {itemStat.Modifier}!");
        }

        public float CompareStatModifiers(CharacterStatModifier playerStatModifier, StatModifier other) =>
            CompareStatModifiers(playerStatModifier.Stat, playerStatModifier.Modifier, other);

        /// <summary>
        /// What swapping the worn <paramref name="other"/> for the hovered <paramref name="current"/>
        /// would do to the stat's total: positive when the hovered modifier is the better one. Works
        /// on copies, so the hero's own stat is never touched.
        /// </summary>
        public float CompareStatModifiers(StatName stat, StatModifier current, StatModifier other)
        {
            var currentStat = GetStat(stat);
            var clonedStat = currentStat.GetDeepCopy();
            var clonedStat2 = currentStat.GetDeepCopy();

            if (clonedStat.TryRemoveModifier(current))
                clonedStat.AddModifier(other);

            if (clonedStat2.TryRemoveModifier(other))
                clonedStat2.AddModifier(current);

            return clonedStat2.TotalValue - clonedStat.TotalValue;
            //return currentStat.TotalValue - clonedStat.TotalValue;
        }

        // --- regeneration and healing -------------------------------------------------------------

        /// <summary>
        /// Applies one step of Health, Resource and Shield regeneration for
        /// <paramref name="deltaSeconds"/> of elapsed time. Driven by the simulation driver at
        /// sim speed, in both Town and Field (issue #45). A dead hero's Health does not come
        /// back, but the caller decides whether to tick one at all.
        /// </summary>
        public void Regenerate(float deltaSeconds)
        {
            _healthSecondsEmpty = ResourceRegen.Step(
                GetResource(StatName.Health),
                GetStatValue(StatName.HealthRegeneration),
                recoveryDelay: -1f, _healthSecondsEmpty, deltaSeconds);

            _resourceSecondsEmpty = ResourceRegen.Step(
                GetResource(StatName.Resource),
                GetStatValue(StatName.ResourceRegeneration),
                recoveryDelay: 0f, _resourceSecondsEmpty, deltaSeconds);

            //TODO: design Shield recharge
            _shieldSecondsEmpty = ResourceRegen.Step(
                GetResource(StatName.Shield),
                GetStatValue(StatName.HealthRegeneration),
                recoveryDelay: 2f, _shieldSecondsEmpty, deltaSeconds);
        }

        /// <summary>A full Health and Resource refill: the Healer's, and a level-up's.</summary>
        public void Heal()
        {
            GetResource(StatName.Health).RefillCurrent();
            GetResource(StatName.Resource).RefillCurrent();
        }

        // --- level and XP -------------------------------------------------------------------------

        public void GainExperience(float exp, uint monsterLevel)
        {
            if (IsDead)
                return;

            //TODO: design exp gain
            // Signed: both levels are uint, so a monster below the hero would wrap to ~4 billion.
            var levelDifference = (float)monsterLevel - Level;
            var levelBalanceExp = exp * Mathf.Max(0f, 1f + levelDifference / 100f);
            var experience = GetResource(StatName.Experience);

            while (0 < levelBalanceExp)
            {
                levelBalanceExp = experience.AddToCurrent(levelBalanceExp);

                if (experience.IsFull)
                {
                    Level++;

                    experience.AddModifier(LevelProgression.ExperienceThresholdModifier(Level));
                    experience.DepleteCurrent();

                    Heal();
                }
            }
        }

        /// <summary>
        /// Puts a saved level back: the hero carries the Experience modifiers of every level it climbed
        /// past, exactly as if it had earned them, without replaying the XP gain, the heal or the
        /// depleted bar. Touches no current value, so the caller sets those after the maximums are in
        /// place. Only climbs: a level below the current one throws.
        /// </summary>
        public void RestoreLevel(uint level)
        {
            if (level < Level)
                throw new ArgumentOutOfRangeException(nameof(level), level, $"A hero at level {Level} cannot be restored to a lower level.");

            var experience = RequireResource(StatName.Experience);

            while (Level < level)
            {
                Level++;
                experience.AddModifier(LevelProgression.ExperienceThresholdModifier(Level));
            }
        }

        // --- damage -------------------------------------------------------------------------------

        /// <summary>
        /// Pays the attack's Resource cost and hits <paramref name="target"/> for this hero's damage
        /// output. Nothing happens if this hero is dead or cannot pay.
        /// </summary>
        public void DealDamageTo(Hero target, DamageType damageType)
        {
            if (IsDead)
                return;

            var resourceCost = CalculateRequiredResource(damageType);
            var resource = GetResource(StatName.Resource);

            // TODO: if Shield protects resource
            //var shield = GetResource(this, StatName.Shield);

            //resourceCost = shield.RemoveFromCurrent(resourceCost);

            if (resourceCost == 0 || resourceCost <= resource.CurrentValue)
            {
                resource.RemoveFromCurrent(resourceCost);

                var damageOutput = CalculateDamageOutput(damageType);

                DamageDealt?.Invoke(damageType, damageOutput);
                target.ReceiveDamage(damageType, damageOutput);
            }

            //AddDealtDPS(damageOutput);
        }

        /// <summary>
        /// The incoming-damage path: mitigate by the matching resist, spend the Shield, then the
        /// Health. The Encounter sim's enemies are not heroes, so the hero adapter (issue #43)
        /// routes their Strikes straight here.
        /// </summary>
        public void ReceiveDamage(DamageType damageType, float incomingDamage)
        {
            var health = GetResource(StatName.Health);

            if (health.IsDepleted)
                return;

            var mitigatedDamage = CalculateReceivingDamage(damageType, incomingDamage);

            // TODO: if Shield protects resource instead => then skip shielding
            var shield = GetResource(StatName.Shield);

            var unshieldedDamage = shield.RemoveFromCurrent(mitigatedDamage);

            DamageReceived?.Invoke(damageType,
                Mathf.Min(mitigatedDamage - unshieldedDamage, shield.TotalValue),
                Mathf.Min(unshieldedDamage, health.TotalValue));

            health.RemoveFromCurrent(unshieldedDamage);

            //AddReceivedDPS(healthDamage);
        }

        public float CalculateRequiredResource(DamageType damageType)
        {
            if (!SpendResource)
                return 0;

            var resource = GetStatValue(StatName.Resource);

            // implementation is just for testing
            var damageTypeMod = damageType switch
            {
                DamageType.PhysicalDamage => .03f,
                DamageType.MagicalDamage => .1f,

                _ => 0f,
            };

            return resource * damageTypeMod;
        }

        public float CalculateDamageOutput(DamageType damageType)
        {
            // TODO: if attackSpeed has only percantMods and a base of 0 it will return 0
            var attackSpeed = GetStatValue(StatName.AttackSpeed);
            var damageTypeMod = damageType switch
            {
                DamageType.PhysicalDamage => GetStatValue(StatName.PhysicalDamage),
                DamageType.MagicalDamage => GetStatValue(StatName.MagicalDamage),

                _ => 0f,
            };

            return damageTypeMod * (1f + attackSpeed * 0.01f); // * (1f + damageTypeMod * 0.01f);
        }

        public float CalculateReceivingDamage(DamageType damageType, float incomingDamage)
        {
            if (IsInvincible)
                return 0f;

            var damageTypeResist = damageType switch
            {
                DamageType.PhysicalDamage => GetStatValue(StatName.Armor),
                DamageType.MagicalDamage => GetStatValue(StatName.MagicResist),

                _ => 0f,
            };

            // TODO: Desing defenses
            // Avoidance (block, dodge, barrier absorbtion)
            // Mitigation (resistances, armor)
            // Recovery

            // TODO: this linear "percent mitigated" formula has no diminishing returns, so a
            // resist stat past 100 flips mitigation negative and a "hit" heals instead of damages
            // (CharacterResource.RemoveFromCurrent has no floor on a negative amount). Gear
            // rolls already stack Health/Regen well past base within a couple of items (issue
            // seen 2026-09-27: 3 pieces took Health from 640 to 1037), so Armor/MagicResist will
            // eventually hit the same wall. Clamping below is a stopgap, not the fix - needs a
            // real formula (e.g. armor / (armor + K)) that approaches but never reaches 100%
            // mitigation, however high the stat climbs.
            var clampedResist = Mathf.Clamp(damageTypeResist, 0f, 100f);
            var mitigatedDamage = incomingDamage * (1f - clampedResist * 0.01f);

            return mitigatedDamage;
        }

        // --- construction from the template -------------------------------------------------------

        // A template without one of the four resources is mis-authored: fail at construction, naming
        // it, rather than as a NullReferenceException on the first hit or regeneration tick.
        private CharacterResource RequireResource(StatName resource) =>
            GetResource(resource) ?? throw new InvalidOperationException($"A Hero needs a {resource} resource; its template has none.");

        private static IEnumerable<CharacterStat> BuildStats(HeroData data) =>
            Require(data).Stats.Select(entry => new CharacterStat(entry.Stat, entry.BaseValue));

        private static IEnumerable<CharacterResource> BuildResources(HeroData data) =>
            Require(data).Resources.Select(entry => new CharacterResource(entry.Stat, entry.BaseValue));

        // Unity's lifetime-aware == : a destroyed template is not literally null.
        private static HeroData Require(HeroData data) =>
            data != null ? data : throw new ArgumentNullException(nameof(data));
    }
}

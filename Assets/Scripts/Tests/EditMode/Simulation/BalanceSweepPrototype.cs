// PROTOTYPE (#296, map #293) - throwaway balance sweep. Lives on branch prototype/296-balance-sweep only; never merge.
// Runs the real EncounterSimulation headless with a SweepHero that carries the Run-risk levers of
// dev/specs/2026-10-17-run-risk-levers-design.md (no level-up heal, base Health regen 0, a flat growth table),
// a Location Population (ADR-0023) and a Recall whose portal opens over 0.5 s of sim time. Loot is the real thing:
// the real ItemGenerator over the real Item Catalog asset, the authored Thornwood / Ashfall category and rarity
// odds, real affix pools, and the real stat math (flat, then percent-add, then percent-mult). Writes
// dev/prototypes/2026-10-17-balance-sweep/out-*.md.
//
//   dev/run-tests.sh EditMode BalanceSweepPrototype
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation.BalanceSweep
{
    // ─── knobs ──────────────────────────────────────────────────────────────────────────────────

    internal static class Knobs
    {
        // The starter hero. Stats absent here are 0. The live DefaultHero.asset is
        // Health 640, Armor 26, MR 30, Phys 59, Mag 0, AS 0.658, Resource 280 @ 7/s, Health regen 3.5.
        public static readonly Dictionary<StatName, float> Base = new()
        {
            [StatName.Health] = 400, [StatName.Armor] = 12, [StatName.MagicResist] = 12,
            [StatName.PhysicalDamage] = 32, [StatName.MagicalDamage] = 14, [StatName.AttackSpeed] = 0.75f,
            [StatName.HealthRegeneration] = 0, [StatName.Resource] = 200, [StatName.ResourceRegeneration] = 6,
            [StatName.MovementSpeed] = 325,
        };

        // Flat, per level reached past 1. No heal.
        public static readonly Dictionary<StatName, float> Growth = new()
        {
            [StatName.Health] = 20, [StatName.PhysicalDamage] = 2.5f, [StatName.MagicalDamage] = 2.5f, [StatName.Resource] = 8,
        };

        public static int ThornwoodPopulation = 5, AshfallPopulation = 6, CurvePopulation = 5;

        // Mitigation: 0 = the live linear percent (Hero.CalculateReceivingDamage, clamped 0..100);
        // K > 0 = diminishing, R / (R + K), so no stack of Armor or Magic Resist reaches immunity.
        public static float MitigationK = 100f;

        public static float Mitigation(float resist) =>
            MitigationK > 0f ? Math.Max(0f, resist) / (Math.Max(0f, resist) + MitigationK) : Math.Clamp(resist, 0f, 100f) / 100f;

        public const float RecallAt = 0.35f;     // a played hero opens a portal at this Health fraction
        public const float PortalOpening = 0.5f; // sim seconds the portal takes to open (town-portal spec)
        public const float TownTrip = 15f;       // sim seconds a trip to the Healer and back costs
        public const float SendCap = 600f;
        public const float XpLossFraction = 0.25f;
        public const int Engagement = 3;
        public const int CastCost = 16;
        public const int Seeds = 8;
        public const int CampaignSeeds = 20;
        public const int CampaignMaxSends = 30;

        // A potion is not built (effects model, #298; belt, #300). A played hero drinks one at RecallAt before he
        // opens a portal, if he carries one; it heals this fraction of his Health at once. The careless hero never does.
        public const float PotionHealFraction = 0.35f;
        public const int PotionCarry = 8; // two belt columns of four
    }

    // ─── loot variants ──────────────────────────────────────────────────────────────────────────

    internal sealed class Table : LootTable
    {
        public IReadOnlyList<float> CategoryOdds { get; set; }
        public IReadOnlyList<float> RarityOdds { get; set; }

        // Enum declaration order: ItemCategory NONE, Consumable, Equipment, Currency;
        // ItemRarity NoDrop, Common, Magic, Rare, Unique.
        public static Table Of(float consumable, float equipment, float currency, float fail, float common, float magic, float rare, float unique) => new()
        {
            CategoryOdds = Normalized(0f, consumable, equipment, currency),
            RarityOdds = Normalized(fail, common, magic, rare, unique),
        };

        private static IReadOnlyList<float> Normalized(params float[] w)
        {
            var sum = w.Sum();
            return w.Select(x => x / sum).ToArray();
        }
    }

    internal sealed class LootVariant
    {
        public string Name;
        public string Description;
        public Table Thornwood, Ashfall;
        public bool RarityPicksUniqueDefinition; // false: the live generator's uniform pick over every definition
        public float ItemLevelScale;              // flat affix value x (1 + scale x (itemLevel - 1)); live code: 0
        public int[] ThornwoodDeck, AshfallDeck;  // card counts NoDrop, Common, Magic, Rare, Unique: dealt without replacement, reshuffled when spent

        public int[] DeckFor(Loc loc) => loc.S <= 3 ? ThornwoodDeck : AshfallDeck;

        public Table TableFor(Loc loc) => loc.S <= 3 ? Thornwood : Ashfall;

        public static readonly LootVariant Live = new()
        {
            Name = "live",
            Description = "The code and data as they are: authored tables (Fail Bucket 0), the generator's uniform pick over all 180 definitions (156 are Uniques), no item-level scaling.",
            Thornwood = Table.Of(1, 4, 1, 0, 160, 80, 40, 20),
            Ashfall = Table.Of(1, 6, 1, 0, 80, 80, 60, 30),
            RarityPicksUniqueDefinition = false,
            ItemLevelScale = 0f,
        };

        public static readonly LootVariant Proposed = new()
        {
            Name = "proposed",
            Description = "Fail Bucket ~76% of kills; the rarity roll comes first and only a Unique roll picks a Unique definition (~1 in 500 kills); flat affixes scale +25% per item level past 1.",
            Thornwood = Table.Of(1, 4, 1, 950, 160, 80, 40, 2.5f),
            Ashfall = Table.Of(1, 6, 1, 800, 80, 80, 60, 3f),
            RarityPicksUniqueDefinition = true,
            ItemLevelScale = 0.25f,
        };

        public static readonly LootVariant Deck = new()
        {
            Name = "deck",
            Description = "Proposed, with the rarity dealt from a card deck of 1000 per Location per hero (760 NoDrop, 2 Unique): exactly 1 Unique in every 500 kills, and no long dry streak.",
            Thornwood = Table.Of(1, 4, 1, 950, 160, 80, 40, 2.5f),
            Ashfall = Table.Of(1, 6, 1, 800, 80, 80, 60, 3f),
            RarityPicksUniqueDefinition = true,
            ItemLevelScale = 0.25f,
            ThornwoodDeck = new[] { 760, 128, 64, 46, 2 },
            AshfallDeck = new[] { 760, 80, 80, 78, 2 },
        };

        public static readonly LootVariant[] All = { Live, Proposed, Deck };
    }

    internal sealed class Looter
    {
        private static IItemCatalog _catalog;
        private readonly LootVariant _variant;
        private readonly ItemGenerator _generator;
        private readonly Random _rng;

        public Looter(LootVariant variant, int seed)
        {
            _variant = variant;
            _rng = new Random(seed * 17 + 5);
            _generator = new ItemGenerator(Catalog, new SeededRollSource(seed));
        }

        public static IItemCatalog Catalog => _catalog ??= UnityEditor.AssetDatabase.LoadAssetAtPath<ItemCatalogAsset>(
            "Assets/Scripts/InventorySystem/Data/ItemDefinitions/Item Catalog.asset")
            ?? throw new InvalidOperationException("Item Catalog asset not found");

        public static ItemDefinition Definition(ItemInstance item) => Catalog.Definition(item.DefinitionId);

        /// <summary>One kill's drop (each archetype rolls once), or null for the Fail Bucket.</summary>
        public ItemInstance Roll(Loc loc)
        {
            var table = _variant.TableFor(loc);
            if (!_variant.RarityPicksUniqueDefinition)
            {
                try { return _generator.Roll(new RollContext(table, loc.S, 0f)); }
                catch (InvalidOperationException) { return null; } // LootFlow: a NoDrop roll throws, "no items this kill"
            }

            var category = Sample(table.CategoryOdds, new[] { ItemCategory.NONE, ItemCategory.Consumable, ItemCategory.Equipment, ItemCategory.Currency });
            var rarity = _variant.DeckFor(loc) is { } deck
                ? Deal(loc.S, deck)
                : Sample(table.RarityOdds, new[] { ItemRarity.NoDrop, ItemRarity.Common, ItemRarity.Magic, ItemRarity.Rare, ItemRarity.Unique });
            if (rarity == ItemRarity.NoDrop) return null;
            var wantUnique = rarity == ItemRarity.Unique;
            var pool = Catalog.OfCategory(category).Where(d => d.IsUnique == wantUnique).ToList();
            if (pool.Count == 0) pool = Catalog.OfCategory(category).ToList();
            return _generator.Roll(pool[_rng.Next(pool.Count)], rarity, loc.S);
        }

        private readonly Dictionary<int, List<ItemRarity>> _decks = new();

        private ItemRarity Deal(int key, int[] counts)
        {
            if (!_decks.TryGetValue(key, out var deck) || deck.Count == 0)
            {
                var rarities = new[] { ItemRarity.NoDrop, ItemRarity.Common, ItemRarity.Magic, ItemRarity.Rare, ItemRarity.Unique };
                deck = new List<ItemRarity>();
                for (var i = 0; i < counts.Length; i++) deck.AddRange(Enumerable.Repeat(rarities[i], counts[i]));
                for (var i = deck.Count - 1; i > 0; i--) { var j = _rng.Next(i + 1); (deck[i], deck[j]) = (deck[j], deck[i]); }
                _decks[key] = deck;
            }
            var card = deck[^1];
            deck.RemoveAt(deck.Count - 1);
            return card;
        }

        private T Sample<T>(IReadOnlyList<float> odds, T[] outcomes)
        {
            var x = (float)_rng.NextDouble();
            for (var i = 0; i < odds.Count; i++)
            {
                if (x < odds[i]) return outcomes[i];
                x -= odds[i];
            }
            return outcomes[^1];
        }

        public float ScaleOf(ItemInstance item) => 1f + _variant.ItemLevelScale * Math.Max(0, item.ItemLevel - 1);
    }

    // ─── the hero ───────────────────────────────────────────────────────────────────────────────

    internal enum Build { Physical, Magical }

    internal sealed class Gear
    {
        public readonly Dictionary<string, (ItemInstance item, float scale)> Equipped = new();

        public Gear Clone()
        {
            var g = new Gear();
            foreach (var kv in Equipped) g.Equipped[kv.Key] = kv.Value;
            return g;
        }

        // 14 slots, as the Equipment container (GameConfig EquipmentSize 14).
        public static string[] SlotsFor(EquipmentType type) => type switch
        {
            EquipmentType.Ring => new[] { "Ring1", "Ring2" },
            EquipmentType.Amulet => new[] { "Amulet" },
            EquipmentType.Shield or EquipmentType.Quiver => new[] { "OffHand" },
            EquipmentType.Sword or EquipmentType.Bow or EquipmentType.Crossbow or EquipmentType.GreatSword => new[] { "MainHand" },
            EquipmentType.NONE => Array.Empty<string>(),
            _ => new[] { type.ToString() },
        };
    }

    internal sealed class SweepHero : IHeroCombatant
    {
        private readonly Dictionary<StatName, float> _total = new();
        private Gear _gear;

        public SweepHero(Build build, int level, Gear gear = null)
        {
            Build = build;
            Level = level;
            _gear = gear ?? new Gear();
            Refresh();
            FullHeal();
        }

        public Build Build { get; }
        public Gear Gear
        {
            get => _gear;
            set { _gear = value; Refresh(); }
        }

        public int Potions { get; set; }
        public float XpIntoLevel { get; private set; }

        public float Stat(StatName s) => _total.TryGetValue(s, out var v) ? v : 0f;

        private void Refresh() => Compute(_gear, _total);

        // MutableFloat: (base + sum of flat) x (1 + sum of percent-add / 100) x product of (1 + percent-mult / 100).
        public void Compute(Gear gear, Dictionary<StatName, float> into)
        {
            var flat = new Dictionary<StatName, float>();
            var pct = new Dictionary<StatName, float>();
            var mult = new Dictionary<StatName, float>();
            foreach (var kv in Knobs.Base) flat[kv.Key] = kv.Value;
            foreach (var kv in Knobs.Growth) flat[kv.Key] = flat.GetValueOrDefault(kv.Key) + kv.Value * (Level - 1);
            foreach (var (item, scale) in gear.Equipped.Values)
                foreach (var affix in item.Affixes)
                {
                    var m = affix.Modifier;
                    switch (m.Type)
                    {
                        case StatModifierType.FlatAdd: flat[affix.Stat] = flat.GetValueOrDefault(affix.Stat) + m.Value * scale; break;
                        case StatModifierType.PercentAdd: pct[affix.Stat] = pct.GetValueOrDefault(affix.Stat) + m.Value; break;
                        case StatModifierType.PercentMult: mult[affix.Stat] = (mult.TryGetValue(affix.Stat, out var p) ? p : 1f) * (1f + m.Value / 100f); break;
                    }
                }
            into.Clear();
            foreach (var s in flat.Keys.Concat(pct.Keys).Concat(mult.Keys).Distinct())
                into[s] = flat.GetValueOrDefault(s) * (1f + pct.GetValueOrDefault(s) / 100f) * (mult.TryGetValue(s, out var mm) ? mm : 1f);
            if (Build == Build.Magical && into.ContainsKey(StatName.PhysicalDamage)) into[StatName.PhysicalDamage] *= 0.5f; // a caster swings weakly
        }

        /// <summary>How good a gear set is for this hero: effective Health x damage per second (Strike + Cast).</summary>
        public float Score(Gear gear)
        {
            var t = new Dictionary<StatName, float>();
            Compute(gear, t);
            float S(StatName s) => t.TryGetValue(s, out var v) ? v : 0f;
            var taken = (1f - Knobs.Mitigation(S(StatName.Armor)) + 1f - Knobs.Mitigation(S(StatName.MagicResist))) / 2f;
            var ehp = S(StatName.Health) / Math.Max(0.05f, taken) + S(StatName.HealthRegeneration) * 60f;
            var castsPerSecond = S(StatName.ResourceRegeneration) / Knobs.CastCost;
            var dps = S(StatName.PhysicalDamage) * S(StatName.AttackSpeed) + S(StatName.MagicalDamage) * castsPerSecond * 1.5f;
            return ehp * dps;
        }

        public bool Offer(ItemInstance item, float scale)
        {
            var def = Looter.Definition(item);
            if (def.Category != ItemCategory.Equipment) return false;
            var best = _gear;
            var bestScore = Score(_gear);
            foreach (var slot in Gear.SlotsFor(def.EquipmentType))
            {
                var trial = _gear.Clone();
                trial.Equipped[slot] = (item, scale);
                var s = Score(trial);
                if (s > bestScore) { best = trial; bestScore = s; }
            }
            if (ReferenceEquals(best, _gear)) return false;
            Gear = best;
            return true;
        }

        public static float BarMax(int level)
        {
            var max = 280f;
            for (var l = 2; l <= level; l++) max += l * 100 + 80;
            return max;
        }

        public void GainXp(int xp)
        {
            XpIntoLevel += xp;
            while (XpIntoLevel >= BarMax(Level))
            {
                XpIntoLevel -= BarMax(Level);
                Level++;
                Refresh(); // the growth, and no heal (run-risk spec)
            }
        }

        public void LoseXpOnDeath() => XpIntoLevel *= 1f - Knobs.XpLossFraction;

        public void FullHeal()
        {
            Health = MaxHealth;
            Resource = MaxResource;
        }

        public float Health { get; set; }
        public float MaxHealth => Stat(StatName.Health);
        public float HealthFraction => MaxHealth <= 0f ? 0f : Math.Clamp(Health / MaxHealth, 0f, 1f);
        public bool IsDown => Health <= 0f;
        public float Resource { get; set; }
        public float MaxResource => Stat(StatName.Resource);
        public float ResourceFraction => MaxResource <= 0f ? 0f : Math.Clamp(Resource / MaxResource, 0f, 1f);
        public float PhysicalDamage => Stat(StatName.PhysicalDamage);
        public float AttackSpeed => Stat(StatName.AttackSpeed);
        public float MagicalDamage => Stat(StatName.MagicalDamage);
        public float CastCost => Knobs.CastCost;
        public float MovementSpeed => Stat(StatName.MovementSpeed);
        public float? WeaponStrikeRange => null;
        public int Level { get; private set; }
        public float MagicFind => 0f;
        public float IncreasedItemQuantity => 0f;

        public float ReceivePhysical(float raw) => Take(raw, Stat(StatName.Armor));
        public float ReceiveMagical(float raw) => Take(raw, Stat(StatName.MagicResist));

        // Hero.CalculateReceivingDamage: linear percent, clamped 0..100.
        private float Take(float raw, float resist)
        {
            if (raw <= 0f || Health <= 0f) return 0f;
            var lost = Math.Min(Health, raw * (1f - Knobs.Mitigation(resist)));
            Health -= lost;
            return lost;
        }

        public void SpendResource(float amount) => Resource = Math.Max(0f, Resource - amount);

        public void Regenerate(float dt)
        {
            if (Health > 0f) Health = Math.Min(MaxHealth, Health + Stat(StatName.HealthRegeneration) * dt);
            Resource = Math.Min(MaxResource, Resource + Stat(StatName.ResourceRegeneration) * dt);
        }
    }

    // ─── Locations ──────────────────────────────────────────────────────────────────────────────

    internal sealed class Loc
    {
        public string Name;
        public int S;
        public EnemyArchetype Packed;
        public IntRange Brute, Skirmisher, PackBatch;
        public float PackedWeight, Interval;
        public int Population;

        public int KillsPerSend => Population * (Brute.Min + Skirmisher.Min);

        public EncounterProfile Profile() => new(S, Packed, Brute, Skirmisher, PackBatch, PackedWeight, Interval,
            FakeLootTable.ForCategory(ItemCategory.Equipment), 0f, 2);

        // Thornwood's shape: Brute-packed, 9 + 5. Ashfall's: Skirmisher-packed, 4 + 13.
        public static Loc BrutePacked(int s, int population) => new()
        {
            Name = $"S{s} Brute", S = s, Packed = EnemyArchetype.Brute, Brute = new IntRange(9), Skirmisher = new IntRange(5),
            PackBatch = new IntRange(2, 3), PackedWeight = 0.5f, Interval = 2.4f, Population = population,
        };

        public static Loc SkirmisherPacked(int s, int population) => new()
        {
            Name = $"S{s} Skirm", S = s, Packed = EnemyArchetype.Skirmisher, Brute = new IntRange(4), Skirmisher = new IntRange(13),
            PackBatch = new IntRange(3, 5), PackedWeight = 0.7f, Interval = 3.6f, Population = population,
        };

        public static Loc Thornwood() { var l = BrutePacked(2, Knobs.ThornwoodPopulation); l.Name = "Thornwood"; return l; }
        public static Loc Ashfall() { var l = SkirmisherPacked(5, Knobs.AshfallPopulation); l.Name = "Ashfall"; return l; }
    }

    // ─── one Send ───────────────────────────────────────────────────────────────────────────────

    internal enum Policy { Careless, Played }

    internal sealed class SendResult
    {
        public bool Completed, Died, DiedOpening;
        public float Time, CompleteTime = -1f;
        public int Encounters, Kills, Trips, PotionsDrunk;
        public readonly List<ItemInstance> Drops = new();
    }

    internal static class Sender
    {
        public static SendResult Run(SweepHero hero, Loc loc, Policy policy, int seed, bool keepGoing = false, Looter looter = null)
        {
            var r = new SendResult();
            var behaviour = new HeroBehaviour { Engagement = Knobs.Engagement };
            var streams = new RollStreams(new SeededRollSource(seed), new SeededRollSource(seed * 7 + 1), new SeededRollSource(seed * 13 + 2));
            var sim = new EncounterSimulation(hero, loc.Profile(), streams, behaviour, EncounterTuning.Standard());

            sim.XpGained += hero.GainXp;
            sim.EnemyDefeated += _ =>
            {
                r.Kills++;
                var item = looter?.Roll(loc);
                if (item != null) r.Drops.Add(item);
            };

            const float dt = 0.1f;
            var t = 0f;
            var opening = -1f;
            while (true)
            {
                sim.Advance(dt);
                hero.Regenerate(dt);
                t += dt;

                if (hero.IsDown) { r.Died = true; r.DiedOpening = opening >= 0f; break; }

                if (sim.EncountersCleared >= loc.Population && r.CompleteTime < 0f)
                {
                    r.CompleteTime = t;
                    r.Completed = true;
                    if (!keepGoing) break;
                }

                if (policy == Policy.Played && opening < 0f && hero.HealthFraction <= Knobs.RecallAt)
                {
                    if (hero.Potions > 0)
                    {
                        hero.Potions--;
                        r.PotionsDrunk++;
                        hero.Health = Math.Min(hero.MaxHealth, hero.Health + hero.MaxHealth * Knobs.PotionHealFraction);
                    }
                    else opening = t;
                }

                if (opening >= 0f && t - opening >= Knobs.PortalOpening)
                {
                    // The Run holds, frozen; the Healer is free; the portal takes him back into the same fight.
                    hero.FullHeal();
                    r.Trips++;
                    opening = -1f;
                }

                if (t >= Knobs.SendCap) break;
            }

            r.Time = t + r.Trips * Knobs.TownTrip;
            r.Encounters = sim.EncountersCleared;
            return r;
        }
    }

    // ─── the sweep ──────────────────────────────────────────────────────────────────────────────

    [TestFixture, Explicit("PROTOTYPE #296 - run by hand: dev/run-tests.sh EditMode BalanceSweepPrototype")]
    public sealed class BalanceSweepPrototype
    {
        private StringBuilder _out = new();

        [Test, Timeout(3_600_000)]
        public void Sweep0Census() => Section("census", Census);

        [Test, Timeout(3_600_000)]
        public void Sweep1Authored() => Section("authored", AuthoredTable);

        [Test, Timeout(3_600_000)]
        public void Sweep2Curve() => Section("curve", CurveTable);

        [Test, Timeout(3_600_000)]
        public void Sweep3Campaign() => Section("campaign", Campaign);

        private void Section(string name, Action<LootVariant> body)
        {
            _out = new StringBuilder();
            _out.AppendLine($"# Balance sweep (#296) - {name}");
            _out.AppendLine();
            _out.AppendLine($"Generated {DateTime.Now:yyyy-MM-dd HH:mm}. Real `EncounterSimulation`, `EncounterTuning.Standard()`, Engagement {Knobs.Engagement}. " +
                            $"Careless = no Recall, no potion. Played = at {Knobs.RecallAt:P0} Health drink a potion if carried (+{Knobs.PotionHealFraction:P0}), else Recall: the portal opens over {Knobs.PortalOpening} s, a trip costs {Knobs.TownTrip} s.");
            _out.AppendLine();
            HeroBlock();
            foreach (var variant in LootVariant.All)
            {
                _out.AppendLine($"## Loot `{variant.Name}`");
                _out.AppendLine();
                _out.AppendLine(variant.Description);
                _out.AppendLine();
                body(variant);
            }
            var dir = Path.Combine(UnityEngine.Application.dataPath, "..", "dev", "prototypes", "2026-10-17-balance-sweep");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, $"out-{name}.md"), _out.ToString());
        }

        private void HeroBlock()
        {
            _out.AppendLine("Hero base: " + string.Join(", ", Knobs.Base.Select(kv => $"{kv.Key} {kv.Value}")) + ".");
            _out.AppendLine("Per level: " + string.Join(", ", Knobs.Growth.Select(kv => $"{kv.Key} +{kv.Value}")) + ". XP bar: " +
                            string.Join(", ", Enumerable.Range(1, 8).Select(l => $"L{l} {SweepHero.BarMax(l):0}")) + ".");
            _out.AppendLine($"Populations: Thornwood {Knobs.ThornwoodPopulation}, Ashfall {Knobs.AshfallPopulation}, synthetic {Knobs.CurvePopulation}. Mitigation: {(Knobs.MitigationK > 0 ? $"R / (R + {Knobs.MitigationK})" : "linear percent, clamped")}.");
            _out.AppendLine();
        }

        // ─── what the loot is ───────────────────────────────────────────────────────────────────

        private void Census(LootVariant variant)
        {
            const int kills = 20000;
            foreach (var loc in new[] { Loc.Thornwood(), Loc.Ashfall() })
            {
                var looter = new Looter(variant, 42);
                var drops = new List<ItemInstance>();
                var fails = 0;
                for (var i = 0; i < kills; i++)
                {
                    var item = looter.Roll(loc);
                    if (item == null) fails++; else drops.Add(item);
                }
                var defs = drops.Select(d => (item: d, def: Looter.Definition(d))).ToList();
                var equipment = defs.Where(x => x.def.Category == ItemCategory.Equipment).ToList();
                var potions = defs.Count(x => x.def.ConsumableType == ConsumableType.Potion);
                var perSend = loc.KillsPerSend / (float)kills;

                _out.AppendLine($"### {loc.Name} (item level {loc.S}), {kills} kills, {loc.KillsPerSend} kills a Send");
                _out.AppendLine();
                _out.AppendLine($"- Drop per kill: {(kills - fails) / (float)kills:P1} (Fail Bucket {fails / (float)kills:P1}); a Send drops ~{(kills - fails) * perSend:0.0} items: " +
                                $"{equipment.Count * perSend:0.0} equipment, {potions * perSend:0.0} potions.");
                _out.AppendLine("- Category: " + string.Join(", ", defs.GroupBy(x => x.def.Category).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count() / (float)drops.Count:P0}")));
                _out.AppendLine("- Consumables a Send: " + string.Join(", ", defs.Where(x => x.def.Category == ItemCategory.Consumable).GroupBy(x => x.def.ConsumableType).Select(g => $"{g.Key} {g.Count() * perSend:0.0}")));
                _out.AppendLine("- Equipment rarity: " + string.Join(", ", equipment.GroupBy(x => x.item.Rarity).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count() / (float)equipment.Count:P1}")) +
                                $"; on a Unique **definition**: {equipment.Count(x => x.def.IsUnique) / (float)Math.Max(1, equipment.Count):P0} (1 in {kills / Math.Max(1f, equipment.Count(x => x.def.IsUnique)):0} kills).");
                _out.AppendLine();
                _out.AppendLine("| equipment type | share | affixes it can roll (stat: range, type) | mean rolled value per drop |");
                _out.AppendLine("|---|---|---|---|");
                foreach (var g in equipment.GroupBy(x => x.def.EquipmentType).OrderBy(g => g.Key))
                {
                    var pool = g.First().def.AffixPool;
                    var poolText = string.Join("; ", pool.Select(a => $"{a.Stat}: {a.RangeMin}-{a.RangeMax} {(a.ModifierType == StatModifierType.FlatAdd ? "flat" : a.ModifierType == StatModifierType.PercentAdd ? "%" : a.ModifierType.ToString())}"));
                    var means = g.SelectMany(x => x.item.Affixes.Select(a => (a.Stat, a.Modifier.Type, v: a.Modifier.Value * (a.Modifier.Type == StatModifierType.FlatAdd ? looter.ScaleOf(x.item) : 1f))))
                        .GroupBy(a => (a.Stat, a.Type)).Select(s => $"{s.Key.Stat} {s.Sum(a => a.v) / g.Count():0.#}{(s.Key.Type == StatModifierType.FlatAdd ? "" : "%")}");
                    _out.AppendLine($"| {g.Key} | {g.Count() / (float)equipment.Count:P0} | {poolText} | {string.Join(", ", means)} |");
                }
                _out.AppendLine();
            }
        }

        // ─── farmed gear ────────────────────────────────────────────────────────────────────────

        /// <summary>The gear a hero holds after farming <paramref name="sends"/> Sends' worth of kills at <paramref name="loc"/>, equipping every upgrade.</summary>
        private static Gear Farmed(LootVariant variant, Loc loc, int sends, int seed, Build build)
        {
            if (sends <= 0) return new Gear();
            var looter = new Looter(variant, seed);
            var hero = new SweepHero(build, 1);
            for (var i = 0; i < sends * loc.KillsPerSend; i++)
            {
                var item = looter.Roll(loc);
                if (item != null) hero.Offer(item, looter.ScaleOf(item));
            }
            return hero.Gear;
        }

        private static string GearText(SweepHero h) =>
            $"HP {h.MaxHealth:0}, Arm {h.Stat(StatName.Armor):0} ({Knobs.Mitigation(h.Stat(StatName.Armor)):P0}), MR {h.Stat(StatName.MagicResist):0} ({Knobs.Mitigation(h.Stat(StatName.MagicResist)):P0}), Phys {h.PhysicalDamage:0}, Mag {h.MagicalDamage:0}, AS {h.AttackSpeed:0.00}, regen {h.Stat(StatName.HealthRegeneration):0.0}, {h.Gear.Equipped.Count} worn";

        private struct Cell
        {
            public float CompletePct, MedianTtd, Encounters, SecPerKill, TripsPerSend, PlayedDeathPct;
            public string Gear;
        }

        private static Cell Measure(Loc loc, Build build, int level, Func<int, Gear> gearForSeed)
        {
            var careless = new List<SendResult>();
            var played = new List<SendResult>();
            string gearText = null;
            for (var seed = 1; seed <= Knobs.Seeds; seed++)
            {
                var gear = gearForSeed(seed);
                var c = new SweepHero(build, level, gear);
                gearText ??= GearText(c);
                careless.Add(Sender.Run(c, loc, Policy.Careless, seed, keepGoing: true));
                played.Add(Sender.Run(new SweepHero(build, level, gear), loc, Policy.Played, seed));
            }
            var ttd = careless.Select(r => r.Died ? r.Time : Knobs.SendCap).OrderBy(x => x).ToList();
            return new Cell
            {
                CompletePct = careless.Count(r => r.Completed) * 100f / careless.Count,
                MedianTtd = ttd[ttd.Count / 2],
                Encounters = (float)careless.Average(r => r.Encounters),
                SecPerKill = careless.Sum(r => r.Time) / Math.Max(1, careless.Sum(r => r.Kills)),
                TripsPerSend = (float)played.Average(r => r.Trips),
                PlayedDeathPct = played.Count(r => r.Died) * 100f / played.Count,
                Gear = gearText,
            };
        }

        private static string Fmt(Cell c, int population) =>
            $"{c.CompletePct:0}% | {(c.MedianTtd >= Knobs.SendCap ? "never" : c.MedianTtd.ToString("0") + " s")} | {c.Encounters:0.0}/{population} | {c.SecPerKill:0.0} | {c.TripsPerSend:0.0} | {c.PlayedDeathPct:0}% | {c.Gear}";

        private const string CellHeader = "careless Completes | careless time to die | Encounters before death | s / kill | played trips / Send | played deaths | hero (seed 1)";
        private const string CellRule = "---|---|---|---|---|---|---";

        private void AuthoredTable(LootVariant variant)
        {
            _out.AppendLine($"| Location | build | level | gear | {CellHeader} |");
            _out.AppendLine($"|---|---|---|---|{CellRule}|");
            var thorn = Loc.Thornwood();
            var ash = Loc.Ashfall();
            var gears = new (string, Func<int, Build, Gear>)[]
            {
                ("none", (_, _) => new Gear()),
                ("1 Thornwood Send", (s, b) => Farmed(variant, thorn, 1, s, b)),
                ("5 Thornwood Sends", (s, b) => Farmed(variant, thorn, 5, s, b)),
                ("20 Thornwood Sends", (s, b) => Farmed(variant, thorn, 20, s, b)),
                ("5 Ashfall Sends", (s, b) => Farmed(variant, ash, 5, s, b)),
            };
            foreach (var loc in new[] { thorn, ash })
                foreach (var build in new[] { Build.Physical, Build.Magical })
                    foreach (var level in new[] { 1, 3, 6 })
                        foreach (var (name, gear) in gears)
                            _out.AppendLine($"| {loc.Name} | {build} | {level} | {name} | {Fmt(Measure(loc, build, level, s => gear(s, build)), loc.Population)} |");
            _out.AppendLine();
        }

        private void CurveTable(LootVariant variant)
        {
            _out.AppendLine("Synthetic Locations, hero on level (L = S), physical build, gear farmed two source levels below or at this one.");
            _out.AppendLine();
            _out.AppendLine($"| Location | gear | {CellHeader} |");
            _out.AppendLine($"|---|---|{CellRule}|");
            for (var s = 1; s <= 10; s++)
                foreach (var loc in new[] { Loc.BrutePacked(s, Knobs.CurvePopulation), Loc.SkirmisherPacked(s, Knobs.CurvePopulation) })
                {
                    var below = Loc.BrutePacked(Math.Max(1, s - 2), Knobs.CurvePopulation);
                    foreach (var (name, gear) in new (string, Func<int, Gear>)[]
                             {
                                 ("none", _ => new Gear()),
                                 ($"5 Sends at S{below.S}", seed => Farmed(variant, below, 5, seed, Build.Physical)),
                                 ($"5 Sends at S{s}", seed => Farmed(variant, loc, 5, seed, Build.Physical)),
                             })
                        _out.AppendLine($"| {loc.Name} | {name} | {Fmt(Measure(loc, Build.Physical, s, gear), loc.Population)} |");
                }
            _out.AppendLine();
        }

        // A fresh level-1 hero with nothing equipped farms Thornwood until its first Complete, then Ashfall until its
        // first Complete. Every kill rolls real loot (Fail Bucket included); upgrades are equipped, potions carried up to
        // the belt; a Death loses 25% of the bar and the next Send starts fresh.
        private void Campaign(LootVariant variant)
        {
            _out.AppendLine("| policy | build | Location | Sends to Complete (median / max) | Deaths (median) | trips (median) | potions drunk / dropped | level at Complete | items dropped / equipped | time (median, min) | hero at Complete (seed 1) |");
            _out.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var policy in new[] { Policy.Careless, Policy.Played })
                foreach (var build in new[] { Build.Physical, Build.Magical })
                {
                    var rows = new Dictionary<string, List<(int sends, int deaths, int trips, int drunk, int potions, int level, int items, int equipped, float time, string gear)>>
                    {
                        ["Thornwood"] = new(), ["Ashfall"] = new(),
                    };
                    for (var seed = 1; seed <= Knobs.CampaignSeeds; seed++)
                    {
                        var hero = new SweepHero(build, 1);
                        var looter = new Looter(variant, seed * 31);
                        var sendSeed = seed * 1000;
                        foreach (var loc in new[] { Loc.Thornwood(), Loc.Ashfall() })
                        {
                            int sends = 0, deaths = 0, trips = 0, drunk = 0, potions = 0, items = 0, equipped = 0;
                            var time = 0f;
                            while (sends < Knobs.CampaignMaxSends)
                            {
                                sends++;
                                hero.FullHeal();
                                var r = Sender.Run(hero, loc, policy, ++sendSeed, looter: looter);
                                foreach (var item in r.Drops)
                                {
                                    items++;
                                    if (Looter.Definition(item).ConsumableType == ConsumableType.Potion)
                                    {
                                        potions++;
                                        hero.Potions = Math.Min(Knobs.PotionCarry, hero.Potions + 1);
                                    }
                                    else if (hero.Offer(item, looter.ScaleOf(item))) equipped++;
                                }
                                trips += r.Trips;
                                drunk += r.PotionsDrunk;
                                time += r.Time;
                                if (r.Died) { deaths++; hero.LoseXpOnDeath(); }
                                if (r.Completed) break;
                            }
                            rows[loc.Name].Add((sends, deaths, trips, drunk, potions, hero.Level, items, equipped, time, seed == 1 ? GearText(hero) : null));
                        }
                    }
                    foreach (var (name, list) in rows)
                    {
                        static float Med(IEnumerable<float> xs) { var l = xs.OrderBy(x => x).ToList(); return l[l.Count / 2]; }
                        _out.AppendLine($"| {policy} | {build} | {name} | {Med(list.Select(x => (float)x.sends)):0} / {list.Max(x => x.sends)} | {Med(list.Select(x => (float)x.deaths)):0} | " +
                                        $"{Med(list.Select(x => (float)x.trips)):0} | {Med(list.Select(x => (float)x.drunk)):0} / {Med(list.Select(x => (float)x.potions)):0} | {Med(list.Select(x => (float)x.level)):0} | " +
                                        $"{Med(list.Select(x => (float)x.items)):0} / {Med(list.Select(x => (float)x.equipped)):0} | {Med(list.Select(x => x.time)) / 60f:0.0} | {list[0].gear} |");
                    }
                }
            _out.AppendLine();
        }
    }
}

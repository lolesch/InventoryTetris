// PROTOTYPE (#296, map #293) - throwaway balance sweep. Lives on branch prototype/296-balance-sweep only; never merge.
// Runs the real EncounterSimulation headless with a SweepHero that carries the Run-risk levers of
// dev/specs/2026-10-17-run-risk-levers-design.md (no level-up heal, base Health regen 0, a flat growth table),
// a Location Population (ADR-0023), a Recall whose portal opens over 0.5 s of sim time, and a gear + loot model
// with a Fail Bucket. Writes dev/prototypes/2026-10-17-balance-sweep/sweep-output.md.
//
//   dev/run-tests.sh EditMode BalanceSweepPrototype
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation.BalanceSweep
{
    // ─── knobs ──────────────────────────────────────────────────────────────────────────────────

    internal static class Knobs
    {
        public static string Preset = "proposed";
        public static readonly string[] Presets = { "middle" };
        public static int ThornwoodPopulation = 5, AshfallPopulation = 6, CurvePopulation = 5;

        // The live DefaultHero.asset, with Health Regeneration 0 (run-risk spec).
        public static readonly HeroStats LiveBase = new()
        {
            Health = 640, Armor = 26, MagicResist = 30, Physical = 59, Magical = 0, AttackSpeed = 0.658f,
            HealthRegen = 0, Resource = 280, ResourceRegen = 7, MovementSpeed = 325,
        };

        public static readonly HeroStats ProposedBase = new()
        {
            Health = 200, Armor = 0, MagicResist = 0, Physical = 14, Magical = 0, AttackSpeed = 0.9f,
            HealthRegen = 0, Resource = 100, ResourceRegen = 5, MovementSpeed = 325,
        };

        public static readonly HeroStats ProposedGrowth = new() { Health = 12, Physical = 1.2f, Magical = 1.2f, Resource = 5 };

        public static readonly HeroStats MiddleBase = new()
        {
            Health = 400, Armor = 12, MagicResist = 12, Physical = 32, Magical = 14, AttackSpeed = 0.75f,
            HealthRegen = 0, Resource = 200, ResourceRegen = 6, MovementSpeed = 325,
        };

        public static readonly HeroStats MiddleGrowth = new() { Health = 20, Physical = 2.5f, Magical = 2.5f, Resource = 8 };

        public static HeroStats Base => Preset switch { "live" => LiveBase, "middle" => MiddleBase, _ => ProposedBase };
        public static HeroStats Growth => Preset switch { "live" => default, "middle" => MiddleGrowth, _ => ProposedGrowth };

        // Gear: an item's power is itemLevel x rarity multiplier x roll; a slot turns power into stats.
        public const int WeaponSlot = 0, ArmorSlots = 6, JewelrySlots = 4, Slots = 1 + ArmorSlots + JewelrySlots;
        public static float WeaponDamagePerPower = 4.0f;
        public static float ArmorHealthPerPower = 9f;
        public static float ArmorResistPerPower = 0.8f; // Armor and Magic Resist each, percent points
        public static float JewelryRegenPerPower = 0.06f;
        public static float JewelryDamagePerPower = 0.8f;
        public static float JewelryAttackSpeedPerPower = 0.0f;

        public static float RarityMultiplier(ItemRarity r) => r switch
        {
            ItemRarity.Common => 0.6f,
            ItemRarity.Magic => 0.8f,
            ItemRarity.Rare => 1.0f,
            ItemRarity.Unique => 1.3f,
            _ => 0f,
        };

        public const float TypicalQuality = 0.8f; // a kit of average Magic items
        public const float GoodQuality = 1.1f;    // a kit of high-rolled Rares

        // Loot: the Fail Bucket is a weight beside the authored rarity quantities (160/80/40/20).
        public static float FailWeight = 900f;
        public static readonly (ItemRarity rarity, float weight)[] RarityWeights =
        {
            (ItemRarity.Common, 160f), (ItemRarity.Magic, 80f), (ItemRarity.Rare, 40f), (ItemRarity.Unique, 2f),
        };

        public const float RecallAt = 0.35f;     // a played hero opens a portal at this Health fraction
        public const float PortalOpening = 0.5f; // sim seconds the portal takes to open (town-portal spec)
        public const float TownTrip = 15f;       // sim seconds a trip to the Healer and back costs
        public const float SendCap = 600f;       // a careless Send that neither dies nor Completes by then is stopped
        public const float XpLossFraction = 0.25f;
        public const int Engagement = 3;
        public const int CastCost = 16;
        public const int Seeds = 8;
        public const int CampaignSeeds = 20;
    }

    // ─── the hero ───────────────────────────────────────────────────────────────────────────────

    internal struct HeroStats
    {
        public float Health, Armor, MagicResist, Physical, Magical, AttackSpeed, HealthRegen, Resource, ResourceRegen, MovementSpeed;

        public static HeroStats operator +(HeroStats a, HeroStats b) => new()
        {
            Health = a.Health + b.Health, Armor = a.Armor + b.Armor, MagicResist = a.MagicResist + b.MagicResist,
            Physical = a.Physical + b.Physical, Magical = a.Magical + b.Magical, AttackSpeed = a.AttackSpeed + b.AttackSpeed,
            HealthRegen = a.HealthRegen + b.HealthRegen, Resource = a.Resource + b.Resource,
            ResourceRegen = a.ResourceRegen + b.ResourceRegen, MovementSpeed = a.MovementSpeed + b.MovementSpeed,
        };

        public HeroStats Scaled(float k) => new()
        {
            Health = Health * k, Armor = Armor * k, MagicResist = MagicResist * k, Physical = Physical * k, Magical = Magical * k,
            AttackSpeed = AttackSpeed * k, HealthRegen = HealthRegen * k, Resource = Resource * k,
            ResourceRegen = ResourceRegen * k, MovementSpeed = MovementSpeed * k,
        };
    }

    internal enum Build { Physical, Magical }

    internal sealed class Kit
    {
        public readonly float[] Power = new float[Knobs.Slots];

        public static Kit None => new();

        public static Kit At(int itemLevel, float quality)
        {
            var kit = new Kit();
            for (var i = 0; i < Knobs.Slots; i++) kit.Power[i] = itemLevel * quality;
            return kit;
        }

        public Kit Clone()
        {
            var k = new Kit();
            Array.Copy(Power, k.Power, Power.Length);
            return k;
        }

        public float Average => Power.Average();

        public HeroStats Stats(Build build)
        {
            var s = new HeroStats();
            var weaponDamage = Power[Knobs.WeaponSlot] * Knobs.WeaponDamagePerPower;
            var jewelDamage = 0f;
            for (var i = 1; i <= Knobs.ArmorSlots; i++)
            {
                s.Health += Power[i] * Knobs.ArmorHealthPerPower;
                s.Armor += Power[i] * Knobs.ArmorResistPerPower;
                s.MagicResist += Power[i] * Knobs.ArmorResistPerPower;
            }
            for (var i = 1 + Knobs.ArmorSlots; i < Knobs.Slots; i++)
            {
                s.HealthRegen += Power[i] * Knobs.JewelryRegenPerPower;
                jewelDamage += Power[i] * Knobs.JewelryDamagePerPower;
                s.AttackSpeed += Power[i] * Knobs.JewelryAttackSpeedPerPower;
            }
            if (build == Build.Physical) s.Physical += weaponDamage + jewelDamage;
            else s.Magical += weaponDamage + jewelDamage;
            return s;
        }
    }

    internal sealed class SweepHero : IHeroCombatant
    {
        private HeroStats _total;
        private Kit _kit;

        public SweepHero(Build build, int level, Kit kit)
        {
            Build = build;
            Level = level;
            _kit = kit ?? Kit.None;
            Refresh();
            FullHeal();
        }

        public Build Build { get; }
        public HeroStats Total => _total;
        public Kit Kit
        {
            get => _kit;
            set { _kit = value; Refresh(); }
        }

        public int Levels { get; private set; } // level-ups this hero took since the counter was read
        public float XpIntoLevel { get; private set; }

        private void Refresh()
        {
            _total = Knobs.Base + Knobs.Growth.Scaled(Level - 1) + _kit.Stats(Build);
            if (Build == Build.Magical && _total.Physical > 0) _total.Physical *= 0.5f; // a caster still swings, weakly
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
                Levels++;
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
        public float MaxHealth => _total.Health;
        public float HealthFraction => MaxHealth <= 0f ? 0f : Math.Clamp(Health / MaxHealth, 0f, 1f);
        public bool IsDown => Health <= 0f;

        public float Resource { get; set; }
        public float MaxResource => _total.Resource;
        public float ResourceFraction => MaxResource <= 0f ? 0f : Math.Clamp(Resource / MaxResource, 0f, 1f);
        public float PhysicalDamage => _total.Physical;
        public float AttackSpeed => _total.AttackSpeed;
        public float MagicalDamage => _total.Magical;
        public float CastCost => Knobs.CastCost;
        public float MovementSpeed => _total.MovementSpeed;
        public float? WeaponStrikeRange => null;
        public int Level { get; private set; }
        public float MagicFind => 0f;
        public float IncreasedItemQuantity => 0f;

        public float DamageTaken { get; set; }

        public float ReceivePhysical(float raw) => Take(raw, _total.Armor);
        public float ReceiveMagical(float raw) => Take(raw, _total.MagicResist);

        // Hero.CalculateReceivingDamage: linear percent, clamped 0..100.
        private float Take(float raw, float resist)
        {
            if (raw <= 0f || Health <= 0f) return 0f;
            var mitigated = raw * (1f - Math.Clamp(resist, 0f, 100f) * 0.01f);
            var lost = Math.Min(Health, mitigated);
            Health -= lost;
            DamageTaken += lost;
            return lost;
        }

        public void SpendResource(float amount) => Resource = Math.Max(0f, Resource - amount);

        public void Regenerate(float dt)
        {
            if (Health > 0f) Health = Math.Min(MaxHealth, Health + _total.HealthRegen * dt);
            Resource = Math.Min(MaxResource, Resource + _total.ResourceRegen * dt);
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

        public static Loc Thornwood(int population) { var l = BrutePacked(2, population); l.Name = "Thornwood"; return l; }
        public static Loc Ashfall(int population) { var l = SkirmisherPacked(5, population); l.Name = "Ashfall"; return l; }
    }

    // ─── one Send ───────────────────────────────────────────────────────────────────────────────

    internal enum Policy { Careless, Played }

    internal sealed class SendResult
    {
        public bool Completed, Died, DiedOpening, Capped;
        public float Time, CompleteTime = -1f;
        public int Encounters, Kills, BruteKills, SkirmisherKills, Trips, LevelsGained, Xp;
        public float DamageTaken;
        public readonly List<ItemRarity> Drops = new();
    }

    internal static class Sender
    {
        // keepGoing: a careless Send keeps fighting past its Population, so one run measures both the Complete and the
        // time to die.
        public static SendResult Run(SweepHero hero, Loc loc, Policy policy, int seed, bool keepGoing = false, Random loot = null)
        {
            var r = new SendResult();
            var behaviour = new HeroBehaviour { Engagement = Knobs.Engagement };
            var streams = new RollStreams(new SeededRollSource(seed), new SeededRollSource(seed * 7 + 1), new SeededRollSource(seed * 13 + 2));
            var sim = new EncounterSimulation(hero, loc.Profile(), streams, behaviour, EncounterTuning.Standard());
            var levelsBefore = hero.Level;
            hero.DamageTaken = 0f;

            sim.XpGained += xp => { hero.GainXp(xp); r.Xp += xp; };
            sim.EnemyDefeated += e =>
            {
                r.Kills++;
                if (e.Archetype == EnemyArchetype.Brute) r.BruteKills++; else r.SkirmisherKills++;
                if (loot != null) r.Drops.Add(Loot.Roll(loot));
            };

            const float dt = 0.1f;
            var t = 0f;
            var opening = -1f;
            while (true)
            {
                sim.Advance(dt);
                hero.Regenerate(dt);
                t += dt;

                if (hero.IsDown)
                {
                    r.Died = true;
                    r.DiedOpening = opening >= 0f;
                    break;
                }

                if (sim.EncountersCleared >= loc.Population && r.CompleteTime < 0f)
                {
                    r.CompleteTime = t;
                    r.Completed = true;
                    if (!keepGoing) break;
                }

                if (policy == Policy.Played)
                {
                    if (opening < 0f && hero.HealthFraction <= Knobs.RecallAt) opening = t;
                    if (opening >= 0f && t - opening >= Knobs.PortalOpening)
                    {
                        // The Run holds, frozen; the Healer is free; the portal takes him back into the same fight.
                        hero.FullHeal();
                        r.Trips++;
                        opening = -1f;
                    }
                }

                if (t >= Knobs.SendCap) { r.Capped = true; break; }
            }

            r.Time = t + r.Trips * Knobs.TownTrip;
            r.Encounters = sim.EncountersCleared;
            r.LevelsGained = hero.Level - levelsBefore;
            r.DamageTaken = hero.DamageTaken;
            return r;
        }
    }

    internal static class Loot
    {
        public static ItemRarity Roll(Random rng)
        {
            var total = Knobs.FailWeight + Knobs.RarityWeights.Sum(w => w.weight);
            var x = rng.NextDouble() * total;
            if (x < Knobs.FailWeight) return ItemRarity.NoDrop;
            x -= Knobs.FailWeight;
            foreach (var (rarity, weight) in Knobs.RarityWeights)
            {
                if (x < weight) return rarity;
                x -= weight;
            }
            return Knobs.RarityWeights[^1].rarity;
        }

        // Equip each drop into a random slot if it beats what is there. Item level = the Location's source level.
        public static void Equip(SweepHero hero, IEnumerable<ItemRarity> drops, int itemLevel, Random rng)
        {
            var kit = hero.Kit.Clone();
            foreach (var rarity in drops)
            {
                if (rarity == ItemRarity.NoDrop) continue;
                var slot = rng.Next(Knobs.Slots);
                var power = itemLevel * Knobs.RarityMultiplier(rarity) * (0.8f + 0.4f * (float)rng.NextDouble());
                if (power > kit.Power[slot]) kit.Power[slot] = power;
            }
            hero.Kit = kit;
        }
    }

    // ─── the sweep ──────────────────────────────────────────────────────────────────────────────

    [TestFixture, Explicit("PROTOTYPE #296 - run by hand: dev/run-tests.sh EditMode BalanceSweepPrototype")]
    public sealed class BalanceSweepPrototype
    {
        private StringBuilder _out = new();

        [Test, Timeout(3_600_000)]
        public void Sweep1Authored() => Section("authored", () => { HeroTable(); EnemyTable(); AuthoredTable(); });

        [Test, Timeout(3_600_000)]
        public void Sweep2Curve() => Section("curve", CurveTable);

        [Test, Timeout(3_600_000)]
        public void Sweep3Campaign() => Section("campaign", Campaign);

        private void Section(string name, Action body)
        {
            foreach (var preset in Knobs.Presets)
            {
                Knobs.Preset = preset;
                _out = new StringBuilder();
                _out.AppendLine($"# Balance sweep (#296) - {name}, preset `{Knobs.Preset}`");
                _out.AppendLine();
                _out.AppendLine($"Generated {DateTime.Now:yyyy-MM-dd HH:mm}. Real `EncounterSimulation`, `EncounterTuning.Standard()`, Engagement {Knobs.Engagement}, " +
                                $"{Knobs.Seeds} seeds a cell. Careless = no Recall, no potion. Played = Recall at {Knobs.RecallAt:P0} Health, portal opens over {Knobs.PortalOpening} s, a trip costs {Knobs.TownTrip} s. TTD capped at {Knobs.SendCap} s.");
                _out.AppendLine();
                body();
                var dir = Path.Combine(UnityEngine.Application.dataPath, "..", "dev", "prototypes", "2026-10-17-balance-sweep");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, $"out-{name}-{Knobs.Preset}.md"), _out.ToString());
            }
        }

        private void HeroTable()
        {
            var b = Knobs.Base;
            var g = Knobs.Growth;
            _out.AppendLine("## Hero");
            _out.AppendLine();
            _out.AppendLine("| | Health | Armor % | MR % | Phys | Mag | AS | HP regen | Resource | Res regen |");
            _out.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
            _out.AppendLine($"| base | {b.Health} | {b.Armor} | {b.MagicResist} | {b.Physical} | {b.Magical} | {b.AttackSpeed} | {b.HealthRegen} | {b.Resource} | {b.ResourceRegen} |");
            _out.AppendLine($"| per level | {g.Health} | {g.Armor} | {g.MagicResist} | {g.Physical} | {g.Magical} | {g.AttackSpeed} | {g.HealthRegen} | {g.Resource} | {g.ResourceRegen} |");
            foreach (var (label, kit) in new[] { ("typical kit ilvl 2", Kit.At(2, Knobs.TypicalQuality)), ("good kit ilvl 2", Kit.At(2, Knobs.GoodQuality)), ("typical kit ilvl 5", Kit.At(5, Knobs.TypicalQuality)), ("good kit ilvl 5", Kit.At(5, Knobs.GoodQuality)), ("good kit ilvl 10", Kit.At(10, Knobs.GoodQuality)) })
            {
                var k = kit.Stats(Build.Physical);
                _out.AppendLine($"| {label} (phys) | +{k.Health:0} | +{k.Armor:0.#} | +{k.MagicResist:0.#} | +{k.Physical:0.#} | +{k.Magical:0.#} | +{k.AttackSpeed:0.##} | +{k.HealthRegen:0.##} | | |");
            }
            _out.AppendLine();
            _out.AppendLine("XP bar: " + string.Join(", ", Enumerable.Range(1, 10).Select(l => $"L{l} {SweepHero.BarMax(l):0}")));
            _out.AppendLine();
        }

        private void EnemyTable()
        {
            _out.AppendLine("## Enemies per source level (archetype curves, live)");
            _out.AppendLine();
            _out.AppendLine("| S = ilvl | Brute HP | Brute hit | Brute raw DPS | Brute XP | Skirm HP | Skirm hit | Skirm raw DPS | Skirm XP | XP / Encounter (B-packed) | XP / Encounter (S-packed) |");
            _out.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            for (var s = 1; s <= 10; s++)
            {
                var br = EnemyArchetypes.Brute;
                var sk = EnemyArchetypes.Skirmisher;
                var xpB = 9 * br.Xp.At(s) + 5 * sk.Xp.At(s);
                var xpS = 4 * br.Xp.At(s) + 13 * sk.Xp.At(s);
                _out.AppendLine($"| {s} | {br.Health.At(s):0} | {br.Damage.At(s):0.0} | {br.Damage.At(s) * br.AttackSpeed:0.0} | {br.Xp.At(s):0} | {sk.Health.At(s):0} | {sk.Damage.At(s):0.0} | {sk.Damage.At(s) * sk.AttackSpeed:0.0} | {sk.Xp.At(s):0} | {xpB:0} | {xpS:0} |");
            }
            _out.AppendLine();
        }

        private struct Cell
        {
            public float CompletePct, DiedPct, MedianTtd, MedianComplete, SecPerKill, TripsPerSend, DiedOpeningPct, Encounters;
        }

        private static Cell Measure(Loc loc, Build build, int level, Kit kit)
        {
            var careless = new List<SendResult>();
            var played = new List<SendResult>();
            for (var seed = 1; seed <= Knobs.Seeds; seed++)
            {
                careless.Add(Sender.Run(new SweepHero(build, level, kit), loc, Policy.Careless, seed, keepGoing: true));
                played.Add(Sender.Run(new SweepHero(build, level, kit), loc, Policy.Played, seed));
            }
            var ttd = careless.Select(r => r.Died ? r.Time : Knobs.SendCap).OrderBy(x => x).ToList();
            var complete = careless.Where(r => r.Completed).Select(r => r.CompleteTime).OrderBy(x => x).ToList();
            var kills = careless.Sum(r => r.Kills);
            return new Cell
            {
                CompletePct = careless.Count(r => r.Completed && (!r.Died || r.CompleteTime < r.Time)) * 100f / careless.Count,
                DiedPct = careless.Count(r => r.Died) * 100f / careless.Count,
                MedianTtd = ttd[ttd.Count / 2],
                MedianComplete = complete.Count > 0 ? complete[complete.Count / 2] : -1f,
                SecPerKill = kills > 0 ? careless.Sum(r => r.Time) / kills : -1f,
                TripsPerSend = (float)played.Average(r => r.Trips),
                DiedOpeningPct = played.Count(r => r.Died) * 100f / played.Count,
                Encounters = (float)careless.Average(r => r.Encounters),
            };
        }

        private static string Fmt(Cell c, int population) =>
            $"{c.CompletePct:0}% | {(c.MedianTtd >= Knobs.SendCap ? "never" : c.MedianTtd.ToString("0") + " s")} | {c.Encounters:0.0}/{population} | " +
            $"{(c.MedianComplete < 0 ? "-" : c.MedianComplete.ToString("0") + " s")} | {c.SecPerKill:0.0} | {c.TripsPerSend:0.0} | {c.DiedOpeningPct:0}%";

        private const string CellHeader = "careless Completes | careless TTD (median) | Encounters before death | time to Complete | s / kill | played trips / Send | played deaths";
        private const string CellRule = "---|---|---|---|---|---|---";

        private void AuthoredTable()
        {
            var population = Knobs.ThornwoodPopulation;
            _out.AppendLine($"## Thornwood (Population {Knobs.ThornwoodPopulation}) and Ashfall (Population {Knobs.AshfallPopulation})");
            _out.AppendLine();
            _out.AppendLine($"| Location | build | level | kit | {CellHeader} |");
            _out.AppendLine($"|---|---|---|---|{CellRule}|");
            var kits = new (string, Func<Kit>)[]
            {
                ("none", () => Kit.None), ("typical ilvl 2", () => Kit.At(2, Knobs.TypicalQuality)), ("good ilvl 2", () => Kit.At(2, Knobs.GoodQuality)),
                ("typical ilvl 5", () => Kit.At(5, Knobs.TypicalQuality)),
            };
            foreach (var loc in new[] { Loc.Thornwood(Knobs.ThornwoodPopulation), Loc.Ashfall(Knobs.AshfallPopulation) })
                foreach (var build in new[] { Build.Physical, Build.Magical })
                    foreach (var level in new[] { 1, 3, 5 })
                        foreach (var (name, kit) in kits)
                            _out.AppendLine($"| {loc.Name} | {build} | {level} | {name} | {Fmt(Measure(loc, build, level, kit()), loc.Population)} |");
            _out.AppendLine();
        }

        private void CurveTable()
        {
            var population = Knobs.CurvePopulation;
            _out.AppendLine($"## The curve: synthetic Locations S1-10, hero on level (L = S), physical build, Population {population}");
            _out.AppendLine();
            _out.AppendLine($"| Location | kit | {CellHeader} |");
            _out.AppendLine($"|---|---|{CellRule}|");
            for (var s = 1; s <= 10; s++)
                foreach (var loc in new[] { Loc.BrutePacked(s, population), Loc.SkirmisherPacked(s, population) })
                    foreach (var (name, kit) in new (string, Kit)[]
                             {
                                 ("none", Kit.None), ($"typical ilvl {Math.Max(1, s - 2)}", Kit.At(Math.Max(1, s - 2), Knobs.TypicalQuality)),
                                 ($"typical ilvl {s}", Kit.At(s, Knobs.TypicalQuality)), ($"good ilvl {s}", Kit.At(s, Knobs.GoodQuality)),
                             })
                        _out.AppendLine($"| {loc.Name} | {name} | {Fmt(Measure(loc, Build.Physical, s, kit), population)} |");
            _out.AppendLine();
        }

        // A fresh level-1 hero with nothing equipped farms Thornwood until its first Complete, then Ashfall until its
        // first Complete. Every Send rolls loot (Fail Bucket included) and equips upgrades; a Death loses 25% of the
        // bar and starts the next Send fresh.
        private void Campaign()
        {
            var total = Knobs.FailWeight + Knobs.RarityWeights.Sum(w => w.weight);
            _out.AppendLine($"## Campaign: fresh hero, Thornwood (Population {Knobs.ThornwoodPopulation}) then Ashfall (Population {Knobs.AshfallPopulation}), {Knobs.CampaignSeeds} seeds");
            _out.AppendLine();
            _out.AppendLine($"Loot: each kill rolls once; Fail Bucket {Knobs.FailWeight / total:P0}, " +
                            string.Join(", ", Knobs.RarityWeights.Select(w => $"{w.rarity} {w.weight / total:P1}")) + ".");
            _out.AppendLine();
            _out.AppendLine("| policy | build | Location | Sends to Complete (median / max) | Deaths (median) | trips (median) | level at Complete | kit avg power | items equipped | time (median, min) |");
            _out.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
            foreach (var policy in new[] { Policy.Careless, Policy.Played })
                foreach (var build in new[] { Build.Physical, Build.Magical })
                {
                    var rows = new Dictionary<string, List<(int sends, int deaths, int trips, int level, float kit, int items, float time)>>
                    {
                        ["Thornwood"] = new(), ["Ashfall"] = new(),
                    };
                    for (var seed = 1; seed <= Knobs.CampaignSeeds; seed++)
                    {
                        var hero = new SweepHero(build, 1, Kit.None);
                        var rng = new Random(seed * 31);
                        var sendSeed = seed * 1000;
                        foreach (var loc in new[] { Loc.Thornwood(Knobs.ThornwoodPopulation), Loc.Ashfall(Knobs.AshfallPopulation) })
                        {
                            int sends = 0, deaths = 0, trips = 0, items = 0;
                            var time = 0f;
                            while (sends < 40)
                            {
                                sends++;
                                hero.FullHeal();
                                var r = Sender.Run(hero, loc, policy, ++sendSeed, loot: rng);
                                items += r.Drops.Count(d => d != ItemRarity.NoDrop);
                                Loot.Equip(hero, r.Drops, loc.S, rng);
                                trips += r.Trips;
                                time += r.Time;
                                if (r.Died) { deaths++; hero.LoseXpOnDeath(); }
                                if (r.Completed) break;
                            }
                            rows[loc.Name].Add((sends, deaths, trips, hero.Level, hero.Kit.Average, items, time));
                        }
                    }
                    foreach (var (name, list) in rows)
                    {
                        static float Med(IEnumerable<float> xs) { var l = xs.OrderBy(x => x).ToList(); return l[l.Count / 2]; }
                        _out.AppendLine($"| {policy} | {build} | {name} | {Med(list.Select(x => (float)x.sends)):0} / {list.Max(x => x.sends)} | {Med(list.Select(x => (float)x.deaths)):0} | " +
                                        $"{Med(list.Select(x => (float)x.trips)):0} | {Med(list.Select(x => (float)x.level)):0} | {Med(list.Select(x => x.kit)):0.0} | {Med(list.Select(x => (float)x.items)):0} | {Med(list.Select(x => x.time)) / 60f:0.0} |");
                    }
                }
            _out.AppendLine();
        }
    }
}

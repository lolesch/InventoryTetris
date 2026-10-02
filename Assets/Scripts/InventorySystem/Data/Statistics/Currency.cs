using System;
using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data
{
    [Serializable]
    public struct Currency
    {
        [field: SerializeField] public uint Iron { get; private set; }
        [field: SerializeField] public uint Copper { get; private set; }
        [field: SerializeField] public uint Silver { get; private set; }
        [field: SerializeField] public uint Gold { get; private set; }

        /// Denomination ladder: iron -> copper -> silver -> gold at 5 / 12 / 20.
        /// Iron is the base unit - the cheapest metal, and the one almost never
        /// coined, because it is heavy, brittle when cast, and rusts. Mirrors
        /// pound-shilling-pence: 12 pence = 1 shilling, 20 shillings = 1 pound.
        /// The ratios multiply to the same 1200 as the old 20/12/5 ladder, so gold
        /// keeps its value and no item price needs retuning.
        public static readonly uint ironToCopper = 5u;
        public static readonly uint ironToSilver = 60u;
        public static readonly uint ironToGold = 1200u;
        public static readonly uint copperToSilver = ironToSilver / ironToCopper; // = 12
        public static readonly uint silverToGold = ironToGold / ironToSilver;     // = 20

        /// The one table of denominations, largest first, each with its worth in iron.
        /// Anything that walks the ladder (depositing, paying, the coin row) iterates
        /// <see cref="Denominations"/> rather than naming coins in its own order, so a
        /// ladder change lands here and nowhere else. Declared after the ratios above:
        /// static fields initialise in textual order.
        private static readonly (CurrencyType type, uint ironValue)[] ladder =
        {
            (CurrencyType.Gold, ironToGold),
            (CurrencyType.Silver, ironToSilver),
            (CurrencyType.Copper, ironToCopper),
            (CurrencyType.Iron, 1u),
        };

        private static readonly CurrencyType[] denominations = Array.ConvertAll(ladder, entry => entry.type);

        /// <summary>The coin denominations, largest value first. A read-only view, so no
        /// caller can reorder the ladder out from under the others.</summary>
        public static ReadOnlySpan<CurrencyType> Denominations => denominations;

        /// <summary>A coin's worth in iron base units; zero for <see cref="CurrencyType.NONE"/>.</summary>
        public static uint ValueOf(CurrencyType type)
        {
            foreach (var (denomination, ironValue) in ladder)
                if (denomination == type)
                    return ironValue;

            return 0u;
        }

        /// <summary><paramref name="amount"/> coins of one denomination; empty for <see cref="CurrencyType.NONE"/>.</summary>
        public static Currency Of(CurrencyType type, uint amount) => default(Currency).With(type, amount);

        /// <summary>How many coins of <paramref name="type"/> this holds.</summary>
        public readonly uint CountOf(CurrencyType type) => type switch
        {
            CurrencyType.Iron => Iron,
            CurrencyType.Copper => Copper,
            CurrencyType.Silver => Silver,
            CurrencyType.Gold => Gold,
            _ => 0u,
        };

        /// <summary>A copy with the <paramref name="type"/> count replaced; unchanged for <see cref="CurrencyType.NONE"/>.</summary>
        public readonly Currency With(CurrencyType type, uint count)
        {
            var copy = this;

            switch (type)
            {
                case CurrencyType.Iron: copy.Iron = count; break;
                case CurrencyType.Copper: copy.Copper = count; break;
                case CurrencyType.Silver: copy.Silver = count; break;
                case CurrencyType.Gold: copy.Gold = count; break;
            }

            return copy;
        }

        public readonly uint Total => Iron + Copper * ironToCopper + Silver * ironToSilver + Gold * ironToGold;

        /// <summary>
        /// The denomination's fixed Rarity on CONTEXT.md's ladder — iron Common, copper
        /// Magic, silver Rare, gold Unique — so a loot filter reads coins and items on one
        /// scale and a minted coin carries the tint of its tier. Was
        /// <c>HeroBehaviour.RarityOf</c>; hoisted here because the mint path
        /// (<see cref="ICurrencyMinter"/>) and the loot filter both need it.
        /// </summary>
        public static ItemRarity RarityOf(CurrencyType denomination) => denomination switch
        {
            CurrencyType.Iron => ItemRarity.Common,
            CurrencyType.Copper => ItemRarity.Magic,
            CurrencyType.Silver => ItemRarity.Rare,
            CurrencyType.Gold => ItemRarity.Unique,
            _ => ItemRarity.NoDrop,
        };

        public Currency( uint total )
        {
            // Carry the remainder down instead of re-deriving it at each denomination:
            // 3 divisions + 3 modulos instead of 3 + 6, and each div/mod pair on the
            // same operands is one hardware division.
            Gold = total / ironToGold;

            var rest = total % ironToGold;
            Silver = rest / ironToSilver;

            rest %= ironToSilver;
            Copper = rest / ironToCopper;
            Iron = rest % ironToCopper;
        }

        public Currency( float total ) => this = new Currency( (uint)Mathf.Abs( total ) );

        public Currency(uint iron, uint copper, uint silver, uint gold)
        {
            Iron = iron;
            Copper = copper;
            Silver = silver;
            Gold = gold;
        }

        /// <summary>
        /// Works out how to pay <paramref name="price"/> from this wallet, spending the
        /// smallest denominations first so large coins are kept. Returns false (both
        /// outs left at zero) when this wallet's total value is below the price; a zero
        /// price returns true and charges nothing.
        /// </summary>
        public readonly bool TryGetPayment(Currency price, out Currency toRemove, out Currency change)
        {
            toRemove = default;
            change = default;

            var owed = price.Total;

            if (Total < owed)
                return false;

            uint paid = 0u;

            var spent = default(Currency);

            for (var i = ladder.Length - 1; 0 <= i; i--) // smallest first
            {
                var (type, ironValue) = ladder[i];
                spent = spent.With(type, Take(CountOf(type), ironValue));
            }

            toRemove = spent;
            change = new Currency(paid - owed); // paid >= owed is guaranteed once Total >= owed
            return true;

            uint Take(uint have, uint denomination)
            {
                if (owed <= paid || 0u == have)
                    return 0u;

                var stillOwed = owed - paid;
                var wanted = (stillOwed + denomination - 1u) / denomination; // ceil(stillOwed / denomination)
                var taken = have < wanted ? have : wanted;

                paid += taken * denomination;
                return taken;
            }
        }

        public readonly override string ToString() => $"{Gold}G, {Silver}S, {Copper}C, {Iron}I ({Total})";
    }
}

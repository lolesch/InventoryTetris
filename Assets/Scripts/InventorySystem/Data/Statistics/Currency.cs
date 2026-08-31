using System;
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

        public readonly uint Total => Iron + Copper * ironToCopper + Silver * ironToSilver + Gold * ironToGold;

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

            var iron = Take(Iron, 1u);
            var copper = Take(Copper, ironToCopper);
            var silver = Take(Silver, ironToSilver);
            var gold = Take(Gold, ironToGold);

            toRemove = new Currency(iron, copper, silver, gold);
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

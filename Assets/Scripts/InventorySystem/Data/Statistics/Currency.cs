using System;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data
{
    [Serializable]
    public struct Currency
    {
        [field: SerializeField] public uint Copper { get; private set; }
        [field: SerializeField] public uint Iron { get; private set; }
        [field: SerializeField] public uint Silver { get; private set; }
        [field: SerializeField] public uint Gold { get; private set; }

        public static readonly uint copperToIron = 20u;
        public static readonly uint copperToSilver = 240u;
        public static readonly uint copperToGold = 1200u;
        public static readonly uint ironToSilver = copperToSilver / copperToIron; // = 12
        public static readonly uint silverToGold = copperToGold / copperToSilver; // = 5

        public readonly uint Total => Copper + Iron * copperToIron + Silver * copperToSilver + Gold * copperToGold;

        public Currency( uint total )
        {
            // Carry the remainder down instead of re-deriving it at each denomination:
            // 3 divisions + 3 modulos instead of 3 + 6, and each div/mod pair on the
            // same operands is one hardware division.
            Gold = total / copperToGold;

            var rest = total % copperToGold;
            Silver = rest / copperToSilver;

            rest %= copperToSilver;
            Iron = rest / copperToIron;
            Copper = rest % copperToIron;
        }

        public Currency( float total ) => this = new Currency( (uint)Mathf.Abs( total ) );

        public Currency(uint copper, uint iron, uint silver, uint gold)
        {
            Copper = copper;
            Iron = iron;
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

            var copper = Take(Copper, 1u);
            var iron = Take(Iron, copperToIron);
            var silver = Take(Silver, copperToSilver);
            var gold = Take(Gold, copperToGold);

            toRemove = new Currency(copper, iron, silver, gold);
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

        public readonly override string ToString() => $"{Gold}G, {Silver}S, {Iron}I, {Copper}C ({Total})";
    }
}

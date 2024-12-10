using System;
using ToolSmiths.InventorySystem.Data.Enums;
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

        [field: SerializeField] public readonly uint Total => Copper + Iron * copperToIron + Silver * copperToSilver + Gold * copperToGold;

        public Currency( uint total )
        {
            Gold = total / copperToGold;
            Silver = total % copperToGold / copperToSilver;
            Iron = total % copperToGold % copperToSilver / copperToIron;
            Copper = total % copperToGold % copperToSilver % copperToIron;
        }

        public Currency( float total ) => this = new Currency( (uint)Mathf.Abs( total ) );

        public Currency(uint copper, uint iron, uint silver, uint gold)
        {
            Copper = copper;
            Iron = iron;
            Silver = silver;
            Gold = gold;
        }

        public Currency GetClosestPriceWithoutChange( Currency price )
        {
            if( Copper < price.Copper )
                price.RoundTo( CurrencyType.Iron );
            if( Iron < price.Iron )
                price.RoundTo( CurrencyType.Silver );
            if( Silver < price.Silver )
                price.RoundTo( CurrencyType.Gold );

            return price;
        }

        private void RoundTo( CurrencyType type )
        {
            if (type == CurrencyType.Iron)
            {
                Copper = 0;
                Iron += 1;
                if (Iron >= ironToSilver)
                    RoundTo( CurrencyType.Silver );
            }
            else if (type == CurrencyType.Silver)
            {
                Copper = 0;
                Iron = 0;
                Silver += 1;
                if (Silver >= silverToGold)
                    RoundTo( CurrencyType.Gold );
            }
            else if (type == CurrencyType.Gold)
            {
                Copper = 0;
                Iron = 0;
                Silver = 0;
                Gold += 1;
            }
        }

        public readonly override string ToString() => $"{Gold}G, {Silver}S, {Iron}I, {Copper}C ({Total})";
    }
}

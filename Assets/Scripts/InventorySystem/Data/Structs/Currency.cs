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
        public static readonly uint copperToSilver = 240u; // ironToSilver = 12;
        public static readonly uint copperToGold = 1200u;  // silverToGold = 5;

        [SerializeField] public readonly uint Total => Copper + Iron * copperToIron + Silver * copperToSilver + Gold * copperToGold;

        public Currency(uint total)
        {
            Gold = total / copperToGold;
            Silver = total % copperToGold / copperToSilver;
            Iron = total % copperToGold % copperToSilver / copperToIron;
            Copper = total % copperToGold % copperToSilver % copperToIron;
        }

        public Currency(float total) => this = new Currency((uint)Mathf.Abs(total));

        public override readonly string ToString() => $"{Gold}G, {Silver}S, {Iron}C, {Copper}I ({Total})";
    }
}
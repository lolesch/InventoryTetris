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

        public static readonly uint ironToCopper = 20u;
        public static readonly uint ironToSilver = 240u; // copperToSilver = 12;
        public static readonly uint ironToGold = 1200u;  // silverToGold = 5;

        [SerializeField] public readonly uint Total => Iron + Copper * ironToCopper + Silver * ironToSilver + Gold * ironToGold;

        public Currency(uint total)
        {
            //var remainder = total;

            Gold = total / ironToGold;
            Silver = total % ironToGold / ironToSilver;
            Copper = total % ironToGold % ironToSilver / ironToCopper;
            Iron = total % ironToGold % ironToSilver % ironToCopper;

            if (Iron + Copper * ironToCopper + Silver * ironToSilver + Gold * ironToGold != total)
                Debug.LogWarning("something went wrong here");

            Debug.Log(ToString());
        }

        public Currency(float total) => this = new Currency((uint)Mathf.Abs(total));

        public override readonly string ToString() => $"{Gold}G, {Silver}S, {Copper}C, {Iron}I ({Total})";
    }
}
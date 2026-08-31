using ToolSmiths.InventorySystem.Data.Distributions;
using UnityEditor;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data.Distributions.EditorScripts
{
    /// <summary>
    /// Adds a magic-find slider that previews the cascaded vector, plus the landmark
    /// crossover points so a retune is a deliberate, visible change (design § Further Notes).
    /// </summary>
    [CustomEditor(typeof(ItemRarityDistribution))]
    public class ItemRarityDistributionEditor : ProbabilityDistributionEditor
    {
        private float _magicFind;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var dist = (ItemRarityDistribution)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Magic-find preview (IncreasedItemRarity %)", EditorStyles.boldLabel);
            _magicFind = EditorGUILayout.Slider(_magicFind, 0f, 1500f);

            var cascaded = RarityMagicFind.Apply(dist.Probabilities, _magicFind);
            DrawVector(dist.OutcomeNames, cascaded);

            EditorGUILayout.HelpBox(
                "Landmarks — consequences of the base weights + Diablo II factors:\n" +
                "  Magic overtakes Common     ~50%\n" +
                "  Common reaches 0%           200%\n" +
                "  Rare overtakes Magic       ~429%\n" +
                "  Unique overtakes Magic    ~1364%\n" +
                "To retune, move the base weights or the factors in RarityMagicFind.cs.",
                MessageType.Info);
        }
    }
}

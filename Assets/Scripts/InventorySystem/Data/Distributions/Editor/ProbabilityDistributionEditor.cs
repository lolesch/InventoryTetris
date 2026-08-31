using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data.Distributions;
using UnityEditor;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data.Distributions.EditorScripts
{
    /// <summary>
    /// Draws the authored fields, then the derived probability vector and a rolled sample
    /// as read-only labels. Nothing here writes to serializedObject, so merely inspecting
    /// an asset never dirties it — the version-control churn the old OnValidate caused.
    /// </summary>
    [CustomEditor(typeof(AbstractProbabilityDistribution), true)]
    public class ProbabilityDistributionEditor : Editor
    {
        private const int SampleSize = 20;
        private string _sample;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector(); // failWeight + quantities — the only writable fields

            var dist = (AbstractProbabilityDistribution)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Probabilities — derived, not saved", EditorStyles.boldLabel);
            DrawVector(dist.OutcomeNames, dist.Probabilities);

            EditorGUILayout.Space();
            if (GUILayout.Button($"Roll {SampleSize} sample outcomes"))
                _sample = RollSample(dist);
            if (!string.IsNullOrEmpty(_sample))
                EditorGUILayout.HelpBox(_sample, MessageType.None);
        }

        protected static void DrawVector(IReadOnlyList<string> names, IReadOnlyList<float> probabilities)
        {
            for (var i = 0; i < names.Count && i < probabilities.Count; i++)
            {
                var rect = EditorGUILayout.GetControlRect();
                EditorGUI.ProgressBar(rect, probabilities[i], $"{names[i]}   {probabilities[i] * 100f:0.0}%");
            }
        }

        private static string RollSample(AbstractProbabilityDistribution dist)
        {
            var rng = new System.Random();
            var counts = new Dictionary<string, int>();
            for (var i = 0; i < SampleSize; i++)
            {
                var name = dist.SampleName((float)rng.NextDouble());
                counts.TryGetValue(name, out var c);
                counts[name] = c + 1;
            }
            return string.Join("\n", counts.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}: {kv.Value}"));
        }
    }
}

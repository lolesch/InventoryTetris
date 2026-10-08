using System;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// What each <see cref="EnemyArchetype"/> looks like in the arena (issue #176): its sprite and size.
    /// Where it stands and how fast it gets there are the sim's (spatial-combat spec, ADR-0018): an archetype's
    /// Strike Range and movement speed live on <see cref="Enemy"/>, and the arena only projects them.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyVisuals", menuName = "Inventory System/Enemy Visuals")]
    public sealed class EnemyVisuals : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public EnemyArchetype Archetype;
            [Tooltip("Authored facing right: the arena flips it with scale.x.")]
            public Sprite Sprite;
            [Tooltip("Canvas units; the view's root takes this size and the health bar sits above it.")]
            public Vector2 Size;
        }

        /// <summary>What an archetype without an entry gets, so a missing row shows a plain box, not nothing.</summary>
        public static readonly Entry Fallback = new()
        {
            Size = new Vector2(96f, 96f),
        };

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        /// <summary>The entry for <paramref name="archetype"/>, or <see cref="Fallback"/> when none is authored.</summary>
        public Entry For(EnemyArchetype archetype)
        {
            foreach (var entry in entries)
                if (entry.Archetype == archetype)
                    return entry;

            return Fallback;
        }
    }
}

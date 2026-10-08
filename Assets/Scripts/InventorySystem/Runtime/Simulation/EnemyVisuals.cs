using System;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// What each <see cref="EnemyArchetype"/> looks like in the arena (issue #176): its sprite and size,
    /// and where it stands - the ring radii around the hero anchor and how fast it walks to them. Both
    /// archetypes are strike-only in the sim, so a different stop distance and speed is the only identity a
    /// Skirmisher has against a Brute.
    /// <para>
    /// Radii are canvas units, not derived from the anchor's rect: the anchor is a small map icon.
    /// </para>
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
            [Min(1f), Tooltip("Ring half-width in canvas units.")]
            public float RingRadiusX;
            [Min(1f), Tooltip("Ring half-height in canvas units.")]
            public float RingRadiusY;
            [Min(1f), Tooltip("Canvas units per sim second, walking in from outside the ring. At least 1: a 0 never arrives.")]
            public float ApproachSpeed;
        }

        /// <summary>What an archetype without an entry gets, so a missing row shows a plain box, not nothing.</summary>
        public static readonly Entry Fallback = new()
        {
            Size = new Vector2(96f, 96f),
            RingRadiusX = 150f,
            RingRadiusY = 90f,
            ApproachSpeed = 200f,
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

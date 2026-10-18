using System;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Locations
{
    /// <summary>
    /// The authored base of everything the Unlock graph opens (ADR-0023): a stable id the save names, and
    /// what this one opens when it reaches Active and when it reaches Complete. Links are authored on the
    /// unlocking side; the target never lists its sources.
    /// </summary>
    public abstract class Unlockable : ScriptableObject
    {
        [Tooltip("Stable id a save references - a slug the author picks. Never the asset name, never the Unity asset GUID.")]
        [SerializeField] private string id;

        [Tooltip("Opened once this one is Active (and stays so once it is Complete).")]
        [SerializeField] private Unlockable[] unlocksWhenActive = Array.Empty<Unlockable>();

        [Tooltip("Opened once this one is Complete.")]
        [SerializeField] private Unlockable[] unlocksWhenComplete = Array.Empty<Unlockable>();

        /// <summary>Stable identity a save references. Never the asset GUID, never the asset name.</summary>
        public string Id => id;

        public Unlockable[] UnlocksWhenActive => unlocksWhenActive;
        public Unlockable[] UnlocksWhenComplete => unlocksWhenComplete;

#if UNITY_EDITOR
        /// <summary>Editor-only authoring seam for the id, used by a subclass's own <c>Author</c>.</summary>
        internal void AuthorId(string value) => id = value;
#endif
    }
}

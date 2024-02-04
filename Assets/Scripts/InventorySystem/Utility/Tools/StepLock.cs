using System;
using System.Collections.Generic;
using System.Linq;

namespace ToolSmiths.InventorySystem.Utility
{
    [Serializable]
    public class StepLock
    {
        private readonly List<object> lockers = new();

        /// <summary>
        /// This action is called on toggle - on first in it invokes true, on last out it invokes false
        /// </summary>
        public event Action<bool> locked;

        public bool IsLocked => lockers.Any();
        public bool IsUnlocked => !IsLocked;

        public void ForceUnlock() => lockers.Clear();

        public virtual void Add(object locker)
        {
            if (IsUnlocked)
                locked?.Invoke(true);

            if (!lockers.Contains(locker))
                lockers.Add(locker);
        }

        public virtual void Remove(object locker)
        {
            if (lockers.Contains(locker))
                lockers.Remove(locker);

            if (IsUnlocked)
                locked?.Invoke(false);
        }
    }
}

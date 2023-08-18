using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Components.Toggles
{
    public class RadioGroup : MonoBehaviour
    {
        [field: SerializeField, ReadOnly] public AbstractToggle ActivatedToggle { get; private set; }
        [field: SerializeField] public bool AllowSwitchOff { get; private set; } = false;

        public event Action OnGroupChanged;

        private readonly List<AbstractToggle> toggleList = new();

        private void OnValidate()
        {
            if (!TryGetComponent(out LayoutGroup _))
                LogExtensions.MissingComponent(nameof(LayoutGroup), gameObject);
        }

        public void SetOtherTogglesOff(AbstractToggle activatedToggle)
        {
            if (activatedToggle == null || ActivatedToggle == activatedToggle)
                return;

            ActivatedToggle = activatedToggle;

            for (var i = 0; i < toggleList.Count; i++)
                if (toggleList[i].IsOn && toggleList[i] != ActivatedToggle)
                    toggleList[i].SetToggle(false);

            OnGroupChanged?.Invoke();
        }

        public void Register(AbstractToggle item)
        {
            if (Contains(item))
                return;

            toggleList.Add(item);

            OnGroupChanged?.Invoke();

            bool Contains(AbstractToggle item)
            {
                for (var i = 0; i < toggleList.Count; i++)
                    if (toggleList[i].Equals(item))
                        return true;

                return false;
            }
        }

        public void Unregister(AbstractToggle item)
        {
            for (var i = toggleList.Count; i-- > 0;)
                if (toggleList[i].Equals(item))
                {
                    toggleList.RemoveAt(i);

                    OnGroupChanged?.Invoke();
                }
        }
    }
}
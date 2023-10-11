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

        private readonly List<AbstractToggle> radioToggles = new();

        private void OnValidate()
        {
            if (!TryGetComponent(out LayoutGroup _))
                LogExtensions.MissingComponent(nameof(LayoutGroup), gameObject);
        }

        public void Activate(AbstractToggle activatedToggle)
        {
            if (activatedToggle == null || ActivatedToggle == activatedToggle)
                return;

            ActivatedToggle = activatedToggle;

            foreach (var toggle in radioToggles)
                if (toggle != ActivatedToggle /*&& toggle.IsOn*/)
                    toggle.SetToggle(false);

            OnGroupChanged?.Invoke();
        }

        public void Register(AbstractToggle item)
        {
            if (radioToggles.Contains(item))
                return;

            radioToggles.Add(item);

            OnGroupChanged?.Invoke();
        }

        public void Unregister(AbstractToggle item)
        {
            if (!radioToggles.Contains(item))
                return;

            radioToggles.Remove(item);

            OnGroupChanged?.Invoke();
        }
    }
}
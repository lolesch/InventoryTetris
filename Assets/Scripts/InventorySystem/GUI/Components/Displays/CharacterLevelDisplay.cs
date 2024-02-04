using System;
using TMPro;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Runtime.Provider;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    [Serializable]
    public class CharacterLevelDisplay : MonoBehaviour, IDisplay<float>
    {
        [SerializeField] protected Image icon;

        [SerializeField] protected TextMeshProUGUI text;
        [SerializeField] protected BaseCharacter character;

        private void OnDisable()
        {
            var experience = character.GetResource(StatName.Experience);
            experience.TotalHasChanged -= RefreshDisplay;

            CharacterProvider.Instance.PlayerChanged -= SetCharacter;
        }

        private void OnEnable()
        {
            if (CharacterProvider.Instance.Player != null)
                SetCharacter(CharacterProvider.Instance.Player);
            else
            {
                CharacterProvider.Instance.PlayerChanged -= SetCharacter;
                CharacterProvider.Instance.PlayerChanged += SetCharacter;
            }
        }

        private void SetCharacter(BaseCharacter newCharacter)
        {
            if (character != newCharacter)
            {
                character = newCharacter;

                var experience = character.GetResource(StatName.Experience);
                experience.TotalHasChanged -= RefreshDisplay;
                experience.TotalHasChanged += RefreshDisplay;

                icon.sprite = ItemProvider.Instance.ItemTypeData.GetStatIcon(experience.Stat);

                RefreshDisplay();
            }
        }

        public void RefreshDisplay(float ignoreMe = 0) => text.text = character.CharacterLevel.ToString();
    }
}
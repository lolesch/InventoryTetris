using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Runtime.Provider;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    public class SkillHUD : MonoBehaviour
    {
        private LocalPlayer player;
        [SerializeField] private SkillSlot[] skillSlots = new SkillSlot[6];

        private void OnDisable() => CharacterProvider.Instance.PlayerChanged -= SetPlayer;
        private void OnEnable()
        {
            if (CharacterProvider.Instance.Player != null)
                SetPlayer(CharacterProvider.Instance.Player);
            else
            {
                CharacterProvider.Instance.PlayerChanged -= SetPlayer;
                CharacterProvider.Instance.PlayerChanged += SetPlayer;
            }
        }

        private void SetPlayer(LocalPlayer player) => this.player = player;

        private void LateUpdate()
        {
            if (player == null)
                return;

            for (var i = 0; i < player.ActiveSkills.Length; i++)
            {
                var skill = player.ActiveSkills[i];
                // tick down the skills cooldown 
                if (skill == null)
                    skillSlots[i].SetCooldownFillAmount(1f);
                else
                {
                    if (skill.SpawnData.CooldownTicker.HasRemainingDuration)
                        skill.SpawnData.CooldownTicker.Tick(Time.deltaTime);

                    skillSlots[i].SetCooldownFillAmount(1f - skill.SpawnData.CooldownTicker.Progress01);
                }
            }
        }
    }
}

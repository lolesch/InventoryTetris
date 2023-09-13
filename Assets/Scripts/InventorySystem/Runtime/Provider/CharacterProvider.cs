using TMPro;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Runtime.Provider;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Inventories
{
    public class CharacterProvider : AbstractProvider<CharacterProvider>
    {
        [field: SerializeField] public LocalPlayer Player { get; private set; }
        [field: SerializeField] public DummyTarget Dummy { get; private set; }

        [SerializeField] private Slider dummyCurrentHealthSlider;
        [SerializeField] private TextMeshProUGUI dummyCurrentHealthText;

        private uint DummyHealthAmount => dummyCurrentHealthSlider != null ? (uint)dummyCurrentHealthSlider.value : 100;

        private void DealDamage(BaseCharacter dealer, BaseCharacter receiver, DamageType damageType) => dealer.DealDamageTo(receiver, damageType);

        public void PlayerDealsPhysicalDamageToDummy() => DealDamage(Player, Dummy, DamageType.PhysicalDamage);
        public void PlayerDealsMagicalDamageToDummy() => DealDamage(Player, Dummy, DamageType.MagicalDamage);

        public void DummyDealsPhysicalDamageToPlayer() => DealDamage(Dummy, Player, DamageType.PhysicalDamage);
        public void DummyDealsMagicalDamageToPlayer() => DealDamage(Dummy, Player, DamageType.MagicalDamage);
    }
}

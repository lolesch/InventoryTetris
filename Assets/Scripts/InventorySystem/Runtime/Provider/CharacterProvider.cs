using System;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Provider
{
    public class CharacterProvider : AbstractProvider<CharacterProvider>
    {
        [field: SerializeField, ReadOnly] public LocalPlayer Player { get; private set; }
        [field: SerializeField, ReadOnly] public DummyTarget Dummy { get; private set; }
        [field: SerializeField] private LocalPlayer PlayerPrefab;
        [field: SerializeField] private DummyTarget DummyPrefab;

        public event Action<LocalPlayer> PlayerChanged;

        public void Awake()
        {
            SpawnDummy(DummyPrefab);
            SpawnPlayer(PlayerPrefab);

            PlayerChanged?.Invoke(Player);
        }

        private void SpawnPlayer(LocalPlayer player)
        {
            Player = Instantiate(player, gameObject.transform);

            Player.GetComponentInChildren<BaseMovement>().speed = Player.GetStat(StatName.MovementSpeed);
            Player.gameObject.SetActive(true);
            player.gameObject.SetActive(false);
        }

        private void SpawnDummy(DummyTarget dummy)
        {
            Dummy = dummy;

            var dummyPrefab = Instantiate(Dummy, Vector3.forward * 3, Quaternion.identity, gameObject.transform);

            dummyPrefab.GetComponentInChildren<BaseMovement>().speed = Dummy.GetStat(StatName.MovementSpeed);
            dummyPrefab.gameObject.SetActive(true);
            dummy.gameObject.SetActive(false);
        }

        private void DealDamage(BaseCharacter dealer, BaseCharacter receiver, DamageType damageType) => dealer.DealDamageTo(receiver, damageType);

        public void PlayerDealsPhysicalDamageToDummy() => DealDamage(Player, Dummy, DamageType.PhysicalDamage);
        public void PlayerDealsMagicalDamageToDummy() => DealDamage(Player, Dummy, DamageType.MagicalDamage);

        public void DummyDealsPhysicalDamageToPlayer() => DealDamage(Dummy, Player, DamageType.PhysicalDamage);
        public void DummyDealsMagicalDamageToPlayer() => DealDamage(Dummy, Player, DamageType.MagicalDamage);
        public void KillPlayer() => Player.GetResource(StatName.Health).DepleteCurrent();
        public void KillDummy() => Dummy.GetResource(StatName.Health).DepleteCurrent();

        public void ToggleSpendingResource() => Player.SpendResource = !Player.SpendResource;
    }
}

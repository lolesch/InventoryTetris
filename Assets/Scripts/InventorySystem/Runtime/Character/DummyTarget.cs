using System.Collections;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Runtime.Provider;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Character
{
    public class DummyTarget : BaseCharacter
    {
        [ContextMenu("OnDeath")]
        protected override void OnDeath()
        {
            Debug.LogWarning($"{name.ColoredComponent()} {"died!".Colored(Color.red)}", this);

            var randomEquipment = ItemProvider.Instance.GenerateRandomLoot();

            foreach (var item in randomEquipment)
                //rework to drop items on the floor
                _ = CharacterProvider.Instance.Player.PickUpItem(new Package(null, item, 1u));

            // TODO: use event instead?
            CharacterProvider.Instance.Player.GainExperience(BaseValues.ExperienceWhenKilled, CharacterLevel);

            StartCoroutine(RespawnDelay());

            IEnumerator RespawnDelay(float delay = 1f)
            {
                yield return new WaitForSeconds(delay);
                Respawn();
            }
        }

        private void Respawn()
        {
            CharacterLevel++;

            BaseValues.CharacterResources.Where(x => x.Stat == StatName.Health).FirstOrDefault().BaseValue += CharacterLevel + 3; // * 100 + 80

            OnBirth();
        }
    }
}

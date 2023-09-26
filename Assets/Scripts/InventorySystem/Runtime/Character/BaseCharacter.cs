using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Character
{
    public abstract class BaseCharacter : MonoBehaviour
    {
        /// ADVANCED DAMAGE CONCEPT:
        // BaseCharacter performs an areal attack (DamageZone/Area/Shape)
        // validate all BaseCharacters within that area as targets (Faction)
        // the area deals damage to all targets over its lifespan

        [field: SerializeField] public bool IsInvincible { get; protected set; } = false;
        [field: SerializeField] public bool IsBlocking { get; protected set; } = false;
        [field: SerializeField] public bool SpendResource { get; set; } = true;
        [SerializeField] public bool IsDead => this.GetResource(StatName.Health).IsDepleted;

        [field: SerializeField] public CharacterStat[] CharacterStats { get; protected set; }
        [field: SerializeField] public CharacterResource[] CharacterResources { get; protected set; }

        [field: SerializeField, Range(1, 100)] public uint CharacterLevel { get; protected set; } = 1;

        protected void OnValidate() => ResetStatsAndResources();

        protected void Start()
        {
            var health = this.GetResource(StatName.Health);
            health.CurrentHasDepleted -= OnDeath;
            health.CurrentHasDepleted += OnDeath;
            health.RefillCurrent();

            var resource = this.GetResource(StatName.Resource);
            resource.CurrentHasDepleted -= CharacterResourceWarning;
            resource.CurrentHasDepleted += CharacterResourceWarning;
            resource.RefillCurrent();

            //TODO: design Shield recharge
            this.GetResource(StatName.Shield).DepleteCurrent();

            //TODO: design Experience
            this.GetResource(StatName.Experience).DepleteCurrent();

            void CharacterResourceWarning() => Debug.LogWarning($"{name.ColoredComponent()} resource {"depleted".Colored(Color.red)}", this);
        }

        protected void Update()
        {
            //TODO: COMBAT TICK RATE
            //var interval = 0f;
            //interval += Time.deltaTime;
            //if(interval >= combatTickRate)

            RegenerateResource(this.GetResource(StatName.Resource), this.GetStat(StatName.ResourceRegeneration).TotalValue, false);
            RegenerateResource(this.GetResource(StatName.Health), this.GetStat(StatName.HealthRegeneration).TotalValue, true);

            static void RegenerateResource(CharacterResource resource, float recoveryAmount, bool stopIfDepleeted)
            {
                if (stopIfDepleeted && resource.IsDepleted)
                    return;

                _ = resource.AddToCurrent(recoveryAmount * Time.deltaTime);
            }
        }

        [ContextMenu("ResetStatsAndResources")]
        private void ResetStatsAndResources()
        {
            var resourcesOnly = new List<StatName>() { StatName.Health, StatName.Resource, StatName.Shield, StatName.Experience };

            var statNames = System.Enum.GetValues(typeof(StatName)) as StatName[];
            var statsOnly = statNames.ToList();

            foreach (var resource in resourcesOnly)
                statsOnly.Remove(resource);

            if (CharacterStats.Length != statsOnly.Count)
            {
                CharacterStats = new CharacterStat[statsOnly.Count];

                for (var i = 0; i < statsOnly.Count; i++)
                    CharacterStats[i] = new CharacterStat(statsOnly[i], 1);
            }

            if (CharacterResources.Length != resourcesOnly.Count)
            {
                //for (var i = 0; i < resourcesOnly.Count; i++)
                //    CharacterResources[i] = new CharacterResource(resourcesOnly[i], 100);

                CharacterResources = new CharacterResource[] {
                    new CharacterResource(StatName.Health, 100),
                    new CharacterResource(StatName.Resource, 60),
                    new CharacterResource(StatName.Shield, 0),
                    new CharacterResource(StatName.Experience, 280),
                };
            }
        }

        protected abstract void OnDeath();

        public void DealDamageTo(BaseCharacter target, DamageType damageType)
        {
            if (IsDead)
                return;

            var resourceCost = this.CalculateRequiredResource(damageType);
            var resource = this.GetResource(StatName.Resource);

            // TODO: if Shield protects resource
            //var shield = GetResource(this, StatName.Shield);

            //resourceCost = shield.RemoveFromCurrent(resourceCost);

            if (resourceCost == 0 || CanSpendResource(resource, resourceCost))
            {
                resource.RemoveFromCurrent(resourceCost);

                var damageOutput = this.CalculateDamageOutput(damageType);

                Debug.Log($"{name.ColoredComponent()} deals {damageOutput.ToString().Colored(Color.red)} {damageType}", this);
                target.ReceiveDamageFrom(this, damageType, damageOutput);
            }

            //AddDealtDPS(damageOutput);
        }

        private static bool CanSpendResource(CharacterResource resource, float amount) => amount <= resource.CurrentValue;

        public void ReceiveDamageFrom(BaseCharacter dealer, DamageType damageType, float incomingDamage)
        {
            var health = this.GetResource(StatName.Health);

            if (health.IsDepleted)
                return;

            var mitigatedDamage = this.CalculateReceivingDamage(damageType, incomingDamage);

            // TODO: if Shield protects resource instead => then skip shielding
            var shield = this.GetResource(StatName.Shield);

            var unshieldedDamage = shield.RemoveFromCurrent(mitigatedDamage);

            Debug.Log($"{name.ColoredComponent()} absorbs {Mathf.Min(mitigatedDamage - unshieldedDamage, shield.TotalValue).ToString().Colored(Color.red)} {damageType}", this);

            Debug.Log($"{name.ColoredComponent()} receives {Mathf.Min(unshieldedDamage, health.TotalValue).ToString().Colored(Color.red)} {damageType}", this);

            health.RemoveFromCurrent(unshieldedDamage);

            //AddReceivedDPS(healthDamage);
        }
    }
}

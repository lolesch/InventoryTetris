using System;
using System.Linq;
using System.Threading.Tasks;
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

        // public Dictionary<StatName,CharacterStat> CharacterStats { get; protected set; }
        [field: SerializeField] public CharacterBaseValues BaseValues;
        [field: SerializeField] public CharacterStat[] CharacterStats { get; protected set; }
        [field: SerializeField] public CharacterResource[] CharacterResources { get; protected set; }

        [field: SerializeField, Range(1, 100)] public uint CharacterLevel { get; protected set; } = 1;

        private CharacterResource health;
        private CharacterResource resource;
        private CharacterResource shield;
        private CharacterResource experience;

        //protected void OnValidate() => OnBirth();

        protected virtual void OnDisable()
        {
            var health = this.GetResource(StatName.Health);
            health.CurrentHasDepleted -= OnDeath;

            var resource = this.GetResource(StatName.Resource);
            resource.CurrentHasDepleted -= CharacterResourceWarning;
        }

        protected virtual void Awake() => OnBirth();

        protected virtual void OnEnable()
        {
            health.CurrentHasDepleted -= OnDeath;
            health.CurrentHasDepleted += OnDeath;

            resource.CurrentHasDepleted -= CharacterResourceWarning;
            resource.CurrentHasDepleted += CharacterResourceWarning;

        }

        protected virtual void Update()
        {
            //TODO: COMBAT TICK RATE
            //var interval = 0f;
            //interval += Time.deltaTime;
            //if(interval >= combatTickRate)

            RegenerateResource(health, this.GetStat(StatName.HealthRegeneration).TotalValue, -1f);
            RegenerateResource(this.GetResource(StatName.Resource), this.GetStat(StatName.ResourceRegeneration).TotalValue, 0f);

            if (health.IsDepleted)
                shield.DepleteCurrent();
            //TODO: design Shield recharge
            else
                RegenerateResource(shield, this.GetStat(StatName.ShieldRecharge).TotalValue, 2f);
        }

        private void CharacterResourceWarning() => Debug.LogWarning($"{name.ColoredComponent()} resource {"depleted".Colored(Color.red)}", this);

        private static async void RegenerateResource(CharacterResource resource, float recoveryAmount, float recoveryDelay)
        {
            if (resource == null)
                return;

            if (resource.IsFull)
                return;

            if (resource.IsDepleted)
                if (recoveryDelay < 0) // a negative delay will end the regeneration
                    return;
                else if (0 < recoveryDelay)
                    await Task.Delay(TimeSpan.FromSeconds(recoveryDelay));

            _ = resource.AddToCurrent(recoveryAmount * Time.deltaTime);
        }

        [ContextMenu("ResetStatsAndResources")]
        public void ResetStatsAndResources()
        {
            if (BaseValues == null)
            {
                Debug.LogWarning("Missing Character Base Values", this);
                return;
            }

            var statNames = Enum.GetValues(typeof(StatName)) as StatName[];

            var resourcesOnly = new StatName[] { StatName.Health, StatName.Resource, StatName.Shield, StatName.Experience };
            var statsOnly = statNames.ToList();
            foreach (var resource in resourcesOnly)
                statsOnly.Remove(resource);

            if (CharacterStats.Length != statsOnly.Count)
            {
                CharacterStats = new CharacterStat[statsOnly.Count];

                for (var i = 0; i < statsOnly.Count; i++)
                {
                    var baseValue = BaseValues.CharacterStats.Where(x => x.Stat == statsOnly[i]).FirstOrDefault().BaseValue;
                    CharacterStats[i] = new CharacterStat(statsOnly[i], baseValue);
                }
            }

            foreach (var stat in CharacterStats)
                stat.CalculateTotalValue();

            if (CharacterResources.Length != resourcesOnly.Length)
            {
                var health = BaseValues.CharacterResources.Where(x => x.Stat == StatName.Health).FirstOrDefault().BaseValue;
                var resource = BaseValues.CharacterResources.Where(x => x.Stat == StatName.Resource).FirstOrDefault().BaseValue;
                var shield = BaseValues.CharacterResources.Where(x => x.Stat == StatName.Shield).FirstOrDefault().BaseValue;
                var experience = BaseValues.CharacterResources.Where(x => x.Stat == StatName.Experience).FirstOrDefault().BaseValue;

                CharacterResources = new CharacterResource[] {
                    new CharacterResource(StatName.Health, health),
                    new CharacterResource(StatName.Resource, resource),
                    new CharacterResource(StatName.Shield, shield),
                    new CharacterResource(StatName.Experience, experience),
                };
            }

            foreach (var resource in CharacterResources)
                resource.CalculateTotalValue();
        }

        protected abstract void OnDeath();

        public virtual void OnBirth()
        {
            ResetStatsAndResources();

            health = this.GetResource(StatName.Health);
            resource = this.GetResource(StatName.Resource);
            shield = this.GetResource(StatName.Shield);
            experience = this.GetResource(StatName.Experience);

            health.RefillCurrent();
            resource.RefillCurrent();
            shield.RefillCurrent();
            experience.DepleteCurrent();
        }

        //TODO: design Experience
        //this.GetResource(StatName.Experience).DepleteCurrent();

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

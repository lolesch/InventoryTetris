using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using Submodules.Utility.Extensions;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Character
{
    /// <summary>
    /// The scene face of a <see cref="Hero"/>: the component the scene's displays and the
    /// simulation hold, forwarding everything to the plain hero it owns. The stats, regeneration,
    /// damage and XP live on the <see cref="Hero"/> (issue #110); what stays here is what needs a
    /// <see cref="MonoBehaviour"/> - the death and depletion reactions, and the log lines that name
    /// the object they came from. A subclass builds its hero once, on first ask, so it exists
    /// whichever component's <c>Awake</c> or <c>OnEnable</c> reaches it first.
    /// </summary>
    public abstract class BaseCharacter : MonoBehaviour
    {
        /// ADVANCED DAMAGE CONCEPT:
        // BaseCharacter performs an areal attack (DamageZone/Area/Shape)
        // validate all BaseCharacters within that area as targets (Faction)
        // the area deals damage to all targets over its lifespan

        private Hero _hero;

        public virtual Hero Hero => _hero ??= BuildHero();

        protected abstract Hero BuildHero();

        public bool IsInvincible => Hero.IsInvincible;
        public bool IsBlocking => Hero.IsBlocking;
        public bool SpendResource { get => Hero.SpendResource; set => Hero.SpendResource = value; }
        public bool IsDead => Hero.IsDead;

        public uint CharacterLevel => Hero.Level;

        // The hero the reactions below are bound to. A face that reads its hero off the Session
        // (LocalPlayer) outlives a Stop with scene reload disabled, and the next Play entry (or a
        // hero load, #114) hands it a different hero, so the binding follows the hero it sees.
        private Hero _bound;

        protected void Start() => Bind(Hero);

        private void Update()
        {
            var hero = Hero;

            if (!ReferenceEquals(hero, _bound))
                Bind(hero);
        }

        private void Bind(Hero hero)
        {
            Unbind(_bound);
            _bound = hero;

            var health = hero.GetResource(StatName.Health);
            health.CurrentHasDepleted += OnDeath;

            var resource = hero.GetResource(StatName.Resource);
            resource.CurrentHasDepleted += CharacterResourceWarning;

            hero.DamageDealt += LogDamageDealt;
            hero.DamageReceived += LogDamageReceived;
        }

        private void Unbind(Hero hero)
        {
            if (hero == null)
                return;

            hero.GetResource(StatName.Health).CurrentHasDepleted -= OnDeath;
            hero.GetResource(StatName.Resource).CurrentHasDepleted -= CharacterResourceWarning;
            hero.DamageDealt -= LogDamageDealt;
            hero.DamageReceived -= LogDamageReceived;
        }

        private void OnDestroy() => Unbind(_bound);

        /// <summary>
        /// Applies one step of Health, Resource and Shield regeneration for
        /// <paramref name="deltaSeconds"/> of elapsed time. Driven by
        /// <see cref="Simulation.SimulationDriver"/> at sim speed, in both Town and Field
        /// (issue #45). Dead heroes do not regenerate.
        /// </summary>
        public void Regenerate(float deltaSeconds) => Hero.Regenerate(deltaSeconds);

        protected abstract void OnDeath();

        public void DealDamageTo(BaseCharacter target, DamageType damageType) => Hero.DealDamageTo(target.Hero, damageType);

        public void ReceiveDamageFrom(BaseCharacter dealer, DamageType damageType, float incomingDamage) =>
            ReceiveDamage(damageType, incomingDamage);

        /// <summary>
        /// The dealer-less incoming-damage path: mitigate by the matching resist, spend the
        /// Shield, then the Health - identical to <see cref="ReceiveDamageFrom"/> minus the
        /// (unused) dealer reference. The Encounter sim's enemies are not <see cref="BaseCharacter"/>s,
        /// so the hero adapter (issue #43) routes their Strikes through here.
        /// </summary>
        public void ReceiveDamage(DamageType damageType, float incomingDamage) => Hero.ReceiveDamage(damageType, incomingDamage);

        private void CharacterResourceWarning() => Debug.LogWarning($"{name.ColoredComponent()} resource {"depleted".Colored(Color.red)}", this);

        private void LogDamageDealt(DamageType damageType, float damageOutput) =>
            Debug.Log($"{name.ColoredComponent()} deals {damageOutput.ToString().Colored(Color.red)} {damageType}", this);

        private void LogDamageReceived(DamageType damageType, float absorbed, float received)
        {
            Debug.Log($"{name.ColoredComponent()} absorbs {absorbed.ToString().Colored(Color.red)} {damageType}", this);

            Debug.Log($"{name.ColoredComponent()} receives {received.ToString().Colored(Color.red)} {damageType}", this);
        }
    }
}

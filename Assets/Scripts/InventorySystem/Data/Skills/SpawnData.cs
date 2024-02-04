using ToolSmiths.InventorySystem.Utility;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data.Skills
{
    [CreateAssetMenu(fileName = "New Spawn Behaviour", menuName = "Inventory System/Skills/SpawnBehaviour")]
    public class SpawnData : ScriptableObject
    {
        // Active/Passive/Toggle
        // CastingType
        // Targeting - auto, self, single, ground, skillshot
        // Shape
        // Effect

        // COMPONENT SETTINGS:

        // scaling - what stats this ability will benefit from
        // charges/ammo - as opposed to a cooldown

        public Ticker CooldownTicker;

        [Tooltip("The duration (seconds) to wait before re-casting is enabled")]
        [Range(0f, 300f)][SerializeField] private float cooldownDuration = 0f;
        public float CooldownDuration => cooldownDuration;

        [Tooltip("The required resource amount consumed during casting")]
        [Range(0u, 300u)][SerializeField] private uint manaCost = 10;
        public uint ManaCost => manaCost;

        [Header("Spawn Settings")]

        // Range - the maximum distance between caster and target in order for the ability to be cast / to hit

        // Casttime - Abilities may incur a casting time in which the caster is unable to perform any other actions for a short period of time until the ability takes effect
        //              (can only be interrupted by the champion's death).
        //              Cast times are measured in seconds and commonly last for 0.25, 0.5, and 1 second(s).
        //              They prevent abilities from triggering their effects instantaneously.
        //                  A cast may include a lockout that prevents certain actives until afterward(buffer), or disables them entirely.
        //                  This lockout can extend past the cast time up until the ability ends.

        [Tooltip("Spawn at the current cursor position? else spawn at the caster's position")]
        [SerializeField] private bool spawnAtCursor = false;
        public bool SpawnAtCursor => spawnAtCursor;

        [Tooltip("The max distance to the caster the skill can spawn at")]
        [Range(0f, 30f)][SerializeField] private float spawnRange = 5;
        public float SpawnRange => spawnRange;

        [Header("Projectile Settings")]

        [Tooltip("The inner radius of the shape")]
        [Range(0f, 14f)][SerializeField] private float innerRadius = 0f;
        public float InnerRadius => innerRadius;

        [Tooltip("The outer radius of the shape")]
        [Range(0.1f, 15f)][SerializeField] private float outerRadius = 5;
        public float OuterRadius => outerRadius;

        [Tooltip("The angle of the projectile centered on it's forward direction")]
        [Range(0u, 360u)][SerializeField] private uint shapeAngle = 360u;
        public float ShapeAngle => shapeAngle;

        [Range(0f, 20f)][SerializeField] private float lifetime = 0;
        public float Lifetime => lifetime;

        //[SerializeField] private bool isRequiringATarget = false;
        //[SerializeField] private bool isPiercing = false;
        //[SerializeField] private bool isHoming = false;

        ///
        [Header("Path Settings")]

        [Tooltip("The amount of projectiles spawned")]
        [Range(1u, 24u)][SerializeField] private uint projectileAmount = 1u;
        public uint ProjectileAmount => projectileAmount;

        [Tooltip("The traveling speed of the projectile")]
        [Range(0u, 30u)][SerializeField] private uint projectileSpeed = 3u;
        public float ProjectileSpeed => projectileSpeed;

        [Tooltip("The maximal travel distance before despawning the projectile")]
        [Range(0f, 30f)][SerializeField] private float despawnRange = 1;
        public float DespawnRange => despawnRange;

        //[SerializeField] private Projectile projectile;
        //public Projectile Projectile => projectile;
        //
        //[Tooltip("The type of Interactables to target with this projectile")]
        //[SerializeField] private List<InteractionType> targetTypes = new();
        //public List<InteractionType> TargetTypes => targetTypes;
        //
        //[SerializeReference] public List<IEffect> effects = new();
    }
}

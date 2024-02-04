using ToolSmiths.InventorySystem.Utility;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;
using UnityEngine.AI;

namespace ToolSmiths.InventorySystem.Runtime.Character
{
    [RequireComponent(typeof(NavMeshAgent))]
    public abstract class BaseMovement : MonoBehaviour
    {
        [SerializeField] protected NavMeshAgent agent;
        [SerializeField] public CharacterStat speed;
        //[SerializeField] protected Animator animator;

        [Header("Rotation")]
        [Range(1f, 40f)]
        [SerializeField] private float rotationSpeed = 17f;

        protected StepLock movementLocker = new();

        protected virtual void OnValidate()
        {
            agent = GetComponent<NavMeshAgent>();
            if (!agent)
                Debug.LogError($"Missing component of type {nameof(NavMeshAgent)} on {gameObject.name}");

            //character = GetComponent<BaseCharacter>();
            //if (!character)
            //    Debug.LogError($"Missing component of type {nameof(BaseCharacter)} on {gameObject.name}");
        }

        protected virtual void OnDisable()
        {
            movementLocker.locked -= ForceStop;

            speed.TotalHasChanged -= SetSpeed;
        }

        protected virtual void Awake() => agent.updateRotation = false;

        protected virtual void OnEnable()
        {
            movementLocker.locked -= ForceStop;
            movementLocker.locked += ForceStop;

            speed.TotalHasChanged -= SetSpeed;
            speed.TotalHasChanged += SetSpeed;

            SetSpeed(speed.TotalValue);
        }

        protected virtual void SetSpeed(float speed) => agent.speed = speed;

        protected virtual void SetDestination(Vector3 target)
        {
            if (target != agent.pathEndPosition)
                agent.SetDestination(target);
            Debug.DrawLine(transform.position, agent.destination);
        }

        private void ForceStop(bool locked)
        {
            agent.isStopped = locked;
            SetDestination(transform.position + transform.forward * agent.stoppingDistance);
        }

        protected void SetRotation()
        {
            var rotationTarget = agent.steeringTarget;
            rotationTarget.y = transform.position.y;

            var direction = transform.position.Direction(rotationTarget);

            if (direction == Vector3.zero)
                return;

            var desiredRotation = Quaternion.LookRotation(direction);

            if (transform.rotation != desiredRotation)
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Time.deltaTime * rotationSpeed);
        }
    }
}

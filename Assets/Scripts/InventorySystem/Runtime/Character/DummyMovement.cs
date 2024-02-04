using ToolSmiths.InventorySystem.Runtime.Provider;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Character
{
    public class DummyMovement : BaseMovement
    {
        [SerializeField, ReadOnly] private PlayerMovement playerAgent;

        protected override void Awake()
        {
            base.Awake();

            // implement interaction range -> so enemies stop when close enough to perform an action
            agent.stoppingDistance = 2.4f * agent.radius;
        }

        private void Start() => playerAgent = CharacterProvider.Instance.Player.GetComponentInChildren<PlayerMovement>();

        private void Update()
        {
            SetDestination(playerAgent.gameObject.transform.position);

            SetRotation();
        }
    }
}

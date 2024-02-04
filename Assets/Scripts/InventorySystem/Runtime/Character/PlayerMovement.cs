using UnityEngine;
using UnityEngine.AI;

namespace ToolSmiths.InventorySystem.Runtime.Character
{
    public class PlayerMovement : BaseMovement
    {
        private NavMeshHit navMeshHit;

        private void Update()
        {
            if (Input.GetMouseButton(0))
                SetMovementTarget(Input.mousePosition);

            //if (hasMovementInput)
            //{
            //    var hasGamepadMovementInput = 0.125f < InputProvider.Instance.LeftStickVector.magnitude;
            //
            //    Vector2 screenPoint = hasGamepadMovementInput ? Camera.main.WorldToScreenPoint(InputProvider.Instance.LeftStickVector) : InputProvider.Instance.PointerPosition;
            //    SetMovementTarget(screenPoint);
            //}

            //var speed = character.GetStat(StatName.MovementSpeed);

            //if ( 0 < speed.TotalValue)
            //    animator.SetFloat("Run Blend", agent.velocity.magnitude / speed.TotalValue);

            // make pawnRotation a component that listens to several rotation invokers
            // casting a skill can rotate the pawn without the need of movement input

            SetRotation();
        }

        //private void SetMove(bool clickMove) => hasMovementInput = clickMove;

        public void SetMovementTarget(Vector3 inputTarget)
        {
            var ray = Camera.main.ScreenPointToRay(inputTarget);

            if (Physics.Raycast(ray, out var hit, Mathf.Infinity, 3))
                inputTarget = hit.point;

            //if (Interactable.Current == null)
            SetDestination(FindNavigableLocationAt(inputTarget));

            // switch case?
            //else if (Interactable.Current.Interaction == InteractionType.Enemy || Interactable.Current.Interaction == InteractionType.Destroyable)
            //{
            //    Vector3 target = Interactable.Current.transform.position;
            //    target = target - GetDirection(transform.position, target) * Mathf.Min(/* TODO: skill attack range or default meele attack range */20 + Interactable.Current.InteractionRange, Vector3.Distance(transform.position, target));
            //    SetDestination(FindNavigableLocationAt(target));
            //}
            //
            //else if (Interactable.Current.Interaction == InteractionType.NPC || Interactable.Current.Interaction == InteractionType.Container)
            //{
            //    Vector3 target = Interactable.Current.transform.position;
            //    target = target - GetDirection(transform.position, target) * Mathf.Min(Interactable.Current.InteractionRange, Vector3.Distance(transform.position, target));
            //    SetDestination(FindNavigableLocationAt(target));
            //}
        }

        public void LockMovement(bool stop)
        {
            if (stop)
                movementLocker.Add(this);
            else
                movementLocker.Remove(this);
        }

        private Vector3 FindNavigableLocationAt(Vector3 input)
        {
            //if (Interactable.Current)
            //    return Interactable.Current.transform.position -
            //            GetDirection(transform.position, Interactable.Current.transform.position) *
            //            Mathf.Min(Vector3.Distance(Interactable.Current.transform.position, transform.position), Interactable.Current.InteractionRange);

            ///check if hitResult.point is navigable
            if (SamplePosition(input))
                return navMeshHit.position;

            var lerpLocation = Vector3.zero;
            var from = transform.position;
            var to = input;

            ///calculate nearest navigable position
            for (var i = 0; i < 5; i++)
            {
                lerpLocation = Vector3.Lerp(from, to, .5f);

                if (SamplePosition(lerpLocation))
                    from = lerpLocation;
                else
                    to = lerpLocation;
            }

            Debug.DrawLine(transform.position, input, Color.red);

            if (SamplePosition(from))
                return navMeshHit.position;

            Debug.LogError($"No navigable target after lerp at \t {lerpLocation}");
            return transform.position;
        }

        private bool SamplePosition(Vector3 position) => NavMesh.SamplePosition(position, out navMeshHit, 1f, NavMesh.AllAreas);

    }
}

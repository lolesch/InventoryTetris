using ToolSmiths.InventorySystem.Data;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ToolSmiths.InventorySystem.Displays
{
    [System.Serializable]
    [RequireComponent(typeof(RectTransform))]

    public class EquipmentSlotDisplay : AbstractSlotDisplay
    {
        public override void OnPointerClick(PointerEventData eventData)
        {
            throw new System.NotImplementedException();
        }

        public override void OnBeginDrag(PointerEventData eventData) => PickUpItem();

        public override void OnDrag(PointerEventData eventData)
        {
            throw new System.NotImplementedException();
        }

        public override void OnDrop(PointerEventData eventData) => DropItem();

        protected internal override void PickUpItem()
        {
            throw new System.NotImplementedException();
        }

        protected internal override void DropItem()
        {
            throw new System.NotImplementedException();
        }

        protected internal override void RefreshSlotDisplay(Package package)
        {
            throw new System.NotImplementedException();
        }
    }
}

using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Components
{
    [RequireComponent(typeof(GraphicRaycaster), typeof(CanvasRenderer))]
    public class TooltipRequester : Selectable, ISubmitHandler
    {
        // TODO: import and update tooltip from LOCA CSV Table
        [SerializeField, TextArea] private string tooltip = "";

        protected override void OnEnable()
        {
            base.OnEnable();

            if (targetGraphic)
                targetGraphic.raycastTarget = true;
            else
                LogExtensions.MissingComponent(nameof(Graphic), gameObject);
        }

        protected override void OnDisable() => base.OnDisable();// TooltipProvider.Instance.HideTooltip();

        public override void OnPointerEnter(PointerEventData eventData) => base.OnPointerEnter(eventData);// TooltipProvider.Instance.ShowTooltipData(tooltip);

        public override void OnPointerExit(PointerEventData eventData) => base.OnPointerExit(eventData);// TooltipProvider.Instance.HideTooltip();

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);

            LogExtensions.Select(eventData.selectedObject);
        }

        public virtual void OnSubmit(BaseEventData eventData) { }
    }
}
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ToolSmiths.InventorySystem.GUI.Components.Buttons
{
    public abstract class AbstractButton : TooltipRequester, IPointerClickHandler
    {
        // [SerializeField, Range(.8f, 1.2f)] protected float scaleOnSelect = 1.12f;
        // protected bool DoScaleOnSelect => scaleOnSelect != 1f;
        [SerializeField, Range(.8f, 1.2f)] protected float scaleOnHover = 1.06f;
        protected bool DoScaleOnHover => scaleOnHover != 1f;

        //private readonly UnityEvent onClick = new UnityEvent();

        protected override void OnEnable() => base.OnEnable();//onClick.RemoveListener(OnClick);//onClick.AddListener(OnClick);

        protected override void OnDisable() => base.OnDisable();//onClick.RemoveListener(OnClick);//if (targetGraphic && DOTween.IsTweening(targetGraphic.transform))//    _ = DOTween.Kill(targetGraphic.transform);

        // TODO: disable the button for x seconds to disable button spaming
        protected abstract void OnClick();

        public override void OnSubmit(BaseEventData eventData)
        {
            base.OnSubmit(eventData);

            if (!interactable)
                return;

            OnClick();

            if (DoScaleOnHover)
                Scale(false, scaleOnHover);
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);

            if (interactable)
                if (DoScaleOnHover)
                    Scale(true, scaleOnHover);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);

            if (interactable)
                if (DoScaleOnHover)
                    Scale(false, scaleOnHover);
        }

        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if (!interactable)
                return;

            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            //onClick.Invoke();
            OnClick();

            if (DoScaleOnHover)
                Scale(false, scaleOnHover);
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);

            if (interactable)
                if (DoScaleOnHover)
                    Scale(false, scaleOnHover);
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);

            if (interactable)
            {
                PlayHoverSound();

                if (DoScaleOnHover)
                    Scale(true, scaleOnHover);
            }
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);

            if (interactable)
            {
                PlayClickSound();

                if (DoScaleOnHover)
                    Scale(true, 1 - (scaleOnHover - 1) / 2);
            }
        }

        private void Scale(bool condition, float factor)
        {
            if (targetGraphic)
                _ = targetGraphic.transform.DOScale(condition ? factor : 1, .15f).SetEase(Ease.InOutSine);
        }

        public virtual void PlayHoverSound() { } // => AudioProvider.Instance.PlayButtonHover();
        public virtual void PlayClickSound() { } // => AudioProvider.Instance.PlayButtonClick();
    }
}
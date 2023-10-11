using DG.Tweening;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Components.Toggles
{
    public abstract class AbstractToggle : TooltipRequester, IPointerClickHandler
    {
        [SerializeField, Range(.8f, 1.2f)] protected float scaleOnSelect = 1.12f;
        protected bool DoScaleOnSelect => scaleOnSelect != 1f;
        [SerializeField, Range(.8f, 1.2f)] protected float scaleOnHover = 1.06f;
        protected bool DoScaleOnHover => scaleOnHover != 1f;

        [field: SerializeField] public bool IsOn { get; private set; } = false;

        [SerializeField, ReadOnly] protected RadioGroup radioGroup = null;
        public RadioGroup RadioGroup => radioGroup != null ? radioGroup : radioGroup = GetComponentInParent<RadioGroup>();

        // test wich one to use or if both are necessary
        //[SerializeField] protected bool isToggledOnAwake = false;
        //[SerializeField] protected bool isToggledOnEnable = false;

        [SerializeField] private Sprite toggledOffSprite;
        [SerializeField] private Sprite toggledOnSprite;

        public event Action<bool> OnToggle;

#pragma warning disable CS0114 // Member hides inherited member; missing override keyword
        protected void OnValidate()
#pragma warning restore CS0114 // Member hides inherited member; missing override keyword
        {
            if (RadioGroup != null && RadioGroup.transform != transform.parent)
                radioGroup = null;

            //IsOn = isToggledOnAwake || isToggledOnEnable;

            if (IsOn && RadioGroup)
                RadioGroup.Activate(this);
        }

        protected override void Awake() => base.Awake();//if (isToggledOnAwake)//    SetToggle(isToggledOnAwake);

        protected override void OnEnable()
        {
            base.OnEnable();

            //if (!isToggledOnAwake)
            //    SetToggle(isToggledOnEnable);

            if (RadioGroup && interactable)
                RadioGroup.Register(this);
        }

        protected override void Start()
        {
            SetToggle(IsOn);

            DoStateTransition(IsOn ? SelectionState.Selected : SelectionState.Normal, false);

            if (DoScaleOnSelect)
                Scale(IsOn, scaleOnSelect);
            else if (DoScaleOnHover)
                Scale(IsOn, scaleOnHover);
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (targetGraphic && DOTween.IsTweening(targetGraphic.transform))
                DOTween.Kill(targetGraphic.transform);

            if (RadioGroup)
                RadioGroup.Unregister(this);
        }

        //public void Toggle() => SetToggle(!IsOn);

        public virtual void SetToggle(bool isOn)
        {
            if (isOn == IsOn)
                return;

            IsOn = isOn;
            OnToggle?.Invoke(IsOn);

            if (toggledOffSprite != null && toggledOnSprite != null)
                (targetGraphic as Image).sprite = IsOn ? toggledOnSprite : toggledOffSprite;

            if (DoScaleOnSelect)
                Scale(IsOn, scaleOnSelect);

            DoStateTransition(IsOn ? SelectionState.Selected : SelectionState.Normal, true);

            PlayToggleSound(IsOn);

            if (IsOn && RadioGroup)
                RadioGroup.Activate(this);
        }

        public override void OnSubmit(BaseEventData eventData)
        {
            base.OnSubmit(eventData);

            if (!interactable)
                return;

            if (RadioGroup && !RadioGroup.AllowSwitchOff && IsOn)
                return;

            SetToggle(!IsOn);
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);

            if (interactable)
                if (DoScaleOnHover)
                    Scale(!IsOn, scaleOnHover);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);

            if (interactable)
            {
                DoStateTransition(IsOn ? SelectionState.Selected : SelectionState.Normal, false);

                if (DoScaleOnSelect)
                    Scale(IsOn, scaleOnSelect);
                else if (DoScaleOnHover)
                    Scale(IsOn, scaleOnHover);
            }
        }

        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if (!interactable)
                return;

            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            if (RadioGroup && !RadioGroup.AllowSwitchOff && IsOn)
                return;

            SetToggle(!IsOn);
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);

            if (interactable)
            {
                DoStateTransition(IsOn ? SelectionState.Selected : SelectionState.Normal, false);

                if (DoScaleOnSelect)
                    Scale(IsOn, scaleOnSelect);
                else if (DoScaleOnHover)
                    Scale(IsOn, scaleOnHover);
            }
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);

            if (interactable)
            {
                PlayHoverSound();

                if (DoScaleOnHover && !IsOn)
                    Scale(true, scaleOnHover);
            }
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);

            if (interactable && !IsOn)
                PlayClickSound();
        }

        private void Scale(bool condition, float factor)
        {
            if (targetGraphic)
                targetGraphic.transform.DOScale(condition ? factor : 1, .15f).SetEase(Ease.InOutSine);
        }

        public virtual void PlayHoverSound() { } // => AudioProvider.Instance.PlayButtonHover();
        public virtual void PlayClickSound() { } // => AudioProvider.Instance.PlayButtonClick();
        public virtual void PlayToggleSound(bool isOn) { } // => AudioProvider.Instance.PlayButtonClick();
    }
}
using DG.Tweening;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Panels
{
    /// <summary>
    /// Panels provide appearence options such as fading in and out, scaling and movement.
    /// A panel should always stay enabled, only its canvasGroup alpha is set to 0.
    /// Therefore use <see cref="BeforeAppear"/> instead of <see cref="OnEnable"/> to set data.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup), typeof(GraphicRaycaster))]
    public abstract class AbstractPanel : MonoBehaviour
    {
        #region COMPONENT REFERENCES
        protected CanvasGroup _canvasGroup = null;
        public CanvasGroup CanvasGroup => _canvasGroup != null ? _canvasGroup : _canvasGroup = GetComponentInParent<CanvasGroup>();

        protected RectTransform _transform = null;
        public RectTransform Transform => _transform != null ? _transform : _transform = GetComponentInParent<RectTransform>();
        #endregion COMPONENT REFERENCES

        [field: SerializeField, Range(0, 1)] public float FadeDuration { get; } = .2f;

        [Tooltip("The timespan to pass after fading in before automatically fading out again. \n0 means no autoFadeOut.")]
        [SerializeField, Range(0, 10)] private float fadeOutDelay = 0f;

        [SerializeField, Range(0f, 2f)] protected float scaleFrom = 1f;
        [SerializeField] protected Vector2 moveFrom = Vector2.zero;

        private Vector2 startPosition;

        protected bool IsScaling => scaleFrom != 1f;
        protected bool IsMoving => moveFrom != Vector2.zero;

        protected virtual void Awake()
        {
            startPosition = Transform.anchoredPosition;

            FadeOut(0);
        }

        protected virtual void OnDestroy() => KillTweens();

        protected virtual void OnDisable() => KillTweens();

        [ContextMenu("FadeIn")]
        public void FadeIn() => FadeIn(FadeDuration);
        public void FadeIn(float fadeInDuration)
        {
            KillTweens();

            BeforeAppear();

            if (fadeInDuration <= 0)
            {
                CanvasGroup.blocksRaycasts = true;
                CanvasGroup.alpha = 1;

                OnAppear();

                return;
            }

            _ = CanvasGroup.DOFade(1, fadeInDuration).SetEase(Ease.InOutQuad).OnComplete(() => OnAppear());

            if (IsMoving)
                _ = Transform.DOAnchorPos(startPosition, fadeInDuration).SetEase(Ease.InOutQuad);

            if (IsScaling)
            {
                Transform.localScale = new Vector2(scaleFrom, scaleFrom);
                _ = Transform.DOScale(1, fadeInDuration).SetEase(Ease.InOutQuad);
            }
        }

        public void FadeInAfterDelay(float fadeInDelay = 0)
        {
            if (fadeInDelay >= 0)
            {
                FadeIn();
                return;
            }

            var sequence = DOTween.Sequence();
            _ = sequence.InsertCallback(fadeInDelay, FadeIn);
        }

        /// <summary>
        /// Called right before the CanvasGroup fades in.
        /// </summary>
        protected virtual void BeforeAppear() { }

        /// <summary>
        /// Called after the CanvasGroup completed fading in.
        /// </summary>
        protected virtual void OnAppear()
        {
            CanvasGroup.blocksRaycasts = true;

            if (0 < fadeOutDelay)
            {
                var sequence = DOTween.Sequence();
                _ = sequence.InsertCallback(fadeOutDelay, FadeOut);
            }
        }

        [ContextMenu("FadeOut")]
        public void FadeOut() => FadeOut(FadeDuration);
        public void FadeOut(float fadeOutDuration)
        {
            KillTweens();

            // BeforeDisappear()

            if (fadeOutDuration <= 0)
            {
                CanvasGroup.blocksRaycasts = false;
                CanvasGroup.alpha = 0;

                // OnDisappear(); => used to reset the display values

                return;
            }

            CanvasGroup.blocksRaycasts = false;

            _ = CanvasGroup.DOFade(0, fadeOutDuration).SetEase(Ease.InQuad); //.OnComplete(() => OnDisappear());

            if (IsMoving)
                _ = Transform.DOAnchorPos(startPosition + moveFrom, fadeOutDuration).SetEase(Ease.InQuad);

            if (IsScaling)
                _ = Transform.DOScale(scaleFrom, fadeOutDuration).SetEase(Ease.InQuad);
        }

        [ContextMenu("Toggle Visibility")]
        public void Toggle()
        {
            if (CanvasGroup.alpha < 1)
                FadeIn();
            else
                FadeOut();
        }

        private void KillTweens()
        {
            if (CanvasGroup == null)
                return;

            if (DOTween.IsTweening(CanvasGroup))
                _ = DOTween.Kill(CanvasGroup);
        }

        //public abstract void SetData();
    }
}

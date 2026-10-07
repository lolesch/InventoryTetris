using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    public sealed class ResourceDisplay : MonoBehaviour
    {
        /// <summary>Which edge the fill grows from.</summary>
        private enum FillDirection { LeftToRight, RightToLeft, BottomToTop, TopToBottom }

        [SerializeField, Tooltip("RectMask2D that clips its child to the current value, as the ValueSlider does. Keeps sliced, tiled and shaped fills intact, which an Image's Filled type does not.")]
        private RectMask2D fillMask;
        [SerializeField, Tooltip("The edge the fill grows from: a bar from the left, a globe from the bottom.")]
        private FillDirection fillDirection;
        // [SerializeField] protected Image impactImage; // TODO: look it up in RuadhWarbands

        [SerializeField] private TextMeshProUGUI currentText;
        [SerializeField] private TextMeshProUGUI recoveryText;

        [SerializeField, Tooltip("Show the Session's Hero. Off: the display is driven by Bind, like an enemy's health bar.")] private bool followsHero;
        [SerializeField] private StatName resourceName = StatName.Health;
        [SerializeField] private StatName recoveryName = StatName.HealthRegeneration;

        [SerializeField] private AnimationCurve globeVolume;

        private CharacterResource _resource;
        private CharacterStat _recovery;
        private bool _bound;
        private float _fraction = 1f;
        private Vector2 _maskSize;
        private float _current;
        private float _total;
        private float _recoveryTotal;
        private bool _altShown;

        private static bool AltHeld => ModifierKeys.Alt;

        /// <summary>
        /// Drive this display from a resource handed in, instead of from the Hero — an
        /// enemy's health, which no Hero owns. <paramref name="recovery"/>
        /// is optional: without it the recovery text is left alone. Stays bound across a
        /// disable/enable (a pooled bar) until <see cref="Unbind"/>.
        /// </summary>
        public void Bind(CharacterResource resource, CharacterStat recovery = null)
        {
            Release();

            _resource = resource;
            _recovery = recovery;
            _bound = true;

            if (isActiveAndEnabled)
                Acquire();
        }

        /// <summary>Stop following the bound resource. Safe when nothing is bound.</summary>
        public void Unbind()
        {
            Release();

            _resource = null;
            _recovery = null;
            _bound = false;
        }

        private void OnEnable()
        {
            // No Session armed (an enable in Edit Mode): nothing to follow, and the Hero is not there to read.
            if (!_bound && followsHero && Session.TrySubscribeHeroLoaded(FollowHero))
            {
                var hero = Session.Instance.Hero;
                _resource = hero.GetResource(resourceName);
                _recovery = hero.GetStat(recoveryName);
            }

            if (currentText)
                currentText.text = string.Empty;
            
            Acquire();
        }

        private void OnDisable()
        {
            Session.UnsubscribeHeroLoaded(FollowHero);

            Release();

            if (_bound) 
                return;
            _resource = null;
            _recovery = null;
        }

        // A hero load (#114) replaces the Hero and with it its resource and stat: let go of the old
        // ones and follow the new. A display driven by Bind stays on what it was handed.
        private void FollowHero()
        {
            if (_bound || !followsHero)
                return;

            Release();

            var hero = Session.Instance.Hero;
            _resource = hero.GetResource(resourceName);
            _recovery = hero.GetStat(recoveryName);

            Acquire();
        }

        private void Acquire()
        {
            if (_resource != null)
            {
                _resource.CurrentHasChanged -= UpdateDisplay;
                _resource.CurrentHasChanged += UpdateDisplay;

                UpdateDisplay(0, _resource.CurrentValue, _resource.TotalValue);
            }

            if (_recovery != null)
            {
                _recovery.TotalHasChanged -= UpdateRechargeDisplay;
                _recovery.TotalHasChanged += UpdateRechargeDisplay;

                UpdateRechargeDisplay(_recovery.TotalValue);
            }
        }

        private void Release()
        {
            if (_resource != null)
                _resource.CurrentHasChanged -= UpdateDisplay;

            if (_recovery != null)
                _recovery.TotalHasChanged -= UpdateRechargeDisplay;
        }

        private void LateUpdate()
        {
            // The padding is in pixels of the mask's rect, so a layout that resizes the bar has to re-cut the fill.
            if (fillMask && fillMask.rectTransform.rect.size != _maskSize)
                ApplyFill();

            // The texts are formatted when a value changes, so a change of the Alt state has to re-format them.
            // Compared as a held state, not as KeyDown/KeyUp: either Alt counts, and an Alt+Tab never sends the KeyUp.
            if (AltHeld != _altShown)
                RefreshTexts();
        }

        private void UpdateDisplay(float previous, float current, float total)
        {
            _current = current;
            _total = total;

            var fraction = 0 < total ? Mathf.Clamp01(current / total) : 0f;

            _fraction = Mathf.Clamp01(0 < globeVolume.length ? globeVolume.Evaluate(fraction) : fraction);
            ApplyFill();
            RefreshTexts();
        }

        private void UpdateRechargeDisplay(float total)
        {
            _recoveryTotal = total;

            RefreshTexts();
        }

        /// <summary>Alt swaps the figures for a percentage. The texts are formatted from the cached values, so
        /// they can be redrawn on a key press without waiting for the resource to change.</summary>
        private void RefreshTexts()
        {
            _altShown = AltHeld;

            if (currentText && _resource != null)
                currentText.text = _altShown
                    ? $"{resourceName}: {(0 < _total ? _current / _total * 100 : 0f):0.#}%"
                    : $"{_current:0.#} / {_total:0.#}";

            if (recoveryText && _recovery != null)
                recoveryText.text = _altShown
                    ? $"{recoveryName}: {_recoveryTotal:F1} / sec"
                    : $"{_recoveryTotal:F1}";
        }

        /// <summary>Clips the mask from the far edge, so what is left shows <see cref="_fraction"/> of the bar.</summary>
        private void ApplyFill()
        {
            if (!fillMask)
                return;

            _maskSize = fillMask.rectTransform.rect.size;

            var hidden = 1f - _fraction;
            var left = fillDirection == FillDirection.RightToLeft ? _maskSize.x * hidden : 0f;
            var bottom = fillDirection == FillDirection.TopToBottom ? _maskSize.y * hidden : 0f;
            var right = fillDirection == FillDirection.LeftToRight ? _maskSize.x * hidden : 0f;
            var top = fillDirection == FillDirection.BottomToTop ? _maskSize.y * hidden : 0f;

            var padding = new Vector4(left, bottom, right, top);

            if (fillMask.padding != padding)
                fillMask.padding = padding;
        }
    }
}

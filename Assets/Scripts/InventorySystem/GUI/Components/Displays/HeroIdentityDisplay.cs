using Submodules.Utility.Services;
using Submodules.Utility.UI;
using TMPro;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    /// <summary>
    /// One text of the Session's hero - its name, level or class - that follows the hero like
    /// <see cref="ResourceDisplay"/> does: it repaints when a hero loads, when the hero list changes and when the
    /// hero's stats do (a level-up). A subclass says only how one <see cref="HeroIdentity"/> reads, so a screen
    /// that wants the hero's name drops <see cref="HeroNameDisplay"/> in and wires no code. With no hero selected
    /// the text is blank. Subscribes in <see cref="OnEnable"/>, not <c>Awake</c>, which a scene object keeps
    /// across Play entries without its subscriptions (see <c>codebase-notes.md</c>).
    /// </summary>
    public abstract class HeroIdentityDisplay : MonoBehaviour, IDisplay<HeroIdentity>
    {
        [SerializeField] private TextMeshProUGUI label;

        private IHeroSaveService watched;
        private Hero followed;

        private void OnEnable()
        {
            if (!Application.isPlaying || !Session.TrySubscribeHeroLoaded(Follow))
                return;

            watched = HeroSaveService.Instance;
            watched.HeroesChanged += Follow;
            Follow();
        }

        private void OnDisable()
        {
            Session.UnsubscribeHeroLoaded(Follow);
            Release();

            if (watched == null)
                return;

            watched.HeroesChanged -= Follow;
            watched = null;
        }

        public void Refresh(HeroIdentity identity) => label.text = identity.Exists ? Format(identity) : string.Empty;

        /// <summary>How the hero reads in this display. Only called for a hero that exists.</summary>
        protected abstract string Format(HeroIdentity identity);

        // A hero load replaces the Hero: let go of the old one's stats and follow the new.
        private void Follow()
        {
            Release();

            followed = Session.Instance.Hero;
            followed.StatsChanged += Repaint;
            Repaint();
        }

        private void Release()
        {
            if (followed != null)
                followed.StatsChanged -= Repaint;

            followed = null;
        }

        private void Repaint() => Refresh(HeroIdentity.Of(watched, followed));
    }
}

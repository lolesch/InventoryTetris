using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    /// <summary>
    /// The hero's stat sheet: one <see cref="CharacterStatDisplay"/> row per stat and resource,
    /// rebuilt whenever the <see cref="Hero"/> announces a change (issue #111). The hero knows no
    /// view - this subscribes to <see cref="Hero.StatsChanged"/>, and the scene's
    /// <see cref="BaseCharacter"/> only hands over the hero it owns.
    ///
    /// Rebinds to the new hero when <see cref="ISession.HeroLoaded"/> replaces it (#114).
    ///
    /// Subscribes in <see cref="OnEnable"/> and releases in <see cref="OnDisable"/>, never in
    /// <c>Awake</c>: with domain reload disabled a scene object keeps its <c>Awake</c> across Play
    /// entries but not its subscriptions (see <c>codebase-notes.md</c>). A change only marks the
    /// sheet dirty and the rebuild runs once in <see cref="LateUpdate"/>, so an item with six
    /// affixes, or a level-up that crosses several levels, costs one rebuild.
    /// </summary>
    public sealed class CharacterStatPanel : MonoBehaviour
    {
        [SerializeField, Tooltip("The character whose hero the sheet shows.")] private BaseCharacter character;
        [SerializeField, Tooltip("The row every stat is shown in; the rows are made beside it.")] private CharacterStatDisplay rowPrefab;

        // Made at runtime, so the Play session that made them destroys them while this component, with
        // domain reload disabled, outlives it: the stale entries are pruned on every rebuild.
        private readonly List<CharacterStatDisplay> _rows = new();
        private Hero _hero;
        private bool _dirty;

        private void OnEnable()
        {
            // Unity's lifetime-aware == : a character that was never assigned.
            if (character == null)
            {
                Debug.LogError($"{name} has no character whose stats it could show.", this);
                return;
            }

            _ = Session.TrySubscribeHeroLoaded(Rebind);

            Rebind();
        }

        private void OnDisable()
        {
            Session.UnsubscribeHeroLoaded(Rebind);

            Release();
        }

        // A hero load replaces the Hero (#114): let go of the old one's change event and bind the new one's.
        private void Rebind()
        {
            Release();

            _hero = character.Hero;
            _hero.StatsChanged += MarkDirty;

            Refresh();
        }

        private void Release()
        {
            if (_hero != null)
                _hero.StatsChanged -= MarkDirty;

            _hero = null;
        }

        private void MarkDirty() => _dirty = true;

        private void LateUpdate()
        {
            if (_dirty)
                Refresh();
        }

        private void Refresh()
        {
            _dirty = false;
            _rows.RemoveAll(row => row == null);

            var shown = 0;

            //TODO: extend prefabPool to support IDisplay<T> that update the Refresh(newData) before activating the object
            foreach (var stat in _hero.Resources.Cast<CharacterStat>().Union(_hero.Stats))
            {
                if (shown == _rows.Count)
                    _rows.Add(Instantiate(rowPrefab, rowPrefab.transform.parent));

                var row = _rows[shown++];

                row.Refresh(new(stat));

                row.gameObject.SetActive(true);
            }

            for (; shown < _rows.Count; shown++)
                _rows[shown].gameObject.SetActive(false);
        }
    }
}

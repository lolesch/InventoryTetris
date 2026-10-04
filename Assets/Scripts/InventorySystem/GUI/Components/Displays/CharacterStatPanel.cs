using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Runtime.Character;
using Submodules.Utility.Tools;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    /// <summary>
    /// The hero's stat sheet: one <see cref="CharacterStatDisplay"/> row per stat and resource,
    /// rebuilt whenever the <see cref="Hero"/> announces a change (issue #111). The hero knows no
    /// view - this subscribes to <see cref="Hero.StatsChanged"/>, and the scene's
    /// <see cref="BaseCharacter"/> only hands over the hero it owns.
    ///
    /// Subscribes in <see cref="OnEnable"/> and releases in <see cref="OnDisable"/>, never in
    /// <c>Awake</c>: with domain reload disabled a scene object keeps its <c>Awake</c> across Play
    /// entries but not its subscriptions (see <c>codebase-notes.md</c>).
    /// </summary>
    public sealed class CharacterStatPanel : MonoBehaviour
    {
        [SerializeField, Tooltip("The character whose hero the sheet shows.")] private BaseCharacter character;
        [SerializeField, Tooltip("The row every stat is shown in; the rows are made beside it.")] private CharacterStatDisplay rowPrefab;

        private PrefabPool<CharacterStatDisplay> _rows;
        private Hero _hero;

        private void OnEnable()
        {
            _hero = character.Hero;
            _hero.StatsChanged -= Refresh;
            _hero.StatsChanged += Refresh;

            Refresh();
        }

        private void OnDisable()
        {
            if (_hero != null)
                _hero.StatsChanged -= Refresh;

            _hero = null;
        }

        private void Refresh()
        {
            _rows ??= new(rowPrefab);
            _rows.ReleaseAll();

            //TODO: extend prefabPool to support IDisplay<T> that update the Refresh(newData) before activating the object
            foreach (var stat in _hero.Resources.Cast<CharacterStat>().Union(_hero.Stats))
            {
                var row = _rows.GetObject(false);

                row.Refresh(new(stat));

                row.gameObject.SetActive(true);
            }
        }
    }
}

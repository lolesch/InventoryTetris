using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.GUI.Displays;
using ToolSmiths.InventorySystem.Runtime.Provider;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Character
{
    public class LocalPlayerDisplays : MonoBehaviour
    {
        [SerializeField] private CharacterStatDisplay characterStatPrefab;

        [SerializeField] private CharacterDPSDisplay physicalDPS;
        [SerializeField] private CharacterDPSDisplay magicalDPS;

        [SerializeField, HideInInspector] private CharacterStat[] statsAndResources;
        [SerializeField] private StatName[] offensiveStats;
        [SerializeField] private RectTransform offensiveParent;
        [SerializeField] private StatName[] defensiveStats;
        [SerializeField] private RectTransform defensiveParent;
        [SerializeField] private StatName[] utilityStats;
        [SerializeField] private RectTransform utilityParent;
        [SerializeField] private LocalPlayer player;

        private void OnDisable()
        {
            foreach (var stat in statsAndResources)
                stat.TotalHasChanged -= UpdateDpsDisplay;

            CharacterProvider.Instance.PlayerChanged -= SetPlayer;
        }

        private void OnEnable()
        {
            foreach (var stat in statsAndResources)
            {
                stat.TotalHasChanged -= UpdateDpsDisplay;
                stat.TotalHasChanged += UpdateDpsDisplay;
            }

            if (CharacterProvider.Instance.Player != null)
                SetPlayer(CharacterProvider.Instance.Player);
            else
            {
                CharacterProvider.Instance.PlayerChanged -= SetPlayer;
                CharacterProvider.Instance.PlayerChanged += SetPlayer;
            }
        }

        private void UpdateDpsDisplay(float debug = 0)
        {
            physicalDPS.RefreshDisplay(new DPSData(player, DamageType.PhysicalDamage));
            magicalDPS.RefreshDisplay(new DPSData(player, DamageType.MagicalDamage));
        }

        private void SetPlayer(LocalPlayer newPlayer)
        {
            if (player != newPlayer)
            {
                player = newPlayer;

                statsAndResources = player.CharacterResources.Concat(player.CharacterStats).ToArray();

                foreach (var statName in offensiveStats)
                {
                    var display = Instantiate(characterStatPrefab, offensiveParent);
                    var stat = statsAndResources.Where(x => x.Stat == statName).FirstOrDefault();

                    if (stat != null)
                    {
                        display.RefreshDisplay(new(stat));
                        display.gameObject.SetActive(true);
                    }
                    else
                        display.gameObject.SetActive(false);
                }

                foreach (var statName in defensiveStats)
                {
                    var display = Instantiate(characterStatPrefab, defensiveParent);
                    var stat = statsAndResources.Where(x => x.Stat == statName).FirstOrDefault();

                    if (stat != null)
                    {
                        display.RefreshDisplay(new(stat));
                        display.gameObject.SetActive(true);
                    }
                    else
                        display.gameObject.SetActive(false);
                }

                foreach (var statName in utilityStats)
                {
                    var display = Instantiate(characterStatPrefab, utilityParent);
                    var stat = statsAndResources.Where(x => x.Stat == statName).FirstOrDefault();

                    if (stat != null)
                    {
                        display.RefreshDisplay(new(stat));
                        display.gameObject.SetActive(true);
                    }
                    else
                        display.gameObject.SetActive(false);
                }

                UpdateDpsDisplay();
            }
        }
    }
}

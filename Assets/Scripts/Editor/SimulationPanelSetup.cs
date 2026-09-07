using System.Linq;
using Submodules.Utility.UI;
using TMPro;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Runtime.Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Editor
{
    /// <summary>
    /// Editor helper that creates the simulation panels (MapPanel, BehaviourSlidersPanel)
    /// under the CombatContext MultiplePanelToggle and wires them into the scene (issue #27).
    /// Run via menu: Inventory System → Setup Simulation Panels.
    ///
    /// Idempotent: skips panels that already exist. Safe to re-run after scene changes.
    /// </summary>
    public static class SimulationPanelSetup
    {
        private const string MenuPath = "Inventory System/Setup Simulation Panels";

        [MenuItem(MenuPath)]
        public static void Setup()
        {
            // Find the CombatContext container and the Switch Context toggle.
            var combatContext = GameObject.Find("CombatContext");
            if (combatContext == null)
            {
                Debug.LogError("SimulationPanelSetup: CombatContext not found in scene.");
                return;
            }

            var switchContext = GameObject.Find("Switch Context");
            if (switchContext == null)
            {
                Debug.LogError("SimulationPanelSetup: Switch Context not found in scene.");
                return;
            }

            var multiToggle = switchContext.GetComponent<MultiplePanelToggle>();
            if (multiToggle == null)
            {
                Debug.LogError("SimulationPanelSetup: Switch Context has no MultiplePanelToggle.");
                return;
            }

            // Find or create the MapPanel.
            var mapPanel = FindOrCreatePanel<MapPanel>(combatContext.transform, "MapPanel");
            SetupMapPanel(mapPanel);

            // Find or create the BehaviourSlidersPanel.
            var slidersPanel = FindOrCreatePanel<BehaviourSlidersPanel>(combatContext.transform, "BehaviourSlidersPanel");
            SetupBehaviourSlidersPanel(slidersPanel);

            // Wire panels into the MultiplePanelToggle — both panels show in Town, hide in Field.
            WirePanelToggle(multiToggle, mapPanel.gameObject, addToTurnOn: true);
            WirePanelToggle(multiToggle, slidersPanel.gameObject, addToTurnOn: true);

            // Register the RunPhaseUIBinding if missing.
            if (combatContext.GetComponent<RunPhaseUIBinding>() == null)
            {
                var binding = combatContext.AddComponent<RunPhaseUIBinding>();
                var bindingSo = new SerializedObject(binding);
                bindingSo.FindProperty("switchContext").objectReferenceValue = multiToggle;
                bindingSo.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("SimulationPanelSetup: Added RunPhaseUIBinding to CombatContext.");
            }
            else
            {
                // Ensure the existing binding points at the right MultiplePanelToggle.
                var bindingSo = new SerializedObject(combatContext.GetComponent<RunPhaseUIBinding>());
                bindingSo.FindProperty("switchContext").objectReferenceValue = multiToggle;
                bindingSo.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(combatContext);
            Debug.Log("SimulationPanelSetup: Done. MapPanel and BehaviourSlidersPanel wired.");
        }

        private static T FindOrCreatePanel<T>(Transform parent, string name) where T : MonoBehaviour
        {
            var existing = parent.GetComponentsInChildren<T>(true)
                .FirstOrDefault(c => c.gameObject.name == name);

            if (existing != null)
                return existing;

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var panel = go.AddComponent<T>();
            Debug.Log($"SimulationPanelSetup: Created {name} under {parent.name}.");
            return panel;
        }

        private static void SetupMapPanel(MapPanel panel)
        {
            var go = panel.gameObject;

            // Ensure required AbstractPanel components.
            EnsureComponent<CanvasGroup>(go);
            EnsureComponent<GraphicRaycaster>(go);

            // Layout: vertical group, top-anchored, left side.
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(80, 0);
            rt.sizeDelta = new Vector2(250, 0);

            var layout = EnsureComponent<VerticalLayoutGroup>(go);
            layout.spacing = 4;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.padding = new RectOffset(8, 8, 8, 8);

            var fitter = EnsureComponent<ContentSizeFitter>(go);
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Create the RadioGroup container.
            var radioGo = new GameObject("LocationGroup", typeof(RectTransform));
            radioGo.transform.SetParent(go.transform, false);
            var radioRt = radioGo.GetComponent<RectTransform>();
            radioRt.sizeDelta = new Vector2(250, 30);
            var radioGroup = EnsureComponent<RadioGroup>(radioGo);
            EnsureComponent<VerticalLayoutGroup>(radioGo);

            // Set serialized fields on MapPanel via SerializedObject.
            var so = new SerializedObject(panel);
            so.FindProperty("locationGroup").objectReferenceValue = radioGroup;

            // Auto-populate LocationToggle entries from authored LocationConfig assets.
            var locations = AssetDatabase.FindAssets("t:LocationConfig")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<LocationConfig>)
                .Where(l => l != null)
                .ToArray();

            AbstractToggle townToggle = null;

            foreach (var location in locations)
            {
                var toggleGo = CreateLocationToggle(radioGo.transform, location);
                var toggle = toggleGo.GetComponent<LocationToggle>();
                // townToggle will be set after the loop.
            }

            // Create the Town toggle (plain AbstractToggle — triggers Recall).
            townToggle = CreateTownToggle(radioGo.transform);
            so.FindProperty("townToggle").objectReferenceValue = townToggle;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateLocationToggle(Transform parent, LocationConfig location)
        {
            var displayName = string.IsNullOrWhiteSpace(location.DisplayName)
                ? location.name
                : location.DisplayName;

            var go = new GameObject($"Toggle_{displayName}", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(250, 36);

            // Background image.
            var bg = EnsureComponent<Image>(go);
            bg.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);

            // LocationToggle component.
            var toggle = go.AddComponent<LocationToggle>();

            // Set the location via SerializedObject.
            var so = new SerializedObject(toggle);
            so.FindProperty("location").objectReferenceValue = location;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Label.
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.sizeDelta = Vector2.zero;
            var tmp = EnsureComponent<TextMeshProUGUI>(labelGo);
            tmp.text = displayName;
            tmp.fontSize = 14;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = Color.white;
            var tmpRt = labelGo.GetComponent<RectTransform>();
            tmpRt.offsetMin = new Vector2(8, 0);
            tmpRt.offsetMax = new Vector2(-8, 0);

            return go;
        }

        private static AbstractToggle CreateTownToggle(Transform parent)
        {
            var go = new GameObject("Toggle_Town", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(250, 36);

            // Background image.
            var bg = EnsureComponent<Image>(go);
            bg.color = new Color(0.2f, 0.15f, 0.1f, 0.8f);

            // Use a plain AbstractToggle subclass — TestToggle works for this.
            // We need a concrete subclass since AbstractToggle is abstract.
            var toggle = go.AddComponent<ToggleStub>();

            // Label.
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.sizeDelta = Vector2.zero;
            var tmp = EnsureComponent<TextMeshProUGUI>(labelGo);
            tmp.text = "Town";
            tmp.fontSize = 14;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = Color.white;
            var tmpRt = labelGo.GetComponent<RectTransform>();
            tmpRt.offsetMin = new Vector2(8, 0);
            tmpRt.offsetMax = new Vector2(-8, 0);

            return toggle;
        }

        private static void SetupBehaviourSlidersPanel(BehaviourSlidersPanel panel)
        {
            var go = panel.gameObject;

            // Ensure required AbstractPanel components.
            EnsureComponent<CanvasGroup>(go);
            EnsureComponent<GraphicRaycaster>(go);

            // Layout: vertical group, right side, matching ItemPanel position.
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-80, 100);
            rt.sizeDelta = new Vector2(305, 0);

            var layout = EnsureComponent<VerticalLayoutGroup>(go);
            layout.spacing = 4;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.padding = new RectOffset(8, 8, 8, 8);

            var fitter = EnsureComponent<ContentSizeFitter>(go);
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Create 5 slider rows. Each row: [Label] [Slider] [Value].
            var sliderNames = new[] { "Retreat HP", "Recall Bag", "Reserve", "Loot Filter", "Sim Speed" };
            var sliderFields = new[] { "retreatHealthSlider", "recallBagFillSlider", "resourceReserveSlider", "lootFilterSlider", "simSpeedSlider" };
            var sliderRanges = new[] {
                new Vector2(0f, 1f),   // Retreat HP
                new Vector2(0f, 1f),   // Recall Bag Fill
                new Vector2(0f, 1f),   // Resource Reserve
                new Vector2(0f, 3f),   // Loot Filter (discrete 0-3)
                new Vector2(0f, 1f),   // Sim Speed (log mapped)
            };
            var sliderWholeNumbers = new[] { false, false, false, true, false };

            var so = new SerializedObject(panel);

            for (var i = 0; i < sliderNames.Length; i++)
            {
                var rowGo = CreateSliderRow(go.transform, sliderNames[i], sliderRanges[i], sliderWholeNumbers[i]);
                var slider = rowGo.GetComponentInChildren<Slider>(true);
                so.FindProperty(sliderFields[i]).objectReferenceValue = slider;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateSliderRow(Transform parent, string label, Vector2 range, bool wholeNumbers)
        {
            var rowGo = new GameObject($"Row_{label}", typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);

            var rowRt = rowGo.GetComponent<RectTransform>();
            rowRt.sizeDelta = new Vector2(305, 32);

            var layout = EnsureComponent<HorizontalLayoutGroup>(rowGo);
            layout.spacing = 6;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.padding = new RectOffset(4, 4, 2, 2);

            // Label.
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(rowGo.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.sizeDelta = new Vector2(90, 32);
            var tmp = EnsureComponent<TextMeshProUGUI>(labelGo);
            tmp.text = label;
            tmp.fontSize = 12;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = Color.white;
            var le = EnsureComponent<LayoutElement>(labelGo);
            le.minWidth = 90;
            le.preferredWidth = 90;

            // Slider.
            var sliderGo = new GameObject("Slider", typeof(RectTransform));
            sliderGo.transform.SetParent(rowGo.transform, false);
            var sliderRt = sliderGo.GetComponent<RectTransform>();
            sliderRt.sizeDelta = new Vector2(120, 32);

            var bg = EnsureComponent<Image>(sliderGo);
            bg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            var slider = sliderGo.AddComponent<Slider>();
            slider.minValue = range.x;
            slider.maxValue = range.y;
            slider.wholeNumbers = wholeNumbers;
            slider.value = range.x;

            // Fill area.
            var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGo.transform.SetParent(sliderGo.transform, false);
            var fillAreaRt = fillAreaGo.GetComponent<RectTransform>();
            fillAreaRt.anchorMin = new Vector2(0, 0.25f);
            fillAreaRt.anchorMax = new Vector2(1, 0.75f);
            fillAreaRt.sizeDelta = Vector2.zero;

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.sizeDelta = Vector2.zero;
            var fillImg = EnsureComponent<Image>(fillGo);
            fillImg.color = new Color(0.4f, 0.7f, 0.4f, 1f);
            slider.fillRect = fillRt;

            // Handle slide area.
            var handleAreaGo = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleAreaGo.transform.SetParent(sliderGo.transform, false);
            var handleAreaRt = handleAreaGo.GetComponent<RectTransform>();
            handleAreaRt.anchorMin = Vector2.zero;
            handleAreaRt.anchorMax = Vector2.one;
            handleAreaRt.sizeDelta = Vector2.zero;

            var handleGo = new GameObject("Handle", typeof(RectTransform));
            handleGo.transform.SetParent(handleAreaGo.transform, false);
            var handleRt = handleGo.GetComponent<RectTransform>();
            handleRt.sizeDelta = new Vector2(16, 0);
            var handleImg = EnsureComponent<Image>(handleGo);
            handleImg.color = Color.white;
            slider.handleRect = handleRt;

            var sle = EnsureComponent<LayoutElement>(sliderGo);
            sle.minWidth = 120;
            sle.preferredWidth = 120;

            // Value display.
            var valueGo = new GameObject("Value", typeof(RectTransform));
            valueGo.transform.SetParent(rowGo.transform, false);
            var valueRt = valueGo.GetComponent<RectTransform>();
            valueRt.sizeDelta = new Vector2(50, 32);
            var valueTmp = EnsureComponent<TextMeshProUGUI>(valueGo);
            valueTmp.text = "0%";
            valueTmp.fontSize = 12;
            valueTmp.alignment = TextAlignmentOptions.MidlineRight;
            valueTmp.color = Color.white;
            var vle = EnsureComponent<LayoutElement>(valueGo);
            vle.minWidth = 50;
            vle.preferredWidth = 50;

            return rowGo;
        }

        /// <summary>Add the panel to the MultiplePanelToggle's panelsToTurnOn or panelsToTurnOff list.</summary>
        private static void WirePanelToggle(MultiplePanelToggle multiToggle, GameObject panelGo, bool addToTurnOn)
        {
            var so = new SerializedObject(multiToggle);
            var prop = so.FindProperty(addToTurnOn ? "panelsToTurnOn" : "panelsToTurnOff");

            // Check if already wired.
            for (var i = 0; i < prop.arraySize; i++)
            {
                if (prop.GetArrayElementAtIndex(i).objectReferenceValue == panelGo)
                    return;
            }

            prop.InsertArrayElementAtIndex(prop.arraySize);
            prop.GetArrayElementAtIndex(prop.arraySize - 1).objectReferenceValue = panelGo;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            return go.GetComponent<T>() != null ? go.GetComponent<T>() : go.AddComponent<T>();
        }

        /// <summary>Minimal concrete subclass of AbstractToggle for the Town button (editor-only).</summary>
        private class ToggleStub : AbstractToggle
        {
            public override void SetToggle(bool isOn) => base.SetToggle(isOn);
        }
    }
}

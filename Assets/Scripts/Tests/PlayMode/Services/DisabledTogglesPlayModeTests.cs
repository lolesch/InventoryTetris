using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Tests.PlayMode.Services
{
    /// <summary>
    /// A non-interactable toggle in the <c>Example</c> scene (Smith, Pub, the locked Vendor) shows its disabled
    /// colour the first time the scene loads, not only after the Town face has been left and entered again.
    /// </summary>
    public sealed class DisabledTogglesPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("Example");
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ANonInteractableToggle_ShowsItsDisabledColour_OnTheFirstLoad(
            [Values("Smith", "Pub", "VendorToggle")] string name)
        {
            yield return new WaitForSecondsRealtime(0.5f);

            var toggle = GameObject.Find(name).GetComponent<Selectable>();

            Assert.That(toggle.interactable, Is.False, "premise");
            Assert.That(toggle.targetGraphic.canvasRenderer.GetColor().a,
                Is.EqualTo(toggle.colors.disabledColor.a).Within(0.01f), $"{name} wears the disabled colour");
        }
    }
}

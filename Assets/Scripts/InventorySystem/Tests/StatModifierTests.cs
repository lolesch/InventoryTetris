using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests
{
    public class StatModifierTests
    {
        static void Release(Object obj) => Object.DestroyImmediate(obj);

        [Test]
        public void CanSetOrigin()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            StatModifier statModifier = new StatModifier();

            statModifier.SetOrigin(go);

            Assert.AreEqual(go, statModifier.Origin);

            Release(go);
        }
    }
}

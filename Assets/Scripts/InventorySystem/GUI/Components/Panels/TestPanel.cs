using System.Collections.Generic;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Panels
{
    public class TestPanel : AbstractPanel { }

    internal sealed class TestGrid : MonoBehaviour
    {
        [SerializeField][Range(0, 10)] private int rows = 4;
        [SerializeField][Range(0, 10)] private int columns = 5;
        [SerializeField] private List<AbstractPanel> displays = new();

        private void Start()
        {
            var ratio = columns / rows;

            PrintGridOrder(ratio);
        }

        public void PrintGridOrder(float ratio = 1)
        {
            for (var x = 0; x < rows; x++)
                for (var y = 0; y < columns; y++)
                {
                    var delay = x * ratio + y;
                    displays[x + y].FadeInAfterDelay(delay);
                }
        }
    }
}

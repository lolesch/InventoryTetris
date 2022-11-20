using UnityEditor;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Items
{
    [CustomEditor(typeof(AbstractItemObject), true)]
    public class AbstractItemObjectCustomEditor : Editor
    {
        private AbstractItemObject item;
    
        private void OnEnable() => item = target as AbstractItemObject;
    
        public override void OnInspectorGUI()
        {
            item.Icon = EditorGUILayout.ObjectField(item.Icon, typeof(Sprite), true, GUILayout.Height(80), GUILayout.Width(80)) as Sprite;
                
            base.OnInspectorGUI();
        }
    }
}
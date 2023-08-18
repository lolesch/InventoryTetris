using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SceneRefAttribute))]
public class SceneRefDrawer : PropertyDrawer
{
    private static List<string> SceneList = new List<string> { string.Empty, };

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if ((EditorBuildSettings.scenes.Length + 1) != SceneList.Count)
            LoadSceneList();

        if (SceneList.Count == 0)
            SceneList.Add(string.Empty);

        var index = SceneList.IndexOf(property.stringValue);

        if (index == -1)
            index = 0;

        int goalIndex = EditorGUI.Popup(position, property.displayName, index, SceneList.ToArray());
        property.stringValue = SceneList[goalIndex];
    }

    private void LoadSceneList()
    {
        SceneList = new List<string> { string.Empty };

        foreach (var scene in EditorBuildSettings.scenes)
        {
            var scenePath = scene.path.Split('/');
            string sceneName = scenePath[scenePath.Length - 1];
            sceneName = sceneName.Remove(sceneName.Length - 6, 6);

            SceneList.Add(sceneName);
        }
    }
}

using UnityEditor;
using UnityEngine;

namespace TGame.SceneNavigator
{
    [CustomPropertyDrawer(typeof(ScenePathAttribute))]
    public class ScenePathDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(property.stringValue);
            EditorGUI.BeginChangeCheck();

            sceneAsset = EditorGUI.ObjectField(position, label, sceneAsset, typeof(SceneAsset), false) as SceneAsset;

            if (EditorGUI.EndChangeCheck())
            {
                var path = AssetDatabase.GetAssetPath(sceneAsset);
                property.stringValue = path;
            }
        }
    }
}

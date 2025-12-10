using UnityEditor;
using UnityEngine;

namespace IrakliChkuaseli.UI.Glimmer.Editor
{
    [CustomEditor(typeof(GlimmerElement))]
    public class GlimmerElementEditor : UnityEditor.Editor
    {
        private SerializedProperty _ignoreGlimmerProp;
        private SerializedProperty _overrideCornerRadiusProp;
        private SerializedProperty _cornerRadiusProp;

        private void OnEnable()
        {
            _ignoreGlimmerProp = serializedObject.FindProperty("ignoreGlimmer");
            _overrideCornerRadiusProp = serializedObject.FindProperty("overrideCornerRadius");
            _cornerRadiusProp = serializedObject.FindProperty("cornerRadius");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_ignoreGlimmerProp, new GUIContent("Ignore Glimmer"));

            using (new EditorGUI.DisabledScope(_ignoreGlimmerProp.boolValue))
            {
                EditorGUILayout.PropertyField(_overrideCornerRadiusProp, new GUIContent("Override Corner Radius"));

                if (_overrideCornerRadiusProp.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_cornerRadiusProp, new GUIContent("Corner Radius"));
                    EditorGUI.indentLevel--;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}

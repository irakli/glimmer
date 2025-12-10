using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IrakliChkuaseli.UI.Glimmer.Editor
{
    [CustomEditor(typeof(GlimmerGroup))]
    public class GlimmerGroupEditor : UnityEditor.Editor
    {
        private const string ShaderName = "UI/Glimmer/Shimmer";

        private SerializedProperty _targetGraphicsProp;
        private SerializedProperty _autoRefreshTargetsProp;
        private SerializedProperty _baseColorProp;
        private SerializedProperty _shimmerColorProp;
        private SerializedProperty _cornerRadiusProp;
        private SerializedProperty _shimmerDurationProp;
        private SerializedProperty _shimmerAngleProp;
        private SerializedProperty _shimmerWidthProp;
        private SerializedProperty _previewInEditorProp;

        private void OnEnable()
        {
            _targetGraphicsProp = serializedObject.FindProperty("targetGraphics");
            _autoRefreshTargetsProp = serializedObject.FindProperty("autoRefreshTargets");
            _baseColorProp = serializedObject.FindProperty("baseColor");
            _shimmerColorProp = serializedObject.FindProperty("shimmerColor");
            _cornerRadiusProp = serializedObject.FindProperty("cornerRadius");
            _shimmerDurationProp = serializedObject.FindProperty("shimmerDuration");
            _shimmerAngleProp = serializedObject.FindProperty("shimmerAngle");
            _shimmerWidthProp = serializedObject.FindProperty("shimmerWidth");
            _previewInEditorProp = serializedObject.FindProperty("previewInEditor");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var glimmerGroup = (GlimmerGroup)target;

            // Validation warnings (only show if there are issues)
            DrawValidationWarnings(glimmerGroup);

            // Preview toggle - compact inline control
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Preview");

            var isShowing = glimmerGroup.IsShowing;
            var buttonText = isShowing ? "● Showing" : "○ Hidden";
            var buttonStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fontStyle = isShowing ? FontStyle.Bold : FontStyle.Normal,
                fixedWidth = 80
            };

            var originalColor = GUI.backgroundColor;
            if (isShowing)
                GUI.backgroundColor = new Color(0.6f, 0.9f, 0.6f);

            if (GUILayout.Button(buttonText, buttonStyle))
            {
                glimmerGroup.Toggle();
                EditorUtility.SetDirty(target);
            }

            GUI.backgroundColor = originalColor;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            // Targets section
            EditorGUILayout.LabelField("Targets", EditorStyles.boldLabel);

            // Target list with count
            var targetCount = _targetGraphicsProp.arraySize;
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_targetGraphicsProp, new GUIContent($"Target Graphics ({targetCount})"), true);
            EditorGUILayout.PropertyField(_autoRefreshTargetsProp, new GUIContent("Auto-Refresh on Validate"));
            EditorGUI.indentLevel--;

            // Target management buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh Targets", GUILayout.Height(24)))
            {
                glimmerGroup.RefreshTargets();
            }
            if (GUILayout.Button("Clear All", GUILayout.Height(24)))
            {
                _targetGraphicsProp.ClearArray();
                serializedObject.ApplyModifiedProperties();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Appearance section
            EditorGUILayout.LabelField("Appearance", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_baseColorProp);
            EditorGUILayout.PropertyField(_shimmerColorProp);
            EditorGUILayout.PropertyField(_cornerRadiusProp);
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(5);

            // Animation section
            EditorGUILayout.LabelField("Animation", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_shimmerDurationProp, new GUIContent("Duration (seconds)"));
            EditorGUILayout.PropertyField(_shimmerAngleProp, new GUIContent("Angle (degrees)"));
            EditorGUILayout.PropertyField(_shimmerWidthProp, new GUIContent("Width"));
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(5);

            // Editor section
            EditorGUILayout.LabelField("Editor", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_previewInEditorProp, new GUIContent("Live Preview"));
            EditorGUI.indentLevel--;

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawValidationWarnings(GlimmerGroup glimmerGroup)
        {
            // Check shader availability
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                EditorGUILayout.HelpBox(
                    "Shimmer shader not found! Package may be corrupted.",
                    MessageType.Error);
                EditorGUILayout.Space(4);
                return;
            }

            // Check sibling order - GlimmerGroup must be AFTER all TMP_Text targets
            var maxTextSiblingIndex = GetMaxTextTargetSiblingIndex(glimmerGroup);
            if (maxTextSiblingIndex >= 0 && glimmerGroup.transform.GetSiblingIndex() <= maxTextSiblingIndex)
            {
                EditorGUILayout.HelpBox(
                    "GlimmerGroup must be after all text targets in the hierarchy. " +
                    "Unity UI renders siblings in order - text placeholders will be obscured.",
                    MessageType.Warning);

                if (GUILayout.Button("Move After Text Targets"))
                {
                    Undo.SetSiblingIndex(glimmerGroup.transform, maxTextSiblingIndex + 1, "Move GlimmerGroup");
                }

                EditorGUILayout.Space(4);
            }

            // Only show warnings, not info
            var count = _targetGraphicsProp.arraySize;
            if (count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No targets assigned. Click 'Refresh Targets' or add manually.",
                    MessageType.Warning);
                EditorGUILayout.Space(4);
                return;
            }

            // Count null references and ignored targets
            var nullCount = 0;
            var ignoredCount = 0;
            var radiusOverrideCount = 0;

            for (var i = 0; i < count; i++)
            {
                var graphicObj = _targetGraphicsProp.GetArrayElementAtIndex(i).objectReferenceValue;
                if (graphicObj == null)
                {
                    nullCount++;
                    continue;
                }

                var graphic = graphicObj as UnityEngine.UI.Graphic;
                if (graphic != null)
                {
                    var element = graphic.GetComponent<GlimmerElement>();
                    if (element != null)
                    {
                        if (element.IgnoreGlimmer) ignoredCount++;
                        if (element.HasCornerRadiusOverride) radiusOverrideCount++;
                    }
                }
            }

            if (nullCount > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{nullCount} missing reference(s). Click 'Refresh Targets' to fix.",
                    MessageType.Warning);
                EditorGUILayout.Space(4);
            }

            if (ignoredCount > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{ignoredCount} target(s) have 'Ignore Glimmer' enabled and will be skipped. " +
                    "Click 'Refresh Targets' to remove them from the list.",
                    MessageType.Info);
                EditorGUILayout.Space(4);
            }

            if (radiusOverrideCount > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{radiusOverrideCount} target(s) have corner radius overrides via GlimmerElement.",
                    MessageType.None);
                EditorGUILayout.Space(4);
            }
        }

        private int GetMaxTextTargetSiblingIndex(GlimmerGroup glimmerGroup)
        {
            var maxIndex = -1;
            var glimmerParent = glimmerGroup.transform.parent;

            foreach (var graphic in glimmerGroup.TargetGraphics)
            {
                if (graphic is TMP_Text && graphic.transform.parent == glimmerParent)
                {
                    var siblingIndex = graphic.transform.GetSiblingIndex();
                    if (siblingIndex > maxIndex)
                        maxIndex = siblingIndex;
                }
            }

            return maxIndex;
        }

        [MenuItem("GameObject/UI/Glimmer Group", false, 2100)]
        private static void CreateGlimmerGroup(MenuCommand command)
        {
            var go = ObjectFactory.CreateGameObject("Glimmer Group",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(GlimmerGroup));

            StageUtility.PlaceGameObjectInCurrentStage(go);
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);

            var contextObject = command.context as GameObject;
            if (contextObject != null)
            {
                GameObjectUtility.SetParentAndAlign(go, contextObject);
                Undo.SetTransformParent(go.transform, contextObject.transform, "Parent " + go.name);

                // Move to last sibling position by default
                go.transform.SetAsLastSibling();
            }

            Selection.activeGameObject = go;
        }
    }
}

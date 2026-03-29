using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NestedSplineGraphAsset))]
public sealed class NestedSplineGraphAssetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty rootLayerProperty = serializedObject.FindProperty("rootLayer");
        DrawLayer(rootLayerProperty, "Root Layer", alwaysExpanded: true, forceSegmentsExpanded: true, depth: 0);

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawLayer(
        SerializedProperty layerProperty,
        string label,
        bool alwaysExpanded,
        bool forceSegmentsExpanded,
        int depth)
    {
        if (layerProperty == null)
        {
            return;
        }

        if (alwaysExpanded)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        }
        else
        {
            layerProperty.isExpanded = EditorGUILayout.Foldout(layerProperty.isExpanded, label, true);
            if (!layerProperty.isExpanded)
            {
                return;
            }
        }

        EditorGUI.indentLevel++;

        EditorGUILayout.PropertyField(layerProperty.FindPropertyRelative("axis"));
        EditorGUILayout.PropertyField(layerProperty.FindPropertyRelative("rootSpline"));

        SerializedProperty segmentsProperty = layerProperty.FindPropertyRelative("segments");
        if (segmentsProperty != null && depth < 2)
        {
            DrawSegments(segmentsProperty, forceSegmentsExpanded, depth);
        }

        EditorGUI.indentLevel--;
    }

    private static void DrawSegments(SerializedProperty segmentsProperty, bool forceExpanded, int depth)
    {
        if (segmentsProperty == null)
        {
            return;
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Segments", EditorStyles.boldLabel);

        int newSize = EditorGUILayout.IntField("Count", segmentsProperty.arraySize);
        segmentsProperty.arraySize = Mathf.Max(0, newSize);
        int deleteIndex = -1;

        for (int i = 0; i < segmentsProperty.arraySize; i++)
        {
            SerializedProperty segmentProperty = segmentsProperty.GetArrayElementAtIndex(i);
            if (forceExpanded)
            {
                DrawSegmentBody(segmentProperty, i, depth, ref deleteIndex);
            }
            else
            {
                segmentProperty.isExpanded = EditorGUILayout.Foldout(segmentProperty.isExpanded, $"Segment {i}", true);
                if (segmentProperty.isExpanded)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    DrawSegmentBody(segmentProperty, i, depth, ref deleteIndex);
                    EditorGUILayout.EndVertical();
                }
            }
        }

        if (deleteIndex >= 0 && deleteIndex < segmentsProperty.arraySize)
        {
            segmentsProperty.DeleteArrayElementAtIndex(deleteIndex);
        }

        if (GUILayout.Button("Add Segment"))
        {
            int insertIndex = segmentsProperty.arraySize;
            segmentsProperty.InsertArrayElementAtIndex(insertIndex);
            SerializedProperty insertedSegment = segmentsProperty.GetArrayElementAtIndex(insertIndex);
            SerializedProperty endInputProperty = insertedSegment.FindPropertyRelative("endInput");
            endInputProperty.floatValue = 1f;

            SerializedProperty useChildLayerProperty = insertedSegment.FindPropertyRelative("useChildLayer");
            if (useChildLayerProperty != null)
            {
                useChildLayerProperty.boolValue = false;
            }
        }
    }

    private static void DrawSegmentBody(SerializedProperty segmentProperty, int index, int depth, ref int deleteIndex)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Segment {index}", EditorStyles.boldLabel);
        if (GUILayout.Button("Remove", GUILayout.Width(70f)))
        {
            deleteIndex = index;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.PropertyField(segmentProperty.FindPropertyRelative("endInput"), new GUIContent("End Input"));

        SerializedProperty useChildLayerProperty = segmentProperty.FindPropertyRelative("useChildLayer");
        SerializedProperty childLayerProperty = segmentProperty.FindPropertyRelative("childLayer");
        if (useChildLayerProperty != null && childLayerProperty != null)
        {
            EditorGUILayout.PropertyField(useChildLayerProperty, new GUIContent("Use Child Layer"));
            if (useChildLayerProperty.boolValue && depth < 2)
            {
                DrawLayer(childLayerProperty, $"Child Layer {depth + 2}", alwaysExpanded: false, forceSegmentsExpanded: false, depth: depth + 1);
            }
        }

        EditorGUILayout.EndVertical();
    }
}

using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ContFilterAsset))]
public sealed class ContFilterAssetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "Fixed Points are reapplied to the curve. You can add points below, and those positions will stay locked to their configured input/output values.",
            MessageType.Info);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("curve"));
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("fixedPoints"), true);
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("bakeResolution"));

        bool changed = serializedObject.ApplyModifiedProperties();
        if (changed)
        {
            ApplyFixedPoints();
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Apply Fixed Points"))
        {
            ApplyFixedPoints();
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(((ContFilterAsset)target).BakedSummary, MessageType.Info);
        if (GUILayout.Button("Bake Filter LUT"))
        {
            Bake();
        }
    }

    private void ApplyFixedPoints()
    {
        ContFilterAsset asset = (ContFilterAsset)target;
        Undo.RecordObject(asset, "Apply Cont Filter Fixed Points");
        asset.SyncFixedPointsToCurve();
        EditorUtility.SetDirty(asset);
    }

    private void Bake()
    {
        ContFilterAsset asset = (ContFilterAsset)target;
        Undo.RecordObject(asset, "Bake Cont Filter LUT");
        asset.Bake();
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
    }
}

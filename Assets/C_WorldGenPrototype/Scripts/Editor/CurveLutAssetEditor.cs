using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CurveLutAsset), true)]
public sealed class CurveLutAssetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "Common curve LUT asset. Input is sampled across -1..1, fixed points are reapplied to the curve, and the baked LUT can be reused for filters or direct value mapping.",
            MessageType.Info);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("curve"));
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("fixedPoints"), true);
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("outputMin"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("outputMax"));
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
        EditorGUILayout.HelpBox(((CurveLutAsset)target).BakedSummary, MessageType.Info);
        if (GUILayout.Button("Bake Curve LUT"))
        {
            Bake();
        }
    }

    private void ApplyFixedPoints()
    {
        CurveLutAsset asset = (CurveLutAsset)target;
        Undo.RecordObject(asset, "Apply Curve LUT Fixed Points");
        asset.SyncFixedPointsToCurve();
        EditorUtility.SetDirty(asset);
    }

    private void Bake()
    {
        CurveLutAsset asset = (CurveLutAsset)target;
        Undo.RecordObject(asset, "Bake Curve LUT");
        asset.Bake();
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
    }
}

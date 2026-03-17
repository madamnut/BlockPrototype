using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SplineTreeAsset))]
public sealed class SplineTreeAssetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        SplineTreeAsset asset = (SplineTreeAsset)target;
        EditorGUILayout.HelpBox("Spline trees are edited only in the Spline Tree Graph window.", MessageType.Info);

        if (GUILayout.Button("Open Node Graph"))
        {
            SplineTreeGraphWindow.Open(asset);
        }
    }
}

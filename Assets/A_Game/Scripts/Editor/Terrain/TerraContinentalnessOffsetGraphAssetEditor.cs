#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TerraContinentalnessOffsetGraphAsset))]
public sealed class TerraContinentalnessOffsetGraphAssetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (GUILayout.Button("Open Graph Window", GUILayout.Height(32f)))
        {
            TerraContinentalnessOffsetGraphWindow.Open((TerraContinentalnessOffsetGraphAsset)target);
        }
    }
}
#endif

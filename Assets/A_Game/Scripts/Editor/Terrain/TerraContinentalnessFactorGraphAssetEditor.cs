#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TerraContinentalnessFactorGraphAsset))]
public sealed class TerraContinentalnessFactorGraphAssetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (GUILayout.Button("Open Graph Window", GUILayout.Height(32f)))
        {
            TerraContinentalnessFactorGraphWindow.Open((TerraContinentalnessFactorGraphAsset)target);
        }
    }
}
#endif

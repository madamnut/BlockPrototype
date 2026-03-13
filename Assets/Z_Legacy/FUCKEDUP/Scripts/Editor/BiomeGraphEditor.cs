#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BiomeGraph))]
public sealed class BiomeGraphEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "BiomeGraph editing is handled in the dedicated window.\n" +
            "Open the graph editor to place biome points, move them, and bake a Voronoi preview.",
            MessageType.Info);

        DrawDefaultInspector();

        EditorGUILayout.Space(10f);

        if (GUILayout.Button("Open Graph Editor", GUILayout.Height(32f)))
        {
            BiomeGraphWindow.Open((BiomeGraph)target);
        }
    }
}
#endif

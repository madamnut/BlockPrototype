#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VoxelBlockAuthoringDatabase))]
public sealed class VoxelBlockAuthoringDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(12f);

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Bake Block Assets", GUILayout.Height(30f)))
            {
                VoxelBlockAssetBaker.Bake((VoxelBlockAuthoringDatabase)target);
            }
        }
    }
}
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VoxelBlockAuthoringDatabase))]
public sealed class VoxelBlockAuthoringDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "Texture naming rules:\n" +
            "- AllSame: BlockName_All or BlockName\n" +
            "- TopBottomSide: BlockName_Top, BlockName_Bottom, BlockName_Side\n" +
            "- TopAndSide: BlockName_Top, BlockName_Side\n" +
            "- Individual: BlockName_Top, BlockName_Bottom, BlockName_Front, BlockName_Back, BlockName_Left, BlockName_Right\n\n" +
            "Bake automatically detects the layout from the texture names in the texture root folder.",
            MessageType.Info);

        EditorGUILayout.Space(8f);
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

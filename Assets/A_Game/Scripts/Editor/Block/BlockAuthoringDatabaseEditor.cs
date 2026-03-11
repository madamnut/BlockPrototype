#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BlockAuthoringDatabase))]
public sealed class BlockAuthoringDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "Texture naming rules:\n" +
            "- Kind = Block: BlockName_All or BlockName, BlockName_Top/Bottom/Side, BlockName_TopBottom/Side, BlockName_Top/Side, or full BlockName_Top/Bottom/Front/Back/Left/Right\n" +
            "- Kind = Foliage: BlockName\n\n" +
            "Bake uses the Kind selected on each entry, then resolves textures from the texture root folder.",
            MessageType.Info);

        EditorGUILayout.Space(8f);
        DrawDefaultInspector();

        EditorGUILayout.Space(12f);

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Bake Block Assets", GUILayout.Height(30f)))
            {
                BlockAssetBaker.Bake((BlockAuthoringDatabase)target);
            }
        }
    }
}
#endif

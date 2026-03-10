#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(VoxelBlockAuthoringEntry))]
public sealed class VoxelBlockAuthoringEntryDrawer : PropertyDrawer
{
    private const float VerticalSpacing = 2f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        const int lineCount = 1;
        return (lineCount * EditorGUIUtility.singleLineHeight) + ((lineCount - 1) * VerticalSpacing);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty blockId = property.FindPropertyRelative("blockId");
        SerializedProperty blockName = property.FindPropertyRelative("blockName");

        string entryName = string.IsNullOrWhiteSpace(blockName.stringValue) ? label.text : $"{label.text} - {blockName.stringValue}";
        Rect contentRect = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), new GUIContent(entryName));
        int previousIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        float lineHeight = EditorGUIUtility.singleLineHeight;
        float y = contentRect.y;

        Rect row = new(contentRect.x, y, contentRect.width, lineHeight);
        float idWidth = Mathf.Min(90f, contentRect.width * 0.3f);
        Rect idRect = new(row.x, row.y, idWidth, row.height);
        Rect nameRect = new(row.x + idWidth + 6f, row.y, contentRect.width - idWidth - 6f, row.height);
        EditorGUI.PropertyField(idRect, blockId, GUIContent.none);
        EditorGUI.PropertyField(nameRect, blockName, GUIContent.none);

        EditorGUI.indentLevel = previousIndent;
        EditorGUI.EndProperty();
    }
}
#endif

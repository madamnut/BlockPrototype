#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed class TerraContinentalnessFactorGraphWindow : EditorWindow
{
    private const float PointHandleRadius = 6f;
    private const float GraphFrameMarginLeft = 56f;
    private const float GraphFrameMarginRight = 28f;
    private const float GraphPaddingLeft = 18f;
    private const float GraphPaddingRight = 22f;
    private const float GraphPaddingTop = 12f;
    private const float GraphPaddingBottom = 18f;

    private TerraContinentalnessFactorGraphAsset _asset;
    private SerializedObject _serializedObject;
    private SerializedProperty _pointsProperty;
    private int _draggingPointIndex = -1;

    public static void Open(TerraContinentalnessFactorGraphAsset asset)
    {
        TerraContinentalnessFactorGraphWindow window = GetWindow<TerraContinentalnessFactorGraphWindow>("Terra Factor Graph");
        window.minSize = new Vector2(640f, 520f);
        window.SetAsset(asset);
        window.Show();
        window.Focus();
    }

    private void OnEnable()
    {
        if (_asset == null && Selection.activeObject is TerraContinentalnessFactorGraphAsset selectedAsset)
        {
            SetAsset(selectedAsset);
        }
    }

    private void OnSelectionChange()
    {
        if (Selection.activeObject is TerraContinentalnessFactorGraphAsset selectedAsset)
        {
            SetAsset(selectedAsset);
            Repaint();
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6f);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Terra Continentalness Factor Graph", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (_asset != null && GUILayout.Button("Ping Asset", GUILayout.Width(90f)))
            {
                EditorGUIUtility.PingObject(_asset);
            }
        }

        EditorGUILayout.Space(4f);

        if (_asset == null)
        {
            EditorGUILayout.HelpBox("Select a TerraContinentalnessFactorGraph asset or open the window from the asset inspector.", MessageType.Info);
            return;
        }

        EnsureSerializedBindings();
        _serializedObject.Update();

        EditorGUILayout.Space(4f);

        DrawGraph();
        EditorGUILayout.Space(8f);
        DrawPointList();

        if (_serializedObject.ApplyModifiedProperties())
        {
            _asset.MarkCurveDirty();
            EditorUtility.SetDirty(_asset);
        }
    }

    private void SetAsset(TerraContinentalnessFactorGraphAsset asset)
    {
        _asset = asset;
        _serializedObject = null;
        _pointsProperty = null;
        _draggingPointIndex = -1;
    }

    private void EnsureSerializedBindings()
    {
        if (_asset == null)
        {
            return;
        }

        if (_serializedObject != null && _serializedObject.targetObject == _asset)
        {
            return;
        }

        _serializedObject = new SerializedObject(_asset);
        _pointsProperty = _serializedObject.FindProperty("points");
    }

    private void DrawGraph()
    {
        Rect totalRect = GUILayoutUtility.GetRect(10f, 300f, GUILayout.ExpandWidth(true));
        Rect frameRect = new(
            totalRect.x + GraphFrameMarginLeft,
            totalRect.y,
            totalRect.width - (GraphFrameMarginLeft + GraphFrameMarginRight),
            250f);
        Rect graphRect = new(
            frameRect.x + GraphPaddingLeft,
            frameRect.y + GraphPaddingTop,
            frameRect.width - (GraphPaddingLeft + GraphPaddingRight),
            frameRect.height - (GraphPaddingTop + GraphPaddingBottom));

        float minY = GetMinPointY() - 0.2f;
        float maxY = GetMaxPointY() + 0.2f;
        if (Mathf.Approximately(minY, maxY))
        {
            minY -= 0.5f;
            maxY += 0.5f;
        }

        EditorGUI.DrawRect(frameRect, new Color(0.11f, 0.11f, 0.12f, 1f));
        DrawGrid(graphRect, minY, maxY);
        DrawAxis(graphRect, minY, maxY);
        DrawCurve(graphRect, minY, maxY);
        DrawHandles(graphRect, minY, maxY);
        HandleGraphInput(graphRect, minY, maxY);
    }

    private void DrawCurve(Rect graphRect, float minY, float maxY)
    {
        if (_pointsProperty.arraySize < 2)
        {
            return;
        }

        AnimationCurve curve = _asset.GetPreviewCurve();
        Handles.color = new Color(0.45f, 0.88f, 0.95f, 1f);

        const int segments = 96;
        Vector3[] line = new Vector3[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float x = Mathf.Lerp(-1f, 1f, t);
            float y = curve.Evaluate(x);
            line[i] = ToGraphPosition(graphRect, x, y, minY, maxY);
        }

        Handles.DrawAAPolyLine(2f, line);
    }

    private void DrawHandles(Rect graphRect, float minY, float maxY)
    {
        for (int i = 0; i < _pointsProperty.arraySize; i++)
        {
            SerializedProperty pointProperty = _pointsProperty.GetArrayElementAtIndex(i);
            float x = pointProperty.FindPropertyRelative("x").floatValue;
            float y = pointProperty.FindPropertyRelative("y").floatValue;
            Vector3 position = ToGraphPosition(graphRect, x, y, minY, maxY);

            Handles.color = i == _draggingPointIndex
                ? new Color(1f, 0.5f, 0.25f, 1f)
                : new Color(0.92f, 0.92f, 0.92f, 1f);
            Handles.DrawSolidDisc(position, Vector3.forward, PointHandleRadius);
        }
    }

    private void HandleGraphInput(Rect graphRect, float minY, float maxY)
    {
        Event current = Event.current;
        int controlId = GUIUtility.GetControlID(FocusType.Passive);

        switch (current.GetTypeForControl(controlId))
        {
            case EventType.MouseDown:
                if (current.button != 0 || !graphRect.Contains(current.mousePosition))
                {
                    break;
                }

                _draggingPointIndex = FindPointAtPosition(graphRect, current.mousePosition, minY, maxY);
                if (_draggingPointIndex >= 0)
                {
                    GUIUtility.hotControl = controlId;
                    current.Use();
                }
                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl != controlId || _draggingPointIndex < 0)
                {
                    break;
                }

                SerializedProperty pointProperty = _pointsProperty.GetArrayElementAtIndex(_draggingPointIndex);
                SerializedProperty yProperty = pointProperty.FindPropertyRelative("y");
                yProperty.floatValue = Mathf.Max(0.05f, FromGraphY(graphRect, current.mousePosition.y, minY, maxY));
                _serializedObject.ApplyModifiedProperties();
                _asset.MarkCurveDirty();
                EditorUtility.SetDirty(_asset);
                Repaint();
                current.Use();
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl == controlId)
                {
                    GUIUtility.hotControl = 0;
                    _draggingPointIndex = -1;
                    current.Use();
                }
                break;
        }
    }

    private void DrawPointList()
    {
        EditorGUILayout.LabelField("Control Points", EditorStyles.boldLabel);

        for (int i = 0; i < _pointsProperty.arraySize; i++)
        {
            SerializedProperty pointProperty = _pointsProperty.GetArrayElementAtIndex(i);
            SerializedProperty xProperty = pointProperty.FindPropertyRelative("x");
            SerializedProperty yProperty = pointProperty.FindPropertyRelative("y");

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("X", GUILayout.Width(22f)))
            {
                _pointsProperty.DeleteArrayElementAtIndex(i);
                break;
            }

            EditorGUILayout.LabelField($"Point {i}", GUILayout.Width(44f));
            EditorGUILayout.LabelField("X", GUILayout.Width(14f));
            xProperty.floatValue = Mathf.Clamp(EditorGUILayout.FloatField(xProperty.floatValue, GUILayout.Width(90f)), -1f, 1f);
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Y", GUILayout.Width(14f));
            yProperty.floatValue = Mathf.Max(0.05f, EditorGUILayout.FloatField(yProperty.floatValue, GUILayout.Width(90f)));
            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Add Point", GUILayout.Width(100f)))
        {
            int index = _pointsProperty.arraySize;
            _pointsProperty.InsertArrayElementAtIndex(index);
            SerializedProperty newPoint = _pointsProperty.GetArrayElementAtIndex(index);
            newPoint.FindPropertyRelative("x").floatValue = 0f;
            newPoint.FindPropertyRelative("y").floatValue = 1f;
        }
        EditorGUILayout.EndHorizontal();
    }

    private int FindPointAtPosition(Rect graphRect, Vector2 mousePosition, float minY, float maxY)
    {
        float bestDistance = PointHandleRadius + 4f;
        int bestIndex = -1;

        for (int i = 0; i < _pointsProperty.arraySize; i++)
        {
            SerializedProperty pointProperty = _pointsProperty.GetArrayElementAtIndex(i);
            float x = pointProperty.FindPropertyRelative("x").floatValue;
            float y = pointProperty.FindPropertyRelative("y").floatValue;
            Vector3 position = ToGraphPosition(graphRect, x, y, minY, maxY);
            float distance = Vector2.Distance(mousePosition, position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private float GetMinPointY()
    {
        if (_pointsProperty.arraySize == 0)
        {
            return 0.5f;
        }

        float min = float.PositiveInfinity;
        for (int i = 0; i < _pointsProperty.arraySize; i++)
        {
            float y = _pointsProperty.GetArrayElementAtIndex(i).FindPropertyRelative("y").floatValue;
            min = Mathf.Min(min, y);
        }

        return min;
    }

    private float GetMaxPointY()
    {
        if (_pointsProperty.arraySize == 0)
        {
            return 1.5f;
        }

        float max = float.NegativeInfinity;
        for (int i = 0; i < _pointsProperty.arraySize; i++)
        {
            float y = _pointsProperty.GetArrayElementAtIndex(i).FindPropertyRelative("y").floatValue;
            max = Mathf.Max(max, y);
        }

        return max;
    }

    private static Vector3 ToGraphPosition(Rect graphRect, float x, float y, float minY, float maxY)
    {
        float graphX = Mathf.Lerp(graphRect.xMin, graphRect.xMax, Mathf.InverseLerp(-1f, 1f, x));
        float graphY = Mathf.Lerp(graphRect.yMax, graphRect.yMin, Mathf.InverseLerp(minY, maxY, y));
        return new Vector3(graphX, graphY, 0f);
    }

    private static float FromGraphY(Rect graphRect, float mouseY, float minY, float maxY)
    {
        float t = Mathf.InverseLerp(graphRect.yMax, graphRect.yMin, mouseY);
        return Mathf.Lerp(minY, maxY, t);
    }

    private static void DrawGrid(Rect rect, float minY, float maxY)
    {
        Handles.color = new Color(1f, 1f, 1f, 0.06f);

        for (int i = 1; i < 8; i++)
        {
            float t = i / 8f;
            float x = Mathf.Lerp(rect.xMin, rect.xMax, t);
            Handles.DrawLine(new Vector3(x, rect.yMin), new Vector3(x, rect.yMax));
        }

        for (int i = 1; i < 6; i++)
        {
            float t = i / 6f;
            float y = Mathf.Lerp(rect.yMin, rect.yMax, t);
            Handles.DrawLine(new Vector3(rect.xMin, y), new Vector3(rect.xMax, y));
        }

        GUIStyle yTickStyle = new(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleRight,
            normal = { textColor = new Color(0.8f, 0.8f, 0.8f, 0.75f) }
        };

        for (int i = 0; i <= 4; i++)
        {
            float t = i / 4f;
            float yValue = Mathf.Lerp(maxY, minY, t);
            float y = Mathf.Lerp(rect.yMin, rect.yMax, t);
            Rect labelRect = new(rect.xMin - 52f, y - 8f, 46f, 16f);
            EditorGUI.LabelField(labelRect, yValue.ToString("0.##"), yTickStyle);
        }
    }

    private static void DrawAxis(Rect rect, float minY, float maxY)
    {
        Handles.color = new Color(1f, 1f, 1f, 0.35f);
        float zeroX = Mathf.Lerp(rect.xMin, rect.xMax, Mathf.InverseLerp(-1f, 1f, 0f));
        Handles.DrawLine(new Vector3(zeroX, rect.yMin), new Vector3(zeroX, rect.yMax));

        if (minY <= 0f && maxY >= 0f)
        {
            float zeroY = Mathf.Lerp(rect.yMax, rect.yMin, Mathf.InverseLerp(minY, maxY, 0f));
            Handles.DrawLine(new Vector3(rect.xMin, zeroY), new Vector3(rect.xMax, zeroY));
        }

        GUIStyle xTickStyle = new(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.UpperCenter,
            normal = { textColor = new Color(0.9f, 0.9f, 0.9f, 0.9f) }
        };

        for (int i = 0; i <= 4; i++)
        {
            float xValue = Mathf.Lerp(-1f, 1f, i / 4f);
            float x = Mathf.Lerp(rect.xMin, rect.xMax, i / 4f);
            Rect labelRect = new(x - 18f, rect.yMax - 18f, 36f, 16f);
            EditorGUI.LabelField(labelRect, xValue.ToString("0.0"), xTickStyle);
        }
    }
}
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed class BiomeGraphWindow : EditorWindow
{
    private const float CanvasPadding = 16f;
    private const float SidebarWidth = 280f;
    private const float PointPickRadius = 10f;
    private const int PreviewResolution = 256;

    private BiomeGraph _graph;
    private SerializedObject _serializedObject;
    private SerializedProperty _entriesProperty;
    private SerializedProperty _xAxisLabelProperty;
    private SerializedProperty _yAxisLabelProperty;
    private SerializedProperty _graphBackgroundColorProperty;

    private int _selectedEntryIndex = -1;
    private int _draggingEntryIndex = -1;
    private Vector2 _mouseDownPosition;
    private bool _dragStarted;
    private Texture2D _previewTexture;
    private bool _previewDirty = true;

    public static void Open(BiomeGraph graph)
    {
        BiomeGraphWindow window = GetWindow<BiomeGraphWindow>("Biome Graph");
        window.SetGraph(graph);
    }

    private void OnDisable()
    {
        if (_previewTexture != null)
        {
            DestroyImmediate(_previewTexture);
            _previewTexture = null;
        }
    }

    private void OnGUI()
    {
        DrawToolbar();

        if (_graph == null)
        {
            EditorGUILayout.HelpBox("Select a BiomeGraph asset and open this window from the inspector.", MessageType.Info);
            return;
        }

        HandleKeyboardInput();
        _serializedObject.Update();

        Rect fullRect = new(0f, 40f, position.width, position.height - 40f);
        float availableCanvasWidth = Mathf.Max(240f, fullRect.width - SidebarWidth - (CanvasPadding * 3f));
        float availableCanvasHeight = Mathf.Max(240f, fullRect.height - (CanvasPadding * 2f));
        float canvasSize = Mathf.Min(availableCanvasWidth, availableCanvasHeight);
        Rect canvasRect = new(
            CanvasPadding,
            fullRect.y + CanvasPadding + ((availableCanvasHeight - canvasSize) * 0.5f),
            canvasSize,
            canvasSize);
        Rect sidebarRect = new(
            canvasRect.xMax + CanvasPadding,
            fullRect.y + CanvasPadding,
            SidebarWidth,
            availableCanvasHeight);

        DrawCanvas(canvasRect);
        DrawSidebar(sidebarRect);

        _serializedObject.ApplyModifiedProperties();
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("Voronoi Biome Editor", EditorStyles.toolbarButton);
            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(_graph == null))
            {
                if (GUILayout.Button("Bake Preview", EditorStyles.toolbarButton))
                {
                    BakePreview();
                }

                if (GUILayout.Button("Clear Points", EditorStyles.toolbarButton))
                {
                    Undo.RecordObject(_graph, "Clear Biome Points");
                    _entriesProperty.ClearArray();
                    _selectedEntryIndex = -1;
                    _serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(_graph);
                    _previewDirty = true;
                }
            }
        }
    }

    private void DrawCanvas(Rect rect)
    {
        EditorGUI.DrawRect(rect, _graphBackgroundColorProperty.colorValue);
        DrawGrid(rect);

        if (_previewTexture != null)
        {
            GUI.DrawTexture(rect, _previewTexture, ScaleMode.StretchToFill, false);
        }

        DrawPoints(rect);
        DrawAxes(rect);
        HandleCanvasInput(rect);
    }

    private void DrawSidebar(Rect rect)
    {
        GUILayout.BeginArea(rect, EditorStyles.helpBox);
        EditorGUILayout.LabelField("Point UX", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "1. Empty space click: add biome point\n" +
            "2. Point drag: move point\n" +
            "3. Bake Preview: build Voronoi regions\n" +
            "4. Delete: remove selected point",
            MessageType.None);

        if (_previewDirty)
        {
            EditorGUILayout.HelpBox("Preview is dirty. Click Bake Preview to refresh Voronoi regions.", MessageType.Info);
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField($"Biome Points: {_entriesProperty.arraySize}");

        EditorGUILayout.Space(8f);
        if (_selectedEntryIndex >= 0 && _selectedEntryIndex < _entriesProperty.arraySize)
        {
            SerializedProperty entry = _entriesProperty.GetArrayElementAtIndex(_selectedEntryIndex);
            EditorGUILayout.LabelField($"Selected Biome: {_selectedEntryIndex}", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("biomeName"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("color"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("climatePoint"));
        }

        EditorGUILayout.Space(10f);
        EditorGUILayout.PropertyField(_xAxisLabelProperty);
        EditorGUILayout.PropertyField(_yAxisLabelProperty);
        EditorGUILayout.PropertyField(_graphBackgroundColorProperty);
        GUILayout.EndArea();
    }

    private void DrawGrid(Rect rect)
    {
        Color gridColor = new(0f, 0f, 0f, 0.12f);
        for (int step = 1; step < 4; step++)
        {
            float t = step / 4f;
            float x = Mathf.Lerp(rect.xMin, rect.xMax, t);
            float y = Mathf.Lerp(rect.yMax, rect.yMin, t);
            EditorGUI.DrawRect(new Rect(x, rect.yMin, 1f, rect.height), gridColor);
            EditorGUI.DrawRect(new Rect(rect.xMin, y, rect.width, 1f), gridColor);
        }

        Handles.color = new Color(0f, 0f, 0f, 0.35f);
        Handles.DrawSolidRectangleWithOutline(rect, Color.clear, Handles.color);
    }

    private void DrawPoints(Rect rect)
    {
        GUIStyle labelStyle = new(EditorStyles.whiteMiniLabel)
        {
            alignment = TextAnchor.MiddleCenter
        };

        for (int index = 0; index < _entriesProperty.arraySize; index++)
        {
            SerializedProperty entry = _entriesProperty.GetArrayElementAtIndex(index);
            Vector2 point = BiomeGraph.ClampPoint(entry.FindPropertyRelative("climatePoint").vector2Value);
            Vector2 canvasPoint = CanvasPointFromGraph(rect, point);
            Color color = entry.FindPropertyRelative("color").colorValue;

            if (index == _selectedEntryIndex)
            {
                EditorGUI.DrawRect(new Rect(canvasPoint.x - 8f, canvasPoint.y - 8f, 16f, 16f), Color.white);
            }

            Rect outlineRect = new(canvasPoint.x - 6f, canvasPoint.y - 6f, 12f, 12f);
            Rect fillRect = new(canvasPoint.x - 4f, canvasPoint.y - 4f, 8f, 8f);
            EditorGUI.DrawRect(outlineRect, Color.black);
            EditorGUI.DrawRect(fillRect, color);

            string biomeName = entry.FindPropertyRelative("biomeName").stringValue;
            if (!string.IsNullOrWhiteSpace(biomeName))
            {
                Rect labelRect = new(canvasPoint.x - 50f, canvasPoint.y - 24f, 100f, 18f);
                EditorGUI.DropShadowLabel(labelRect, biomeName, labelStyle);
            }
        }
    }

    private void DrawAxes(Rect rect)
    {
        GUIStyle axisStyle = new(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter
        };

        EditorGUI.LabelField(new Rect(rect.xMin, rect.yMax + 4f, rect.width, 18f), _xAxisLabelProperty.stringValue, axisStyle);

        Rect yRect = new(rect.xMin - 44f, rect.yMin, 40f, rect.height);
        Matrix4x4 previousMatrix = GUI.matrix;
        GUIUtility.RotateAroundPivot(-90f, yRect.center);
        EditorGUI.LabelField(yRect, _yAxisLabelProperty.stringValue, axisStyle);
        GUI.matrix = previousMatrix;
    }

    private void HandleCanvasInput(Rect rect)
    {
        Event currentEvent = Event.current;
        if (!rect.Contains(currentEvent.mousePosition))
        {
            if (currentEvent.type == EventType.MouseUp)
            {
                _draggingEntryIndex = -1;
                _dragStarted = false;
            }
            return;
        }

        switch (currentEvent.type)
        {
            case EventType.MouseDown when currentEvent.button == 0:
                _mouseDownPosition = currentEvent.mousePosition;
                _dragStarted = false;
                if (TryFindEntry(rect, currentEvent.mousePosition, out int entryIndex))
                {
                    _selectedEntryIndex = entryIndex;
                    _draggingEntryIndex = entryIndex;
                }
                else
                {
                    _selectedEntryIndex = CreateEntry(GraphPointFromCanvas(rect, currentEvent.mousePosition));
                    _draggingEntryIndex = _selectedEntryIndex;
                }

                currentEvent.Use();
                break;

            case EventType.MouseDrag when currentEvent.button == 0:
                if (_draggingEntryIndex >= 0)
                {
                    if (!_dragStarted && (currentEvent.mousePosition - _mouseDownPosition).sqrMagnitude > 16f)
                    {
                        _dragStarted = true;
                    }

                    MoveEntry(_draggingEntryIndex, GraphPointFromCanvas(rect, currentEvent.mousePosition));
                    currentEvent.Use();
                }
                break;

            case EventType.MouseUp when currentEvent.button == 0:
                _draggingEntryIndex = -1;
                _dragStarted = false;
                currentEvent.Use();
                break;
        }
    }

    private void HandleKeyboardInput()
    {
        Event currentEvent = Event.current;
        if (currentEvent.type != EventType.KeyDown || _graph == null)
        {
            return;
        }

        if ((currentEvent.keyCode == KeyCode.Delete || currentEvent.keyCode == KeyCode.Backspace) &&
            _selectedEntryIndex >= 0 &&
            _selectedEntryIndex < _entriesProperty.arraySize)
        {
            Undo.RecordObject(_graph, "Delete Biome Point");
            _entriesProperty.DeleteArrayElementAtIndex(_selectedEntryIndex);
            _selectedEntryIndex = -1;
            _serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(_graph);
            _previewDirty = true;
            currentEvent.Use();
        }
    }

    private bool TryFindEntry(Rect rect, Vector2 mousePosition, out int entryIndex)
    {
        float pickRadiusSquared = PointPickRadius * PointPickRadius;
        for (int index = 0; index < _entriesProperty.arraySize; index++)
        {
            SerializedProperty entry = _entriesProperty.GetArrayElementAtIndex(index);
            Vector2 canvasPoint = CanvasPointFromGraph(rect, entry.FindPropertyRelative("climatePoint").vector2Value);
            if ((mousePosition - canvasPoint).sqrMagnitude <= pickRadiusSquared)
            {
                entryIndex = index;
                return true;
            }
        }

        entryIndex = -1;
        return false;
    }

    private int CreateEntry(Vector2 graphPoint)
    {
        Undo.RecordObject(_graph, "Create Biome Point");
        int insertIndex = _entriesProperty.arraySize;
        _entriesProperty.InsertArrayElementAtIndex(insertIndex);
        SerializedProperty entry = _entriesProperty.GetArrayElementAtIndex(insertIndex);
        entry.FindPropertyRelative("biomeName").stringValue = $"Biome {insertIndex + 1}";
        entry.FindPropertyRelative("color").colorValue = Random.ColorHSV(0f, 1f, 0.5f, 0.9f, 0.6f, 0.95f);
        entry.FindPropertyRelative("climatePoint").vector2Value = BiomeGraph.ClampPoint(graphPoint);
        _serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(_graph);
        _previewDirty = true;
        return insertIndex;
    }

    private void MoveEntry(int entryIndex, Vector2 graphPoint)
    {
        if (entryIndex < 0 || entryIndex >= _entriesProperty.arraySize)
        {
            return;
        }

        Undo.RecordObject(_graph, "Move Biome Point");
        SerializedProperty entry = _entriesProperty.GetArrayElementAtIndex(entryIndex);
        entry.FindPropertyRelative("climatePoint").vector2Value = BiomeGraph.ClampPoint(graphPoint);
        _serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(_graph);
        _previewDirty = true;
        Repaint();
    }

    private void BakePreview()
    {
        if (_previewTexture == null || _previewTexture.width != PreviewResolution || _previewTexture.height != PreviewResolution)
        {
            if (_previewTexture != null)
            {
                DestroyImmediate(_previewTexture);
            }

            _previewTexture = new Texture2D(PreviewResolution, PreviewResolution, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
        }

        Color background = _graphBackgroundColorProperty.colorValue;
        Color[] pixels = new Color[PreviewResolution * PreviewResolution];

        for (int y = 0; y < PreviewResolution; y++)
        {
            float humidity = 1f - (y / (float)(PreviewResolution - 1));
            for (int x = 0; x < PreviewResolution; x++)
            {
                float temperature = x / (float)(PreviewResolution - 1);
                Color color = background;
                if (_graph.TryGetBiome(temperature, humidity, out BiomeGraphEntry entry))
                {
                    color = entry.Color;
                }

                int flippedY = (PreviewResolution - 1) - y;
                pixels[(flippedY * PreviewResolution) + x] = color;
            }
        }

        _previewTexture.SetPixels(pixels);
        _previewTexture.Apply(false, false);
        _previewDirty = false;
        Repaint();
    }

    private void SetGraph(BiomeGraph graph)
    {
        if (_graph == graph)
        {
            return;
        }

        _graph = graph;
        if (_graph == null)
        {
            _serializedObject = null;
            _entriesProperty = null;
            _xAxisLabelProperty = null;
            _yAxisLabelProperty = null;
            _graphBackgroundColorProperty = null;
            _selectedEntryIndex = -1;
            return;
        }

        _serializedObject = new SerializedObject(_graph);
        _entriesProperty = _serializedObject.FindProperty("entries");
        _xAxisLabelProperty = _serializedObject.FindProperty("xAxisLabel");
        _yAxisLabelProperty = _serializedObject.FindProperty("yAxisLabel");
        _graphBackgroundColorProperty = _serializedObject.FindProperty("graphBackgroundColor");
        _selectedEntryIndex = -1;
        _previewDirty = true;
        Repaint();
    }

    private static Vector2 GraphPointFromCanvas(Rect rect, Vector2 canvasPoint)
    {
        float x = Mathf.InverseLerp(rect.xMin, rect.xMax, canvasPoint.x);
        float y = Mathf.InverseLerp(rect.yMax, rect.yMin, canvasPoint.y);
        return BiomeGraph.ClampPoint(new Vector2(x, y));
    }

    private static Vector2 CanvasPointFromGraph(Rect rect, Vector2 graphPoint)
    {
        graphPoint = BiomeGraph.ClampPoint(graphPoint);
        return new Vector2(
            Mathf.Lerp(rect.xMin, rect.xMax, graphPoint.x),
            Mathf.Lerp(rect.yMax, rect.yMin, graphPoint.y));
    }
}
#endif

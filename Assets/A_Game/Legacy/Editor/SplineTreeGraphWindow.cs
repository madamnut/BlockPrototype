using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class SplineTreeGraphWindow : EditorWindow
{
    private const float GraphHeight = 240f;
    private const int GraphSampleCount = 48;
    private const float InspectorWidth = 380f;
    private const float PointPickRadius = 8f;

    private sealed class GraphNavigation
    {
        public SplineTreeAsset.NodeAsset node;
        public string path;
        public GraphNavigation parent;
        public int parentPointIndex;
    }

    private SplineTreeAsset _asset;
    private GraphNavigation _currentNav;
    private int _selectedPointIndex = -1;
    private Vector2 _graphScroll;
    private Vector2 _inspectorScroll;

    private float _previewContinentalness;
    private float _previewErosion;
    private float _previewWeirdness;
    private float _previewPeaksValleys;

    [MenuItem("Window/WorldGen/Spline Tree Graph")]
    public static void OpenWindow()
    {
        GetWindow<SplineTreeGraphWindow>("Spline Tree Graph");
    }

    public static void Open(SplineTreeAsset asset)
    {
        SplineTreeGraphWindow window = GetWindow<SplineTreeGraphWindow>("Spline Tree Graph");
        window.SetAsset(asset);
        window.Show();
    }

    private void OnSelectionChange()
    {
        if (Selection.activeObject is SplineTreeAsset asset && asset != _asset)
        {
            SetAsset(asset);
            Repaint();
        }
    }

    private void OnGUI()
    {
        DrawToolbar();

        if (_asset == null)
        {
            EditorGUILayout.HelpBox("Select a SplineTreeAsset or open this window from the inspector.", MessageType.Info);
            return;
        }

        EnsureNavigation();

        Rect contentRect = new Rect(0f, 36f, position.width, position.height - 36f);
        Rect graphRect = new Rect(contentRect.x, contentRect.y, Mathf.Max(120f, contentRect.width - InspectorWidth), contentRect.height);
        Rect inspectorRect = new Rect(graphRect.xMax, contentRect.y, InspectorWidth, contentRect.height);

        DrawGraphPanels(graphRect);
        DrawInspector(inspectorRect);
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            EditorGUI.BeginChangeCheck();
            SplineTreeAsset newAsset = (SplineTreeAsset)EditorGUILayout.ObjectField(_asset, typeof(SplineTreeAsset), false, GUILayout.Width(260f));
            if (EditorGUI.EndChangeCheck())
            {
                SetAsset(newAsset);
            }

            GUI.enabled = _asset != null;

            if (GUILayout.Button("Overworld Offset", EditorStyles.toolbarButton, GUILayout.Width(120f)))
            {
                Undo.RecordObject(_asset, "Load Overworld Offset Tree");
                _asset.ResetToVanillaOverworldOffsetTree();
                EditorUtility.SetDirty(_asset);
                ResetNavigationToRoot();
            }

            if (GUILayout.Button("Overworld Factor", EditorStyles.toolbarButton, GUILayout.Width(120f)))
            {
                Undo.RecordObject(_asset, "Load Overworld Factor Tree");
                _asset.ResetToVanillaOverworldFactorTree();
                EditorUtility.SetDirty(_asset);
                ResetNavigationToRoot();
            }

            if (GUILayout.Button("Overworld Jaggedness", EditorStyles.toolbarButton, GUILayout.Width(140f)))
            {
                Undo.RecordObject(_asset, "Load Overworld Jaggedness Tree");
                _asset.ResetToVanillaOverworldJaggednessTree();
                EditorUtility.SetDirty(_asset);
                ResetNavigationToRoot();
            }

            if (GUILayout.Button("Bake", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                Undo.RecordObject(_asset, "Bake Spline Tree");
                _asset.Bake();
                EditorUtility.SetDirty(_asset);
            }

            GUI.enabled = true;
            GUILayout.FlexibleSpace();
        }
    }

    private void DrawGraphPanels(Rect graphRect)
    {
        GUI.Box(graphRect, GUIContent.none);

        Rect viewRect = new Rect(0f, 0f, graphRect.width - 18f, GetGraphContentHeight());
        _graphScroll = GUI.BeginScrollView(graphRect, _graphScroll, viewRect);

        GUILayout.BeginArea(viewRect);
        DrawPreviewInputs();
        EditorGUILayout.Space(8f);

        DrawSplinePanel(_asset.RootNode, "Base Graph", RootPath(), true);

        if (_currentNav != null && _currentNav.node != null && _currentNav.node != _asset.RootNode)
        {
            EditorGUILayout.Space(12f);
            DrawSplinePanel(_currentNav.node, "Selected Nested Graph", _currentNav.path, false);
        }

        GUILayout.EndArea();
        GUI.EndScrollView();
    }

    private void DrawPreviewInputs()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Preview Inputs", EditorStyles.boldLabel);
            _previewContinentalness = EditorGUILayout.Slider("Continentalness", _previewContinentalness, -1f, 1f);
            _previewErosion = EditorGUILayout.Slider("Erosion", _previewErosion, -1f, 1f);
            _previewWeirdness = EditorGUILayout.Slider("Weirdness", _previewWeirdness, -1f, 1f);
            _previewPeaksValleys = EditorGUILayout.Slider("Peaks & Valleys", _previewPeaksValleys, -1f, 1f);
        }
    }

    private void DrawSplinePanel(SplineTreeAsset.NodeAsset node, string title, string path, bool isBaseGraph)
    {
        if (node == null)
        {
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Coordinate: {node.coordinate}", EditorStyles.miniLabel);
            if (!isBaseGraph)
            {
                EditorGUILayout.LabelField($"Path: {path}", EditorStyles.miniLabel);
            }

            Rect graphArea = GUILayoutUtility.GetRect(10f, GraphHeight, GUILayout.ExpandWidth(true));
            DrawGraph(node, path, graphArea);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Segments / Points", EditorStyles.miniBoldLabel);

            List<SplineTreeAsset.PointAsset> points = node.points;
            if (points == null)
            {
                return;
            }

            for (int i = 0; i < points.Count; i++)
            {
                DrawPointRow(node, path, points[i], i);
            }
        }
    }

    private void DrawPointRow(SplineTreeAsset.NodeAsset node, string path, SplineTreeAsset.PointAsset point, int index)
    {
        if (point == null)
        {
            return;
        }

        bool isCurrentNode = _currentNav != null && _currentNav.node == node;
        bool isSelectedPoint = isCurrentNode && _selectedPointIndex == index;
        using (new EditorGUILayout.HorizontalScope(isSelectedPoint ? EditorStyles.helpBox : GUIStyle.none))
        {
            string segmentLabel = point.valueType == SplineTreePointValueType.NestedSpline ? "Nested" : "Constant";
            if (GUILayout.Button($"P{index} @ {point.location:0.##}", GUILayout.Width(90f)))
            {
                SelectNode(node, path);
                _selectedPointIndex = index;
            }

            EditorGUILayout.LabelField(segmentLabel, GUILayout.Width(60f));
            EditorGUILayout.LabelField($"d {point.derivative:0.##}", GUILayout.Width(70f));
            EditorGUILayout.LabelField(
                point.valueType == SplineTreePointValueType.NestedSpline ? $"fallback {point.constantValue:0.###}" : point.constantValue.ToString("0.###"));

            if (point.valueType == SplineTreePointValueType.NestedSpline && point.childNode != null)
            {
                if (GUILayout.Button("Open Child", GUILayout.Width(90f)))
                {
                    OpenChildNode(path, index, point.childNode);
                }
            }
        }
    }

    private void DrawGraph(SplineTreeAsset.NodeAsset node, string path, Rect rect)
    {
        if (Event.current.type != EventType.Repaint && Event.current.type != EventType.MouseDown)
        {
            return;
        }

        EditorGUI.DrawRect(rect, new Color(0.11f, 0.11f, 0.11f, 1f));

        List<SplineTreeAsset.PointAsset> points = node.points ?? new List<SplineTreeAsset.PointAsset>();
        float minY;
        float maxY;
        float[] sampled = SampleNode(node, out minY, out maxY);
        float range = Mathf.Max(0.0001f, maxY - minY);

        Handles.BeginGUI();
        try
        {
            DrawSegmentBands(rect, points);
            DrawGrid(rect);

            Vector3[] line = new Vector3[sampled.Length];
            for (int i = 0; i < sampled.Length; i++)
            {
                float x = Mathf.Lerp(-1f, 1f, i / (float)(sampled.Length - 1));
                line[i] = new Vector3(ToPixelX(rect, x), ToPixelY(rect, sampled[i], minY, range), 0f);
            }

            Handles.color = new Color(0.35f, 0.75f, 1f, 1f);
            Handles.DrawAAPolyLine(2f, line);

            for (int i = 0; i < points.Count; i++)
            {
                SplineTreeAsset.PointAsset point = points[i];
                if (point == null)
                {
                    continue;
                }

                float pointY = EvaluatePointValue(point);
                Vector2 pointPosition = new Vector2(
                    ToPixelX(rect, point.location),
                    ToPixelY(rect, pointY, minY, range));

                Handles.color = point.valueType == SplineTreePointValueType.NestedSpline
                    ? new Color(1f, 0.72f, 0.25f, 1f)
                    : new Color(0.95f, 0.92f, 0.55f, 1f);

                if (_currentNav != null && _currentNav.node == node && _selectedPointIndex == i)
                {
                    Handles.color = new Color(0.2f, 1f, 1f, 1f);
                }

                Handles.DrawSolidDisc(pointPosition, Vector3.forward, 4f);

                if (Event.current.type == EventType.MouseDown &&
                    rect.Contains(Event.current.mousePosition) &&
                    Vector2.Distance(Event.current.mousePosition, pointPosition) <= PointPickRadius)
                {
                    SelectNode(node, path);
                    _selectedPointIndex = i;
                    Repaint();
                    Event.current.Use();
                }
            }
        }
        finally
        {
            Handles.EndGUI();
        }
    }

    private float[] SampleNode(SplineTreeAsset.NodeAsset node, out float minY, out float maxY)
    {
        float[] values = new float[GraphSampleCount];
        minY = float.PositiveInfinity;
        maxY = float.NegativeInfinity;

        for (int i = 0; i < GraphSampleCount; i++)
        {
            float x = Mathf.Lerp(-1f, 1f, i / (float)(GraphSampleCount - 1));
            float y = EvaluateNode(node, x);
            values[i] = y;
            minY = Mathf.Min(minY, y);
            maxY = Mathf.Max(maxY, y);
        }

        List<SplineTreeAsset.PointAsset> points = node.points;
        if (points != null)
        {
            for (int i = 0; i < points.Count; i++)
            {
                SplineTreeAsset.PointAsset point = points[i];
                if (point == null)
                {
                    continue;
                }

                float pointY = EvaluatePointValue(point);
                minY = Mathf.Min(minY, pointY);
                maxY = Mathf.Max(maxY, pointY);
            }
        }

        if (float.IsNaN(minY) || float.IsInfinity(minY) || float.IsNaN(maxY) || float.IsInfinity(maxY))
        {
            minY = -1f;
            maxY = 1f;
        }

        if (Mathf.Abs(maxY - minY) < 0.0001f)
        {
            float pad = Mathf.Max(1f, Mathf.Abs(maxY) * 0.25f);
            minY -= pad;
            maxY += pad;
        }
        else
        {
            float pad = (maxY - minY) * 0.1f;
            minY -= pad;
            maxY += pad;
        }

        return values;
    }

    private void DrawSegmentBands(Rect rect, List<SplineTreeAsset.PointAsset> points)
    {
        if (points == null || points.Count == 0)
        {
            return;
        }

        for (int i = 0; i < points.Count; i++)
        {
            float x = ToPixelX(rect, points[i].location);
            EditorGUI.DrawRect(new Rect(x - 0.5f, rect.yMin, 1f, rect.height), new Color(1f, 1f, 1f, 0.16f));
        }

        for (int i = 0; i < points.Count - 1; i++)
        {
            float xMin = ToPixelX(rect, points[i].location);
            float xMax = ToPixelX(rect, points[i + 1].location);
            Color bandColor = (i % 2 == 0)
                ? new Color(1f, 1f, 1f, 0.03f)
                : new Color(0.35f, 0.75f, 1f, 0.04f);
            EditorGUI.DrawRect(new Rect(xMin, rect.yMin, Mathf.Max(1f, xMax - xMin), rect.height), bandColor);
        }
    }

    private void DrawGrid(Rect rect)
    {
        Handles.color = new Color(1f, 1f, 1f, 0.08f);
        for (int i = 1; i < 4; i++)
        {
            float x = Mathf.Lerp(rect.xMin, rect.xMax, i / 4f);
            Handles.DrawLine(new Vector3(x, rect.yMin), new Vector3(x, rect.yMax));
        }

        for (int i = 1; i < 4; i++)
        {
            float y = Mathf.Lerp(rect.yMin, rect.yMax, i / 4f);
            Handles.DrawLine(new Vector3(rect.xMin, y), new Vector3(rect.xMax, y));
        }
    }

    private void DrawInspector(Rect inspectorRect)
    {
        GUILayout.BeginArea(inspectorRect, EditorStyles.helpBox);
        _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll);

        if (_currentNav == null || _currentNav.node == null)
        {
            EditorGUILayout.HelpBox("Select a point or open a child graph.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
            return;
        }

        EditorGUILayout.LabelField("Current Graph", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(_currentNav.path, EditorStyles.miniLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = _currentNav.parent != null;
            if (GUILayout.Button("Go Parent"))
            {
                _currentNav = _currentNav.parent;
                _selectedPointIndex = _currentNav.parentPointIndex;
            }

            GUI.enabled = _asset != null && _currentNav.node != _asset.RootNode;
            if (GUILayout.Button("Go Root"))
            {
                ResetNavigationToRoot();
            }

            GUI.enabled = true;
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Node Settings", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        SplineTreeCoordinateSource coordinate = (SplineTreeCoordinateSource)EditorGUILayout.EnumPopup("Coordinate", _currentNav.node.coordinate);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(_asset, "Change Node Coordinate");
            _currentNav.node.coordinate = coordinate;
            EditorUtility.SetDirty(_asset);
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Points", EditorStyles.boldLabel);
        for (int i = 0; i < _currentNav.node.points.Count; i++)
        {
            DrawPointInspector(_currentNav.node, i);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Add Point", GUILayout.Width(100f)))
            {
                Undo.RecordObject(_asset, "Add Point");
                _currentNav.node.points.Add(new SplineTreeAsset.PointAsset());
                NormalizePoints(_currentNav.node);
                _selectedPointIndex = _currentNav.node.points.Count - 1;
                EditorUtility.SetDirty(_asset);
            }
        }

        EditorGUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawPointInspector(SplineTreeAsset.NodeAsset node, int index)
    {
        SplineTreeAsset.PointAsset point = node.points[index];
        if (point == null)
        {
            return;
        }

        bool isSelected = _selectedPointIndex == index;
        using (new EditorGUILayout.VerticalScope(isSelected ? EditorStyles.helpBox : GUI.skin.box))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button($"Point {index}", EditorStyles.miniButtonLeft, GUILayout.Width(80f)))
                {
                    _selectedPointIndex = index;
                }

                if (GUILayout.Button("Remove", EditorStyles.miniButtonRight, GUILayout.Width(70f)))
                {
                    Undo.RecordObject(_asset, "Remove Point");
                    node.points.RemoveAt(index);
                    NormalizePoints(node);
                    _selectedPointIndex = Mathf.Clamp(_selectedPointIndex, -1, node.points.Count - 1);
                    EditorUtility.SetDirty(_asset);
                    return;
                }
            }

            EditorGUI.BeginChangeCheck();
            float location = EditorGUILayout.Slider("Location", point.location, -1f, 1f);
            float derivative = EditorGUILayout.FloatField("Derivative", point.derivative);
            SplineTreePointValueType valueType = (SplineTreePointValueType)EditorGUILayout.EnumPopup("Value Type", point.valueType);
            float constantValue = EditorGUILayout.FloatField(valueType == SplineTreePointValueType.Constant ? "Value" : "Fallback", point.constantValue);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_asset, "Edit Point");
                point.location = location;
                point.derivative = derivative;
                point.valueType = valueType;
                point.constantValue = constantValue;
                NormalizePoints(node);
                EditorUtility.SetDirty(_asset);
            }

            if (point.valueType == SplineTreePointValueType.NestedSpline)
            {
                if (point.childNode == null)
                {
                    if (GUILayout.Button("Create Child Node"))
                    {
                        Undo.RecordObject(_asset, "Create Child Node");
                        point.childNode = CreateDefaultChildNode();
                        EditorUtility.SetDirty(_asset);
                    }
                }
                else
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Open Child"))
                        {
                            OpenChildNode(_currentNav.path, index, point.childNode);
                        }

                        if (GUILayout.Button("Remove Child"))
                        {
                            Undo.RecordObject(_asset, "Remove Child Node");
                            point.childNode = null;
                            EditorUtility.SetDirty(_asset);
                        }
                    }
                }
            }
        }
    }

    private float EvaluateNode(SplineTreeAsset.NodeAsset node, float currentCoordinateValue)
    {
        if (node == null || node.points == null || node.points.Count == 0)
        {
            return 0f;
        }

        int leftIndex = -1;
        for (int i = 0; i < node.points.Count; i++)
        {
            if (currentCoordinateValue < node.points[i].location)
            {
                break;
            }

            leftIndex = i;
        }

        if (leftIndex < 0)
        {
            SplineTreeAsset.PointAsset firstPoint = node.points[0];
            return EvaluatePointValue(firstPoint) + firstPoint.derivative * (currentCoordinateValue - firstPoint.location);
        }

        if (leftIndex >= node.points.Count - 1)
        {
            SplineTreeAsset.PointAsset lastPoint = node.points[node.points.Count - 1];
            return EvaluatePointValue(lastPoint) + lastPoint.derivative * (currentCoordinateValue - lastPoint.location);
        }

        SplineTreeAsset.PointAsset pointA = node.points[leftIndex];
        SplineTreeAsset.PointAsset pointB = node.points[leftIndex + 1];
        float delta = pointB.location - pointA.location;
        float t = Mathf.Abs(delta) > 0.000001f
            ? Mathf.Clamp01((currentCoordinateValue - pointA.location) / delta)
            : 0f;
        float valueA = EvaluatePointValue(pointA);
        float valueB = EvaluatePointValue(pointB);
        float valueDelta = valueB - valueA;
        float slopeA = pointA.derivative * delta - valueDelta;
        float slopeB = -pointB.derivative * delta + valueDelta;
        return Mathf.Lerp(valueA, valueB, t) + (t * (1f - t) * Mathf.Lerp(slopeA, slopeB, t));
    }

    private float EvaluatePointValue(SplineTreeAsset.PointAsset point)
    {
        if (point == null)
        {
            return 0f;
        }

        if (point.valueType == SplineTreePointValueType.Constant || point.childNode == null)
        {
            return point.constantValue;
        }

        return EvaluateNode(point.childNode, EvaluateCurrentCoordinate(point.childNode.coordinate));
    }

    private float EvaluateCurrentCoordinate(SplineTreeCoordinateSource source)
    {
        switch (source)
        {
            case SplineTreeCoordinateSource.Erosion:
                return _previewErosion;
            case SplineTreeCoordinateSource.Weirdness:
                return _previewWeirdness;
            case SplineTreeCoordinateSource.PeaksValleys:
                return _previewPeaksValleys;
            default:
                return _previewContinentalness;
        }
    }

    private void SetAsset(SplineTreeAsset asset)
    {
        _asset = asset;
        ResetNavigationToRoot();
        titleContent = new GUIContent(asset != null ? $"Spline Graph: {asset.name}" : "Spline Tree Graph");
    }

    private void EnsureNavigation()
    {
        if (_asset == null)
        {
            _currentNav = null;
            return;
        }

        if (_currentNav == null || _currentNav.node == null)
        {
            ResetNavigationToRoot();
        }
    }

    private void ResetNavigationToRoot()
    {
        if (_asset == null || _asset.RootNode == null)
        {
            _currentNav = null;
            _selectedPointIndex = -1;
            return;
        }

        _currentNav = new GraphNavigation
        {
            node = _asset.RootNode,
            path = RootPath(),
            parent = null,
            parentPointIndex = -1,
        };
        _selectedPointIndex = -1;
    }

    private void SelectNode(SplineTreeAsset.NodeAsset node, string path)
    {
        if (_currentNav != null && _currentNav.node == node)
        {
            return;
        }

        _currentNav = new GraphNavigation
        {
            node = node,
            path = path,
            parent = null,
            parentPointIndex = -1,
        };
        _selectedPointIndex = -1;
    }

    private void OpenChildNode(string parentPath, int pointIndex, SplineTreeAsset.NodeAsset childNode)
    {
        if (childNode == null)
        {
            return;
        }

        GraphNavigation parentNav = _currentNav;
        if (parentNav == null || parentNav.node == null)
        {
            parentNav = new GraphNavigation
            {
                node = _asset.RootNode,
                path = RootPath(),
                parent = null,
                parentPointIndex = -1,
            };
        }

        _currentNav = new GraphNavigation
        {
            node = childNode,
            path = parentPath + ".child[" + pointIndex + "]",
            parent = parentNav,
            parentPointIndex = pointIndex,
        };
        _selectedPointIndex = -1;
    }

    private float GetGraphContentHeight()
    {
        float height = 120f + GraphHeight + 150f;
        if (_currentNav != null && _currentNav.node != null && _currentNav.node != _asset.RootNode)
        {
            height += GraphHeight + 150f;
        }

        return height;
    }

    private static float ToPixelX(Rect rect, float x)
    {
        return Mathf.Lerp(rect.xMin + 12f, rect.xMax - 12f, (x + 1f) * 0.5f);
    }

    private static float ToPixelY(Rect rect, float y, float minY, float range)
    {
        return Mathf.Lerp(rect.yMax - 12f, rect.yMin + 12f, Mathf.Clamp01((y - minY) / range));
    }

    private static void NormalizePoints(SplineTreeAsset.NodeAsset node)
    {
        if (node == null)
        {
            return;
        }

        node.points ??= new List<SplineTreeAsset.PointAsset>();
        for (int i = 0; i < node.points.Count; i++)
        {
            SplineTreeAsset.PointAsset point = node.points[i];
            if (point == null)
            {
                node.points[i] = new SplineTreeAsset.PointAsset();
                point = node.points[i];
            }

            point.location = Mathf.Clamp(point.location, -1f, 1f);
        }

        node.points.Sort((a, b) => a.location.CompareTo(b.location));
    }

    private static SplineTreeAsset.NodeAsset CreateDefaultChildNode()
    {
        return new SplineTreeAsset.NodeAsset
        {
            coordinate = SplineTreeCoordinateSource.Erosion,
            points = new List<SplineTreeAsset.PointAsset>
            {
                new SplineTreeAsset.PointAsset { location = -1f, constantValue = 72f },
                new SplineTreeAsset.PointAsset { location = 0f, constantValue = 92f },
                new SplineTreeAsset.PointAsset { location = 1f, constantValue = 120f },
            }
        };
    }

    private static string RootPath()
    {
        return "root";
    }
}

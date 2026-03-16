using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SplineTreeAsset))]
public sealed class SplineTreeAssetEditor : Editor
{
    private const float GraphHeight = 140f;
    private static float previewContinentalness;
    private static float previewErosion;
    private static float previewWeirdness;
    private static float previewPeaksValleys;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawToolbar();
        EditorGUILayout.Space(6f);
        DrawPreviewInputs();
        EditorGUILayout.Space(8f);

        SerializedProperty rootNode = serializedObject.FindProperty("rootNode");
        DrawNode(rootNode, "Root Node", 0);

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(10f);
        SplineTreeAsset asset = (SplineTreeAsset)target;
        EditorGUILayout.HelpBox(asset.BakedSummary, MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Overworld Offset"))
            {
                Undo.RecordObject(asset, "Load Overworld Offset Tree");
                asset.ResetToVanillaOverworldOffsetTree();
                EditorUtility.SetDirty(asset);
                serializedObject.Update();
            }

            if (GUILayout.Button("Overworld Factor"))
            {
                Undo.RecordObject(asset, "Load Overworld Factor Tree");
                asset.ResetToVanillaOverworldFactorTree();
                EditorUtility.SetDirty(asset);
                serializedObject.Update();
            }

            if (GUILayout.Button("Overworld Jaggedness"))
            {
                Undo.RecordObject(asset, "Load Overworld Jaggedness Tree");
                asset.ResetToVanillaOverworldJaggednessTree();
                EditorUtility.SetDirty(asset);
                serializedObject.Update();
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Bake Spline Tree"))
            {
                Undo.RecordObject(asset, "Bake Spline Tree");
                asset.Bake();
                EditorUtility.SetDirty(asset);
            }
        }
    }

    private void DrawToolbar()
    {
        EditorGUILayout.HelpBox(
            "DEEPSLATE-style nested spline tree. Each node picks a coordinate axis and each point can be a constant or another nested spline. Preset buttons load vanilla overworld offset/factor/jaggedness trees for editing/reference.",
            MessageType.None);
    }

    private void DrawPreviewInputs()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Preview Inputs", EditorStyles.boldLabel);
            previewContinentalness = EditorGUILayout.Slider("Continentalness", previewContinentalness, -1f, 1f);
            previewErosion = EditorGUILayout.Slider("Erosion", previewErosion, -1f, 1f);
            previewWeirdness = EditorGUILayout.Slider("Weirdness", previewWeirdness, -1f, 1f);
            previewPeaksValleys = EditorGUILayout.Slider("Peaks & Valleys", previewPeaksValleys, -1f, 1f);
        }
    }

    private void DrawNode(SerializedProperty nodeProperty, string label, int depth)
    {
        if (nodeProperty == null)
        {
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(nodeProperty.FindPropertyRelative("coordinate"));

            Rect graphRect = GUILayoutUtility.GetRect(10f, GraphHeight, GUILayout.ExpandWidth(true));
            DrawNodeGraph(graphRect, nodeProperty);

            SerializedProperty pointsProperty = nodeProperty.FindPropertyRelative("points");
            if (pointsProperty == null)
            {
                return;
            }

            EditorGUILayout.Space(4f);
            for (int i = 0; i < pointsProperty.arraySize; i++)
            {
                DrawPoint(pointsProperty, i, depth);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Add Point", GUILayout.Width(100f)))
                {
                    pointsProperty.InsertArrayElementAtIndex(pointsProperty.arraySize);
                    SerializedProperty point = pointsProperty.GetArrayElementAtIndex(pointsProperty.arraySize - 1);
                    InitializePoint(point, nodeProperty);
                }
            }
        }
    }

    private void DrawPoint(SerializedProperty pointsProperty, int index, int depth)
    {
        SerializedProperty point = pointsProperty.GetArrayElementAtIndex(index);
        SerializedProperty location = point.FindPropertyRelative("location");
        SerializedProperty derivative = point.FindPropertyRelative("derivative");
        SerializedProperty valueType = point.FindPropertyRelative("valueType");
        SerializedProperty constantValue = point.FindPropertyRelative("constantValue");
        SerializedProperty childNode = point.FindPropertyRelative("childNode");

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Point {index}", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Remove", GUILayout.Width(70f)))
                {
                    pointsProperty.DeleteArrayElementAtIndex(index);
                    return;
                }
            }

            EditorGUILayout.PropertyField(location);
            EditorGUILayout.PropertyField(derivative);
            EditorGUILayout.PropertyField(valueType);

            if ((SplineTreePointValueType)valueType.enumValueIndex == SplineTreePointValueType.Constant)
            {
                EditorGUILayout.PropertyField(constantValue, new GUIContent("Value"));
            }
            else
            {
                EditorGUILayout.PropertyField(constantValue, new GUIContent("Fallback Value"));
                if (!HasNodeData(childNode))
                {
                    if (GUILayout.Button("Create Child Node"))
                    {
                        CreateChildNode(childNode);
                    }
                }
                else
                {
                    EditorGUILayout.Space(4f);
                    DrawNode(childNode, $"Child Node ({depth + 1})", depth + 1);
                }
            }
        }
    }

    private void DrawNodeGraph(Rect rect, SerializedProperty nodeProperty)
    {
        if (Event.current.type != EventType.Repaint)
        {
            return;
        }

        EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 1f));
        Handles.BeginGUI();
        try
        {
            DrawGrid(rect);
            float minY;
            float maxY;
            DrawCurve(rect, nodeProperty, out minY, out maxY);
            Handles.color = new Color(1f, 1f, 1f, 0.25f);
            float range = Mathf.Max(0.0001f, maxY - minY);
            float previewNormalized = Mathf.Clamp01((previewY(nodeProperty) - minY) / range);
            Handles.DrawLine(
                new Vector3(rect.xMin, Mathf.Lerp(rect.yMax, rect.yMin, previewNormalized)),
                new Vector3(rect.xMax, Mathf.Lerp(rect.yMax, rect.yMin, previewNormalized)));
        }
        finally
        {
            Handles.EndGUI();
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

    private void DrawCurve(Rect rect, SerializedProperty nodeProperty, out float minY, out float maxY)
    {
        const int sampleCount = 64;
        float[] sampledY = new float[sampleCount];
        minY = float.PositiveInfinity;
        maxY = float.NegativeInfinity;
        for (int i = 0; i < sampleCount; i++)
        {
            float x = Mathf.Lerp(-1f, 1f, i / (float)(sampleCount - 1));
            float y = EvaluateNode(nodeProperty, x);
            sampledY[i] = y;
            minY = Mathf.Min(minY, y);
            maxY = Mathf.Max(maxY, y);
        }

        SerializedProperty pointsProperty = nodeProperty.FindPropertyRelative("points");
        for (int i = 0; i < pointsProperty.arraySize; i++)
        {
            float pointY = EvaluatePointValue(pointsProperty.GetArrayElementAtIndex(i));
            minY = Mathf.Min(minY, pointY);
            maxY = Mathf.Max(maxY, pointY);
        }

        if (float.IsNaN(minY) || float.IsNaN(maxY) || float.IsInfinity(minY) || float.IsInfinity(maxY))
        {
            minY = -1f;
            maxY = 1f;
        }

        if (Mathf.Abs(maxY - minY) < 0.0001f)
        {
            float padding = Mathf.Max(1f, Mathf.Abs(maxY) * 0.25f);
            minY -= padding;
            maxY += padding;
        }
        else
        {
            float padding = (maxY - minY) * 0.1f;
            minY -= padding;
            maxY += padding;
        }

        float range = Mathf.Max(0.0001f, maxY - minY);
        Vector3[] points = new Vector3[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float x = Mathf.Lerp(-1f, 1f, i / (float)(sampleCount - 1));
            float y = sampledY[i];
            float px = Mathf.Lerp(rect.xMin, rect.xMax, (x + 1f) * 0.5f);
            float py = Mathf.Lerp(rect.yMax, rect.yMin, Mathf.Clamp01((y - minY) / range));
            points[i] = new Vector3(px, py, 0f);
        }

        Handles.color = new Color(0.35f, 0.75f, 1f, 1f);
        Handles.DrawAAPolyLine(2f, points);

        Handles.color = new Color(1f, 0.9f, 0.3f, 1f);
        for (int i = 0; i < pointsProperty.arraySize; i++)
        {
            SerializedProperty point = pointsProperty.GetArrayElementAtIndex(i);
            float x = point.FindPropertyRelative("location").floatValue;
            float y = EvaluatePointValue(point);
            float px = Mathf.Lerp(rect.xMin, rect.xMax, (x + 1f) * 0.5f);
            float py = Mathf.Lerp(rect.yMax, rect.yMin, Mathf.Clamp01((y - minY) / range));
            Handles.DrawSolidDisc(new Vector3(px, py, 0f), Vector3.forward, 3f);
        }
    }

    private float previewY(SerializedProperty nodeProperty)
    {
        float currentCoordinate = EvaluateCurrentCoordinate(nodeProperty);
        return EvaluateNode(nodeProperty, currentCoordinate);
    }

    private float EvaluateCurrentCoordinate(SerializedProperty nodeProperty)
    {
        SerializedProperty coordinate = nodeProperty.FindPropertyRelative("coordinate");
        return coordinate != null
            ? SelectPreviewCoordinate((SplineTreeCoordinateSource)coordinate.enumValueIndex)
            : previewContinentalness;
    }

    private float EvaluateNode(SerializedProperty nodeProperty, float currentCoordinateValue)
    {
        SerializedProperty coordinateProperty = nodeProperty.FindPropertyRelative("coordinate");
        SerializedProperty pointsProperty = nodeProperty.FindPropertyRelative("points");
        if (coordinateProperty == null || pointsProperty == null || pointsProperty.arraySize == 0)
        {
            return 0f;
        }

        int leftIndex = -1;
        for (int i = 0; i < pointsProperty.arraySize; i++)
        {
            SerializedProperty point = pointsProperty.GetArrayElementAtIndex(i);
            if (currentCoordinateValue < point.FindPropertyRelative("location").floatValue)
            {
                break;
            }
            leftIndex = i;
        }

        if (leftIndex < 0)
        {
            SerializedProperty firstPoint = pointsProperty.GetArrayElementAtIndex(0);
            float baseValue = EvaluatePointValue(firstPoint);
            float location = firstPoint.FindPropertyRelative("location").floatValue;
            float derivative = firstPoint.FindPropertyRelative("derivative").floatValue;
            return baseValue + derivative * (currentCoordinateValue - location);
        }

        if (leftIndex >= pointsProperty.arraySize - 1)
        {
            SerializedProperty lastPoint = pointsProperty.GetArrayElementAtIndex(pointsProperty.arraySize - 1);
            float baseValue = EvaluatePointValue(lastPoint);
            float location = lastPoint.FindPropertyRelative("location").floatValue;
            float derivative = lastPoint.FindPropertyRelative("derivative").floatValue;
            return baseValue + derivative * (currentCoordinateValue - location);
        }

        SerializedProperty pointA = pointsProperty.GetArrayElementAtIndex(leftIndex);
        SerializedProperty pointB = pointsProperty.GetArrayElementAtIndex(leftIndex + 1);

        float locationA = pointA.FindPropertyRelative("location").floatValue;
        float locationB = pointB.FindPropertyRelative("location").floatValue;
        float derivativeA = pointA.FindPropertyRelative("derivative").floatValue;
        float derivativeB = pointB.FindPropertyRelative("derivative").floatValue;
        float valueA = EvaluatePointValue(pointA);
        float valueB = EvaluatePointValue(pointB);
        float delta = locationB - locationA;
        float t = Mathf.Abs(delta) > 0.000001f
            ? Mathf.Clamp01((currentCoordinateValue - locationA) / delta)
            : 0f;
        float valueDelta = valueB - valueA;
        float slopeA = derivativeA * delta - valueDelta;
        float slopeB = -derivativeB * delta + valueDelta;
        return Mathf.Lerp(valueA, valueB, t) + (t * (1f - t) * Mathf.Lerp(slopeA, slopeB, t));
    }

    private float EvaluatePointValue(SerializedProperty pointProperty)
    {
        SerializedProperty valueType = pointProperty.FindPropertyRelative("valueType");
        if ((SplineTreePointValueType)valueType.enumValueIndex == SplineTreePointValueType.Constant)
        {
            return pointProperty.FindPropertyRelative("constantValue").floatValue;
        }

        SerializedProperty childNode = pointProperty.FindPropertyRelative("childNode");
        if (!HasNodeData(childNode))
        {
            return pointProperty.FindPropertyRelative("constantValue").floatValue;
        }

        return EvaluateNode(childNode, EvaluateCurrentCoordinate(childNode));
    }

    private static bool HasNodeData(SerializedProperty nodeProperty)
    {
        return nodeProperty != null &&
               nodeProperty.managedReferenceValue != null &&
               nodeProperty.FindPropertyRelative("points") != null;
    }

    private static void CreateChildNode(SerializedProperty childNodeProperty)
    {
        childNodeProperty.managedReferenceValue = new SplineTreeAsset.NodeAsset();
        SerializedProperty coordinate = childNodeProperty.FindPropertyRelative("coordinate");
        if (coordinate != null)
        {
            coordinate.enumValueIndex = (int)SplineTreeCoordinateSource.Erosion;
        }

        SerializedProperty points = childNodeProperty.FindPropertyRelative("points");
        if (points == null)
        {
            return;
        }

        points.arraySize = 3;
        InitializePoint(points.GetArrayElementAtIndex(0), null);
        InitializePoint(points.GetArrayElementAtIndex(1), null);
        InitializePoint(points.GetArrayElementAtIndex(2), null);
        points.GetArrayElementAtIndex(0).FindPropertyRelative("location").floatValue = -1f;
        points.GetArrayElementAtIndex(0).FindPropertyRelative("constantValue").floatValue = 72f;
        points.GetArrayElementAtIndex(1).FindPropertyRelative("location").floatValue = 0f;
        points.GetArrayElementAtIndex(1).FindPropertyRelative("constantValue").floatValue = 92f;
        points.GetArrayElementAtIndex(2).FindPropertyRelative("location").floatValue = 1f;
        points.GetArrayElementAtIndex(2).FindPropertyRelative("constantValue").floatValue = 120f;
    }

    private static void InitializePoint(SerializedProperty pointProperty, SerializedProperty nodeProperty)
    {
        if (pointProperty == null)
        {
            return;
        }

        pointProperty.FindPropertyRelative("location").floatValue = 0f;
        pointProperty.FindPropertyRelative("derivative").floatValue = 0f;
        pointProperty.FindPropertyRelative("valueType").enumValueIndex = (int)SplineTreePointValueType.Constant;
        pointProperty.FindPropertyRelative("constantValue").floatValue = 63f;
        SerializedProperty childNode = pointProperty.FindPropertyRelative("childNode");
        if (childNode != null)
        {
            childNode.managedReferenceValue = null;
        }
    }

    private static float SelectPreviewCoordinate(SplineTreeCoordinateSource source)
    {
        switch (source)
        {
            case SplineTreeCoordinateSource.Erosion:
                return previewErosion;
            case SplineTreeCoordinateSource.Weirdness:
                return previewWeirdness;
            case SplineTreeCoordinateSource.PeaksValleys:
                return previewPeaksValleys;
            default:
                return previewContinentalness;
        }
    }
}

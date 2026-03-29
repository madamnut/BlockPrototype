using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SplineGraphAsset))]
public sealed class SplineGraphAssetEditor : Editor
{
    private const float PreviewHeight = 180f;
    private const float Padding = 10f;
    private const float PointRadius = 5f;
    private const int DragControlIdHint = 184237;

    private static int s_DraggingPointIndex = -1;
    private static float s_DragMinHeight;
    private static float s_DragMaxHeight;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SplineGraphAsset splineAsset = (SplineGraphAsset)target;
        SerializedProperty pointsProperty = serializedObject.FindProperty("points");
        SerializedProperty lutResolutionProperty = serializedObject.FindProperty("lutResolution");

        GetDisplayInputRange(splineAsset, out float displayMinInput, out float displayMaxInput);
        GetDisplayHeightRange(splineAsset, out float displayMinHeight, out float displayMaxHeight);
        GetEditHeightRange(splineAsset, out float editMinHeight, out float editMaxHeight);
        DrawGraphPreview(
            splineAsset,
            pointsProperty,
            displayMinInput,
            displayMaxInput,
            displayMinHeight,
            displayMaxHeight,
            editMinHeight,
            editMaxHeight);
        DrawLutControls(splineAsset, lutResolutionProperty);
        EditorGUILayout.Space(10f);
        DrawPointsEditor(pointsProperty, editMinHeight, editMaxHeight);

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawGraphPreview(
        SplineGraphAsset splineAsset,
        SerializedProperty pointsProperty,
        float displayMinInput,
        float displayMaxInput,
        float displayMinHeight,
        float displayMaxHeight,
        float editMinHeight,
        float editMaxHeight)
    {
        EditorGUILayout.LabelField("Spline Graph", EditorStyles.boldLabel);
        Rect rect = GUILayoutUtility.GetRect(10f, PreviewHeight, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 1f));

        float displayHeightRange = Mathf.Max(1f, displayMaxHeight - displayMinHeight);

        Rect plotRect = new(
            rect.x + Padding,
            rect.y + Padding,
            rect.width - (Padding * 2f),
            rect.height - (Padding * 2f));

        EditorGUI.DrawRect(plotRect, new Color(0.16f, 0.16f, 0.16f, 1f));
        DrawGrid(plotRect);

        DrawCurve(plotRect, splineAsset, displayMinInput, displayMaxInput, displayMinHeight, displayHeightRange);
        HandlePointDragging(plotRect, pointsProperty, displayMinInput, displayMaxInput, displayMinHeight, displayHeightRange, editMinHeight, editMaxHeight, splineAsset);
        DrawPoints(plotRect, pointsProperty, displayMinInput, displayMaxInput, displayMinHeight, displayHeightRange);
        DrawGraphLabels(rect, plotRect, displayMinInput, displayMaxInput, displayMinHeight, displayMaxHeight);
    }

    private static void DrawCurve(Rect plotRect, SplineGraphAsset splineAsset, float minInput, float maxInput, float minHeight, float heightRange)
    {
        GndSplinePoint[] sorted = GndSplineUtility.SanitizePoints(splineAsset.GetPointsOrDefault());
        Vector3[] linePoints = new Vector3[Mathf.Max(2, plotRect.width > 0f ? Mathf.CeilToInt(plotRect.width) : 2)];
        int sampleCount = linePoints.Length;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = sampleCount == 1 ? 0f : i / (float)(sampleCount - 1);
            float input = Mathf.Lerp(minInput, maxInput, t);
            float height = GndSplineUtility.EvaluateSanitized(sorted, input);
            float normalizedHeight = Mathf.InverseLerp(minHeight, minHeight + heightRange, height);
            linePoints[i] = new Vector3(
                Mathf.Lerp(plotRect.xMin, plotRect.xMax, t),
                Mathf.Lerp(plotRect.yMax, plotRect.yMin, normalizedHeight),
                0f);
        }

        Handles.BeginGUI();
        Handles.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        Handles.DrawAAPolyLine(2f, linePoints);
        Handles.EndGUI();
    }

    private static void DrawPoints(Rect plotRect, SerializedProperty pointsProperty, float minInput, float maxInput, float minHeight, float heightRange)
    {
        Handles.BeginGUI();
        for (int i = 0; i < pointsProperty.arraySize; i++)
        {
            SerializedProperty pointProperty = pointsProperty.GetArrayElementAtIndex(i);
            float input = pointProperty.FindPropertyRelative("input").floatValue;
            float height = pointProperty.FindPropertyRelative("height").floatValue;
            Vector2 center = GraphToScreen(input, height, plotRect, minInput, maxInput, minHeight, heightRange);

            Handles.color = Color.black;
            Handles.DrawSolidDisc(center, Vector3.forward, PointRadius + 1.5f);
            Handles.color = s_DraggingPointIndex == i
                ? new Color(1f, 0.8f, 0.2f, 1f)
                : new Color(0.3f, 0.8f, 1f, 1f);
            Handles.DrawSolidDisc(center, Vector3.forward, PointRadius);
        }

        Handles.EndGUI();
    }

    private static void HandlePointDragging(
        Rect plotRect,
        SerializedProperty pointsProperty,
        float displayMinInput,
        float displayMaxInput,
        float displayMinHeight,
        float displayHeightRange,
        float editMinHeight,
        float editMaxHeight,
        SplineGraphAsset splineAsset)
    {
        Event currentEvent = Event.current;
        int controlId = GUIUtility.GetControlID(DragControlIdHint, FocusType.Passive);

        switch (currentEvent.GetTypeForControl(controlId))
        {
            case EventType.MouseDown:
                if (currentEvent.button != 0)
                {
                    break;
                }

                for (int i = 0; i < pointsProperty.arraySize; i++)
                {
                    SerializedProperty pointProperty = pointsProperty.GetArrayElementAtIndex(i);
                    float input = pointProperty.FindPropertyRelative("input").floatValue;
                    float height = pointProperty.FindPropertyRelative("height").floatValue;
                    Vector2 center = GraphToScreen(input, height, plotRect, displayMinInput, displayMaxInput, displayMinHeight, displayHeightRange);
                    Rect pointRect = new(center.x - 8f, center.y - 8f, 16f, 16f);
                    if (!pointRect.Contains(currentEvent.mousePosition))
                    {
                        continue;
                    }

                    s_DraggingPointIndex = i;
                    s_DragMinHeight = editMinHeight;
                    s_DragMaxHeight = editMaxHeight;
                    GUIUtility.hotControl = controlId;
                    currentEvent.Use();
                    break;
                }

                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl != controlId || s_DraggingPointIndex < 0)
                {
                    break;
                }

                Undo.RecordObject(splineAsset, "Move Spline Graph Point");
                Vector2 clamped = new(
                    currentEvent.mousePosition.x,
                    Mathf.Clamp(currentEvent.mousePosition.y, plotRect.yMin, plotRect.yMax));
                float dragHeightRange = Mathf.Max(1f, s_DragMaxHeight - s_DragMinHeight);
                ScreenToGraph(clamped, plotRect, s_DragMinHeight, dragHeightRange, out float movedInput, out float movedHeight);

                SerializedProperty draggedPoint = pointsProperty.GetArrayElementAtIndex(s_DraggingPointIndex);
                movedInput = draggedPoint.FindPropertyRelative("input").floatValue;
                draggedPoint.FindPropertyRelative("height").floatValue = movedHeight;
                SortPoints(pointsProperty);
                EditorUtility.SetDirty(splineAsset);
                currentEvent.Use();
                GUI.changed = true;
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl != controlId)
                {
                    break;
                }

                s_DraggingPointIndex = -1;
                s_DragMinHeight = 0f;
                s_DragMaxHeight = 0f;
                GUIUtility.hotControl = 0;
                currentEvent.Use();
                break;

            case EventType.Repaint:
                for (int i = 0; i < pointsProperty.arraySize; i++)
                {
                    SerializedProperty pointProperty = pointsProperty.GetArrayElementAtIndex(i);
                    float input = pointProperty.FindPropertyRelative("input").floatValue;
                    float height = pointProperty.FindPropertyRelative("height").floatValue;
                    Vector2 center = GraphToScreen(input, height, plotRect, displayMinInput, displayMaxInput, displayMinHeight, displayHeightRange);
                    EditorGUIUtility.AddCursorRect(
                        new Rect(center.x - 8f, center.y - 8f, 16f, 16f),
                        MouseCursor.MoveArrow);
                }

                break;
        }
    }

    private static void DrawGraphLabels(Rect rect, Rect plotRect, float minInput, float maxInput, float minHeight, float maxHeight)
    {
        GUIStyle labelStyle = new(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(0.8f, 0.8f, 0.8f, 1f) }
        };

        GUI.Label(new Rect(plotRect.xMin, rect.yMin, 80f, 16f), minInput.ToString("0.##"), labelStyle);
        GUI.Label(new Rect(plotRect.xMax - 60f, rect.yMin, 60f, 16f), maxInput.ToString("0.##"), labelStyle);
        GUI.Label(new Rect(rect.xMin + 4f, plotRect.yMin - 2f, 80f, 16f), maxHeight.ToString("0"), labelStyle);
        GUI.Label(new Rect(rect.xMin + 4f, plotRect.yMax - 14f, 80f, 16f), minHeight.ToString("0"), labelStyle);
    }

    private static void DrawGrid(Rect plotRect)
    {
        Handles.BeginGUI();
        Handles.color = new Color(1f, 1f, 1f, 0.08f);
        for (int i = 1; i < 4; i++)
        {
            float x = Mathf.Lerp(plotRect.xMin, plotRect.xMax, i / 4f);
            Handles.DrawLine(new Vector3(x, plotRect.yMin), new Vector3(x, plotRect.yMax));

            float y = Mathf.Lerp(plotRect.yMin, plotRect.yMax, i / 4f);
            Handles.DrawLine(new Vector3(plotRect.xMin, y), new Vector3(plotRect.xMax, y));
        }

        Handles.color = new Color(1f, 1f, 1f, 0.18f);
        float centerX = Mathf.Lerp(plotRect.xMin, plotRect.xMax, 0.5f);
        Handles.DrawLine(new Vector3(centerX, plotRect.yMin), new Vector3(centerX, plotRect.yMax));
        Handles.EndGUI();
    }

    private static void DrawPointsEditor(SerializedProperty pointsProperty, float minHeight, float maxHeight)
    {
        EditorGUILayout.LabelField("Points", EditorStyles.boldLabel);

        int newSize = EditorGUILayout.IntField("Count", pointsProperty.arraySize);
        pointsProperty.arraySize = Mathf.Max(2, newSize);

        for (int i = 0; i < pointsProperty.arraySize; i++)
        {
            SerializedProperty pointProperty = pointsProperty.GetArrayElementAtIndex(i);
            SerializedProperty inputProperty = pointProperty.FindPropertyRelative("input");
            SerializedProperty heightProperty = pointProperty.FindPropertyRelative("height");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Point {i}", EditorStyles.boldLabel);

            GUI.enabled = pointsProperty.arraySize > 2;
            if (GUILayout.Button("Remove", GUILayout.Width(70f)))
            {
                pointsProperty.DeleteArrayElementAtIndex(i);
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(inputProperty, new GUIContent("Input"));
            EditorGUILayout.Slider(heightProperty, minHeight, maxHeight, "Height");
            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("Add Point"))
        {
            int newIndex = pointsProperty.arraySize;
            pointsProperty.InsertArrayElementAtIndex(newIndex);

            SerializedProperty insertedPoint = pointsProperty.GetArrayElementAtIndex(newIndex);
            SerializedProperty insertedInput = insertedPoint.FindPropertyRelative("input");
            SerializedProperty insertedHeight = insertedPoint.FindPropertyRelative("height");

            SerializedProperty previousPoint = pointsProperty.GetArrayElementAtIndex(Mathf.Max(0, newIndex - 1));
            insertedInput.floatValue = previousPoint.FindPropertyRelative("input").floatValue;
            insertedHeight.floatValue = previousPoint.FindPropertyRelative("height").floatValue;
        }
    }

    private static Vector2 GraphToScreen(float input, float height, Rect plotRect, float minInput, float maxInput, float minHeight, float heightRange)
    {
        float normalizedX = Mathf.InverseLerp(minInput, maxInput, input);
        float normalizedY = Mathf.InverseLerp(minHeight, minHeight + heightRange, height);
        return new Vector2(
            Mathf.Lerp(plotRect.xMin, plotRect.xMax, normalizedX),
            Mathf.Lerp(plotRect.yMax, plotRect.yMin, normalizedY));
    }

    private static void ScreenToGraph(Vector2 position, Rect plotRect, float minHeight, float heightRange, out float input, out float height)
    {
        float normalizedX = Mathf.InverseLerp(plotRect.xMin, plotRect.xMax, position.x);
        float normalizedY = Mathf.InverseLerp(plotRect.yMax, plotRect.yMin, position.y);
        input = Mathf.Lerp(-1f, 1f, normalizedX);
        height = Mathf.Lerp(minHeight, minHeight + heightRange, normalizedY);
    }

    private static void SortPoints(SerializedProperty pointsProperty)
    {
        int count = pointsProperty.arraySize;
        if (count < 2)
        {
            return;
        }

        (float input, float height)[] values = new (float input, float height)[count];
        for (int i = 0; i < count; i++)
        {
            SerializedProperty pointProperty = pointsProperty.GetArrayElementAtIndex(i);
            values[i] = (
                pointProperty.FindPropertyRelative("input").floatValue,
                pointProperty.FindPropertyRelative("height").floatValue);
        }

        Array.Sort(values, static (left, right) => left.input.CompareTo(right.input));
        for (int i = 0; i < count; i++)
        {
            SerializedProperty pointProperty = pointsProperty.GetArrayElementAtIndex(i);
            pointProperty.FindPropertyRelative("input").floatValue = values[i].input;
            pointProperty.FindPropertyRelative("height").floatValue = values[i].height;
        }
    }

    private void DrawLutControls(
        SplineGraphAsset splineAsset,
        SerializedProperty lutResolutionProperty)
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("LUT", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(lutResolutionProperty, new GUIContent("Resolution"));

        string status;
        MessageType messageType;
        if (splineAsset.HasFreshBakedHeightLut)
        {
            status = $"Ready ({Mathf.Max(2, splineAsset.lutResolution)} samples)";
            messageType = MessageType.Info;
        }
        else if (splineAsset.HasBakedHeightLut)
        {
            status = "Stale LUT detected. Re-bake to use the latest points.";
            messageType = MessageType.Warning;
        }
        else
        {
            status = "No baked LUT yet.";
            messageType = MessageType.None;
        }

        EditorGUILayout.HelpBox(status, messageType);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Bake LUT"))
        {
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(splineAsset, "Bake Spline LUT");
            splineAsset.BakeHeightLut();
            EditorUtility.SetDirty(splineAsset);
            serializedObject.Update();
        }

        if (GUILayout.Button("Clear LUT"))
        {
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(splineAsset, "Clear Spline LUT");
            splineAsset.ClearBakedHeightLut();
            EditorUtility.SetDirty(splineAsset);
            serializedObject.Update();
        }

        EditorGUILayout.EndHorizontal();
    }

    private static void GetDisplayHeightRange(SplineGraphAsset splineAsset, out float minHeight, out float maxHeight)
    {
        minHeight = splineAsset.MinHeight;
        maxHeight = splineAsset.MaxHeight;
        if (Mathf.Approximately(minHeight, maxHeight))
        {
            minHeight -= 0.5f;
            maxHeight += 0.5f;
        }
    }

    private static void GetEditHeightRange(SplineGraphAsset splineAsset, out float minHeight, out float maxHeight)
    {
        float actualMin = splineAsset.MinHeight;
        float actualMax = splineAsset.MaxHeight;
        float actualRange = actualMax - actualMin;

        if (Mathf.Approximately(actualMin, actualMax))
        {
            minHeight = actualMin - 8f;
            maxHeight = actualMax + 8f;
            return;
        }

        float padding = Mathf.Max(8f, actualRange * 0.15f);
        minHeight = actualMin - padding;
        maxHeight = actualMax + padding;
    }

    private static void GetDisplayInputRange(SplineGraphAsset splineAsset, out float minInput, out float maxInput)
    {
        GndSplinePoint[] points = GndSplineUtility.SanitizePoints(splineAsset.GetPointsOrDefault());
        minInput = points[0].input;
        maxInput = points[points.Length - 1].input;

        if (Mathf.Approximately(minInput, maxInput))
        {
            minInput -= 0.5f;
            maxInput += 0.5f;
        }
    }
}

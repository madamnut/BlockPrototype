#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SplineAsset))]
public sealed class SplineAssetEditor : Editor
{
    private SerializedProperty _coordinateKindProperty;
    private SerializedProperty _runtimeJsonProperty;
    private SerializedProperty _pointsProperty;

    private void OnEnable()
    {
        _coordinateKindProperty = serializedObject.FindProperty("coordinateKind");
        _runtimeJsonProperty = serializedObject.FindProperty("runtimeJson");
        _pointsProperty = serializedObject.FindProperty("points");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SplineAsset asset = (SplineAsset)target;

        EditorGUILayout.LabelField("Spline", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_coordinateKindProperty);
        EditorGUILayout.PropertyField(_runtimeJsonProperty);
        EditorGUILayout.IntField("Point Count", _pointsProperty.arraySize);

        EditorGUILayout.Space(8f);
        DrawPreview(asset);

        EditorGUILayout.Space(8f);
        EditorGUILayout.PropertyField(_pointsProperty, new GUIContent("Points"), true);

        EditorGUILayout.Space(10f);
        using (new EditorGUI.DisabledScope(asset.RuntimeJson == null))
        {
            if (GUILayout.Button("Bake To Runtime JSON", GUILayout.Height(28f)))
            {
                BakeSpline(asset);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawPreview(SplineAsset asset)
    {
        Rect rect = GUILayoutUtility.GetRect(10f, 220f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.10f, 0.10f, 0.11f, 1f));

        Rect graphRect = new(rect.x + 18f, rect.y + 12f, rect.width - 36f, rect.height - 24f);
        Handles.BeginGUI();

        Handles.color = new Color(1f, 1f, 1f, 0.06f);
        for (int i = 1; i < 8; i++)
        {
            float t = i / 8f;
            float x = Mathf.Lerp(graphRect.xMin, graphRect.xMax, t);
            Handles.DrawLine(new Vector3(x, graphRect.yMin), new Vector3(x, graphRect.yMax));
        }

        for (int i = 1; i < 6; i++)
        {
            float t = i / 6f;
            float y = Mathf.Lerp(graphRect.yMin, graphRect.yMax, t);
            Handles.DrawLine(new Vector3(graphRect.xMin, y), new Vector3(graphRect.xMax, y));
        }

        float minY = GetPreviewMinY(asset);
        float maxY = GetPreviewMaxY(asset);
        if (Mathf.Approximately(minY, maxY))
        {
            minY -= 1f;
            maxY += 1f;
        }

        Handles.color = new Color(1f, 1f, 1f, 0.35f);
        float zeroX = Mathf.Lerp(graphRect.xMin, graphRect.xMax, Mathf.InverseLerp(-1f, 1f, 0f));
        Handles.DrawLine(new Vector3(zeroX, graphRect.yMin), new Vector3(zeroX, graphRect.yMax));
        if (minY <= 0f && maxY >= 0f)
        {
            float zeroY = Mathf.Lerp(graphRect.yMax, graphRect.yMin, Mathf.InverseLerp(minY, maxY, 0f));
            Handles.DrawLine(new Vector3(graphRect.xMin, zeroY), new Vector3(graphRect.xMax, zeroY));
        }

        if (asset.Points != null && asset.Points.Length > 0)
        {
            try
            {
                const int segments = 96;
                Vector3[] line = new Vector3[segments + 1];
                for (int i = 0; i <= segments; i++)
                {
                    float t = i / (float)segments;
                    float coordinate = Mathf.Lerp(-1f, 1f, t);
                    SplineContext context = asset.CoordinateKind switch
                    {
                        SplineCoordinateKind.Continentalness => new SplineContext(coordinate, 0f, 0f, 0f),
                        SplineCoordinateKind.Erosion => new SplineContext(0f, coordinate, 0f, 0f),
                        SplineCoordinateKind.PeaksAndValleys => new SplineContext(0f, 0f, coordinate, 0f),
                        SplineCoordinateKind.Ridges => new SplineContext(0f, 0f, 0f, coordinate),
                        _ => new SplineContext(0f, 0f, 0f, 0f),
                    };

                    float y = asset.Evaluate(context);
                    float px = Mathf.Lerp(graphRect.xMin, graphRect.xMax, t);
                    float py = Mathf.Lerp(graphRect.yMax, graphRect.yMin, Mathf.InverseLerp(minY, maxY, y));
                    line[i] = new Vector3(px, py, 0f);
                }

                Handles.color = new Color(0.95f, 0.82f, 0.28f, 1f);
                Handles.DrawAAPolyLine(2f, line);
            }
            catch
            {
                EditorGUI.HelpBox(rect, "Preview unavailable. Check child spline references.", MessageType.Warning);
            }
        }

        Handles.EndGUI();
    }

    private static float GetPreviewMinY(SplineAsset asset)
    {
        if (asset?.Points == null || asset.Points.Length == 0)
        {
            return -1f;
        }

        float min = float.PositiveInfinity;
        try
        {
            for (int i = 0; i <= 48; i++)
            {
                float coordinate = Mathf.Lerp(-1f, 1f, i / 48f);
                SplineContext context = asset.CoordinateKind switch
                {
                    SplineCoordinateKind.Continentalness => new SplineContext(coordinate, 0f, 0f, 0f),
                    SplineCoordinateKind.Erosion => new SplineContext(0f, coordinate, 0f, 0f),
                    SplineCoordinateKind.PeaksAndValleys => new SplineContext(0f, 0f, coordinate, 0f),
                    SplineCoordinateKind.Ridges => new SplineContext(0f, 0f, 0f, coordinate),
                    _ => new SplineContext(0f, 0f, 0f, 0f),
                };
                min = Mathf.Min(min, asset.Evaluate(context));
            }
        }
        catch
        {
            return -1f;
        }

        return float.IsInfinity(min) ? -1f : min - 1f;
    }

    private static float GetPreviewMaxY(SplineAsset asset)
    {
        if (asset?.Points == null || asset.Points.Length == 0)
        {
            return 1f;
        }

        float max = float.NegativeInfinity;
        try
        {
            for (int i = 0; i <= 48; i++)
            {
                float coordinate = Mathf.Lerp(-1f, 1f, i / 48f);
                SplineContext context = asset.CoordinateKind switch
                {
                    SplineCoordinateKind.Continentalness => new SplineContext(coordinate, 0f, 0f, 0f),
                    SplineCoordinateKind.Erosion => new SplineContext(0f, coordinate, 0f, 0f),
                    SplineCoordinateKind.PeaksAndValleys => new SplineContext(0f, 0f, coordinate, 0f),
                    SplineCoordinateKind.Ridges => new SplineContext(0f, 0f, 0f, coordinate),
                    _ => new SplineContext(0f, 0f, 0f, 0f),
                };
                max = Mathf.Max(max, asset.Evaluate(context));
            }
        }
        catch
        {
            return 1f;
        }

        return float.IsNegativeInfinity(max) ? 1f : max + 1f;
    }

    private static void BakeSpline(SplineAsset sourceGraph)
    {
        string assetPath = AssetDatabase.GetAssetPath(sourceGraph.RuntimeJson);
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            throw new IOException($"Could not resolve asset path for runtime JSON '{sourceGraph.RuntimeJson.name}'.");
        }

        string json = SplineJsonWriter.Serialize(sourceGraph);
        File.WriteAllText(assetPath, json);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
        Debug.Log($"Baked spline '{sourceGraph.name}' to '{assetPath}'.");
    }
}
#endif

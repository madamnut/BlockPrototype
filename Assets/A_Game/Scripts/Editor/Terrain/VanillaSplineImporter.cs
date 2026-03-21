#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class VanillaSplineImporter
{
    private const string VanillaRoot = "Assets/DEEPSLATE/Reference/Vanilla/misode-mcmeta-current/worldgen/density_function/overworld";
    private const string TerraGraphRoot = "Assets/A_Game/WorldGen/Terra/Graphs";

    [MenuItem("Tools/OOZOO/Terra/Import Vanilla DEEPSLATE Splines")]
    public static void ImportVanillaSplines()
    {
        ImportOne(
            Path.Combine(VanillaRoot, "offset.json"),
            Path.Combine(TerraGraphRoot, "OffsetSplineGraph.asset"));
        ImportOne(
            Path.Combine(VanillaRoot, "factor.json"),
            Path.Combine(TerraGraphRoot, "FactorSplineGraph.asset"));
        ImportOne(
            Path.Combine(VanillaRoot, "jaggedness.json"),
            Path.Combine(TerraGraphRoot, "JaggednessSplineGraph.asset"));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Imported vanilla DEEPSLATE offset/factor/jaggedness splines into OOZOO Terra graph assets.");
    }

    private static void ImportOne(string sourceJsonPath, string targetGraphPath)
    {
        SplineAsset rootAsset = AssetDatabase.LoadAssetAtPath<SplineAsset>(targetGraphPath);
        if (rootAsset == null)
        {
            throw new InvalidOperationException($"Could not load target spline asset at '{targetGraphPath}'.");
        }

        string sourceJson = File.ReadAllText(sourceJsonPath);
        object raw = SplineJsonLoader.DeserializeRaw(sourceJson);
        if (raw is not Dictionary<string, object> rootObject)
        {
            throw new InvalidOperationException($"Vanilla spline file '{sourceJsonPath}' does not contain an object root.");
        }

        if (FindFirstSplineObject(rootObject) is not Dictionary<string, object> splineObject)
        {
            throw new InvalidOperationException($"Could not find root spline object in '{sourceJsonPath}'.");
        }

        string assetPath = AssetDatabase.GetAssetPath(rootAsset);
        RemoveChildSubAssets(assetPath, rootAsset);
        PopulateAssetRecursive(rootAsset, splineObject, assetPath, rootAsset.name, 0);
        EditorUtility.SetDirty(rootAsset);
        BakeRuntimeJson(rootAsset);
    }

    private static Dictionary<string, object> FindFirstSplineObject(object current)
    {
        if (current is Dictionary<string, object> objectValue)
        {
            if (objectValue.ContainsKey("coordinate") && objectValue.ContainsKey("points"))
            {
                return objectValue;
            }

            foreach (KeyValuePair<string, object> pair in objectValue)
            {
                if (pair.Key == "spline" && pair.Value is Dictionary<string, object> splineValue)
                {
                    return splineValue;
                }

                Dictionary<string, object> found = FindFirstSplineObject(pair.Value);
                if (found != null)
                {
                    return found;
                }
            }
        }
        else if (current is IList listValue)
        {
            foreach (object item in listValue)
            {
                Dictionary<string, object> found = FindFirstSplineObject(item);
                if (found != null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private static void PopulateAssetRecursive(
        SplineAsset asset,
        Dictionary<string, object> splineObject,
        string hostAssetPath,
        string childNamePrefix,
        int depth)
    {
        SplineCoordinateKind coordinateKind = ParseCoordinate(ReadRequiredString(splineObject, "coordinate"));
        List<object> pointObjects = ReadRequiredArray(splineObject, "points");
        SplinePointData[] points = new SplinePointData[pointObjects.Count];

        for (int i = 0; i < pointObjects.Count; i++)
        {
            if (pointObjects[i] is not Dictionary<string, object> pointObject)
            {
                throw new InvalidOperationException($"Spline point[{i}] must be an object.");
            }

            SplinePointData point = new();
            point.Location = ReadRequiredFloat(pointObject, "location");
            point.Derivative = ReadOptionalFloat(pointObject, "derivative", 0f);

            if (!pointObject.TryGetValue("value", out object valueRaw))
            {
                throw new InvalidOperationException($"Spline point[{i}] is missing value.");
            }

            if (valueRaw is Dictionary<string, object> childSplineObject)
            {
                SplineAsset childAsset = ScriptableObject.CreateInstance<SplineAsset>();
                childAsset.name = $"{childNamePrefix}_Child_{depth}_{i}";
                AssetDatabase.AddObjectToAsset(childAsset, hostAssetPath);
                PopulateAssetRecursive(childAsset, childSplineObject, hostAssetPath, childAsset.name, depth + 1);

                point.Value.UseChildSpline = true;
                point.Value.ConstantValue = 0f;
                point.Value.ChildSpline = childAsset;
                EditorUtility.SetDirty(childAsset);
            }
            else
            {
                point.Value.UseChildSpline = false;
                point.Value.ConstantValue = ConvertToFloat(valueRaw);
                point.Value.ChildSpline = null;
            }

            points[i] = point;
        }

        asset.ReplaceData(coordinateKind, points);
        EditorUtility.SetDirty(asset);
    }

    private static void RemoveChildSubAssets(string assetPath, SplineAsset rootAsset)
    {
        UnityEngine.Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < allAssets.Length; i++)
        {
            UnityEngine.Object candidate = allAssets[i];
            if (candidate == null || candidate == rootAsset || candidate is not SplineAsset)
            {
                continue;
            }

            UnityEngine.Object.DestroyImmediate(candidate, true);
        }
    }

    private static void BakeRuntimeJson(SplineAsset sourceGraph)
    {
        if (sourceGraph.RuntimeJson == null)
        {
            throw new InvalidOperationException($"Spline '{sourceGraph.name}' is missing runtime JSON reference.");
        }

        string assetPath = AssetDatabase.GetAssetPath(sourceGraph.RuntimeJson);
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            throw new IOException($"Could not resolve asset path for runtime JSON '{sourceGraph.RuntimeJson.name}'.");
        }

        string json = SplineJsonWriter.Serialize(sourceGraph);
        File.WriteAllText(assetPath, json);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
    }

    private static SplineCoordinateKind ParseCoordinate(string rawCoordinate)
    {
        return rawCoordinate switch
        {
            "continentalness" => SplineCoordinateKind.Continentalness,
            "erosion" => SplineCoordinateKind.Erosion,
            "peaks_and_valleys" => SplineCoordinateKind.PeaksAndValleys,
            "ridges" => SplineCoordinateKind.Ridges,
            "minecraft:overworld/continents" => SplineCoordinateKind.Continentalness,
            "minecraft:overworld/erosion" => SplineCoordinateKind.Erosion,
            "minecraft:overworld/ridges_folded" => SplineCoordinateKind.PeaksAndValleys,
            "minecraft:overworld/ridges" => SplineCoordinateKind.Ridges,
            _ => throw new InvalidOperationException($"Unsupported spline coordinate '{rawCoordinate}'."),
        };
    }

    private static string ReadRequiredString(Dictionary<string, object> root, string key)
    {
        if (!root.TryGetValue(key, out object value) || value is not string stringValue)
        {
            throw new InvalidOperationException($"Spline object is missing string field '{key}'.");
        }

        return stringValue;
    }

    private static List<object> ReadRequiredArray(Dictionary<string, object> root, string key)
    {
        if (!root.TryGetValue(key, out object value) || value is not List<object> arrayValue)
        {
            throw new InvalidOperationException($"Spline object is missing array field '{key}'.");
        }

        return arrayValue;
    }

    private static float ReadRequiredFloat(Dictionary<string, object> root, string key)
    {
        if (!root.TryGetValue(key, out object value))
        {
            throw new InvalidOperationException($"Spline object is missing float field '{key}'.");
        }

        return ConvertToFloat(value);
    }

    private static float ReadOptionalFloat(Dictionary<string, object> root, string key, float defaultValue)
    {
        return root.TryGetValue(key, out object value) ? ConvertToFloat(value) : defaultValue;
    }

    private static float ConvertToFloat(object value)
    {
        return value switch
        {
            double doubleValue => (float)doubleValue,
            float floatValue => floatValue,
            long longValue => longValue,
            int intValue => intValue,
            string stringValue => float.Parse(stringValue, CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException("Spline value must be numeric."),
        };
    }
}
#endif

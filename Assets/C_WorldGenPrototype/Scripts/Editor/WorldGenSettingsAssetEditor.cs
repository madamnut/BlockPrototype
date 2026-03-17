using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WorldGenSettingsAsset))]
public sealed class WorldGenSettingsAssetEditor : Editor
{
    private static bool showHeightSplineValues = true;
    private static bool showWaterValues = true;
    private static bool showCdfRemapValues = true;
    private static bool showContinentalnessValues = true;
    private static bool showErosionValues = true;
    private static bool showRidgesValues = true;
    private static bool showBandValues = true;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawFoldoutSection(
            ref showHeightSplineValues,
            "Terrain Spline Trees",
            "offsetSplineTree",
            "factorSplineTree",
            "jaggednessSplineTree");

        DrawFoldoutSection(
            ref showWaterValues,
            "Water",
            "seaLevel");

        DrawFoldoutSection(
            ref showCdfRemapValues,
            "Value Remap",
            "useContinentalnessRemap",
            "useErosionRemap",
            "useRidgesRemap");

        DrawFoldoutSection(
            ref showContinentalnessValues,
            "Continentalness Values",
            "useWarp",
            "warpOctaves",
            "warpFrequency",
            "warpAmplitude",
            "warpLacunarity",
            "warpGain",
            "useMacro",
            "macroOctaves",
            "macroFrequency",
            "macroLacunarity",
            "macroGain",
            "macroWeight",
            "useBroad",
            "broadOctaves",
            "broadFrequency",
            "broadLacunarity",
            "broadGain",
            "broadWeight",
            "useDetail",
            "detailOctaves",
            "detailFrequency",
            "detailLacunarity",
            "detailGain",
            "detailWeight");

        DrawFoldoutSection(
            ref showErosionValues,
            "Erosion Values",
            "erosionUseWarp",
            "erosionWarpOctaves",
            "erosionWarpFrequency",
            "erosionWarpAmplitude",
            "erosionWarpLacunarity",
            "erosionWarpGain",
            "erosionUseMacro",
            "erosionMacroOctaves",
            "erosionMacroFrequency",
            "erosionMacroLacunarity",
            "erosionMacroGain",
            "erosionMacroWeight",
            "erosionUseBroad",
            "erosionBroadOctaves",
            "erosionBroadFrequency",
            "erosionBroadLacunarity",
            "erosionBroadGain",
            "erosionBroadWeight",
            "erosionUseDetail",
            "erosionDetailOctaves",
            "erosionDetailFrequency",
            "erosionDetailLacunarity",
            "erosionDetailGain",
            "erosionDetailWeight");

        DrawFoldoutSection(
            ref showRidgesValues,
            "Weirdness Values",
            "ridgesUseWarp",
            "ridgesWarpOctaves",
            "ridgesWarpFrequency",
            "ridgesWarpAmplitude",
            "ridgesWarpLacunarity",
            "ridgesWarpGain",
            "ridgesUseMacro",
            "ridgesMacroOctaves",
            "ridgesMacroFrequency",
            "ridgesMacroLacunarity",
            "ridgesMacroGain",
            "ridgesMacroWeight",
            "ridgesUseBroad",
            "ridgesBroadOctaves",
            "ridgesBroadFrequency",
            "ridgesBroadLacunarity",
            "ridgesBroadGain",
            "ridgesBroadWeight",
            "ridgesUseDetail",
            "ridgesDetailOctaves",
            "ridgesDetailFrequency",
            "ridgesDetailLacunarity",
            "ridgesDetailGain",
            "ridgesDetailWeight");

        DrawContinentalnessBandsSection();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawFoldoutSection(ref bool isOpen, string label, params string[] propertyNames)
    {
        isOpen = EditorGUILayout.Foldout(isOpen, label, true);
        if (!isOpen)
        {
            EditorGUILayout.Space(4f);
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            for (int i = 0; i < propertyNames.Length; i++)
            {
                SerializedProperty property = serializedObject.FindProperty(propertyNames[i]);
                if (property != null)
                {
                    EditorGUILayout.PropertyField(property, true);
                }
            }
        }

        EditorGUILayout.Space(6f);
    }

    private void DrawContinentalnessBandsSection()
    {
        showBandValues = EditorGUILayout.Foldout(showBandValues, "Continentalness Bands", true);
        if (!showBandValues)
        {
            EditorGUILayout.Space(4f);
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawProperty("abyssUpperBound");
            DrawProperty("abyssColor");
            DrawProperty("deepOceanUpperBound");
            DrawProperty("deepOceanColor");
            DrawProperty("oceanUpperBound");
            DrawProperty("oceanColor");
            DrawProperty("shallowOceanTransitionColor", "Shallow Ocean (Transition)");
            DrawProperty("coastUpperBound");
            DrawProperty("coastColor", "Coast (Transition)");
            DrawProperty("inlandUpperBound");
            DrawProperty("inlandColor");
            DrawProperty("deepInlandUpperBound");
            DrawProperty("deepInlandColor");
            DrawProperty("continentalCoreColor");
        }

        EditorGUILayout.Space(6f);
    }

    private void DrawProperty(string propertyName, string label = null)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(label))
        {
            EditorGUILayout.PropertyField(property, true);
            return;
        }

        EditorGUILayout.PropertyField(property, new GUIContent(label), true);
    }
}

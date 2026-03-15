using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WorldGenSettingsAsset))]
public sealed class WorldGenSettingsAssetEditor : Editor
{
    private static bool showRuntimeTerrainValues = true;
    private static bool showCdfRemapValues = true;
    private static bool showFilterValues = true;
    private static bool showContinentalnessValues = true;
    private static bool showErosionValues = true;
    private static bool showRidgesValues = true;
    private static bool showTemperatureValues = true;
    private static bool showPrecipitationValues = true;
    private static bool showBandValues = true;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawFoldoutSection(
            ref showRuntimeTerrainValues,
            "Runtime Terrain",
            "seaLevel",
            "minTerrainHeight",
            "maxTerrainHeight");

        DrawFoldoutSection(
            ref showCdfRemapValues,
            "Value Remap",
            "useContinentalnessRemap",
            "useErosionRemap",
            "useRidgesRemap");

        DrawFoldoutSection(
            ref showFilterValues,
            "Filters",
            "useContinentalnessFilter",
            "continentalnessFilter");

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
            "Peaks/Ridges Values",
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

        DrawFoldoutSection(
            ref showTemperatureValues,
            "Temperature Values",
            "temperatureUseWarp",
            "temperatureWarpOctaves",
            "temperatureWarpFrequency",
            "temperatureWarpAmplitude",
            "temperatureWarpLacunarity",
            "temperatureWarpGain",
            "temperatureUseMacro",
            "temperatureMacroOctaves",
            "temperatureMacroFrequency",
            "temperatureMacroLacunarity",
            "temperatureMacroGain",
            "temperatureMacroWeight",
            "temperatureUseBroad",
            "temperatureBroadOctaves",
            "temperatureBroadFrequency",
            "temperatureBroadLacunarity",
            "temperatureBroadGain",
            "temperatureBroadWeight",
            "temperatureUseDetail",
            "temperatureDetailOctaves",
            "temperatureDetailFrequency",
            "temperatureDetailLacunarity",
            "temperatureDetailGain",
            "temperatureDetailWeight");

        DrawFoldoutSection(
            ref showPrecipitationValues,
            "Precipitation Values",
            "precipitationUseWarp",
            "precipitationWarpOctaves",
            "precipitationWarpFrequency",
            "precipitationWarpAmplitude",
            "precipitationWarpLacunarity",
            "precipitationWarpGain",
            "precipitationUseMacro",
            "precipitationMacroOctaves",
            "precipitationMacroFrequency",
            "precipitationMacroLacunarity",
            "precipitationMacroGain",
            "precipitationMacroWeight",
            "precipitationUseBroad",
            "precipitationBroadOctaves",
            "precipitationBroadFrequency",
            "precipitationBroadLacunarity",
            "precipitationBroadGain",
            "precipitationBroadWeight",
            "precipitationUseDetail",
            "precipitationDetailOctaves",
            "precipitationDetailFrequency",
            "precipitationDetailLacunarity",
            "precipitationDetailGain",
            "precipitationDetailWeight");

        DrawFoldoutSection(
            ref showBandValues,
            "Continentalness Bands",
            "abyssUpperBound",
            "abyssColor",
            "deepOceanUpperBound",
            "deepOceanColor",
            "oceanUpperBound",
            "oceanColor",
            "shallowOceanColor",
            "coastUpperBound",
            "coastColor",
            "inlandUpperBound",
            "inlandColor",
            "deepInlandUpperBound",
            "deepInlandColor",
            "continentalCoreColor");

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
}

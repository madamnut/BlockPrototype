using UnityEngine;

[CreateAssetMenu(fileName = "WorldGenPack", menuName = "World/Terra/WorldGen Pack")]
public sealed class WorldGenPackAsset : ScriptableObject
{
    [SerializeField, Range(0, TerrainData.WorldHeight - 1)] private int seaLevel = TerrainData.DefaultSeaLevel;
    [Header("Runtime Simplex")]
    [SerializeField] private SimplexNoiseSettingsAsset continentalnessSimplexSettings;
    [SerializeField] private SimplexNoiseSettingsAsset erosionSimplexSettings;
    [SerializeField] private SimplexNoiseSettingsAsset weirdnessSimplexSettings;
    [SerializeField] private SimplexNoiseSettingsAsset jaggedSimplexSettings;
    [SerializeField] private SimplexNoise3DSettingsAsset terrain3DSimplexSettings;

    [SerializeField] private SplineAsset offsetSplineGraph;
    [SerializeField] private SplineAsset factorSplineGraph;
    [SerializeField] private SplineAsset jaggednessSplineGraph;

    public int SeaLevel => seaLevel;
    public SimplexNoiseSettingsAsset ContinentalnessSimplexSettings => continentalnessSimplexSettings;
    public SimplexNoiseSettingsAsset ErosionSimplexSettings => erosionSimplexSettings;
    public SimplexNoiseSettingsAsset WeirdnessSimplexSettings => weirdnessSimplexSettings;
    public SimplexNoiseSettingsAsset JaggedSimplexSettings => jaggedSimplexSettings;
    public SimplexNoise3DSettingsAsset Terrain3DSimplexSettings => terrain3DSimplexSettings;
    public SplineAsset OffsetSplineGraph => offsetSplineGraph;
    public SplineAsset FactorSplineGraph => factorSplineGraph;
    public SplineAsset JaggednessSplineGraph => jaggednessSplineGraph;
}

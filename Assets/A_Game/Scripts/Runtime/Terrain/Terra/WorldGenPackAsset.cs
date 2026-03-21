using UnityEngine;

[CreateAssetMenu(fileName = "WorldGenPack", menuName = "World/Terra/WorldGen Pack")]
public sealed class WorldGenPackAsset : ScriptableObject
{
    [SerializeField, Range(0, TerrainData.WorldHeight - 1)] private int seaLevel = TerrainData.DefaultSeaLevel;
    [SerializeField] private ContinentalnessSettingsAsset continentalnessSettings;
    [SerializeField] private ErosionSettingsAsset erosionSettings;
    [SerializeField] private WeirdnessSettingsAsset weirdnessSettings;
    [SerializeField] private SplineAsset offsetSplineGraph;
    [SerializeField] private SplineAsset factorSplineGraph;
    [SerializeField] private SplineAsset jaggednessSplineGraph;

    public int SeaLevel => seaLevel;
    public ContinentalnessSettingsAsset ContinentalnessSettings => continentalnessSettings;
    public ErosionSettingsAsset ErosionSettings => erosionSettings;
    public WeirdnessSettingsAsset WeirdnessSettings => weirdnessSettings;
    public SplineAsset OffsetSplineGraph => offsetSplineGraph;
    public SplineAsset FactorSplineGraph => factorSplineGraph;
    public SplineAsset JaggednessSplineGraph => jaggednessSplineGraph;
}

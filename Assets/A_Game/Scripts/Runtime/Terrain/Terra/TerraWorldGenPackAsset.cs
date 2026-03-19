using UnityEngine;

[CreateAssetMenu(fileName = "TerraWorldGenPack", menuName = "World/Terra/WorldGen Pack")]
public sealed class TerraWorldGenPackAsset : ScriptableObject
{
    [SerializeField, Range(0, TerrainData.WorldHeight - 1)] private int seaLevel = TerrainData.DefaultSeaLevel;
    [SerializeField] private TerraContinentalnessSettingsAsset continentalnessSettings;
    [SerializeField] private TerraWeirdnessSettingsAsset weirdnessSettings;
    [SerializeField] private TerraContinentalnessOffsetGraphAsset continentalnessOffsetGraph;
    [SerializeField] private TerraContinentalnessFactorGraphAsset continentalnessFactorGraph;

    public int SeaLevel => seaLevel;
    public TerraContinentalnessSettingsAsset ContinentalnessSettings => continentalnessSettings;
    public TerraWeirdnessSettingsAsset WeirdnessSettings => weirdnessSettings;
    public TerraContinentalnessOffsetGraphAsset ContinentalnessOffsetGraph => continentalnessOffsetGraph;
    public TerraContinentalnessFactorGraphAsset ContinentalnessFactorGraph => continentalnessFactorGraph;
}

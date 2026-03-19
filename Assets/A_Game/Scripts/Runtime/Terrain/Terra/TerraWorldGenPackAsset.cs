using UnityEngine;

[CreateAssetMenu(fileName = "TerraWorldGenPack", menuName = "World/Terra/WorldGen Pack")]
public sealed class TerraWorldGenPackAsset : ScriptableObject
{
    [SerializeField] private TerraContinentalnessSettingsAsset continentalnessSettings;
    [SerializeField] private TerraContinentalnessOffsetGraphAsset continentalnessOffsetGraph;

    public TerraContinentalnessSettingsAsset ContinentalnessSettings => continentalnessSettings;
    public TerraContinentalnessOffsetGraphAsset ContinentalnessOffsetGraph => continentalnessOffsetGraph;
}

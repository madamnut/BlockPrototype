using UnityEngine;

public sealed class VoxelWorldGenSettingsAsset : ScriptableObject
{
    public TerrainGenerationSettings settings = TerrainGenerationSettings.Default;
}

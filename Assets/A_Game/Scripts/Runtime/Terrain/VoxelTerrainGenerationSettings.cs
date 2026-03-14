using System;
using UnityEngine;

[Serializable]
public struct VoxelTerrainGenerationSettings
{
    [Range(0, VoxelTerrainData.WorldHeight - 1)] public int seaLevel;
    [Range(0, VoxelTerrainData.WorldHeight - 1)] public int minTerrainHeight;
    [Range(0, VoxelTerrainData.WorldHeight - 1)] public int maxTerrainHeight;
    public bool useContinentalnessCdfRemap;
    public ContinentalnessSettings continentalness;

    public bool IsInitialized =>
        maxTerrainHeight > minTerrainHeight;

    public static VoxelTerrainGenerationSettings Default => new()
    {
        seaLevel = 63,
        minTerrainHeight = 0,
        maxTerrainHeight = 180,
        useContinentalnessCdfRemap = false,
        continentalness = WorldGenSettingsAsset.CreateDefaultSettings(),
    };

    public static VoxelTerrainGenerationSettings FromWorldGenSettings(
        WorldGenSettingsAsset worldGenSettingsAsset,
        bool useContinentalnessCdfRemap)
    {
        if (worldGenSettingsAsset == null)
        {
            VoxelTerrainGenerationSettings defaults = Default;
            defaults.useContinentalnessCdfRemap = useContinentalnessCdfRemap;
            return defaults;
        }

        return new VoxelTerrainGenerationSettings
        {
            seaLevel = worldGenSettingsAsset.SeaLevel,
            minTerrainHeight = worldGenSettingsAsset.MinTerrainHeight,
            maxTerrainHeight = worldGenSettingsAsset.MaxTerrainHeight,
            useContinentalnessCdfRemap = useContinentalnessCdfRemap,
            continentalness = worldGenSettingsAsset.ToSettings(),
        };
    }
}

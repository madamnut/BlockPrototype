using System;
using UnityEngine;

[Serializable]
public struct VoxelTerrainGenerationSettings
{
    [Range(0, VoxelTerrainData.WorldHeight - 1)] public int seaLevel;
    [Range(0, VoxelTerrainData.WorldHeight - 1)] public int minTerrainHeight;
    [Range(0, VoxelTerrainData.WorldHeight - 1)] public int maxTerrainHeight;
    public bool useContinentalnessCdfRemap;
    public bool useErosionCdfRemap;
    public bool useRidgesCdfRemap;
    public ContinentalnessSettings continentalness;
    public ErosionSettings erosion;
    public RidgesSettings ridges;

    public bool IsInitialized =>
        maxTerrainHeight > minTerrainHeight;

    public static VoxelTerrainGenerationSettings Default => new()
    {
        seaLevel = 63,
        minTerrainHeight = 0,
        maxTerrainHeight = 180,
        useContinentalnessCdfRemap = false,
        useErosionCdfRemap = false,
        useRidgesCdfRemap = false,
        continentalness = WorldGenSettingsAsset.CreateDefaultSettings(),
        erosion = WorldGenSettingsAsset.CreateDefaultErosionSettings(),
        ridges = WorldGenSettingsAsset.CreateDefaultRidgesSettings(),
    };

    public static VoxelTerrainGenerationSettings FromWorldGenSettings(
        WorldGenSettingsAsset worldGenSettingsAsset,
        bool useContinentalnessCdfRemap,
        bool useErosionCdfRemap,
        bool useRidgesCdfRemap)
    {
        if (worldGenSettingsAsset == null)
        {
            VoxelTerrainGenerationSettings defaults = Default;
            defaults.useContinentalnessCdfRemap = useContinentalnessCdfRemap;
            defaults.useErosionCdfRemap = useErosionCdfRemap;
            defaults.useRidgesCdfRemap = useRidgesCdfRemap;
            return defaults;
        }

        return new VoxelTerrainGenerationSettings
        {
            seaLevel = worldGenSettingsAsset.SeaLevel,
            minTerrainHeight = worldGenSettingsAsset.MinTerrainHeight,
            maxTerrainHeight = worldGenSettingsAsset.MaxTerrainHeight,
            useContinentalnessCdfRemap = useContinentalnessCdfRemap,
            useErosionCdfRemap = useErosionCdfRemap,
            useRidgesCdfRemap = useRidgesCdfRemap,
            continentalness = worldGenSettingsAsset.ToSettings(),
            erosion = worldGenSettingsAsset.ToErosionSettings(),
            ridges = worldGenSettingsAsset.ToRidgesSettings(),
        };
    }
}

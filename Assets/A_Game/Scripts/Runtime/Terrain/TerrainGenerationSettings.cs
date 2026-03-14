using System;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public struct TerrainGenerationSettings
{
    [Range(0, TerrainData.WorldHeight - 1)] public int seaLevel;
    [Range(0, TerrainData.WorldHeight - 1)] public int minTerrainHeight;
    [Range(0, TerrainData.WorldHeight - 1)] public int maxTerrainHeight;
    [FormerlySerializedAs("useContinentalnessCdfRemap")] public bool useContinentalnessRemap;
    [FormerlySerializedAs("useErosionCdfRemap")] public bool useErosionRemap;
    [FormerlySerializedAs("useRidgesCdfRemap")] public bool useRidgesRemap;
    public ContinentalnessSettings continentalness;
    public ErosionSettings erosion;
    public RidgesSettings ridges;

    public bool IsInitialized =>
        maxTerrainHeight > minTerrainHeight;

    public static TerrainGenerationSettings Default => new()
    {
        seaLevel = 63,
        minTerrainHeight = 0,
        maxTerrainHeight = 180,
        useContinentalnessRemap = false,
        useErosionRemap = false,
        useRidgesRemap = false,
        continentalness = WorldGenSettingsAsset.CreateDefaultSettings(),
        erosion = WorldGenSettingsAsset.CreateDefaultErosionSettings(),
        ridges = WorldGenSettingsAsset.CreateDefaultRidgesSettings(),
    };

    public static TerrainGenerationSettings FromWorldGenSettings(
        WorldGenSettingsAsset worldGenSettingsAsset,
        bool useContinentalnessRemap,
        bool useErosionRemap,
        bool useRidgesRemap)
    {
        if (worldGenSettingsAsset == null)
        {
            TerrainGenerationSettings defaults = Default;
            defaults.useContinentalnessRemap = useContinentalnessRemap;
            defaults.useErosionRemap = useErosionRemap;
            defaults.useRidgesRemap = useRidgesRemap;
            return defaults;
        }

        return new TerrainGenerationSettings
        {
            seaLevel = worldGenSettingsAsset.SeaLevel,
            minTerrainHeight = worldGenSettingsAsset.MinTerrainHeight,
            maxTerrainHeight = worldGenSettingsAsset.MaxTerrainHeight,
            useContinentalnessRemap = useContinentalnessRemap,
            useErosionRemap = useErosionRemap,
            useRidgesRemap = useRidgesRemap,
            continentalness = worldGenSettingsAsset.ToSettings(),
            erosion = worldGenSettingsAsset.ToErosionSettings(),
            ridges = worldGenSettingsAsset.ToRidgesSettings(),
        };
    }
}

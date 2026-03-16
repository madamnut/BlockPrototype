using System;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public struct TerrainGenerationSettings
{
    [FormerlySerializedAs("useContinentalnessCdfRemap")] public bool useContinentalnessRemap;
    [FormerlySerializedAs("useErosionCdfRemap")] public bool useErosionRemap;
    [FormerlySerializedAs("useRidgesCdfRemap")] public bool useRidgesRemap;
    public ContinentalnessSettings continentalness;
    public ErosionSettings erosion;
    public RidgesSettings ridges;

    public bool IsInitialized =>
        true;

    public static TerrainGenerationSettings Default => new()
    {
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
            useContinentalnessRemap = useContinentalnessRemap,
            useErosionRemap = useErosionRemap,
            useRidgesRemap = useRidgesRemap,
            continentalness = worldGenSettingsAsset.ToSettings(),
            erosion = worldGenSettingsAsset.ToErosionSettings(),
            ridges = worldGenSettingsAsset.ToRidgesSettings(),
        };
    }
}

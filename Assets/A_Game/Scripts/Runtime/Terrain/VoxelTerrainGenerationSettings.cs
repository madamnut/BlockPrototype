using System;
using UnityEngine;

[Serializable]
public struct VoxelTerrainGenerationSettings
{
    [Range(0, VoxelTerrainData.WorldHeight - 1)] public int seaLevel;
    [Range(0, VoxelTerrainData.WorldHeight - 1)] public int minTerrainHeight;
    [Range(0, VoxelTerrainData.WorldHeight - 1)] public int maxTerrainHeight;
    [Range(0f, 1f)] public float continentalnessSeaLevel;
    [Range(0f, 1f)] public float continentalnessWeight;
    [Range(0.1f, 8f)] public float continentalnessWarpScaleMultiplier;
    [Range(0f, 512f)] public float continentalnessWarpStrength;
    [Range(1f, 16f)] public float continentalnessDetailScaleMultiplier;
    [Range(0f, 1f)] public float continentalnessDetailWeight;
    [Range(0f, 1f)] public float continentalnessRemapMin;
    [Range(0f, 1f)] public float continentalnessRemapMax;
    public VoxelTerrainNoiseSettings continentalness;

    public bool IsInitialized =>
        maxTerrainHeight > minTerrainHeight &&
        continentalness.IsConfigured;

    public static VoxelTerrainGenerationSettings Default
    {
        get
        {
            return new VoxelTerrainGenerationSettings
            {
                seaLevel = 63,
                minTerrainHeight = 28,
                maxTerrainHeight = 156,
                continentalnessSeaLevel = 0.44f,
                continentalnessWeight = 1f,
                continentalnessWarpScaleMultiplier = 0.35f,
                continentalnessWarpStrength = 120f,
                continentalnessDetailScaleMultiplier = 3f,
                continentalnessDetailWeight = 0.08f,
                continentalnessRemapMin = 0.43f,
                continentalnessRemapMax = 0.6f,
                continentalness = VoxelTerrainNoiseSettings.Create(0.00125f, 2, 0.45f, 2.1f, new Vector2(11.3f, 57.8f)),
            };
        }
    }
}

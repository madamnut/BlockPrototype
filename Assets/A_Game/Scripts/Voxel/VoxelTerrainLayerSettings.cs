using System;
using UnityEngine;

[Serializable]
public struct VoxelTerrainLayerSettings
{
    [Min(0)] public int baseHeight;
    [Min(0f)] public float coarseScale;
    [Min(0f)] public float coarseAmplitude;
    [Min(0f)] public float detailScale;
    [Min(0f)] public float detailAmplitude;
    [Min(0f)] public float ridgeScale;
    [Min(0f)] public float ridgeAmplitude;

    public static VoxelTerrainLayerSettings CreateDefaultDirt()
    {
        return new VoxelTerrainLayerSettings
        {
            baseHeight = 34,
            coarseScale = 0.028f,
            coarseAmplitude = 30f,
            detailScale = 0.085f,
            detailAmplitude = 10f,
            ridgeScale = 0.041f,
            ridgeAmplitude = 8f,
        };
    }

    public static VoxelTerrainLayerSettings CreateDefaultRock()
    {
        return new VoxelTerrainLayerSettings
        {
            baseHeight = 18,
            coarseScale = 0.021f,
            coarseAmplitude = 18f,
            detailScale = 0.072f,
            detailAmplitude = 6f,
            ridgeScale = 0.037f,
            ridgeAmplitude = 4f,
        };
    }
}

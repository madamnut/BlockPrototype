using System;
using UnityEngine;

[Serializable]
public struct VoxelTerrainGenerationSettings
{
    [Range(0, VoxelTerrainData.WorldHeight - 1)] public int seaLevel;
    public VoxelTerrainLayerSettings dirt;
    public VoxelTerrainLayerSettings rock;

    public static VoxelTerrainGenerationSettings Default
    {
        get
        {
            return new VoxelTerrainGenerationSettings
            {
                seaLevel = 24,
                dirt = VoxelTerrainLayerSettings.CreateDefaultDirt(),
                rock = VoxelTerrainLayerSettings.CreateDefaultRock(),
            };
        }
    }
}

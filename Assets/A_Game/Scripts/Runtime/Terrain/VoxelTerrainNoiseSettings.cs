using System;
using UnityEngine;

[Serializable]
public struct VoxelTerrainNoiseSettings
{
    [Min(0.00001f)] public float scale;
    [Min(1)] public int octaves;
    [Range(0f, 1f)] public float persistence;
    [Min(1f)] public float lacunarity;
    public Vector2 offset;

    public bool IsConfigured => scale > 0f && octaves > 0;

    public static VoxelTerrainNoiseSettings Create(float scale, int octaves, float persistence, float lacunarity, Vector2 offset)
    {
        return new VoxelTerrainNoiseSettings
        {
            scale = scale,
            octaves = octaves,
            persistence = persistence,
            lacunarity = lacunarity,
            offset = offset,
        };
    }
}

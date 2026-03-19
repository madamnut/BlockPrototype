using System;
using UnityEngine;

[Serializable]
public struct TerraDensitySettings
{
    [Min(0.0001f)] public float primaryScaleXZ;
    [Min(0.0001f)] public float primaryScaleY;
    [Min(0f)] public float primaryAmplitude;

    [Min(0.0001f)] public float detailScaleXZ;
    [Min(0.0001f)] public float detailScaleY;
    [Min(0f)] public float detailAmplitude;

    [Min(0.0001f)] public float caveScaleXZ;
    [Min(0.0001f)] public float caveScaleY;
    [Min(0f)] public float caveNoiseAmplitude;
    [Min(0f)] public float caveThreshold;
    [Min(0f)] public float caveCarveStrength;
    [Min(0)] public int caveMinY;
    [Min(0)] public int caveMaxY;
    [Min(0f)] public float caveFade;

    public float primarySeedOffset;
    public float detailSeedOffset;
    public float caveSeedOffset;

    public static TerraDensitySettings Default => new()
    {
        primaryScaleXZ = 0.028f,
        primaryScaleY = 0.024f,
        primaryAmplitude = 6f,
        detailScaleXZ = 0.065f,
        detailScaleY = 0.05f,
        detailAmplitude = 2.5f,
        caveScaleXZ = 0.045f,
        caveScaleY = 0.05f,
        caveNoiseAmplitude = 1f,
        caveThreshold = 0.16f,
        caveCarveStrength = 22f,
        caveMinY = 8,
        caveMaxY = 104,
        caveFade = 18f,
        primarySeedOffset = 421.37f,
        detailSeedOffset = 937.11f,
        caveSeedOffset = 1543.29f,
    };
}

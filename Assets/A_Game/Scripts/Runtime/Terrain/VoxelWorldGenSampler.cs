using Unity.Mathematics;
using UnityEngine;

public static class VoxelWorldGenSampler
{
    public static float SampleContinentalness(int worldX, int worldZ, int seed, in VoxelTerrainGenerationSettings settings)
    {
        float seedOffset = seed * 0.000031f;
        ApplyContinentalnessWarp(ref worldX, ref worldZ, seedOffset, settings);
        float baseContinentalness = SampleNoise(worldX, worldZ, settings.continentalness, seedOffset);
        float detailContinentalness = SampleNoise(
            worldX,
            worldZ,
            settings.continentalness.scale * Mathf.Max(1f, settings.continentalnessDetailScaleMultiplier),
            settings.continentalness.octaves,
            settings.continentalness.persistence,
            settings.continentalness.lacunarity,
            settings.continentalness.offset.x + 173.31f,
            settings.continentalness.offset.y + 91.77f,
            seedOffset);

        return ShapeContinentalness(
            baseContinentalness,
            detailContinentalness,
            settings.continentalnessDetailWeight,
            settings.continentalnessRemapMin,
            settings.continentalnessRemapMax);
    }

    public static void ApplyContinentalnessWarp(ref int worldX, ref int worldZ, float seedOffset, in VoxelTerrainGenerationSettings settings)
    {
        float warpScale = settings.continentalness.scale * Mathf.Clamp(settings.continentalnessWarpScaleMultiplier, 0.1f, 8f);
        float warpStrength = Mathf.Max(0f, settings.continentalnessWarpStrength);
        if (warpStrength <= 0.0001f)
        {
            return;
        }

        float warpX = SampleNoise(
            worldX,
            worldZ,
            warpScale,
            settings.continentalness.octaves,
            settings.continentalness.persistence,
            settings.continentalness.lacunarity,
            settings.continentalness.offset.x + 401.17f,
            settings.continentalness.offset.y + 233.91f,
            seedOffset);
        float warpZ = SampleNoise(
            worldX,
            worldZ,
            warpScale,
            settings.continentalness.octaves,
            settings.continentalness.persistence,
            settings.continentalness.lacunarity,
            settings.continentalness.offset.x + 719.43f,
            settings.continentalness.offset.y + 587.29f,
            seedOffset);

        worldX = Mathf.RoundToInt(worldX + ((warpX - 0.5f) * 2f * warpStrength));
        worldZ = Mathf.RoundToInt(worldZ + ((warpZ - 0.5f) * 2f * warpStrength));
    }

    public static float SampleNoise(int worldX, int worldZ, in VoxelTerrainNoiseSettings settings, float seedOffset)
    {
        return SampleNoise(
            worldX,
            worldZ,
            settings.scale,
            settings.octaves,
            settings.persistence,
            settings.lacunarity,
            settings.offset.x,
            settings.offset.y,
            seedOffset);
    }

    public static float SampleNoise(
        float worldX,
        float worldZ,
        float scale,
        int octaves,
        float persistence,
        float lacunarity,
        float offsetX,
        float offsetY,
        float seedOffset)
    {
        int octaveCount = Mathf.Max(1, octaves);
        float amplitude = 1f;
        float frequency = 1f;
        float total = 0f;
        float amplitudeSum = 0f;
        float safeScale = Mathf.Max(0.00001f, scale);
        float clampedPersistence = Mathf.Clamp01(persistence);
        float safeLacunarity = Mathf.Max(1f, lacunarity);

        for (int octave = 0; octave < octaveCount; octave++)
        {
            float sampleX = (worldX * safeScale * frequency) + offsetX + seedOffset;
            float sampleZ = (worldZ * safeScale * frequency) + offsetY + seedOffset;
            total += SamplePerlin2D(sampleX, sampleZ) * amplitude;
            amplitudeSum += amplitude;
            amplitude *= clampedPersistence;
            frequency *= safeLacunarity;
        }

        return amplitudeSum <= 0f ? 0.5f : total / amplitudeSum;
    }

    public static int ComposeSurfaceHeight(
        float continentalness,
        int minHeight,
        int maxHeight,
        float seaLevelThreshold,
        float continentalnessWeight)
    {
        float landness = Mathf.Clamp01(Mathf.InverseLerp(seaLevelThreshold, 1f, continentalness));
        float height01 = Mathf.Clamp01(landness * continentalnessWeight);

        int clampedMin = Mathf.Clamp(minHeight, 0, VoxelTerrainData.WorldHeight - 1);
        int clampedMax = Mathf.Clamp(Mathf.Max(minHeight, maxHeight), 0, VoxelTerrainData.WorldHeight - 1);
        return Mathf.RoundToInt(Mathf.Lerp(clampedMin, clampedMax, height01));
    }

    public static int SampleSurfaceHeight(int worldX, int worldZ, int seed, in VoxelTerrainGenerationSettings settings)
    {
        float continentalness = SampleContinentalness(worldX, worldZ, seed, settings);

        return ComposeSurfaceHeight(
            continentalness,
            settings.minTerrainHeight,
            settings.maxTerrainHeight,
            settings.continentalnessSeaLevel,
            settings.continentalnessWeight);
    }

    public static float ShapeContinentalness(
        float baseContinentalness,
        float detailContinentalness,
        float detailWeight,
        float remapMin,
        float remapMax)
    {
        float baseRemapped = Mathf.InverseLerp(remapMin, remapMax, baseContinentalness);
        float baseSmoothed = baseRemapped * baseRemapped * (3f - (2f * baseRemapped));

        float coastMask = 1f - Mathf.Abs((baseSmoothed * 2f) - 1f);
        float detailSigned = (detailContinentalness - 0.5f) * 2f;
        float coastAdjusted = Mathf.Clamp01(baseSmoothed + (detailSigned * Mathf.Clamp01(detailWeight) * coastMask));

        float centered = (coastAdjusted * 2f) - 1f;
        float contrasted = Mathf.Sign(centered) * Mathf.Pow(Mathf.Abs(centered), 0.72f);
        return Mathf.Clamp01((contrasted * 0.5f) + 0.5f);
    }

    private static float SamplePerlin2D(float x, float z)
    {
        float cellX = Mathf.Floor(x);
        float cellZ = Mathf.Floor(z);
        float localX = x - cellX;
        float localZ = z - cellZ;

        Gradient(cellX, cellZ, out float g00x, out float g00z);
        Gradient(cellX + 1f, cellZ, out float g10x, out float g10z);
        Gradient(cellX, cellZ + 1f, out float g01x, out float g01z);
        Gradient(cellX + 1f, cellZ + 1f, out float g11x, out float g11z);

        float n00 = (g00x * localX) + (g00z * localZ);
        float n10 = (g10x * (localX - 1f)) + (g10z * localZ);
        float n01 = (g01x * localX) + (g01z * (localZ - 1f));
        float n11 = (g11x * (localX - 1f)) + (g11z * (localZ - 1f));

        float fadeX = Fade(localX);
        float fadeZ = Fade(localZ);
        float nx0 = Mathf.Lerp(n00, n10, fadeX);
        float nx1 = Mathf.Lerp(n01, n11, fadeX);
        float nxy = Mathf.Lerp(nx0, nx1, fadeZ);
        return Mathf.Clamp01((nxy * 0.70710677f) + 0.5f);
    }

    private static float Fade(float value)
    {
        return value * value * value * (value * (value * 6f - 15f) + 10f);
    }

    private static void Gradient(float cellX, float cellZ, out float gx, out float gz)
    {
        uint hash = Hash((int)cellX, (int)cellZ);
        float angle = (hash / 4294967295f) * 6.28318530718f;
        gx = Mathf.Cos(angle);
        gz = Mathf.Sin(angle);
    }

    private static uint Hash(int xValue, int zValue)
    {
        uint x = (uint)xValue;
        uint z = (uint)zValue;
        uint h = (x * 374761393u) + (z * 668265263u);
        h = (h ^ (h >> 13)) * 1274126177u;
        return h ^ (h >> 16);
    }
}

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

[BurstCompile]
public static class WorldGenPreviewNoiseJobs
{
    [BurstCompile]
    public struct WorldSpacePerlinContinentalnessColorJob : IJobParallelFor
    {
        public int size;
        public float blocksPerPixel;
        public float scale;
        public int octaves;
        public float persistence;
        public float lacunarity;
        public float2 offset;
        public float seedOffset;
        public float seaLevelThreshold;
        public float warpScaleMultiplier;
        public float warpStrength;
        public float detailScaleMultiplier;
        public float detailWeight;
        public float remapMin;
        public float remapMax;
        public float oceanDeepR;
        public float oceanDeepG;
        public float oceanDeepB;
        public float oceanShallowR;
        public float oceanShallowG;
        public float oceanShallowB;
        public float landLowR;
        public float landLowG;
        public float landLowB;
        public float landHighR;
        public float landHighG;
        public float landHighB;

        [WriteOnly] public NativeArray<Color32> pixels;

        public void Execute(int index)
        {
            int x = index % size;
            int y = index / size;
            float worldX = (x + 0.5f) * blocksPerPixel;
            float worldZ = (y + 0.5f) * blocksPerPixel;
            ApplyContinentalnessWarp(ref worldX, ref worldZ);
            float baseValue = SampleFractalPerlin(worldX, worldZ, scale, offset.x, offset.y, seedOffset, octaves, persistence, lacunarity);
            float detailValue = SampleFractalPerlin(
                worldX,
                worldZ,
                scale * max(1f, detailScaleMultiplier),
                offset.x + 173.31f,
                offset.y + 91.77f,
                seedOffset,
                octaves,
                persistence,
                lacunarity);

            float clampedValue = ShapeContinentalness(
                baseValue,
                detailValue,
                detailWeight,
                remapMin,
                remapMax);

            float3 color;
            if (clampedValue < seaLevelThreshold)
            {
                float ocean01 = seaLevelThreshold <= 0.0001f ? 0f : saturate(clampedValue / seaLevelThreshold);
                color = lerp(
                    float3(oceanDeepR, oceanDeepG, oceanDeepB),
                    float3(oceanShallowR, oceanShallowG, oceanShallowB),
                    ocean01);
            }
            else
            {
                float land01 = seaLevelThreshold >= 0.9999f ? 1f : saturate(unlerp(seaLevelThreshold, 1f, clampedValue));
                color = lerp(
                    float3(landLowR, landLowG, landLowB),
                    float3(landHighR, landHighG, landHighB),
                    land01);
            }

            pixels[index] = new Color32(
                (byte)round(saturate(color.x) * 255f),
                (byte)round(saturate(color.y) * 255f),
                (byte)round(saturate(color.z) * 255f),
                255);
        }

        private static float ShapeContinentalness(
            float baseContinentalness,
            float detailContinentalness,
            float detailWeight,
            float remapMin,
            float remapMax)
        {
            float baseRemapped = saturate(unlerp(remapMin, remapMax, baseContinentalness));
            float baseSmoothed = baseRemapped * baseRemapped * (3f - (2f * baseRemapped));

            float coastMask = 1f - abs((baseSmoothed * 2f) - 1f);
            float detailSigned = (detailContinentalness - 0.5f) * 2f;
            float coastAdjusted = saturate(baseSmoothed + (detailSigned * saturate(detailWeight) * coastMask));

            float centered = (coastAdjusted * 2f) - 1f;
            float contrasted = sign(centered) * pow(abs(centered), 0.72f);
            return saturate((contrasted * 0.5f) + 0.5f);
        }

        private void ApplyContinentalnessWarp(ref float worldX, ref float worldZ)
        {
            float safeWarpStrength = max(0f, warpStrength);
            if (safeWarpStrength <= 0.0001f)
            {
                return;
            }

            float warpScale = scale * clamp(warpScaleMultiplier, 0.1f, 8f);
            float warpX = SampleFractalPerlin(
                worldX,
                worldZ,
                warpScale,
                offset.x + 401.17f,
                offset.y + 233.91f,
                seedOffset,
                octaves,
                persistence,
                lacunarity);
            float warpZ = SampleFractalPerlin(
                worldX,
                worldZ,
                warpScale,
                offset.x + 719.43f,
                offset.y + 587.29f,
                seedOffset,
                octaves,
                persistence,
                lacunarity);

            worldX += ((warpX - 0.5f) * 2f) * safeWarpStrength;
            worldZ += ((warpZ - 0.5f) * 2f) * safeWarpStrength;
        }
    }

    public static float SampleFractalPerlin(
        float worldX,
        float worldZ,
        float scale,
        float offsetX,
        float offsetY,
        float seedOffset,
        int octaves,
        float persistence,
        float lacunarity)
    {
        int octaveCount = max(1, octaves);
        float amplitude = 1f;
        float frequency = 1f;
        float total = 0f;
        float amplitudeSum = 0f;
        float safeScale = max(0.00001f, scale);
        float clampedPersistence = clamp(persistence, 0f, 1f);
        float clampedLacunarity = max(1f, lacunarity);

        for (int octave = 0; octave < octaveCount; octave++)
        {
            float sampleX = (worldX * safeScale * frequency) + offsetX + seedOffset;
            float sampleZ = (worldZ * safeScale * frequency) + offsetY + seedOffset;
            total += SamplePerlin2D(sampleX, sampleZ) * amplitude;
            amplitudeSum += amplitude;
            amplitude *= clampedPersistence;
            frequency *= clampedLacunarity;
        }

        return amplitudeSum <= 0f ? 0.5f : total / amplitudeSum;
    }

    private static float SamplePerlin2D(float x, float z)
    {
        float cellX = floor(x);
        float cellZ = floor(z);
        float localX = frac(x);
        float localZ = frac(z);

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
        float nx0 = lerp(n00, n10, fadeX);
        float nx1 = lerp(n01, n11, fadeX);
        float nxy = lerp(nx0, nx1, fadeZ);
        return saturate((nxy * 0.70710677f) + 0.5f);
    }

    private static float Fade(float t)
    {
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    private static void Gradient(float cellX, float cellZ, out float gx, out float gz)
    {
        uint hash = Hash((int)cellX, (int)cellZ);
        float angle = (hash / 4294967295f) * 6.28318530718f;
        gx = cos(angle);
        gz = sin(angle);
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

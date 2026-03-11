using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;
using static Unity.Mathematics.noise;

[BurstCompile]
public static class WorldGenPreviewJobs
{
    [BurstCompile]
    public struct PerlinMapJob : IJobParallelFor
    {
        public int size;
        public float scale;
        public int octaves;
        public float persistence;
        public float lacunarity;
        public float2 offset;
        public float seedOffset;
        public bool useErosion;
        public float erosionScale;
        public int erosionOctaves;
        public float erosionPersistence;
        public float erosionLacunarity;
        public float2 erosionOffset;
        public float smoothColorR;
        public float smoothColorG;
        public float smoothColorB;
        public float ruggedColorR;
        public float ruggedColorG;
        public float ruggedColorB;
        public float erosionOverlayStrength;

        [WriteOnly] public NativeArray<Color32> pixels;

        public void Execute(int index)
        {
            int x = index % size;
            int y = index / size;
            float continentalness = SampleFractalPerlin(x, y, scale, offset.x, offset.y, seedOffset, octaves, persistence, lacunarity);
            float baseValue = clamp(continentalness, 0f, 1f);

            if (!useErosion)
            {
                byte channel = (byte)round(baseValue * 255f);
                pixels[index] = new Color32(channel, channel, channel, 255);
                return;
            }

            float erosion = SampleFractalPerlin(x, y, erosionScale, erosionOffset.x, erosionOffset.y, seedOffset, erosionOctaves, erosionPersistence, erosionLacunarity);
            float smoothWeight = clamp(erosion, 0f, 1f);
            float ruggedWeight = 1f - smoothWeight;
            float strength = clamp(erosionOverlayStrength, 0f, 1f);

            float tintR = (ruggedColorR * ruggedWeight) + (smoothColorR * smoothWeight);
            float tintG = (ruggedColorG * ruggedWeight) + (smoothColorG * smoothWeight);
            float tintB = (ruggedColorB * ruggedWeight) + (smoothColorB * smoothWeight);

            float mixedR = lerp(baseValue, saturate(baseValue * 0.6f + tintR * 0.4f), strength);
            float mixedG = lerp(baseValue, saturate(baseValue * 0.6f + tintG * 0.4f), strength);
            float mixedB = lerp(baseValue, saturate(baseValue * 0.6f + tintB * 0.4f), strength);

            pixels[index] = new Color32(
                (byte)round(clamp(mixedR, 0f, 1f) * 255f),
                (byte)round(clamp(mixedG, 0f, 1f) * 255f),
                (byte)round(clamp(mixedB, 0f, 1f) * 255f),
                255);
        }
    }

    [BurstCompile]
    public struct SimplexMapJob : IJobParallelFor
    {
        public int size;
        public float scale;
        public int octaves;
        public float persistence;
        public float lacunarity;
        public float2 offset;
        public float seedOffset;
        public bool useErosion;
        public float erosionScale;
        public int erosionOctaves;
        public float erosionPersistence;
        public float erosionLacunarity;
        public float2 erosionOffset;
        public float smoothColorR;
        public float smoothColorG;
        public float smoothColorB;
        public float ruggedColorR;
        public float ruggedColorG;
        public float ruggedColorB;
        public float erosionOverlayStrength;

        [WriteOnly] public NativeArray<Color32> pixels;

        public void Execute(int index)
        {
            int x = index % size;
            int y = index / size;
            float continentalness = SampleFractalSimplex(x, y, scale, offset.x, offset.y, seedOffset, octaves, persistence, lacunarity);
            float baseValue = clamp(continentalness, 0f, 1f);

            if (!useErosion)
            {
                byte channel = (byte)round(baseValue * 255f);
                pixels[index] = new Color32(channel, channel, channel, 255);
                return;
            }

            float erosion = SampleFractalSimplex(x, y, erosionScale, erosionOffset.x, erosionOffset.y, seedOffset, erosionOctaves, erosionPersistence, erosionLacunarity);
            float smoothWeight = clamp(erosion, 0f, 1f);
            float ruggedWeight = 1f - smoothWeight;
            float strength = clamp(erosionOverlayStrength, 0f, 1f);

            float tintR = (ruggedColorR * ruggedWeight) + (smoothColorR * smoothWeight);
            float tintG = (ruggedColorG * ruggedWeight) + (smoothColorG * smoothWeight);
            float tintB = (ruggedColorB * ruggedWeight) + (smoothColorB * smoothWeight);

            float mixedR = lerp(baseValue, saturate(baseValue * 0.6f + tintR * 0.4f), strength);
            float mixedG = lerp(baseValue, saturate(baseValue * 0.6f + tintG * 0.4f), strength);
            float mixedB = lerp(baseValue, saturate(baseValue * 0.6f + tintB * 0.4f), strength);

            pixels[index] = new Color32(
                (byte)round(clamp(mixedR, 0f, 1f) * 255f),
                (byte)round(clamp(mixedG, 0f, 1f) * 255f),
                (byte)round(clamp(mixedB, 0f, 1f) * 255f),
                255);
        }
    }

    [BurstCompile]
    public struct LloydRelaxationPassJob : IJob
    {
        public int size;

        [ReadOnly] public NativeArray<float2> inputSites;
        public NativeArray<float2> outputSites;
        public NativeArray<float2> sums;
        public NativeArray<int> counts;

        public void Execute()
        {
            int siteCount = inputSites.Length;

            for (int index = 0; index < siteCount; index++)
            {
                sums[index] = 0f;
                counts[index] = 0;
            }

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int nearestIndex = 0;
                    float nearestDistance = float.MaxValue;

                    for (int siteIndex = 0; siteIndex < inputSites.Length; siteIndex++)
                    {
                        float dx = x - inputSites[siteIndex].x;
                        float dy = y - inputSites[siteIndex].y;
                        float distance = (dx * dx) + (dy * dy);

                        if (distance < nearestDistance)
                        {
                            nearestDistance = distance;
                            nearestIndex = siteIndex;
                        }
                    }

                    sums[nearestIndex] += new float2(x, y);
                    counts[nearestIndex] += 1;
                }
            }

            for (int index = 0; index < siteCount; index++)
            {
                if (counts[index] <= 0)
                {
                    outputSites[index] = inputSites[index];
                    continue;
                }

                float2 centroid = sums[index] / counts[index];
                outputSites[index] = clamp(centroid, 0f, size - 1f);
            }
        }
    }

    [BurstCompile]
    public struct VoronoiRasterJob : IJobParallelFor
    {
        public int size;
        public byte backgroundR;
        public byte backgroundG;
        public byte backgroundB;
        public byte backgroundA;
        public bool useBorderNoise;
        public float borderNoiseScale;
        public float borderNoiseStrength;
        public int borderNoiseOctaves;
        public float borderNoisePersistence;
        public float borderNoiseLacunarity;
        public float2 borderNoiseOffsetA;
        public float2 borderNoiseOffsetB;
        public float seedOffset;

        [ReadOnly] public NativeArray<float2> sites;
        [WriteOnly] public NativeArray<int> cellIndices;
        [WriteOnly] public NativeArray<Color32> pixels;

        public void Execute(int index)
        {
            int x = index % size;
            int y = index / size;
            float2 point = new float2(x, y);

            if (useBorderNoise)
            {
                float noiseX = SampleFractalPerlin(x, y, borderNoiseScale, borderNoiseOffsetA.x, borderNoiseOffsetA.y, seedOffset, borderNoiseOctaves, borderNoisePersistence, borderNoiseLacunarity);
                float noiseY = SampleFractalPerlin(x, y, borderNoiseScale, borderNoiseOffsetB.x, borderNoiseOffsetB.y, seedOffset, borderNoiseOctaves, borderNoisePersistence, borderNoiseLacunarity);
                point.x += (noiseX - 0.5f) * 2f * borderNoiseStrength;
                point.y += (noiseY - 0.5f) * 2f * borderNoiseStrength;
            }

            int nearestIndex = 0;
            float nearestDistance = float.MaxValue;

            for (int siteIndex = 0; siteIndex < sites.Length; siteIndex++)
            {
                float dx = point.x - sites[siteIndex].x;
                float dy = point.y - sites[siteIndex].y;
                float distance = (dx * dx) + (dy * dy);

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = siteIndex;
                }
            }

            cellIndices[index] = nearestIndex;
            pixels[index] = new Color32(backgroundR, backgroundG, backgroundB, backgroundA);
        }
    }

    [BurstCompile]
    public struct VoronoiBoundaryJob : IJobParallelFor
    {
        public int size;
        public bool drawBoundaries;
        public byte lineR;
        public byte lineG;
        public byte lineB;
        public byte lineA;

        [ReadOnly] public NativeArray<int> cellIndices;
        public NativeArray<Color32> pixels;

        public void Execute(int index)
        {
            if (!drawBoundaries)
            {
                return;
            }

            int x = index % size;
            int y = index / size;

            if (x <= 0 || x >= size - 1 || y <= 0 || y >= size - 1)
            {
                return;
            }

            int cellIndex = cellIndices[index];
            if (cellIndices[index - 1] != cellIndex ||
                cellIndices[index + 1] != cellIndex ||
                cellIndices[index - size] != cellIndex ||
                cellIndices[index + size] != cellIndex)
            {
                pixels[index] = new Color32(lineR, lineG, lineB, lineA);
            }
        }
    }

    [BurstCompile]
    public static float SampleFractalPerlin(float x, float y, float scale, float offsetX, float offsetY, float seedOffset, int octaves, float persistence, float lacunarity)
    {
        int octaveCount = max(1, octaves);
        float amplitude = 1f;
        float frequency = 1f;
        float total = 0f;
        float amplitudeSum = 0f;
        float clampedPersistence = saturate(persistence);
        float clampedLacunarity = max(1f, lacunarity);
        float safeScale = max(0.00001f, scale);

        for (int octave = 0; octave < octaveCount; octave++)
        {
            float sampleX = (x * safeScale * frequency) + offsetX + seedOffset;
            float sampleY = (y * safeScale * frequency) + offsetY + seedOffset;
            float noiseValue = Perlin2D(sampleX, sampleY);
            total += noiseValue * amplitude;
            amplitudeSum += amplitude;
            amplitude *= clampedPersistence;
            frequency *= clampedLacunarity;
        }

        return amplitudeSum <= 0f ? 0.5f : total / amplitudeSum;
    }

    [BurstCompile]
    public static float SampleFractalSimplex(float x, float y, float scale, float offsetX, float offsetY, float seedOffset, int octaves, float persistence, float lacunarity)
    {
        int octaveCount = max(1, octaves);
        float amplitude = 1f;
        float frequency = 1f;
        float total = 0f;
        float amplitudeSum = 0f;
        float clampedPersistence = saturate(persistence);
        float clampedLacunarity = max(1f, lacunarity);
        float safeScale = max(0.00001f, scale);

        for (int octave = 0; octave < octaveCount; octave++)
        {
            float2 samplePoint = new float2(
                (x * safeScale * frequency) + offsetX + seedOffset,
                (y * safeScale * frequency) + offsetY + seedOffset);
            float noiseValue = (snoise(samplePoint) * 0.5f) + 0.5f;
            total += noiseValue * amplitude;
            amplitudeSum += amplitude;
            amplitude *= clampedPersistence;
            frequency *= clampedLacunarity;
        }

        return amplitudeSum <= 0f ? 0.5f : total / amplitudeSum;
    }

    [BurstCompile]
    private static float Perlin2D(float x, float y)
    {
        float cellX = floor(x);
        float cellY = floor(y);
        float localX = frac(x);
        float localY = frac(y);

        Gradient(cellX, cellY, out float g00x, out float g00y);
        Gradient(cellX + 1f, cellY, out float g10x, out float g10y);
        Gradient(cellX, cellY + 1f, out float g01x, out float g01y);
        Gradient(cellX + 1f, cellY + 1f, out float g11x, out float g11y);

        float n00 = (g00x * localX) + (g00y * localY);
        float n10 = (g10x * (localX - 1f)) + (g10y * localY);
        float n01 = (g01x * localX) + (g01y * (localY - 1f));
        float n11 = (g11x * (localX - 1f)) + (g11y * (localY - 1f));

        float fadeX = localX * localX * localX * (localX * (localX * 6f - 15f) + 10f);
        float fadeY = localY * localY * localY * (localY * (localY * 6f - 15f) + 10f);
        float nx0 = lerp(n00, n10, fadeX);
        float nx1 = lerp(n01, n11, fadeX);
        float nxy = lerp(nx0, nx1, fadeY);
        return saturate((nxy * 0.70710677f) + 0.5f);
    }

    [BurstCompile]
    private static void Gradient(float cellX, float cellY, out float gx, out float gy)
    {
        uint hash = Hash((int)cellX, (int)cellY);
        float angle = (hash / 4294967295f) * 6.28318530718f;
        gx = cos(angle);
        gy = sin(angle);
    }

    [BurstCompile]
    private static uint Hash(int xValue, int yValue)
    {
        uint x = (uint)xValue;
        uint y = (uint)yValue;
        uint h = x * 374761393u + y * 668265263u;
        h = (h ^ (h >> 13)) * 1274126177u;
        return h ^ (h >> 16);
    }

}

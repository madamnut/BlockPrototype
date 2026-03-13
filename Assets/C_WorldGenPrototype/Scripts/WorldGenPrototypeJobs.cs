using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[System.Serializable]
public struct ContinentalnessSettings
{
    public bool useWarp;
    public int warpOctaves;
    public float warpFrequency;
    public float warpAmplitude;
    public float warpLacunarity;
    public float warpGain;

    public bool useMacro;
    public int macroOctaves;
    public float macroFrequency;
    public float macroLacunarity;
    public float macroGain;
    public float macroWeight;

    public bool useBroad;
    public int broadOctaves;
    public float broadFrequency;
    public float broadLacunarity;
    public float broadGain;
    public float broadWeight;

    public bool useDetail;
    public int detailOctaves;
    public float detailFrequency;
    public float detailLacunarity;
    public float detailGain;
    public float detailWeight;

    public float abyssUpperBound;
    public float deepOceanUpperBound;
    public float oceanUpperBound;
    public float coastUpperBound;
    public float inlandUpperBound;
    public float deepInlandUpperBound;

    public Color32 abyssColor;
    public Color32 deepOceanColor;
    public Color32 oceanColor;
    public Color32 shallowOceanColor;
    public Color32 coastColor;
    public Color32 inlandColor;
    public Color32 deepInlandColor;
    public Color32 continentalCoreColor;
}

[BurstCompile]
public static class WorldGenPrototypeJobs
{
    public const int ChunkSizeInBlocks = 16;
    public const int RegionSizeInChunks = 16;
    public const int RegionSizeInBlocks = ChunkSizeInBlocks * RegionSizeInChunks;
    public const int SectorSizeInRegions = 16;
    public const int SectorSizeInBlocks = RegionSizeInBlocks * SectorSizeInRegions;
    public const int SectorSizeInChunks = RegionSizeInChunks * SectorSizeInRegions;

    [BurstCompile]
    public struct ContinentalnessPreviewJob : IJobParallelFor
    {
        public int size;
        public int seed;
        public int sectorIndexX;
        public int sectorIndexZ;
        public bool useCdfRemap;
        public ContinentalnessSettings settings;

        [ReadOnly] public NativeArray<float> cdfLut;
        [WriteOnly] public NativeArray<Color32> pixels;
        [WriteOnly] public NativeArray<float> values;

        public void Execute(int index)
        {
            int localX = index % size;
            int localZ = index / size;
            int worldBlockX = (sectorIndexX * SectorSizeInBlocks) + localX;
            int worldBlockZ = (sectorIndexZ * SectorSizeInBlocks) + localZ;
            float worldRegionX = worldBlockX / (float)RegionSizeInBlocks;
            float worldRegionZ = worldBlockZ / (float)RegionSizeInBlocks;

            float rawContinentalness = SampleRawContinentalness(seed, worldRegionX, worldRegionZ, settings);
            float continentalness = RemapRawContinentalness(rawContinentalness, useCdfRemap, cdfLut);
            values[index] = continentalness;
            pixels[index] = EvaluateContinentalnessColor(continentalness, settings);
        }
    }

    [BurstCompile]
    public struct RawContinentalnessSampleJob : IJobParallelFor
    {
        public int sampleSeed;
        public int startIndex;
        public int sectorRange;
        public ContinentalnessSettings settings;

        [WriteOnly] public NativeSlice<float> samples;

        public void Execute(int index)
        {
            int sampleIndex = startIndex + index;
            uint baseSalt = ComputeHash(sampleSeed, sampleIndex, 0x0B5297A4, 0x9E3779B9u);
            int worldSeed = (int)ComputeHash(sampleSeed, sampleIndex, 0x1F123BB5, baseSalt);
            int sectorSpan = math.max(1, (sectorRange * 2) + 1);
            int sectorX = (int)(ComputeHash(sampleSeed, sampleIndex, 0x5F3759DF, baseSalt ^ 0xA341316Cu) % (uint)sectorSpan) - sectorRange;
            int sectorZ = (int)(ComputeHash(sampleSeed, sampleIndex, unchecked((int)0x94D049BB), baseSalt ^ 0xC8013EA4u) % (uint)sectorSpan) - sectorRange;
            float localRegionX = HashToUnit(sampleSeed, sampleIndex, unchecked((int)0x632BE59B), baseSalt ^ 0x85EBCA77u) * SectorSizeInRegions;
            float localRegionZ = HashToUnit(sampleSeed, sampleIndex, unchecked((int)0x85157AF5), baseSalt ^ 0x27D4EB2Fu) * SectorSizeInRegions;
            float worldRegionX = (sectorX * SectorSizeInRegions) + localRegionX;
            float worldRegionZ = (sectorZ * SectorSizeInRegions) + localRegionZ;
            samples[index] = SampleRawContinentalness(worldSeed, worldRegionX, worldRegionZ, settings);
        }
    }

    [BurstCompile]
    public struct FloatStatsJob : IJob
    {
        [ReadOnly] public NativeArray<float> values;
        [WriteOnly] public NativeArray<float> stats;

        public void Execute()
        {
            if (values.Length == 0)
            {
                stats[0] = 0f;
                stats[1] = 0f;
                stats[2] = 0f;
                return;
            }

            float min = float.MaxValue;
            float max = float.MinValue;
            float sum = 0f;

            for (int i = 0; i < values.Length; i++)
            {
                float value = values[i];
                min = math.min(min, value);
                max = math.max(max, value);
                sum += value;
            }

            stats[0] = min;
            stats[1] = max;
            stats[2] = sum / values.Length;
        }
    }

    [BurstCompile]
    public struct GridOverlayJob : IJobParallelFor
    {
        public int width;
        public int height;
        public int divisionCount;
        public int lineWidthX;
        public int lineWidthY;
        public Color32 lineColor;

        [WriteOnly] public NativeArray<Color32> pixels;

        public void Execute(int index)
        {
            int x = index % width;
            int y = index / width;
            bool onVertical = IsOnGridLine(x, width, divisionCount, lineWidthX);
            bool onHorizontal = IsOnGridLine(y, height, divisionCount, lineWidthY);
            pixels[index] = onVertical || onHorizontal ? lineColor : default;
        }
    }

    public static float SampleContinentalness(int seed, float worldRegionX, float worldRegionZ, ContinentalnessSettings settings)
    {
        return RemapRawContinentalness(SampleRawContinentalness(seed, worldRegionX, worldRegionZ, settings), false, default);
    }

    public static float SampleRawContinentalness(int seed, float worldRegionX, float worldRegionZ, ContinentalnessSettings settings)
    {
        float2 p = new float2(worldRegionX, worldRegionZ);
        float2 warped = p;

        if (settings.useWarp)
        {
            float warpX = (FractalPerlinNoise(seed, p * settings.warpFrequency, settings.warpOctaves, settings.warpLacunarity, settings.warpGain, 0xA341316Cu) - 0.5f) * settings.warpAmplitude;
            float warpZ = (FractalPerlinNoise(seed, (p + new float2(173f, 59f)) * settings.warpFrequency, settings.warpOctaves, settings.warpLacunarity, settings.warpGain, 0xC8013EA4u) - 0.5f) * settings.warpAmplitude;
            warped = p + new float2(warpX, warpZ);
        }

        float macro = settings.useMacro
            ? FractalPerlinNoise(seed, warped * settings.macroFrequency, settings.macroOctaves, settings.macroLacunarity, settings.macroGain, 0x85EBCA77u)
            : 0f;
        float broad = settings.useBroad
            ? FractalPerlinNoise(seed, warped * settings.broadFrequency, settings.broadOctaves, settings.broadLacunarity, settings.broadGain, 0x9E3779B9u)
            : 0f;
        float detail = settings.useDetail
            ? FractalPerlinNoise(seed, warped * settings.detailFrequency, settings.detailOctaves, settings.detailLacunarity, settings.detailGain, 0x27D4EB2Fu)
            : 0f;

        float macroWeight = settings.useMacro ? settings.macroWeight : 0f;
        float broadWeight = settings.useBroad ? settings.broadWeight : 0f;
        float detailWeight = settings.useDetail ? settings.detailWeight : 0f;
        float weightSum = macroWeight + broadWeight + detailWeight;
        if (weightSum <= 0.0001f)
        {
            return 0.5f;
        }

        float continentalness = ((macro * macroWeight) + (broad * broadWeight) + (detail * detailWeight)) / weightSum;
        return math.saturate(continentalness);
    }

    private static float RemapRawContinentalness(float rawContinentalness, bool useCdfRemap, NativeArray<float> cdfLut)
    {
        float normalized = math.saturate(rawContinentalness);
        if (useCdfRemap && cdfLut.IsCreated && cdfLut.Length > 1)
        {
            float scaledIndex = normalized * (cdfLut.Length - 1);
            int lowerIndex = (int)math.floor(scaledIndex);
            int upperIndex = math.min(lowerIndex + 1, cdfLut.Length - 1);
            float t = scaledIndex - lowerIndex;
            normalized = math.lerp(cdfLut[lowerIndex], cdfLut[upperIndex], t);
        }

        return (normalized * 2f) - 1f;
    }

    private static Color32 EvaluateContinentalnessColor(float value, ContinentalnessSettings settings)
    {
        if (value < settings.abyssUpperBound)
        {
            return settings.abyssColor;
        }

        if (value < settings.deepOceanUpperBound)
        {
            return settings.deepOceanColor;
        }

        if (value < settings.oceanUpperBound)
        {
            return settings.oceanColor;
        }

        if (value < 0f)
        {
            return settings.shallowOceanColor;
        }

        if (value < settings.coastUpperBound)
        {
            return settings.coastColor;
        }

        if (value < settings.inlandUpperBound)
        {
            return settings.inlandColor;
        }

        if (value < settings.deepInlandUpperBound)
        {
            return settings.deepInlandColor;
        }

        return settings.continentalCoreColor;
    }

    private static float FractalPerlinNoise(int seed, float2 point, int octaves, float lacunarity, float gain, uint salt)
    {
        float amplitude = 1f;
        float frequency = 1f;
        float total = 0f;
        float normalization = 0f;
        float2 seedOffset = CreateSeedOffset(seed, salt);

        for (int octave = 0; octave < octaves; octave++)
        {
            uint octaveSalt = salt + (uint)(octave * 0x9E3779B9u);
            float2 octaveOffset = seedOffset + CreateSeedOffset(seed ^ (int)octaveSalt, octaveSalt);
            float perlin = noise.cnoise((point * frequency) + octaveOffset);
            total += amplitude * ((perlin * 0.5f) + 0.5f);
            normalization += amplitude;
            amplitude *= gain;
            frequency *= lacunarity;
        }

        return normalization <= 0f ? 0f : total / normalization;
    }

    private static float2 CreateSeedOffset(int seed, uint salt)
    {
        uint hashX = ComputeHash(seed, 0x1234ABCD, unchecked((int)0x55AA11EE), salt);
        uint hashZ = ComputeHash(seed, 0x0F1E2D3C, unchecked((int)0x66778899), salt ^ 0x9E3779B9u);
        float offsetX = ((hashX & 0x00FFFFFFu) / 16777215f) * 4096f;
        float offsetZ = ((hashZ & 0x00FFFFFFu) / 16777215f) * 4096f;
        return new float2(offsetX, offsetZ);
    }

    private static float HashToUnit(int seed, int x, int z, uint salt)
    {
        uint hash = ComputeHash(seed, x, z, salt);
        return (hash & 0x00FFFFFFu) / 16777215f;
    }

    private static uint ComputeHash(int seed, int x, int z, uint salt)
    {
        unchecked
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)seed) * 16777619u;
            hash = (hash ^ (uint)x) * 16777619u;
            hash = (hash ^ (uint)z) * 16777619u;
            hash = (hash ^ salt) * 16777619u;
            hash ^= hash >> 16;
            hash *= 2246822519u;
            hash ^= hash >> 13;
            return hash;
        }
    }

    private static bool IsOnGridLine(int position, int max, int divisions, int lineWidth)
    {
        int safeDivisions = math.max(1, divisions);
        int safeLineWidth = math.max(1, lineWidth);

        for (int offset = 0; offset < safeLineWidth; offset++)
        {
            int samplePosition = position - offset;
            if (samplePosition <= 0 || samplePosition >= max - 1)
            {
                return true;
            }

            int previousBucket = ((samplePosition - 1) * safeDivisions) / max;
            int currentBucket = (samplePosition * safeDivisions) / max;
            if (previousBucket != currentBucket)
            {
                return true;
            }
        }

        return false;
    }
}

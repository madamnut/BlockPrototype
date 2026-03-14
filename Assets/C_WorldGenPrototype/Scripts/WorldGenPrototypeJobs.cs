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

[System.Serializable]
public struct ErosionSettings
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
}

[System.Serializable]
public struct RidgesSettings
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
}

[System.Serializable]
public struct TemperatureSettings
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
}

[System.Serializable]
public struct PrecipitationSettings
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
            float continentalness = RemapRawNoise(rawContinentalness, useCdfRemap, cdfLut);
            values[index] = continentalness;
            pixels[index] = EvaluateContinentalnessColor(continentalness, settings);
        }
    }

    [BurstCompile]
    public struct ErosionPreviewJob : IJobParallelFor
    {
        public int size;
        public int seed;
        public int sectorIndexX;
        public int sectorIndexZ;
        public bool useCdfRemap;
        public ErosionSettings settings;

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

            float rawErosion = SampleRawErosion(seed, worldRegionX, worldRegionZ, settings);
            float erosion = RemapRawNoise(rawErosion, useCdfRemap, cdfLut);
            values[index] = erosion;
            pixels[index] = EvaluateErosionColor(erosion);
        }
    }

    [BurstCompile]
    public struct RidgesPreviewJob : IJobParallelFor
    {
        public int size;
        public int seed;
        public int sectorIndexX;
        public int sectorIndexZ;
        public bool useCdfRemap;
        public RidgesSettings settings;

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

            float rawRidges = SampleRawRidges(seed, worldRegionX, worldRegionZ, settings);
            float ridges = RemapRawNoise(rawRidges, useCdfRemap, cdfLut);
            values[index] = ridges;
            pixels[index] = EvaluateRidgesColor(ridges);
        }
    }

    [BurstCompile]
    public struct TemperaturePreviewJob : IJobParallelFor
    {
        public int size;
        public int seed;
        public int sectorIndexX;
        public int sectorIndexZ;
        public bool useCdfRemap;
        public TemperatureSettings settings;

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

            float rawTemperature = SampleRawTemperature(seed, worldRegionX, worldRegionZ, settings);
            float temperature = RemapRawNoise(rawTemperature, useCdfRemap, cdfLut);
            values[index] = temperature;
            pixels[index] = EvaluateScalarColor(temperature);
        }
    }

    [BurstCompile]
    public struct PrecipitationPreviewJob : IJobParallelFor
    {
        public int size;
        public int seed;
        public int sectorIndexX;
        public int sectorIndexZ;
        public bool useCdfRemap;
        public PrecipitationSettings settings;

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

            float rawPrecipitation = SampleRawPrecipitation(seed, worldRegionX, worldRegionZ, settings);
            float precipitation = RemapRawNoise(rawPrecipitation, useCdfRemap, cdfLut);
            values[index] = precipitation;
            pixels[index] = EvaluateScalarColor(precipitation);
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
    public struct RawErosionSampleJob : IJobParallelFor
    {
        public int sampleSeed;
        public int startIndex;
        public int sectorRange;
        public ErosionSettings settings;

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
            samples[index] = SampleRawErosion(worldSeed, worldRegionX, worldRegionZ, settings);
        }
    }

    [BurstCompile]
    public struct RawRidgesSampleJob : IJobParallelFor
    {
        public int sampleSeed;
        public int startIndex;
        public int sectorRange;
        public RidgesSettings settings;

        [WriteOnly] public NativeSlice<float> samples;

        public void Execute(int index)
        {
            int sampleIndex = startIndex + index;
            uint baseSalt = ComputeHash(sampleSeed, sampleIndex, unchecked((int)0x3C6EF372), 0x9E3779B9u);
            int worldSeed = (int)ComputeHash(sampleSeed, sampleIndex, unchecked((int)0xDAA66D2B), baseSalt);
            int sectorSpan = math.max(1, (sectorRange * 2) + 1);
            int sectorX = (int)(ComputeHash(sampleSeed, sampleIndex, unchecked((int)0x7F4A7C15), baseSalt ^ 0xA341316Cu) % (uint)sectorSpan) - sectorRange;
            int sectorZ = (int)(ComputeHash(sampleSeed, sampleIndex, unchecked((int)0x91E10DA5), baseSalt ^ 0xC8013EA4u) % (uint)sectorSpan) - sectorRange;
            float localRegionX = HashToUnit(sampleSeed, sampleIndex, unchecked((int)0x52DCE729), baseSalt ^ 0x85EBCA77u) * SectorSizeInRegions;
            float localRegionZ = HashToUnit(sampleSeed, sampleIndex, unchecked((int)0x38495AB5), baseSalt ^ 0x27D4EB2Fu) * SectorSizeInRegions;
            float worldRegionX = (sectorX * SectorSizeInRegions) + localRegionX;
            float worldRegionZ = (sectorZ * SectorSizeInRegions) + localRegionZ;
            samples[index] = SampleRawRidges(worldSeed, worldRegionX, worldRegionZ, settings);
        }
    }

    [BurstCompile]
    public struct RawTemperatureSampleJob : IJobParallelFor
    {
        public int sampleSeed;
        public int startIndex;
        public int sectorRange;
        public TemperatureSettings settings;

        [WriteOnly] public NativeSlice<float> samples;

        public void Execute(int index)
        {
            int sampleIndex = startIndex + index;
            uint baseSalt = ComputeHash(sampleSeed, sampleIndex, unchecked((int)0x2C1B3C6D), 0x9E3779B9u);
            int worldSeed = (int)ComputeHash(sampleSeed, sampleIndex, unchecked((int)0x7A4E6A13), baseSalt);
            int sectorSpan = math.max(1, (sectorRange * 2) + 1);
            int sectorX = (int)(ComputeHash(sampleSeed, sampleIndex, unchecked((int)0x5C8F1E2D), baseSalt ^ 0xA341316Cu) % (uint)sectorSpan) - sectorRange;
            int sectorZ = (int)(ComputeHash(sampleSeed, sampleIndex, unchecked((int)0x6E2F9B17), baseSalt ^ 0xC8013EA4u) % (uint)sectorSpan) - sectorRange;
            float localRegionX = HashToUnit(sampleSeed, sampleIndex, unchecked((int)0x1D73A5C9), baseSalt ^ 0x85EBCA77u) * SectorSizeInRegions;
            float localRegionZ = HashToUnit(sampleSeed, sampleIndex, unchecked((int)0x4F61B8E3), baseSalt ^ 0x27D4EB2Fu) * SectorSizeInRegions;
            float worldRegionX = (sectorX * SectorSizeInRegions) + localRegionX;
            float worldRegionZ = (sectorZ * SectorSizeInRegions) + localRegionZ;
            samples[index] = SampleRawTemperature(worldSeed, worldRegionX, worldRegionZ, settings);
        }
    }

    [BurstCompile]
    public struct RawPrecipitationSampleJob : IJobParallelFor
    {
        public int sampleSeed;
        public int startIndex;
        public int sectorRange;
        public PrecipitationSettings settings;

        [WriteOnly] public NativeSlice<float> samples;

        public void Execute(int index)
        {
            int sampleIndex = startIndex + index;
            uint baseSalt = ComputeHash(sampleSeed, sampleIndex, unchecked((int)0x4A7C159B), 0x9E3779B9u);
            int worldSeed = (int)ComputeHash(sampleSeed, sampleIndex, unchecked((int)0x319642B5), baseSalt);
            int sectorSpan = math.max(1, (sectorRange * 2) + 1);
            int sectorX = (int)(ComputeHash(sampleSeed, sampleIndex, unchecked((int)0x8E9D5A21), baseSalt ^ 0xA341316Cu) % (uint)sectorSpan) - sectorRange;
            int sectorZ = (int)(ComputeHash(sampleSeed, sampleIndex, unchecked((int)0x73B24D19), baseSalt ^ 0xC8013EA4u) % (uint)sectorSpan) - sectorRange;
            float localRegionX = HashToUnit(sampleSeed, sampleIndex, unchecked((int)0x2654C6F1), baseSalt ^ 0x85EBCA77u) * SectorSizeInRegions;
            float localRegionZ = HashToUnit(sampleSeed, sampleIndex, unchecked((int)0x5AB91E47), baseSalt ^ 0x27D4EB2Fu) * SectorSizeInRegions;
            float worldRegionX = (sectorX * SectorSizeInRegions) + localRegionX;
            float worldRegionZ = (sectorZ * SectorSizeInRegions) + localRegionZ;
            samples[index] = SampleRawPrecipitation(worldSeed, worldRegionX, worldRegionZ, settings);
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
        return RemapRawNoise(SampleRawContinentalness(seed, worldRegionX, worldRegionZ, settings), false, default);
    }

    public static float SampleErosion(int seed, float worldRegionX, float worldRegionZ, ErosionSettings settings)
    {
        return RemapRawNoise(SampleRawErosion(seed, worldRegionX, worldRegionZ, settings), false, default);
    }

    public static float SampleRidges(int seed, float worldRegionX, float worldRegionZ, RidgesSettings settings)
    {
        return RemapRawNoise(SampleRawRidges(seed, worldRegionX, worldRegionZ, settings), false, default);
    }

    public static float SampleTemperature(int seed, float worldRegionX, float worldRegionZ, TemperatureSettings settings)
    {
        return RemapRawNoise(SampleRawTemperature(seed, worldRegionX, worldRegionZ, settings), false, default);
    }

    public static float SamplePrecipitation(int seed, float worldRegionX, float worldRegionZ, PrecipitationSettings settings)
    {
        return RemapRawNoise(SampleRawPrecipitation(seed, worldRegionX, worldRegionZ, settings), false, default);
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

    public static float SampleRawErosion(int seed, float worldRegionX, float worldRegionZ, ErosionSettings settings)
    {
        float2 p = new float2(worldRegionX, worldRegionZ);
        float2 warped = p;

        if (settings.useWarp)
        {
            float warpX = (FractalPerlinNoise(seed, p * settings.warpFrequency, settings.warpOctaves, settings.warpLacunarity, settings.warpGain, 0x6C8E9CF5u) - 0.5f) * settings.warpAmplitude;
            float warpZ = (FractalPerlinNoise(seed, (p + new float2(91f, 211f)) * settings.warpFrequency, settings.warpOctaves, settings.warpLacunarity, settings.warpGain, 0xB5297A4Du) - 0.5f) * settings.warpAmplitude;
            warped = p + new float2(warpX, warpZ);
        }

        float macro = settings.useMacro
            ? FractalPerlinNoise(seed, warped * settings.macroFrequency, settings.macroOctaves, settings.macroLacunarity, settings.macroGain, 0x68E31DA4u)
            : 0f;
        float broad = settings.useBroad
            ? FractalPerlinNoise(seed, warped * settings.broadFrequency, settings.broadOctaves, settings.broadLacunarity, settings.broadGain, 0x1B56C4E9u)
            : 0f;
        float detail = settings.useDetail
            ? FractalPerlinNoise(seed, warped * settings.detailFrequency, settings.detailOctaves, settings.detailLacunarity, settings.detailGain, 0xC2B2AE35u)
            : 0f;

        float macroWeight = settings.useMacro ? settings.macroWeight : 0f;
        float broadWeight = settings.useBroad ? settings.broadWeight : 0f;
        float detailWeight = settings.useDetail ? settings.detailWeight : 0f;
        float weightSum = macroWeight + broadWeight + detailWeight;
        if (weightSum <= 0.0001f)
        {
            return 0.5f;
        }

        float erosion = ((macro * macroWeight) + (broad * broadWeight) + (detail * detailWeight)) / weightSum;
        return math.saturate(erosion);
    }

    public static float SampleRawRidges(int seed, float worldRegionX, float worldRegionZ, RidgesSettings settings)
    {
        float2 p = new float2(worldRegionX, worldRegionZ);
        float2 warped = p;

        if (settings.useWarp)
        {
            float warpX = (FractalPerlinNoise(seed, p * settings.warpFrequency, settings.warpOctaves, settings.warpLacunarity, settings.warpGain, 0x4CF5AD43u) - 0.5f) * settings.warpAmplitude;
            float warpZ = (FractalPerlinNoise(seed, (p + new float2(137f, 73f)) * settings.warpFrequency, settings.warpOctaves, settings.warpLacunarity, settings.warpGain, 0xA24BAEDFu) - 0.5f) * settings.warpAmplitude;
            warped = p + new float2(warpX, warpZ);
        }

        float macro = settings.useMacro
            ? FractalPerlinNoise(seed, warped * settings.macroFrequency, settings.macroOctaves, settings.macroLacunarity, settings.macroGain, 0x165667B1u)
            : 0f;
        float broad = settings.useBroad
            ? FractalPerlinNoise(seed, warped * settings.broadFrequency, settings.broadOctaves, settings.broadLacunarity, settings.broadGain, 0xD3A2646Cu)
            : 0f;
        float detail = settings.useDetail
            ? FractalPerlinNoise(seed, warped * settings.detailFrequency, settings.detailOctaves, settings.detailLacunarity, settings.detailGain, 0xFD7046C5u)
            : 0f;

        float macroWeight = settings.useMacro ? settings.macroWeight : 0f;
        float broadWeight = settings.useBroad ? settings.broadWeight : 0f;
        float detailWeight = settings.useDetail ? settings.detailWeight : 0f;
        float weightSum = macroWeight + broadWeight + detailWeight;
        if (weightSum <= 0.0001f)
        {
            return 0.5f;
        }

        float ridges = ((macro * macroWeight) + (broad * broadWeight) + (detail * detailWeight)) / weightSum;
        return math.saturate(ridges);
    }

    public static float SampleRawTemperature(int seed, float worldRegionX, float worldRegionZ, TemperatureSettings settings)
    {
        return SampleRawLayeredNoise(
            seed,
            worldRegionX,
            worldRegionZ,
            settings.useWarp,
            settings.warpOctaves,
            settings.warpFrequency,
            settings.warpAmplitude,
            settings.warpLacunarity,
            settings.warpGain,
            settings.useMacro,
            settings.macroOctaves,
            settings.macroFrequency,
            settings.macroLacunarity,
            settings.macroGain,
            settings.macroWeight,
            settings.useBroad,
            settings.broadOctaves,
            settings.broadFrequency,
            settings.broadLacunarity,
            settings.broadGain,
            settings.broadWeight,
            settings.useDetail,
            settings.detailOctaves,
            settings.detailFrequency,
            settings.detailLacunarity,
            settings.detailGain,
            settings.detailWeight,
            0x71A5C2D3u,
            0x92C4E6F1u,
            0x31D7A85Bu,
            0xA671C8F3u,
            0x58B92DE1u);
    }

    public static float SampleRawPrecipitation(int seed, float worldRegionX, float worldRegionZ, PrecipitationSettings settings)
    {
        return SampleRawLayeredNoise(
            seed,
            worldRegionX,
            worldRegionZ,
            settings.useWarp,
            settings.warpOctaves,
            settings.warpFrequency,
            settings.warpAmplitude,
            settings.warpLacunarity,
            settings.warpGain,
            settings.useMacro,
            settings.macroOctaves,
            settings.macroFrequency,
            settings.macroLacunarity,
            settings.macroGain,
            settings.macroWeight,
            settings.useBroad,
            settings.broadOctaves,
            settings.broadFrequency,
            settings.broadLacunarity,
            settings.broadGain,
            settings.broadWeight,
            settings.useDetail,
            settings.detailOctaves,
            settings.detailFrequency,
            settings.detailLacunarity,
            settings.detailGain,
            settings.detailWeight,
            0x4E7B3C11u,
            0xB2D54F89u,
            0x2648C9F7u,
            0xD9A16E43u,
            0x83F24B5Du);
    }

    private static float RemapRawNoise(float rawValue, bool useCdfRemap, NativeArray<float> cdfLut)
    {
        float normalized = math.saturate(rawValue);
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

    private static Color32 EvaluateErosionColor(float value)
    {
        byte intensity = (byte)math.round(math.clamp((1f - ((value + 1f) * 0.5f)) * 255f, 0f, 255f));
        return new Color32(intensity, intensity, intensity, 255);
    }

    private static Color32 EvaluateRidgesColor(float value)
    {
        byte intensity = (byte)math.round(math.clamp((1f - ((value + 1f) * 0.5f)) * 255f, 0f, 255f));
        return new Color32(intensity, intensity, intensity, 255);
    }

    private static Color32 EvaluateScalarColor(float value)
    {
        byte intensity = (byte)math.round(math.clamp((1f - ((value + 1f) * 0.5f)) * 255f, 0f, 255f));
        return new Color32(intensity, intensity, intensity, 255);
    }

    private static float SampleRawLayeredNoise(
        int seed,
        float worldRegionX,
        float worldRegionZ,
        bool useWarp,
        int warpOctaves,
        float warpFrequency,
        float warpAmplitude,
        float warpLacunarity,
        float warpGain,
        bool useMacro,
        int macroOctaves,
        float macroFrequency,
        float macroLacunarity,
        float macroGain,
        float macroWeight,
        bool useBroad,
        int broadOctaves,
        float broadFrequency,
        float broadLacunarity,
        float broadGain,
        float broadWeight,
        bool useDetail,
        int detailOctaves,
        float detailFrequency,
        float detailLacunarity,
        float detailGain,
        float detailWeight,
        uint warpSaltX,
        uint warpSaltZ,
        uint macroSalt,
        uint broadSalt,
        uint detailSalt)
    {
        float2 p = new float2(worldRegionX, worldRegionZ);
        float2 warped = p;

        if (useWarp)
        {
            float warpX = (FractalPerlinNoise(seed, p * warpFrequency, warpOctaves, warpLacunarity, warpGain, warpSaltX) - 0.5f) * warpAmplitude;
            float warpZ = (FractalPerlinNoise(seed, (p + new float2(113f, 197f)) * warpFrequency, warpOctaves, warpLacunarity, warpGain, warpSaltZ) - 0.5f) * warpAmplitude;
            warped = p + new float2(warpX, warpZ);
        }

        float macro = useMacro
            ? FractalPerlinNoise(seed, warped * macroFrequency, macroOctaves, macroLacunarity, macroGain, macroSalt)
            : 0f;
        float broad = useBroad
            ? FractalPerlinNoise(seed, warped * broadFrequency, broadOctaves, broadLacunarity, broadGain, broadSalt)
            : 0f;
        float detail = useDetail
            ? FractalPerlinNoise(seed, warped * detailFrequency, detailOctaves, detailLacunarity, detailGain, detailSalt)
            : 0f;

        float safeMacroWeight = useMacro ? macroWeight : 0f;
        float safeBroadWeight = useBroad ? broadWeight : 0f;
        float safeDetailWeight = useDetail ? detailWeight : 0f;
        float weightSum = safeMacroWeight + safeBroadWeight + safeDetailWeight;
        if (weightSum <= 0.0001f)
        {
            return 0.5f;
        }

        float value = ((macro * safeMacroWeight) + (broad * safeBroadWeight) + (detail * safeDetailWeight)) / weightSum;
        return math.saturate(value);
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

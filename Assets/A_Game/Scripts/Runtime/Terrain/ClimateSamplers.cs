using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public readonly struct TemperatureRuntimeSettings
{
    public readonly int worldSizeXZ;
    public readonly float latitudeStrength;
    public readonly float latitudePhaseRadians;
    public readonly float latitudeWarpStrength;
    public readonly int latitudeWarpBaseCycles;
    public readonly int latitudeWarpOctaveCount;
    public readonly float latitudeWarpPersistence;
    public readonly float latitudeWarpLacunarity;
    public readonly int noiseBaseCycles;
    public readonly int noiseOctaveCount;
    public readonly float noisePersistence;
    public readonly float noiseLacunarity;
    public readonly float noiseStrength;

    public TemperatureRuntimeSettings(
        int worldSizeXZ,
        float latitudeStrength,
        float latitudePhaseDegrees,
        float latitudeWarpStrength,
        int latitudeWarpBaseCycles,
        int latitudeWarpOctaveCount,
        float latitudeWarpPersistence,
        float latitudeWarpLacunarity,
        int noiseBaseCycles,
        int noiseOctaveCount,
        float noisePersistence,
        float noiseLacunarity,
        float noiseStrength)
    {
        this.worldSizeXZ = Mathf.Max(1, worldSizeXZ);
        this.latitudeStrength = Mathf.Max(0f, latitudeStrength);
        latitudePhaseRadians = latitudePhaseDegrees * Mathf.Deg2Rad;
        this.latitudeWarpStrength = Mathf.Max(0f, latitudeWarpStrength);
        this.latitudeWarpBaseCycles = Mathf.Max(1, latitudeWarpBaseCycles);
        this.latitudeWarpOctaveCount = Mathf.Max(1, latitudeWarpOctaveCount);
        this.latitudeWarpPersistence = Mathf.Clamp01(latitudeWarpPersistence);
        this.latitudeWarpLacunarity = Mathf.Max(1f, latitudeWarpLacunarity);
        this.noiseBaseCycles = Mathf.Max(1, noiseBaseCycles);
        this.noiseOctaveCount = Mathf.Max(1, noiseOctaveCount);
        this.noisePersistence = Mathf.Clamp01(noisePersistence);
        this.noiseLacunarity = Mathf.Max(1f, noiseLacunarity);
        this.noiseStrength = Mathf.Max(0f, noiseStrength);
    }

    public static TemperatureRuntimeSettings CreateDefault()
    {
        return new TemperatureRuntimeSettings(
            TerrainData.WorldSizeXZ,
            0.85f,
            0f,
            0f,
            3,
            2,
            0.5f,
            2f,
            4,
            3,
            0.5f,
            2f,
            0.15f);
    }
}

public struct TemperatureJobData
{
    public int worldSizeXZ;
    public float latitudeStrength;
    public float latitudePhaseRadians;
    public float latitudeWarpStrength;
    public int latitudeWarpBaseCycles;
    public int latitudeWarpOctaveCount;
    public float latitudeWarpPersistence;
    public float latitudeWarpLacunarity;
    public int noiseBaseCycles;
    public int noiseOctaveCount;
    public float noisePersistence;
    public float noiseLacunarity;
    public float noiseStrength;

    [ReadOnly] public NativeArray<int> noisePermutations;
    [ReadOnly] public NativeArray<float> noisePhaseX;
    [ReadOnly] public NativeArray<float> noisePhaseZ;
    [ReadOnly] public NativeArray<int> latitudeWarpPermutations;
    [ReadOnly] public NativeArray<float> latitudeWarpPhaseX;
    [ReadOnly] public NativeArray<float> latitudeWarpPhaseZ;
}

[Serializable]
public readonly struct PrecipitationRuntimeSettings
{
    public readonly int worldSizeXZ;
    public readonly int baseCycles;
    public readonly int octaveCount;
    public readonly float persistence;
    public readonly float lacunarity;

    public PrecipitationRuntimeSettings(
        int worldSizeXZ,
        int baseCycles,
        int octaveCount,
        float persistence,
        float lacunarity)
    {
        this.worldSizeXZ = Mathf.Max(1, worldSizeXZ);
        this.baseCycles = Mathf.Max(1, baseCycles);
        this.octaveCount = Mathf.Max(1, octaveCount);
        this.persistence = Mathf.Clamp01(persistence);
        this.lacunarity = Mathf.Max(1f, lacunarity);
    }

    public static PrecipitationRuntimeSettings CreateDefault()
    {
        return new PrecipitationRuntimeSettings(
            TerrainData.WorldSizeXZ,
            4,
            4,
            0.5f,
            2f);
    }
}

public struct PrecipitationJobData
{
    public int worldSizeXZ;
    public int baseCycles;
    public int octaveCount;
    public float persistence;
    public float lacunarity;

    [ReadOnly] public NativeArray<int> permutations;
    [ReadOnly] public NativeArray<float> phaseX;
    [ReadOnly] public NativeArray<float> phaseZ;
}

[Serializable]
public readonly struct ReliefRuntimeSettings
{
    public readonly int worldSizeXZ;
    public readonly int baseCycles;
    public readonly int octaveCount;
    public readonly float persistence;
    public readonly float lacunarity;

    public ReliefRuntimeSettings(
        int worldSizeXZ,
        int baseCycles,
        int octaveCount,
        float persistence,
        float lacunarity)
    {
        this.worldSizeXZ = Mathf.Max(1, worldSizeXZ);
        this.baseCycles = Mathf.Max(1, baseCycles);
        this.octaveCount = Mathf.Max(1, octaveCount);
        this.persistence = Mathf.Clamp01(persistence);
        this.lacunarity = Mathf.Max(1f, lacunarity);
    }

    public static ReliefRuntimeSettings CreateDefault()
    {
        return new ReliefRuntimeSettings(
            TerrainData.WorldSizeXZ,
            4,
            3,
            0.5f,
            2f);
    }
}

public struct ReliefJobData
{
    public int worldSizeXZ;
    public int baseCycles;
    public int octaveCount;
    public float persistence;
    public float lacunarity;

    [ReadOnly] public NativeArray<int> permutations;
    [ReadOnly] public NativeArray<float> phaseX;
    [ReadOnly] public NativeArray<float> phaseZ;
}

[Serializable]
public readonly struct VeinRuntimeSettings
{
    public readonly int worldSizeXZ;
    public readonly int baseCycles;
    public readonly int octaveCount;
    public readonly float persistence;
    public readonly float lacunarity;

    public VeinRuntimeSettings(
        int worldSizeXZ,
        int baseCycles,
        int octaveCount,
        float persistence,
        float lacunarity)
    {
        this.worldSizeXZ = Mathf.Max(1, worldSizeXZ);
        this.baseCycles = Mathf.Max(1, baseCycles);
        this.octaveCount = Mathf.Max(1, octaveCount);
        this.persistence = Mathf.Clamp01(persistence);
        this.lacunarity = Mathf.Max(1f, lacunarity);
    }

    public static VeinRuntimeSettings CreateDefault()
    {
        return new VeinRuntimeSettings(
            TerrainData.WorldSizeXZ,
            6,
            4,
            0.5f,
            2f);
    }
}

public struct VeinJobData
{
    public int worldSizeXZ;
    public int baseCycles;
    public int octaveCount;
    public float persistence;
    public float lacunarity;

    [ReadOnly] public NativeArray<int> permutations;
    [ReadOnly] public NativeArray<float> phaseX;
    [ReadOnly] public NativeArray<float> phaseZ;
}

internal static class ToroidalNoiseSamplerUtility
{
    public const int PermutationTableLength = 512;

    public static void InitializePermutationTable(int seed, NativeArray<int> destination, int baseIndex)
    {
        int[] source = new int[256];
        for (int i = 0; i < source.Length; i++)
        {
            source[i] = i;
        }

        System.Random random = new(seed);
        for (int i = source.Length - 1; i >= 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            (source[i], source[swapIndex]) = (source[swapIndex], source[i]);
        }

        for (int i = 0; i < PermutationTableLength; i++)
        {
            destination[baseIndex + i] = source[i & 255];
        }
    }

    public static void DisposeIfCreated<T>(NativeArray<T> array) where T : struct
    {
        if (array.IsCreated)
        {
            array.Dispose();
        }
    }
}

public sealed class TemperatureSampler : IDisposable
{
    private bool _disposed;
    private readonly TemperatureJobData _jobData;

    public TemperatureSampler(TemperatureRuntimeSettings settings, int seed)
    {
        TemperatureJobData jobData = new()
        {
            worldSizeXZ = settings.worldSizeXZ,
            latitudeStrength = settings.latitudeStrength,
            latitudePhaseRadians = settings.latitudePhaseRadians,
            latitudeWarpStrength = settings.latitudeWarpStrength,
            latitudeWarpBaseCycles = settings.latitudeWarpBaseCycles,
            latitudeWarpOctaveCount = settings.latitudeWarpOctaveCount,
            latitudeWarpPersistence = settings.latitudeWarpPersistence,
            latitudeWarpLacunarity = settings.latitudeWarpLacunarity,
            noiseBaseCycles = settings.noiseBaseCycles,
            noiseOctaveCount = settings.noiseOctaveCount,
            noisePersistence = settings.noisePersistence,
            noiseLacunarity = settings.noiseLacunarity,
            noiseStrength = settings.noiseStrength,
            latitudeWarpPermutations = new NativeArray<int>(settings.latitudeWarpOctaveCount * ToroidalNoiseSamplerUtility.PermutationTableLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
            latitudeWarpPhaseX = new NativeArray<float>(settings.latitudeWarpOctaveCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
            latitudeWarpPhaseZ = new NativeArray<float>(settings.latitudeWarpOctaveCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
            noisePermutations = new NativeArray<int>(settings.noiseOctaveCount * ToroidalNoiseSamplerUtility.PermutationTableLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
            noisePhaseX = new NativeArray<float>(settings.noiseOctaveCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
            noisePhaseZ = new NativeArray<float>(settings.noiseOctaveCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
        };

        System.Random random = new(seed);
        for (int octave = 0; octave < settings.latitudeWarpOctaveCount; octave++)
        {
            ToroidalNoiseSamplerUtility.InitializePermutationTable(random.Next(), jobData.latitudeWarpPermutations, octave * ToroidalNoiseSamplerUtility.PermutationTableLength);
            jobData.latitudeWarpPhaseX[octave] = (float)(random.NextDouble() * GndBurstUtility.Tau);
            jobData.latitudeWarpPhaseZ[octave] = (float)(random.NextDouble() * GndBurstUtility.Tau);
        }

        for (int octave = 0; octave < settings.noiseOctaveCount; octave++)
        {
            ToroidalNoiseSamplerUtility.InitializePermutationTable(random.Next(), jobData.noisePermutations, octave * ToroidalNoiseSamplerUtility.PermutationTableLength);
            jobData.noisePhaseX[octave] = (float)(random.NextDouble() * GndBurstUtility.Tau);
            jobData.noisePhaseZ[octave] = (float)(random.NextDouble() * GndBurstUtility.Tau);
        }

        _jobData = jobData;
    }

    public float SampleTemperature(int worldX, int worldZ)
    {
        return ClimateBurstUtility.SampleTemperature(_jobData, worldX, worldZ);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ToroidalNoiseSamplerUtility.DisposeIfCreated(_jobData.latitudeWarpPermutations);
        ToroidalNoiseSamplerUtility.DisposeIfCreated(_jobData.latitudeWarpPhaseX);
        ToroidalNoiseSamplerUtility.DisposeIfCreated(_jobData.latitudeWarpPhaseZ);
        ToroidalNoiseSamplerUtility.DisposeIfCreated(_jobData.noisePermutations);
        ToroidalNoiseSamplerUtility.DisposeIfCreated(_jobData.noisePhaseX);
        ToroidalNoiseSamplerUtility.DisposeIfCreated(_jobData.noisePhaseZ);
    }
}

public sealed class PrecipitationSampler : IDisposable
{
    private bool _disposed;
    private readonly PrecipitationJobData _jobData;

    public PrecipitationSampler(PrecipitationRuntimeSettings settings, int seed)
    {
        PrecipitationJobData jobData = new()
        {
            worldSizeXZ = settings.worldSizeXZ,
            baseCycles = settings.baseCycles,
            octaveCount = settings.octaveCount,
            persistence = settings.persistence,
            lacunarity = settings.lacunarity,
            permutations = new NativeArray<int>(settings.octaveCount * ToroidalNoiseSamplerUtility.PermutationTableLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
            phaseX = new NativeArray<float>(settings.octaveCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
            phaseZ = new NativeArray<float>(settings.octaveCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
        };

        System.Random random = new(seed);
        for (int octave = 0; octave < settings.octaveCount; octave++)
        {
            ToroidalNoiseSamplerUtility.InitializePermutationTable(random.Next(), jobData.permutations, octave * ToroidalNoiseSamplerUtility.PermutationTableLength);
            jobData.phaseX[octave] = (float)(random.NextDouble() * GndBurstUtility.Tau);
            jobData.phaseZ[octave] = (float)(random.NextDouble() * GndBurstUtility.Tau);
        }

        _jobData = jobData;
    }

    public float SamplePrecipitation(int worldX, int worldZ)
    {
        return ClimateBurstUtility.SamplePrecipitation(_jobData, worldX, worldZ);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ToroidalNoiseSamplerUtility.DisposeIfCreated(_jobData.permutations);
        ToroidalNoiseSamplerUtility.DisposeIfCreated(_jobData.phaseX);
        ToroidalNoiseSamplerUtility.DisposeIfCreated(_jobData.phaseZ);
    }
}

public sealed class ReliefSampler : IDisposable
{
    private const int SeedSalt = unchecked((int)0x52FCE729);

    private bool _disposed;
    private readonly ReliefJobData _jobData;

    public ReliefSampler(ReliefRuntimeSettings settings, int seed)
    {
        ReliefJobData jobData = new()
        {
            worldSizeXZ = settings.worldSizeXZ,
            baseCycles = settings.baseCycles,
            octaveCount = settings.octaveCount,
            persistence = settings.persistence,
            lacunarity = settings.lacunarity,
            permutations = new NativeArray<int>(settings.octaveCount * ToroidalNoiseSamplerUtility.PermutationTableLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
            phaseX = new NativeArray<float>(settings.octaveCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
            phaseZ = new NativeArray<float>(settings.octaveCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
        };

        System.Random random = new(seed ^ SeedSalt);
        for (int octave = 0; octave < settings.octaveCount; octave++)
        {
            ToroidalNoiseSamplerUtility.InitializePermutationTable(random.Next(), jobData.permutations, octave * ToroidalNoiseSamplerUtility.PermutationTableLength);
            jobData.phaseX[octave] = (float)(random.NextDouble() * GndBurstUtility.Tau);
            jobData.phaseZ[octave] = (float)(random.NextDouble() * GndBurstUtility.Tau);
        }

        _jobData = jobData;
    }

    public float SampleRelief(int worldX, int worldZ)
    {
        return ClimateBurstUtility.SampleRelief(_jobData, worldX, worldZ);
    }

    public ReliefJobData JobData => _jobData;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ToroidalNoiseSamplerUtility.DisposeIfCreated(_jobData.permutations);
        ToroidalNoiseSamplerUtility.DisposeIfCreated(_jobData.phaseX);
        ToroidalNoiseSamplerUtility.DisposeIfCreated(_jobData.phaseZ);
    }
}

public sealed class VeinSampler : IDisposable
{
    private const int SeedSalt = unchecked((int)0x68E31DA4);

    private bool _disposed;
    private readonly VeinJobData _jobData;

    public VeinSampler(VeinRuntimeSettings settings, int seed)
    {
        VeinJobData jobData = new()
        {
            worldSizeXZ = settings.worldSizeXZ,
            baseCycles = settings.baseCycles,
            octaveCount = settings.octaveCount,
            persistence = settings.persistence,
            lacunarity = settings.lacunarity,
            permutations = new NativeArray<int>(settings.octaveCount * ToroidalNoiseSamplerUtility.PermutationTableLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
            phaseX = new NativeArray<float>(settings.octaveCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
            phaseZ = new NativeArray<float>(settings.octaveCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
        };

        System.Random random = new(seed ^ SeedSalt);
        for (int octave = 0; octave < settings.octaveCount; octave++)
        {
            ToroidalNoiseSamplerUtility.InitializePermutationTable(random.Next(), jobData.permutations, octave * ToroidalNoiseSamplerUtility.PermutationTableLength);
            jobData.phaseX[octave] = (float)(random.NextDouble() * GndBurstUtility.Tau);
            jobData.phaseZ[octave] = (float)(random.NextDouble() * GndBurstUtility.Tau);
        }

        _jobData = jobData;
    }

    public float SampleVein(int worldX, int worldZ)
    {
        return ClimateBurstUtility.SampleVein(_jobData, worldX, worldZ);
    }

    public float SampleVeinFold(int worldX, int worldZ)
    {
        return ClimateBurstUtility.FoldVein(SampleVein(worldX, worldZ));
    }

    public VeinJobData JobData => _jobData;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ToroidalNoiseSamplerUtility.DisposeIfCreated(_jobData.permutations);
        ToroidalNoiseSamplerUtility.DisposeIfCreated(_jobData.phaseX);
        ToroidalNoiseSamplerUtility.DisposeIfCreated(_jobData.phaseZ);
    }
}

public static class ClimateBurstUtility
{
    public static float SampleTemperature(in TemperatureJobData data, int worldX, int worldZ)
    {
        float wrappedWorldX = GndBurstUtility.WrapCoordinate(worldX, data.worldSizeXZ);
        float wrappedWorldZ = GndBurstUtility.WrapCoordinate(worldZ, data.worldSizeXZ);
        if (data.latitudeWarpStrength > 0f)
        {
            float latitudeWarp = GndBurstUtility.SampleToroidalFractal(
                data.worldSizeXZ,
                wrappedWorldX,
                wrappedWorldZ,
                data.latitudeWarpBaseCycles,
                data.latitudeWarpOctaveCount,
                data.latitudeWarpPersistence,
                data.latitudeWarpLacunarity,
                data.latitudeWarpPermutations,
                data.latitudeWarpPhaseX,
                data.latitudeWarpPhaseZ);

            wrappedWorldZ = GndBurstUtility.WrapCoordinate(
                wrappedWorldZ + (latitudeWarp * data.latitudeWarpStrength),
                data.worldSizeXZ);
        }

        float latitudeAngle = ((GndBurstUtility.Tau * wrappedWorldZ) / data.worldSizeXZ) + data.latitudePhaseRadians;
        float temperature = -math.cos(latitudeAngle) * data.latitudeStrength;

        if (data.noiseStrength <= 0f)
        {
            return temperature;
        }

        float noise = GndBurstUtility.SampleToroidalFractal(
            data.worldSizeXZ,
            wrappedWorldX,
            wrappedWorldZ,
            data.noiseBaseCycles,
            data.noiseOctaveCount,
            data.noisePersistence,
            data.noiseLacunarity,
            data.noisePermutations,
            data.noisePhaseX,
            data.noisePhaseZ);

        return temperature + (noise * data.noiseStrength);
    }

    public static float SamplePrecipitation(in PrecipitationJobData data, int worldX, int worldZ)
    {
        return SampleToroidalNoise(
            data.worldSizeXZ,
            worldX,
            worldZ,
            data.baseCycles,
            data.octaveCount,
            data.persistence,
            data.lacunarity,
            data.permutations,
            data.phaseX,
            data.phaseZ);
    }

    public static float SampleRelief(in ReliefJobData data, int worldX, int worldZ)
    {
        return SampleToroidalNoise(
            data.worldSizeXZ,
            worldX,
            worldZ,
            data.baseCycles,
            data.octaveCount,
            data.persistence,
            data.lacunarity,
            data.permutations,
            data.phaseX,
            data.phaseZ);
    }

    public static float SampleVein(in VeinJobData data, int worldX, int worldZ)
    {
        return SampleToroidalNoise(
            data.worldSizeXZ,
            worldX,
            worldZ,
            data.baseCycles,
            data.octaveCount,
            data.persistence,
            data.lacunarity,
            data.permutations,
            data.phaseX,
            data.phaseZ);
    }

    public static float FoldVein(float vein)
    {
        return math.min(math.abs(2.5f * vein) - 1f, 1f);
    }

    private static float SampleToroidalNoise(
        int worldSizeXZ,
        int worldX,
        int worldZ,
        int baseCycles,
        int octaveCount,
        float persistence,
        float lacunarity,
        NativeArray<int> permutations,
        NativeArray<float> phaseX,
        NativeArray<float> phaseZ)
    {
        float wrappedWorldX = GndBurstUtility.WrapCoordinate(worldX, worldSizeXZ);
        float wrappedWorldZ = GndBurstUtility.WrapCoordinate(worldZ, worldSizeXZ);
        return GndBurstUtility.SampleToroidalFractal(
            worldSizeXZ,
            wrappedWorldX,
            wrappedWorldZ,
            baseCycles,
            octaveCount,
            persistence,
            lacunarity,
            permutations,
            phaseX,
            phaseZ);
    }
}

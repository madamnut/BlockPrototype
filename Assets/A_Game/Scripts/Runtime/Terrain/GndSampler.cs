using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public struct GndSplinePoint
{
    public float input;
    public float height;

    public GndSplinePoint(float input, float height)
    {
        this.input = input;
        this.height = height;
    }
}

public readonly struct GndRuntimeSettings
{
    public readonly int worldSizeXZ;
    public readonly int sampleSpacing;
    public readonly int seaLevel;
    public readonly int baseCycles;
    public readonly int octaveCount;
    public readonly float persistence;
    public readonly float lacunarity;
    public readonly float warpStrength;
    public readonly int warpBaseCycles;
    public readonly int warpOctaveCount;
    public readonly float warpPersistence;
    public readonly float warpLacunarity;

    public GndRuntimeSettings(
        int worldSizeXZ,
        int sampleSpacing,
        int seaLevel,
        int baseCycles,
        int octaveCount,
        float persistence,
        float lacunarity,
        float warpStrength,
        int warpBaseCycles,
        int warpOctaveCount,
        float warpPersistence,
        float warpLacunarity)
    {
        this.worldSizeXZ = Mathf.Max(1, worldSizeXZ);
        this.sampleSpacing = Mathf.Max(1, sampleSpacing);
        this.seaLevel = Mathf.Clamp(seaLevel, 0, TerrainData.WorldHeight - 1);
        this.baseCycles = Mathf.Max(1, baseCycles);
        this.octaveCount = Mathf.Max(1, octaveCount);
        this.persistence = Mathf.Clamp01(persistence);
        this.lacunarity = Mathf.Max(1f, lacunarity);
        this.warpStrength = Mathf.Max(0f, warpStrength);
        this.warpBaseCycles = Mathf.Max(1, warpBaseCycles);
        this.warpOctaveCount = Mathf.Max(1, warpOctaveCount);
        this.warpPersistence = Mathf.Clamp01(warpPersistence);
        this.warpLacunarity = Mathf.Max(1f, warpLacunarity);
    }

    public static GndRuntimeSettings CreateDefault(int seaLevel = TerrainData.DefaultSeaLevel)
    {
        return new GndRuntimeSettings(
            TerrainData.WorldSizeXZ,
            4,
            seaLevel,
            3,
            4,
            0.5f,
            2f,
            0f,
            6,
            2,
            0.5f,
            2f);
    }
}

public struct GndJobData
{
    public int worldSizeXZ;
    public int sampleSpacing;
    public int seaLevel;
    public int baseCycles;
    public int octaveCount;
    public float persistence;
    public float lacunarity;
    public float warpStrength;
    public int warpBaseCycles;
    public int warpOctaveCount;
    public float warpPersistence;
    public float warpLacunarity;

    [ReadOnly]
    public NativeArray<int> gndPermutations;
    [ReadOnly]
    public NativeArray<float> gndPhaseX;
    [ReadOnly]
    public NativeArray<float> gndPhaseZ;
    [ReadOnly]
    public NativeArray<int> warpXPermutations;
    [ReadOnly]
    public NativeArray<int> warpZPermutations;
    [ReadOnly]
    public NativeArray<float> warpXPhaseX;
    [ReadOnly]
    public NativeArray<float> warpXPhaseZ;
    [ReadOnly]
    public NativeArray<float> warpZPhaseX;
    [ReadOnly]
    public NativeArray<float> warpZPhaseZ;
}

public sealed class GndSampler : IDisposable
{
    private const int PermutationTableLength = 512;

    private bool _disposed;
    private readonly GndRuntimeSettings _settings;
    private readonly GndJobData _jobData;

    public GndSampler(GndRuntimeSettings settings, int seed)
    {
        _settings = settings;

        GndJobData jobData = new()
        {
            worldSizeXZ = settings.worldSizeXZ,
            sampleSpacing = settings.sampleSpacing,
            seaLevel = settings.seaLevel,
            baseCycles = settings.baseCycles,
            octaveCount = settings.octaveCount,
            persistence = settings.persistence,
            lacunarity = settings.lacunarity,
            warpStrength = settings.warpStrength,
            warpBaseCycles = settings.warpBaseCycles,
            warpOctaveCount = settings.warpOctaveCount,
            warpPersistence = settings.warpPersistence,
            warpLacunarity = settings.warpLacunarity,
            gndPermutations = new NativeArray<int>(settings.octaveCount * PermutationTableLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
            gndPhaseX = new NativeArray<float>(settings.octaveCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
            gndPhaseZ = new NativeArray<float>(settings.octaveCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
            warpXPermutations = new NativeArray<int>(settings.warpOctaveCount * PermutationTableLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
            warpZPermutations = new NativeArray<int>(settings.warpOctaveCount * PermutationTableLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
            warpXPhaseX = new NativeArray<float>(settings.warpOctaveCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
            warpXPhaseZ = new NativeArray<float>(settings.warpOctaveCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
            warpZPhaseX = new NativeArray<float>(settings.warpOctaveCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
            warpZPhaseZ = new NativeArray<float>(settings.warpOctaveCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
        };

        System.Random random = new(seed);
        for (int octave = 0; octave < settings.octaveCount; octave++)
        {
            InitializePermutationTable(random.Next(), jobData.gndPermutations, octave * PermutationTableLength);
            jobData.gndPhaseX[octave] = (float)(random.NextDouble() * GndBurstUtility.Tau);
            jobData.gndPhaseZ[octave] = (float)(random.NextDouble() * GndBurstUtility.Tau);
        }

        for (int octave = 0; octave < settings.warpOctaveCount; octave++)
        {
            InitializePermutationTable(random.Next(), jobData.warpXPermutations, octave * PermutationTableLength);
            InitializePermutationTable(random.Next(), jobData.warpZPermutations, octave * PermutationTableLength);
            jobData.warpXPhaseX[octave] = (float)(random.NextDouble() * GndBurstUtility.Tau);
            jobData.warpXPhaseZ[octave] = (float)(random.NextDouble() * GndBurstUtility.Tau);
            jobData.warpZPhaseX[octave] = (float)(random.NextDouble() * GndBurstUtility.Tau);
            jobData.warpZPhaseZ[octave] = (float)(random.NextDouble() * GndBurstUtility.Tau);
        }

        _jobData = jobData;
    }

    public GndJobData JobData => _jobData;
    public int SampleSpacing => _settings.sampleSpacing;
    public int SeaLevel => _settings.seaLevel;

    public float SampleGnd(int worldX, int worldZ)
    {
        return GndBurstUtility.SampleGnd(_jobData, worldX, worldZ);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DisposeIfCreated(_jobData.gndPermutations);
        DisposeIfCreated(_jobData.gndPhaseX);
        DisposeIfCreated(_jobData.gndPhaseZ);
        DisposeIfCreated(_jobData.warpXPermutations);
        DisposeIfCreated(_jobData.warpZPermutations);
        DisposeIfCreated(_jobData.warpXPhaseX);
        DisposeIfCreated(_jobData.warpXPhaseZ);
        DisposeIfCreated(_jobData.warpZPhaseX);
        DisposeIfCreated(_jobData.warpZPhaseZ);
    }

    private static void InitializePermutationTable(int seed, NativeArray<int> destination, int baseIndex)
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

    private static void DisposeIfCreated<T>(NativeArray<T> array) where T : struct
    {
        if (array.IsCreated)
        {
            array.Dispose();
        }
    }
}

public static class GndBurstUtility
{
    public const float Tau = math.PI * 2f;
    private const float F4 = 0.30901699437494745f;
    private const float G4 = 0.1381966011250105f;
    private const int PermutationTableLength = 512;

    public static float SampleGnd(in GndJobData data, int worldX, int worldZ)
    {
        float sampleX = WrapCoordinate(worldX, data.worldSizeXZ);
        float sampleZ = WrapCoordinate(worldZ, data.worldSizeXZ);
        if (data.warpStrength > 0f)
        {
            float warpX = SampleToroidalFractal(
                data.worldSizeXZ,
                sampleX,
                sampleZ,
                data.warpBaseCycles,
                data.warpOctaveCount,
                data.warpPersistence,
                data.warpLacunarity,
                data.warpXPermutations,
                data.warpXPhaseX,
                data.warpXPhaseZ);
            float warpZ = SampleToroidalFractal(
                data.worldSizeXZ,
                sampleX,
                sampleZ,
                data.warpBaseCycles,
                data.warpOctaveCount,
                data.warpPersistence,
                data.warpLacunarity,
                data.warpZPermutations,
                data.warpZPhaseX,
                data.warpZPhaseZ);

            sampleX = WrapCoordinate(sampleX + (warpX * data.warpStrength), data.worldSizeXZ);
            sampleZ = WrapCoordinate(sampleZ + (warpZ * data.warpStrength), data.worldSizeXZ);
        }

        return SampleToroidalFractal(
            data.worldSizeXZ,
            sampleX,
            sampleZ,
            data.baseCycles,
            data.octaveCount,
            data.persistence,
            data.lacunarity,
            data.gndPermutations,
            data.gndPhaseX,
            data.gndPhaseZ);
    }

    public static float SampleToroidalFractal(
        int worldSizeXZ,
        float wrappedWorldX,
        float wrappedWorldZ,
        int baseCycles,
        int octaveCount,
        float persistence,
        float lacunarity,
        NativeArray<int> permutations,
        NativeArray<float> phaseX,
        NativeArray<float> phaseZ)
    {
        float amplitude = 1f;
        float cycles = baseCycles;
        float total = 0f;
        float amplitudeSum = 0f;
        for (int octave = 0; octave < octaveCount; octave++)
        {
            float angleX = ((Tau * cycles * wrappedWorldX) / worldSizeXZ) + phaseX[octave];
            float angleZ = ((Tau * cycles * wrappedWorldZ) / worldSizeXZ) + phaseZ[octave];
            math.sincos(angleX, out float sinX, out float cosX);
            math.sincos(angleZ, out float sinZ, out float cosZ);

            float octaveSample = SampleSimplex4D(permutations, octave * PermutationTableLength, cosX, sinX, cosZ, sinZ);
            total += octaveSample * amplitude;
            amplitudeSum += amplitude;
            amplitude *= persistence;
            cycles *= lacunarity;
        }

        return amplitudeSum > 0f ? total / amplitudeSum : 0f;
    }

    private static float SampleSimplex4D(NativeArray<int> permutations, int permBaseIndex, float x, float y, float z, float w)
    {
        float s = (x + y + z + w) * F4;
        int i = (int)math.floor(x + s);
        int j = (int)math.floor(y + s);
        int k = (int)math.floor(z + s);
        int l = (int)math.floor(w + s);

        float t = (i + j + k + l) * G4;
        float x0 = x - (i - t);
        float y0 = y - (j - t);
        float z0 = z - (k - t);
        float w0 = w - (l - t);

        int rankX = 0;
        int rankY = 0;
        int rankZ = 0;
        int rankW = 0;

        if (x0 > y0) rankX++; else rankY++;
        if (x0 > z0) rankX++; else rankZ++;
        if (x0 > w0) rankX++; else rankW++;
        if (y0 > z0) rankY++; else rankZ++;
        if (y0 > w0) rankY++; else rankW++;
        if (z0 > w0) rankZ++; else rankW++;

        int i1 = rankX >= 3 ? 1 : 0;
        int j1 = rankY >= 3 ? 1 : 0;
        int k1 = rankZ >= 3 ? 1 : 0;
        int l1 = rankW >= 3 ? 1 : 0;
        int i2 = rankX >= 2 ? 1 : 0;
        int j2 = rankY >= 2 ? 1 : 0;
        int k2 = rankZ >= 2 ? 1 : 0;
        int l2 = rankW >= 2 ? 1 : 0;
        int i3 = rankX >= 1 ? 1 : 0;
        int j3 = rankY >= 1 ? 1 : 0;
        int k3 = rankZ >= 1 ? 1 : 0;
        int l3 = rankW >= 1 ? 1 : 0;

        float x1 = x0 - i1 + G4;
        float y1 = y0 - j1 + G4;
        float z1 = z0 - k1 + G4;
        float w1 = w0 - l1 + G4;
        float x2 = x0 - i2 + (2f * G4);
        float y2 = y0 - j2 + (2f * G4);
        float z2 = z0 - k2 + (2f * G4);
        float w2 = w0 - l2 + (2f * G4);
        float x3 = x0 - i3 + (3f * G4);
        float y3 = y0 - j3 + (3f * G4);
        float z3 = z0 - k3 + (3f * G4);
        float w3 = w0 - l3 + (3f * G4);
        float x4 = x0 - 1f + (4f * G4);
        float y4 = y0 - 1f + (4f * G4);
        float z4 = z0 - 1f + (4f * G4);
        float w4 = w0 - 1f + (4f * G4);

        int ii = i & 255;
        int jj = j & 255;
        int kk = k & 255;
        int ll = l & 255;

        float n0 = CornerNoise(permutations, permBaseIndex, ii, jj, kk, ll, 0, 0, 0, 0, x0, y0, z0, w0);
        float n1 = CornerNoise(permutations, permBaseIndex, ii, jj, kk, ll, i1, j1, k1, l1, x1, y1, z1, w1);
        float n2 = CornerNoise(permutations, permBaseIndex, ii, jj, kk, ll, i2, j2, k2, l2, x2, y2, z2, w2);
        float n3 = CornerNoise(permutations, permBaseIndex, ii, jj, kk, ll, i3, j3, k3, l3, x3, y3, z3, w3);
        float n4 = CornerNoise(permutations, permBaseIndex, ii, jj, kk, ll, 1, 1, 1, 1, x4, y4, z4, w4);

        return 27f * (n0 + n1 + n2 + n3 + n4);
    }

    private static float CornerNoise(
        NativeArray<int> permutations,
        int permBaseIndex,
        int ii,
        int jj,
        int kk,
        int ll,
        int iOffset,
        int jOffset,
        int kOffset,
        int lOffset,
        float x,
        float y,
        float z,
        float w)
    {
        float t = 0.6f - (x * x) - (y * y) - (z * z) - (w * w);
        if (t < 0f)
        {
            return 0f;
        }

        t *= t;
        int gradientIndex = permutations[
            permBaseIndex + ii + iOffset + permutations[
                permBaseIndex + jj + jOffset + permutations[
                    permBaseIndex + kk + kOffset + permutations[
                        permBaseIndex + ll + lOffset]]]] & 31;
        return t * t * GradientDot(gradientIndex, x, y, z, w);
    }

    private static float GradientDot(int gradientIndex, float x, float y, float z, float w)
    {
        int group = gradientIndex >> 3;
        int subIndex = gradientIndex & 7;
        float signA = (subIndex & 4) == 0 ? 1f : -1f;
        float signB = (subIndex & 2) == 0 ? 1f : -1f;
        float signC = (subIndex & 1) == 0 ? 1f : -1f;

        return group switch
        {
            0 => (signA * y) + (signB * z) + (signC * w),
            1 => (signA * x) + (signB * z) + (signC * w),
            2 => (signA * x) + (signB * y) + (signC * w),
            _ => (signA * x) + (signB * y) + (signC * z),
        };
    }

    public static float WrapCoordinate(int value, int modulus)
    {
        int wrapped = value % modulus;
        if (wrapped < 0)
        {
            wrapped += modulus;
        }

        return wrapped;
    }

    public static float WrapCoordinate(float value, int modulus)
    {
        float wrapped = value - (math.floor(value / modulus) * modulus);
        if (wrapped < 0f)
        {
            wrapped += modulus;
        }

        return wrapped;
    }
}

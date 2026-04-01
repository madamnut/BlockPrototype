using System;
using UnityEngine;

public sealed class SimplexNoiseSampler
{
    private const double Tau = Math.PI * 2d;
    private readonly SimplexNoiseSettingsAsset _settings;
    private readonly SimplexNoise4D _noise;

    public SimplexNoiseSampler(int seed, SimplexNoiseSettingsAsset settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        int combinedSeed = unchecked(seed + (_settings.SeedOffset * 486187739));
        _noise = new SimplexNoise4D(combinedSeed);
    }

    public float Sample(int worldX, int worldZ)
    {
        Vector2 coordinateOffset = _settings.CoordinateOffset;
        int wrappedWorldX = TerrainData.WrapWorldCoord(worldX);
        int wrappedWorldZ = TerrainData.WrapWorldCoord(worldZ);
        double angleX = ((wrappedWorldX + coordinateOffset.x) / TerrainData.WorldSizeXZ) * Tau;
        double angleZ = ((wrappedWorldZ + coordinateOffset.y) / TerrainData.WorldSizeXZ) * Tau;

        double value = 0d;
        double amplitude = 1d;
        double amplitudeSum = 0d;
        double frequency = 1d;

        for (int octave = 0; octave < _settings.Octaves; octave++)
        {
            double octaveFrequency = _settings.Frequency * frequency;
            double radius = GetTorusRadius(octaveFrequency);
            value += _noise.Sample(
                Math.Cos(angleX) * radius,
                Math.Sin(angleX) * radius,
                Math.Cos(angleZ) * radius,
                Math.Sin(angleZ) * radius) * amplitude;
            amplitudeSum += amplitude;
            frequency *= _settings.Lacunarity;
            amplitude *= _settings.Persistence;
        }

        double normalized = amplitudeSum > 0d ? value / amplitudeSum : 0d;
        return (float)((normalized * _settings.Amplitude) + _settings.Bias);
    }

    private static double GetTorusRadius(double sampleFrequency)
    {
        return (TerrainData.WorldSizeXZ * sampleFrequency) / Tau;
    }
}

public sealed class SimplexNoise3DSampler
{
    private const double Tau = Math.PI * 2d;
    private const double YOffsetA = 0.173d;
    private const double YOffsetB = 0.381d;
    private const double YOffsetC = 0.593d;
    private const double YOffsetD = 0.827d;
    private readonly SimplexNoise3DSettingsAsset _settings;
    private readonly SimplexNoise4D _noise;

    public SimplexNoise3DSampler(int seed, SimplexNoise3DSettingsAsset settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        int combinedSeed = unchecked(seed + (_settings.SeedOffset * 486187739));
        _noise = new SimplexNoise4D(combinedSeed);
    }

    public float Sample(int worldX, int worldY, int worldZ)
    {
        Vector3 coordinateOffset = _settings.CoordinateOffset;
        int wrappedWorldX = TerrainData.WrapWorldCoord(worldX);
        int wrappedWorldZ = TerrainData.WrapWorldCoord(worldZ);
        double angleX = ((wrappedWorldX + coordinateOffset.x) / TerrainData.WorldSizeXZ) * Tau;
        double angleZ = ((wrappedWorldZ + coordinateOffset.z) / TerrainData.WorldSizeXZ) * Tau;

        double value = 0d;
        double amplitude = 1d;
        double amplitudeSum = 0d;
        double frequency = 1d;

        for (int octave = 0; octave < _settings.Octaves; octave++)
        {
            double octaveFrequency = _settings.Frequency * frequency;
            double radius = GetTorusRadius(octaveFrequency);
            double yOffset = (worldY + coordinateOffset.y) * octaveFrequency;
            value += _noise.Sample(
                (Math.Cos(angleX) * radius) + (yOffset * YOffsetA),
                (Math.Sin(angleX) * radius) + (yOffset * YOffsetB),
                (Math.Cos(angleZ) * radius) + (yOffset * YOffsetC),
                (Math.Sin(angleZ) * radius) + (yOffset * YOffsetD)) * amplitude;
            amplitudeSum += amplitude;
            frequency *= _settings.Lacunarity;
            amplitude *= _settings.Persistence;
        }

        double normalized = amplitudeSum > 0d ? value / amplitudeSum : 0d;
        return (float)((normalized * _settings.Amplitude) + _settings.Bias);
    }

    private static double GetTorusRadius(double sampleFrequency)
    {
        return (TerrainData.WorldSizeXZ * sampleFrequency) / Tau;
    }
}

public sealed class TemperatureSampler
{
    private const double Tau = Math.PI * 2d;
    private readonly TemperatureSettingsAsset _settings;
    private readonly SimplexNoise4D _noise;

    public TemperatureSampler(int seed, TemperatureSettingsAsset settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        int combinedSeed = unchecked(seed + (_settings.SeedOffset * 486187739));
        _noise = new SimplexNoise4D(combinedSeed);
    }

    public float Sample(int worldX, int worldZ)
    {
        Vector2 coordinateOffset = _settings.CoordinateOffset;
        int wrappedWorldX = TerrainData.WrapWorldCoord(worldX);
        int wrappedWorldZ = TerrainData.WrapWorldCoord(worldZ);
        float latitudeTemperature = EvaluateLatitudeTemperature(wrappedWorldZ);
        double angleX = ((wrappedWorldX + coordinateOffset.x) / TerrainData.WorldSizeXZ) * Tau;
        double angleZ = ((wrappedWorldZ + coordinateOffset.y) / TerrainData.WorldSizeXZ) * Tau;

        double noiseValue = 0d;
        double amplitude = 1d;
        double amplitudeSum = 0d;
        double frequency = 1d;

        for (int octave = 0; octave < _settings.Octaves; octave++)
        {
            double octaveFrequency = _settings.Frequency * frequency;
            double radius = GetTorusRadius(octaveFrequency);
            noiseValue += _noise.Sample(
                Math.Cos(angleX) * radius,
                Math.Sin(angleX) * radius,
                Math.Cos(angleZ) * radius,
                Math.Sin(angleZ) * radius) * amplitude;
            amplitudeSum += amplitude;
            frequency *= _settings.Lacunarity;
            amplitude *= _settings.Persistence;
        }

        float normalizedNoise = amplitudeSum > 0d ? (float)(noiseValue / amplitudeSum) : 0f;
        float combined =
            (latitudeTemperature * _settings.LatitudeStrength) +
            (normalizedNoise * _settings.NoiseAmplitude) +
            _settings.Bias;
        return Mathf.Clamp(combined, -1f, 1f);
    }

    private float EvaluateLatitudeTemperature(int wrappedWorldZ)
    {
        float normalizedLatitude = wrappedWorldZ / (float)(TerrainData.WorldSizeXZ - 1);
        float distanceFromEquator = Mathf.Abs((normalizedLatitude * 2f) - 1f);
        float heat01 = 1f - Mathf.Pow(distanceFromEquator, _settings.LatitudeExponent);
        return Mathf.Lerp(-1f, 1f, heat01);
    }

    private static double GetTorusRadius(double sampleFrequency)
    {
        return (TerrainData.WorldSizeXZ * sampleFrequency) / Tau;
    }
}

internal sealed class SimplexNoise4D
{
    private const double F4 = 0.30901699437494745d;
    private const double G4 = 0.1381966011250105d;

    private static readonly int[,] Gradients =
    {
        { 0, 1, 1, 1 }, { 0, 1, 1, -1 }, { 0, 1, -1, 1 }, { 0, 1, -1, -1 },
        { 0, -1, 1, 1 }, { 0, -1, 1, -1 }, { 0, -1, -1, 1 }, { 0, -1, -1, -1 },
        { 1, 0, 1, 1 }, { 1, 0, 1, -1 }, { 1, 0, -1, 1 }, { 1, 0, -1, -1 },
        { -1, 0, 1, 1 }, { -1, 0, 1, -1 }, { -1, 0, -1, 1 }, { -1, 0, -1, -1 },
        { 1, 1, 0, 1 }, { 1, 1, 0, -1 }, { 1, -1, 0, 1 }, { 1, -1, 0, -1 },
        { -1, 1, 0, 1 }, { -1, 1, 0, -1 }, { -1, -1, 0, 1 }, { -1, -1, 0, -1 },
        { 1, 1, 1, 0 }, { 1, 1, -1, 0 }, { 1, -1, 1, 0 }, { 1, -1, -1, 0 },
        { -1, 1, 1, 0 }, { -1, 1, -1, 0 }, { -1, -1, 1, 0 }, { -1, -1, -1, 0 },
    };

    private readonly byte[] _perm = new byte[512];

    public SimplexNoise4D(int seed)
    {
        byte[] source = new byte[256];
        for (int i = 0; i < source.Length; i++)
        {
            source[i] = (byte)i;
        }

        System.Random random = new(seed);
        for (int i = source.Length - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            (source[i], source[swapIndex]) = (source[swapIndex], source[i]);
        }

        for (int i = 0; i < _perm.Length; i++)
        {
            _perm[i] = source[i & 255];
        }
    }

    public double Sample(double x, double y, double z, double w)
    {
        double skew = (x + y + z + w) * F4;
        int i = FastFloor(x + skew);
        int j = FastFloor(y + skew);
        int k = FastFloor(z + skew);
        int l = FastFloor(w + skew);

        double unskew = (i + j + k + l) * G4;
        double x0 = x - (i - unskew);
        double y0 = y - (j - unskew);
        double z0 = z - (k - unskew);
        double w0 = w - (l - unskew);

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

        double x1 = x0 - i1 + G4;
        double y1 = y0 - j1 + G4;
        double z1 = z0 - k1 + G4;
        double w1 = w0 - l1 + G4;
        double x2 = x0 - i2 + (2d * G4);
        double y2 = y0 - j2 + (2d * G4);
        double z2 = z0 - k2 + (2d * G4);
        double w2 = w0 - l2 + (2d * G4);
        double x3 = x0 - i3 + (3d * G4);
        double y3 = y0 - j3 + (3d * G4);
        double z3 = z0 - k3 + (3d * G4);
        double w3 = w0 - l3 + (3d * G4);
        double x4 = x0 - 1d + (4d * G4);
        double y4 = y0 - 1d + (4d * G4);
        double z4 = z0 - 1d + (4d * G4);
        double w4 = w0 - 1d + (4d * G4);

        int ii = i & 255;
        int jj = j & 255;
        int kk = k & 255;
        int ll = l & 255;

        int gi0 = _perm[ii + _perm[jj + _perm[kk + _perm[ll]]]];
        int gi1 = _perm[ii + i1 + _perm[jj + j1 + _perm[kk + k1 + _perm[ll + l1]]]];
        int gi2 = _perm[ii + i2 + _perm[jj + j2 + _perm[kk + k2 + _perm[ll + l2]]]];
        int gi3 = _perm[ii + i3 + _perm[jj + j3 + _perm[kk + k3 + _perm[ll + l3]]]];
        int gi4 = _perm[ii + 1 + _perm[jj + 1 + _perm[kk + 1 + _perm[ll + 1]]]];

        double n0 = CornerContribution(gi0, x0, y0, z0, w0);
        double n1 = CornerContribution(gi1, x1, y1, z1, w1);
        double n2 = CornerContribution(gi2, x2, y2, z2, w2);
        double n3 = CornerContribution(gi3, x3, y3, z3, w3);
        double n4 = CornerContribution(gi4, x4, y4, z4, w4);

        return 27d * (n0 + n1 + n2 + n3 + n4);
    }

    private static int FastFloor(double value)
    {
        int integer = (int)value;
        return value < integer ? integer - 1 : integer;
    }

    private static double Dot(int gradientIndex, double x, double y, double z, double w)
    {
        int index = gradientIndex & 31;
        return
            (Gradients[index, 0] * x) +
            (Gradients[index, 1] * y) +
            (Gradients[index, 2] * z) +
            (Gradients[index, 3] * w);
    }

    private static double CornerContribution(int gradientIndex, double x, double y, double z, double w)
    {
        double falloff = 0.6d - (x * x) - (y * y) - (z * z) - (w * w);
        if (falloff <= 0d)
        {
            return 0d;
        }

        falloff *= falloff;
        return falloff * falloff * Dot(gradientIndex, x, y, z, w);
    }
}

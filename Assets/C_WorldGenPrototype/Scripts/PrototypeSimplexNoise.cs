using System;
using UnityEngine;

public sealed class PrototypeSimplexNoiseSampler
{
    private readonly PrototypeSimplexNoiseSettingsAsset _settings;
    private readonly PrototypeSimplexNoise2D _noise;

    public PrototypeSimplexNoiseSampler(int seed, PrototypeSimplexNoiseSettingsAsset settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        int combinedSeed = unchecked(seed + (_settings.SeedOffset * 486187739));
        _noise = new PrototypeSimplexNoise2D(combinedSeed);
    }

    public float Sample(int worldX, int worldZ)
    {
        Vector2 coordinateOffset = _settings.CoordinateOffset;
        double baseX = (worldX + coordinateOffset.x) * _settings.Frequency;
        double baseZ = (worldZ + coordinateOffset.y) * _settings.Frequency;

        double value = 0d;
        double amplitude = 1d;
        double amplitudeSum = 0d;
        double frequency = 1d;

        for (int octave = 0; octave < _settings.Octaves; octave++)
        {
            value += _noise.Sample(baseX * frequency, baseZ * frequency) * amplitude;
            amplitudeSum += amplitude;
            frequency *= _settings.Lacunarity;
            amplitude *= _settings.Persistence;
        }

        double normalized = amplitudeSum > 0d ? value / amplitudeSum : 0d;
        return (float)((normalized * _settings.Amplitude) + _settings.Bias);
    }
}

public sealed class PrototypeSimplexNoise3DSampler
{
    private readonly PrototypeSimplexNoise3DSettingsAsset _settings;
    private readonly PrototypeSimplexNoise3D _noise;

    public PrototypeSimplexNoise3DSampler(int seed, PrototypeSimplexNoise3DSettingsAsset settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        int combinedSeed = unchecked(seed + (_settings.SeedOffset * 486187739));
        _noise = new PrototypeSimplexNoise3D(combinedSeed);
    }

    public float Sample(int worldX, int worldY, int worldZ)
    {
        Vector3 coordinateOffset = _settings.CoordinateOffset;
        double baseX = (worldX + coordinateOffset.x) * _settings.Frequency;
        double baseY = (worldY + coordinateOffset.y) * _settings.Frequency;
        double baseZ = (worldZ + coordinateOffset.z) * _settings.Frequency;

        double value = 0d;
        double amplitude = 1d;
        double amplitudeSum = 0d;
        double frequency = 1d;

        for (int octave = 0; octave < _settings.Octaves; octave++)
        {
            value += _noise.Sample(baseX * frequency, baseY * frequency, baseZ * frequency) * amplitude;
            amplitudeSum += amplitude;
            frequency *= _settings.Lacunarity;
            amplitude *= _settings.Persistence;
        }

        double normalized = amplitudeSum > 0d ? value / amplitudeSum : 0d;
        return (float)((normalized * _settings.Amplitude) + _settings.Bias);
    }
}

internal sealed class PrototypeSimplexNoise2D
{
    private const double F2 = 0.3660254037844386d;
    private const double G2 = 0.21132486540518713d;

    private static readonly int[,] Gradients =
    {
        { 1, 1 }, { -1, 1 }, { 1, -1 }, { -1, -1 },
        { 1, 0 }, { -1, 0 }, { 1, 0 }, { -1, 0 },
        { 0, 1 }, { 0, -1 }, { 0, 1 }, { 0, -1 },
    };

    private readonly byte[] _perm = new byte[512];

    public PrototypeSimplexNoise2D(int seed)
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

    public double Sample(double x, double y)
    {
        double skew = (x + y) * F2;
        int i = FastFloor(x + skew);
        int j = FastFloor(y + skew);

        double unskew = (i + j) * G2;
        double x0 = x - (i - unskew);
        double y0 = y - (j - unskew);

        int i1;
        int j1;
        if (x0 > y0)
        {
            i1 = 1;
            j1 = 0;
        }
        else
        {
            i1 = 0;
            j1 = 1;
        }

        double x1 = x0 - i1 + G2;
        double y1 = y0 - j1 + G2;
        double x2 = x0 - 1d + (2d * G2);
        double y2 = y0 - 1d + (2d * G2);

        int ii = i & 255;
        int jj = j & 255;

        int gi0 = _perm[ii + _perm[jj]];
        int gi1 = _perm[ii + i1 + _perm[jj + j1]];
        int gi2 = _perm[ii + 1 + _perm[jj + 1]];

        double n0 = CornerContribution(gi0, x0, y0);
        double n1 = CornerContribution(gi1, x1, y1);
        double n2 = CornerContribution(gi2, x2, y2);

        return 70d * (n0 + n1 + n2);
    }

    private static int FastFloor(double value)
    {
        int integer = (int)value;
        return value < integer ? integer - 1 : integer;
    }

    private static double Dot(int gradientIndex, double x, double y)
    {
        int index = gradientIndex % 12;
        return (Gradients[index, 0] * x) + (Gradients[index, 1] * y);
    }

    private static double CornerContribution(int gradientIndex, double x, double y)
    {
        double falloff = 0.5d - (x * x) - (y * y);
        if (falloff <= 0d)
        {
            return 0d;
        }

        falloff *= falloff;
        return falloff * falloff * Dot(gradientIndex, x, y);
    }
}

internal sealed class PrototypeSimplexNoise3D
{
    private const double F3 = 1d / 3d;
    private const double G3 = 1d / 6d;

    private static readonly int[,] Gradients =
    {
        { 1, 1, 0 }, { -1, 1, 0 }, { 1, -1, 0 }, { -1, -1, 0 },
        { 1, 0, 1 }, { -1, 0, 1 }, { 1, 0, -1 }, { -1, 0, -1 },
        { 0, 1, 1 }, { 0, -1, 1 }, { 0, 1, -1 }, { 0, -1, -1 },
    };

    private readonly byte[] _perm = new byte[512];

    public PrototypeSimplexNoise3D(int seed)
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

    public double Sample(double x, double y, double z)
    {
        double skew = (x + y + z) * F3;
        int i = FastFloor(x + skew);
        int j = FastFloor(y + skew);
        int k = FastFloor(z + skew);

        double unskew = (i + j + k) * G3;
        double x0 = x - (i - unskew);
        double y0 = y - (j - unskew);
        double z0 = z - (k - unskew);

        int i1;
        int j1;
        int k1;
        int i2;
        int j2;
        int k2;

        if (x0 >= y0)
        {
            if (y0 >= z0)
            {
                i1 = 1; j1 = 0; k1 = 0;
                i2 = 1; j2 = 1; k2 = 0;
            }
            else if (x0 >= z0)
            {
                i1 = 1; j1 = 0; k1 = 0;
                i2 = 1; j2 = 0; k2 = 1;
            }
            else
            {
                i1 = 0; j1 = 0; k1 = 1;
                i2 = 1; j2 = 0; k2 = 1;
            }
        }
        else
        {
            if (y0 < z0)
            {
                i1 = 0; j1 = 0; k1 = 1;
                i2 = 0; j2 = 1; k2 = 1;
            }
            else if (x0 < z0)
            {
                i1 = 0; j1 = 1; k1 = 0;
                i2 = 0; j2 = 1; k2 = 1;
            }
            else
            {
                i1 = 0; j1 = 1; k1 = 0;
                i2 = 1; j2 = 1; k2 = 0;
            }
        }

        double x1 = x0 - i1 + G3;
        double y1 = y0 - j1 + G3;
        double z1 = z0 - k1 + G3;
        double x2 = x0 - i2 + (2d * G3);
        double y2 = y0 - j2 + (2d * G3);
        double z2 = z0 - k2 + (2d * G3);
        double x3 = x0 - 1d + (3d * G3);
        double y3 = y0 - 1d + (3d * G3);
        double z3 = z0 - 1d + (3d * G3);

        int ii = i & 255;
        int jj = j & 255;
        int kk = k & 255;

        int gi0 = _perm[ii + _perm[jj + _perm[kk]]];
        int gi1 = _perm[ii + i1 + _perm[jj + j1 + _perm[kk + k1]]];
        int gi2 = _perm[ii + i2 + _perm[jj + j2 + _perm[kk + k2]]];
        int gi3 = _perm[ii + 1 + _perm[jj + 1 + _perm[kk + 1]]];

        double n0 = CornerContribution(gi0, x0, y0, z0);
        double n1 = CornerContribution(gi1, x1, y1, z1);
        double n2 = CornerContribution(gi2, x2, y2, z2);
        double n3 = CornerContribution(gi3, x3, y3, z3);

        return 32d * (n0 + n1 + n2 + n3);
    }

    private static int FastFloor(double value)
    {
        int integer = (int)value;
        return value < integer ? integer - 1 : integer;
    }

    private static double Dot(int gradientIndex, double x, double y, double z)
    {
        int index = gradientIndex % 12;
        return (Gradients[index, 0] * x) + (Gradients[index, 1] * y) + (Gradients[index, 2] * z);
    }

    private static double CornerContribution(int gradientIndex, double x, double y, double z)
    {
        double falloff = 0.6d - (x * x) - (y * y) - (z * z);
        if (falloff <= 0d)
        {
            return 0d;
        }

        falloff *= falloff;
        return falloff * falloff * Dot(gradientIndex, x, y, z);
    }
}

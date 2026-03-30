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

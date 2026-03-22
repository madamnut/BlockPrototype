using System;
using System.Security.Cryptography;
using System.Text;

internal readonly struct VanillaNoiseParameters
{
    public VanillaNoiseParameters(int firstOctave, double[] amplitudes)
    {
        FirstOctave = firstOctave;
        Amplitudes = amplitudes;
    }

    public int FirstOctave { get; }
    public double[] Amplitudes { get; }

    public static VanillaNoiseParameters From(VanillaNoiseSettingsData source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (source.Amplitudes == null || source.Amplitudes.Length == 0)
        {
            throw new InvalidOperationException("Vanilla noise amplitudes must contain at least one entry.");
        }

        double[] amplitudes = new double[source.Amplitudes.Length];
        for (int i = 0; i < source.Amplitudes.Length; i++)
        {
            amplitudes[i] = source.Amplitudes[i];
        }

        return new VanillaNoiseParameters(source.FirstOctave, amplitudes);
    }
}

internal sealed class VanillaNormalNoise
{
    private const double InputFactor = 1.0181268882175227d;

    private readonly VanillaPerlinNoise _first;
    private readonly VanillaPerlinNoise _second;
    private readonly double _valueFactor;

    public VanillaNormalNoise(VanillaXoroshiroRandom random, VanillaNoiseParameters parameters)
    {
        _first = new VanillaPerlinNoise(random, parameters);
        _second = new VanillaPerlinNoise(random, parameters);

        int min = int.MaxValue;
        int max = int.MinValue;
        for (int i = 0; i < parameters.Amplitudes.Length; i++)
        {
            if (parameters.Amplitudes[i] != 0d)
            {
                min = Math.Min(min, i);
                max = Math.Max(max, i);
            }
        }

        double expectedDeviation = 0.1d * (1d + (1d / ((max - min) + 1d)));
        _valueFactor = (1d / 6d) / expectedDeviation;
        MaxValue = (_first.MaxValue + _second.MaxValue) * _valueFactor;
    }

    public double MaxValue { get; }

    public double Sample(double x, double y, double z)
    {
        double x2 = x * InputFactor;
        double y2 = y * InputFactor;
        double z2 = z * InputFactor;
        return (_first.Sample(x, y, z) + _second.Sample(x2, y2, z2)) * _valueFactor;
    }
}

internal sealed class VanillaPerlinNoise
{
    private readonly VanillaImprovedNoise[] _noiseLevels;
    private readonly double[] _amplitudes;
    private readonly double _lowestFreqInputFactor;
    private readonly double _lowestFreqValueFactor;

    public VanillaPerlinNoise(VanillaXoroshiroRandom random, VanillaNoiseParameters parameters)
    {
        VanillaPositionalRandom positional = random.ForkPositional();
        _noiseLevels = new VanillaImprovedNoise[parameters.Amplitudes.Length];
        for (int i = 0; i < parameters.Amplitudes.Length; i++)
        {
            if (parameters.Amplitudes[i] != 0d)
            {
                int octave = parameters.FirstOctave + i;
                _noiseLevels[i] = new VanillaImprovedNoise(positional.FromHashOf($"octave_{octave}"));
            }
        }

        _amplitudes = parameters.Amplitudes;
        _lowestFreqInputFactor = Math.Pow(2d, parameters.FirstOctave);
        _lowestFreqValueFactor = Math.Pow(2d, parameters.Amplitudes.Length - 1) / (Math.Pow(2d, parameters.Amplitudes.Length) - 1d);
        MaxValue = EdgeValue(2d);
    }

    public double MaxValue { get; }

    public double Sample(double x, double y, double z, double yScale = 0d, double yLimit = 0d, bool fixY = false)
    {
        double value = 0d;
        double inputFactor = _lowestFreqInputFactor;
        double valueFactor = _lowestFreqValueFactor;
        for (int i = 0; i < _noiseLevels.Length; i++)
        {
            VanillaImprovedNoise noise = _noiseLevels[i];
            if (noise != null)
            {
                value += _amplitudes[i] * valueFactor * noise.Sample(
                    Wrap(x * inputFactor),
                    fixY ? -noise.YOffset : Wrap(y * inputFactor),
                    Wrap(z * inputFactor),
                    yScale * inputFactor,
                    yLimit * inputFactor);
            }

            inputFactor *= 2d;
            valueFactor /= 2d;
        }

        return value;
    }

    public VanillaImprovedNoise GetOctaveNoise(int index)
    {
        int actualIndex = _noiseLevels.Length - 1 - index;
        return actualIndex >= 0 && actualIndex < _noiseLevels.Length ? _noiseLevels[actualIndex] : null;
    }

    public double EdgeValue(double x)
    {
        double value = 0d;
        double valueFactor = _lowestFreqValueFactor;
        for (int i = 0; i < _noiseLevels.Length; i++)
        {
            if (_noiseLevels[i] != null)
            {
                value += _amplitudes[i] * x * valueFactor;
            }

            valueFactor /= 2d;
        }

        return value;
    }

    public static double Wrap(double value)
    {
        return value - (VanillaNoiseMath.FloorToLong((value / 33554432d) + 0.5d) * 33554432d);
    }
}

internal sealed class VanillaImprovedNoise
{
    private readonly int[] _permutations = new int[256];
    private readonly double _xo;
    private readonly double _yo;
    private readonly double _zo;

    public VanillaImprovedNoise(VanillaXoroshiroRandom random)
    {
        _xo = random.NextDouble() * 256d;
        _yo = random.NextDouble() * 256d;
        _zo = random.NextDouble() * 256d;

        for (int i = 0; i < 256; i++)
        {
            _permutations[i] = i > 127 ? i - 256 : i;
        }

        for (int i = 0; i < 256; i++)
        {
            int j = random.NextInt(256 - i);
            int temp = _permutations[i];
            _permutations[i] = _permutations[i + j];
            _permutations[i + j] = temp;
        }
    }

    public double YOffset => _yo;

    public double Sample(double x, double y, double z, double yScale = 0d, double yLimit = 0d)
    {
        double x2 = x + _xo;
        double y2 = y + _yo;
        double z2 = z + _zo;
        int x3 = (int)Math.Floor(x2);
        int y3 = (int)Math.Floor(y2);
        int z3 = (int)Math.Floor(z2);
        double x4 = x2 - x3;
        double y4 = y2 - y3;
        double z4 = z2 - z3;

        double yOffset = 0d;
        if (yScale != 0d)
        {
            double limited = yLimit >= 0d && yLimit < y4 ? yLimit : y4;
            yOffset = Math.Floor((limited / yScale) + 1e-7d) * yScale;
        }

        return SampleAndLerp(x3, y3, z3, x4, y4 - yOffset, z4, y4);
    }

    private double SampleAndLerp(int a, int b, int c, double d, double e, double f, double g)
    {
        int h = Permutation(a);
        int i = Permutation(a + 1);
        int j = Permutation(h + b);
        int k = Permutation(h + b + 1);
        int l = Permutation(i + b);
        int m = Permutation(i + b + 1);

        double n = VanillaNoiseMath.GradDot(Permutation(j + c), d, e, f);
        double o = VanillaNoiseMath.GradDot(Permutation(l + c), d - 1d, e, f);
        double p = VanillaNoiseMath.GradDot(Permutation(k + c), d, e - 1d, f);
        double q = VanillaNoiseMath.GradDot(Permutation(m + c), d - 1d, e - 1d, f);
        double r = VanillaNoiseMath.GradDot(Permutation(j + c + 1), d, e, f - 1d);
        double s = VanillaNoiseMath.GradDot(Permutation(l + c + 1), d - 1d, e, f - 1d);
        double t = VanillaNoiseMath.GradDot(Permutation(k + c + 1), d, e - 1d, f - 1d);
        double u = VanillaNoiseMath.GradDot(Permutation(m + c + 1), d - 1d, e - 1d, f - 1d);

        double v = VanillaNoiseMath.Smoothstep(d);
        double w = VanillaNoiseMath.Smoothstep(g);
        double x = VanillaNoiseMath.Smoothstep(f);
        return VanillaNoiseMath.Lerp3(v, w, x, n, o, p, q, r, s, t, u);
    }

    private int Permutation(int index)
    {
        return _permutations[index & 0xFF] & 0xFF;
    }
}

internal sealed class VanillaBlendedNoise
{
    private readonly VanillaPerlinNoise _minLimitNoise;
    private readonly VanillaPerlinNoise _maxLimitNoise;
    private readonly VanillaPerlinNoise _mainNoise;
    private readonly double _xzMultiplier;
    private readonly double _yMultiplier;

    public VanillaBlendedNoise(
        VanillaXoroshiroRandom random,
        double xzScale,
        double yScale,
        double xzFactor,
        double yFactor,
        double smearScaleMultiplier)
    {
        XZScale = xzScale;
        YScale = yScale;
        XZFactor = xzFactor;
        YFactor = yFactor;
        SmearScaleMultiplier = smearScaleMultiplier;

        _minLimitNoise = new VanillaPerlinNoise(random, new VanillaNoiseParameters(-15, CreateFilledArray(16, 1d)));
        _maxLimitNoise = new VanillaPerlinNoise(random, new VanillaNoiseParameters(-15, CreateFilledArray(16, 1d)));
        _mainNoise = new VanillaPerlinNoise(random, new VanillaNoiseParameters(-7, CreateFilledArray(8, 1d)));
        _xzMultiplier = 684.412d * xzScale;
        _yMultiplier = 684.412d * yScale;
        MaxValue = _minLimitNoise.EdgeValue(_yMultiplier + 2d);
    }

    public double XZScale { get; }
    public double YScale { get; }
    public double XZFactor { get; }
    public double YFactor { get; }
    public double SmearScaleMultiplier { get; }
    public double MaxValue { get; }

    public double Sample(double x, double y, double z)
    {
        double scaledX = x * _xzMultiplier;
        double scaledY = y * _yMultiplier;
        double scaledZ = z * _xzMultiplier;

        double factoredX = scaledX / XZFactor;
        double factoredY = scaledY / YFactor;
        double factoredZ = scaledZ / XZFactor;

        double smear = _yMultiplier * SmearScaleMultiplier;
        double factoredSmear = smear / YFactor;

        VanillaImprovedNoise noise;
        double value = 0d;
        double factor = 1d;
        for (int i = 0; i < 8; i++)
        {
            noise = _mainNoise.GetOctaveNoise(i);
            if (noise != null)
            {
                double xx = VanillaPerlinNoise.Wrap(factoredX * factor);
                double yy = VanillaPerlinNoise.Wrap(factoredY * factor);
                double zz = VanillaPerlinNoise.Wrap(factoredZ * factor);
                value += noise.Sample(xx, yy, zz, factoredSmear * factor, factoredY * factor) / factor;
            }

            factor /= 2d;
        }

        value = ((value / 10d) + 1d) / 2d;
        factor = 1d;
        double min = 0d;
        double max = 0d;
        for (int i = 0; i < 16; i++)
        {
            double xx = VanillaPerlinNoise.Wrap(scaledX * factor);
            double yy = VanillaPerlinNoise.Wrap(scaledY * factor);
            double zz = VanillaPerlinNoise.Wrap(scaledZ * factor);
            double smearScaled = smear * factor;

            if (value < 1d && (noise = _minLimitNoise.GetOctaveNoise(i)) != null)
            {
                min += noise.Sample(xx, yy, zz, smearScaled, scaledY * factor) / factor;
            }

            if (value > 0d && (noise = _maxLimitNoise.GetOctaveNoise(i)) != null)
            {
                max += noise.Sample(xx, yy, zz, smearScaled, scaledY * factor) / factor;
            }

            factor /= 2d;
        }

        return VanillaNoiseMath.ClampedLerp(min / 512d, max / 512d, value) / 128d;
    }

    private static double[] CreateFilledArray(int count, double value)
    {
        double[] result = new double[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = value;
        }

        return result;
    }
}

internal sealed class VanillaPositionalRandom
{
    private readonly ulong _seedLo;
    private readonly ulong _seedHi;

    public VanillaPositionalRandom(ulong seedLo, ulong seedHi)
    {
        _seedLo = seedLo;
        _seedHi = seedHi;
    }

    public VanillaXoroshiroRandom FromHashOf(string name)
    {
        byte[] hash;
        using (MD5 md5 = MD5.Create())
        {
            hash = md5.ComputeHash(Encoding.UTF8.GetBytes(name));
        }

        ulong lo = VanillaNoiseMath.ReadUInt64BigEndian(hash, 0);
        ulong hi = VanillaNoiseMath.ReadUInt64BigEndian(hash, 8);
        return new VanillaXoroshiroRandom(lo ^ _seedLo, hi ^ _seedHi);
    }

    public VanillaXoroshiroRandom At(int x, int y, int z)
    {
        long hashedSeed = VanillaNoiseMath.GetCoordinateSeed(x, y, z);
        ulong seedLo = unchecked((ulong)hashedSeed) ^ _seedLo;
        return new VanillaXoroshiroRandom(seedLo, _seedHi);
    }
}

internal sealed class VanillaXoroshiroRandom
{
    private const ulong SilverRatio64 = 7640891576956012809UL;
    private const ulong GoldenRatio64 = unchecked((ulong)-7046029254386353131L);
    private const ulong Stafford1 = unchecked((ulong)-4658895280553007687L);
    private const ulong Stafford2 = unchecked((ulong)-7723592293110705685L);
    private const double DoubleMultiplier = 1.1102230246251565E-16d;

    private ulong _seedLo;
    private ulong _seedHi;

    public VanillaXoroshiroRandom(ulong seedLo, ulong seedHi)
    {
        _seedLo = seedLo;
        _seedHi = seedHi;
    }

    public static VanillaXoroshiroRandom Create(int seed)
    {
        ulong seedValue = unchecked((ulong)(long)seed);
        ulong seedLo = seedValue ^ SilverRatio64;
        ulong seedHi = seedLo + GoldenRatio64;
        return new VanillaXoroshiroRandom(MixStafford13(seedLo), MixStafford13(seedHi));
    }

    public VanillaPositionalRandom ForkPositional()
    {
        return new VanillaPositionalRandom(NextULong(), NextULong());
    }

    public int NextInt(int max)
    {
        if (max <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(max));
        }

        uint value = (uint)NextULong();
        ulong product = (ulong)value * (uint)max;
        uint productLo = (uint)product;
        if (productLo < max)
        {
            uint threshold = unchecked((uint)(-(uint)max)) % (uint)max;
            while (productLo < threshold)
            {
                value = (uint)NextULong();
                product = (ulong)value * (uint)max;
                productLo = (uint)product;
            }
        }

        return (int)(product >> 32);
    }

    public double NextDouble()
    {
        return (NextULong() >> 11) * DoubleMultiplier;
    }

    public float NextFloat()
    {
        return (float)(NextULong() >> 40) * 5.9604645E-8f;
    }

    private ulong NextULong()
    {
        ulong seedLo = _seedLo;
        ulong seedHi = _seedHi;
        ulong value = VanillaNoiseMath.RotateLeft(seedLo + seedHi, 17) + seedLo;

        seedHi ^= seedLo;
        _seedLo = VanillaNoiseMath.RotateLeft(seedLo, 49) ^ seedHi ^ (seedHi << 21);
        _seedHi = VanillaNoiseMath.RotateLeft(seedHi, 28);
        return value;
    }

    private static ulong MixStafford13(ulong value)
    {
        value = ((value ^ (value >> 30)) * Stafford1);
        value = ((value ^ (value >> 27)) * Stafford2);
        return value ^ (value >> 31);
    }
}

internal static class VanillaNoiseMath
{
    private static readonly int[,] Gradient =
    {
        { 1, 1, 0 }, { -1, 1, 0 }, { 1, -1, 0 }, { -1, -1, 0 },
        { 1, 0, 1 }, { -1, 0, 1 }, { 1, 0, -1 }, { -1, 0, -1 },
        { 0, 1, 1 }, { 0, -1, 1 }, { 0, 1, -1 }, { 0, -1, -1 },
        { 1, 1, 0 }, { 0, -1, 1 }, { -1, 1, 0 }, { 0, -1, -1 },
    };

    public static ulong RotateLeft(ulong value, int shift)
    {
        return (value << shift) | (value >> (64 - shift));
    }

    public static long FloorToLong(double value)
    {
        return (long)Math.Floor(value);
    }

    public static double Smoothstep(double value)
    {
        return value * value * value * (value * ((value * 6d) - 15d) + 10d);
    }

    public static double Lerp(double delta, double min, double max)
    {
        return min + (delta * (max - min));
    }

    public static double Lerp2(double a, double b, double c, double d, double e, double f)
    {
        return Lerp(b, Lerp(a, c, d), Lerp(a, e, f));
    }

    public static double Lerp3(double a, double b, double c, double d, double e, double f, double g, double h, double i, double j, double k)
    {
        return Lerp(c, Lerp2(a, b, d, e, f, g), Lerp2(a, b, h, i, j, k));
    }

    public static double ClampedLerp(double min, double max, double delta)
    {
        if (delta < 0d)
        {
            return min;
        }

        if (delta > 1d)
        {
            return max;
        }

        return Lerp(delta, min, max);
    }

    public static double GradDot(int hash, double x, double y, double z)
    {
        int index = hash & 15;
        return (Gradient[index, 0] * x) + (Gradient[index, 1] * y) + (Gradient[index, 2] * z);
    }

    public static ulong ReadUInt64BigEndian(byte[] bytes, int startIndex)
    {
        ulong value = 0UL;
        for (int i = 0; i < 8; i++)
        {
            value = (value << 8) | bytes[startIndex + i];
        }

        return value;
    }

    public static long GetCoordinateSeed(int x, int y, int z)
    {
        long value = (x * 3129871L) ^ (z * 116129781L) ^ y;
        value = value * value * 42317861L + value * 11L;
        return value >> 16;
    }
}

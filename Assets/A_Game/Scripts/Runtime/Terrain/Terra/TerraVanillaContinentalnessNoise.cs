using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public sealed class TerraVanillaContinentalnessNoise
{
    private TerraVanillaNormalNoise _offsetNoise;
    private TerraVanillaNormalNoise _targetNoise;
    private double _scale;
    private double _shiftInputScale;
    private double _targetXZScale;
    private double _shiftStrength;

    public TerraVanillaContinentalnessNoise(int seed, TerraContinentalnessSettingsAsset settings)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        if (settings.OffsetNoise == null)
        {
            throw new InvalidOperationException("Terra continentalness settings are missing offset noise parameters.");
        }

        if (settings.ContinentalnessNoise == null)
        {
            throw new InvalidOperationException("Terra continentalness settings are missing continentalness noise parameters.");
        }

        Initialize(
            seed,
            "minecraft:offset",
            "minecraft:continentalness",
            settings.Scale,
            settings.ShiftInputScale,
            settings.ContinentsXZScale,
            settings.ShiftStrength,
            settings.OffsetNoise,
            settings.ContinentalnessNoise);
    }

    public TerraVanillaContinentalnessNoise(
        int seed,
        string shiftNoiseHashName,
        string targetNoiseHashName,
        float scale,
        float shiftInputScale,
        float targetXZScale,
        float shiftStrength,
        TerraVanillaNoiseSettingsData offsetNoise,
        TerraVanillaNoiseSettingsData targetNoise)
    {
        Initialize(seed, shiftNoiseHashName, targetNoiseHashName, scale, shiftInputScale, targetXZScale, shiftStrength, offsetNoise, targetNoise);
    }

    private void Initialize(
        int seed,
        string shiftNoiseHashName,
        string targetNoiseHashName,
        float scale,
        float shiftInputScale,
        float targetXZScale,
        float shiftStrength,
        TerraVanillaNoiseSettingsData offsetNoise,
        TerraVanillaNoiseSettingsData targetNoise)
    {
        if (offsetNoise == null)
        {
            throw new InvalidOperationException("Shifted noise requires offset noise parameters.");
        }

        if (targetNoise == null)
        {
            throw new InvalidOperationException("Shifted noise requires target noise parameters.");
        }

        _scale = scale;
        _shiftInputScale = shiftInputScale;
        _targetXZScale = targetXZScale;
        _shiftStrength = shiftStrength;

        TerraVanillaNoiseParameters offsetParameters = TerraVanillaNoiseParameters.From(offsetNoise);
        TerraVanillaNoiseParameters targetParameters = TerraVanillaNoiseParameters.From(targetNoise);

        TerraVanillaXoroshiroRandom random = TerraVanillaXoroshiroRandom.Create(seed);
        TerraVanillaPositionalRandom positional = random.ForkPositional();
        _offsetNoise = new TerraVanillaNormalNoise(positional.FromHashOf(shiftNoiseHashName), offsetParameters);
        _targetNoise = new TerraVanillaNormalNoise(positional.FromHashOf(targetNoiseHashName), targetParameters);
    }

    public float Sample(int worldX, int worldZ)
    {
        double scaledX = worldX * _scale;
        double scaledZ = worldZ * _scale;

        double shiftX = _offsetNoise.Sample(scaledX * _shiftInputScale, 0d, scaledZ * _shiftInputScale) * _shiftStrength;
        double shiftZ = _offsetNoise.Sample(scaledZ * _shiftInputScale, scaledX * _shiftInputScale, 0d) * _shiftStrength;

        double sampleX = (scaledX * _targetXZScale) + shiftX;
        double sampleZ = (scaledZ * _targetXZScale) + shiftZ;
        return (float)_targetNoise.Sample(sampleX, 0d, sampleZ);
    }

    private readonly struct TerraVanillaNoiseParameters
    {
        public TerraVanillaNoiseParameters(int firstOctave, double[] amplitudes)
        {
            FirstOctave = firstOctave;
            Amplitudes = amplitudes;
        }

        public int FirstOctave { get; }
        public double[] Amplitudes { get; }

        public static TerraVanillaNoiseParameters From(TerraVanillaNoiseSettingsData source)
        {
            if (source.Amplitudes == null || source.Amplitudes.Length == 0)
            {
                throw new InvalidOperationException("Vanilla noise amplitudes must contain at least one entry.");
            }

            double[] amplitudes = new double[source.Amplitudes.Length];
            for (int i = 0; i < source.Amplitudes.Length; i++)
            {
                amplitudes[i] = source.Amplitudes[i];
            }

            return new TerraVanillaNoiseParameters(source.FirstOctave, amplitudes);
        }
    }

    private sealed class TerraVanillaNormalNoise
    {
        private const double InputFactor = 1.0181268882175227d;

        private readonly TerraVanillaPerlinNoise _first;
        private readonly TerraVanillaPerlinNoise _second;
        private readonly double _valueFactor;

        public TerraVanillaNormalNoise(TerraVanillaXoroshiroRandom random, TerraVanillaNoiseParameters parameters)
        {
            _first = new TerraVanillaPerlinNoise(random, parameters);
            _second = new TerraVanillaPerlinNoise(random, parameters);

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
        }

        public double Sample(double x, double y, double z)
        {
            double x2 = x * InputFactor;
            double y2 = y * InputFactor;
            double z2 = z * InputFactor;
            return (_first.Sample(x, y, z) + _second.Sample(x2, y2, z2)) * _valueFactor;
        }
    }

    private sealed class TerraVanillaPerlinNoise
    {
        private readonly TerraVanillaImprovedNoise[] _noiseLevels;
        private readonly double[] _amplitudes;
        private readonly double _lowestFreqInputFactor;
        private readonly double _lowestFreqValueFactor;

        public TerraVanillaPerlinNoise(TerraVanillaXoroshiroRandom random, TerraVanillaNoiseParameters parameters)
        {
            TerraVanillaPositionalRandom positional = random.ForkPositional();
            _noiseLevels = new TerraVanillaImprovedNoise[parameters.Amplitudes.Length];
            for (int i = 0; i < parameters.Amplitudes.Length; i++)
            {
                if (parameters.Amplitudes[i] != 0d)
                {
                    int octave = parameters.FirstOctave + i;
                    _noiseLevels[i] = new TerraVanillaImprovedNoise(positional.FromHashOf($"octave_{octave}"));
                }
            }

            _amplitudes = parameters.Amplitudes;
            _lowestFreqInputFactor = Math.Pow(2d, parameters.FirstOctave);
            _lowestFreqValueFactor = Math.Pow(2d, parameters.Amplitudes.Length - 1) / (Math.Pow(2d, parameters.Amplitudes.Length) - 1d);
        }

        public double Sample(double x, double y, double z)
        {
            double value = 0d;
            double inputFactor = _lowestFreqInputFactor;
            double valueFactor = _lowestFreqValueFactor;
            for (int i = 0; i < _noiseLevels.Length; i++)
            {
                TerraVanillaImprovedNoise noise = _noiseLevels[i];
                if (noise != null)
                {
                    value += _amplitudes[i] * valueFactor * noise.Sample(
                        Wrap(x * inputFactor),
                        Wrap(y * inputFactor),
                        Wrap(z * inputFactor));
                }

                inputFactor *= 2d;
                valueFactor /= 2d;
            }

            return value;
        }

        private static double Wrap(double value)
        {
            return value - (Math.Floor((value / 33554432d) + 0.5d) * 33554432d);
        }
    }

    private sealed class TerraVanillaImprovedNoise
    {
        private readonly int[] _permutations = new int[256];
        private readonly double _xo;
        private readonly double _yo;
        private readonly double _zo;

        public TerraVanillaImprovedNoise(TerraVanillaXoroshiroRandom random)
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
                int tmp = _permutations[i];
                _permutations[i] = _permutations[i + j];
                _permutations[i + j] = tmp;
            }
        }

        public double Sample(double x, double y, double z)
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

            return SampleAndLerp(x3, y3, z3, x4, y4, z4, y4);
        }

        private double SampleAndLerp(int a, int b, int c, double d, double e, double f, double g)
        {
            int h = Permutation(a);
            int i = Permutation(a + 1);
            int j = Permutation(h + b);
            int k = Permutation(h + b + 1);
            int l = Permutation(i + b);
            int m = Permutation(i + b + 1);

            double n = TerraVanillaNoiseMath.GradDot(Permutation(j + c), d, e, f);
            double o = TerraVanillaNoiseMath.GradDot(Permutation(l + c), d - 1d, e, f);
            double p = TerraVanillaNoiseMath.GradDot(Permutation(k + c), d, e - 1d, f);
            double q = TerraVanillaNoiseMath.GradDot(Permutation(m + c), d - 1d, e - 1d, f);
            double r = TerraVanillaNoiseMath.GradDot(Permutation(j + c + 1), d, e, f - 1d);
            double s = TerraVanillaNoiseMath.GradDot(Permutation(l + c + 1), d - 1d, e, f - 1d);
            double t = TerraVanillaNoiseMath.GradDot(Permutation(k + c + 1), d, e - 1d, f - 1d);
            double u = TerraVanillaNoiseMath.GradDot(Permutation(m + c + 1), d - 1d, e - 1d, f - 1d);

            double v = TerraVanillaNoiseMath.Smoothstep(d);
            double w = TerraVanillaNoiseMath.Smoothstep(g);
            double x = TerraVanillaNoiseMath.Smoothstep(f);
            return TerraVanillaNoiseMath.Lerp3(v, w, x, n, o, p, q, r, s, t, u);
        }

        private int Permutation(int index)
        {
            return _permutations[index & 0xFF] & 0xFF;
        }
    }

    private sealed class TerraVanillaPositionalRandom
    {
        private readonly ulong _seedLo;
        private readonly ulong _seedHi;

        public TerraVanillaPositionalRandom(ulong seedLo, ulong seedHi)
        {
            _seedLo = seedLo;
            _seedHi = seedHi;
        }

        public TerraVanillaXoroshiroRandom FromHashOf(string name)
        {
            byte[] hash;
            using (MD5 md5 = MD5.Create())
            {
                hash = md5.ComputeHash(Encoding.UTF8.GetBytes(name));
            }

            ulong lo = TerraVanillaNoiseMath.ReadUInt64BigEndian(hash, 0);
            ulong hi = TerraVanillaNoiseMath.ReadUInt64BigEndian(hash, 8);
            return new TerraVanillaXoroshiroRandom(lo ^ _seedLo, hi ^ _seedHi);
        }
    }

    private sealed class TerraVanillaXoroshiroRandom
    {
        private const ulong SilverRatio64 = 7640891576956012809UL;
        private const ulong GoldenRatio64 = unchecked((ulong)-7046029254386353131L);
        private const ulong Stafford1 = unchecked((ulong)-4658895280553007687L);
        private const ulong Stafford2 = unchecked((ulong)-7723592293110705685L);
        private const double DoubleMultiplier = 1.1102230246251565E-16d;

        private ulong _seedLo;
        private ulong _seedHi;

        public TerraVanillaXoroshiroRandom(ulong seedLo, ulong seedHi)
        {
            _seedLo = seedLo;
            _seedHi = seedHi;
        }

        public static TerraVanillaXoroshiroRandom Create(int seed)
        {
            ulong seedValue = unchecked((ulong)(long)seed);
            ulong seedLo = seedValue ^ SilverRatio64;
            ulong seedHi = seedLo + GoldenRatio64;
            return new TerraVanillaXoroshiroRandom(MixStafford13(seedLo), MixStafford13(seedHi));
        }

        public TerraVanillaPositionalRandom ForkPositional()
        {
            return new TerraVanillaPositionalRandom(NextULong(), NextULong());
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

        private ulong NextULong()
        {
            ulong seedLo = _seedLo;
            ulong seedHi = _seedHi;
            ulong value = TerraVanillaNoiseMath.RotateLeft(seedLo + seedHi, 17) + seedLo;

            seedHi ^= seedLo;
            _seedLo = TerraVanillaNoiseMath.RotateLeft(seedLo, 49) ^ seedHi ^ (seedHi << 21);
            _seedHi = TerraVanillaNoiseMath.RotateLeft(seedHi, 28);
            return value;
        }

        private static ulong MixStafford13(ulong value)
        {
            value = ((value ^ (value >> 30)) * Stafford1);
            value = ((value ^ (value >> 27)) * Stafford2);
            return value ^ (value >> 31);
        }
    }

    private static class TerraVanillaNoiseMath
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
    }
}

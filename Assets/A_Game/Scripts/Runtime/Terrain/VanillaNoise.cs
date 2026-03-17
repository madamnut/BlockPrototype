using System;
using System.Security.Cryptography;
using System.Text;
using Unity.Collections;
using Unity.Mathematics;

public struct VanillaImprovedNoiseOctave
{
    public int permutationStart;
    public float xo;
    public float yo;
    public float zo;
    public byte exists;
}

public readonly struct VanillaBlendedNoiseManaged
{
    public readonly VanillaImprovedNoiseOctave[] minLimitOctaves;
    public readonly int[] minLimitPermutations;
    public readonly VanillaImprovedNoiseOctave[] maxLimitOctaves;
    public readonly int[] maxLimitPermutations;
    public readonly VanillaImprovedNoiseOctave[] mainOctaves;
    public readonly int[] mainPermutations;

    public VanillaBlendedNoiseManaged(
        VanillaImprovedNoiseOctave[] minLimitOctaves,
        int[] minLimitPermutations,
        VanillaImprovedNoiseOctave[] maxLimitOctaves,
        int[] maxLimitPermutations,
        VanillaImprovedNoiseOctave[] mainOctaves,
        int[] mainPermutations)
    {
        this.minLimitOctaves = minLimitOctaves;
        this.minLimitPermutations = minLimitPermutations;
        this.maxLimitOctaves = maxLimitOctaves;
        this.maxLimitPermutations = maxLimitPermutations;
        this.mainOctaves = mainOctaves;
        this.mainPermutations = mainPermutations;
    }
}

public readonly struct VanillaNormalNoiseManaged
{
    public readonly VanillaImprovedNoiseOctave[] firstOctaves;
    public readonly int[] firstPermutations;
    public readonly VanillaImprovedNoiseOctave[] secondOctaves;
    public readonly int[] secondPermutations;
    public readonly float valueFactor;

    public VanillaNormalNoiseManaged(
        VanillaImprovedNoiseOctave[] firstOctaves,
        int[] firstPermutations,
        VanillaImprovedNoiseOctave[] secondOctaves,
        int[] secondPermutations,
        float valueFactor)
    {
        this.firstOctaves = firstOctaves;
        this.firstPermutations = firstPermutations;
        this.secondOctaves = secondOctaves;
        this.secondPermutations = secondPermutations;
        this.valueFactor = valueFactor;
    }
}

public readonly struct VanillaClimateNoiseManaged
{
    public readonly VanillaNormalNoiseManaged offset;
    public readonly VanillaNormalNoiseManaged continentalness;
    public readonly VanillaNormalNoiseManaged erosion;
    public readonly VanillaNormalNoiseManaged ridge;

    public VanillaClimateNoiseManaged(
        VanillaNormalNoiseManaged offset,
        VanillaNormalNoiseManaged continentalness,
        VanillaNormalNoiseManaged erosion,
        VanillaNormalNoiseManaged ridge)
    {
        this.offset = offset;
        this.continentalness = continentalness;
        this.erosion = erosion;
        this.ridge = ridge;
    }
}

internal interface INoiseRandom
{
    void Consume(int count);
    int NextInt(int max);
    double NextDouble();
}

internal struct LegacyRandom48
    : INoiseRandom
{
    private const ulong ModulusMask = 281474976710655UL;
    private const ulong Multiplier = 25214903917UL;
    private const ulong Increment = 11UL;
    private const float FloatMultiplier = 1f / (1 << 24);
    private const double DoubleMultiplier = 1.0 / (1L << 30);

    private ulong _seed;

    public LegacyRandom48(long seed)
    {
        _seed = 0UL;
        SetSeed(seed);
    }

    public void SetSeed(long seed)
    {
        _seed = (((ulong)seed) ^ Multiplier) & ModulusMask;
    }

    public void Consume(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Advance();
        }
    }

    public int NextInt()
    {
        return Next(32);
    }

    public int NextInt(int max)
    {
        if (max <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(max));
        }

        if ((max & (max - 1)) == 0)
        {
            return (int)((max * (long)Next(31)) >> 31);
        }

        int bits;
        int value;
        do
        {
            bits = Next(31);
            value = bits % max;
        }
        while (bits - value + (max - 1) < 0);

        return value;
    }

    public long NextLong()
    {
        return ((long)Next(32) << 32) + (uint)Next(32);
    }

    public float NextFloat()
    {
        return Next(24) * FloatMultiplier;
    }

    public double NextDouble()
    {
        int value = Next(30);
        Advance();
        return value * DoubleMultiplier;
    }

    private int Next(int bits)
    {
        Advance();
        return (int)(_seed >> (48 - bits));
    }

    private void Advance()
    {
        _seed = (_seed * Multiplier + Increment) & ModulusMask;
    }
}

internal struct XoroshiroRandom128 : INoiseRandom
{
    private const ulong SilverRatio64 = 7640891576956012809UL;
    private const ulong GoldenRatio64 = unchecked((ulong)-7046029254386353131L);
    private const ulong Stafford1 = unchecked((ulong)-4658895280553007687L);
    private const ulong Stafford2 = unchecked((ulong)-7723592293110705685L);
    private const double DoubleMultiplier = 1.1102230246251565E-16;

    private ulong _seedLo;
    private ulong _seedHi;

    public XoroshiroRandom128(ulong seedLo, ulong seedHi)
    {
        _seedLo = seedLo;
        _seedHi = seedHi;
    }

    public static XoroshiroRandom128 Create(long seed)
    {
        UpgradeSeedTo128Bit(seed, out ulong seedLo, out ulong seedHi);
        return new XoroshiroRandom128(seedLo, seedHi);
    }

    public XoroshiroPositionalRandom ForkPositional()
    {
        ulong seedLo = NextUInt64();
        ulong seedHi = NextUInt64();
        return new XoroshiroPositionalRandom(seedLo, seedHi);
    }

    public void Consume(int count)
    {
        for (int i = 0; i < count; i++)
        {
            NextUInt64();
        }
    }

    public int NextInt(int max)
    {
        if (max <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(max));
        }

        ulong value = NextUInt64() & 0xFFFFFFFFUL;
        ulong maxValue = (uint)max;
        ulong product = value * maxValue;
        uint productLow = (uint)product;
        if (productLow < maxValue)
        {
            ulong threshold = ((~maxValue) + 1UL) % maxValue;
            while (productLow < threshold)
            {
                value = NextUInt64() & 0xFFFFFFFFUL;
                product = value * maxValue;
                productLow = (uint)product;
            }
        }

        return (int)(product >> 32);
    }

    public double NextDouble()
    {
        return (NextUInt64() >> 11) * DoubleMultiplier;
    }

    private ulong NextUInt64()
    {
        ulong seedLo = _seedLo;
        ulong seedHi = _seedHi;
        ulong value = RotateLeft(seedLo + seedHi, 17) + seedLo;

        seedHi ^= seedLo;
        _seedLo = RotateLeft(seedLo, 49) ^ seedHi ^ (seedHi << 21);
        _seedHi = RotateLeft(seedHi, 28);
        return value;
    }

    private static void UpgradeSeedTo128Bit(long seed, out ulong seedLo, out ulong seedHi)
    {
        ulong seedValue = unchecked((ulong)seed);
        ulong mixedLo = seedValue ^ SilverRatio64;
        ulong mixedHi = mixedLo + GoldenRatio64;
        seedLo = MixStafford13(mixedLo);
        seedHi = MixStafford13(mixedHi);
    }

    private static ulong MixStafford13(ulong value)
    {
        value = (value ^ (value >> 30)) * Stafford1;
        value = (value ^ (value >> 27)) * Stafford2;
        return value ^ (value >> 31);
    }

    private static ulong RotateLeft(ulong value, int shift)
    {
        return (value << shift) | (value >> (64 - shift));
    }
}

internal readonly struct XoroshiroPositionalRandom
{
    private readonly ulong _seedLo;
    private readonly ulong _seedHi;

    public XoroshiroPositionalRandom(ulong seedLo, ulong seedHi)
    {
        _seedLo = seedLo;
        _seedHi = seedHi;
    }

    public XoroshiroRandom128 FromHashOf(string name)
    {
        VanillaNoise.HashNameSeed128(name, out ulong hashLo, out ulong hashHi);
        return new XoroshiroRandom128(_seedLo ^ hashLo, _seedHi ^ hashHi);
    }
}

public static class VanillaNoise
{
    private const int PermutationSize = 256;
    private const float PerlinWrap = 3.3554432e7f;
    private const float NormalNoiseInputFactor = 1.0181268882175227f;
    private const float BlendedNoiseXzScale = 0.25f;
    private const float BlendedNoiseYScale = 0.125f;
    private const float BlendedNoiseXzFactor = 80f;
    private const float BlendedNoiseYFactor = 160f;
    private const float BlendedNoiseSmearScaleMultiplier = 8f;
    private static readonly int[] Gradient = {
        1, 1, 0,  -1, 1, 0,  1, -1, 0,  -1, -1, 0,
        1, 0, 1,  -1, 0, 1,  1, 0, -1, -1, 0, -1,
        0, 1, 1,  0, -1, 1, 0, 1, -1, 0, -1, -1,
        1, 1, 0,  0, -1, 1, -1, 1, 0, 0, -1, -1
    };
    private static readonly float[] OffsetAmplitudes = { 1f, 1f, 1f, 0f };
    private static readonly float[] ContinentalnessAmplitudes = { 1f, 1f, 2f, 2f, 2f, 1f, 1f, 1f, 1f };
    private static readonly float[] ErosionAmplitudes = { 1f, 1f, 0f, 1f, 1f };
    private static readonly float[] RidgeAmplitudes = { 1f, 2f, 1f, 0f, 0f, 0f };

    public static VanillaBlendedNoiseManaged CreateManagedOverworldBlendedNoise(int seed)
    {
        XoroshiroRandom128 random = XoroshiroRandom128.Create(seed);
        XoroshiroRandom128 terrainRandom = random.ForkPositional().FromHashOf("minecraft:terrain");
        BuildLegacyStylePerlin(ref terrainRandom, -15, 16, out VanillaImprovedNoiseOctave[] minOctaves, out int[] minPermutations);
        BuildLegacyStylePerlin(ref terrainRandom, -15, 16, out VanillaImprovedNoiseOctave[] maxOctaves, out int[] maxPermutations);
        BuildLegacyStylePerlin(ref terrainRandom, -7, 8, out VanillaImprovedNoiseOctave[] mainOctaves, out int[] mainPermutations);
        return new VanillaBlendedNoiseManaged(
            minOctaves,
            minPermutations,
            maxOctaves,
            maxPermutations,
            mainOctaves,
            mainPermutations);
    }

    public static VanillaNormalNoiseManaged CreateManagedJaggedNoise(int seed)
    {
        return CreateManagedNormalNoiseXoroshiro(seed, "minecraft:jagged", -16, CreateFilledAmplitudes(16, 1f));
    }

    public static VanillaClimateNoiseManaged CreateManagedOverworldClimateNoise(int seed)
    {
        VanillaNormalNoiseManaged offset = CreateManagedNormalNoiseXoroshiro(seed, "minecraft:offset", -3, OffsetAmplitudes);
        VanillaNormalNoiseManaged continentalness = CreateManagedNormalNoiseXoroshiro(seed, "minecraft:continentalness", -9, ContinentalnessAmplitudes);
        VanillaNormalNoiseManaged erosion = CreateManagedNormalNoiseXoroshiro(seed, "minecraft:erosion", -9, ErosionAmplitudes);
        VanillaNormalNoiseManaged ridge = CreateManagedNormalNoiseXoroshiro(seed, "minecraft:ridge", -7, RidgeAmplitudes);
        return new VanillaClimateNoiseManaged(offset, continentalness, erosion, ridge);
    }

    public static VanillaNormalNoiseManaged CreateManagedNormalNoiseXoroshiro(int seed, string key, int firstOctave, float[] amplitudes)
    {
        XoroshiroRandom128 random = XoroshiroRandom128.Create(seed);
        XoroshiroRandom128 keyedRandom = random.ForkPositional().FromHashOf(key);
        BuildXoroshiroPerlin(ref keyedRandom, firstOctave, amplitudes, out VanillaImprovedNoiseOctave[] firstOctaves, out int[] firstPermutations);
        BuildXoroshiroPerlin(ref keyedRandom, firstOctave, amplitudes, out VanillaImprovedNoiseOctave[] secondOctaves, out int[] secondPermutations);
        return new VanillaNormalNoiseManaged(
            firstOctaves,
            firstPermutations,
            secondOctaves,
            secondPermutations,
            CalculateNormalNoiseValueFactor(amplitudes));
    }

    public static NativeArray<VanillaImprovedNoiseOctave> CreateNativeOctaves(
        VanillaImprovedNoiseOctave[] managedOctaves,
        Allocator allocator)
    {
        if (managedOctaves == null || managedOctaves.Length == 0)
        {
            return new NativeArray<VanillaImprovedNoiseOctave>(0, allocator, NativeArrayOptions.UninitializedMemory);
        }

        NativeArray<VanillaImprovedNoiseOctave> nativeOctaves =
            new(managedOctaves.Length, allocator, NativeArrayOptions.UninitializedMemory);
        for (int i = 0; i < managedOctaves.Length; i++)
        {
            nativeOctaves[i] = managedOctaves[i];
        }

        return nativeOctaves;
    }

    public static NativeArray<int> CreateNativePermutations(int[] managedPermutations, Allocator allocator)
    {
        if (managedPermutations == null || managedPermutations.Length == 0)
        {
            return new NativeArray<int>(0, allocator, NativeArrayOptions.UninitializedMemory);
        }

        NativeArray<int> nativePermutations = new(managedPermutations.Length, allocator, NativeArrayOptions.UninitializedMemory);
        for (int i = 0; i < managedPermutations.Length; i++)
        {
            nativePermutations[i] = managedPermutations[i];
        }

        return nativePermutations;
    }

    public static float SampleOverworldBlendedNoise(
        float x,
        float y,
        float z,
        VanillaBlendedNoiseManaged data)
    {
        return SampleBlendedNoise(
            x,
            y,
            z,
            data.minLimitOctaves,
            data.minLimitPermutations,
            data.maxLimitOctaves,
            data.maxLimitPermutations,
            data.mainOctaves,
            data.mainPermutations);
    }

    public static float SampleOverworldBlendedNoise(
        float x,
        float y,
        float z,
        NativeArray<VanillaImprovedNoiseOctave> minLimitOctaves,
        NativeArray<int> minLimitPermutations,
        NativeArray<VanillaImprovedNoiseOctave> maxLimitOctaves,
        NativeArray<int> maxLimitPermutations,
        NativeArray<VanillaImprovedNoiseOctave> mainOctaves,
        NativeArray<int> mainPermutations)
    {
        return SampleBlendedNoise(
            x,
            y,
            z,
            minLimitOctaves,
            minLimitPermutations,
            maxLimitOctaves,
            maxLimitPermutations,
            mainOctaves,
            mainPermutations);
    }

    public static float SampleJaggedNoise(float x, float z, VanillaNormalNoiseManaged data)
    {
        return SampleNormalNoise(x * 1500f, 0f, z * 1500f, data);
    }

    public static float SampleJaggedNoise(
        float x,
        float z,
        NativeArray<VanillaImprovedNoiseOctave> firstOctaves,
        NativeArray<int> firstPermutations,
        NativeArray<VanillaImprovedNoiseOctave> secondOctaves,
        NativeArray<int> secondPermutations,
        float valueFactor)
    {
        return SampleNormalNoise(x * 1500f, 0f, z * 1500f, firstOctaves, firstPermutations, secondOctaves, secondPermutations, valueFactor);
    }

    public static float SampleManagedNormalNoise(float x, float y, float z, VanillaNormalNoiseManaged data)
    {
        return SampleNormalNoise(x, y, z, data);
    }

    public static float SampleNativeNormalNoise(
        float x,
        float y,
        float z,
        NativeArray<VanillaImprovedNoiseOctave> firstOctaves,
        NativeArray<int> firstPermutations,
        NativeArray<VanillaImprovedNoiseOctave> secondOctaves,
        NativeArray<int> secondPermutations,
        float valueFactor)
    {
        return SampleNormalNoise(x, y, z, firstOctaves, firstPermutations, secondOctaves, secondPermutations, valueFactor);
    }

    public static float SampleOverworldContinentalness(int worldX, int worldZ, VanillaClimateNoiseManaged climate)
    {
        return SampleClimateTarget(worldX, worldZ, climate.offset, climate.continentalness);
    }

    public static float SampleOverworldErosion(int worldX, int worldZ, VanillaClimateNoiseManaged climate)
    {
        return SampleClimateTarget(worldX, worldZ, climate.offset, climate.erosion);
    }

    public static float SampleOverworldWeirdness(int worldX, int worldZ, VanillaClimateNoiseManaged climate)
    {
        return SampleClimateTarget(worldX, worldZ, climate.offset, climate.ridge);
    }

    public static float SampleOverworldContinentalness(
        int worldX,
        int worldZ,
        VanillaNormalNoiseManaged offsetNoise,
        VanillaNormalNoiseManaged continentalnessNoise)
    {
        return SampleClimateTarget(worldX, worldZ, offsetNoise, continentalnessNoise);
    }

    public static float SampleOverworldErosion(
        int worldX,
        int worldZ,
        VanillaNormalNoiseManaged offsetNoise,
        VanillaNormalNoiseManaged erosionNoise)
    {
        return SampleClimateTarget(worldX, worldZ, offsetNoise, erosionNoise);
    }

    public static float SampleOverworldWeirdness(
        int worldX,
        int worldZ,
        VanillaNormalNoiseManaged offsetNoise,
        VanillaNormalNoiseManaged ridgeNoise)
    {
        return SampleClimateTarget(worldX, worldZ, offsetNoise, ridgeNoise);
    }

    public static float SampleOverworldContinentalness(
        int worldX,
        int worldZ,
        NativeArray<VanillaImprovedNoiseOctave> offsetFirstOctaves,
        NativeArray<int> offsetFirstPermutations,
        NativeArray<VanillaImprovedNoiseOctave> offsetSecondOctaves,
        NativeArray<int> offsetSecondPermutations,
        float offsetValueFactor,
        NativeArray<VanillaImprovedNoiseOctave> targetFirstOctaves,
        NativeArray<int> targetFirstPermutations,
        NativeArray<VanillaImprovedNoiseOctave> targetSecondOctaves,
        NativeArray<int> targetSecondPermutations,
        float targetValueFactor)
    {
        return SampleClimateTarget(
            worldX,
            worldZ,
            offsetFirstOctaves,
            offsetFirstPermutations,
            offsetSecondOctaves,
            offsetSecondPermutations,
            offsetValueFactor,
            targetFirstOctaves,
            targetFirstPermutations,
            targetSecondOctaves,
            targetSecondPermutations,
            targetValueFactor);
    }

    public static float SampleOverworldErosion(
        int worldX,
        int worldZ,
        NativeArray<VanillaImprovedNoiseOctave> offsetFirstOctaves,
        NativeArray<int> offsetFirstPermutations,
        NativeArray<VanillaImprovedNoiseOctave> offsetSecondOctaves,
        NativeArray<int> offsetSecondPermutations,
        float offsetValueFactor,
        NativeArray<VanillaImprovedNoiseOctave> targetFirstOctaves,
        NativeArray<int> targetFirstPermutations,
        NativeArray<VanillaImprovedNoiseOctave> targetSecondOctaves,
        NativeArray<int> targetSecondPermutations,
        float targetValueFactor)
    {
        return SampleClimateTarget(
            worldX,
            worldZ,
            offsetFirstOctaves,
            offsetFirstPermutations,
            offsetSecondOctaves,
            offsetSecondPermutations,
            offsetValueFactor,
            targetFirstOctaves,
            targetFirstPermutations,
            targetSecondOctaves,
            targetSecondPermutations,
            targetValueFactor);
    }

    public static float SampleOverworldWeirdness(
        int worldX,
        int worldZ,
        NativeArray<VanillaImprovedNoiseOctave> offsetFirstOctaves,
        NativeArray<int> offsetFirstPermutations,
        NativeArray<VanillaImprovedNoiseOctave> offsetSecondOctaves,
        NativeArray<int> offsetSecondPermutations,
        float offsetValueFactor,
        NativeArray<VanillaImprovedNoiseOctave> targetFirstOctaves,
        NativeArray<int> targetFirstPermutations,
        NativeArray<VanillaImprovedNoiseOctave> targetSecondOctaves,
        NativeArray<int> targetSecondPermutations,
        float targetValueFactor)
    {
        return SampleClimateTarget(
            worldX,
            worldZ,
            offsetFirstOctaves,
            offsetFirstPermutations,
            offsetSecondOctaves,
            offsetSecondPermutations,
            offsetValueFactor,
            targetFirstOctaves,
            targetFirstPermutations,
            targetSecondOctaves,
            targetSecondPermutations,
            targetValueFactor);
    }

    private static float SampleBlendedNoise(
        float x,
        float y,
        float z,
        VanillaImprovedNoiseOctave[] minLimitOctaves,
        int[] minLimitPermutations,
        VanillaImprovedNoiseOctave[] maxLimitOctaves,
        int[] maxLimitPermutations,
        VanillaImprovedNoiseOctave[] mainOctaves,
        int[] mainPermutations)
    {
        float xzMultiplier = 684.412f * BlendedNoiseXzScale;
        float yMultiplier = 684.412f * BlendedNoiseYScale;
        float scaledX = x * xzMultiplier;
        float scaledY = y * yMultiplier;
        float scaledZ = z * xzMultiplier;
        float factoredX = scaledX / BlendedNoiseXzFactor;
        float factoredY = scaledY / BlendedNoiseYFactor;
        float factoredZ = scaledZ / BlendedNoiseXzFactor;
        float smear = yMultiplier * BlendedNoiseSmearScaleMultiplier;
        float factoredSmear = smear / BlendedNoiseYFactor;

        float value = 0f;
        float factor = 1f;
        for (int i = 0; i < 8; i++)
        {
            int octaveIndex = mainOctaves.Length - 1 - i;
            if (octaveIndex >= 0 && octaveIndex < mainOctaves.Length && mainOctaves[octaveIndex].exists != 0)
            {
                value += SampleImprovedNoise(
                    mainOctaves[octaveIndex],
                    mainPermutations,
                    Wrap(factoredX * factor),
                    Wrap(factoredY * factor),
                    Wrap(factoredZ * factor),
                    factoredSmear * factor,
                    factoredY * factor) / factor;
            }

            factor *= 0.5f;
        }

        value = ((value / 10f) + 1f) * 0.5f;
        factor = 1f;
        float min = 0f;
        float max = 0f;
        for (int i = 0; i < 16; i++)
        {
            float xx = Wrap(scaledX * factor);
            float yy = Wrap(scaledY * factor);
            float zz = Wrap(scaledZ * factor);
            float smearScale = smear * factor;

            int octaveIndex = minLimitOctaves.Length - 1 - i;
            if (value < 1f && octaveIndex >= 0 && octaveIndex < minLimitOctaves.Length && minLimitOctaves[octaveIndex].exists != 0)
            {
                min += SampleImprovedNoise(
                    minLimitOctaves[octaveIndex],
                    minLimitPermutations,
                    xx,
                    yy,
                    zz,
                    smearScale,
                    scaledY * factor) / factor;
            }

            octaveIndex = maxLimitOctaves.Length - 1 - i;
            if (value > 0f && octaveIndex >= 0 && octaveIndex < maxLimitOctaves.Length && maxLimitOctaves[octaveIndex].exists != 0)
            {
                max += SampleImprovedNoise(
                    maxLimitOctaves[octaveIndex],
                    maxLimitPermutations,
                    xx,
                    yy,
                    zz,
                    smearScale,
                    scaledY * factor) / factor;
            }

            factor *= 0.5f;
        }

        return math.lerp(min / 512f, max / 512f, math.saturate(value)) / 128f;
    }

    private static float SampleBlendedNoise(
        float x,
        float y,
        float z,
        NativeArray<VanillaImprovedNoiseOctave> minLimitOctaves,
        NativeArray<int> minLimitPermutations,
        NativeArray<VanillaImprovedNoiseOctave> maxLimitOctaves,
        NativeArray<int> maxLimitPermutations,
        NativeArray<VanillaImprovedNoiseOctave> mainOctaves,
        NativeArray<int> mainPermutations)
    {
        float xzMultiplier = 684.412f * BlendedNoiseXzScale;
        float yMultiplier = 684.412f * BlendedNoiseYScale;
        float scaledX = x * xzMultiplier;
        float scaledY = y * yMultiplier;
        float scaledZ = z * xzMultiplier;
        float factoredX = scaledX / BlendedNoiseXzFactor;
        float factoredY = scaledY / BlendedNoiseYFactor;
        float factoredZ = scaledZ / BlendedNoiseXzFactor;
        float smear = yMultiplier * BlendedNoiseSmearScaleMultiplier;
        float factoredSmear = smear / BlendedNoiseYFactor;

        float value = 0f;
        float factor = 1f;
        for (int i = 0; i < 8; i++)
        {
            int octaveIndex = mainOctaves.Length - 1 - i;
            if (octaveIndex >= 0 && octaveIndex < mainOctaves.Length && mainOctaves[octaveIndex].exists != 0)
            {
                value += SampleImprovedNoise(
                    mainOctaves[octaveIndex],
                    mainPermutations,
                    Wrap(factoredX * factor),
                    Wrap(factoredY * factor),
                    Wrap(factoredZ * factor),
                    factoredSmear * factor,
                    factoredY * factor) / factor;
            }

            factor *= 0.5f;
        }

        value = ((value / 10f) + 1f) * 0.5f;
        factor = 1f;
        float min = 0f;
        float max = 0f;
        for (int i = 0; i < 16; i++)
        {
            float xx = Wrap(scaledX * factor);
            float yy = Wrap(scaledY * factor);
            float zz = Wrap(scaledZ * factor);
            float smearScale = smear * factor;

            int octaveIndex = minLimitOctaves.Length - 1 - i;
            if (value < 1f && octaveIndex >= 0 && octaveIndex < minLimitOctaves.Length && minLimitOctaves[octaveIndex].exists != 0)
            {
                min += SampleImprovedNoise(
                    minLimitOctaves[octaveIndex],
                    minLimitPermutations,
                    xx,
                    yy,
                    zz,
                    smearScale,
                    scaledY * factor) / factor;
            }

            octaveIndex = maxLimitOctaves.Length - 1 - i;
            if (value > 0f && octaveIndex >= 0 && octaveIndex < maxLimitOctaves.Length && maxLimitOctaves[octaveIndex].exists != 0)
            {
                max += SampleImprovedNoise(
                    maxLimitOctaves[octaveIndex],
                    maxLimitPermutations,
                    xx,
                    yy,
                    zz,
                    smearScale,
                    scaledY * factor) / factor;
            }

            factor *= 0.5f;
        }

        return math.lerp(min / 512f, max / 512f, math.saturate(value)) / 128f;
    }

    private static float SampleNormalNoise(float x, float y, float z, VanillaNormalNoiseManaged data)
    {
        float x2 = x * NormalNoiseInputFactor;
        float y2 = y * NormalNoiseInputFactor;
        float z2 = z * NormalNoiseInputFactor;
        float first = SamplePerlinNoise(data.firstOctaves, data.firstPermutations, -16, x, y, z);
        float second = SamplePerlinNoise(data.secondOctaves, data.secondPermutations, -16, x2, y2, z2);
        return (first + second) * data.valueFactor;
    }

    private static float SampleNormalNoise(
        float x,
        float y,
        float z,
        NativeArray<VanillaImprovedNoiseOctave> firstOctaves,
        NativeArray<int> firstPermutations,
        NativeArray<VanillaImprovedNoiseOctave> secondOctaves,
        NativeArray<int> secondPermutations,
        float valueFactor)
    {
        float x2 = x * NormalNoiseInputFactor;
        float y2 = y * NormalNoiseInputFactor;
        float z2 = z * NormalNoiseInputFactor;
        float first = SamplePerlinNoise(firstOctaves, firstPermutations, -16, x, y, z);
        float second = SamplePerlinNoise(secondOctaves, secondPermutations, -16, x2, y2, z2);
        return (first + second) * valueFactor;
    }

    private static float SamplePerlinNoise(
        VanillaImprovedNoiseOctave[] octaves,
        int[] permutations,
        int firstOctave,
        float x,
        float y,
        float z)
    {
        float lowestFreqInputFactor = math.pow(2f, firstOctave);
        float lowestFreqValueFactor = math.pow(2f, octaves.Length - 1) / (math.pow(2f, octaves.Length) - 1f);
        float value = 0f;
        float inputFactor = lowestFreqInputFactor;
        float valueFactor = lowestFreqValueFactor;
        for (int i = 0; i < octaves.Length; i++)
        {
            if (octaves[i].exists != 0)
            {
                value += valueFactor * SampleImprovedNoise(
                    octaves[i],
                    permutations,
                    Wrap(x * inputFactor),
                    Wrap(y * inputFactor),
                    Wrap(z * inputFactor),
                    0f,
                    0f);
            }

            inputFactor *= 2f;
            valueFactor *= 0.5f;
        }

        return value;
    }

    private static float SamplePerlinNoise(
        NativeArray<VanillaImprovedNoiseOctave> octaves,
        NativeArray<int> permutations,
        int firstOctave,
        float x,
        float y,
        float z)
    {
        float lowestFreqInputFactor = math.pow(2f, firstOctave);
        float lowestFreqValueFactor = math.pow(2f, octaves.Length - 1) / (math.pow(2f, octaves.Length) - 1f);
        float value = 0f;
        float inputFactor = lowestFreqInputFactor;
        float valueFactor = lowestFreqValueFactor;
        for (int i = 0; i < octaves.Length; i++)
        {
            if (octaves[i].exists != 0)
            {
                value += valueFactor * SampleImprovedNoise(
                    octaves[i],
                    permutations,
                    Wrap(x * inputFactor),
                    Wrap(y * inputFactor),
                    Wrap(z * inputFactor),
                    0f,
                    0f);
            }

            inputFactor *= 2f;
            valueFactor *= 0.5f;
        }

        return value;
    }

    private static float SampleImprovedNoise(
        VanillaImprovedNoiseOctave octave,
        int[] permutations,
        float x,
        float y,
        float z,
        float yScale,
        float yLimit)
    {
        float x2 = x + octave.xo;
        float y2 = y + octave.yo;
        float z2 = z + octave.zo;
        int xFloor = FastFloor(x2);
        int yFloor = FastFloor(y2);
        int zFloor = FastFloor(z2);
        float xFrac = x2 - xFloor;
        float yFrac = y2 - yFloor;
        float zFrac = z2 - zFloor;
        float yOffset = 0f;
        if (yScale != 0f)
        {
            float t = yLimit >= 0f && yLimit < yFrac ? yLimit : yFrac;
            yOffset = FastFloor((t / yScale) + 1e-7f) * yScale;
        }

        return SampleAndLerp(octave, permutations, xFloor, yFloor, zFloor, xFrac, yFrac - yOffset, zFrac, yFrac);
    }

    private static float SampleImprovedNoise(
        VanillaImprovedNoiseOctave octave,
        NativeArray<int> permutations,
        float x,
        float y,
        float z,
        float yScale,
        float yLimit)
    {
        float x2 = x + octave.xo;
        float y2 = y + octave.yo;
        float z2 = z + octave.zo;
        int xFloor = FastFloor(x2);
        int yFloor = FastFloor(y2);
        int zFloor = FastFloor(z2);
        float xFrac = x2 - xFloor;
        float yFrac = y2 - yFloor;
        float zFrac = z2 - zFloor;
        float yOffset = 0f;
        if (yScale != 0f)
        {
            float t = yLimit >= 0f && yLimit < yFrac ? yLimit : yFrac;
            yOffset = FastFloor((t / yScale) + 1e-7f) * yScale;
        }

        return SampleAndLerp(octave, permutations, xFloor, yFloor, zFloor, xFrac, yFrac - yOffset, zFrac, yFrac);
    }

    private static float SampleAndLerp(
        VanillaImprovedNoiseOctave octave,
        int[] permutations,
        int x,
        int y,
        int z,
        float xf,
        float yf,
        float zf,
        float originalY)
    {
        int h = Permutation(octave, permutations, x);
        int i = Permutation(octave, permutations, x + 1);
        int j = Permutation(octave, permutations, h + y);
        int k = Permutation(octave, permutations, h + y + 1);
        int l = Permutation(octave, permutations, i + y);
        int m = Permutation(octave, permutations, i + y + 1);
        float n = GradDot(Permutation(octave, permutations, j + z), xf, yf, zf);
        float o = GradDot(Permutation(octave, permutations, l + z), xf - 1f, yf, zf);
        float p = GradDot(Permutation(octave, permutations, k + z), xf, yf - 1f, zf);
        float q = GradDot(Permutation(octave, permutations, m + z), xf - 1f, yf - 1f, zf);
        float r = GradDot(Permutation(octave, permutations, j + z + 1), xf, yf, zf - 1f);
        float s = GradDot(Permutation(octave, permutations, l + z + 1), xf - 1f, yf, zf - 1f);
        float t = GradDot(Permutation(octave, permutations, k + z + 1), xf, yf - 1f, zf - 1f);
        float u = GradDot(Permutation(octave, permutations, m + z + 1), xf - 1f, yf - 1f, zf - 1f);
        float vx = SmoothStep(xf);
        float vy = SmoothStep(originalY);
        float vz = SmoothStep(zf);
        return Lerp3(vx, vy, vz, n, o, p, q, r, s, t, u);
    }

    private static float SampleAndLerp(
        VanillaImprovedNoiseOctave octave,
        NativeArray<int> permutations,
        int x,
        int y,
        int z,
        float xf,
        float yf,
        float zf,
        float originalY)
    {
        int h = Permutation(octave, permutations, x);
        int i = Permutation(octave, permutations, x + 1);
        int j = Permutation(octave, permutations, h + y);
        int k = Permutation(octave, permutations, h + y + 1);
        int l = Permutation(octave, permutations, i + y);
        int m = Permutation(octave, permutations, i + y + 1);
        float n = GradDot(Permutation(octave, permutations, j + z), xf, yf, zf);
        float o = GradDot(Permutation(octave, permutations, l + z), xf - 1f, yf, zf);
        float p = GradDot(Permutation(octave, permutations, k + z), xf, yf - 1f, zf);
        float q = GradDot(Permutation(octave, permutations, m + z), xf - 1f, yf - 1f, zf);
        float r = GradDot(Permutation(octave, permutations, j + z + 1), xf, yf, zf - 1f);
        float s = GradDot(Permutation(octave, permutations, l + z + 1), xf - 1f, yf, zf - 1f);
        float t = GradDot(Permutation(octave, permutations, k + z + 1), xf, yf - 1f, zf - 1f);
        float u = GradDot(Permutation(octave, permutations, m + z + 1), xf - 1f, yf - 1f, zf - 1f);
        float vx = SmoothStep(xf);
        float vy = SmoothStep(originalY);
        float vz = SmoothStep(zf);
        return Lerp3(vx, vy, vz, n, o, p, q, r, s, t, u);
    }

    private static int Permutation(VanillaImprovedNoiseOctave octave, int[] permutations, int index)
    {
        return permutations[octave.permutationStart + (index & 255)];
    }

    private static int Permutation(VanillaImprovedNoiseOctave octave, NativeArray<int> permutations, int index)
    {
        return permutations[octave.permutationStart + (index & 255)];
    }

    private static float GradDot(int gradientIndex, float x, float y, float z)
    {
        int baseIndex = (gradientIndex & 15) * 3;
        return (Gradient[baseIndex] * x) + (Gradient[baseIndex + 1] * y) + (Gradient[baseIndex + 2] * z);
    }

    private static float Wrap(float value)
    {
        return value - (FastFloor((value / PerlinWrap) + 0.5f) * PerlinWrap);
    }

    private static float SmoothStep(float value)
    {
        return value * value * value * (value * ((value * 6f) - 15f) + 10f);
    }

    private static float Lerp(float alpha, float a, float b)
    {
        return a + (alpha * (b - a));
    }

    private static float Lerp2(float x, float y, float v00, float v10, float v01, float v11)
    {
        return Lerp(y, Lerp(x, v00, v10), Lerp(x, v01, v11));
    }

    private static float Lerp3(float x, float y, float z, float v000, float v100, float v010, float v110, float v001, float v101, float v011, float v111)
    {
        return Lerp(z, Lerp2(x, y, v000, v100, v010, v110), Lerp2(x, y, v001, v101, v011, v111));
    }

    private static int FastFloor(float value)
    {
        return (int)math.floor(value);
    }

    private static float SampleClimateTarget(int worldX, int worldZ, VanillaNormalNoiseManaged offsetNoise, VanillaNormalNoiseManaged targetNoise)
    {
        float shiftX = SampleClimateShiftX(worldX, worldZ, offsetNoise);
        float shiftZ = SampleClimateShiftZ(worldX, worldZ, offsetNoise);
        return SampleNormalNoise((worldX * 0.25f) + shiftX, 0f, (worldZ * 0.25f) + shiftZ, targetNoise);
    }

    private static float SampleClimateTarget(
        int worldX,
        int worldZ,
        NativeArray<VanillaImprovedNoiseOctave> offsetFirstOctaves,
        NativeArray<int> offsetFirstPermutations,
        NativeArray<VanillaImprovedNoiseOctave> offsetSecondOctaves,
        NativeArray<int> offsetSecondPermutations,
        float offsetValueFactor,
        NativeArray<VanillaImprovedNoiseOctave> targetFirstOctaves,
        NativeArray<int> targetFirstPermutations,
        NativeArray<VanillaImprovedNoiseOctave> targetSecondOctaves,
        NativeArray<int> targetSecondPermutations,
        float targetValueFactor)
    {
        float shiftX = SampleClimateShiftX(
            worldX,
            worldZ,
            offsetFirstOctaves,
            offsetFirstPermutations,
            offsetSecondOctaves,
            offsetSecondPermutations,
            offsetValueFactor);
        float shiftZ = SampleClimateShiftZ(
            worldX,
            worldZ,
            offsetFirstOctaves,
            offsetFirstPermutations,
            offsetSecondOctaves,
            offsetSecondPermutations,
            offsetValueFactor);
        return SampleNormalNoise(
            (worldX * 0.25f) + shiftX,
            0f,
            (worldZ * 0.25f) + shiftZ,
            targetFirstOctaves,
            targetFirstPermutations,
            targetSecondOctaves,
            targetSecondPermutations,
            targetValueFactor);
    }

    private static float SampleClimateShiftX(int worldX, int worldZ, VanillaNormalNoiseManaged offsetNoise)
    {
        return SampleNormalNoise(worldX * 0.25f, 0f, worldZ * 0.25f, offsetNoise) * 4f;
    }

    private static float SampleClimateShiftZ(int worldX, int worldZ, VanillaNormalNoiseManaged offsetNoise)
    {
        return SampleNormalNoise(worldZ * 0.25f, worldX * 0.25f, 0f, offsetNoise) * 4f;
    }

    private static float SampleClimateShiftX(
        int worldX,
        int worldZ,
        NativeArray<VanillaImprovedNoiseOctave> firstOctaves,
        NativeArray<int> firstPermutations,
        NativeArray<VanillaImprovedNoiseOctave> secondOctaves,
        NativeArray<int> secondPermutations,
        float valueFactor)
    {
        return SampleNormalNoise(worldX * 0.25f, 0f, worldZ * 0.25f, firstOctaves, firstPermutations, secondOctaves, secondPermutations, valueFactor) * 4f;
    }

    private static float SampleClimateShiftZ(
        int worldX,
        int worldZ,
        NativeArray<VanillaImprovedNoiseOctave> firstOctaves,
        NativeArray<int> firstPermutations,
        NativeArray<VanillaImprovedNoiseOctave> secondOctaves,
        NativeArray<int> secondPermutations,
        float valueFactor)
    {
        return SampleNormalNoise(worldZ * 0.25f, worldX * 0.25f, 0f, firstOctaves, firstPermutations, secondOctaves, secondPermutations, valueFactor) * 4f;
    }

    private static float CalculateNormalNoiseValueFactor(int octaveCount)
    {
        const float expectedDeviationBase = 0.1f;
        float expectedDeviation = expectedDeviationBase * (1f + (1f / octaveCount));
        return (1f / 6f) / expectedDeviation;
    }

    private static float CalculateNormalNoiseValueFactor(float[] amplitudes)
    {
        int min = int.MaxValue;
        int max = int.MinValue;
        for (int i = 0; i < amplitudes.Length; i++)
        {
            if (math.abs(amplitudes[i]) > 0f)
            {
                min = math.min(min, i);
                max = math.max(max, i);
            }
        }

        if (min == int.MaxValue || max == int.MinValue)
        {
            return 0f;
        }

        float expectedDeviation = 0.1f * (1f + (1f / ((max - min) + 1f)));
        return (1f / 6f) / expectedDeviation;
    }

    private static void BuildLegacyStylePerlin<TRandom>(
        ref TRandom random,
        int firstOctave,
        int octaveCount,
        out VanillaImprovedNoiseOctave[] octaves,
        out int[] permutations)
        where TRandom : struct, INoiseRandom
    {
        octaves = new VanillaImprovedNoiseOctave[octaveCount];
        permutations = new int[octaveCount * PermutationSize];
        for (int i = -firstOctave; i >= 0; i--)
        {
            if (i < octaveCount)
            {
                octaves[i] = CreateImprovedNoise(ref random, permutations, i * PermutationSize);
            }
            else
            {
                random.Consume(262);
            }
        }
    }

    private static void BuildXoroshiroPerlin(
        ref XoroshiroRandom128 random,
        int firstOctave,
        float[] amplitudes,
        out VanillaImprovedNoiseOctave[] octaves,
        out int[] permutations)
    {
        octaves = new VanillaImprovedNoiseOctave[amplitudes.Length];
        permutations = new int[amplitudes.Length * PermutationSize];
        XoroshiroPositionalRandom positional = random.ForkPositional();
        for (int i = 0; i < amplitudes.Length; i++)
        {
            if (math.abs(amplitudes[i]) <= 0f)
            {
                continue;
            }

            int octave = firstOctave + i;
            XoroshiroRandom128 octaveRandom = positional.FromHashOf("octave_" + octave);
            octaves[i] = CreateImprovedNoise(ref octaveRandom, permutations, i * PermutationSize);
        }
    }

    private static VanillaImprovedNoiseOctave CreateImprovedNoise<TRandom>(ref TRandom random, int[] permutations, int permutationStart)
        where TRandom : struct, INoiseRandom
    {
        VanillaImprovedNoiseOctave octave = new()
        {
            permutationStart = permutationStart,
            xo = (float)(random.NextDouble() * 256.0),
            yo = (float)(random.NextDouble() * 256.0),
            zo = (float)(random.NextDouble() * 256.0),
            exists = 1,
        };

        for (int i = 0; i < PermutationSize; i++)
        {
            permutations[permutationStart + i] = i;
        }

        for (int i = 0; i < PermutationSize; i++)
        {
            int j = random.NextInt(PermutationSize - i);
            int indexA = permutationStart + i;
            int indexB = permutationStart + i + j;
            (permutations[indexA], permutations[indexB]) = (permutations[indexB], permutations[indexA]);
        }

        return octave;
    }

    private static long HashNameSeed(string name)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(name);
        byte[] hash;
        using (MD5 md5 = MD5.Create())
        {
            hash = md5.ComputeHash(bytes);
        }
        ulong value =
            ((ulong)hash[0] << 56) |
            ((ulong)hash[1] << 48) |
            ((ulong)hash[2] << 40) |
            ((ulong)hash[3] << 32) |
            ((ulong)hash[4] << 24) |
            ((ulong)hash[5] << 16) |
            ((ulong)hash[6] << 8) |
            hash[7];
        return unchecked((long)value);
    }

    internal static void HashNameSeed128(string name, out ulong seedLo, out ulong seedHi)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(name);
        byte[] hash;
        using (MD5 md5 = MD5.Create())
        {
            hash = md5.ComputeHash(bytes);
        }

        seedLo =
            ((ulong)hash[0] << 56) |
            ((ulong)hash[1] << 48) |
            ((ulong)hash[2] << 40) |
            ((ulong)hash[3] << 32) |
            ((ulong)hash[4] << 24) |
            ((ulong)hash[5] << 16) |
            ((ulong)hash[6] << 8) |
            hash[7];
        seedHi =
            ((ulong)hash[8] << 56) |
            ((ulong)hash[9] << 48) |
            ((ulong)hash[10] << 40) |
            ((ulong)hash[11] << 32) |
            ((ulong)hash[12] << 24) |
            ((ulong)hash[13] << 16) |
            ((ulong)hash[14] << 8) |
            hash[15];
    }

    private static float[] CreateFilledAmplitudes(int count, float value)
    {
        float[] amplitudes = new float[count];
        for (int i = 0; i < count; i++)
        {
            amplitudes[i] = value;
        }

        return amplitudes;
    }
}

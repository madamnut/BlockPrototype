using UnityEngine;

public readonly struct TerraSurfaceSample
{
    public readonly int surfaceHeight;
    public readonly float continentalness;
    public readonly float erosion;
    public readonly float weirdness;
    public readonly float peaksAndValleys;
    public readonly float offset;
    public readonly float factor;
    public readonly float jaggedness;

    public TerraSurfaceSample(
        int surfaceHeight,
        float continentalness,
        float erosion,
        float weirdness,
        float peaksAndValleys,
        float offset,
        float factor,
        float jaggedness)
    {
        this.surfaceHeight = surfaceHeight;
        this.continentalness = continentalness;
        this.erosion = erosion;
        this.weirdness = weirdness;
        this.peaksAndValleys = peaksAndValleys;
        this.offset = offset;
        this.factor = factor;
        this.jaggedness = jaggedness;
    }
}

public sealed class TerraHeightSampler
{
    private readonly int _seed;
    private readonly int _seaLevel;
    private readonly TerraNoiseSettings _settings;

    public TerraHeightSampler(int seed, int seaLevel, TerraNoiseSettings settings)
    {
        _seed = seed;
        _seaLevel = Mathf.Clamp(seaLevel, 0, TerrainData.WorldHeight - 1);
        _settings = settings;
    }

    public TerraSurfaceSample Sample(int worldX, int worldZ)
    {
        float continentalness = SampleSignedNoise(
            worldX,
            worldZ,
            _settings.continentalnessScale,
            _settings.continentalnessSeedOffset);

        float erosion = SampleSignedNoise(
            worldX,
            worldZ,
            _settings.erosionScale,
            _settings.erosionSeedOffset);

        float weirdness = SampleSignedNoise(
            worldX,
            worldZ,
            _settings.weirdnessScale,
            _settings.weirdnessSeedOffset);

        float peaksAndValleys = CalculatePeaksAndValleys(weirdness);
        float offset = ComposeOffset(continentalness, erosion);
        float factor = ComposeFactor(continentalness, erosion, peaksAndValleys);
        float jaggedness = ComposeJaggedness(weirdness, peaksAndValleys);
        int surfaceHeight = ComposeSurfaceHeight(offset, factor, jaggedness, weirdness, peaksAndValleys);
        return new TerraSurfaceSample(
            surfaceHeight,
            continentalness,
            erosion,
            weirdness,
            peaksAndValleys,
            offset,
            factor,
            jaggedness);
    }

    public float ComposeOffset(float continentalness, float erosion)
    {
        float offset = _settings.offsetBase;
        offset += continentalness * _settings.continentalnessOffset;
        offset -= erosion * _settings.erosionOffset;
        return offset;
    }

    public float ComposeFactor(float continentalness, float erosion, float peaksAndValleys)
    {
        float factor = _settings.factorBase;
        factor += continentalness * _settings.continentalnessFactor;
        factor -= erosion * _settings.erosionFactor;
        factor += peaksAndValleys * _settings.peaksAndValleysFactor;
        return Mathf.Max(0.1f, factor);
    }

    public float ComposeJaggedness(float weirdness, float peaksAndValleys)
    {
        float jaggedness = weirdness * _settings.weirdnessJaggedness;
        jaggedness += peaksAndValleys * _settings.peaksAndValleysJaggedness;
        return jaggedness;
    }

    public int ComposeSurfaceHeight(float offset, float factor, float jaggedness, float weirdness, float peaksAndValleys)
    {
        float surfaceHeight = _seaLevel + offset;
        surfaceHeight += factor * peaksAndValleys;
        surfaceHeight += jaggedness * weirdness;
        return Mathf.Clamp(Mathf.RoundToInt(surfaceHeight), 1, TerrainData.WorldHeight - 8);
    }

    private float SampleSignedNoise(int worldX, int worldZ, float scale, float seedOffset)
    {
        float x = worldX + (_seed * 0.137f) + seedOffset;
        float z = worldZ - (_seed * 0.091f) + (seedOffset * 1.7f);
        float sample = Mathf.PerlinNoise((x * scale) + 10000f, (z * scale) + 10000f);
        return (sample * 2f) - 1f;
    }

    private static float CalculatePeaksAndValleys(float weirdness)
    {
        float folded = 1f - Mathf.Abs(weirdness);
        return Mathf.Clamp((folded * 2f) - 1f, -1f, 1f);
    }
}

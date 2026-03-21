using System;

public sealed class VanillaContinentalnessNoise
{
    private VanillaNormalNoise _offsetNoise;
    private VanillaNormalNoise _targetNoise;
    private double _shiftInputScale;
    private double _targetXZScale;
    private double _shiftStrength;

    public VanillaContinentalnessNoise(int seed, ContinentalnessSettingsAsset settings)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        if (settings.OffsetNoise == null)
        {
            throw new InvalidOperationException("Continentalness settings are missing offset noise parameters.");
        }

        if (settings.ContinentalnessNoise == null)
        {
            throw new InvalidOperationException("Continentalness settings are missing continentalness noise parameters.");
        }

        Initialize(
            seed,
            "minecraft:offset",
            "minecraft:continentalness",
            settings.ShiftInputScale,
            settings.ContinentsXZScale,
            settings.ShiftStrength,
            settings.OffsetNoise,
            settings.ContinentalnessNoise);
    }

    public VanillaContinentalnessNoise(
        int seed,
        string shiftNoiseHashName,
        string targetNoiseHashName,
        float shiftInputScale,
        float targetXZScale,
        float shiftStrength,
        VanillaNoiseSettingsData offsetNoise,
        VanillaNoiseSettingsData targetNoise)
    {
        Initialize(seed, shiftNoiseHashName, targetNoiseHashName, shiftInputScale, targetXZScale, shiftStrength, offsetNoise, targetNoise);
    }

    private void Initialize(
        int seed,
        string shiftNoiseHashName,
        string targetNoiseHashName,
        float shiftInputScale,
        float targetXZScale,
        float shiftStrength,
        VanillaNoiseSettingsData offsetNoise,
        VanillaNoiseSettingsData targetNoise)
    {
        if (offsetNoise == null)
        {
            throw new InvalidOperationException("Shifted noise requires offset noise parameters.");
        }

        if (targetNoise == null)
        {
            throw new InvalidOperationException("Shifted noise requires target noise parameters.");
        }

        _shiftInputScale = shiftInputScale;
        _targetXZScale = targetXZScale;
        _shiftStrength = shiftStrength;

        VanillaNoiseParameters offsetParameters = VanillaNoiseParameters.From(offsetNoise);
        VanillaNoiseParameters targetParameters = VanillaNoiseParameters.From(targetNoise);

        VanillaXoroshiroRandom random = VanillaXoroshiroRandom.Create(seed);
        VanillaPositionalRandom positional = random.ForkPositional();
        _offsetNoise = new VanillaNormalNoise(positional.FromHashOf(shiftNoiseHashName), offsetParameters);
        _targetNoise = new VanillaNormalNoise(positional.FromHashOf(targetNoiseHashName), targetParameters);
    }

    public float Sample(int worldX, int worldZ)
    {
        double shiftX = _offsetNoise.Sample(worldX * _shiftInputScale, 0d, worldZ * _shiftInputScale) * _shiftStrength;
        double shiftZ = _offsetNoise.Sample(worldZ * _shiftInputScale, worldX * _shiftInputScale, 0d) * _shiftStrength;

        double sampleX = (worldX * _targetXZScale) + shiftX;
        double sampleZ = (worldZ * _targetXZScale) + shiftZ;
        return (float)_targetNoise.Sample(sampleX, 0d, sampleZ);
    }
}

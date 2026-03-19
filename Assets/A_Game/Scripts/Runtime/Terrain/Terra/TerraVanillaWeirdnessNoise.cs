using System;

public sealed class TerraVanillaWeirdnessNoise
{
    private readonly TerraVanillaContinentalnessNoise _noise;

    public TerraVanillaWeirdnessNoise(int seed, TerraWeirdnessSettingsAsset settings)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        if (settings.OffsetNoise == null)
        {
            throw new InvalidOperationException("Terra weirdness settings are missing offset noise parameters.");
        }

        if (settings.WeirdnessNoise == null)
        {
            throw new InvalidOperationException("Terra weirdness settings are missing weirdness noise parameters.");
        }

        _noise = new TerraVanillaContinentalnessNoise(
            seed,
            settings.ShiftNoiseHashName,
            settings.WeirdnessNoiseHashName,
            settings.Scale,
            settings.ShiftInputScale,
            settings.RidgesXZScale,
            settings.ShiftStrength,
            settings.OffsetNoise,
            settings.WeirdnessNoise);
    }

    public float Sample(int worldX, int worldZ)
    {
        return _noise.Sample(worldX, worldZ);
    }
}

using System;

public sealed class VanillaWeirdnessNoise
{
    private readonly VanillaContinentalnessNoise _noise;

    public VanillaWeirdnessNoise(int seed, WeirdnessSettingsAsset settings)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        if (settings.OffsetNoise == null)
        {
            throw new InvalidOperationException("Weirdness settings are missing offset noise parameters.");
        }

        if (settings.WeirdnessNoise == null)
        {
            throw new InvalidOperationException("Weirdness settings are missing weirdness noise parameters.");
        }

        _noise = new VanillaContinentalnessNoise(
            seed,
            settings.ShiftNoiseHashName,
            settings.WeirdnessNoiseHashName,
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

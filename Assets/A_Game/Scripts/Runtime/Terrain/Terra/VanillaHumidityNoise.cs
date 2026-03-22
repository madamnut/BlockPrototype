using System;

public sealed class VanillaHumidityNoise
{
    private readonly VanillaContinentalnessNoise _noise;

    public VanillaHumidityNoise(int seed, HumiditySettingsAsset settings)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        if (settings.OffsetNoise == null)
        {
            throw new InvalidOperationException("Humidity settings are missing offset noise parameters.");
        }

        if (settings.HumidityNoise == null)
        {
            throw new InvalidOperationException("Humidity settings are missing humidity noise parameters.");
        }

        _noise = new VanillaContinentalnessNoise(
            seed,
            settings.ShiftNoiseHashName,
            settings.HumidityNoiseHashName,
            settings.ShiftInputScale,
            settings.HumidityXZScale,
            settings.ShiftStrength,
            settings.OffsetNoise,
            settings.HumidityNoise);
    }

    public float Sample(int worldX, int worldZ)
    {
        return _noise.Sample(worldX, worldZ);
    }
}

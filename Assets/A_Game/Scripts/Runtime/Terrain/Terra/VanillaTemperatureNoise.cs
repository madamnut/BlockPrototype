using System;

public sealed class VanillaTemperatureNoise
{
    private readonly VanillaContinentalnessNoise _noise;

    public VanillaTemperatureNoise(int seed, TemperatureSettingsAsset settings)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        if (settings.OffsetNoise == null)
        {
            throw new InvalidOperationException("Temperature settings are missing offset noise parameters.");
        }

        if (settings.TemperatureNoise == null)
        {
            throw new InvalidOperationException("Temperature settings are missing temperature noise parameters.");
        }

        _noise = new VanillaContinentalnessNoise(
            seed,
            settings.ShiftNoiseHashName,
            settings.TemperatureNoiseHashName,
            settings.ShiftInputScale,
            settings.TemperatureXZScale,
            settings.ShiftStrength,
            settings.OffsetNoise,
            settings.TemperatureNoise);
    }

    public float Sample(int worldX, int worldZ)
    {
        return _noise.Sample(worldX, worldZ);
    }
}

using System;

public sealed class VanillaErosionNoise
{
    private readonly VanillaContinentalnessNoise _noise;

    public VanillaErosionNoise(int seed, ErosionSettingsAsset settings)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        if (settings.OffsetNoise == null)
        {
            throw new InvalidOperationException("Erosion settings are missing offset noise parameters.");
        }

        if (settings.ErosionNoise == null)
        {
            throw new InvalidOperationException("Erosion settings are missing erosion noise parameters.");
        }

        _noise = new VanillaContinentalnessNoise(
            seed,
            settings.ShiftNoiseHashName,
            settings.ErosionNoiseHashName,
            settings.ShiftInputScale,
            settings.ErosionXZScale,
            settings.ShiftStrength,
            settings.OffsetNoise,
            settings.ErosionNoise);
    }

    public float Sample(int worldX, int worldZ)
    {
        return _noise.Sample(worldX, worldZ);
    }
}

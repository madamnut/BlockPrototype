using UnityEngine;

public sealed class ContinentalnessSampler
{
    private readonly ContinentalnessSettingsAsset _settings;
    private readonly VanillaContinentalnessNoise _vanillaContinents;

    public ContinentalnessSampler(int seed, ContinentalnessSettingsAsset settings)
    {
        _settings = settings != null
            ? settings
            : throw new System.ArgumentNullException(nameof(settings), "ContinentalnessSampler requires a continentalness settings asset.");
        _vanillaContinents = new VanillaContinentalnessNoise(seed, _settings);
    }

    public float Sample(int worldX, int worldZ)
    {
        return _vanillaContinents.Sample(worldX, worldZ);
    }
}

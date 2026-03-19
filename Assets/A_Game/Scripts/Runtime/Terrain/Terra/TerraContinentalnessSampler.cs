using UnityEngine;

public sealed class TerraContinentalnessSampler
{
    private readonly TerraContinentalnessSettingsAsset _settings;
    private readonly TerraVanillaContinentalnessNoise _vanillaContinents;

    public TerraContinentalnessSampler(int seed, TerraContinentalnessSettingsAsset settings)
    {
        _settings = settings != null
            ? settings
            : throw new System.ArgumentNullException(nameof(settings), "TerraContinentalnessSampler requires a continentalness settings asset.");
        _vanillaContinents = new TerraVanillaContinentalnessNoise(seed, _settings);
    }

    public float Sample(int worldX, int worldZ)
    {
        return _vanillaContinents.Sample(worldX, worldZ);
    }
}

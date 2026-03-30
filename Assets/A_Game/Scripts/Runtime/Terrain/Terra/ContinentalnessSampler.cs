using UnityEngine;

public sealed class ContinentalnessSampler
{
    private readonly SimplexNoiseSampler _simplexNoise;

    public ContinentalnessSampler(int seed, SimplexNoiseSettingsAsset settings)
    {
        _simplexNoise = new SimplexNoiseSampler(
            seed,
            settings ?? throw new System.ArgumentNullException(nameof(settings), "ContinentalnessSampler requires a simplex settings asset."));
    }

    public float Sample(int worldX, int worldZ)
    {
        return _simplexNoise.Sample(worldX, worldZ);
    }
}

public sealed class WeirdnessSampler
{
    private readonly SimplexNoiseSampler _simplexNoise;

    public WeirdnessSampler(int seed, SimplexNoiseSettingsAsset settings)
    {
        _simplexNoise = new SimplexNoiseSampler(
            seed,
            settings ?? throw new System.ArgumentNullException(nameof(settings), "WeirdnessSampler requires a simplex settings asset."));
    }

    public float Sample(int worldX, int worldZ)
    {
        return _simplexNoise.Sample(worldX, worldZ);
    }
}

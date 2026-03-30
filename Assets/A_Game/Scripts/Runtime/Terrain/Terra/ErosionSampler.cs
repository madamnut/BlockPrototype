public sealed class ErosionSampler
{
    private readonly SimplexNoiseSampler _simplexNoise;

    public ErosionSampler(int seed, SimplexNoiseSettingsAsset settings)
    {
        _simplexNoise = new SimplexNoiseSampler(
            seed,
            settings ?? throw new System.ArgumentNullException(nameof(settings), "ErosionSampler requires a simplex settings asset."));
    }

    public float Sample(int worldX, int worldZ)
    {
        return _simplexNoise.Sample(worldX, worldZ);
    }
}

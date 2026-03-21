public sealed class ErosionSampler
{
    private readonly VanillaErosionNoise _noise;

    public ErosionSampler(int seed, ErosionSettingsAsset settings)
    {
        _noise = new VanillaErosionNoise(seed, settings);
    }

    public float Sample(int worldX, int worldZ)
    {
        return _noise.Sample(worldX, worldZ);
    }
}

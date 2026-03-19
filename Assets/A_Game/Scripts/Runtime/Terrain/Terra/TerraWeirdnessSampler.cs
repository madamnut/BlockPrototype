public sealed class TerraWeirdnessSampler
{
    private readonly TerraVanillaWeirdnessNoise _noise;

    public TerraWeirdnessSampler(int seed, TerraWeirdnessSettingsAsset settings)
    {
        _noise = new TerraVanillaWeirdnessNoise(seed, settings);
    }

    public float Sample(int worldX, int worldZ)
    {
        return _noise.Sample(worldX, worldZ);
    }
}

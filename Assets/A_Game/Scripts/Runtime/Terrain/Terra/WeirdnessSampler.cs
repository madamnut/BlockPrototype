public sealed class WeirdnessSampler
{
    private readonly VanillaWeirdnessNoise _noise;

    public WeirdnessSampler(int seed, WeirdnessSettingsAsset settings)
    {
        _noise = new VanillaWeirdnessNoise(seed, settings);
    }

    public float Sample(int worldX, int worldZ)
    {
        return _noise.Sample(worldX, worldZ);
    }
}

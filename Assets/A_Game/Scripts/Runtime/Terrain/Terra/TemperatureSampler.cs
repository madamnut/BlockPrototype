public sealed class TemperatureSampler
{
    private readonly VanillaTemperatureNoise _noise;

    public TemperatureSampler(int seed, TemperatureSettingsAsset settings)
    {
        _noise = new VanillaTemperatureNoise(seed, settings);
    }

    public float Sample(int worldX, int worldZ)
    {
        return _noise.Sample(worldX, worldZ);
    }
}

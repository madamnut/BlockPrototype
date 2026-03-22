public sealed class HumiditySampler
{
    private readonly VanillaHumidityNoise _noise;

    public HumiditySampler(int seed, HumiditySettingsAsset settings)
    {
        _noise = new VanillaHumidityNoise(seed, settings);
    }

    public float Sample(int worldX, int worldZ)
    {
        return _noise.Sample(worldX, worldZ);
    }
}

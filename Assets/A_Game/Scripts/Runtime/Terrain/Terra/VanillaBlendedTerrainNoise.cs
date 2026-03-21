public sealed class VanillaBlendedTerrainNoise
{
    private const string TerrainNoiseHashName = "minecraft:terrain";

    // Vanilla overworld base_3d_noise.
    private const double XZScale = 0.25d;
    private const double YScale = 0.125d;
    private const double XZFactor = 80d;
    private const double YFactor = 160d;
    private const double SmearScaleMultiplier = 8d;

    private readonly VanillaBlendedNoise _blendedNoise;

    public VanillaBlendedTerrainNoise(int seed)
    {
        VanillaXoroshiroRandom random = VanillaXoroshiroRandom.Create(seed);
        VanillaPositionalRandom positional = random.ForkPositional();
        _blendedNoise = new VanillaBlendedNoise(
            positional.FromHashOf(TerrainNoiseHashName),
            XZScale,
            YScale,
            XZFactor,
            YFactor,
            SmearScaleMultiplier);
    }

    public float Sample(int worldX, int minecraftY, int worldZ)
    {
        return (float)_blendedNoise.Sample(worldX, minecraftY, worldZ);
    }
}

public sealed class VanillaJaggedNoise
{
    private const string JaggedNoiseHashName = "minecraft:jagged";
    private const double XZScale = 1500d;
    private static readonly VanillaNoiseParameters JaggedNoiseParameters =
        new(-16, new[]
        {
            1d, 1d, 1d, 1d, 1d, 1d, 1d, 1d,
            1d, 1d, 1d, 1d, 1d, 1d, 1d, 1d,
        });

    private readonly VanillaNormalNoise _jaggedNoise;

    public VanillaJaggedNoise(int seed)
    {
        VanillaXoroshiroRandom random = VanillaXoroshiroRandom.Create(seed);
        VanillaPositionalRandom positional = random.ForkPositional();
        _jaggedNoise = new VanillaNormalNoise(positional.FromHashOf(JaggedNoiseHashName), JaggedNoiseParameters);
    }

    public float Sample(int worldX, int worldZ)
    {
        return (float)_jaggedNoise.Sample(worldX * XZScale, 0d, worldZ * XZScale);
    }
}

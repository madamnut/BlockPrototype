public sealed class VanillaSurfaceNoise
{
    private const string SurfaceNoiseHashName = "minecraft:surface";
    private const string SurfaceSecondaryNoiseHashName = "minecraft:surface_secondary";
    private static readonly VanillaNoiseParameters SurfaceNoiseParameters =
        new(-6, new[] { 1d, 1d, 1d });
    private static readonly VanillaNoiseParameters SurfaceSecondaryNoiseParameters =
        new(-6, new[] { 1d, 1d, 0d, 1d });

    private readonly VanillaNormalNoise _surfaceNoise;
    private readonly VanillaNormalNoise _surfaceSecondaryNoise;
    private readonly VanillaPositionalRandom _surfaceDepthRandom;

    public VanillaSurfaceNoise(int seed)
    {
        VanillaXoroshiroRandom random = VanillaXoroshiroRandom.Create(seed);
        VanillaPositionalRandom positional = random.ForkPositional();
        _surfaceNoise = new VanillaNormalNoise(positional.FromHashOf(SurfaceNoiseHashName), SurfaceNoiseParameters);
        _surfaceSecondaryNoise = new VanillaNormalNoise(positional.FromHashOf(SurfaceSecondaryNoiseHashName), SurfaceSecondaryNoiseParameters);
        _surfaceDepthRandom = positional;
    }

    public float Sample(int worldX, int worldZ)
    {
        return (float)_surfaceNoise.Sample(worldX, 0d, worldZ);
    }

    public float SampleSecondary(int worldX, int worldZ)
    {
        return (float)_surfaceSecondaryNoise.Sample(worldX, 0d, worldZ);
    }

    public int GetSurfaceDepth(int worldX, int worldZ)
    {
        double surfaceNoise = _surfaceNoise.Sample(worldX, 0d, worldZ);
        double random = _surfaceDepthRandom.At(worldX, 0, worldZ).NextDouble();
        return (int)((surfaceNoise * 2.75d) + 3d + (random * 0.25d));
    }
}

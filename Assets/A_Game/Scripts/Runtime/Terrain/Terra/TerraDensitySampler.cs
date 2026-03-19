public sealed class TerraDensitySampler
{
    private readonly TerraHeightSampler _heightSampler;
    private readonly ITerraDensityFunction _rootFunction;

    public TerraDensitySampler(int seed, int seaLevel, TerraNoiseSettings noiseSettings, TerraHeightSampler heightSampler, TerraDensitySettings densitySettings)
    {
        _heightSampler = heightSampler;
        _rootFunction = BuildDefaultGraph(seed, seaLevel, noiseSettings, densitySettings);
    }

    public TerraSurfaceSample SampleSurface(int worldX, int worldZ)
    {
        return _heightSampler.Sample(worldX, worldZ);
    }

    public float SampleDensity(int worldX, int worldY, int worldZ)
    {
        return SampleDensity(_heightSampler.Sample(worldX, worldZ), worldX, worldY, worldZ);
    }

    public float SampleDensity(TerraSurfaceSample surface, int worldX, int worldY, int worldZ)
    {
        TerraDensityContext context = new(
            worldX,
            worldY,
            worldZ,
            surface.surfaceHeight,
            surface.continentalness,
            surface.erosion,
            surface.weirdness,
            surface.peaksAndValleys,
            surface.offset,
            surface.factor,
            surface.jaggedness);
        return _rootFunction.Evaluate(in context);
    }

    private static ITerraDensityFunction BuildDefaultGraph(int seed, int seaLevel, TerraNoiseSettings noiseSettings, TerraDensitySettings densitySettings)
    {
        ITerraDensityFunction baseHeight = new TerraConstantDensityFunction(seaLevel);
        ITerraDensityFunction offset = new TerraShapeChannelDensityFunction(TerraShapeChannel.Offset);
        ITerraDensityFunction factorTimesPv = new TerraMultiplyDensityFunction(
            new TerraShapeChannelDensityFunction(TerraShapeChannel.Factor),
            new TerraShapeChannelDensityFunction(TerraShapeChannel.PeaksAndValleys));
        ITerraDensityFunction jaggednessTimesWeirdness = new TerraMultiplyDensityFunction(
            new TerraShapeChannelDensityFunction(TerraShapeChannel.Jaggedness),
            new TerraShapeChannelDensityFunction(TerraShapeChannel.Weirdness));
        ITerraDensityFunction negativeY = new TerraMultiplyDensityFunction(
            new TerraYDensityFunction(),
            new TerraConstantDensityFunction(-1f));

        ITerraDensityFunction baseShape = new TerraAddDensityFunction(
            new TerraAddDensityFunction(
                new TerraAddDensityFunction(
                    new TerraAddDensityFunction(baseHeight, offset),
                    factorTimesPv),
                jaggednessTimesWeirdness),
            negativeY);

        ITerraDensityFunction primaryNoise = new TerraSignedNoise3DDensityFunction(
            seed,
            densitySettings.primaryScaleXZ,
            densitySettings.primaryScaleY,
            densitySettings.primaryAmplitude,
            densitySettings.primarySeedOffset);
        ITerraDensityFunction detailNoise = new TerraSignedNoise3DDensityFunction(
            seed,
            densitySettings.detailScaleXZ,
            densitySettings.detailScaleY,
            densitySettings.detailAmplitude,
            densitySettings.detailSeedOffset);

        ITerraDensityFunction terrainDensity = new TerraAddDensityFunction(
            new TerraAddDensityFunction(baseShape, primaryNoise),
            detailNoise);

        ITerraDensityFunction caveNoise = new TerraSignedNoise3DDensityFunction(
            seed,
            densitySettings.caveScaleXZ,
            densitySettings.caveScaleY,
            densitySettings.caveNoiseAmplitude,
            densitySettings.caveSeedOffset);
        ITerraDensityFunction caveCore = new TerraClampDensityFunction(
            new TerraSubtractDensityFunction(
                new TerraConstantDensityFunction(densitySettings.caveThreshold),
                new TerraAbsDensityFunction(caveNoise)),
            0f,
            densitySettings.caveThreshold);
        ITerraDensityFunction caveMask = new TerraVerticalRangeMaskDensityFunction(
            densitySettings.caveMinY,
            densitySettings.caveMaxY,
            densitySettings.caveFade);
        ITerraDensityFunction caveCarve = new TerraMultiplyDensityFunction(
            new TerraMultiplyDensityFunction(
                caveCore,
                new TerraConstantDensityFunction(densitySettings.caveCarveStrength)),
            caveMask);

        return new TerraSubtractDensityFunction(terrainDensity, caveCarve);
    }
}

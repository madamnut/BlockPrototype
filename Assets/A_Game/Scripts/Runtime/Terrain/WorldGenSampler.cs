using UnityEngine;

public static class WorldGenSampler
{
    public static float SampleContinentalness(
        int worldX,
        int worldZ,
        in VanillaClimateNoiseManaged climate)
    {
        return VanillaNoise.SampleOverworldContinentalness(worldX, worldZ, climate);
    }

    public static float SampleErosion(
        int worldX,
        int worldZ,
        in VanillaClimateNoiseManaged climate)
    {
        return VanillaNoise.SampleOverworldErosion(worldX, worldZ, climate);
    }

    public static float SampleWeirdness(
        int worldX,
        int worldZ,
        in VanillaClimateNoiseManaged climate)
    {
        return VanillaNoise.SampleOverworldWeirdness(worldX, worldZ, climate);
    }

    public static float SampleFoldedWeirdness(
        int worldX,
        int worldZ,
        in VanillaClimateNoiseManaged climate)
    {
        float weirdness = SampleWeirdness(worldX, worldZ, climate);
        return WorldGenPrototypeJobs.CalculatePvFromWeirdness(weirdness);
    }

    public static float SampleContinentalness(
        int worldX,
        int worldZ,
        int seed,
        in TerrainGenerationSettings settings,
        float[] continentalnessCdfLut = null)
    {
        float worldRegionX = worldX / (float)WorldGenPrototypeJobs.RegionSizeInBlocks;
        float worldRegionZ = worldZ / (float)WorldGenPrototypeJobs.RegionSizeInBlocks;
        float rawContinentalness = WorldGenPrototypeJobs.SampleRawContinentalness(
            seed,
            worldRegionX,
            worldRegionZ,
            settings.continentalness);

        return RemapRawContinentalness(
            rawContinentalness,
            settings.useContinentalnessRemap,
            continentalnessCdfLut);
    }

    public static float SampleErosion(
        int worldX,
        int worldZ,
        int seed,
        in TerrainGenerationSettings settings,
        float[] erosionCdfLut = null)
    {
        float worldRegionX = worldX / (float)WorldGenPrototypeJobs.RegionSizeInBlocks;
        float worldRegionZ = worldZ / (float)WorldGenPrototypeJobs.RegionSizeInBlocks;
        float rawErosion = WorldGenPrototypeJobs.SampleRawErosion(
            seed,
            worldRegionX,
            worldRegionZ,
            settings.erosion);

        return RemapRawScalar(
            rawErosion,
            settings.useErosionRemap,
            erosionCdfLut);
    }

    public static float SampleWeirdness(
        int worldX,
        int worldZ,
        int seed,
        in TerrainGenerationSettings settings,
        float[] ridgesCdfLut = null)
    {
        float worldRegionX = worldX / (float)WorldGenPrototypeJobs.RegionSizeInBlocks;
        float worldRegionZ = worldZ / (float)WorldGenPrototypeJobs.RegionSizeInBlocks;
        float rawRidges = WorldGenPrototypeJobs.SampleRawRidges(
            seed,
            worldRegionX,
            worldRegionZ,
            settings.ridges);

        return RemapRawScalar(
            rawRidges,
            settings.useRidgesRemap,
            ridgesCdfLut);
    }

    public static float SampleFoldedWeirdness(
        int worldX,
        int worldZ,
        int seed,
        in TerrainGenerationSettings settings,
        float[] ridgesCdfLut = null)
    {
        float weirdness = SampleWeirdness(worldX, worldZ, seed, settings, ridgesCdfLut);
        return WorldGenPrototypeJobs.CalculatePvFromWeirdness(weirdness);
    }

    public static int ComposeSurfaceHeight(
        int worldX,
        int worldZ,
        float continentalness,
        float erosion,
        float weirdness,
        float foldedWeirdness,
        SplineTreeBakedNode[] offsetSplineNodes = null,
        SplineTreeBakedPoint[] offsetSplinePoints = null,
        SplineTreeBakedNode[] factorSplineNodes = null,
        SplineTreeBakedPoint[] factorSplinePoints = null,
        SplineTreeBakedNode[] jaggednessSplineNodes = null,
        SplineTreeBakedPoint[] jaggednessSplinePoints = null,
        VanillaBlendedNoiseManaged? blendedNoise = null,
        VanillaNormalNoiseManaged? jaggedNoise = null)
    {
        if (HasDensitySplineTrees(
                offsetSplineNodes,
                offsetSplinePoints,
                factorSplineNodes,
                factorSplinePoints,
                jaggednessSplineNodes,
                jaggednessSplinePoints))
        {
            TerrainShapeSample shape = EvaluateTerrainShape(
                continentalness,
                erosion,
                weirdness,
                foldedWeirdness,
                offsetSplineNodes,
                offsetSplinePoints,
                factorSplineNodes,
                factorSplinePoints,
                jaggednessSplineNodes,
                jaggednessSplinePoints);
            VanillaBlendedNoiseManaged blended = blendedNoise ?? VanillaNoise.CreateManagedOverworldBlendedNoise(0);
            VanillaNormalNoiseManaged jagged = jaggedNoise ?? VanillaNoise.CreateManagedJaggedNoise(0);
            float jaggedSample = VanillaNoise.SampleJaggedNoise(worldX, worldZ, jagged);
            return WorldGenDensity.FindSurfaceHeight(worldX, worldZ, TerrainData.WorldHeight, shape, jaggedSample, blended);
        }

        return 0;
    }

    public static TerrainShapeSample EvaluateTerrainShape(
        float continentalness,
        float erosion,
        float weirdness,
        float foldedWeirdness,
        SplineTreeBakedNode[] offsetSplineNodes,
        SplineTreeBakedPoint[] offsetSplinePoints,
        SplineTreeBakedNode[] factorSplineNodes,
        SplineTreeBakedPoint[] factorSplinePoints,
        SplineTreeBakedNode[] jaggednessSplineNodes,
        SplineTreeBakedPoint[] jaggednessSplinePoints)
    {
        float offset = SplineTreeEvaluator.EvaluateValue(
            continentalness,
            erosion,
            weirdness,
            foldedWeirdness,
            offsetSplineNodes,
            offsetSplinePoints);
        float factor = SplineTreeEvaluator.EvaluateValue(
            continentalness,
            erosion,
            weirdness,
            foldedWeirdness,
            factorSplineNodes,
            factorSplinePoints);
        float jaggedness = SplineTreeEvaluator.EvaluateValue(
            continentalness,
            erosion,
            weirdness,
            foldedWeirdness,
            jaggednessSplineNodes,
            jaggednessSplinePoints);
        return new TerrainShapeSample(offset, Mathf.Max(0.0001f, factor), jaggedness);
    }

    public static int SampleSurfaceHeight(
        int worldX,
        int worldZ,
        in VanillaClimateNoiseManaged climate,
        VanillaBlendedNoiseManaged blendedNoise,
        VanillaNormalNoiseManaged jaggedNoise,
        SplineTreeBakedNode[] offsetSplineNodes = null,
        SplineTreeBakedPoint[] offsetSplinePoints = null,
        SplineTreeBakedNode[] factorSplineNodes = null,
        SplineTreeBakedPoint[] factorSplinePoints = null,
        SplineTreeBakedNode[] jaggednessSplineNodes = null,
        SplineTreeBakedPoint[] jaggednessSplinePoints = null)
    {
        float continentalness = SampleContinentalness(worldX, worldZ, climate);
        float erosion = SampleErosion(worldX, worldZ, climate);
        float weirdness = SampleWeirdness(worldX, worldZ, climate);
        float foldedWeirdness = WorldGenPrototypeJobs.CalculatePvFromWeirdness(weirdness);
        return ComposeSurfaceHeight(
            worldX,
            worldZ,
            continentalness,
            erosion,
            weirdness,
            foldedWeirdness,
            offsetSplineNodes,
            offsetSplinePoints,
            factorSplineNodes,
            factorSplinePoints,
            jaggednessSplineNodes,
            jaggednessSplinePoints,
            blendedNoise,
            jaggedNoise);
    }

    public static int SampleSurfaceHeight(
        int worldX,
        int worldZ,
        int seed,
        in TerrainGenerationSettings settings,
        float[] continentalnessCdfLut = null,
        float[] erosionCdfLut = null,
        float[] ridgesCdfLut = null,
        SplineTreeBakedNode[] offsetSplineNodes = null,
        SplineTreeBakedPoint[] offsetSplinePoints = null,
        SplineTreeBakedNode[] factorSplineNodes = null,
        SplineTreeBakedPoint[] factorSplinePoints = null,
        SplineTreeBakedNode[] jaggednessSplineNodes = null,
        SplineTreeBakedPoint[] jaggednessSplinePoints = null)
    {
        VanillaClimateNoiseManaged climate = VanillaNoise.CreateManagedOverworldClimateNoise(seed);
        VanillaBlendedNoiseManaged blendedNoise = VanillaNoise.CreateManagedOverworldBlendedNoise(seed);
        VanillaNormalNoiseManaged jaggedNoise = VanillaNoise.CreateManagedJaggedNoise(seed);
        return SampleSurfaceHeight(
            worldX,
            worldZ,
            climate,
            blendedNoise,
            jaggedNoise,
            offsetSplineNodes,
            offsetSplinePoints,
            factorSplineNodes,
            factorSplinePoints,
            jaggednessSplineNodes,
            jaggednessSplinePoints);
    }

    private static bool HasDensitySplineTrees(
        SplineTreeBakedNode[] offsetSplineNodes,
        SplineTreeBakedPoint[] offsetSplinePoints,
        SplineTreeBakedNode[] factorSplineNodes,
        SplineTreeBakedPoint[] factorSplinePoints,
        SplineTreeBakedNode[] jaggednessSplineNodes,
        SplineTreeBakedPoint[] jaggednessSplinePoints)
    {
        return offsetSplineNodes != null &&
               offsetSplinePoints != null &&
               factorSplineNodes != null &&
               factorSplinePoints != null &&
               jaggednessSplineNodes != null &&
               jaggednessSplinePoints != null &&
               offsetSplineNodes.Length > 0 &&
               offsetSplinePoints.Length > 0 &&
               factorSplineNodes.Length > 0 &&
               factorSplinePoints.Length > 0 &&
               jaggednessSplineNodes.Length > 0 &&
               jaggednessSplinePoints.Length > 0;
    }

    public static float RemapRawContinentalness(
        float rawContinentalness,
        bool useCdfRemap,
        float[] continentalnessCdfLut)
    {
        float normalized = Mathf.Clamp01(rawContinentalness);
        if (useCdfRemap && continentalnessCdfLut != null && continentalnessCdfLut.Length > 1)
        {
            float scaledIndex = normalized * (continentalnessCdfLut.Length - 1);
            int lowerIndex = Mathf.Clamp(Mathf.FloorToInt(scaledIndex), 0, continentalnessCdfLut.Length - 1);
            int upperIndex = Mathf.Min(lowerIndex + 1, continentalnessCdfLut.Length - 1);
            float t = scaledIndex - lowerIndex;
            normalized = Mathf.Lerp(continentalnessCdfLut[lowerIndex], continentalnessCdfLut[upperIndex], t);
        }

        return (normalized * 2f) - 1f;
    }

    public static float RemapRawScalar(
        float rawValue,
        bool useCdfRemap,
        float[] cdfLut)
    {
        float normalized = Mathf.Clamp01(rawValue);
        if (useCdfRemap && cdfLut != null && cdfLut.Length > 1)
        {
            float scaledIndex = normalized * (cdfLut.Length - 1);
            int lowerIndex = Mathf.Clamp(Mathf.FloorToInt(scaledIndex), 0, cdfLut.Length - 1);
            int upperIndex = Mathf.Min(lowerIndex + 1, cdfLut.Length - 1);
            float t = scaledIndex - lowerIndex;
            normalized = Mathf.Lerp(cdfLut[lowerIndex], cdfLut[upperIndex], t);
        }

        return (normalized * 2f) - 1f;
    }

}

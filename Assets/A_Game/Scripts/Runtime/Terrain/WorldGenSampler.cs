using UnityEngine;

public static class WorldGenSampler
{

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
        SplineTreeBakedNode[] legacyTerrainHeightSplineNodes = null,
        SplineTreeBakedPoint[] legacyTerrainHeightSplinePoints = null,
        float[] continentalnessHeightLut = null)
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
            return WorldGenDensity.FindSurfaceHeight(TerrainData.WorldHeight, shape, foldedWeirdness);
        }

        if (legacyTerrainHeightSplineNodes != null &&
            legacyTerrainHeightSplinePoints != null &&
            legacyTerrainHeightSplineNodes.Length > 0 &&
            legacyTerrainHeightSplinePoints.Length > 0)
        {
            return SplineTreeEvaluator.EvaluateHeight(
                continentalness,
                erosion,
                weirdness,
                foldedWeirdness,
                legacyTerrainHeightSplineNodes,
                legacyTerrainHeightSplinePoints,
                TerrainData.WorldHeight);
        }

        if (continentalnessHeightLut != null && continentalnessHeightLut.Length > 1)
        {
            float normalized = (Mathf.Clamp(continentalness, -1f, 1f) + 1f) * 0.5f;
            float scaledIndex = normalized * (continentalnessHeightLut.Length - 1);
            int lowerIndex = Mathf.Clamp(Mathf.FloorToInt(scaledIndex), 0, continentalnessHeightLut.Length - 1);
            int upperIndex = Mathf.Min(lowerIndex + 1, continentalnessHeightLut.Length - 1);
            float t = scaledIndex - lowerIndex;
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(continentalnessHeightLut[lowerIndex], continentalnessHeightLut[upperIndex], t)), 0, TerrainData.WorldHeight - 1);
        }

        float normalizedContinentalness = Mathf.Clamp01((continentalness + 1f) * 0.5f);
        return Mathf.RoundToInt(Mathf.Lerp(0f, 180f, normalizedContinentalness));
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
        SplineTreeBakedPoint[] jaggednessSplinePoints = null,
        SplineTreeBakedNode[] legacyTerrainHeightSplineNodes = null,
        SplineTreeBakedPoint[] legacyTerrainHeightSplinePoints = null,
        float[] continentalnessHeightLut = null)
    {
        float continentalness = SampleContinentalness(worldX, worldZ, seed, settings, continentalnessCdfLut);
        float erosion = SampleErosion(worldX, worldZ, seed, settings, erosionCdfLut);
        float weirdness = SampleWeirdness(worldX, worldZ, seed, settings, ridgesCdfLut);
        float foldedWeirdness = WorldGenPrototypeJobs.CalculatePvFromWeirdness(weirdness);
        return ComposeSurfaceHeight(
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
            legacyTerrainHeightSplineNodes,
            legacyTerrainHeightSplinePoints,
            continentalnessHeightLut);
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

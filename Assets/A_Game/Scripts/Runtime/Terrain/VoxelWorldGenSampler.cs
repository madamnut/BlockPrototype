using UnityEngine;

public static class VoxelWorldGenSampler
{
    private const float ReliefStrengthHigh = 42f;
    private const float ReliefStrengthLow = 8f;
    private const float UpliftExponent = 1.25f;
    private const float DepressionExponent = 1.6f;
    private const float DepressionScale = 0.85f;

    public static float SampleContinentalness(
        int worldX,
        int worldZ,
        int seed,
        in VoxelTerrainGenerationSettings settings,
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
            settings.useContinentalnessCdfRemap,
            continentalnessCdfLut);
    }

    public static float SampleErosion(
        int worldX,
        int worldZ,
        int seed,
        in VoxelTerrainGenerationSettings settings,
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
            settings.useErosionCdfRemap,
            erosionCdfLut);
    }

    public static float SampleWeirdness(
        int worldX,
        int worldZ,
        int seed,
        in VoxelTerrainGenerationSettings settings,
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
            settings.useRidgesCdfRemap,
            ridgesCdfLut);
    }

    public static float SamplePv(
        int worldX,
        int worldZ,
        int seed,
        in VoxelTerrainGenerationSettings settings,
        float[] ridgesCdfLut = null)
    {
        float weirdness = SampleWeirdness(worldX, worldZ, seed, settings, ridgesCdfLut);
        return WorldGenPrototypeJobs.CalculatePvFromWeirdness(weirdness);
    }

    public static int ComposeSurfaceHeight(
        float continentalness,
        int minHeight,
        int seaLevel,
        int maxHeight)
    {
        int clampedMin = Mathf.Clamp(minHeight, 0, VoxelTerrainData.WorldHeight - 1);
        int clampedSeaLevel = Mathf.Clamp(seaLevel, 0, VoxelTerrainData.WorldHeight - 1);
        int clampedMax = Mathf.Clamp(Mathf.Max(seaLevel, maxHeight), 0, VoxelTerrainData.WorldHeight - 1);

        if (continentalness < 0f)
        {
            float oceanT = Mathf.Clamp01(continentalness + 1f);
            return Mathf.RoundToInt(Mathf.Lerp(clampedMin, clampedSeaLevel, oceanT));
        }

        float landT = Mathf.Clamp01(continentalness);
        return Mathf.RoundToInt(Mathf.Lerp(clampedSeaLevel, clampedMax, landT));
    }

    public static int ComposeSurfaceHeight(
        float continentalness,
        float erosion,
        float pv,
        int minHeight,
        int seaLevel,
        int maxHeight)
    {
        int baseHeight = ComposeSurfaceHeight(continentalness, minHeight, seaLevel, maxHeight);
        float inlandMask = Mathf.SmoothStep(-0.05f, 0.35f, continentalness);
        float erosion01 = (erosion + 1f) * 0.5f;
        float reliefStrength = Mathf.Lerp(ReliefStrengthHigh, ReliefStrengthLow, erosion01);
        float uplift = Mathf.Pow(Mathf.Max(0f, pv), UpliftExponent);
        float depression = Mathf.Pow(Mathf.Max(0f, -pv), DepressionExponent);
        float finalHeight = baseHeight +
                            (uplift * inlandMask * reliefStrength) -
                            (depression * inlandMask * reliefStrength * DepressionScale);

        return Mathf.Clamp(Mathf.RoundToInt(finalHeight), 0, VoxelTerrainData.WorldHeight - 1);
    }

    public static int SampleSurfaceHeight(
        int worldX,
        int worldZ,
        int seed,
        in VoxelTerrainGenerationSettings settings,
        float[] continentalnessCdfLut = null,
        float[] erosionCdfLut = null,
        float[] ridgesCdfLut = null)
    {
        float continentalness = SampleContinentalness(worldX, worldZ, seed, settings, continentalnessCdfLut);
        float erosion = SampleErosion(worldX, worldZ, seed, settings, erosionCdfLut);
        float pv = SamplePv(worldX, worldZ, seed, settings, ridgesCdfLut);
        return ComposeSurfaceHeight(
            continentalness,
            erosion,
            pv,
            settings.minTerrainHeight,
            settings.seaLevel,
            settings.maxTerrainHeight);
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

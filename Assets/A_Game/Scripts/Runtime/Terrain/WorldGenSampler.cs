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

    public static float SamplePv(
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
        int seaLevel,
        float[] continentalnessHeightLut = null)
    {
        if (continentalnessHeightLut != null && continentalnessHeightLut.Length > 1)
        {
            float normalized = (Mathf.Clamp(continentalness, -1f, 1f) + 1f) * 0.5f;
            float scaledIndex = normalized * (continentalnessHeightLut.Length - 1);
            int lowerIndex = Mathf.Clamp(Mathf.FloorToInt(scaledIndex), 0, continentalnessHeightLut.Length - 1);
            int upperIndex = Mathf.Min(lowerIndex + 1, continentalnessHeightLut.Length - 1);
            float t = scaledIndex - lowerIndex;
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(continentalnessHeightLut[lowerIndex], continentalnessHeightLut[upperIndex], t)), 0, TerrainData.WorldHeight - 1);
        }

        int clampedSeaLevel = Mathf.Clamp(seaLevel, 0, TerrainData.WorldHeight - 1);

        if (continentalness < 0f)
        {
            float oceanT = Mathf.Clamp01(continentalness + 1f);
            return Mathf.RoundToInt(Mathf.Lerp(0f, clampedSeaLevel, oceanT));
        }

        float landT = Mathf.Clamp01(continentalness);
        return Mathf.RoundToInt(Mathf.Lerp(clampedSeaLevel, 180f, landT));
    }

    public static int SampleSurfaceHeight(
        int worldX,
        int worldZ,
        int seed,
        in TerrainGenerationSettings settings,
        float[] continentalnessCdfLut = null,
        float[] erosionCdfLut = null,
        float[] ridgesCdfLut = null,
        float[] continentalnessHeightLut = null)
    {
        float continentalness = SampleContinentalness(worldX, worldZ, seed, settings, continentalnessCdfLut);
        return ComposeSurfaceHeight(continentalness, settings.seaLevel, continentalnessHeightLut);
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

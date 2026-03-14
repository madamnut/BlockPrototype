using UnityEngine;

public static class VoxelWorldGenSampler
{
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

    public static int SampleSurfaceHeight(
        int worldX,
        int worldZ,
        int seed,
        in VoxelTerrainGenerationSettings settings,
        float[] continentalnessCdfLut = null)
    {
        float continentalness = SampleContinentalness(worldX, worldZ, seed, settings, continentalnessCdfLut);
        return ComposeSurfaceHeight(
            continentalness,
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
}

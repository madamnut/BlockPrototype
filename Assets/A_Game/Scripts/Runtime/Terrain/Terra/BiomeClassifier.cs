using UnityEngine;

public static class BiomeClassifier
{
    private const float MushroomFieldsContinentalnessThreshold = -1.05f;
    private const float OceanContinentalnessThreshold = -0.19f;
    private const float InlandContinentalnessMax = 0.55f;
    private const float RiverErosionMax = 0.55f;
    private const float RiverWeirdnessMin = -0.05f;
    private const float RiverWeirdnessMax = 0.05f;

    public static BiomeKind Classify(float continentalness, float erosion, float weirdness)
    {
        if (continentalness < MushroomFieldsContinentalnessThreshold)
        {
            return BiomeKind.Special;
        }

        if (continentalness < OceanContinentalnessThreshold)
        {
            return BiomeKind.Ocean;
        }

        if (continentalness < InlandContinentalnessMax
            && erosion < RiverErosionMax
            && weirdness >= RiverWeirdnessMin
            && weirdness <= RiverWeirdnessMax)
        {
            return BiomeKind.River;
        }

        return BiomeKind.Land;
    }
}

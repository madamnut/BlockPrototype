using System;
using UnityEngine;

[Serializable]
public struct TerraNoiseSettings
{
    [Min(0.0001f)] public float continentalnessScale;
    [Min(0.0001f)] public float erosionScale;
    [Min(0.0001f)] public float weirdnessScale;

    public float offsetBase;
    public float continentalnessOffset;
    public float erosionOffset;

    public float factorBase;
    public float continentalnessFactor;
    public float erosionFactor;
    public float peaksAndValleysFactor;

    public float weirdnessJaggedness;
    public float peaksAndValleysJaggedness;

    public float continentalnessSeedOffset;
    public float erosionSeedOffset;
    public float weirdnessSeedOffset;

    public static TerraNoiseSettings Default => new()
    {
        continentalnessScale = 0.0028f,
        erosionScale = 0.009f,
        weirdnessScale = 0.018f,
        offsetBase = 8f,
        continentalnessOffset = 20f,
        erosionOffset = 6f,
        factorBase = 10f,
        continentalnessFactor = 10f,
        erosionFactor = 4f,
        peaksAndValleysFactor = 6f,
        weirdnessJaggedness = 2.5f,
        peaksAndValleysJaggedness = 3.5f,
        continentalnessSeedOffset = 17.17f,
        erosionSeedOffset = 91.73f,
        weirdnessSeedOffset = 173.31f,
    };
}

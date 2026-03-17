using UnityEngine.Serialization;
using UnityEngine;

[CreateAssetMenu(
    fileName = "WorldGenRemapProfile",
    menuName = "World/WorldGen/Remap Profile")]
public sealed class WorldGenRemapProfileAsset : ScriptableObject
{
    [Header("Bake Source")]
    [SerializeField] private WorldGenSettingsAsset sourceSettingsAsset;

    [Header("Bake Settings")]
    [SerializeField, Min(16)] private int lutResolution = 512;
    [SerializeField, Min(1024)] private int sampleCount = 200000;
    [SerializeField] private int bakeRandomSeed = 12345;
    [SerializeField, Min(1)] private int sectorRange = 128;

    [Header("Baked Data")]
    [FormerlySerializedAs("bakedCdfLut")]
    [FormerlySerializedAs("bakedContinentalnessCdfLut")]
    [SerializeField, HideInInspector] private float[] bakedContinentalnessRemapLut = new float[0];
    [FormerlySerializedAs("bakedErosionCdfLut")]
    [SerializeField, HideInInspector] private float[] bakedErosionRemapLut = new float[0];
    [FormerlySerializedAs("bakedRidgesCdfLut")]
    [SerializeField, HideInInspector] private float[] bakedRidgesRemapLut = new float[0];
    [FormerlySerializedAs("bakedSampleCount")]
    [SerializeField, HideInInspector] private int bakedContinentalnessSampleCount;
    [SerializeField, HideInInspector] private int bakedErosionSampleCount;
    [SerializeField, HideInInspector] private int bakedRidgesSampleCount;
    [SerializeField, HideInInspector] private int bakeVersion;
    [FormerlySerializedAs("bakedSummary")]
    [SerializeField, HideInInspector] private string bakedContinentalnessSummary = "Continentalness: Not baked";
    [SerializeField, HideInInspector] private string bakedErosionSummary = "Erosion: Not baked";
    [SerializeField, HideInInspector] private string bakedRidgesSummary = "Weirdness: Not baked";

    public WorldGenSettingsAsset SourceSettingsAsset => sourceSettingsAsset;
    public int LutResolution => Mathf.Max(16, lutResolution);
    public int SampleCount => Mathf.Max(1024, sampleCount);
    public int BakeRandomSeed => bakeRandomSeed;
    public int SectorRange => Mathf.Max(1, sectorRange);
    public float[] BakedContinentalnessRemapLut => bakedContinentalnessRemapLut;
    public float[] BakedErosionRemapLut => bakedErosionRemapLut;
    public float[] BakedRidgesRemapLut => bakedRidgesRemapLut;
    public int BakeVersion => bakeVersion;
    public string BakedSummary => $"{bakedContinentalnessSummary}\n{bakedErosionSummary}\n{bakedRidgesSummary}";

    public bool HasContinentalnessRemap => bakedContinentalnessRemapLut != null && bakedContinentalnessRemapLut.Length > 1;
    public bool HasErosionRemap => bakedErosionRemapLut != null && bakedErosionRemapLut.Length > 1;
    public bool HasRidgesRemap => bakedRidgesRemapLut != null && bakedRidgesRemapLut.Length > 1;

    public void StoreBakedLuts(
        float[] continentalnessLut,
        int continentalnessSampleCount,
        float continentalnessRawMin,
        float continentalnessRawAverage,
        float continentalnessRawMax,
        float[] erosionLut,
        int erosionSampleCount,
        float erosionRawMin,
        float erosionRawAverage,
        float erosionRawMax,
        float[] ridgesLut,
        int ridgesSampleCount,
        float ridgesRawMin,
        float ridgesRawAverage,
        float ridgesRawMax)
    {
        bakedContinentalnessRemapLut = continentalnessLut ?? new float[0];
        bakedErosionRemapLut = erosionLut ?? new float[0];
        bakedRidgesRemapLut = ridgesLut ?? new float[0];
        bakedContinentalnessSampleCount = continentalnessSampleCount;
        bakedErosionSampleCount = erosionSampleCount;
        bakedRidgesSampleCount = ridgesSampleCount;
        bakeVersion++;
        bakedContinentalnessSummary = $"Continentalness: Samples {continentalnessSampleCount}, Raw Min/Avg/Max {continentalnessRawMin:F4} / {continentalnessRawAverage:F4} / {continentalnessRawMax:F4}, Remap LUT {bakedContinentalnessRemapLut.Length}";
        bakedErosionSummary = $"Erosion: Samples {erosionSampleCount}, Raw Min/Avg/Max {erosionRawMin:F4} / {erosionRawAverage:F4} / {erosionRawMax:F4}, Remap LUT {bakedErosionRemapLut.Length}";
        bakedRidgesSummary = $"Weirdness: Samples {ridgesSampleCount}, Raw Min/Avg/Max {ridgesRawMin:F4} / {ridgesRawAverage:F4} / {ridgesRawMax:F4}, Remap LUT {bakedRidgesRemapLut.Length}";
    }
}

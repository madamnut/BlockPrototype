using UnityEngine.Serialization;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ContinentalnessCdfProfile",
    menuName = "World/WorldGen/CDF Profile")]
public sealed class ContinentalnessCdfProfileAsset : ScriptableObject
{
    [Header("Runtime")]
    [SerializeField] private bool enableRemap = true;

    [Header("Bake Source")]
    [SerializeField] private WorldGenSettingsAsset sourceSettingsAsset;

    [Header("Bake Settings")]
    [SerializeField, Min(16)] private int lutResolution = 512;
    [SerializeField, Min(1024)] private int sampleCount = 200000;
    [SerializeField] private int bakeRandomSeed = 12345;
    [SerializeField, Min(1)] private int sectorRange = 128;

    [Header("Baked Data")]
    [FormerlySerializedAs("bakedCdfLut")]
    [SerializeField, HideInInspector] private float[] bakedContinentalnessCdfLut = new float[0];
    [SerializeField, HideInInspector] private float[] bakedErosionCdfLut = new float[0];
    [SerializeField, HideInInspector] private float[] bakedRidgesCdfLut = new float[0];
    [SerializeField, HideInInspector] private float[] bakedTemperatureCdfLut = new float[0];
    [SerializeField, HideInInspector] private float[] bakedPrecipitationCdfLut = new float[0];
    [FormerlySerializedAs("bakedSampleCount")]
    [SerializeField, HideInInspector] private int bakedContinentalnessSampleCount;
    [SerializeField, HideInInspector] private int bakedErosionSampleCount;
    [SerializeField, HideInInspector] private int bakedRidgesSampleCount;
    [SerializeField, HideInInspector] private int bakedTemperatureSampleCount;
    [SerializeField, HideInInspector] private int bakedPrecipitationSampleCount;
    [SerializeField, HideInInspector] private int bakeVersion;
    [FormerlySerializedAs("bakedSummary")]
    [SerializeField, HideInInspector] private string bakedContinentalnessSummary = "Continentalness: Not baked";
    [SerializeField, HideInInspector] private string bakedErosionSummary = "Erosion: Not baked";
    [SerializeField, HideInInspector] private string bakedRidgesSummary = "Peaks/Ridges: Not baked";
    [SerializeField, HideInInspector] private string bakedTemperatureSummary = "Temperature: Not baked";
    [SerializeField, HideInInspector] private string bakedPrecipitationSummary = "Precipitation: Not baked";

    public bool EnableRemap => enableRemap;
    public WorldGenSettingsAsset SourceSettingsAsset => sourceSettingsAsset;
    public int LutResolution => Mathf.Max(16, lutResolution);
    public int SampleCount => Mathf.Max(1024, sampleCount);
    public int BakeRandomSeed => bakeRandomSeed;
    public int SectorRange => Mathf.Max(1, sectorRange);
    public float[] BakedContinentalnessCdfLut => bakedContinentalnessCdfLut;
    public float[] BakedErosionCdfLut => bakedErosionCdfLut;
    public float[] BakedRidgesCdfLut => bakedRidgesCdfLut;
    public float[] BakedTemperatureCdfLut => bakedTemperatureCdfLut;
    public float[] BakedPrecipitationCdfLut => bakedPrecipitationCdfLut;
    public int BakeVersion => bakeVersion;
    public string BakedSummary => $"{bakedContinentalnessSummary}\n{bakedErosionSummary}\n{bakedRidgesSummary}\n{bakedTemperatureSummary}\n{bakedPrecipitationSummary}";

    public bool HasContinentalnessRemap => enableRemap && bakedContinentalnessCdfLut != null && bakedContinentalnessCdfLut.Length > 1;
    public bool HasErosionRemap => enableRemap && bakedErosionCdfLut != null && bakedErosionCdfLut.Length > 1;
    public bool HasRidgesRemap => enableRemap && bakedRidgesCdfLut != null && bakedRidgesCdfLut.Length > 1;
    public bool HasTemperatureRemap => enableRemap && bakedTemperatureCdfLut != null && bakedTemperatureCdfLut.Length > 1;
    public bool HasPrecipitationRemap => enableRemap && bakedPrecipitationCdfLut != null && bakedPrecipitationCdfLut.Length > 1;

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
        float ridgesRawMax,
        float[] temperatureLut,
        int temperatureSampleCount,
        float temperatureRawMin,
        float temperatureRawAverage,
        float temperatureRawMax,
        float[] precipitationLut,
        int precipitationSampleCount,
        float precipitationRawMin,
        float precipitationRawAverage,
        float precipitationRawMax)
    {
        bakedContinentalnessCdfLut = continentalnessLut ?? new float[0];
        bakedErosionCdfLut = erosionLut ?? new float[0];
        bakedRidgesCdfLut = ridgesLut ?? new float[0];
        bakedTemperatureCdfLut = temperatureLut ?? new float[0];
        bakedPrecipitationCdfLut = precipitationLut ?? new float[0];
        bakedContinentalnessSampleCount = continentalnessSampleCount;
        bakedErosionSampleCount = erosionSampleCount;
        bakedRidgesSampleCount = ridgesSampleCount;
        bakedTemperatureSampleCount = temperatureSampleCount;
        bakedPrecipitationSampleCount = precipitationSampleCount;
        bakeVersion++;
        bakedContinentalnessSummary = $"Continentalness: Samples {continentalnessSampleCount}, Raw Min/Avg/Max {continentalnessRawMin:F4} / {continentalnessRawAverage:F4} / {continentalnessRawMax:F4}, LUT {bakedContinentalnessCdfLut.Length}";
        bakedErosionSummary = $"Erosion: Samples {erosionSampleCount}, Raw Min/Avg/Max {erosionRawMin:F4} / {erosionRawAverage:F4} / {erosionRawMax:F4}, LUT {bakedErosionCdfLut.Length}";
        bakedRidgesSummary = $"Peaks/Ridges: Samples {ridgesSampleCount}, Raw Min/Avg/Max {ridgesRawMin:F4} / {ridgesRawAverage:F4} / {ridgesRawMax:F4}, LUT {bakedRidgesCdfLut.Length}";
        bakedTemperatureSummary = $"Temperature: Samples {temperatureSampleCount}, Raw Min/Avg/Max {temperatureRawMin:F4} / {temperatureRawAverage:F4} / {temperatureRawMax:F4}, LUT {bakedTemperatureCdfLut.Length}";
        bakedPrecipitationSummary = $"Precipitation: Samples {precipitationSampleCount}, Raw Min/Avg/Max {precipitationRawMin:F4} / {precipitationRawAverage:F4} / {precipitationRawMax:F4}, LUT {bakedPrecipitationCdfLut.Length}";
    }
}

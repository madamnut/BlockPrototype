using UnityEngine;

[CreateAssetMenu(
    fileName = "ContinentalnessCdfProfile",
    menuName = "World Gen Prototype/Continentalness CDF Profile")]
public sealed class ContinentalnessCdfProfileAsset : ScriptableObject
{
    [Header("Runtime")]
    [SerializeField] private bool enableRemap = true;

    [Header("Bake Source")]
    [SerializeField] private ContinentalnessSettingsAsset sourceSettingsAsset;

    [Header("Bake Settings")]
    [SerializeField, Min(16)] private int lutResolution = 512;
    [SerializeField, Min(1024)] private int sampleCount = 200000;
    [SerializeField] private int bakeRandomSeed = 12345;
    [SerializeField, Min(1)] private int sectorRange = 128;

    [Header("Baked Data")]
    [SerializeField, HideInInspector] private float[] bakedCdfLut = new float[0];
    [SerializeField, HideInInspector] private int bakedSampleCount;
    [SerializeField, HideInInspector] private int bakeVersion;
    [SerializeField, HideInInspector] private string bakedSummary = "Not baked";

    public bool EnableRemap => enableRemap && bakedCdfLut != null && bakedCdfLut.Length > 1;
    public ContinentalnessSettingsAsset SourceSettingsAsset => sourceSettingsAsset;
    public int LutResolution => Mathf.Max(16, lutResolution);
    public int SampleCount => Mathf.Max(1024, sampleCount);
    public int BakeRandomSeed => bakeRandomSeed;
    public int SectorRange => Mathf.Max(1, sectorRange);
    public float[] BakedCdfLut => bakedCdfLut;
    public int BakeVersion => bakeVersion;
    public string BakedSummary => bakedSummary;

    public void StoreBakedLut(float[] lut, int actualSampleCount, float rawMin, float rawAverage, float rawMax)
    {
        bakedCdfLut = lut ?? new float[0];
        bakedSampleCount = actualSampleCount;
        bakeVersion++;
        bakedSummary = $"Samples: {actualSampleCount}, Raw Min/Avg/Max: {rawMin:F4} / {rawAverage:F4} / {rawMax:F4}, LUT: {bakedCdfLut.Length}";
    }
}

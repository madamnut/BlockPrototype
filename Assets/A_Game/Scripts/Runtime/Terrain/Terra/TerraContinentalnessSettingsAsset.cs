using UnityEngine;

[System.Serializable]
public sealed class TerraVanillaNoiseSettingsData
{
    [SerializeField] private int firstOctave;
    [SerializeField] private float[] amplitudes;

    public TerraVanillaNoiseSettingsData(int firstOctave, float[] amplitudes)
    {
        this.firstOctave = firstOctave;
        this.amplitudes = amplitudes;
    }

    public int FirstOctave => firstOctave;
    public float[] Amplitudes => amplitudes;
}

[CreateAssetMenu(fileName = "TerraContinentalnessSettings", menuName = "World/Terra/Continentalness Settings")]
public sealed class TerraContinentalnessSettingsAsset : ScriptableObject
{
    [Header("Coordinate Sampling")]
    [SerializeField, Min(0.0001f)] private float scale = 1f;
    [SerializeField, Min(0.0001f)] private float shiftInputScale = 0.25f;
    [SerializeField, Min(0.0001f)] private float continentsXZScale = 0.25f;
    [SerializeField] private float shiftStrength = 4f;

    [Header("Offset Noise")]
    [SerializeField] private TerraVanillaNoiseSettingsData offsetNoise = new(-3, new[] { 1f, 1f, 1f, 0f });

    [Header("Continentalness Noise")]
    [SerializeField] private TerraVanillaNoiseSettingsData continentalnessNoise = new(-9, new[] { 1f, 1f, 2f, 2f, 2f, 1f, 1f, 1f, 1f });

    public float Scale => scale;
    public float ShiftInputScale => shiftInputScale;
    public float ContinentsXZScale => continentsXZScale;
    public float ShiftStrength => shiftStrength;
    public TerraVanillaNoiseSettingsData OffsetNoise => offsetNoise;
    public TerraVanillaNoiseSettingsData ContinentalnessNoise => continentalnessNoise;

    private void OnValidate()
    {
        scale = Mathf.Max(0.0001f, scale);
        shiftInputScale = Mathf.Max(0.0001f, shiftInputScale);
        continentsXZScale = Mathf.Max(0.0001f, continentsXZScale);
    }
}

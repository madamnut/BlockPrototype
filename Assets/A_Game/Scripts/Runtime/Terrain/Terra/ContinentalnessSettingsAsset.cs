using UnityEngine;

[System.Serializable]
public sealed class VanillaNoiseSettingsData
{
    [SerializeField] private int firstOctave;
    [SerializeField] private float[] amplitudes;

    public VanillaNoiseSettingsData(int firstOctave, float[] amplitudes)
    {
        this.firstOctave = firstOctave;
        this.amplitudes = amplitudes;
    }

    public int FirstOctave => firstOctave;
    public float[] Amplitudes => amplitudes;
}

[CreateAssetMenu(fileName = "ContinentalnessSettings", menuName = "World/Terra/Continentalness Settings")]
public sealed class ContinentalnessSettingsAsset : ScriptableObject
{
    [Header("Coordinate Sampling")]
    [SerializeField, Min(0.0001f)] private float shiftInputScale = 0.25f;
    [SerializeField, Min(0.0001f)] private float continentsXZScale = 0.25f;
    [SerializeField] private float shiftStrength = 4f;

    [Header("Offset Noise")]
    [SerializeField] private VanillaNoiseSettingsData offsetNoise = new(-3, new[] { 1f, 1f, 1f, 0f });

    [Header("Continentalness Noise")]
    [SerializeField] private VanillaNoiseSettingsData continentalnessNoise = new(-9, new[] { 1f, 1f, 2f, 2f, 2f, 1f, 1f, 1f, 1f });

    public float ShiftInputScale => shiftInputScale;
    public float ContinentsXZScale => continentsXZScale;
    public float ShiftStrength => shiftStrength;
    public VanillaNoiseSettingsData OffsetNoise => offsetNoise;
    public VanillaNoiseSettingsData ContinentalnessNoise => continentalnessNoise;

    private void OnValidate()
    {
        shiftInputScale = Mathf.Max(0.0001f, shiftInputScale);
        continentsXZScale = Mathf.Max(0.0001f, continentsXZScale);
    }
}

using UnityEngine;

[CreateAssetMenu(fileName = "TemperatureSettings", menuName = "World/Terra/Temperature Settings")]
public sealed class TemperatureSettingsAsset : ScriptableObject
{
    [Header("Coordinate Sampling")]
    [SerializeField, Min(0.0001f)] private float shiftInputScale = 0.25f;
    [SerializeField, Min(0.0001f)] private float temperatureXZScale = 0.25f;
    [SerializeField] private float shiftStrength = 4f;

    [Header("Noise Routing")]
    [SerializeField] private string shiftNoiseHashName = "minecraft:offset";
    [SerializeField] private string temperatureNoiseHashName = "minecraft:temperature";

    [Header("Offset Noise")]
    [SerializeField] private VanillaNoiseSettingsData offsetNoise = new(-3, new[] { 1f, 1f, 1f, 0f });

    [Header("Temperature Noise")]
    [SerializeField] private VanillaNoiseSettingsData temperatureNoise = new(-10, new[] { 1.5f, 0f, 1f, 0f, 0f, 0f });

    public float ShiftInputScale => shiftInputScale;
    public float TemperatureXZScale => temperatureXZScale;
    public float ShiftStrength => shiftStrength;
    public string ShiftNoiseHashName => shiftNoiseHashName;
    public string TemperatureNoiseHashName => temperatureNoiseHashName;
    public VanillaNoiseSettingsData OffsetNoise => offsetNoise;
    public VanillaNoiseSettingsData TemperatureNoise => temperatureNoise;

    private void OnValidate()
    {
        shiftInputScale = Mathf.Max(0.0001f, shiftInputScale);
        temperatureXZScale = Mathf.Max(0.0001f, temperatureXZScale);
        shiftNoiseHashName = string.IsNullOrWhiteSpace(shiftNoiseHashName) ? "minecraft:offset" : shiftNoiseHashName.Trim();
        temperatureNoiseHashName = string.IsNullOrWhiteSpace(temperatureNoiseHashName) ? "minecraft:temperature" : temperatureNoiseHashName.Trim();
    }
}

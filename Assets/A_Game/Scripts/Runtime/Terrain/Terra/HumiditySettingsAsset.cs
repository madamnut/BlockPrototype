using UnityEngine;

[CreateAssetMenu(fileName = "HumiditySettings", menuName = "World/Terra/Humidity Settings")]
public sealed class HumiditySettingsAsset : ScriptableObject
{
    [Header("Coordinate Sampling")]
    [SerializeField, Min(0.0001f)] private float shiftInputScale = 0.25f;
    [SerializeField, Min(0.0001f)] private float humidityXZScale = 0.25f;
    [SerializeField] private float shiftStrength = 4f;

    [Header("Noise Routing")]
    [SerializeField] private string shiftNoiseHashName = "minecraft:offset";
    [SerializeField] private string humidityNoiseHashName = "minecraft:vegetation";

    [Header("Offset Noise")]
    [SerializeField] private VanillaNoiseSettingsData offsetNoise = new(-3, new[] { 1f, 1f, 1f, 0f });

    [Header("Humidity Noise (Vanilla Vegetation)")]
    [SerializeField] private VanillaNoiseSettingsData humidityNoise = new(-8, new[] { 1f, 1f, 0f, 0f, 0f, 0f });

    public float ShiftInputScale => shiftInputScale;
    public float HumidityXZScale => humidityXZScale;
    public float ShiftStrength => shiftStrength;
    public string ShiftNoiseHashName => shiftNoiseHashName;
    public string HumidityNoiseHashName => humidityNoiseHashName;
    public VanillaNoiseSettingsData OffsetNoise => offsetNoise;
    public VanillaNoiseSettingsData HumidityNoise => humidityNoise;

    private void OnValidate()
    {
        shiftInputScale = Mathf.Max(0.0001f, shiftInputScale);
        humidityXZScale = Mathf.Max(0.0001f, humidityXZScale);
        shiftNoiseHashName = string.IsNullOrWhiteSpace(shiftNoiseHashName) ? "minecraft:offset" : shiftNoiseHashName.Trim();
        humidityNoiseHashName = string.IsNullOrWhiteSpace(humidityNoiseHashName) ? "minecraft:vegetation" : humidityNoiseHashName.Trim();
    }
}

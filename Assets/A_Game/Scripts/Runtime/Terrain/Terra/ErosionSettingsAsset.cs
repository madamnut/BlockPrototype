using UnityEngine;

[CreateAssetMenu(fileName = "ErosionSettings", menuName = "World/Terra/Erosion Settings")]
public sealed class ErosionSettingsAsset : ScriptableObject
{
    [Header("Coordinate Sampling")]
    [SerializeField, Min(0.0001f)] private float shiftInputScale = 0.25f;
    [SerializeField, Min(0.0001f)] private float erosionXZScale = 0.25f;
    [SerializeField] private float shiftStrength = 4f;

    [Header("Noise Routing")]
    [SerializeField] private string shiftNoiseHashName = "minecraft:offset";
    [SerializeField] private string erosionNoiseHashName = "minecraft:erosion";

    [Header("Offset Noise")]
    [SerializeField] private VanillaNoiseSettingsData offsetNoise = new(-3, new[] { 1f, 1f, 1f, 0f });

    [Header("Erosion Noise")]
    [SerializeField] private VanillaNoiseSettingsData erosionNoise = new(-9, new[] { 1f, 1f, 0f, 1f, 1f });

    public float ShiftInputScale => shiftInputScale;
    public float ErosionXZScale => erosionXZScale;
    public float ShiftStrength => shiftStrength;
    public string ShiftNoiseHashName => shiftNoiseHashName;
    public string ErosionNoiseHashName => erosionNoiseHashName;
    public VanillaNoiseSettingsData OffsetNoise => offsetNoise;
    public VanillaNoiseSettingsData ErosionNoise => erosionNoise;

    private void OnValidate()
    {
        shiftInputScale = Mathf.Max(0.0001f, shiftInputScale);
        erosionXZScale = Mathf.Max(0.0001f, erosionXZScale);
        shiftNoiseHashName = string.IsNullOrWhiteSpace(shiftNoiseHashName) ? "minecraft:offset" : shiftNoiseHashName.Trim();
        erosionNoiseHashName = string.IsNullOrWhiteSpace(erosionNoiseHashName) ? "minecraft:erosion" : erosionNoiseHashName.Trim();
    }
}

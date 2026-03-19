using UnityEngine;

[CreateAssetMenu(fileName = "TerraWeirdnessSettings", menuName = "World/Terra/Weirdness Settings")]
public sealed class TerraWeirdnessSettingsAsset : ScriptableObject
{
    [Header("Coordinate Sampling")]
    [SerializeField, Min(0.0001f)] private float scale = 1f;
    [SerializeField, Min(0.0001f)] private float shiftInputScale = 0.25f;
    [SerializeField, Min(0.0001f)] private float ridgesXZScale = 0.25f;
    [SerializeField] private float shiftStrength = 4f;

    [Header("Noise Routing")]
    [SerializeField] private string shiftNoiseHashName = "minecraft:offset";
    [SerializeField] private string weirdnessNoiseHashName = "minecraft:ridge";

    [Header("Offset Noise")]
    [SerializeField] private TerraVanillaNoiseSettingsData offsetNoise = new(-3, new[] { 1f, 1f, 1f, 0f });

    [Header("Weirdness Noise")]
    [SerializeField] private TerraVanillaNoiseSettingsData weirdnessNoise = new(-7, new[] { 1f, 2f, 1f, 0f, 0f, 0f });

    public float Scale => scale;
    public float ShiftInputScale => shiftInputScale;
    public float RidgesXZScale => ridgesXZScale;
    public float ShiftStrength => shiftStrength;
    public string ShiftNoiseHashName => shiftNoiseHashName;
    public string WeirdnessNoiseHashName => weirdnessNoiseHashName;
    public TerraVanillaNoiseSettingsData OffsetNoise => offsetNoise;
    public TerraVanillaNoiseSettingsData WeirdnessNoise => weirdnessNoise;

    private void OnValidate()
    {
        scale = Mathf.Max(0.0001f, scale);
        shiftInputScale = Mathf.Max(0.0001f, shiftInputScale);
        ridgesXZScale = Mathf.Max(0.0001f, ridgesXZScale);
        shiftNoiseHashName = string.IsNullOrWhiteSpace(shiftNoiseHashName) ? "minecraft:offset" : shiftNoiseHashName.Trim();
        weirdnessNoiseHashName = string.IsNullOrWhiteSpace(weirdnessNoiseHashName) ? "minecraft:ridge" : weirdnessNoiseHashName.Trim();
    }
}

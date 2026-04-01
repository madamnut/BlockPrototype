using UnityEngine;

[CreateAssetMenu(fileName = "PrototypeTemperatureSettings", menuName = "World/Prototype/Temperature Settings")]
public sealed class PrototypeTemperatureSettingsAsset : ScriptableObject
{
    [SerializeField, Range(0f, 1f)] private float latitudeStrength = 0.8f;
    [SerializeField, Range(0.1f, 4f)] private float latitudeExponent = 1.2f;
    [SerializeField, Min(0.000001f)] private float frequency = 0.00025f;
    [SerializeField, Range(1, 12)] private int octaves = 5;
    [SerializeField, Min(1f)] private float lacunarity = 2f;
    [SerializeField, Range(0f, 1f)] private float persistence = 0.5f;
    [SerializeField] private float noiseAmplitude = 0.3f;
    [SerializeField] private int seedOffset;
    [SerializeField] private Vector2 coordinateOffset;
    [SerializeField] private float bias;

    public float LatitudeStrength => latitudeStrength;
    public float LatitudeExponent => latitudeExponent;
    public float Frequency => frequency;
    public int Octaves => octaves;
    public float Lacunarity => lacunarity;
    public float Persistence => persistence;
    public float NoiseAmplitude => noiseAmplitude;
    public int SeedOffset => seedOffset;
    public Vector2 CoordinateOffset => coordinateOffset;
    public float Bias => bias;

    private void OnValidate()
    {
        latitudeStrength = Mathf.Clamp01(latitudeStrength);
        latitudeExponent = Mathf.Clamp(latitudeExponent, 0.1f, 4f);
        frequency = Mathf.Max(0.000001f, frequency);
        octaves = Mathf.Clamp(octaves, 1, 12);
        lacunarity = Mathf.Max(1f, lacunarity);
        persistence = Mathf.Clamp01(persistence);
    }
}

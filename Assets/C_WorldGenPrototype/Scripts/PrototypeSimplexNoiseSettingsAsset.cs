using UnityEngine;

[CreateAssetMenu(fileName = "PrototypeSimplexNoiseSettings", menuName = "World/Prototype/Simplex Noise Settings")]
public sealed class PrototypeSimplexNoiseSettingsAsset : ScriptableObject
{
    [SerializeField, Min(0.000001f)] private float frequency = 0.001f;
    [SerializeField, Range(1, 12)] private int octaves = 6;
    [SerializeField, Min(1f)] private float lacunarity = 2f;
    [SerializeField, Range(0f, 1f)] private float persistence = 0.5f;
    [SerializeField] private float amplitude = 1f;
    [SerializeField] private int seedOffset;
    [SerializeField] private Vector2 coordinateOffset;
    [SerializeField] private float bias;

    public float Frequency => frequency;
    public int Octaves => octaves;
    public float Lacunarity => lacunarity;
    public float Persistence => persistence;
    public float Amplitude => amplitude;
    public int SeedOffset => seedOffset;
    public Vector2 CoordinateOffset => coordinateOffset;
    public float Bias => bias;

    private void OnValidate()
    {
        frequency = Mathf.Max(0.000001f, frequency);
        octaves = Mathf.Clamp(octaves, 1, 12);
        lacunarity = Mathf.Max(1f, lacunarity);
        persistence = Mathf.Clamp01(persistence);
    }
}

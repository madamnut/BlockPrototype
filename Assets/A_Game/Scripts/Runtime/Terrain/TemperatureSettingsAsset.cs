using UnityEngine;

[CreateAssetMenu(fileName = "TemperatureSettings", menuName = "World/Gen/Temperature Settings")]
public sealed class TemperatureSettingsAsset : ScriptableObject
{
    [Min(1)] public int worldSizeXZ = TerrainData.WorldSizeXZ;
    [Min(0f)] public float latitudeStrength = 0.85f;
    [Range(-180f, 180f)] public float latitudePhaseDegrees = 0f;
    [Header("Latitude Warp")]
    [Min(0f)] public float latitudeWarpStrength = 0f;
    [Min(1)] public int latitudeWarpBaseCycles = 3;
    [Min(1)] public int latitudeWarpOctaveCount = 2;
    [Range(0.01f, 1f)] public float latitudeWarpPersistence = 0.5f;
    [Min(1f)] public float latitudeWarpLacunarity = 2f;
    [Header("Noise")]
    [Min(1)] public int noiseBaseCycles = 4;
    [Min(1)] public int noiseOctaveCount = 3;
    [Range(0.01f, 1f)] public float noisePersistence = 0.5f;
    [Min(1f)] public float noiseLacunarity = 2f;
    [Min(0f)] public float noiseStrength = 0.15f;

    public TemperatureRuntimeSettings BuildRuntimeSettings()
    {
        return new TemperatureRuntimeSettings(
            worldSizeXZ,
            latitudeStrength,
            latitudePhaseDegrees,
            latitudeWarpStrength,
            latitudeWarpBaseCycles,
            latitudeWarpOctaveCount,
            latitudeWarpPersistence,
            latitudeWarpLacunarity,
            noiseBaseCycles,
            noiseOctaveCount,
            noisePersistence,
            noiseLacunarity,
            noiseStrength);
    }

    private void OnValidate()
    {
        worldSizeXZ = Mathf.Max(1, worldSizeXZ);
        latitudeStrength = Mathf.Max(0f, latitudeStrength);
        latitudeWarpStrength = Mathf.Max(0f, latitudeWarpStrength);
        latitudeWarpBaseCycles = Mathf.Max(1, latitudeWarpBaseCycles);
        latitudeWarpOctaveCount = Mathf.Max(1, latitudeWarpOctaveCount);
        latitudeWarpPersistence = Mathf.Clamp01(latitudeWarpPersistence);
        latitudeWarpLacunarity = Mathf.Max(1f, latitudeWarpLacunarity);
        noiseBaseCycles = Mathf.Max(1, noiseBaseCycles);
        noiseOctaveCount = Mathf.Max(1, noiseOctaveCount);
        noisePersistence = Mathf.Clamp01(noisePersistence);
        noiseLacunarity = Mathf.Max(1f, noiseLacunarity);
        noiseStrength = Mathf.Max(0f, noiseStrength);
    }
}

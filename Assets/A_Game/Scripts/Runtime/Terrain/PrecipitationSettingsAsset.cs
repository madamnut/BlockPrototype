using UnityEngine;

[CreateAssetMenu(fileName = "PrecipitationSettings", menuName = "World/Gen/Precipitation Settings")]
public sealed class PrecipitationSettingsAsset : ScriptableObject
{
    [Min(1)] public int worldSizeXZ = TerrainData.WorldSizeXZ;
    [Min(1)] public int baseCycles = 4;
    [Min(1)] public int octaveCount = 4;
    [Range(0.01f, 1f)] public float persistence = 0.5f;
    [Min(1f)] public float lacunarity = 2f;

    public PrecipitationRuntimeSettings BuildRuntimeSettings()
    {
        return new PrecipitationRuntimeSettings(
            worldSizeXZ,
            baseCycles,
            octaveCount,
            persistence,
            lacunarity);
    }

    private void OnValidate()
    {
        worldSizeXZ = Mathf.Max(1, worldSizeXZ);
        baseCycles = Mathf.Max(1, baseCycles);
        octaveCount = Mathf.Max(1, octaveCount);
        persistence = Mathf.Clamp01(persistence);
        lacunarity = Mathf.Max(1f, lacunarity);
    }
}

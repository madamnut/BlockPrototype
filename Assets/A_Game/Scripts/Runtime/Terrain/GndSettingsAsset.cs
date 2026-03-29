using UnityEngine;

[CreateAssetMenu(fileName = "GndSettings", menuName = "World/Gen/Gnd Settings")]
public sealed class GndSettingsAsset : ScriptableObject
{
    [Min(1)] public int worldSizeXZ = TerrainData.WorldSizeXZ;
    [Min(1)] public int sampleSpacing = 4;
    [Min(1)] public int baseCycles = 3;
    [Min(1)] public int octaveCount = 4;
    [Range(0.01f, 1f)] public float persistence = 0.5f;
    [Min(1f)] public float lacunarity = 2f;
    [Header("Domain Warp")]
    [Min(0f)] public float warpStrength = 0f;
    [Min(1)] public int warpBaseCycles = 6;
    [Min(1)] public int warpOctaveCount = 2;
    [Range(0.01f, 1f)] public float warpPersistence = 0.5f;
    [Min(1f)] public float warpLacunarity = 2f;

    public GndRuntimeSettings BuildRuntimeSettings(int seaLevel = TerrainData.DefaultSeaLevel)
    {
        return new GndRuntimeSettings(
            worldSizeXZ,
            sampleSpacing,
            seaLevel,
            baseCycles,
            octaveCount,
            persistence,
            lacunarity,
            warpStrength,
            warpBaseCycles,
            warpOctaveCount,
            warpPersistence,
            warpLacunarity);
    }

    private void Reset()
    {
        GndRuntimeSettings defaults = GndRuntimeSettings.CreateDefault();
        worldSizeXZ = defaults.worldSizeXZ;
        sampleSpacing = defaults.sampleSpacing;
        baseCycles = defaults.baseCycles;
        octaveCount = defaults.octaveCount;
        persistence = defaults.persistence;
        lacunarity = defaults.lacunarity;
        warpStrength = defaults.warpStrength;
        warpBaseCycles = defaults.warpBaseCycles;
        warpOctaveCount = defaults.warpOctaveCount;
        warpPersistence = defaults.warpPersistence;
        warpLacunarity = defaults.warpLacunarity;
    }

    private void OnValidate()
    {
        worldSizeXZ = Mathf.Max(1, worldSizeXZ);
        sampleSpacing = Mathf.Max(1, sampleSpacing);
        baseCycles = Mathf.Max(1, baseCycles);
        octaveCount = Mathf.Max(1, octaveCount);
        persistence = Mathf.Clamp01(persistence);
        lacunarity = Mathf.Max(1f, lacunarity);
        warpStrength = Mathf.Max(0f, warpStrength);
        warpBaseCycles = Mathf.Max(1, warpBaseCycles);
        warpOctaveCount = Mathf.Max(1, warpOctaveCount);
        warpPersistence = Mathf.Clamp01(warpPersistence);
        warpLacunarity = Mathf.Max(1f, warpLacunarity);
    }
}

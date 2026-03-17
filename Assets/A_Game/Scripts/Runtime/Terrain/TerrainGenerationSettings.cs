using System;
using UnityEngine;

[Serializable]
public struct TerrainGenerationSettings
{
    [Min(0)]
    public int seaLevel;

    public bool IsInitialized => true;

    public static TerrainGenerationSettings Default => new()
    {
        seaLevel = TerrainData.DefaultSeaLevel,
    };

    public static TerrainGenerationSettings Create(int seaLevel)
    {
        TerrainGenerationSettings settings = Default;
        settings.seaLevel = Mathf.Clamp(seaLevel, 0, TerrainData.WorldHeight - 1);
        return settings;
    }
}

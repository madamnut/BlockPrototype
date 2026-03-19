using System;

[Serializable]
public struct TerrainGenerationSettings
{
    public bool IsInitialized => true;
    public static TerrainGenerationSettings Default => new();
}

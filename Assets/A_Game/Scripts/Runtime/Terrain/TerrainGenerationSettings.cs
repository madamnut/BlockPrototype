using System;
using UnityEngine;

[Serializable]
public struct TerrainGenerationSettings
{
    [SerializeField] private TerraWorldGenSettingsAsset terraWorldGen;

    public TerraWorldGenSettingsAsset TerraWorldGen => terraWorldGen;
    public GndSettingsAsset Gnd => terraWorldGen != null ? terraWorldGen.gnd : null;
    public NestedSplineGraphAsset LiftNestedSpline => terraWorldGen != null ? terraWorldGen.liftNestedSpline : null;
    public ReliefSettingsAsset Relief => terraWorldGen != null ? terraWorldGen.relief : null;
    public VeinSettingsAsset Vein => terraWorldGen != null ? terraWorldGen.vein : null;
    public TemperatureSettingsAsset Temperature => terraWorldGen != null ? terraWorldGen.temperature : null;
    public PrecipitationSettingsAsset Precipitation => terraWorldGen != null ? terraWorldGen.precipitation : null;
    public bool IsInitialized => terraWorldGen == null || terraWorldGen.gnd != null;

    public GndRuntimeSettings BuildGndRuntimeSettings()
    {
        return terraWorldGen != null
            ? terraWorldGen.BuildGndRuntimeSettings()
            : GndRuntimeSettings.CreateDefault();
    }

    public LiftRuntimeSettings BuildLiftRuntimeSettings()
    {
        return terraWorldGen != null
            ? terraWorldGen.BuildLiftRuntimeSettings()
            : LiftRuntimeSettings.CreateDefault();
    }

    public TemperatureRuntimeSettings BuildTemperatureRuntimeSettings()
    {
        return terraWorldGen != null
            ? terraWorldGen.BuildTemperatureRuntimeSettings()
            : TemperatureRuntimeSettings.CreateDefault();
    }

    public ReliefRuntimeSettings BuildReliefRuntimeSettings()
    {
        return terraWorldGen != null
            ? terraWorldGen.BuildReliefRuntimeSettings()
            : ReliefRuntimeSettings.CreateDefault();
    }

    public VeinRuntimeSettings BuildVeinRuntimeSettings()
    {
        return terraWorldGen != null
            ? terraWorldGen.BuildVeinRuntimeSettings()
            : VeinRuntimeSettings.CreateDefault();
    }

    public PrecipitationRuntimeSettings BuildPrecipitationRuntimeSettings()
    {
        return terraWorldGen != null
            ? terraWorldGen.BuildPrecipitationRuntimeSettings()
            : PrecipitationRuntimeSettings.CreateDefault();
    }

    public static TerrainGenerationSettings Default => new();
}

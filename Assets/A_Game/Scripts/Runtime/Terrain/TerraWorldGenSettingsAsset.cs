using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "TerraWorldGenSettings", menuName = "World/Gen/Terra WorldGen Settings")]
public sealed class TerraWorldGenSettingsAsset : ScriptableObject
{
    [Range(0, TerrainData.WorldHeight - 1)] public int seaLevel = TerrainData.DefaultSeaLevel;
    public GndSettingsAsset gnd;
    public NestedSplineGraphAsset liftNestedSpline;
    public ReliefSettingsAsset relief;
    public VeinSettingsAsset vein;
    public TemperatureSettingsAsset temperature;
    [FormerlySerializedAs("humidity")] public PrecipitationSettingsAsset precipitation;

    public GndRuntimeSettings BuildGndRuntimeSettings()
    {
        return gnd != null
            ? gnd.BuildRuntimeSettings(seaLevel)
            : GndRuntimeSettings.CreateDefault(seaLevel);
    }

    public LiftRuntimeSettings BuildLiftRuntimeSettings()
    {
        return new LiftRuntimeSettings(
            liftNestedSpline != null ? NestedSplineGraphUtility.BakeLut3D(liftNestedSpline, LiftRuntimeSettings.DefaultNestedLutResolution) : null,
            liftNestedSpline != null ? LiftRuntimeSettings.DefaultNestedLutResolution : 0);
    }

    public TemperatureRuntimeSettings BuildTemperatureRuntimeSettings()
    {
        return temperature != null
            ? temperature.BuildRuntimeSettings()
            : TemperatureRuntimeSettings.CreateDefault();
    }

    public ReliefRuntimeSettings BuildReliefRuntimeSettings()
    {
        return relief != null
            ? relief.BuildRuntimeSettings()
            : ReliefRuntimeSettings.CreateDefault();
    }

    public VeinRuntimeSettings BuildVeinRuntimeSettings()
    {
        return vein != null
            ? vein.BuildRuntimeSettings()
            : VeinRuntimeSettings.CreateDefault();
    }

    public PrecipitationRuntimeSettings BuildPrecipitationRuntimeSettings()
    {
        return precipitation != null
            ? precipitation.BuildRuntimeSettings()
            : PrecipitationRuntimeSettings.CreateDefault();
    }

    private void OnValidate()
    {
        seaLevel = Mathf.Clamp(seaLevel, 0, TerrainData.WorldHeight - 1);
    }
}

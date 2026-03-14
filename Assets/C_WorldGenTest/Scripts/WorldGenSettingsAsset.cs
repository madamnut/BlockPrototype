using UnityEngine;

[CreateAssetMenu(
    fileName = "WorldGenSettings",
    menuName = "World/WorldGen/Settings")]
public sealed class WorldGenSettingsAsset : ScriptableObject
{
    [Header("Runtime Terrain")]
    [SerializeField, Range(0, VoxelTerrainData.WorldHeight - 1)] private int seaLevel = 63;
    [SerializeField, Range(0, VoxelTerrainData.WorldHeight - 1)] private int minTerrainHeight = 0;
    [SerializeField, Range(0, VoxelTerrainData.WorldHeight - 1)] private int maxTerrainHeight = 180;

    [Header("CDF Remap")]
    [SerializeField] private bool useContinentalnessCdfRemap = true;
    [SerializeField] private bool useErosionCdfRemap = true;
    [SerializeField] private bool useRidgesCdfRemap = true;

    [Header("Warp")]
    [SerializeField] private bool useWarp = true;
    [SerializeField, Min(1)] private int warpOctaves = 2;
    [SerializeField, Min(0.000001f)] private float warpFrequency = 1f / 20f;
    [SerializeField, Min(0f)] private float warpAmplitude = 0.28f;
    [SerializeField, Min(1f)] private float warpLacunarity = 2f;
    [SerializeField, Range(0f, 1f)] private float warpGain = 0.5f;

    [Header("Macro")]
    [SerializeField] private bool useMacro = true;
    [SerializeField, Min(1)] private int macroOctaves = 5;
    [SerializeField, Min(0.000001f)] private float macroFrequency = 1f / 26f;
    [SerializeField, Min(1f)] private float macroLacunarity = 2f;
    [SerializeField, Range(0f, 1f)] private float macroGain = 0.52f;
    [SerializeField, Min(0f)] private float macroWeight = 0.58f;

    [Header("Broad")]
    [SerializeField] private bool useBroad = true;
    [SerializeField, Min(1)] private int broadOctaves = 3;
    [SerializeField, Min(0.000001f)] private float broadFrequency = 1f / 47f;
    [SerializeField, Min(1f)] private float broadLacunarity = 2f;
    [SerializeField, Range(0f, 1f)] private float broadGain = 0.5f;
    [SerializeField, Min(0f)] private float broadWeight = 0.27f;

    [Header("Detail")]
    [SerializeField] private bool useDetail = true;
    [SerializeField, Min(1)] private int detailOctaves = 3;
    [SerializeField, Min(0.000001f)] private float detailFrequency = 1f / 11f;
    [SerializeField, Min(1f)] private float detailLacunarity = 2f;
    [SerializeField, Range(0f, 1f)] private float detailGain = 0.45f;
    [SerializeField, Min(0f)] private float detailWeight = 0.15f;

    [Header("Erosion Warp")]
    [SerializeField] private bool erosionUseWarp = true;
    [SerializeField, Min(1)] private int erosionWarpOctaves = 2;
    [SerializeField, Min(0.000001f)] private float erosionWarpFrequency = 1f / 18f;
    [SerializeField, Min(0f)] private float erosionWarpAmplitude = 0.2f;
    [SerializeField, Min(1f)] private float erosionWarpLacunarity = 2f;
    [SerializeField, Range(0f, 1f)] private float erosionWarpGain = 0.5f;

    [Header("Erosion Macro")]
    [SerializeField] private bool erosionUseMacro = true;
    [SerializeField, Min(1)] private int erosionMacroOctaves = 4;
    [SerializeField, Min(0.000001f)] private float erosionMacroFrequency = 1f / 20f;
    [SerializeField, Min(1f)] private float erosionMacroLacunarity = 2f;
    [SerializeField, Range(0f, 1f)] private float erosionMacroGain = 0.5f;
    [SerializeField, Min(0f)] private float erosionMacroWeight = 0.5f;

    [Header("Erosion Broad")]
    [SerializeField] private bool erosionUseBroad = true;
    [SerializeField, Min(1)] private int erosionBroadOctaves = 3;
    [SerializeField, Min(0.000001f)] private float erosionBroadFrequency = 1f / 42f;
    [SerializeField, Min(1f)] private float erosionBroadLacunarity = 2f;
    [SerializeField, Range(0f, 1f)] private float erosionBroadGain = 0.48f;
    [SerializeField, Min(0f)] private float erosionBroadWeight = 0.3f;

    [Header("Erosion Detail")]
    [SerializeField] private bool erosionUseDetail = true;
    [SerializeField, Min(1)] private int erosionDetailOctaves = 3;
    [SerializeField, Min(0.000001f)] private float erosionDetailFrequency = 1f / 10f;
    [SerializeField, Min(1f)] private float erosionDetailLacunarity = 2f;
    [SerializeField, Range(0f, 1f)] private float erosionDetailGain = 0.42f;
    [SerializeField, Min(0f)] private float erosionDetailWeight = 0.2f;

    [Header("Peaks/Ridges Warp")]
    [SerializeField] private bool ridgesUseWarp = true;
    [SerializeField, Min(1)] private int ridgesWarpOctaves = 2;
    [SerializeField, Min(0.000001f)] private float ridgesWarpFrequency = 1f / 16f;
    [SerializeField, Min(0f)] private float ridgesWarpAmplitude = 0.24f;
    [SerializeField, Min(1f)] private float ridgesWarpLacunarity = 2f;
    [SerializeField, Range(0f, 1f)] private float ridgesWarpGain = 0.5f;

    [Header("Peaks/Ridges Macro")]
    [SerializeField] private bool ridgesUseMacro = true;
    [SerializeField, Min(1)] private int ridgesMacroOctaves = 4;
    [SerializeField, Min(0.000001f)] private float ridgesMacroFrequency = 1f / 22f;
    [SerializeField, Min(1f)] private float ridgesMacroLacunarity = 2f;
    [SerializeField, Range(0f, 1f)] private float ridgesMacroGain = 0.5f;
    [SerializeField, Min(0f)] private float ridgesMacroWeight = 0.52f;

    [Header("Peaks/Ridges Broad")]
    [SerializeField] private bool ridgesUseBroad = true;
    [SerializeField, Min(1)] private int ridgesBroadOctaves = 3;
    [SerializeField, Min(0.000001f)] private float ridgesBroadFrequency = 1f / 38f;
    [SerializeField, Min(1f)] private float ridgesBroadLacunarity = 2f;
    [SerializeField, Range(0f, 1f)] private float ridgesBroadGain = 0.48f;
    [SerializeField, Min(0f)] private float ridgesBroadWeight = 0.3f;

    [Header("Peaks/Ridges Detail")]
    [SerializeField] private bool ridgesUseDetail = true;
    [SerializeField, Min(1)] private int ridgesDetailOctaves = 3;
    [SerializeField, Min(0.000001f)] private float ridgesDetailFrequency = 1f / 9f;
    [SerializeField, Min(1f)] private float ridgesDetailLacunarity = 2f;
    [SerializeField, Range(0f, 1f)] private float ridgesDetailGain = 0.45f;
    [SerializeField, Min(0f)] private float ridgesDetailWeight = 0.18f;

    [Header("Temperature Warp")]
    [SerializeField] private bool temperatureUseWarp = true;
    [SerializeField, Min(1)] private int temperatureWarpOctaves = 2;
    [SerializeField, Min(0.000001f)] private float temperatureWarpFrequency = 1f / 18f;
    [SerializeField, Min(0f)] private float temperatureWarpAmplitude = 0.18f;
    [SerializeField, Min(1f)] private float temperatureWarpLacunarity = 2f;
    [SerializeField, Range(0f, 1f)] private float temperatureWarpGain = 0.5f;

    [Header("Temperature Macro")]
    [SerializeField] private bool temperatureUseMacro = true;
    [SerializeField, Min(1)] private int temperatureMacroOctaves = 4;
    [SerializeField, Min(0.000001f)] private float temperatureMacroFrequency = 1f / 24f;
    [SerializeField, Min(1f)] private float temperatureMacroLacunarity = 2f;
    [SerializeField, Range(0f, 1f)] private float temperatureMacroGain = 0.5f;
    [SerializeField, Min(0f)] private float temperatureMacroWeight = 0.55f;

    [Header("Temperature Broad")]
    [SerializeField] private bool temperatureUseBroad = true;
    [SerializeField, Min(1)] private int temperatureBroadOctaves = 3;
    [SerializeField, Min(0.000001f)] private float temperatureBroadFrequency = 1f / 44f;
    [SerializeField, Min(1f)] private float temperatureBroadLacunarity = 2f;
    [SerializeField, Range(0f, 1f)] private float temperatureBroadGain = 0.48f;
    [SerializeField, Min(0f)] private float temperatureBroadWeight = 0.3f;

    [Header("Temperature Detail")]
    [SerializeField] private bool temperatureUseDetail = true;
    [SerializeField, Min(1)] private int temperatureDetailOctaves = 3;
    [SerializeField, Min(0.000001f)] private float temperatureDetailFrequency = 1f / 12f;
    [SerializeField, Min(1f)] private float temperatureDetailLacunarity = 2f;
    [SerializeField, Range(0f, 1f)] private float temperatureDetailGain = 0.42f;
    [SerializeField, Min(0f)] private float temperatureDetailWeight = 0.15f;

    [Header("Precipitation Warp")]
    [SerializeField] private bool precipitationUseWarp = true;
    [SerializeField, Min(1)] private int precipitationWarpOctaves = 2;
    [SerializeField, Min(0.000001f)] private float precipitationWarpFrequency = 1f / 19f;
    [SerializeField, Min(0f)] private float precipitationWarpAmplitude = 0.2f;
    [SerializeField, Min(1f)] private float precipitationWarpLacunarity = 2f;
    [SerializeField, Range(0f, 1f)] private float precipitationWarpGain = 0.5f;

    [Header("Precipitation Macro")]
    [SerializeField] private bool precipitationUseMacro = true;
    [SerializeField, Min(1)] private int precipitationMacroOctaves = 4;
    [SerializeField, Min(0.000001f)] private float precipitationMacroFrequency = 1f / 23f;
    [SerializeField, Min(1f)] private float precipitationMacroLacunarity = 2f;
    [SerializeField, Range(0f, 1f)] private float precipitationMacroGain = 0.5f;
    [SerializeField, Min(0f)] private float precipitationMacroWeight = 0.55f;

    [Header("Precipitation Broad")]
    [SerializeField] private bool precipitationUseBroad = true;
    [SerializeField, Min(1)] private int precipitationBroadOctaves = 3;
    [SerializeField, Min(0.000001f)] private float precipitationBroadFrequency = 1f / 40f;
    [SerializeField, Min(1f)] private float precipitationBroadLacunarity = 2f;
    [SerializeField, Range(0f, 1f)] private float precipitationBroadGain = 0.48f;
    [SerializeField, Min(0f)] private float precipitationBroadWeight = 0.28f;

    [Header("Precipitation Detail")]
    [SerializeField] private bool precipitationUseDetail = true;
    [SerializeField, Min(1)] private int precipitationDetailOctaves = 3;
    [SerializeField, Min(0.000001f)] private float precipitationDetailFrequency = 1f / 10f;
    [SerializeField, Min(1f)] private float precipitationDetailLacunarity = 2f;
    [SerializeField, Range(0f, 1f)] private float precipitationDetailGain = 0.42f;
    [SerializeField, Min(0f)] private float precipitationDetailWeight = 0.17f;

    [Header("Sea Bands")]
    [SerializeField, Range(-1f, 0f)] private float abyssUpperBound = -0.75f;
    [SerializeField] private Color abyssColor = new Color(0.03f, 0.07f, 0.18f, 1f);
    [SerializeField, Range(-1f, 0f)] private float deepOceanUpperBound = -0.5f;
    [SerializeField] private Color deepOceanColor = new Color(0.05f, 0.12f, 0.32f, 1f);
    [SerializeField, Range(-1f, 0f)] private float oceanUpperBound = -0.25f;
    [SerializeField] private Color oceanColor = new Color(0.08f, 0.25f, 0.52f, 1f);
    [SerializeField] private Color shallowOceanColor = new Color(0.2f, 0.5f, 0.74f, 1f);

    [Header("Land Bands")]
    [SerializeField, Range(0f, 1f)] private float coastUpperBound = 0.2f;
    [SerializeField] private Color coastColor = new Color(0.9f, 0.84f, 0.64f, 1f);
    [SerializeField, Range(0f, 1f)] private float inlandUpperBound = 0.5f;
    [SerializeField] private Color inlandColor = new Color(0.36f, 0.63f, 0.29f, 1f);
    [SerializeField, Range(0f, 1f)] private float deepInlandUpperBound = 0.75f;
    [SerializeField] private Color deepInlandColor = new Color(0.23f, 0.48f, 0.17f, 1f);
    [SerializeField] private Color continentalCoreColor = new Color(0.47f, 0.38f, 0.24f, 1f);

    public int SeaLevel => Mathf.Clamp(seaLevel, 0, VoxelTerrainData.WorldHeight - 1);
    public int MinTerrainHeight => Mathf.Clamp(minTerrainHeight, 0, VoxelTerrainData.WorldHeight - 1);
    public int MaxTerrainHeight => Mathf.Clamp(Mathf.Max(minTerrainHeight, maxTerrainHeight), 0, VoxelTerrainData.WorldHeight - 1);
    public bool UseContinentalnessCdfRemap => useContinentalnessCdfRemap;
    public bool UseErosionCdfRemap => useErosionCdfRemap;
    public bool UseRidgesCdfRemap => useRidgesCdfRemap;

    public ContinentalnessSettings ToSettings()
    {
        const float epsilon = 0.0001f;
        float sanitizedAbyssUpper = Mathf.Clamp(abyssUpperBound, -1f, -epsilon);
        float sanitizedDeepOceanUpper = Mathf.Clamp(deepOceanUpperBound, sanitizedAbyssUpper + epsilon, -epsilon);
        float sanitizedOceanUpper = Mathf.Clamp(oceanUpperBound, sanitizedDeepOceanUpper + epsilon, -epsilon);
        float sanitizedCoastUpper = Mathf.Clamp(coastUpperBound, epsilon, 1f);
        float sanitizedInlandUpper = Mathf.Clamp(inlandUpperBound, sanitizedCoastUpper + epsilon, 1f);
        float sanitizedDeepInlandUpper = Mathf.Clamp(deepInlandUpperBound, sanitizedInlandUpper + epsilon, 1f);

        return new ContinentalnessSettings
        {
            useWarp = useWarp,
            warpOctaves = Mathf.Max(1, warpOctaves),
            warpFrequency = Mathf.Max(0.000001f, warpFrequency),
            warpAmplitude = Mathf.Max(0f, warpAmplitude),
            warpLacunarity = Mathf.Max(1f, warpLacunarity),
            warpGain = Mathf.Clamp01(warpGain),
            useMacro = useMacro,
            macroOctaves = Mathf.Max(1, macroOctaves),
            macroFrequency = Mathf.Max(0.000001f, macroFrequency),
            macroLacunarity = Mathf.Max(1f, macroLacunarity),
            macroGain = Mathf.Clamp01(macroGain),
            macroWeight = Mathf.Max(0f, macroWeight),
            useBroad = useBroad,
            broadOctaves = Mathf.Max(1, broadOctaves),
            broadFrequency = Mathf.Max(0.000001f, broadFrequency),
            broadLacunarity = Mathf.Max(1f, broadLacunarity),
            broadGain = Mathf.Clamp01(broadGain),
            broadWeight = Mathf.Max(0f, broadWeight),
            useDetail = useDetail,
            detailOctaves = Mathf.Max(1, detailOctaves),
            detailFrequency = Mathf.Max(0.000001f, detailFrequency),
            detailLacunarity = Mathf.Max(1f, detailLacunarity),
            detailGain = Mathf.Clamp01(detailGain),
            detailWeight = Mathf.Max(0f, detailWeight),
            abyssUpperBound = sanitizedAbyssUpper,
            deepOceanUpperBound = sanitizedDeepOceanUpper,
            oceanUpperBound = sanitizedOceanUpper,
            coastUpperBound = sanitizedCoastUpper,
            inlandUpperBound = sanitizedInlandUpper,
            deepInlandUpperBound = sanitizedDeepInlandUpper,
            abyssColor = (Color32)abyssColor,
            deepOceanColor = (Color32)deepOceanColor,
            oceanColor = (Color32)oceanColor,
            shallowOceanColor = (Color32)shallowOceanColor,
            coastColor = (Color32)coastColor,
            inlandColor = (Color32)inlandColor,
            deepInlandColor = (Color32)deepInlandColor,
            continentalCoreColor = (Color32)continentalCoreColor,
        };
    }

    public ErosionSettings ToErosionSettings()
    {
        return new ErosionSettings
        {
            useWarp = erosionUseWarp,
            warpOctaves = Mathf.Max(1, erosionWarpOctaves),
            warpFrequency = Mathf.Max(0.000001f, erosionWarpFrequency),
            warpAmplitude = Mathf.Max(0f, erosionWarpAmplitude),
            warpLacunarity = Mathf.Max(1f, erosionWarpLacunarity),
            warpGain = Mathf.Clamp01(erosionWarpGain),
            useMacro = erosionUseMacro,
            macroOctaves = Mathf.Max(1, erosionMacroOctaves),
            macroFrequency = Mathf.Max(0.000001f, erosionMacroFrequency),
            macroLacunarity = Mathf.Max(1f, erosionMacroLacunarity),
            macroGain = Mathf.Clamp01(erosionMacroGain),
            macroWeight = Mathf.Max(0f, erosionMacroWeight),
            useBroad = erosionUseBroad,
            broadOctaves = Mathf.Max(1, erosionBroadOctaves),
            broadFrequency = Mathf.Max(0.000001f, erosionBroadFrequency),
            broadLacunarity = Mathf.Max(1f, erosionBroadLacunarity),
            broadGain = Mathf.Clamp01(erosionBroadGain),
            broadWeight = Mathf.Max(0f, erosionBroadWeight),
            useDetail = erosionUseDetail,
            detailOctaves = Mathf.Max(1, erosionDetailOctaves),
            detailFrequency = Mathf.Max(0.000001f, erosionDetailFrequency),
            detailLacunarity = Mathf.Max(1f, erosionDetailLacunarity),
            detailGain = Mathf.Clamp01(erosionDetailGain),
            detailWeight = Mathf.Max(0f, erosionDetailWeight),
        };
    }

    public RidgesSettings ToRidgesSettings()
    {
        return new RidgesSettings
        {
            useWarp = ridgesUseWarp,
            warpOctaves = Mathf.Max(1, ridgesWarpOctaves),
            warpFrequency = Mathf.Max(0.000001f, ridgesWarpFrequency),
            warpAmplitude = Mathf.Max(0f, ridgesWarpAmplitude),
            warpLacunarity = Mathf.Max(1f, ridgesWarpLacunarity),
            warpGain = Mathf.Clamp01(ridgesWarpGain),
            useMacro = ridgesUseMacro,
            macroOctaves = Mathf.Max(1, ridgesMacroOctaves),
            macroFrequency = Mathf.Max(0.000001f, ridgesMacroFrequency),
            macroLacunarity = Mathf.Max(1f, ridgesMacroLacunarity),
            macroGain = Mathf.Clamp01(ridgesMacroGain),
            macroWeight = Mathf.Max(0f, ridgesMacroWeight),
            useBroad = ridgesUseBroad,
            broadOctaves = Mathf.Max(1, ridgesBroadOctaves),
            broadFrequency = Mathf.Max(0.000001f, ridgesBroadFrequency),
            broadLacunarity = Mathf.Max(1f, ridgesBroadLacunarity),
            broadGain = Mathf.Clamp01(ridgesBroadGain),
            broadWeight = Mathf.Max(0f, ridgesBroadWeight),
            useDetail = ridgesUseDetail,
            detailOctaves = Mathf.Max(1, ridgesDetailOctaves),
            detailFrequency = Mathf.Max(0.000001f, ridgesDetailFrequency),
            detailLacunarity = Mathf.Max(1f, ridgesDetailLacunarity),
            detailGain = Mathf.Clamp01(ridgesDetailGain),
            detailWeight = Mathf.Max(0f, ridgesDetailWeight),
        };
    }

    public TemperatureSettings ToTemperatureSettings()
    {
        return new TemperatureSettings
        {
            useWarp = temperatureUseWarp,
            warpOctaves = Mathf.Max(1, temperatureWarpOctaves),
            warpFrequency = Mathf.Max(0.000001f, temperatureWarpFrequency),
            warpAmplitude = Mathf.Max(0f, temperatureWarpAmplitude),
            warpLacunarity = Mathf.Max(1f, temperatureWarpLacunarity),
            warpGain = Mathf.Clamp01(temperatureWarpGain),
            useMacro = temperatureUseMacro,
            macroOctaves = Mathf.Max(1, temperatureMacroOctaves),
            macroFrequency = Mathf.Max(0.000001f, temperatureMacroFrequency),
            macroLacunarity = Mathf.Max(1f, temperatureMacroLacunarity),
            macroGain = Mathf.Clamp01(temperatureMacroGain),
            macroWeight = Mathf.Max(0f, temperatureMacroWeight),
            useBroad = temperatureUseBroad,
            broadOctaves = Mathf.Max(1, temperatureBroadOctaves),
            broadFrequency = Mathf.Max(0.000001f, temperatureBroadFrequency),
            broadLacunarity = Mathf.Max(1f, temperatureBroadLacunarity),
            broadGain = Mathf.Clamp01(temperatureBroadGain),
            broadWeight = Mathf.Max(0f, temperatureBroadWeight),
            useDetail = temperatureUseDetail,
            detailOctaves = Mathf.Max(1, temperatureDetailOctaves),
            detailFrequency = Mathf.Max(0.000001f, temperatureDetailFrequency),
            detailLacunarity = Mathf.Max(1f, temperatureDetailLacunarity),
            detailGain = Mathf.Clamp01(temperatureDetailGain),
            detailWeight = Mathf.Max(0f, temperatureDetailWeight),
        };
    }

    public PrecipitationSettings ToPrecipitationSettings()
    {
        return new PrecipitationSettings
        {
            useWarp = precipitationUseWarp,
            warpOctaves = Mathf.Max(1, precipitationWarpOctaves),
            warpFrequency = Mathf.Max(0.000001f, precipitationWarpFrequency),
            warpAmplitude = Mathf.Max(0f, precipitationWarpAmplitude),
            warpLacunarity = Mathf.Max(1f, precipitationWarpLacunarity),
            warpGain = Mathf.Clamp01(precipitationWarpGain),
            useMacro = precipitationUseMacro,
            macroOctaves = Mathf.Max(1, precipitationMacroOctaves),
            macroFrequency = Mathf.Max(0.000001f, precipitationMacroFrequency),
            macroLacunarity = Mathf.Max(1f, precipitationMacroLacunarity),
            macroGain = Mathf.Clamp01(precipitationMacroGain),
            macroWeight = Mathf.Max(0f, precipitationMacroWeight),
            useBroad = precipitationUseBroad,
            broadOctaves = Mathf.Max(1, precipitationBroadOctaves),
            broadFrequency = Mathf.Max(0.000001f, precipitationBroadFrequency),
            broadLacunarity = Mathf.Max(1f, precipitationBroadLacunarity),
            broadGain = Mathf.Clamp01(precipitationBroadGain),
            broadWeight = Mathf.Max(0f, precipitationBroadWeight),
            useDetail = precipitationUseDetail,
            detailOctaves = Mathf.Max(1, precipitationDetailOctaves),
            detailFrequency = Mathf.Max(0.000001f, precipitationDetailFrequency),
            detailLacunarity = Mathf.Max(1f, precipitationDetailLacunarity),
            detailGain = Mathf.Clamp01(precipitationDetailGain),
            detailWeight = Mathf.Max(0f, precipitationDetailWeight),
        };
    }

    public static ContinentalnessSettings CreateDefaultSettings()
    {
        return new ContinentalnessSettings
        {
            useWarp = true,
            warpOctaves = 2,
            warpFrequency = 1f / 20f,
            warpAmplitude = 0.28f,
            warpLacunarity = 2f,
            warpGain = 0.5f,
            useMacro = true,
            macroOctaves = 5,
            macroFrequency = 1f / 26f,
            macroLacunarity = 2f,
            macroGain = 0.52f,
            macroWeight = 0.58f,
            useBroad = true,
            broadOctaves = 3,
            broadFrequency = 1f / 47f,
            broadLacunarity = 2f,
            broadGain = 0.5f,
            broadWeight = 0.27f,
            useDetail = true,
            detailOctaves = 3,
            detailFrequency = 1f / 11f,
            detailLacunarity = 2f,
            detailGain = 0.45f,
            detailWeight = 0.15f,
            abyssUpperBound = -0.75f,
            deepOceanUpperBound = -0.5f,
            oceanUpperBound = -0.25f,
            coastUpperBound = 0.2f,
            inlandUpperBound = 0.5f,
            deepInlandUpperBound = 0.75f,
            abyssColor = new Color32(8, 18, 46, 255),
            deepOceanColor = new Color32(13, 31, 82, 255),
            oceanColor = new Color32(20, 64, 133, 255),
            shallowOceanColor = new Color32(51, 128, 189, 255),
            coastColor = new Color32(230, 214, 163, 255),
            inlandColor = new Color32(92, 161, 74, 255),
            deepInlandColor = new Color32(59, 122, 43, 255),
            continentalCoreColor = new Color32(120, 97, 61, 255),
        };
    }

    public static ErosionSettings CreateDefaultErosionSettings()
    {
        return new ErosionSettings
        {
            useWarp = true,
            warpOctaves = 2,
            warpFrequency = 1f / 18f,
            warpAmplitude = 0.2f,
            warpLacunarity = 2f,
            warpGain = 0.5f,
            useMacro = true,
            macroOctaves = 4,
            macroFrequency = 1f / 20f,
            macroLacunarity = 2f,
            macroGain = 0.5f,
            macroWeight = 0.5f,
            useBroad = true,
            broadOctaves = 3,
            broadFrequency = 1f / 42f,
            broadLacunarity = 2f,
            broadGain = 0.48f,
            broadWeight = 0.3f,
            useDetail = true,
            detailOctaves = 3,
            detailFrequency = 1f / 10f,
            detailLacunarity = 2f,
            detailGain = 0.42f,
            detailWeight = 0.2f,
        };
    }

    public static RidgesSettings CreateDefaultRidgesSettings()
    {
        return new RidgesSettings
        {
            useWarp = true,
            warpOctaves = 2,
            warpFrequency = 1f / 16f,
            warpAmplitude = 0.24f,
            warpLacunarity = 2f,
            warpGain = 0.5f,
            useMacro = true,
            macroOctaves = 4,
            macroFrequency = 1f / 22f,
            macroLacunarity = 2f,
            macroGain = 0.5f,
            macroWeight = 0.52f,
            useBroad = true,
            broadOctaves = 3,
            broadFrequency = 1f / 38f,
            broadLacunarity = 2f,
            broadGain = 0.48f,
            broadWeight = 0.3f,
            useDetail = true,
            detailOctaves = 3,
            detailFrequency = 1f / 9f,
            detailLacunarity = 2f,
            detailGain = 0.45f,
            detailWeight = 0.18f,
        };
    }

    public static TemperatureSettings CreateDefaultTemperatureSettings()
    {
        return new TemperatureSettings
        {
            useWarp = true,
            warpOctaves = 2,
            warpFrequency = 1f / 18f,
            warpAmplitude = 0.18f,
            warpLacunarity = 2f,
            warpGain = 0.5f,
            useMacro = true,
            macroOctaves = 4,
            macroFrequency = 1f / 24f,
            macroLacunarity = 2f,
            macroGain = 0.5f,
            macroWeight = 0.55f,
            useBroad = true,
            broadOctaves = 3,
            broadFrequency = 1f / 44f,
            broadLacunarity = 2f,
            broadGain = 0.48f,
            broadWeight = 0.3f,
            useDetail = true,
            detailOctaves = 3,
            detailFrequency = 1f / 12f,
            detailLacunarity = 2f,
            detailGain = 0.42f,
            detailWeight = 0.15f,
        };
    }

    public static PrecipitationSettings CreateDefaultPrecipitationSettings()
    {
        return new PrecipitationSettings
        {
            useWarp = true,
            warpOctaves = 2,
            warpFrequency = 1f / 19f,
            warpAmplitude = 0.2f,
            warpLacunarity = 2f,
            warpGain = 0.5f,
            useMacro = true,
            macroOctaves = 4,
            macroFrequency = 1f / 23f,
            macroLacunarity = 2f,
            macroGain = 0.5f,
            macroWeight = 0.55f,
            useBroad = true,
            broadOctaves = 3,
            broadFrequency = 1f / 40f,
            broadLacunarity = 2f,
            broadGain = 0.48f,
            broadWeight = 0.28f,
            useDetail = true,
            detailOctaves = 3,
            detailFrequency = 1f / 10f,
            detailLacunarity = 2f,
            detailGain = 0.42f,
            detailWeight = 0.17f,
        };
    }
}

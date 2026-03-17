using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(
    fileName = "WorldGenSettings",
    menuName = "World/WorldGen/Settings")]
public sealed class WorldGenSettingsAsset : ScriptableObject
{
    [Header("Terrain Spline Trees")]
    [SerializeField] private SplineTreeAsset offsetSplineTree;
    [SerializeField] private SplineTreeAsset factorSplineTree;
    [SerializeField] private SplineTreeAsset jaggednessSplineTree;

    [Header("Water")]
    [SerializeField, Min(0)] private int seaLevel = WorldGenDensity.InternalSeaLevel;

    [Header("Value Remap")]
    [FormerlySerializedAs("useContinentalnessCdfRemap")]
    [SerializeField] private bool useContinentalnessRemap = true;
    [FormerlySerializedAs("useErosionCdfRemap")]
    [SerializeField] private bool useErosionRemap = true;
    [FormerlySerializedAs("useRidgesCdfRemap")]
    [SerializeField] private bool useRidgesRemap = true;

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

    [Header("Weirdness Warp")]
    [SerializeField] private bool ridgesUseWarp = true;
    [SerializeField, Min(1)] private int ridgesWarpOctaves = 2;
    [SerializeField, Min(0.000001f)] private float ridgesWarpFrequency = 1f / 16f;
    [SerializeField, Min(0f)] private float ridgesWarpAmplitude = 0.24f;
    [SerializeField, Min(1f)] private float ridgesWarpLacunarity = 2f;
    [SerializeField, Range(0f, 1f)] private float ridgesWarpGain = 0.5f;

    [Header("Weirdness Macro")]
    [SerializeField] private bool ridgesUseMacro = true;
    [SerializeField, Min(1)] private int ridgesMacroOctaves = 4;
    [SerializeField, Min(0.000001f)] private float ridgesMacroFrequency = 1f / 22f;
    [SerializeField, Min(1f)] private float ridgesMacroLacunarity = 2f;
    [SerializeField, Range(0f, 1f)] private float ridgesMacroGain = 0.5f;
    [SerializeField, Min(0f)] private float ridgesMacroWeight = 0.52f;

    [Header("Weirdness Broad")]
    [SerializeField] private bool ridgesUseBroad = true;
    [SerializeField, Min(1)] private int ridgesBroadOctaves = 3;
    [SerializeField, Min(0.000001f)] private float ridgesBroadFrequency = 1f / 38f;
    [SerializeField, Min(1f)] private float ridgesBroadLacunarity = 2f;
    [SerializeField, Range(0f, 1f)] private float ridgesBroadGain = 0.48f;
    [SerializeField, Min(0f)] private float ridgesBroadWeight = 0.3f;

    [Header("Weirdness Detail")]
    [SerializeField] private bool ridgesUseDetail = true;
    [SerializeField, Min(1)] private int ridgesDetailOctaves = 3;
    [SerializeField, Min(0.000001f)] private float ridgesDetailFrequency = 1f / 9f;
    [SerializeField, Min(1f)] private float ridgesDetailLacunarity = 2f;
    [SerializeField, Range(0f, 1f)] private float ridgesDetailGain = 0.45f;
    [SerializeField, Min(0f)] private float ridgesDetailWeight = 0.18f;

    [Header("Sea Bands")]
    [SerializeField, Range(-1f, 0f)] private float abyssUpperBound = -0.7f;
    [SerializeField] private Color abyssColor = new Color(0.03f, 0.07f, 0.18f, 1f);
    [SerializeField, Range(-1f, 0f)] private float deepOceanUpperBound = -0.4f;
    [SerializeField] private Color deepOceanColor = new Color(0.05f, 0.12f, 0.32f, 1f);
    [SerializeField, Range(-1f, 0f)] private float oceanUpperBound = -0.15f;
    [SerializeField] private Color oceanColor = new Color(0.08f, 0.25f, 0.52f, 1f);
    [SerializeField] private Color shallowOceanTransitionColor = new Color(0.9f, 0.84f, 0.64f, 1f);

    [Header("Land Bands")]
    [SerializeField, Range(0f, 1f)] private float coastUpperBound = 0.15f;
    [SerializeField] private Color coastColor = new Color(0.9f, 0.84f, 0.64f, 1f);
    [SerializeField, Range(0f, 1f)] private float inlandUpperBound = 0.4f;
    [SerializeField] private Color inlandColor = new Color(0.36f, 0.63f, 0.29f, 1f);
    [SerializeField, Range(0f, 1f)] private float deepInlandUpperBound = 0.7f;
    [SerializeField] private Color deepInlandColor = new Color(0.23f, 0.48f, 0.17f, 1f);
    [SerializeField] private Color continentalCoreColor = new Color(0.47f, 0.38f, 0.24f, 1f);

    public SplineTreeAsset OffsetSplineTree => offsetSplineTree;
    public SplineTreeAsset FactorSplineTree => factorSplineTree;
    public SplineTreeAsset JaggednessSplineTree => jaggednessSplineTree;
    public int SeaLevel => Mathf.Clamp(seaLevel, 0, TerrainData.WorldHeight - 1);
    public bool UseContinentalnessRemap => useContinentalnessRemap;
    public bool UseErosionRemap => useErosionRemap;
    public bool UseRidgesRemap => useRidgesRemap;

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
            shallowOceanTransitionColor = (Color32)shallowOceanTransitionColor,
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
            abyssUpperBound = -0.7f,
            deepOceanUpperBound = -0.4f,
            oceanUpperBound = -0.15f,
            coastUpperBound = 0.15f,
            inlandUpperBound = 0.4f,
            deepInlandUpperBound = 0.7f,
            abyssColor = new Color32(8, 18, 46, 255),
            deepOceanColor = new Color32(13, 31, 82, 255),
            oceanColor = new Color32(20, 64, 133, 255),
            shallowOceanTransitionColor = new Color32(230, 214, 163, 255),
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

    public TemperatureSettings ToTemperatureSettings()
    {
        return CreateDefaultTemperatureSettings();
    }

    public PrecipitationSettings ToPrecipitationSettings()
    {
        return CreateDefaultPrecipitationSettings();
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

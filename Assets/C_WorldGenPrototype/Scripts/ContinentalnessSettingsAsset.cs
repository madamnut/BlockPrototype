using UnityEngine;

[CreateAssetMenu(
    fileName = "ContinentalnessSettings",
    menuName = "World Gen Prototype/Continentalness Settings")]
public sealed class ContinentalnessSettingsAsset : ScriptableObject
{
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
}

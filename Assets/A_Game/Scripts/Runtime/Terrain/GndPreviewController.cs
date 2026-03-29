using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GndPreviewController : MonoBehaviour
{
    private enum PreviewMode
    {
        None,
        Gnd,
        Relief,
        Vein,
        VeinFold,
        Temperature,
        Precipitation,
    }

    private const int PreviewWorldSpan = 4320;
    private const int PreviewSampleSpacing = 4;
    private const int PreviewResolution = PreviewWorldSpan / PreviewSampleSpacing;

    [SerializeField] private TMP_InputField seedInputField;
    [SerializeField] private Button generateButton;
    [SerializeField] private Button randomButton;
    [SerializeField] private Button gndButton;
    [SerializeField] private Button reliefButton;
    [SerializeField] private Button veinButton;
    [SerializeField] private Button veinFoldButton;
    [SerializeField] private Button temperatureButton;
    [FormerlySerializedAs("humidityButton")]
    [SerializeField] private Button precipitationButton;
    [SerializeField] private RawImage previewImage;
    [SerializeField] private TerraWorldGenSettingsAsset terraWorldGenSettings;
    [SerializeField] private GndSettingsAsset gndSettings;
    [SerializeField] private int defaultSeed = 24680;
    [SerializeField] private Color activeButtonColor = new(0.25f, 0.25f, 0.25f, 1f);
    [SerializeField] private Color inactiveButtonColor = Color.white;
    [SerializeField] private bool selectGndByDefault = true;
    [SerializeField] private Color abyssColor = new(0.04f, 0.1f, 0.31f, 1f);
    [SerializeField] private Color deepOceanColor = new(0.06f, 0.23f, 0.54f, 1f);
    [SerializeField] private Color oceanColor = new(0.13f, 0.48f, 0.78f, 1f);
    [SerializeField] private Color shallowOceanColor = new(0.53f, 0.84f, 1f, 1f);
    [SerializeField] private Color coastColor = new(0.85f, 0.77f, 0.56f, 1f);
    [SerializeField] private Color nearInlandColor = new(0.69f, 0.85f, 0.42f, 1f);
    [SerializeField] private Color inlandColor = new(0.33f, 0.62f, 0.29f, 1f);
    [SerializeField] private Color coreInlandColor = new(0.12f, 0.36f, 0.18f, 1f);
    [SerializeField] private Color temperatureColdColor = new(0.09f, 0.2f, 0.54f, 1f);
    [SerializeField] private Color temperatureCoolColor = new(0.37f, 0.72f, 0.95f, 1f);
    [SerializeField] private Color temperatureTemperateColor = new(0.96f, 0.93f, 0.61f, 1f);
    [SerializeField] private Color temperatureWarmColor = new(0.95f, 0.57f, 0.2f, 1f);
    [SerializeField] private Color temperatureHotColor = new(0.74f, 0.17f, 0.08f, 1f);
    [FormerlySerializedAs("humidityLowColor")]
    [SerializeField] private Color precipitationLowColor = Color.white;
    [FormerlySerializedAs("humidityHighColor")]
    [SerializeField] private Color precipitationHighColor = new(0.12f, 0.38f, 0.92f, 1f);

    private Texture2D _previewTexture;
    private Color32[] _pixels;
    private PreviewMode _selectedMode;

    private void Awake()
    {
        if (!ValidateReferences())
        {
            enabled = false;
            return;
        }

        generateButton.onClick.AddListener(GenerateFromInput);
        randomButton.onClick.AddListener(GenerateRandom);
        gndButton.onClick.AddListener(SelectGndPreview);
        reliefButton.onClick.AddListener(SelectReliefPreview);
        veinButton.onClick.AddListener(SelectVeinPreview);
        veinFoldButton.onClick.AddListener(SelectVeinFoldPreview);
        temperatureButton.onClick.AddListener(SelectTemperaturePreview);
        precipitationButton.onClick.AddListener(SelectPrecipitationPreview);

        if (string.IsNullOrWhiteSpace(seedInputField.text))
        {
            seedInputField.text = defaultSeed.ToString();
        }

        _selectedMode = selectGndByDefault ? PreviewMode.Gnd : PreviewMode.None;
        UpdateButtonVisuals();
    }

    private void OnDestroy()
    {
        generateButton?.onClick.RemoveListener(GenerateFromInput);
        randomButton?.onClick.RemoveListener(GenerateRandom);
        gndButton?.onClick.RemoveListener(SelectGndPreview);
        reliefButton?.onClick.RemoveListener(SelectReliefPreview);
        veinButton?.onClick.RemoveListener(SelectVeinPreview);
        veinFoldButton?.onClick.RemoveListener(SelectVeinFoldPreview);
        temperatureButton?.onClick.RemoveListener(SelectTemperaturePreview);
        precipitationButton?.onClick.RemoveListener(SelectPrecipitationPreview);

        if (_previewTexture != null)
        {
            Destroy(_previewTexture);
            _previewTexture = null;
        }
    }

    private bool ValidateReferences()
    {
        if (seedInputField != null &&
            generateButton != null &&
            randomButton != null &&
            gndButton != null &&
            reliefButton != null &&
            veinButton != null &&
            veinFoldButton != null &&
            temperatureButton != null &&
            precipitationButton != null &&
            previewImage != null)
        {
            return true;
        }

        Debug.LogWarning("GndPreviewController is missing one or more scene references.", this);
        return false;
    }

    private void GenerateFromInput()
    {
        if (_selectedMode == PreviewMode.None)
        {
            Debug.LogWarning("Select a preview mode before generating.", this);
            return;
        }

        int seed = ParseSeedFromInput();
        seedInputField.text = seed.ToString();

        switch (_selectedMode)
        {
            case PreviewMode.Gnd:
                RenderGndPreview(seed);
                break;
            case PreviewMode.Relief:
                RenderReliefPreview(seed);
                break;
            case PreviewMode.Vein:
                RenderVeinPreview(seed);
                break;
            case PreviewMode.VeinFold:
                RenderVeinFoldPreview(seed);
                break;
            case PreviewMode.Temperature:
                RenderTemperaturePreview(seed);
                break;
            case PreviewMode.Precipitation:
                RenderPrecipitationPreview(seed);
                break;
        }
    }

    private void GenerateRandom()
    {
        int seed = new System.Random(unchecked(Environment.TickCount ^ GetInstanceID())).Next(int.MinValue, int.MaxValue);
        seedInputField.text = seed.ToString();
    }

    private void SelectGndPreview()
    {
        _selectedMode = _selectedMode == PreviewMode.Gnd ? PreviewMode.None : PreviewMode.Gnd;
        UpdateButtonVisuals();
    }

    private void SelectReliefPreview()
    {
        _selectedMode = _selectedMode == PreviewMode.Relief ? PreviewMode.None : PreviewMode.Relief;
        UpdateButtonVisuals();
    }

    private void SelectVeinPreview()
    {
        _selectedMode = _selectedMode == PreviewMode.Vein ? PreviewMode.None : PreviewMode.Vein;
        UpdateButtonVisuals();
    }

    private void SelectVeinFoldPreview()
    {
        _selectedMode = _selectedMode == PreviewMode.VeinFold ? PreviewMode.None : PreviewMode.VeinFold;
        UpdateButtonVisuals();
    }

    private void SelectTemperaturePreview()
    {
        _selectedMode = _selectedMode == PreviewMode.Temperature ? PreviewMode.None : PreviewMode.Temperature;
        UpdateButtonVisuals();
    }

    private void SelectPrecipitationPreview()
    {
        _selectedMode = _selectedMode == PreviewMode.Precipitation ? PreviewMode.None : PreviewMode.Precipitation;
        UpdateButtonVisuals();
    }

    private int ParseSeedFromInput()
    {
        return int.TryParse(seedInputField.text, out int parsedSeed) ? parsedSeed : defaultSeed;
    }

    private void RenderGndPreview(int seed)
    {
        EnsurePreviewTexture();

        GndRuntimeSettings runtimeSettings = BuildGndRuntimeSettings();
        int worldOrigin = -(PreviewWorldSpan / 2);
        float minGnd = float.PositiveInfinity;
        float maxGnd = float.NegativeInfinity;
        double sumGnd = 0d;
        int sampleCount = PreviewResolution * PreviewResolution;

        using (GndSampler sampler = new(runtimeSettings, seed))
        {
            for (int pixelY = 0; pixelY < PreviewResolution; pixelY++)
            {
                int worldZ = worldOrigin + (pixelY * PreviewSampleSpacing);
                int rowOffset = (PreviewResolution - 1 - pixelY) * PreviewResolution;
                for (int pixelX = 0; pixelX < PreviewResolution; pixelX++)
                {
                    int worldX = worldOrigin + (pixelX * PreviewSampleSpacing);
                    float gnd = sampler.SampleGnd(worldX, worldZ);
                    minGnd = Mathf.Min(minGnd, gnd);
                    maxGnd = Mathf.Max(maxGnd, gnd);
                    sumGnd += gnd;
                    _pixels[rowOffset + pixelX] = GetBandColor(gnd);
                }
            }
        }

        _previewTexture.SetPixels32(_pixels);
        _previewTexture.Apply(false, false);
        previewImage.texture = _previewTexture;

        float averageGnd = sampleCount > 0 ? (float)(sumGnd / sampleCount) : 0f;
        Debug.Log($"Gnd preview stats | seed={seed} | min={minGnd:F6} | max={maxGnd:F6} | avg={averageGnd:F6}", this);
    }

    private void RenderReliefPreview(int seed)
    {
        ReliefRuntimeSettings runtimeSettings = BuildReliefRuntimeSettings();
        using ReliefSampler sampler = new(runtimeSettings, seed);
        RenderLocalGrayscalePreview("Relief", seed, sampler.SampleRelief);
    }

    private void RenderVeinPreview(int seed)
    {
        VeinRuntimeSettings runtimeSettings = BuildVeinRuntimeSettings();
        using VeinSampler sampler = new(runtimeSettings, seed);
        RenderLocalGrayscalePreview("Vein", seed, sampler.SampleVein);
    }

    private void RenderVeinFoldPreview(int seed)
    {
        VeinRuntimeSettings runtimeSettings = BuildVeinRuntimeSettings();
        using VeinSampler sampler = new(runtimeSettings, seed);
        RenderLocalGrayscalePreview("VeinFold", seed, sampler.SampleVeinFold);
    }

    private void RenderTemperaturePreview(int seed)
    {
        EnsurePreviewTexture();

        TemperatureRuntimeSettings runtimeSettings = BuildTemperatureRuntimeSettings();
        int worldSize = runtimeSettings.worldSizeXZ;
        float minTemperature = float.PositiveInfinity;
        float maxTemperature = float.NegativeInfinity;
        double sumTemperature = 0d;
        int sampleCount = PreviewResolution * PreviewResolution;

        using (TemperatureSampler sampler = new(runtimeSettings, seed))
        {
            for (int pixelY = 0; pixelY < PreviewResolution; pixelY++)
            {
                int worldZ = SampleWorldCoordinateAcrossFullSpan(pixelY, worldSize);
                int rowOffset = (PreviewResolution - 1 - pixelY) * PreviewResolution;
                for (int pixelX = 0; pixelX < PreviewResolution; pixelX++)
                {
                    int worldX = SampleWorldCoordinateAcrossFullSpan(pixelX, worldSize);
                    float temperature = sampler.SampleTemperature(worldX, worldZ);
                    minTemperature = Mathf.Min(minTemperature, temperature);
                    maxTemperature = Mathf.Max(maxTemperature, temperature);
                    sumTemperature += temperature;
                    _pixels[rowOffset + pixelX] = GetTemperatureColor(temperature);
                }
            }
        }

        _previewTexture.SetPixels32(_pixels);
        _previewTexture.Apply(false, false);
        previewImage.texture = _previewTexture;

        float averageTemperature = sampleCount > 0 ? (float)(sumTemperature / sampleCount) : 0f;
        Debug.Log($"Temperature preview stats | seed={seed} | min={minTemperature:F6} | max={maxTemperature:F6} | avg={averageTemperature:F6}", this);
    }

    private void RenderPrecipitationPreview(int seed)
    {
        EnsurePreviewTexture();

        PrecipitationRuntimeSettings runtimeSettings = BuildPrecipitationRuntimeSettings();
        int worldOrigin = -(PreviewWorldSpan / 2);
        float minPrecipitation = float.PositiveInfinity;
        float maxPrecipitation = float.NegativeInfinity;
        double sumPrecipitation = 0d;
        int sampleCount = PreviewResolution * PreviewResolution;

        using (PrecipitationSampler sampler = new(runtimeSettings, seed))
        {
            for (int pixelY = 0; pixelY < PreviewResolution; pixelY++)
            {
                int worldZ = worldOrigin + (pixelY * PreviewSampleSpacing);
                int rowOffset = (PreviewResolution - 1 - pixelY) * PreviewResolution;
                for (int pixelX = 0; pixelX < PreviewResolution; pixelX++)
                {
                    int worldX = worldOrigin + (pixelX * PreviewSampleSpacing);
                    float precipitation = sampler.SamplePrecipitation(worldX, worldZ);
                    minPrecipitation = Mathf.Min(minPrecipitation, precipitation);
                    maxPrecipitation = Mathf.Max(maxPrecipitation, precipitation);
                    sumPrecipitation += precipitation;
                    _pixels[rowOffset + pixelX] = GetPrecipitationColor(precipitation);
                }
            }
        }

        _previewTexture.SetPixels32(_pixels);
        _previewTexture.Apply(false, false);
        previewImage.texture = _previewTexture;

        float averagePrecipitation = sampleCount > 0 ? (float)(sumPrecipitation / sampleCount) : 0f;
        Debug.Log($"Precipitation preview stats | seed={seed} | min={minPrecipitation:F6} | max={maxPrecipitation:F6} | avg={averagePrecipitation:F6}", this);
    }

    private void RenderLocalGrayscalePreview(string label, int seed, Func<int, int, float> sampleFunc)
    {
        EnsurePreviewTexture();

        int worldOrigin = -(PreviewWorldSpan / 2);
        float minValue = float.PositiveInfinity;
        float maxValue = float.NegativeInfinity;
        double sumValue = 0d;
        int sampleCount = PreviewResolution * PreviewResolution;

        for (int pixelY = 0; pixelY < PreviewResolution; pixelY++)
        {
            int worldZ = worldOrigin + (pixelY * PreviewSampleSpacing);
            int rowOffset = (PreviewResolution - 1 - pixelY) * PreviewResolution;
            for (int pixelX = 0; pixelX < PreviewResolution; pixelX++)
            {
                int worldX = worldOrigin + (pixelX * PreviewSampleSpacing);
                float sample = sampleFunc(worldX, worldZ);
                minValue = Mathf.Min(minValue, sample);
                maxValue = Mathf.Max(maxValue, sample);
                sumValue += sample;
                _pixels[rowOffset + pixelX] = GetGrayscaleColor(sample);
            }
        }

        _previewTexture.SetPixels32(_pixels);
        _previewTexture.Apply(false, false);
        previewImage.texture = _previewTexture;

        float averageValue = sampleCount > 0 ? (float)(sumValue / sampleCount) : 0f;
        Debug.Log($"{label} preview stats | seed={seed} | min={minValue:F6} | max={maxValue:F6} | avg={averageValue:F6}", this);
    }

    private void EnsurePreviewTexture()
    {
        if (_previewTexture != null && _previewTexture.width == PreviewResolution && _previewTexture.height == PreviewResolution)
        {
            return;
        }

        if (_previewTexture != null)
        {
            Destroy(_previewTexture);
        }

        _previewTexture = new Texture2D(PreviewResolution, PreviewResolution, TextureFormat.RGBA32, false)
        {
            name = "GndPreviewTexture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        _pixels = new Color32[PreviewResolution * PreviewResolution];
        previewImage.texture = _previewTexture;
    }

    private void UpdateButtonVisuals()
    {
        UpdateButtonGraphic(gndButton, _selectedMode == PreviewMode.Gnd);
        UpdateButtonGraphic(reliefButton, _selectedMode == PreviewMode.Relief);
        UpdateButtonGraphic(veinButton, _selectedMode == PreviewMode.Vein);
        UpdateButtonGraphic(veinFoldButton, _selectedMode == PreviewMode.VeinFold);
        UpdateButtonGraphic(temperatureButton, _selectedMode == PreviewMode.Temperature);
        UpdateButtonGraphic(precipitationButton, _selectedMode == PreviewMode.Precipitation);
    }

    private void UpdateButtonGraphic(Button button, bool active)
    {
        if (button != null && button.targetGraphic is Graphic graphic)
        {
            graphic.color = active ? activeButtonColor : inactiveButtonColor;
        }
    }

    private Color32 GetBandColor(float gnd)
    {
        if (gnd < -0.9f)
        {
            return abyssColor;
        }

        if (gnd < -0.5f)
        {
            return deepOceanColor;
        }

        if (gnd < -0.1f)
        {
            return oceanColor;
        }

        if (gnd < 0f)
        {
            return shallowOceanColor;
        }

        if (gnd < 0.1f)
        {
            return coastColor;
        }

        if (gnd < 0.5f)
        {
            return nearInlandColor;
        }

        if (gnd < 0.9f)
        {
            return inlandColor;
        }

        return coreInlandColor;
    }

    private Color32 GetTemperatureColor(float temperature)
    {
        float normalized = Mathf.InverseLerp(-1f, 1f, temperature);
        if (normalized < 0.25f)
        {
            return Color.Lerp(temperatureColdColor, temperatureCoolColor, normalized / 0.25f);
        }

        if (normalized < 0.5f)
        {
            return Color.Lerp(temperatureCoolColor, temperatureTemperateColor, (normalized - 0.25f) / 0.25f);
        }

        if (normalized < 0.75f)
        {
            return Color.Lerp(temperatureTemperateColor, temperatureWarmColor, (normalized - 0.5f) / 0.25f);
        }

        return Color.Lerp(temperatureWarmColor, temperatureHotColor, (normalized - 0.75f) / 0.25f);
    }

    private Color32 GetPrecipitationColor(float precipitation)
    {
        return Color.Lerp(precipitationLowColor, precipitationHighColor, Mathf.InverseLerp(-1f, 1f, precipitation));
    }

    private static Color32 GetGrayscaleColor(float value)
    {
        float intensity = 1f - Mathf.InverseLerp(-1f, 1f, value);
        return Color.Lerp(Color.black, Color.white, intensity);
    }

    private int SampleWorldCoordinateAcrossFullSpan(int pixelIndex, int worldSize)
    {
        if (PreviewResolution <= 1 || worldSize <= 1)
        {
            return 0;
        }

        float t = pixelIndex / (float)(PreviewResolution - 1);
        return Mathf.RoundToInt(t * (worldSize - 1));
    }

    private GndRuntimeSettings BuildGndRuntimeSettings()
    {
        if (terraWorldGenSettings != null)
        {
            return terraWorldGenSettings.BuildGndRuntimeSettings();
        }

        return gndSettings != null
            ? gndSettings.BuildRuntimeSettings()
            : GndRuntimeSettings.CreateDefault();
    }

    private ReliefRuntimeSettings BuildReliefRuntimeSettings()
    {
        return terraWorldGenSettings != null
            ? terraWorldGenSettings.BuildReliefRuntimeSettings()
            : ReliefRuntimeSettings.CreateDefault();
    }

    private VeinRuntimeSettings BuildVeinRuntimeSettings()
    {
        return terraWorldGenSettings != null
            ? terraWorldGenSettings.BuildVeinRuntimeSettings()
            : VeinRuntimeSettings.CreateDefault();
    }

    private TemperatureRuntimeSettings BuildTemperatureRuntimeSettings()
    {
        return terraWorldGenSettings != null
            ? terraWorldGenSettings.BuildTemperatureRuntimeSettings()
            : TemperatureRuntimeSettings.CreateDefault();
    }

    private PrecipitationRuntimeSettings BuildPrecipitationRuntimeSettings()
    {
        return terraWorldGenSettings != null
            ? terraWorldGenSettings.BuildPrecipitationRuntimeSettings()
            : PrecipitationRuntimeSettings.CreateDefault();
    }
}

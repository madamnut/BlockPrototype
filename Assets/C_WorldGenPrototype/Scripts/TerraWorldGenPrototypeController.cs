using System;
using System.Threading.Tasks;
using Stopwatch = System.Diagnostics.Stopwatch;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TerraWorldGenPrototypeController : MonoBehaviour
{
    private enum PreviewMode : byte
    {
        Continentalness = 0,
        Erosion = 1,
        Weirdness = 2,
        PeaksAndValleys = 3,
        Jagged = 4,
        Terrain3D = 5,
        Temperature = 6,
        Precipitation = 7,
    }

    private readonly struct PreviewStats
    {
        public PreviewStats(float min, float max, float average)
        {
            Min = min;
            Max = max;
            Average = average;
        }

        public float Min { get; }
        public float Max { get; }
        public float Average { get; }
    }

    private readonly struct PreviewSamplers
    {
        public PreviewSamplers(
            int seed,
            PrototypeSimplexNoiseSettingsAsset continentalnessSettings,
            PrototypeSimplexNoiseSettingsAsset erosionSettings,
            PrototypeSimplexNoiseSettingsAsset weirdnessSettings,
            PrototypeSimplexNoiseSettingsAsset jaggedSettings,
            PrototypeSimplexNoise3DSettingsAsset terrain3DSettings,
            PrototypeTemperatureSettingsAsset temperatureSettings,
            PrototypeSimplexNoiseSettingsAsset precipitationSettings)
        {
            Continentalness = new PrototypeSimplexNoiseSampler(seed, continentalnessSettings);
            Erosion = new PrototypeSimplexNoiseSampler(seed, erosionSettings);
            Weirdness = new PrototypeSimplexNoiseSampler(seed, weirdnessSettings);
            Jagged = new PrototypeSimplexNoiseSampler(seed, jaggedSettings);
            Terrain3D = new PrototypeSimplexNoise3DSampler(seed, terrain3DSettings);
            Temperature = new PrototypeTemperatureSampler(seed, temperatureSettings);
            Precipitation = new PrototypeSimplexNoiseSampler(seed, precipitationSettings);
        }

        public PrototypeSimplexNoiseSampler Continentalness { get; }
        public PrototypeSimplexNoiseSampler Erosion { get; }
        public PrototypeSimplexNoiseSampler Weirdness { get; }
        public PrototypeSimplexNoiseSampler Jagged { get; }
        public PrototypeSimplexNoise3DSampler Terrain3D { get; }
        public PrototypeTemperatureSampler Temperature { get; }
        public PrototypeSimplexNoiseSampler Precipitation { get; }
    }

    private static readonly Color32 AbyssColor = new(8, 24, 56, 255);
    private static readonly Color32 DeepOceanColor = new(18, 58, 110, 255);
    private static readonly Color32 OceanColor = new(34, 96, 156, 255);
    private static readonly Color32 CoastColor = new(223, 203, 150, 255);
    private static readonly Color32 NearInlandColor = new(102, 168, 92, 255);
    private static readonly Color32 MidInlandColor = new(66, 132, 74, 255);
    private static readonly Color32 CoreInlandColor = new(136, 110, 78, 255);
    private static readonly Color32 ErosionLowColor = new(74, 45, 35, 255);
    private static readonly Color32 ErosionMidColor = new(172, 154, 118, 255);
    private static readonly Color32 ErosionHighColor = new(218, 228, 206, 255);
    private static readonly Color32 WeirdNegativeColor = new(42, 84, 162, 255);
    private static readonly Color32 WeirdNeutralColor = new(186, 186, 186, 255);
    private static readonly Color32 WeirdPositiveColor = new(208, 116, 56, 255);
    private static readonly Color32 PVValleysColor = new(34, 62, 110, 255);
    private static readonly Color32 PVLowColor = new(69, 110, 168, 255);
    private static readonly Color32 PVMidColor = new(170, 170, 170, 255);
    private static readonly Color32 PVHighColor = new(190, 154, 98, 255);
    private static readonly Color32 PVPeaksColor = new(248, 240, 224, 255);
    private static readonly Color32 TemperaturePolarColor = new(70, 108, 180, 255);
    private static readonly Color32 TemperatureCoolColor = new(130, 196, 234, 255);
    private static readonly Color32 TemperatureMildColor = new(244, 220, 156, 255);
    private static readonly Color32 TemperatureWarmColor = new(232, 146, 72, 255);
    private static readonly Color32 TemperatureHotColor = new(205, 68, 44, 255);
    private static readonly Color32 PrecipitationDryColor = new(152, 122, 86, 255);
    private static readonly Color32 PrecipitationLowColor = new(210, 196, 138, 255);
    private static readonly Color32 PrecipitationMidColor = new(128, 184, 116, 255);
    private static readonly Color32 PrecipitationHighColor = new(58, 142, 116, 255);
    private static readonly Color32 PrecipitationWetColor = new(32, 88, 138, 255);
    private static readonly Color32 GridLineColor = new(255, 0, 0, 128);
    private static readonly Color32 GridClearColor = new(0, 0, 0, 0);

    private const int DefaultTextureResolution = 4096;
    private const int TemperatureTextureResolution = 1080;
    private const int GridCellSize = 16;
    private const int GridLineThickness = 8;
    private const int TerrainPreviewMinecraftY = 64;

    [Header("Simplex Test")]
    [SerializeField] private PrototypeSimplexNoiseSettingsAsset continentalnessSimplexSettings;
    [SerializeField] private PrototypeSimplexNoiseSettingsAsset erosionSimplexSettings;
    [SerializeField] private PrototypeSimplexNoiseSettingsAsset weirdnessSimplexSettings;
    [SerializeField] private PrototypeSimplexNoiseSettingsAsset jaggedSimplexSettings;
    [SerializeField] private PrototypeSimplexNoise3DSettingsAsset terrain3DSimplexSettings;
    [SerializeField] private PrototypeTemperatureSettingsAsset temperatureSettings;
    [SerializeField] private PrototypeSimplexNoiseSettingsAsset precipitationSimplexSettings;

    [Header("UI")]
    [SerializeField] private TMP_InputField seedInput;
    [SerializeField] private Button generateButton;
    [SerializeField] private Button randomGenButton;
    [SerializeField] private Button continentalnessSimplexButton;
    [SerializeField] private Button erosionSimplexButton;
    [SerializeField] private Button weirdnessSimplexButton;
    [SerializeField] private Button peaksAndValleysSimplexButton;
    [SerializeField] private Button jaggedSimplexButton;
    [SerializeField] private Button terrain3DSimplexButton;
    [SerializeField] private Button temperatureButton;
    [SerializeField] private Button precipitationButton;
    [SerializeField] private Button gridButton;
    [SerializeField] private RawImage outputImage;
    [SerializeField] private RawImage gridOverlayImage;

    private Texture2D _previewTexture;
    private Texture2D _gridTexture;
    private Color32[] _previewPixels;
    private float[] _previewSamples;
    private bool _gridVisible = true;
    private PreviewMode _selectedMode = PreviewMode.Continentalness;

    private void Awake()
    {
        ResolveOptionalReferences();
        if (!ValidateReferences())
        {
            enabled = false;
            return;
        }

        generateButton.onClick.AddListener(GeneratePreview);
        randomGenButton.onClick.AddListener(FillRandomSeed);
        continentalnessSimplexButton.onClick.AddListener(SelectContinentalnessMode);
        erosionSimplexButton.onClick.AddListener(SelectErosionMode);
        weirdnessSimplexButton.onClick.AddListener(SelectWeirdnessMode);
        peaksAndValleysSimplexButton.onClick.AddListener(SelectPeaksAndValleysMode);
        jaggedSimplexButton.onClick.AddListener(SelectJaggedMode);
        terrain3DSimplexButton.onClick.AddListener(SelectTerrain3DMode);
        temperatureButton.onClick.AddListener(SelectTemperatureMode);
        precipitationButton.onClick.AddListener(SelectPrecipitationMode);
        gridButton.onClick.AddListener(ToggleGrid);

        EnsurePreviewTextures(GetPreviewResolution(_selectedMode));
        ApplyGridVisibility();
        ApplyModeVisuals();
    }

    private void OnDestroy()
    {
        generateButton?.onClick.RemoveListener(GeneratePreview);
        randomGenButton?.onClick.RemoveListener(FillRandomSeed);
        continentalnessSimplexButton?.onClick.RemoveListener(SelectContinentalnessMode);
        erosionSimplexButton?.onClick.RemoveListener(SelectErosionMode);
        weirdnessSimplexButton?.onClick.RemoveListener(SelectWeirdnessMode);
        peaksAndValleysSimplexButton?.onClick.RemoveListener(SelectPeaksAndValleysMode);
        jaggedSimplexButton?.onClick.RemoveListener(SelectJaggedMode);
        terrain3DSimplexButton?.onClick.RemoveListener(SelectTerrain3DMode);
        temperatureButton?.onClick.RemoveListener(SelectTemperatureMode);
        precipitationButton?.onClick.RemoveListener(SelectPrecipitationMode);
        gridButton?.onClick.RemoveListener(ToggleGrid);

        if (_previewTexture != null)
        {
            Destroy(_previewTexture);
        }

        if (_gridTexture != null)
        {
            Destroy(_gridTexture);
        }
    }

    private bool ValidateReferences()
    {
        bool isValid = true;

        if (continentalnessSimplexSettings == null ||
            erosionSimplexSettings == null ||
            weirdnessSimplexSettings == null ||
            jaggedSimplexSettings == null ||
            terrain3DSimplexSettings == null ||
            temperatureSettings == null ||
            precipitationSimplexSettings == null)
        {
            Debug.LogError("TerraWorldGenPrototypeController requires settings assets for all preview modes.", this);
            isValid = false;
        }

        if (seedInput == null)
        {
            Debug.LogError("TerraWorldGenPrototypeController requires a seed TMP_InputField.", this);
            isValid = false;
        }

        if (generateButton == null ||
            randomGenButton == null ||
            continentalnessSimplexButton == null ||
            erosionSimplexButton == null ||
            weirdnessSimplexButton == null ||
            peaksAndValleysSimplexButton == null ||
            jaggedSimplexButton == null ||
            terrain3DSimplexButton == null ||
            temperatureButton == null ||
            precipitationButton == null ||
            gridButton == null)
        {
            Debug.LogError("TerraWorldGenPrototypeController is missing one or more button references.", this);
            isValid = false;
        }

        if (outputImage == null || gridOverlayImage == null)
        {
            Debug.LogError("TerraWorldGenPrototypeController requires output and grid RawImage references.", this);
            isValid = false;
        }

        return isValid;
    }

    private void ResolveOptionalReferences()
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            string buttonName = button.gameObject.name?.Trim();
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            string buttonLabel = label != null ? label.text?.Trim() : string.Empty;

            if (continentalnessSimplexButton == null &&
                (string.Equals(buttonLabel, "ContSimp", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(buttonName, "ContSimp", StringComparison.OrdinalIgnoreCase)))
            {
                continentalnessSimplexButton = button;
                continue;
            }

            if (erosionSimplexButton == null &&
                (string.Equals(buttonLabel, "ErosionSimp", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(buttonName, "ErosionSimp", StringComparison.OrdinalIgnoreCase)))
            {
                erosionSimplexButton = button;
                continue;
            }

            if (weirdnessSimplexButton == null &&
                (string.Equals(buttonLabel, "WeirdnessSimp", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(buttonName, "WeirdnessSimp", StringComparison.OrdinalIgnoreCase)))
            {
                weirdnessSimplexButton = button;
                continue;
            }

            if (peaksAndValleysSimplexButton == null &&
                (string.Equals(buttonLabel, "PVSimp", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(buttonName, "PVSimp", StringComparison.OrdinalIgnoreCase)))
            {
                peaksAndValleysSimplexButton = button;
                continue;
            }

            if (jaggedSimplexButton == null &&
                (string.Equals(buttonLabel, "JaggedSimp", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(buttonName, "JaggedSimp", StringComparison.OrdinalIgnoreCase)))
            {
                jaggedSimplexButton = button;
                continue;
            }

            if (terrain3DSimplexButton == null &&
                (string.Equals(buttonLabel, "Terrain3DSimp", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(buttonLabel, "3DSimp", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(buttonName, "Terrain3DSimp", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(buttonName, "3DSimp", StringComparison.OrdinalIgnoreCase)))
            {
                terrain3DSimplexButton = button;
                continue;
            }

            if (temperatureButton == null &&
                (string.Equals(buttonLabel, "Temperature", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(buttonName, "Temperature", StringComparison.OrdinalIgnoreCase)))
            {
                temperatureButton = button;
                continue;
            }

            if (precipitationButton == null &&
                (string.Equals(buttonLabel, "Precipitation", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(buttonLabel, "PrecipitationSimp", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(buttonName, "Precipitation", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(buttonName, "PrecipitationSimp", StringComparison.OrdinalIgnoreCase)))
            {
                precipitationButton = button;
                continue;
            }
        }
    }

    private void GeneratePreview()
    {
        if (!TryResolveSeed(out int seed))
        {
            return;
        }

        int resolution = GetPreviewResolution(_selectedMode);
        EnsurePreviewTextures(resolution);

        PreviewSamplers samplers = new(
            seed,
            continentalnessSimplexSettings,
            erosionSimplexSettings,
            weirdnessSimplexSettings,
            jaggedSimplexSettings,
            terrain3DSimplexSettings,
            temperatureSettings,
            precipitationSimplexSettings);

        Stopwatch stopwatch = Stopwatch.StartNew();
        Parallel.For(0, resolution, pixelZ =>
        {
            int rowStart = pixelZ * resolution;
            for (int pixelX = 0; pixelX < resolution; pixelX++)
            {
                MapPreviewWorldCoordinates(_selectedMode, pixelX, pixelZ, resolution, out int worldX, out int worldZ);
                int index = rowStart + pixelX;
                float sample = EvaluatePreviewSample(_selectedMode, samplers, worldX, worldZ);
                _previewSamples[index] = sample;
                _previewPixels[index] = EvaluatePreviewColor(_selectedMode, sample);
            }
        });

        PreviewStats stats = ComputePreviewStats(_previewSamples);
        _previewTexture.SetPixelData(_previewPixels, 0);
        _previewTexture.Apply(false, false);
        stopwatch.Stop();

        outputImage.texture = _previewTexture;
        Debug.Log(
            $"[TerraWorldGenPrototype] Mode={GetPreviewModeLabel(_selectedMode)} Min={stats.Min:F5} Max={stats.Max:F5} Avg={stats.Average:F5} Time={stopwatch.Elapsed.TotalMilliseconds:F2} ms Samples={resolution}x{resolution}",
            this);
    }

    private void FillRandomSeed()
    {
        int seed = Guid.NewGuid().GetHashCode();
        seedInput.SetTextWithoutNotify(seed.ToString());
    }

    private void ToggleGrid()
    {
        _gridVisible = !_gridVisible;
        ApplyGridVisibility();
    }

    private void SelectContinentalnessMode()
    {
        _selectedMode = PreviewMode.Continentalness;
        ApplyModeVisuals();
    }

    private void SelectErosionMode()
    {
        _selectedMode = PreviewMode.Erosion;
        ApplyModeVisuals();
    }

    private void SelectWeirdnessMode()
    {
        _selectedMode = PreviewMode.Weirdness;
        ApplyModeVisuals();
    }

    private void SelectPeaksAndValleysMode()
    {
        _selectedMode = PreviewMode.PeaksAndValleys;
        ApplyModeVisuals();
    }

    private void SelectJaggedMode()
    {
        _selectedMode = PreviewMode.Jagged;
        ApplyModeVisuals();
    }

    private void SelectTerrain3DMode()
    {
        _selectedMode = PreviewMode.Terrain3D;
        ApplyModeVisuals();
    }

    private void SelectTemperatureMode()
    {
        _selectedMode = PreviewMode.Temperature;
        ApplyModeVisuals();
    }

    private void SelectPrecipitationMode()
    {
        _selectedMode = PreviewMode.Precipitation;
        ApplyModeVisuals();
    }

    private void ApplyGridVisibility()
    {
        if (gridOverlayImage != null)
        {
            gridOverlayImage.enabled = _gridVisible;
        }
    }

    private void EnsurePreviewTextures(int resolution)
    {
        if (_previewTexture == null || _previewTexture.width != resolution || _previewTexture.height != resolution)
        {
            if (_previewTexture != null)
            {
                Destroy(_previewTexture);
            }

            _previewTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true)
            {
                name = $"Terra Preview {resolution}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
        }

        if (_gridTexture == null || _gridTexture.width != resolution || _gridTexture.height != resolution)
        {
            RebuildGridOverlayTexture(resolution);
        }

        _previewPixels = new Color32[resolution * resolution];
        _previewSamples = new float[resolution * resolution];
        outputImage.texture = _previewTexture;
        gridOverlayImage.texture = _gridTexture;
    }

    private void RebuildGridOverlayTexture(int resolution)
    {
        if (_gridTexture != null)
        {
            Destroy(_gridTexture);
        }

        _gridTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true)
        {
            name = $"Terra Preview Grid Overlay {resolution}",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };

        Color32[] pixels = new Color32[resolution * resolution];
        int cellPixelSize = Mathf.Max(1, resolution / GridCellSize);

        for (int y = 0; y < resolution; y++)
        {
            bool horizontalLine = y > 0 && (y % cellPixelSize) < GridLineThickness;
            for (int x = 0; x < resolution; x++)
            {
                bool verticalLine = x > 0 && (x % cellPixelSize) < GridLineThickness;
                pixels[(y * resolution) + x] = (horizontalLine || verticalLine) ? GridLineColor : GridClearColor;
            }
        }

        _gridTexture.SetPixelData(pixels, 0);
        _gridTexture.Apply(false, true);
        gridOverlayImage.texture = _gridTexture;
    }

    private bool TryResolveSeed(out int seed)
    {
        string rawText = seedInput.text?.Trim();
        if (string.IsNullOrEmpty(rawText))
        {
            seed = Guid.NewGuid().GetHashCode();
            seedInput.SetTextWithoutNotify(seed.ToString());
            return true;
        }

        if (!int.TryParse(rawText, out seed))
        {
            Debug.LogError($"Invalid seed input: '{rawText}'.", this);
            return false;
        }

        return true;
    }

    private void ApplyModeVisuals()
    {
        ApplyButtonHighlight(continentalnessSimplexButton, _selectedMode == PreviewMode.Continentalness);
        ApplyButtonHighlight(erosionSimplexButton, _selectedMode == PreviewMode.Erosion);
        ApplyButtonHighlight(weirdnessSimplexButton, _selectedMode == PreviewMode.Weirdness);
        ApplyButtonHighlight(peaksAndValleysSimplexButton, _selectedMode == PreviewMode.PeaksAndValleys);
        ApplyButtonHighlight(jaggedSimplexButton, _selectedMode == PreviewMode.Jagged);
        ApplyButtonHighlight(terrain3DSimplexButton, _selectedMode == PreviewMode.Terrain3D);
        ApplyButtonHighlight(temperatureButton, _selectedMode == PreviewMode.Temperature);
        ApplyButtonHighlight(precipitationButton, _selectedMode == PreviewMode.Precipitation);
    }

    private static void ApplyButtonHighlight(Button button, bool selected)
    {
        if (button == null)
        {
            return;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = selected
            ? new Color(0.78f, 0.88f, 1f, 1f)
            : Color.white;
        colors.selectedColor = colors.normalColor;
        button.colors = colors;
    }

    private static float EvaluatePreviewSample(
        PreviewMode mode,
        in PreviewSamplers samplers,
        int worldX,
        int worldZ)
    {
        return mode switch
        {
            PreviewMode.Continentalness => samplers.Continentalness.Sample(worldX, worldZ),
            PreviewMode.Erosion => samplers.Erosion.Sample(worldX, worldZ),
            PreviewMode.Weirdness => samplers.Weirdness.Sample(worldX, worldZ),
            PreviewMode.PeaksAndValleys => PeaksAndValleys.Fold(samplers.Weirdness.Sample(worldX, worldZ)),
            PreviewMode.Jagged => samplers.Jagged.Sample(worldX, worldZ),
            PreviewMode.Terrain3D => samplers.Terrain3D.Sample(worldX, TerrainPreviewMinecraftY, worldZ),
            PreviewMode.Temperature => samplers.Temperature.Sample(worldX, worldZ),
            PreviewMode.Precipitation => samplers.Precipitation.Sample(worldX, worldZ),
            _ => 0f,
        };
    }

    private static Color32 EvaluatePreviewColor(PreviewMode mode, float sample)
    {
        return mode switch
        {
            PreviewMode.Continentalness => EvaluateContinentalnessColor(sample),
            PreviewMode.Erosion => EvaluateErosionColor(sample),
            PreviewMode.Weirdness => EvaluateWeirdnessColor(sample),
            PreviewMode.PeaksAndValleys => EvaluatePeaksAndValleysColor(sample),
            PreviewMode.Jagged or PreviewMode.Terrain3D => EvaluateSignedNoiseColor(sample),
            PreviewMode.Temperature => EvaluateTemperatureColor(sample),
            PreviewMode.Precipitation => EvaluatePrecipitationColor(sample),
            _ => GridClearColor,
        };
    }

    private static PreviewStats ComputePreviewStats(float[] samples)
    {
        float min = float.PositiveInfinity;
        float max = float.NegativeInfinity;
        double sum = 0d;

        for (int i = 0; i < samples.Length; i++)
        {
            float sample = samples[i];
            if (sample < min)
            {
                min = sample;
            }

            if (sample > max)
            {
                max = sample;
            }

            sum += sample;
        }

        return new PreviewStats(min, max, (float)(sum / samples.Length));
    }

    private static int GetPreviewResolution(PreviewMode mode)
    {
        return mode == PreviewMode.Temperature ? TemperatureTextureResolution : DefaultTextureResolution;
    }

    private static void MapPreviewWorldCoordinates(PreviewMode mode, int pixelX, int pixelZ, int resolution, out int worldX, out int worldZ)
    {
        if (mode == PreviewMode.Temperature)
        {
            worldX = MapPixelToWorldSpan(pixelX, resolution);
            worldZ = MapPixelToWorldSpan(pixelZ, resolution);
            return;
        }

        int halfExtent = resolution / 2;
        worldX = pixelX - halfExtent;
        worldZ = pixelZ - halfExtent;
    }

    private static int MapPixelToWorldSpan(int pixel, int resolution)
    {
        if (resolution <= 1)
        {
            return 0;
        }

        double t = pixel / (double)(resolution - 1);
        return Mathf.Clamp(
            Mathf.RoundToInt((float)(t * (PrototypeToroidalNoise.WorldSizeXZ - 1))),
            0,
            PrototypeToroidalNoise.WorldSizeXZ - 1);
    }

    private static string GetPreviewModeLabel(PreviewMode mode)
    {
        return mode switch
        {
            PreviewMode.Continentalness => "ContSimp",
            PreviewMode.Erosion => "ErosionSimp",
            PreviewMode.Weirdness => "WeirdnessSimp",
            PreviewMode.PeaksAndValleys => "PVSimp",
            PreviewMode.Jagged => "JaggedSimp",
            PreviewMode.Terrain3D => $"3D@Y{TerrainPreviewMinecraftY}",
            PreviewMode.Temperature => "Temperature",
            PreviewMode.Precipitation => "Precipitation",
            _ => "Unknown",
        };
    }

    private static Color32 EvaluateContinentalnessColor(float continentalness)
    {
        if (continentalness < -0.7f) return AbyssColor;
        if (continentalness < -0.4f) return DeepOceanColor;
        if (continentalness < -0.15f) return OceanColor;
        if (continentalness < 0.15f) return CoastColor;
        if (continentalness < 0.4f) return NearInlandColor;
        if (continentalness < 0.7f) return MidInlandColor;
        return CoreInlandColor;
    }

    private static Color32 EvaluateErosionColor(float erosion)
    {
        float t = Mathf.InverseLerp(-1f, 1f, erosion);
        if (t < 0.5f)
        {
            return Color32.Lerp(ErosionLowColor, ErosionMidColor, t / 0.5f);
        }

        return Color32.Lerp(ErosionMidColor, ErosionHighColor, (t - 0.5f) / 0.5f);
    }

    private static Color32 EvaluateWeirdnessColor(float weirdness)
    {
        float t = Mathf.InverseLerp(-1f, 1f, weirdness);
        if (t < 0.5f)
        {
            return Color32.Lerp(WeirdNegativeColor, WeirdNeutralColor, t / 0.5f);
        }

        return Color32.Lerp(WeirdNeutralColor, WeirdPositiveColor, (t - 0.5f) / 0.5f);
    }

    private static Color32 EvaluateSignedNoiseColor(float value)
    {
        float t = Mathf.InverseLerp(-1f, 1f, value);
        if (t < 0.5f)
        {
            return Color32.Lerp(WeirdNegativeColor, WeirdNeutralColor, t / 0.5f);
        }

        return Color32.Lerp(WeirdNeutralColor, WeirdPositiveColor, (t - 0.5f) / 0.5f);
    }

    private static Color32 EvaluatePeaksAndValleysColor(float peaksAndValleys)
    {
        if (peaksAndValleys < -0.85f) return PVValleysColor;
        if (peaksAndValleys < -0.2f) return PVLowColor;
        if (peaksAndValleys < 0.2f) return PVMidColor;
        if (peaksAndValleys < 0.7f) return PVHighColor;
        return PVPeaksColor;
    }

    private static Color32 EvaluateTemperatureColor(float temperature)
    {
        float t = Mathf.InverseLerp(-1f, 1f, temperature);
        if (t < 0.25f)
        {
            return Color32.Lerp(TemperaturePolarColor, TemperatureCoolColor, t / 0.25f);
        }

        if (t < 0.5f)
        {
            return Color32.Lerp(TemperatureCoolColor, TemperatureMildColor, (t - 0.25f) / 0.25f);
        }

        if (t < 0.75f)
        {
            return Color32.Lerp(TemperatureMildColor, TemperatureWarmColor, (t - 0.5f) / 0.25f);
        }

        return Color32.Lerp(TemperatureWarmColor, TemperatureHotColor, (t - 0.75f) / 0.25f);
    }

    private static Color32 EvaluatePrecipitationColor(float precipitation)
    {
        float t = Mathf.InverseLerp(-1f, 1f, precipitation);
        if (t < 0.25f)
        {
            return Color32.Lerp(PrecipitationDryColor, PrecipitationLowColor, t / 0.25f);
        }

        if (t < 0.5f)
        {
            return Color32.Lerp(PrecipitationLowColor, PrecipitationMidColor, (t - 0.25f) / 0.25f);
        }

        if (t < 0.75f)
        {
            return Color32.Lerp(PrecipitationMidColor, PrecipitationHighColor, (t - 0.5f) / 0.25f);
        }

        return Color32.Lerp(PrecipitationHighColor, PrecipitationWetColor, (t - 0.75f) / 0.25f);
    }
}

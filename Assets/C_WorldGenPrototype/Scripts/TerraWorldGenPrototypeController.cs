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
            PrototypeSimplexNoise3DSettingsAsset terrain3DSettings)
        {
            Continentalness = new PrototypeSimplexNoiseSampler(seed, continentalnessSettings);
            Erosion = new PrototypeSimplexNoiseSampler(seed, erosionSettings);
            Weirdness = new PrototypeSimplexNoiseSampler(seed, weirdnessSettings);
            Jagged = new PrototypeSimplexNoiseSampler(seed, jaggedSettings);
            Terrain3D = new PrototypeSimplexNoise3DSampler(seed, terrain3DSettings);
        }

        public PrototypeSimplexNoiseSampler Continentalness { get; }
        public PrototypeSimplexNoiseSampler Erosion { get; }
        public PrototypeSimplexNoiseSampler Weirdness { get; }
        public PrototypeSimplexNoiseSampler Jagged { get; }
        public PrototypeSimplexNoise3DSampler Terrain3D { get; }
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
    private static readonly Color32 GridLineColor = new(255, 0, 0, 128);
    private static readonly Color32 GridClearColor = new(0, 0, 0, 0);

    private const int TextureResolution = 4096;
    private const int GridCellSize = 16;
    private const int GridLineThickness = 8;
    private const int TerrainPreviewMinecraftY = 64;

    [Header("Simplex Test")]
    [SerializeField] private PrototypeSimplexNoiseSettingsAsset continentalnessSimplexSettings;
    [SerializeField] private PrototypeSimplexNoiseSettingsAsset erosionSimplexSettings;
    [SerializeField] private PrototypeSimplexNoiseSettingsAsset weirdnessSimplexSettings;
    [SerializeField] private PrototypeSimplexNoiseSettingsAsset jaggedSimplexSettings;
    [SerializeField] private PrototypeSimplexNoise3DSettingsAsset terrain3DSimplexSettings;

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
        gridButton.onClick.AddListener(ToggleGrid);

        EnsurePreviewTexture();
        EnsureGridOverlayTexture();
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
            terrain3DSimplexSettings == null)
        {
            Debug.LogError("TerraWorldGenPrototypeController requires simplex test settings assets for all simplex preview modes.", this);
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
        if (continentalnessSimplexButton != null &&
            erosionSimplexButton != null &&
            weirdnessSimplexButton != null &&
            peaksAndValleysSimplexButton != null &&
            jaggedSimplexButton != null &&
            terrain3DSimplexButton != null)
        {
            return;
        }

        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null)
            {
                continue;
            }

            string buttonLabel = label.text?.Trim();
            if (string.IsNullOrEmpty(buttonLabel))
            {
                continue;
            }

            if (continentalnessSimplexButton == null && string.Equals(buttonLabel, "ContSimp", StringComparison.OrdinalIgnoreCase))
            {
                continentalnessSimplexButton = button;
                continue;
            }

            if (erosionSimplexButton == null && string.Equals(buttonLabel, "ErosionSimp", StringComparison.OrdinalIgnoreCase))
            {
                erosionSimplexButton = button;
                continue;
            }

            if (weirdnessSimplexButton == null && string.Equals(buttonLabel, "WeirdnessSimp", StringComparison.OrdinalIgnoreCase))
            {
                weirdnessSimplexButton = button;
                continue;
            }

            if (peaksAndValleysSimplexButton == null && string.Equals(buttonLabel, "PVSimp", StringComparison.OrdinalIgnoreCase))
            {
                peaksAndValleysSimplexButton = button;
                continue;
            }

            if (jaggedSimplexButton == null && string.Equals(buttonLabel, "JaggedSimp", StringComparison.OrdinalIgnoreCase))
            {
                jaggedSimplexButton = button;
                continue;
            }

            if (terrain3DSimplexButton == null &&
                (string.Equals(buttonLabel, "Terrain3DSimp", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(buttonLabel, "3DSimp", StringComparison.OrdinalIgnoreCase)))
            {
                terrain3DSimplexButton = button;
            }
        }
    }

    private void GeneratePreview()
    {
        if (!TryResolveSeed(out int seed))
        {
            return;
        }

        EnsurePreviewTexture();

        PreviewSamplers samplers = new(
            seed,
            continentalnessSimplexSettings,
            erosionSimplexSettings,
            weirdnessSimplexSettings,
            jaggedSimplexSettings,
            terrain3DSimplexSettings);

        int halfExtent = TextureResolution / 2;
        Stopwatch stopwatch = Stopwatch.StartNew();
        Parallel.For(0, TextureResolution, z =>
        {
            int worldZ = z - halfExtent;
            int rowStart = z * TextureResolution;
            for (int x = 0; x < TextureResolution; x++)
            {
                int worldX = x - halfExtent;
                int index = rowStart + x;
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
            $"[TerraWorldGenPrototype] Mode={GetPreviewModeLabel(_selectedMode)} Min={stats.Min:F5} Max={stats.Max:F5} Avg={stats.Average:F5} Time={stopwatch.Elapsed.TotalMilliseconds:F2} ms Samples={TextureResolution}x{TextureResolution}",
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

    private void ApplyGridVisibility()
    {
        if (gridOverlayImage != null)
        {
            gridOverlayImage.enabled = _gridVisible;
        }
    }

    private void EnsurePreviewTexture()
    {
        if (_previewTexture == null)
        {
            _previewTexture = new Texture2D(TextureResolution, TextureResolution, TextureFormat.RGBA32, false, true)
            {
                name = "Terra Simplex Preview",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
        }

        _previewPixels ??= new Color32[TextureResolution * TextureResolution];
        _previewSamples ??= new float[TextureResolution * TextureResolution];
        outputImage.texture = _previewTexture;
    }

    private void EnsureGridOverlayTexture()
    {
        if (_gridTexture != null)
        {
            gridOverlayImage.texture = _gridTexture;
            return;
        }

        _gridTexture = new Texture2D(TextureResolution, TextureResolution, TextureFormat.RGBA32, false, true)
        {
            name = "Terra Preview Grid Overlay",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };

        Color32[] pixels = new Color32[TextureResolution * TextureResolution];
        int cellPixelSize = TextureResolution / GridCellSize;

        for (int y = 0; y < TextureResolution; y++)
        {
            bool horizontalLine = y > 0 && (y % cellPixelSize) < GridLineThickness;
            for (int x = 0; x < TextureResolution; x++)
            {
                bool verticalLine = x > 0 && (x % cellPixelSize) < GridLineThickness;
                pixels[(y * TextureResolution) + x] = (horizontalLine || verticalLine) ? GridLineColor : GridClearColor;
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
}

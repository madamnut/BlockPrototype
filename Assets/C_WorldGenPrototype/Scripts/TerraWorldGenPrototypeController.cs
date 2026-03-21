using System;
using System.Threading.Tasks;
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

    [Header("WorldGen")]
    [SerializeField] private WorldGenPackAsset terraWorldGenPack;

    [Header("UI")]
    [SerializeField] private TMP_InputField seedInput;
    [SerializeField] private Button generateButton;
    [SerializeField] private Button randomGenButton;
    [SerializeField] private Button rawContButton;
    [SerializeField] private Button erosionButton;
    [SerializeField] private Button weirdnessButton;
    [SerializeField] private Button peaksAndValleysButton;
    [SerializeField] private Button gridButton;
    [SerializeField] private RawImage outputImage;
    [SerializeField] private RawImage gridOverlayImage;

    private Texture2D _previewTexture;
    private Texture2D _gridTexture;
    private Color32[] _previewPixels;
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
        rawContButton.onClick.AddListener(SelectContinentalnessMode);
        erosionButton.onClick.AddListener(SelectErosionMode);
        weirdnessButton.onClick.AddListener(SelectWeirdnessMode);
        peaksAndValleysButton.onClick.AddListener(SelectPeaksAndValleysMode);
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
        rawContButton?.onClick.RemoveListener(SelectContinentalnessMode);
        erosionButton?.onClick.RemoveListener(SelectErosionMode);
        weirdnessButton?.onClick.RemoveListener(SelectWeirdnessMode);
        peaksAndValleysButton?.onClick.RemoveListener(SelectPeaksAndValleysMode);
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

        if (terraWorldGenPack == null)
        {
            Debug.LogError("TerraWorldGenPrototypeController requires a WorldGenPackAsset.", this);
            isValid = false;
        }
        else
        {
            if (terraWorldGenPack.ContinentalnessSettings == null)
            {
                Debug.LogError("TerraWorldGenPrototypeController WorldGenPack is missing continentalness settings.", this);
                isValid = false;
            }

            if (terraWorldGenPack.ErosionSettings == null)
            {
                Debug.LogError("TerraWorldGenPrototypeController WorldGenPack is missing erosion settings.", this);
                isValid = false;
            }

            if (terraWorldGenPack.WeirdnessSettings == null)
            {
                Debug.LogError("TerraWorldGenPrototypeController WorldGenPack is missing weirdness settings.", this);
                isValid = false;
            }
        }

        if (seedInput == null)
        {
            Debug.LogError("TerraWorldGenPrototypeController requires a seed TMP_InputField.", this);
            isValid = false;
        }

        if (generateButton == null || randomGenButton == null || rawContButton == null || erosionButton == null || weirdnessButton == null || peaksAndValleysButton == null || gridButton == null)
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
        if (erosionButton == null || weirdnessButton == null || peaksAndValleysButton == null)
        {
            foreach (Button button in GetComponentsInChildren<Button>(true))
            {
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                if (label == null)
                {
                    continue;
                }

                if (erosionButton == null && string.Equals(label.text, "Erosion", System.StringComparison.OrdinalIgnoreCase))
                {
                    erosionButton = button;
                    continue;
                }

                if (weirdnessButton == null && string.Equals(label.text, "Weirdness", System.StringComparison.OrdinalIgnoreCase))
                {
                    weirdnessButton = button;
                    continue;
                }

                if (peaksAndValleysButton == null && string.Equals(label.text, "PV", System.StringComparison.OrdinalIgnoreCase))
                {
                    peaksAndValleysButton = button;
                }
            }
        }
    }

    private void GeneratePreview()
    {
        if (!TryResolveSeed(out int seed))
        {
            return;
        }

        ContinentalnessSampler continentalnessSampler = new(seed, terraWorldGenPack.ContinentalnessSettings);
        ErosionSampler erosionSampler = new(seed, terraWorldGenPack.ErosionSettings);
        WeirdnessSampler weirdnessSampler = new(seed, terraWorldGenPack.WeirdnessSettings);
        EnsurePreviewTexture();

        int halfExtent = TextureResolution / 2;
        Parallel.For(0, TextureResolution, z =>
        {
            int worldZ = z - halfExtent;
            int rowStart = z * TextureResolution;
            for (int x = 0; x < TextureResolution; x++)
            {
                int worldX = x - halfExtent;
                _previewPixels[rowStart + x] = EvaluatePreviewColor(
                    _selectedMode,
                    continentalnessSampler,
                    erosionSampler,
                    weirdnessSampler,
                    worldX,
                    worldZ);
            }
        });

        _previewTexture.SetPixelData(_previewPixels, 0);
        _previewTexture.Apply(false, false);
        outputImage.texture = _previewTexture;
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
                name = "Terra Raw Cont Preview",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
        }

        _previewPixels ??= new Color32[TextureResolution * TextureResolution];
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
        ApplyButtonHighlight(rawContButton, _selectedMode == PreviewMode.Continentalness);
        ApplyButtonHighlight(erosionButton, _selectedMode == PreviewMode.Erosion);
        ApplyButtonHighlight(weirdnessButton, _selectedMode == PreviewMode.Weirdness);
        ApplyButtonHighlight(peaksAndValleysButton, _selectedMode == PreviewMode.PeaksAndValleys);
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

    private static Color32 EvaluatePreviewColor(
        PreviewMode mode,
        ContinentalnessSampler continentalnessSampler,
        ErosionSampler erosionSampler,
        WeirdnessSampler weirdnessSampler,
        int worldX,
        int worldZ)
    {
        return mode switch
        {
            PreviewMode.Continentalness => EvaluateContinentalnessColor(continentalnessSampler.Sample(worldX, worldZ)),
            PreviewMode.Erosion => EvaluateErosionColor(erosionSampler.Sample(worldX, worldZ)),
            PreviewMode.Weirdness => EvaluateWeirdnessColor(weirdnessSampler.Sample(worldX, worldZ)),
            PreviewMode.PeaksAndValleys => EvaluatePeaksAndValleysColor(PeaksAndValleys.Fold(weirdnessSampler.Sample(worldX, worldZ))),
            _ => GridClearColor,
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

    private static Color32 EvaluatePeaksAndValleysColor(float peaksAndValleys)
    {
        if (peaksAndValleys < -0.85f) return PVValleysColor;
        if (peaksAndValleys < -0.2f) return PVLowColor;
        if (peaksAndValleys < 0.2f) return PVMidColor;
        if (peaksAndValleys < 0.7f) return PVHighColor;
        return PVPeaksColor;
    }
}

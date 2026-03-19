using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TerraWorldGenPrototypeController : MonoBehaviour
{
    private static readonly Color32 AbyssColor = new(8, 24, 56, 255);
    private static readonly Color32 DeepOceanColor = new(18, 58, 110, 255);
    private static readonly Color32 OceanColor = new(34, 96, 156, 255);
    private static readonly Color32 CoastColor = new(223, 203, 150, 255);
    private static readonly Color32 NearInlandColor = new(102, 168, 92, 255);
    private static readonly Color32 MidInlandColor = new(66, 132, 74, 255);
    private static readonly Color32 CoreInlandColor = new(136, 110, 78, 255);
    private static readonly Color32 GridLineColor = new(255, 0, 0, 128);
    private static readonly Color32 GridClearColor = new(0, 0, 0, 0);

    private const int TextureResolution = 4096;
    private const int GridCellSize = 16;
    private const int GridLineThickness = 8;

    [Header("WorldGen")]
    [SerializeField] private TerraWorldGenPackAsset terraWorldGenPack;

    [Header("UI")]
    [SerializeField] private TMP_InputField seedInput;
    [SerializeField] private Button generateButton;
    [SerializeField] private Button randomGenButton;
    [SerializeField] private Button rawContButton;
    [SerializeField] private Button gridButton;
    [SerializeField] private RawImage outputImage;
    [SerializeField] private RawImage gridOverlayImage;

    private Texture2D _previewTexture;
    private Texture2D _gridTexture;
    private Color32[] _previewPixels;
    private bool _gridVisible = true;
    private bool _rawContSelected;

    private void Awake()
    {
        if (!ValidateReferences())
        {
            enabled = false;
            return;
        }

        generateButton.onClick.AddListener(GenerateRawContinentalnessPreview);
        randomGenButton.onClick.AddListener(FillRandomSeed);
        rawContButton.onClick.AddListener(ToggleRawContMode);
        gridButton.onClick.AddListener(ToggleGrid);

        EnsurePreviewTexture();
        EnsureGridOverlayTexture();
        ApplyGridVisibility();
        ApplyModeVisuals();
    }

    private void OnDestroy()
    {
        generateButton?.onClick.RemoveListener(GenerateRawContinentalnessPreview);
        randomGenButton?.onClick.RemoveListener(FillRandomSeed);
        rawContButton?.onClick.RemoveListener(ToggleRawContMode);
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
            Debug.LogError("TerraWorldGenPrototypeController requires a TerraWorldGenPackAsset.", this);
            isValid = false;
        }
        else if (terraWorldGenPack.ContinentalnessSettings == null)
        {
            Debug.LogError("TerraWorldGenPrototypeController TerraWorldGenPack is missing continentalness settings.", this);
            isValid = false;
        }

        if (seedInput == null)
        {
            Debug.LogError("TerraWorldGenPrototypeController requires a seed TMP_InputField.", this);
            isValid = false;
        }

        if (generateButton == null || randomGenButton == null || rawContButton == null || gridButton == null)
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

    private void GenerateRawContinentalnessPreview()
    {
        if (!_rawContSelected)
        {
            Debug.LogWarning("Select Raw Cont mode before generating.", this);
            return;
        }

        if (!TryResolveSeed(out int seed))
        {
            return;
        }

        TerraContinentalnessSampler sampler = new(seed, terraWorldGenPack.ContinentalnessSettings);
        EnsurePreviewTexture();

        int halfExtent = TextureResolution / 2;
        Parallel.For(0, TextureResolution, z =>
        {
            int worldZ = z - halfExtent;
            int rowStart = z * TextureResolution;
            for (int x = 0; x < TextureResolution; x++)
            {
                int worldX = x - halfExtent;
                float continentalness = sampler.Sample(worldX, worldZ);
                _previewPixels[rowStart + x] = EvaluateBandColor(continentalness);
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

    private void ToggleRawContMode()
    {
        _rawContSelected = !_rawContSelected;
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
        if (rawContButton == null)
        {
            return;
        }

        ColorBlock colors = rawContButton.colors;
        colors.normalColor = _rawContSelected
            ? new Color(0.78f, 0.88f, 1f, 1f)
            : Color.white;
        colors.selectedColor = colors.normalColor;
        rawContButton.colors = colors;
    }

    private static Color32 EvaluateBandColor(float continentalness)
    {
        if (continentalness < -0.7f) return AbyssColor;
        if (continentalness < -0.4f) return DeepOceanColor;
        if (continentalness < -0.15f) return OceanColor;
        if (continentalness < 0.15f) return CoastColor;
        if (continentalness < 0.4f) return NearInlandColor;
        if (continentalness < 0.7f) return MidInlandColor;
        return CoreInlandColor;
    }
}

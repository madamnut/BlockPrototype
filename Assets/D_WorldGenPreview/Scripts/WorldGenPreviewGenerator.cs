using TMPro;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WorldGenPreviewGenerator : MonoBehaviour
{
    private const int RandomSeedMin = -999999999;
    private const int RandomSeedMax = 1000000000;

    [Header("UI")]
    [SerializeField] private TMP_InputField seedInputField;
    [SerializeField] private RawImage previewImage;
    [SerializeField] private RawImage regionGridImage;
    [SerializeField] private Button generateButton;
    [SerializeField] private Button continentalnessButton;
    [SerializeField] private Button regionGridButton;

    [Header("Settings")]
    [SerializeField] private VoxelWorldGenSettingsAsset worldGenSettingsAsset;
    [SerializeField] private int textureSize = 1080;
    [SerializeField] private int regionCellSizeInPixels = 54;
    [SerializeField] private int worldRegionSizeInBlocks = 512;
    [SerializeField] private bool drawRegionGrid = true;
    [SerializeField] private Color regionGridColor = new(1f, 0.15f, 0.15f, 1f);
    [SerializeField] private Color buttonOffColor = Color.white;
    [SerializeField] private Color buttonOnColor = new(0.75f, 0.92f, 1f, 1f);
    [SerializeField] private Color deepOceanColor = new(0.04f, 0.17f, 0.38f, 1f);
    [SerializeField] private Color shallowOceanColor = new(0.08f, 0.29f, 0.56f, 1f);
    [SerializeField] private Color lowLandColor = new(0.09f, 0.34f, 0.15f, 1f);
    [SerializeField] private Color highLandColor = new(0.03f, 0.22f, 0.08f, 1f);

    private Texture2D _previewTexture;
    private Texture2D _gridTexture;

    private void Awake()
    {
        BindButtons();
        UpdateButtonVisuals();
    }

    private void Start()
    {
        UpdateRegionGridOverlay(Mathf.Max(16, textureSize));
    }

    private void OnDestroy()
    {
        if (generateButton != null)
        {
            generateButton.onClick.RemoveListener(GenerateFromCurrentInput);
        }

        if (continentalnessButton != null)
        {
            continentalnessButton.onClick.RemoveListener(UpdateButtonVisuals);
        }

        if (regionGridButton != null)
        {
            regionGridButton.onClick.RemoveListener(ToggleRegionGrid);
        }

        if (_previewTexture != null)
        {
            Destroy(_previewTexture);
        }

        if (_gridTexture != null)
        {
            Destroy(_gridTexture);
        }
    }

    public void GenerateFromCurrentInput()
    {
        Generate(ParseOrCreateSeed());
    }

    private void Generate(int seed)
    {
        if (previewImage == null)
        {
            Debug.LogWarning("WorldGenPreviewGenerator requires a preview RawImage reference.");
            return;
        }

        if (worldGenSettingsAsset == null || !worldGenSettingsAsset.settings.IsInitialized)
        {
            Debug.LogWarning("WorldGenPreviewGenerator requires an assigned WorldGenSettingsAsset.");
            return;
        }

        int size = Mathf.Max(16, textureSize);
        EnsureBuffers(size);
        UpdateRegionGridOverlay(size);

        NativeArray<Color32> pixels = new(size * size, Allocator.TempJob);
        JobHandle handle = new WorldGenPreviewNoiseJobs.WorldSpacePerlinContinentalnessColorJob
        {
            size = size,
            blocksPerPixel = (float)worldRegionSizeInBlocks / Mathf.Max(1, regionCellSizeInPixels),
            scale = worldGenSettingsAsset.settings.continentalness.scale,
            octaves = worldGenSettingsAsset.settings.continentalness.octaves,
            persistence = worldGenSettingsAsset.settings.continentalness.persistence,
            lacunarity = worldGenSettingsAsset.settings.continentalness.lacunarity,
            offset = new float2(
                worldGenSettingsAsset.settings.continentalness.offset.x,
                worldGenSettingsAsset.settings.continentalness.offset.y),
            seedOffset = seed * 0.000031f,
            seaLevelThreshold = worldGenSettingsAsset.settings.continentalnessSeaLevel,
            warpScaleMultiplier = worldGenSettingsAsset.settings.continentalnessWarpScaleMultiplier,
            warpStrength = worldGenSettingsAsset.settings.continentalnessWarpStrength,
            detailScaleMultiplier = worldGenSettingsAsset.settings.continentalnessDetailScaleMultiplier,
            detailWeight = worldGenSettingsAsset.settings.continentalnessDetailWeight,
            remapMin = worldGenSettingsAsset.settings.continentalnessRemapMin,
            remapMax = worldGenSettingsAsset.settings.continentalnessRemapMax,
            oceanDeepR = deepOceanColor.r,
            oceanDeepG = deepOceanColor.g,
            oceanDeepB = deepOceanColor.b,
            oceanShallowR = shallowOceanColor.r,
            oceanShallowG = shallowOceanColor.g,
            oceanShallowB = shallowOceanColor.b,
            landLowR = lowLandColor.r,
            landLowG = lowLandColor.g,
            landLowB = lowLandColor.b,
            landHighR = highLandColor.r,
            landHighG = highLandColor.g,
            landHighB = highLandColor.b,
            pixels = pixels,
        }.Schedule(pixels.Length, 128);

        handle.Complete();
        _previewTexture.SetPixelData(pixels, 0);
        _previewTexture.Apply(false, false);
        previewImage.texture = _previewTexture;
        pixels.Dispose();
    }

    private void BindButtons()
    {
        if (generateButton != null)
        {
            generateButton.onClick.RemoveListener(GenerateFromCurrentInput);
            generateButton.onClick.AddListener(GenerateFromCurrentInput);
        }

        if (continentalnessButton != null)
        {
            continentalnessButton.onClick.RemoveListener(UpdateButtonVisuals);
            continentalnessButton.onClick.AddListener(UpdateButtonVisuals);
        }

        if (regionGridButton != null)
        {
            regionGridButton.onClick.RemoveListener(ToggleRegionGrid);
            regionGridButton.onClick.AddListener(ToggleRegionGrid);
        }
    }

    private void ToggleRegionGrid()
    {
        drawRegionGrid = !drawRegionGrid;
        UpdateRegionGridOverlay(Mathf.Max(16, textureSize));
    }

    private void UpdateButtonVisuals()
    {
        SetButtonColor(continentalnessButton, true);
        SetButtonColor(regionGridButton, drawRegionGrid);
    }

    private void SetButtonColor(Button button, bool isOn)
    {
        if (button == null)
        {
            return;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = isOn ? buttonOnColor : buttonOffColor;
        colors.selectedColor = colors.normalColor;
        button.colors = colors;
    }

    private int ParseOrCreateSeed()
    {
        if (seedInputField != null && int.TryParse(seedInputField.text, out int parsedSeed))
        {
            return parsedSeed;
        }

        int seed = UnityEngine.Random.Range(RandomSeedMin, RandomSeedMax);
        if (seedInputField != null)
        {
            seedInputField.text = seed.ToString();
        }

        return seed;
    }

    private void EnsureBuffers(int size)
    {
        if (_previewTexture == null || _previewTexture.width != size || _previewTexture.height != size)
        {
            if (_previewTexture != null)
            {
                Destroy(_previewTexture);
            }

            _previewTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "WorldGenPreviewTexture",
            };
        }

        if (_gridTexture == null || _gridTexture.width != size || _gridTexture.height != size)
        {
            if (_gridTexture != null)
            {
                Destroy(_gridTexture);
            }

            _gridTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "WorldGenPreviewGridTexture",
            };
        }
    }

    private void UpdateRegionGridOverlay(int size)
    {
        if (regionGridImage == null || _gridTexture == null)
        {
            return;
        }

        Color32[] pixels = new Color32[size * size];
        if (drawRegionGrid)
        {
            int cellSize = Mathf.Max(1, regionCellSizeInPixels);
            Color32 gridColor = regionGridColor;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool onVertical = x % cellSize == 0;
                    bool onHorizontal = y % cellSize == 0;
                    if (onVertical || onHorizontal)
                    {
                        pixels[(y * size) + x] = gridColor;
                    }
                }
            }
        }

        _gridTexture.SetPixelData(pixels, 0);
        _gridTexture.Apply(false, false);
        regionGridImage.texture = _gridTexture;
        regionGridImage.enabled = drawRegionGrid;
        UpdateButtonVisuals();
    }
}

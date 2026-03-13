using TMPro;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WorldGenPrototype : MonoBehaviour
{
    private const int ContinentalnessResolution = WorldGenPrototypeJobs.SectorSizeInBlocks;

    private enum GenerationMode
    {
        None,
        Continentalness,
    }

    [Header("UI")]
    [SerializeField] private TMP_InputField seedInputField;
    [SerializeField] private RawImage previewImage;
    [SerializeField] private RawImage gridImage;
    [SerializeField] private Button generateButton;
    [SerializeField] private Button continentalnessButton;
    [SerializeField] private Button gridButton;

    [Header("Continentalness")]
    [SerializeField] private ContinentalnessSettingsAsset continentalnessSettingsAsset;
    [SerializeField] private ContinentalnessCdfProfileAsset continentalnessCdfProfileAsset;

    [Header("Grid")]
    [SerializeField] private bool drawGrid = true;
    [SerializeField] private Color gridColor = new(1f, 1f, 1f, 0.55f);

    [Header("Sector Index")]
    [FormerlySerializedAs("clusterIndexX")]
    [SerializeField] private int sectorIndexX;
    [FormerlySerializedAs("clusterIndexZ")]
    [SerializeField] private int sectorIndexZ;

    [Header("Mode Button Colors")]
    [SerializeField] private Color modeButtonOffColor = Color.white;
    [SerializeField] private Color modeButtonOnColor = new(0.75f, 0.92f, 1f, 1f);

    private Texture2D previewTexture;
    private Texture2D gridTexture;
    private NativeArray<float> cachedCdfLut;
    private ContinentalnessCdfProfileAsset cachedCdfProfileAsset;
    private int cachedCdfBakeVersion = -1;
    private GenerationMode mode = GenerationMode.None;

    private readonly struct ContinentalnessStats
    {
        public ContinentalnessStats(float min, float max, float average)
        {
            Min = min;
            Max = max;
            Average = average;
        }

        public float Min { get; }
        public float Max { get; }
        public float Average { get; }
    }

    private void Awake()
    {
        BindButton(generateButton, GeneratePreview);
        BindButton(continentalnessButton, ToggleContinentalnessMode);
        BindButton(gridButton, ToggleGrid);
        UpdateButtonVisuals();
    }

    private void OnDestroy()
    {
        UnbindButton(generateButton, GeneratePreview);
        UnbindButton(continentalnessButton, ToggleContinentalnessMode);
        UnbindButton(gridButton, ToggleGrid);
        DisposeCachedCdfLut();
        DestroyTexture(ref previewTexture);
        DestroyTexture(ref gridTexture);
    }

    private void GeneratePreview()
    {
        if (mode == GenerationMode.None)
        {
            Debug.Log("[WorldGenPrototype] No generation mode selected. Preview unchanged.");
            return;
        }

        int seed = ResolveSeed();
        EnsureTextures();
        ContinentalnessStats stats = GenerateContinentalnessPreview(seed);
        UpdateGridOverlay();

        Debug.Log(
            $"[WorldGenPrototype] Generated Continentalness preview. Seed: {seed}, SectorIndex: ({sectorIndexX}, {sectorIndexZ}), Resolution: {previewTexture.width}x{previewTexture.height}, CDF Remap: {UseCdfRemap()}, Continentalness Min/Avg/Max: {stats.Min:F3} / {stats.Average:F3} / {stats.Max:F3}");
    }

    private void ToggleContinentalnessMode()
    {
        mode = mode == GenerationMode.Continentalness ? GenerationMode.None : GenerationMode.Continentalness;
        UpdateButtonVisuals();
    }

    private void ToggleGrid()
    {
        drawGrid = !drawGrid;
        if (gridTexture != null)
        {
            UpdateGridOverlay();
        }

        UpdateButtonVisuals();
    }

    private int ResolveSeed()
    {
        if (seedInputField != null && int.TryParse(seedInputField.text, out int parsedSeed))
        {
            return parsedSeed;
        }

        int randomSeed = Random.Range(-999999999, 1000000000);
        if (seedInputField != null)
        {
            seedInputField.text = randomSeed.ToString();
        }

        return randomSeed;
    }

    private void EnsureTextures()
    {
        if (previewTexture == null || previewTexture.width != ContinentalnessResolution || previewTexture.height != ContinentalnessResolution)
        {
            DestroyTexture(ref previewTexture);
            previewTexture = CreateTexture("WorldGenPrototype_Continentalness", ContinentalnessResolution);
        }

        if (gridTexture == null || gridTexture.width != ContinentalnessResolution || gridTexture.height != ContinentalnessResolution)
        {
            DestroyTexture(ref gridTexture);
            gridTexture = CreateTexture("WorldGenPrototype_Grid", ContinentalnessResolution);
        }

        if (previewImage != null)
        {
            previewImage.texture = previewTexture;
        }

        if (gridImage != null)
        {
            gridImage.texture = gridTexture;
            gridImage.enabled = drawGrid;
        }
    }

    private ContinentalnessStats GenerateContinentalnessPreview(int seed)
    {
        int previewSize = previewTexture.width;
        NativeArray<Color32> pixels = new(previewSize * previewSize, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> values = new(previewSize * previewSize, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        ContinentalnessSettings settings = BuildContinentalnessSettings();
        bool useCdfRemap = UseCdfRemap();
        EnsureCdfLutCache();
        NativeArray<float> cdfLut = cachedCdfLut;
        NativeArray<float> statsValues = new(3, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        try
        {
            JobHandle previewHandle = new WorldGenPrototypeJobs.ContinentalnessPreviewJob
            {
                size = previewSize,
                seed = seed,
                sectorIndexX = sectorIndexX,
                sectorIndexZ = sectorIndexZ,
                useCdfRemap = useCdfRemap,
                settings = settings,
                cdfLut = cdfLut,
                pixels = pixels,
                values = values,
            }.Schedule(pixels.Length, 64);

            JobHandle statsHandle = new WorldGenPrototypeJobs.FloatStatsJob
            {
                values = values,
                stats = statsValues,
            }.Schedule(previewHandle);

            statsHandle.Complete();
            previewTexture.SetPixelData(pixels, 0);
            previewTexture.Apply(false, false);
            return new ContinentalnessStats(statsValues[0], statsValues[1], statsValues[2]);
        }
        finally
        {
            if (pixels.IsCreated)
            {
                pixels.Dispose();
            }

            if (values.IsCreated)
            {
                values.Dispose();
            }

            if (statsValues.IsCreated)
            {
                statsValues.Dispose();
            }
        }
    }

    private void UpdateGridOverlay()
    {
        if (gridTexture == null)
        {
            return;
        }

        int width = gridTexture.width;
        int height = gridTexture.height;
        int divisions = WorldGenPrototypeJobs.SectorSizeInRegions;
        NativeArray<Color32> pixels = new(width * height, Allocator.TempJob, NativeArrayOptions.ClearMemory);

        try
        {
            if (drawGrid)
            {
                int cellPixelSizeX = Mathf.Max(1, width / divisions);
                int cellPixelSizeY = Mathf.Max(1, height / divisions);
                JobHandle handle = new WorldGenPrototypeJobs.GridOverlayJob
                {
                    width = width,
                    height = height,
                    divisionCount = divisions,
                    lineWidthX = Mathf.Clamp(cellPixelSizeX / 64, 1, 4),
                    lineWidthY = Mathf.Clamp(cellPixelSizeY / 64, 1, 4),
                    lineColor = (Color32)gridColor,
                    pixels = pixels,
                }.Schedule(pixels.Length, 128);

                handle.Complete();
            }

            gridTexture.SetPixelData(pixels, 0);
            gridTexture.Apply(false, false);
        }
        finally
        {
            if (pixels.IsCreated)
            {
                pixels.Dispose();
            }
        }

        if (gridImage != null)
        {
            gridImage.texture = gridTexture;
            gridImage.enabled = drawGrid;
        }
    }

    private void UpdateButtonVisuals()
    {
        SetButtonVisual(continentalnessButton, mode == GenerationMode.Continentalness);
        SetButtonVisual(gridButton, drawGrid);
    }

    private ContinentalnessSettings BuildContinentalnessSettings()
    {
        if (continentalnessSettingsAsset == null)
        {
            Debug.LogWarning("[WorldGenPrototype] ContinentalnessSettingsAsset is not assigned. Using built-in defaults.");
            return ContinentalnessSettingsAsset.CreateDefaultSettings();
        }

        return continentalnessSettingsAsset.ToSettings();
    }

    private void EnsureCdfLutCache()
    {
        if (!UseCdfRemap())
        {
            DisposeCachedCdfLut();
            return;
        }

        bool needsRefresh =
            !cachedCdfLut.IsCreated ||
            cachedCdfProfileAsset != continentalnessCdfProfileAsset ||
            cachedCdfBakeVersion != continentalnessCdfProfileAsset.BakeVersion;

        if (!needsRefresh)
        {
            return;
        }

        DisposeCachedCdfLut();
        float[] sourceLut = continentalnessCdfProfileAsset.BakedCdfLut;
        cachedCdfLut = new NativeArray<float>(sourceLut.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        for (int i = 0; i < sourceLut.Length; i++)
        {
            cachedCdfLut[i] = sourceLut[i];
        }

        cachedCdfProfileAsset = continentalnessCdfProfileAsset;
        cachedCdfBakeVersion = continentalnessCdfProfileAsset.BakeVersion;
    }

    private bool UseCdfRemap()
    {
        return continentalnessCdfProfileAsset != null && continentalnessCdfProfileAsset.EnableRemap;
    }

    private void DisposeCachedCdfLut()
    {
        if (cachedCdfLut.IsCreated)
        {
            cachedCdfLut.Dispose();
        }

        cachedCdfProfileAsset = null;
        cachedCdfBakeVersion = -1;
    }

    private void SetButtonVisual(Button button, bool isActive)
    {
        if (button == null || button.targetGraphic == null)
        {
            return;
        }

        button.targetGraphic.color = isActive ? modeButtonOnColor : modeButtonOffColor;
    }

    private static Texture2D CreateTexture(string textureName, int size)
    {
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            name = textureName,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        return texture;
    }

    private static void DestroyTexture(ref Texture2D texture)
    {
        if (texture == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(texture);
        }
        else
        {
            DestroyImmediate(texture);
        }

        texture = null;
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void UnbindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
    }
}

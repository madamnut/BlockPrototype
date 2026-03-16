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
        ContPvHeight,
        Erosion,
        Weirdness,
        Pv,
        Temperature,
        Precipitation,
    }

    [Header("UI")]
    [SerializeField] private TMP_InputField seedInputField;
    [SerializeField] private RawImage previewImage;
    [SerializeField] private RawImage gridImage;
    [SerializeField] private Button generateButton;
    [SerializeField] private Button continentalnessButton;
    [SerializeField] private Button contPvButton;
    [SerializeField] private Button erosionButton;
    [FormerlySerializedAs("peaksRidgesButton")]
    [SerializeField] private Button weirdnessButton;
    [SerializeField] private Button pvButton;
    [SerializeField] private Button temperatureButton;
    [SerializeField] private Button precipitationButton;
    [SerializeField] private Button gridButton;

    [Header("World Gen Settings")]
    [FormerlySerializedAs("continentalnessSettingsAsset")]
    [SerializeField] private WorldGenSettingsAsset worldGenSettingsAsset;
    [FormerlySerializedAs("continentalnessCdfProfileAsset")]
    [SerializeField] private WorldGenRemapProfileAsset worldGenRemapProfileAsset;

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
    private NativeArray<float> cachedContinentalnessCdfLut;
    private NativeArray<float> cachedErosionCdfLut;
    private NativeArray<float> cachedRidgesCdfLut;
    private NativeArray<float> cachedTemperatureCdfLut;
    private NativeArray<float> cachedPrecipitationCdfLut;
    private NativeArray<float> cachedContinentalnessHeightLut;
    private WorldGenRemapProfileAsset cachedRemapProfileAsset;
    private int cachedRemapBakeVersion = -1;
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
        BindButton(contPvButton, ToggleContPvMode);
        BindButton(erosionButton, ToggleErosionMode);
        BindButton(weirdnessButton, ToggleWeirdnessMode);
        BindButton(pvButton, TogglePvMode);
        BindButton(temperatureButton, ToggleTemperatureMode);
        BindButton(precipitationButton, TogglePrecipitationMode);
        BindButton(gridButton, ToggleGrid);
        UpdateButtonVisuals();
    }

    private void OnDestroy()
    {
        UnbindButton(generateButton, GeneratePreview);
        UnbindButton(continentalnessButton, ToggleContinentalnessMode);
        UnbindButton(contPvButton, ToggleContPvMode);
        UnbindButton(erosionButton, ToggleErosionMode);
        UnbindButton(weirdnessButton, ToggleWeirdnessMode);
        UnbindButton(pvButton, TogglePvMode);
        UnbindButton(temperatureButton, ToggleTemperatureMode);
        UnbindButton(precipitationButton, TogglePrecipitationMode);
        UnbindButton(gridButton, ToggleGrid);
        DisposeCachedRemapLut();
        DisposeCachedFilterLut();
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
        ContinentalnessStats stats;
        string label;
        switch (mode)
        {
            case GenerationMode.Continentalness:
                stats = GenerateContinentalnessPreview(seed);
                label = $"Remap: {UseContinentalnessRemap()}";
                break;
            case GenerationMode.ContPvHeight:
                stats = GenerateContPvHeightPreview(seed);
                label = $"Height Map, Cont Remap: {UseContinentalnessRemap()}, Cont Height Spline: {UseContinentalnessHeightSpline()}";
                break;
            case GenerationMode.Erosion:
                stats = GenerateErosionPreview(seed);
                label = $"Remap: {UseErosionRemap()}";
                break;
            case GenerationMode.Weirdness:
                stats = GenerateRidgesPreview(seed);
                label = $"Remap: {UseRidgesRemap()}";
                break;
            case GenerationMode.Pv:
                stats = GeneratePvPreview(seed);
                label = $"Source: Weirdness, Remap: {UseRidgesRemap()}";
                break;
            case GenerationMode.Temperature:
                stats = GenerateTemperaturePreview(seed);
                label = $"Remap: {UseTemperatureRemap()}";
                break;
            case GenerationMode.Precipitation:
                stats = GeneratePrecipitationPreview(seed);
                label = $"Remap: {UsePrecipitationRemap()}";
                break;
            default:
                return;
        }

        UpdateGridOverlay();

        Debug.Log(
            $"[WorldGenPrototype] Generated {mode} preview. Seed: {seed}, SectorIndex: ({sectorIndexX}, {sectorIndexZ}), Resolution: {previewTexture.width}x{previewTexture.height}, {label}, Value Min/Avg/Max: {stats.Min:F3} / {stats.Average:F3} / {stats.Max:F3}");
    }

    private void ToggleContinentalnessMode()
    {
        mode = mode == GenerationMode.Continentalness ? GenerationMode.None : GenerationMode.Continentalness;
        UpdateButtonVisuals();
    }

    private void ToggleErosionMode()
    {
        mode = mode == GenerationMode.Erosion ? GenerationMode.None : GenerationMode.Erosion;
        UpdateButtonVisuals();
    }

    private void ToggleContPvMode()
    {
        mode = mode == GenerationMode.ContPvHeight ? GenerationMode.None : GenerationMode.ContPvHeight;
        UpdateButtonVisuals();
    }

    private void ToggleWeirdnessMode()
    {
        mode = mode == GenerationMode.Weirdness ? GenerationMode.None : GenerationMode.Weirdness;
        UpdateButtonVisuals();
    }

    private void TogglePvMode()
    {
        mode = mode == GenerationMode.Pv ? GenerationMode.None : GenerationMode.Pv;
        UpdateButtonVisuals();
    }

    private void ToggleTemperatureMode()
    {
        mode = mode == GenerationMode.Temperature ? GenerationMode.None : GenerationMode.Temperature;
        UpdateButtonVisuals();
    }

    private void TogglePrecipitationMode()
    {
        mode = mode == GenerationMode.Precipitation ? GenerationMode.None : GenerationMode.Precipitation;
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
        bool useCdfRemap = UseContinentalnessRemap();
        EnsureRemapLutCache();
        NativeArray<float> cdfLut = cachedContinentalnessCdfLut;
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

    private ContinentalnessStats GenerateErosionPreview(int seed)
    {
        int previewSize = previewTexture.width;
        NativeArray<Color32> pixels = new(previewSize * previewSize, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> values = new(previewSize * previewSize, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        ErosionSettings settings = BuildErosionSettings();
        bool useCdfRemap = UseErosionRemap();
        EnsureRemapLutCache();
        NativeArray<float> cdfLut = cachedErosionCdfLut;
        NativeArray<float> statsValues = new(3, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        try
        {
            JobHandle previewHandle = new WorldGenPrototypeJobs.ErosionPreviewJob
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

    private ContinentalnessStats GenerateContPvHeightPreview(int seed)
    {
        int previewSize = previewTexture.width;
        NativeArray<Color32> pixels = new(previewSize * previewSize, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> values = new(previewSize * previewSize, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        ContinentalnessSettings continentalnessSettings = BuildContinentalnessSettings();
        bool useContinentalnessCdfRemap = UseContinentalnessRemap();
        EnsureRemapLutCache();
        EnsureFilterLutCache();
        NativeArray<float> statsValues = new(3, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        try
        {
            JobHandle previewHandle = new WorldGenPrototypeJobs.ContPvHeightPreviewJob
            {
                size = previewSize,
                seed = seed,
                sectorIndexX = sectorIndexX,
                sectorIndexZ = sectorIndexZ,
                useContinentalnessRemap = useContinentalnessCdfRemap,
                seaLevel = worldGenSettingsAsset != null ? worldGenSettingsAsset.SeaLevel : 63,
                continentalnessSettings = continentalnessSettings,
                continentalnessCdfLut = cachedContinentalnessCdfLut,
                continentalnessHeightLut = cachedContinentalnessHeightLut,
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

    private ContinentalnessStats GenerateRidgesPreview(int seed)
    {
        int previewSize = previewTexture.width;
        NativeArray<Color32> pixels = new(previewSize * previewSize, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> values = new(previewSize * previewSize, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        RidgesSettings settings = BuildRidgesSettings();
        bool useCdfRemap = UseRidgesRemap();
        EnsureRemapLutCache();
        EnsureFilterLutCache();
        NativeArray<float> cdfLut = cachedRidgesCdfLut;
        NativeArray<float> statsValues = new(3, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        try
        {
            JobHandle previewHandle = new WorldGenPrototypeJobs.RidgesPreviewJob
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

    private ContinentalnessStats GeneratePvPreview(int seed)
    {
        int previewSize = previewTexture.width;
        NativeArray<Color32> pixels = new(previewSize * previewSize, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> values = new(previewSize * previewSize, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        RidgesSettings settings = BuildRidgesSettings();
        bool useCdfRemap = UseRidgesRemap();
        EnsureRemapLutCache();
        EnsureFilterLutCache();
        NativeArray<float> cdfLut = cachedRidgesCdfLut;
        NativeArray<float> statsValues = new(3, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        try
        {
            JobHandle previewHandle = new WorldGenPrototypeJobs.PvPreviewJob
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

    private ContinentalnessStats GenerateTemperaturePreview(int seed)
    {
        int previewSize = previewTexture.width;
        NativeArray<Color32> pixels = new(previewSize * previewSize, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> values = new(previewSize * previewSize, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        TemperatureSettings settings = BuildTemperatureSettings();
        bool useCdfRemap = UseTemperatureRemap();
        EnsureRemapLutCache();
        NativeArray<float> cdfLut = cachedTemperatureCdfLut;
        NativeArray<float> statsValues = new(3, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        try
        {
            JobHandle previewHandle = new WorldGenPrototypeJobs.TemperaturePreviewJob
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

    private ContinentalnessStats GeneratePrecipitationPreview(int seed)
    {
        int previewSize = previewTexture.width;
        NativeArray<Color32> pixels = new(previewSize * previewSize, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> values = new(previewSize * previewSize, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        PrecipitationSettings settings = BuildPrecipitationSettings();
        bool useCdfRemap = UsePrecipitationRemap();
        EnsureRemapLutCache();
        NativeArray<float> cdfLut = cachedPrecipitationCdfLut;
        NativeArray<float> statsValues = new(3, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        try
        {
            JobHandle previewHandle = new WorldGenPrototypeJobs.PrecipitationPreviewJob
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
        SetButtonVisual(contPvButton, mode == GenerationMode.ContPvHeight);
        SetButtonVisual(erosionButton, mode == GenerationMode.Erosion);
        SetButtonVisual(weirdnessButton, mode == GenerationMode.Weirdness);
        SetButtonVisual(pvButton, mode == GenerationMode.Pv);
        SetButtonVisual(temperatureButton, mode == GenerationMode.Temperature);
        SetButtonVisual(precipitationButton, mode == GenerationMode.Precipitation);
        SetButtonVisual(gridButton, drawGrid);
    }

    private ContinentalnessSettings BuildContinentalnessSettings()
    {
        if (worldGenSettingsAsset == null)
        {
            Debug.LogWarning("[WorldGenPrototype] WorldGenSettingsAsset is not assigned. Using built-in defaults.");
            return WorldGenSettingsAsset.CreateDefaultSettings();
        }

        return worldGenSettingsAsset.ToSettings();
    }

    private ErosionSettings BuildErosionSettings()
    {
        if (worldGenSettingsAsset == null)
        {
            Debug.LogWarning("[WorldGenPrototype] WorldGenSettingsAsset is not assigned. Using built-in erosion defaults.");
            return WorldGenSettingsAsset.CreateDefaultErosionSettings();
        }

        return worldGenSettingsAsset.ToErosionSettings();
    }

    private RidgesSettings BuildRidgesSettings()
    {
        if (worldGenSettingsAsset == null)
        {
            Debug.LogWarning("[WorldGenPrototype] WorldGenSettingsAsset is not assigned. Using built-in peaks/ridges defaults.");
            return WorldGenSettingsAsset.CreateDefaultRidgesSettings();
        }

        return worldGenSettingsAsset.ToRidgesSettings();
    }

    private TemperatureSettings BuildTemperatureSettings()
    {
        if (worldGenSettingsAsset == null)
        {
            Debug.LogWarning("[WorldGenPrototype] WorldGenSettingsAsset is not assigned. Using built-in temperature defaults.");
            return WorldGenSettingsAsset.CreateDefaultTemperatureSettings();
        }

        return worldGenSettingsAsset.ToTemperatureSettings();
    }

    private PrecipitationSettings BuildPrecipitationSettings()
    {
        if (worldGenSettingsAsset == null)
        {
            Debug.LogWarning("[WorldGenPrototype] WorldGenSettingsAsset is not assigned. Using built-in precipitation defaults.");
            return WorldGenSettingsAsset.CreateDefaultPrecipitationSettings();
        }

        return worldGenSettingsAsset.ToPrecipitationSettings();
    }

    private void EnsureRemapLutCache()
    {
        if (!UseRemap())
        {
            DisposeCachedRemapLut();
            return;
        }

        bool needsRefresh =
            !cachedContinentalnessCdfLut.IsCreated ||
            !cachedErosionCdfLut.IsCreated ||
            !cachedRidgesCdfLut.IsCreated ||
            !cachedTemperatureCdfLut.IsCreated ||
            !cachedPrecipitationCdfLut.IsCreated ||
            cachedRemapProfileAsset != worldGenRemapProfileAsset ||
            cachedRemapBakeVersion != worldGenRemapProfileAsset.BakeVersion;

        if (!needsRefresh)
        {
            return;
        }

        DisposeCachedRemapLut();
        float[] continentalnessSourceLut = worldGenRemapProfileAsset.BakedContinentalnessRemapLut;
        cachedContinentalnessCdfLut = new NativeArray<float>(continentalnessSourceLut.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        for (int i = 0; i < continentalnessSourceLut.Length; i++)
        {
            cachedContinentalnessCdfLut[i] = continentalnessSourceLut[i];
        }

        float[] erosionSourceLut = worldGenRemapProfileAsset.BakedErosionRemapLut;
        cachedErosionCdfLut = new NativeArray<float>(erosionSourceLut.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        for (int i = 0; i < erosionSourceLut.Length; i++)
        {
            cachedErosionCdfLut[i] = erosionSourceLut[i];
        }

        float[] ridgesSourceLut = worldGenRemapProfileAsset.BakedRidgesRemapLut;
        cachedRidgesCdfLut = new NativeArray<float>(ridgesSourceLut.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        for (int i = 0; i < ridgesSourceLut.Length; i++)
        {
            cachedRidgesCdfLut[i] = ridgesSourceLut[i];
        }

        float[] temperatureSourceLut = worldGenRemapProfileAsset.BakedTemperatureRemapLut;
        cachedTemperatureCdfLut = new NativeArray<float>(temperatureSourceLut.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        for (int i = 0; i < temperatureSourceLut.Length; i++)
        {
            cachedTemperatureCdfLut[i] = temperatureSourceLut[i];
        }

        float[] precipitationSourceLut = worldGenRemapProfileAsset.BakedPrecipitationRemapLut;
        cachedPrecipitationCdfLut = new NativeArray<float>(precipitationSourceLut.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        for (int i = 0; i < precipitationSourceLut.Length; i++)
        {
            cachedPrecipitationCdfLut[i] = precipitationSourceLut[i];
        }

        cachedRemapProfileAsset = worldGenRemapProfileAsset;
        cachedRemapBakeVersion = worldGenRemapProfileAsset.BakeVersion;
    }

    private void EnsureFilterLutCache()
    {
        DisposeCachedFilterLut();

        if (UseContinentalnessHeightSpline())
        {
            float[] sourceLut = worldGenSettingsAsset.ContinentalnessHeightSpline.BakedLut;
            cachedContinentalnessHeightLut = new NativeArray<float>(sourceLut.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < sourceLut.Length; i++)
            {
                cachedContinentalnessHeightLut[i] = sourceLut[i];
            }
        }
    }

    private bool UseRemap()
    {
        return UseContinentalnessRemap() || UseErosionRemap() || UseRidgesRemap() || UseTemperatureRemap() || UsePrecipitationRemap();
    }

    private bool UseContinentalnessRemap()
    {
        return worldGenSettingsAsset != null &&
               worldGenSettingsAsset.UseContinentalnessRemap &&
               worldGenRemapProfileAsset != null &&
               worldGenRemapProfileAsset.HasContinentalnessRemap;
    }

    private bool UseContinentalnessHeightSpline()
    {
        return worldGenSettingsAsset != null &&
               worldGenSettingsAsset.ContinentalnessHeightSpline != null &&
               worldGenSettingsAsset.ContinentalnessHeightSpline.HasBakedLut;
    }

    private bool UseErosionRemap()
    {
        return worldGenSettingsAsset != null &&
               worldGenSettingsAsset.UseErosionRemap &&
               worldGenRemapProfileAsset != null &&
               worldGenRemapProfileAsset.HasErosionRemap;
    }

    private bool UseRidgesRemap()
    {
        return worldGenSettingsAsset != null &&
               worldGenSettingsAsset.UseRidgesRemap &&
               worldGenRemapProfileAsset != null &&
               worldGenRemapProfileAsset.HasRidgesRemap;
    }

    private bool UseTemperatureRemap()
    {
        return worldGenRemapProfileAsset != null && worldGenRemapProfileAsset.HasTemperatureRemap;
    }

    private bool UsePrecipitationRemap()
    {
        return worldGenRemapProfileAsset != null && worldGenRemapProfileAsset.HasPrecipitationRemap;
    }

    private void DisposeCachedRemapLut()
    {
        if (cachedContinentalnessCdfLut.IsCreated)
        {
            cachedContinentalnessCdfLut.Dispose();
        }

        if (cachedErosionCdfLut.IsCreated)
        {
            cachedErosionCdfLut.Dispose();
        }

        if (cachedRidgesCdfLut.IsCreated)
        {
            cachedRidgesCdfLut.Dispose();
        }

        if (cachedTemperatureCdfLut.IsCreated)
        {
            cachedTemperatureCdfLut.Dispose();
        }

        if (cachedPrecipitationCdfLut.IsCreated)
        {
            cachedPrecipitationCdfLut.Dispose();
        }

        cachedRemapProfileAsset = null;
        cachedRemapBakeVersion = -1;
    }

    private void DisposeCachedFilterLut()
    {
        if (cachedContinentalnessHeightLut.IsCreated)
        {
            cachedContinentalnessHeightLut.Dispose();
        }
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

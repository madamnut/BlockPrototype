using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Mathematics;
using UnityEngine.Serialization;

public sealed class VoronoiPreviewGenerator : MonoBehaviour
{
    private const int RandomSeedMin = -999999999;
    private const int RandomSeedMax = 1000000000;
    private enum GenerationMode
    {
        World,
        Voronoi,
        Continentalness,
        Erosion,
        Ridges,
        Heightmap,
        TemperaturePerlin1,
        TemperatureSimplex,
        PrecipitationPerlin1,
        PrecipitationSimplex,
        FertilityPerlin1,
        FertilitySimplex,
    }

    [Header("UI")]
    [SerializeField] private TMP_InputField seedInputField;
    [SerializeField] private RawImage previewImage;
    [SerializeField] private RawImage regionGridImage;
    [SerializeField] private Button generateButton;
    [SerializeField] private Button worldButton;
    [SerializeField] private Button voronoiButton;
    [FormerlySerializedAs("continentalnessPerlinButton")]
    [SerializeField] private Button continentalnessButton;
    [FormerlySerializedAs("erosionButton")]
    [SerializeField] private Button erosionButton;
    [SerializeField] private Button ridgesButton;
    [FormerlySerializedAs("heightmapPerlinButton")]
    [FormerlySerializedAs("heightmapSimplexButton")]
    [SerializeField] private Button heightmapButton;
    [SerializeField] private Button temperaturePerlin1Button;
    [SerializeField] private Button temperatureSimplexButton;
    [SerializeField] private Button precipitationPerlin1Button;
    [SerializeField] private Button precipitationSimplexButton;
    [SerializeField] private Button fertilityPerlin1Button;
    [SerializeField] private Button fertilitySimplexButton;
    [SerializeField] private Button regionGridButton;

    [Header("Mode Button Colors")]
    [SerializeField] private Color modeButtonOffColor = Color.white;
    [SerializeField] private Color modeButtonOnColor = new(0.75f, 0.92f, 1f, 1f);

    [Header("Voronoi")]
    [SerializeField] private int textureSize = 1000;
    [SerializeField] private int voronoiRegionCellSize = 50;
    [SerializeField] private int worldRegionSizeInBlocks = 512;
    [SerializeField] private bool drawRegionGrid = true;
    [SerializeField] private Color regionGridColor = new(1f, 0.15f, 0.15f, 1f);
    [SerializeField] private bool drawBoundaries = true;
    [SerializeField] private Color boundaryColor = new(0.1f, 0.1f, 0.12f, 1f);
    [SerializeField] private bool drawSites = true;
    [SerializeField] private Color siteColor = new(1f, 1f, 1f, 1f);
    [SerializeField] private int siteMarkerRadius = 3;
    [SerializeField] private Color voronoiBackgroundColor = Color.white;

    [Header("World")]
    [SerializeField] private BiomeGraph worldBiomeGraph;
    [SerializeField] private VoxelWorldGenSettingsAsset worldGenSettingsAsset;
    [SerializeField] private bool worldUseSimplexClimate = true;

    [Header("Noisy Border")]
    [SerializeField] private float borderNoiseScale = 0.006f;
    [SerializeField] private float borderNoiseStrength = 28f;
    [SerializeField] private int borderNoiseOctaves = 4;
    [SerializeField] private float borderNoisePersistence = 0.5f;
    [SerializeField] private float borderNoiseLacunarity = 2f;
    [SerializeField] private Vector2 borderNoiseOffsetA = new(37.2f, 91.7f);
    [SerializeField] private Vector2 borderNoiseOffsetB = new(141.9f, 12.4f);

    [Header("Continentalness")]
    [SerializeField] private float continentalnessScale = 0.0025f;
    [SerializeField] private int continentalnessOctaves = 3;
    [SerializeField] private float continentalnessPersistence = 0.5f;
    [SerializeField] private float continentalnessLacunarity = 2f;
    [SerializeField] private Vector2 continentalnessOffset = new(11.3f, 57.8f);

    [Header("Heightmap")]
    [SerializeField] [Range(0f, 1f)] private float continentalnessSeaLevel = 0.36f;
    [SerializeField] [Range(0f, 1f)] private float continentalnessWeight = 0.72f;
    [SerializeField] [Range(0f, 1f)] private float ridgeWeight = 0.48f;
    [SerializeField] [Range(0f, 2f)] private float ridgeSharpness = 1.15f;

    [Header("Erosion")]
    [SerializeField] private float erosionScale = 0.004f;
    [SerializeField] private int erosionOctaves = 3;
    [SerializeField] private float erosionPersistence = 0.5f;
    [SerializeField] private float erosionLacunarity = 2f;
    [SerializeField] private Vector2 erosionOffset = new(83.1f, 27.4f);
    [SerializeField] private Color erosionSmoothColor = new(0.2f, 0.85f, 0.95f, 1f);
    [SerializeField] private Color erosionRuggedColor = new(0.95f, 0.45f, 0.15f, 1f);
    [SerializeField] [Range(0f, 1f)] private float erosionOverlayStrength = 0.55f;

    [Header("Ridges")]
    [SerializeField] private float ridgesScale = 0.0031f;
    [SerializeField] private int ridgesOctaves = 3;
    [SerializeField] private float ridgesPersistence = 0.5f;
    [SerializeField] private float ridgesLacunarity = 2f;
    [SerializeField] private Vector2 ridgesOffset = new(121.8f, 74.6f);

    [Header("Temperature")]
    [SerializeField] private float temperatureScale = 0.0032f;
    [SerializeField] private int temperatureOctaves = 3;
    [SerializeField] private float temperaturePersistence = 0.5f;
    [SerializeField] private float temperatureLacunarity = 2f;
    [SerializeField] private Vector2 temperatureOffset = new(24.6f, 61.2f);
    [SerializeField] private Color temperatureColdColor = new(0.16f, 0.42f, 1f, 1f);
    [SerializeField] private Color temperatureHotColor = new(1f, 0.22f, 0.12f, 1f);

    [Header("Precipitation")]
    [SerializeField] private float precipitationScale = 0.0037f;
    [SerializeField] private int precipitationOctaves = 3;
    [SerializeField] private float precipitationPersistence = 0.5f;
    [SerializeField] private float precipitationLacunarity = 2f;
    [SerializeField] private Vector2 precipitationOffset = new(77.4f, 18.9f);
    [SerializeField] private Color precipitationLowColor = Color.white;
    [SerializeField] private Color precipitationHighColor = new(0.12f, 0.35f, 1f, 1f);

    [Header("Fertility")]
    [SerializeField] private float fertilityScale = 0.0042f;
    [SerializeField] private int fertilityOctaves = 3;
    [SerializeField] private float fertilityPersistence = 0.5f;
    [SerializeField] private float fertilityLacunarity = 2f;
    [SerializeField] private Vector2 fertilityOffset = new(139.7f, 42.5f);
    [SerializeField] private Color fertilityLowColor = Color.white;
    [SerializeField] private Color fertilityHighColor = new(0.45f, 0.28f, 0.12f, 1f);

    private Texture2D _previewTexture;
    private Texture2D _regionGridTexture;
    private Vector2[] _baseSites;
    private GenerationMode _mode = GenerationMode.Voronoi;

    private void Awake()
    {
        BindButtons();
        UpdateModeButtonVisuals();
    }

    private void OnDestroy()
    {
        if (generateButton != null)
        {
            generateButton.onClick.RemoveListener(GenerateFromCurrentInput);
        }

        if (worldButton != null)
        {
            worldButton.onClick.RemoveListener(SelectWorldMode);
        }

        if (voronoiButton != null)
        {
            voronoiButton.onClick.RemoveListener(SelectVoronoiMode);
        }

        if (continentalnessButton != null)
        {
            continentalnessButton.onClick.RemoveListener(SelectContinentalnessMode);
        }

        if (erosionButton != null)
        {
            erosionButton.onClick.RemoveListener(SelectErosionMode);
        }

        if (ridgesButton != null)
        {
            ridgesButton.onClick.RemoveListener(SelectRidgesMode);
        }

        if (heightmapButton != null)
        {
            heightmapButton.onClick.RemoveListener(SelectHeightmapMode);
        }

        if (temperaturePerlin1Button != null)
        {
            temperaturePerlin1Button.onClick.RemoveListener(SelectTemperaturePerlin1Mode);
        }

        if (temperatureSimplexButton != null)
        {
            temperatureSimplexButton.onClick.RemoveListener(SelectTemperatureSimplexMode);
        }

        if (precipitationPerlin1Button != null)
        {
            precipitationPerlin1Button.onClick.RemoveListener(SelectPrecipitationPerlin1Mode);
        }

        if (precipitationSimplexButton != null)
        {
            precipitationSimplexButton.onClick.RemoveListener(SelectPrecipitationSimplexMode);
        }

        if (fertilityPerlin1Button != null)
        {
            fertilityPerlin1Button.onClick.RemoveListener(SelectFertilityPerlin1Mode);
        }

        if (fertilitySimplexButton != null)
        {
            fertilitySimplexButton.onClick.RemoveListener(SelectFertilitySimplexMode);
        }

        if (regionGridButton != null)
        {
            regionGridButton.onClick.RemoveListener(ToggleRegionGrid);
        }

        if (_previewTexture != null)
        {
            Destroy(_previewTexture);
            _previewTexture = null;
        }

        if (_regionGridTexture != null)
        {
            Destroy(_regionGridTexture);
            _regionGridTexture = null;
        }
    }

    private void BindButtons()
    {
        if (generateButton != null)
        {
            generateButton.onClick.RemoveListener(GenerateFromCurrentInput);
            generateButton.onClick.AddListener(GenerateFromCurrentInput);
        }

        if (worldButton != null)
        {
            worldButton.onClick.RemoveListener(SelectWorldMode);
            worldButton.onClick.AddListener(SelectWorldMode);
        }

        if (voronoiButton != null)
        {
            voronoiButton.onClick.RemoveListener(SelectVoronoiMode);
            voronoiButton.onClick.AddListener(SelectVoronoiMode);
        }

        if (continentalnessButton != null)
        {
            continentalnessButton.onClick.RemoveListener(SelectContinentalnessMode);
            continentalnessButton.onClick.AddListener(SelectContinentalnessMode);
        }

        if (erosionButton != null)
        {
            erosionButton.onClick.RemoveListener(SelectErosionMode);
            erosionButton.onClick.AddListener(SelectErosionMode);
        }

        if (ridgesButton != null)
        {
            ridgesButton.onClick.RemoveListener(SelectRidgesMode);
            ridgesButton.onClick.AddListener(SelectRidgesMode);
        }

        if (heightmapButton != null)
        {
            heightmapButton.onClick.RemoveListener(SelectHeightmapMode);
            heightmapButton.onClick.AddListener(SelectHeightmapMode);
        }

        if (temperaturePerlin1Button != null)
        {
            temperaturePerlin1Button.onClick.RemoveListener(SelectTemperaturePerlin1Mode);
            temperaturePerlin1Button.onClick.AddListener(SelectTemperaturePerlin1Mode);
        }

        if (temperatureSimplexButton != null)
        {
            temperatureSimplexButton.onClick.RemoveListener(SelectTemperatureSimplexMode);
            temperatureSimplexButton.onClick.AddListener(SelectTemperatureSimplexMode);
        }

        if (precipitationPerlin1Button != null)
        {
            precipitationPerlin1Button.onClick.RemoveListener(SelectPrecipitationPerlin1Mode);
            precipitationPerlin1Button.onClick.AddListener(SelectPrecipitationPerlin1Mode);
        }

        if (precipitationSimplexButton != null)
        {
            precipitationSimplexButton.onClick.RemoveListener(SelectPrecipitationSimplexMode);
            precipitationSimplexButton.onClick.AddListener(SelectPrecipitationSimplexMode);
        }

        if (fertilityPerlin1Button != null)
        {
            fertilityPerlin1Button.onClick.RemoveListener(SelectFertilityPerlin1Mode);
            fertilityPerlin1Button.onClick.AddListener(SelectFertilityPerlin1Mode);
        }

        if (fertilitySimplexButton != null)
        {
            fertilitySimplexButton.onClick.RemoveListener(SelectFertilitySimplexMode);
            fertilitySimplexButton.onClick.AddListener(SelectFertilitySimplexMode);
        }

        if (regionGridButton != null)
        {
            regionGridButton.onClick.RemoveListener(ToggleRegionGrid);
            regionGridButton.onClick.AddListener(ToggleRegionGrid);
        }
    }

    public void GenerateFromCurrentInput()
    {
        int seed = ParseOrCreateSeed();
        Generate(seed);
    }

    public void SelectVoronoiMode()
    {
        SetMode(GenerationMode.Voronoi);
    }

    public void SelectWorldMode()
    {
        SetMode(GenerationMode.World);
    }

    public void SelectContinentalnessMode()
    {
        SetMode(GenerationMode.Continentalness);
    }

    public void SelectErosionMode()
    {
        SetMode(GenerationMode.Erosion);
    }

    public void SelectRidgesMode()
    {
        SetMode(GenerationMode.Ridges);
    }

    public void SelectHeightmapMode()
    {
        SetMode(GenerationMode.Heightmap);
    }

    public void SelectTemperaturePerlin1Mode()
    {
        SetMode(GenerationMode.TemperaturePerlin1);
    }

    public void SelectTemperatureSimplexMode()
    {
        SetMode(GenerationMode.TemperatureSimplex);
    }

    public void SelectPrecipitationPerlin1Mode()
    {
        SetMode(GenerationMode.PrecipitationPerlin1);
    }

    public void SelectPrecipitationSimplexMode()
    {
        SetMode(GenerationMode.PrecipitationSimplex);
    }

    public void SelectFertilityPerlin1Mode()
    {
        SetMode(GenerationMode.FertilityPerlin1);
    }

    public void SelectFertilitySimplexMode()
    {
        SetMode(GenerationMode.FertilitySimplex);
    }

    public void ToggleRegionGrid()
    {
        drawRegionGrid = !drawRegionGrid;
        if (_regionGridTexture != null)
        {
            UpdateRegionGridOverlay(_regionGridTexture.width);
        }
        UpdateModeButtonVisuals();
    }

    private void SetMode(GenerationMode mode)
    {
        _mode = mode;
        UpdateModeButtonVisuals();
    }

    private void UpdateModeButtonVisuals()
    {
        SetButtonVisual(worldButton, _mode == GenerationMode.World);
        SetButtonVisual(voronoiButton, _mode == GenerationMode.Voronoi);
        SetButtonVisual(continentalnessButton, _mode == GenerationMode.Continentalness);
        SetButtonVisual(erosionButton, _mode == GenerationMode.Erosion);
        SetButtonVisual(ridgesButton, _mode == GenerationMode.Ridges);
        SetButtonVisual(heightmapButton, _mode == GenerationMode.Heightmap);
        SetButtonVisual(temperaturePerlin1Button, _mode == GenerationMode.TemperaturePerlin1);
        SetButtonVisual(temperatureSimplexButton, _mode == GenerationMode.TemperatureSimplex);
        SetButtonVisual(precipitationPerlin1Button, _mode == GenerationMode.PrecipitationPerlin1);
        SetButtonVisual(precipitationSimplexButton, _mode == GenerationMode.PrecipitationSimplex);
        SetButtonVisual(fertilityPerlin1Button, _mode == GenerationMode.FertilityPerlin1);
        SetButtonVisual(fertilitySimplexButton, _mode == GenerationMode.FertilitySimplex);
        SetButtonVisual(regionGridButton, drawRegionGrid);
    }

    private void SetButtonVisual(Button button, bool isActive)
    {
        if (button == null || button.targetGraphic == null)
        {
            return;
        }

        button.targetGraphic.color = isActive ? modeButtonOnColor : modeButtonOffColor;
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

    private void Generate(int seed)
    {
        if (previewImage == null)
        {
            Debug.LogWarning("VoronoiPreviewGenerator requires a RawImage reference.");
            return;
        }

        ApplyWorldGenSettingsAsset();

        int size = Mathf.Max(16, textureSize);

        EnsureBuffers(size);
        UpdateRegionGridOverlay(size);

        if (_mode == GenerationMode.World)
        {
            PaintWorldBiomeMap(seed, size);
            return;
        }

        if (_mode == GenerationMode.Continentalness)
        {
            PaintContinentalnessJob(seed, size);
            return;
        }

        if (_mode == GenerationMode.Erosion)
        {
            PaintErosionJob(seed, size);
            return;
        }

        if (_mode == GenerationMode.Ridges)
        {
            PaintRidgesJob(seed, size);
            return;
        }

        if (_mode == GenerationMode.Heightmap)
        {
            PaintHeightmapJob(seed, size);
            return;
        }

        if (_mode == GenerationMode.TemperaturePerlin1)
        {
            PaintClimateGradientMapJob(
                "Temperature Perlin 1",
                seed,
                size,
                false,
                temperatureScale,
                temperatureOctaves,
                temperaturePersistence,
                temperatureLacunarity,
                temperatureOffset,
                temperatureColdColor,
                temperatureHotColor);
            return;
        }

        if (_mode == GenerationMode.TemperatureSimplex)
        {
            PaintClimateGradientMapJob(
                "Temperature Simplex",
                seed,
                size,
                true,
                temperatureScale,
                temperatureOctaves,
                temperaturePersistence,
                temperatureLacunarity,
                temperatureOffset,
                temperatureColdColor,
                temperatureHotColor);
            return;
        }

        if (_mode == GenerationMode.PrecipitationPerlin1)
        {
            PaintClimateGradientMapJob(
                "Precipitation Perlin 1",
                seed,
                size,
                false,
                precipitationScale,
                precipitationOctaves,
                precipitationPersistence,
                precipitationLacunarity,
                precipitationOffset,
                precipitationLowColor,
                precipitationHighColor);
            return;
        }

        if (_mode == GenerationMode.PrecipitationSimplex)
        {
            PaintClimateGradientMapJob(
                "Precipitation Simplex",
                seed,
                size,
                true,
                precipitationScale,
                precipitationOctaves,
                precipitationPersistence,
                precipitationLacunarity,
                precipitationOffset,
                precipitationLowColor,
                precipitationHighColor);
            return;
        }

        if (_mode == GenerationMode.FertilityPerlin1)
        {
            PaintClimateGradientMapJob(
                "Fertility Perlin 1",
                seed,
                size,
                false,
                fertilityScale,
                fertilityOctaves,
                fertilityPersistence,
                fertilityLacunarity,
                fertilityOffset,
                fertilityLowColor,
                fertilityHighColor);
            return;
        }

        if (_mode == GenerationMode.FertilitySimplex)
        {
            PaintClimateGradientMapJob(
                "Fertility Simplex",
                seed,
                size,
                true,
                fertilityScale,
                fertilityOctaves,
                fertilityPersistence,
                fertilityLacunarity,
                fertilityOffset,
                fertilityLowColor,
                fertilityHighColor);
            return;
        }

        _baseSites = BuildRegionSites(seed, size);
        PaintVoronoiJob(seed, size, _baseSites);
    }

    private void ApplyWorldGenSettingsAsset()
    {
        if (worldGenSettingsAsset == null || !worldGenSettingsAsset.settings.IsInitialized)
        {
            return;
        }

        VoxelTerrainGenerationSettings settings = worldGenSettingsAsset.settings;

        continentalnessSeaLevel = settings.continentalnessSeaLevel;
        continentalnessWeight = settings.continentalnessWeight;

        ApplyNoiseSettings(
            settings.continentalness,
            ref continentalnessScale,
            ref continentalnessOctaves,
            ref continentalnessPersistence,
            ref continentalnessLacunarity,
            ref continentalnessOffset);
    }

    private static void ApplyNoiseSettings(
        VoxelTerrainNoiseSettings source,
        ref float scale,
        ref int octaves,
        ref float persistence,
        ref float lacunarity,
        ref Vector2 offset)
    {
        scale = source.scale;
        octaves = source.octaves;
        persistence = source.persistence;
        lacunarity = source.lacunarity;
        offset = source.offset;
    }

    private Vector2[] BuildRegionSites(int seed, int size)
    {
        int cellSize = Mathf.Max(1, voronoiRegionCellSize);
        int cellsX = Mathf.CeilToInt(size / (float)cellSize);
        int cellsY = Mathf.CeilToInt(size / (float)cellSize);
        List<Vector2> sites = new((cellsX + 2) * (cellsY + 2));

        for (int cellY = -1; cellY <= cellsY; cellY++)
        {
            for (int cellX = -1; cellX <= cellsX; cellX++)
            {
                float offsetX = Hash01(seed, cellX, cellY, 0);
                float offsetY = Hash01(seed, cellX, cellY, 1);
                float siteX = (cellX * cellSize) + (offsetX * cellSize);
                float siteY = (cellY * cellSize) + (offsetY * cellSize);
                sites.Add(new Vector2(siteX, siteY));
            }
        }

        return sites.ToArray();
    }

    private static float Hash01(int seed, int x, int y, int salt)
    {
        unchecked
        {
            uint hash = (uint)seed;
            hash ^= (uint)(x * 374761393);
            hash ^= (uint)(y * 668265263);
            hash ^= (uint)salt * 2246822519u;
            hash = (hash ^ (hash >> 13)) * 1274126177u;
            hash ^= hash >> 16;
            return hash / 4294967295f;
        }
    }

    private void PaintVoronoiJob(int seed, int size, Vector2[] renderSites)
    {
        int pixelCount = size * size;
        float seedOffset = seed * 0.000031f;
        Color32 backgroundColor = voronoiBackgroundColor;
        Color32 lineColor = boundaryColor;

        NativeArray<float2> sites = new(renderSites.Length, Allocator.TempJob);
        NativeArray<int> cellIndices = new(pixelCount, Allocator.TempJob);
        NativeArray<Color32> pixels = new(pixelCount, Allocator.TempJob);

        try
        {
            for (int index = 0; index < renderSites.Length; index++)
            {
                sites[index] = new float2(renderSites[index].x, renderSites[index].y);
            }

            JobHandle rasterHandle = new WorldGenPreviewJobs.VoronoiRasterJob
            {
                size = size,
                backgroundR = backgroundColor.r,
                backgroundG = backgroundColor.g,
                backgroundB = backgroundColor.b,
                backgroundA = backgroundColor.a,
                borderNoiseScale = borderNoiseScale,
                borderNoiseStrength = borderNoiseStrength,
                borderNoiseOctaves = borderNoiseOctaves,
                borderNoisePersistence = borderNoisePersistence,
                borderNoiseLacunarity = borderNoiseLacunarity,
                borderNoiseOffsetA = new float2(borderNoiseOffsetA.x, borderNoiseOffsetA.y),
                borderNoiseOffsetB = new float2(borderNoiseOffsetB.x, borderNoiseOffsetB.y),
                seedOffset = seedOffset,
                sites = sites,
                cellIndices = cellIndices,
                pixels = pixels,
            }.Schedule(pixelCount, 128);

            JobHandle boundaryHandle = new WorldGenPreviewJobs.VoronoiBoundaryJob
            {
                size = size,
                drawBoundaries = drawBoundaries,
                lineR = lineColor.r,
                lineG = lineColor.g,
                lineB = lineColor.b,
                lineA = lineColor.a,
                cellIndices = cellIndices,
                pixels = pixels,
            }.Schedule(pixelCount, 128, rasterHandle);

            boundaryHandle.Complete();

            if (drawSites)
            {
                OverlaySites(size, renderSites, pixels);
            }

            _previewTexture.SetPixelData(pixels, mipLevel: 0);
            _previewTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            previewImage.texture = _previewTexture;
        }
        finally
        {
            if (pixels.IsCreated)
            {
                pixels.Dispose();
            }

            if (cellIndices.IsCreated)
            {
                cellIndices.Dispose();
            }

            if (sites.IsCreated)
            {
                sites.Dispose();
            }
        }
    }

    private void PaintContinentalnessJob(int seed, int size)
    {
        PaintNoiseFieldJob(
            "Continentalness",
            seed,
            size,
            continentalnessScale,
            continentalnessOctaves,
            continentalnessPersistence,
            continentalnessLacunarity,
            continentalnessOffset);
    }

    private void PaintErosionJob(int seed, int size)
    {
        PaintNoiseFieldJob(
            "Erosion",
            seed,
            size,
            erosionScale,
            erosionOctaves,
            erosionPersistence,
            erosionLacunarity,
            erosionOffset);
    }

    private void PaintRidgesJob(int seed, int size)
    {
        PaintNoiseFieldJob(
            "Ridges",
            seed,
            size,
            ridgesScale,
            ridgesOctaves,
            ridgesPersistence,
            ridgesLacunarity,
            ridgesOffset);
    }

    private void PaintNoiseFieldJob(
        string label,
        int seed,
        int size,
        float scale,
        int octaves,
        float persistence,
        float lacunarity,
        Vector2 offset)
    {
        int pixelCount = size * size;
        int cellSize = Mathf.Max(1, voronoiRegionCellSize);
        float blocksPerPixel = (float)Mathf.Max(1, worldRegionSizeInBlocks) / cellSize;
        NativeArray<Color32> pixels = new(pixelCount, Allocator.TempJob);
        float startTime = Time.realtimeSinceStartup;

        try
        {
            JobHandle handle = new WorldGenPreviewJobs.WorldSpacePerlinGrayscaleJob
            {
                size = size,
                blocksPerPixel = blocksPerPixel,
                scale = scale,
                octaves = octaves,
                persistence = persistence,
                lacunarity = lacunarity,
                offset = new float2(offset.x, offset.y),
                seedOffset = seed * 0.000031f,
                pixels = pixels,
            }.Schedule(pixelCount, 128);

            handle.Complete();

            _previewTexture.SetPixelData(pixels, mipLevel: 0);
            _previewTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            previewImage.texture = _previewTexture;

            float elapsedMs = (Time.realtimeSinceStartup - startTime) * 1000f;
            Debug.Log($"[WorldGenTest] {label} generated in {elapsedMs:F2} ms at {size}x{size}.");
        }
        finally
        {
            if (pixels.IsCreated)
            {
                pixels.Dispose();
            }
        }
    }

    private void PaintHeightmapJob(int seed, int size)
    {
        int pixelCount = size * size;
        int cellSize = Mathf.Max(1, voronoiRegionCellSize);
        float blocksPerPixel = (float)Mathf.Max(1, worldRegionSizeInBlocks) / cellSize;
        NativeArray<float> continentalnessValues = new(pixelCount, Allocator.TempJob);
        NativeArray<float> erosionValues = new(pixelCount, Allocator.TempJob);
        NativeArray<float> ridgesValues = new(pixelCount, Allocator.TempJob);
        NativeArray<Color32> pixels = new(pixelCount, Allocator.TempJob);
        float startTime = Time.realtimeSinceStartup;
        float seedOffset = seed * 0.000031f;

        try
        {
            JobHandle continentalnessHandle = new WorldGenPreviewJobs.WorldSpaceFloatMapJob
            {
                size = size,
                useSimplex = false,
                blocksPerPixel = blocksPerPixel,
                scale = continentalnessScale,
                octaves = continentalnessOctaves,
                persistence = continentalnessPersistence,
                lacunarity = continentalnessLacunarity,
                offset = new float2(continentalnessOffset.x, continentalnessOffset.y),
                seedOffset = seedOffset,
                values = continentalnessValues,
            }.Schedule(pixelCount, 128);

            JobHandle erosionHandle = new WorldGenPreviewJobs.WorldSpaceFloatMapJob
            {
                size = size,
                useSimplex = false,
                blocksPerPixel = blocksPerPixel,
                scale = erosionScale,
                octaves = erosionOctaves,
                persistence = erosionPersistence,
                lacunarity = erosionLacunarity,
                offset = new float2(erosionOffset.x, erosionOffset.y),
                seedOffset = seedOffset,
                values = erosionValues,
            }.Schedule(pixelCount, 128);

            JobHandle ridgesHandle = new WorldGenPreviewJobs.WorldSpaceFloatMapJob
            {
                size = size,
                useSimplex = false,
                blocksPerPixel = blocksPerPixel,
                scale = ridgesScale,
                octaves = ridgesOctaves,
                persistence = ridgesPersistence,
                lacunarity = ridgesLacunarity,
                offset = new float2(ridgesOffset.x, ridgesOffset.y),
                seedOffset = seedOffset,
                values = ridgesValues,
            }.Schedule(pixelCount, 128);

            JobHandle combinedHandle = JobHandle.CombineDependencies(continentalnessHandle, erosionHandle, ridgesHandle);

            JobHandle composeHandle = new WorldGenPreviewJobs.HeightmapComposeJob
            {
                continentalnessSeaLevel = continentalnessSeaLevel,
                continentalnessWeight = continentalnessWeight,
                ridgeWeight = ridgeWeight,
                ridgeSharpness = ridgeSharpness,
                continentalnessValues = continentalnessValues,
                erosionValues = erosionValues,
                ridgesValues = ridgesValues,
                pixels = pixels,
            }.Schedule(pixelCount, 128, combinedHandle);

            composeHandle.Complete();

            _previewTexture.SetPixelData(pixels, mipLevel: 0);
            _previewTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            previewImage.texture = _previewTexture;

            float elapsedMs = (Time.realtimeSinceStartup - startTime) * 1000f;
            Debug.Log($"[WorldGenTest] Heightmap generated in {elapsedMs:F2} ms at {size}x{size}.");
        }
        finally
        {
            if (continentalnessValues.IsCreated)
            {
                continentalnessValues.Dispose();
            }

            if (erosionValues.IsCreated)
            {
                erosionValues.Dispose();
            }

            if (ridgesValues.IsCreated)
            {
                ridgesValues.Dispose();
            }

            if (pixels.IsCreated)
            {
                pixels.Dispose();
            }
        }
    }

    private void PaintClimateGradientMapJob(
        string label,
        int seed,
        int size,
        bool useSimplex,
        float scale,
        int octaves,
        float persistence,
        float lacunarity,
        Vector2 offset,
        Color startColor,
        Color endColor)
    {
        int pixelCount = size * size;
        NativeArray<float> values = new(pixelCount, Allocator.TempJob);
        NativeArray<Color32> pixels = new(pixelCount, Allocator.TempJob);
        float startTime = Time.realtimeSinceStartup;

        try
        {
            JobHandle sampleHandle = new WorldGenPreviewJobs.FloatMapJob
            {
                size = size,
                useSimplex = useSimplex,
                scale = scale,
                octaves = octaves,
                persistence = persistence,
                lacunarity = lacunarity,
                offset = new float2(offset.x, offset.y),
                seedOffset = seed * 0.000031f,
                values = values,
            }.Schedule(pixelCount, 128);

            sampleHandle.Complete();

            EqualizeValues(values);

            JobHandle colorizeHandle = new WorldGenPreviewJobs.ColorizeValueMapJob
            {
                startR = startColor.r,
                startG = startColor.g,
                startB = startColor.b,
                endR = endColor.r,
                endG = endColor.g,
                endB = endColor.b,
                values = values,
                pixels = pixels,
            }.Schedule(pixelCount, 128);

            colorizeHandle.Complete();

            _previewTexture.SetPixelData(pixels, mipLevel: 0);
            _previewTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            previewImage.texture = _previewTexture;

            float elapsedMs = (Time.realtimeSinceStartup - startTime) * 1000f;
            Debug.Log($"[WorldGenTest] {label} generated in {elapsedMs:F2} ms at {size}x{size}.");
        }
        finally
        {
            if (values.IsCreated)
            {
                values.Dispose();
            }

            if (pixels.IsCreated)
            {
                pixels.Dispose();
            }
        }
    }

    private void PaintVoronoiAttributeMapJob(
        string label,
        int seed,
        int size,
        Vector2[] renderSites,
        bool useSimplex,
        float scale,
        int octaves,
        float persistence,
        float lacunarity,
        Vector2 offset,
        Color startColor,
        Color endColor)
    {
        int pixelCount = size * size;
        float seedOffset = seed * 0.000031f;
        Color32 lineColor = boundaryColor;
        NativeArray<float2> sites = new(renderSites.Length, Allocator.TempJob);
        NativeArray<float> siteValues = new(renderSites.Length, Allocator.TempJob);
        NativeArray<int> cellIndices = new(pixelCount, Allocator.TempJob);
        NativeArray<Color32> pixels = new(pixelCount, Allocator.TempJob);
        float startTime = Time.realtimeSinceStartup;

        try
        {
            for (int index = 0; index < renderSites.Length; index++)
            {
                Vector2 site = renderSites[index];
                sites[index] = new float2(site.x, site.y);
                siteValues[index] = useSimplex
                    ? WorldGenPreviewJobs.SampleFractalSimplex(site.x, site.y, scale, offset.x, offset.y, seedOffset, octaves, persistence, lacunarity)
                    : WorldGenPreviewJobs.SampleFractalPerlin(site.x, site.y, scale, offset.x, offset.y, seedOffset, octaves, persistence, lacunarity);
            }

            EqualizeValues(siteValues);

            JobHandle rasterHandle = new WorldGenPreviewJobs.VoronoiAttributeRasterJob
            {
                size = size,
                borderNoiseScale = borderNoiseScale,
                borderNoiseStrength = borderNoiseStrength,
                borderNoiseOctaves = borderNoiseOctaves,
                borderNoisePersistence = borderNoisePersistence,
                borderNoiseLacunarity = borderNoiseLacunarity,
                borderNoiseOffsetA = new float2(borderNoiseOffsetA.x, borderNoiseOffsetA.y),
                borderNoiseOffsetB = new float2(borderNoiseOffsetB.x, borderNoiseOffsetB.y),
                seedOffset = seedOffset,
                startR = startColor.r,
                startG = startColor.g,
                startB = startColor.b,
                endR = endColor.r,
                endG = endColor.g,
                endB = endColor.b,
                sites = sites,
                siteValues = siteValues,
                cellIndices = cellIndices,
                pixels = pixels,
            }.Schedule(pixelCount, 128);

            JobHandle boundaryHandle = new WorldGenPreviewJobs.VoronoiBoundaryJob
            {
                size = size,
                drawBoundaries = drawBoundaries,
                lineR = lineColor.r,
                lineG = lineColor.g,
                lineB = lineColor.b,
                lineA = lineColor.a,
                cellIndices = cellIndices,
                pixels = pixels,
            }.Schedule(pixelCount, 128, rasterHandle);

            boundaryHandle.Complete();

            if (drawSites)
            {
                OverlaySites(size, renderSites, pixels);
            }

            _previewTexture.SetPixelData(pixels, mipLevel: 0);
            _previewTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            previewImage.texture = _previewTexture;

            float elapsedMs = (Time.realtimeSinceStartup - startTime) * 1000f;
            Debug.Log($"[WorldGenTest] {label} generated in {elapsedMs:F2} ms at {size}x{size}.");
        }
        finally
        {
            if (pixels.IsCreated)
            {
                pixels.Dispose();
            }

            if (cellIndices.IsCreated)
            {
                cellIndices.Dispose();
            }

            if (siteValues.IsCreated)
            {
                siteValues.Dispose();
            }

            if (sites.IsCreated)
            {
                sites.Dispose();
            }
        }
    }

    private void PaintWorldBiomeMap(int seed, int size)
    {
        if (worldBiomeGraph == null)
        {
            Debug.LogWarning("VoronoiPreviewGenerator requires a BiomeGraph reference for World mode.");
            return;
        }

        _baseSites = BuildRegionSites(seed, size);

        int pixelCount = size * size;
        float seedOffset = seed * 0.000031f;
        bool useSimplex = worldUseSimplexClimate;
        Color32 lineColor = boundaryColor;
        NativeArray<float2> sites = new(_baseSites.Length, Allocator.TempJob);
        NativeArray<float> temperatureValues = new(_baseSites.Length, Allocator.TempJob);
        NativeArray<float> humidityValues = new(_baseSites.Length, Allocator.TempJob);
        NativeArray<Color32> siteColors = new(_baseSites.Length, Allocator.TempJob);
        NativeArray<int> cellIndices = new(pixelCount, Allocator.TempJob);
        NativeArray<Color32> pixels = new(pixelCount, Allocator.TempJob);
        float startTime = Time.realtimeSinceStartup;

        try
        {
            for (int index = 0; index < _baseSites.Length; index++)
            {
                Vector2 site = _baseSites[index];
                sites[index] = new float2(site.x, site.y);
                temperatureValues[index] = useSimplex
                    ? WorldGenPreviewJobs.SampleFractalSimplex(site.x, site.y, temperatureScale, temperatureOffset.x, temperatureOffset.y, seedOffset, temperatureOctaves, temperaturePersistence, temperatureLacunarity)
                    : WorldGenPreviewJobs.SampleFractalPerlin(site.x, site.y, temperatureScale, temperatureOffset.x, temperatureOffset.y, seedOffset, temperatureOctaves, temperaturePersistence, temperatureLacunarity);
                humidityValues[index] = useSimplex
                    ? WorldGenPreviewJobs.SampleFractalSimplex(site.x, site.y, precipitationScale, precipitationOffset.x, precipitationOffset.y, seedOffset, precipitationOctaves, precipitationPersistence, precipitationLacunarity)
                    : WorldGenPreviewJobs.SampleFractalPerlin(site.x, site.y, precipitationScale, precipitationOffset.x, precipitationOffset.y, seedOffset, precipitationOctaves, precipitationPersistence, precipitationLacunarity);
            }

            EqualizeValues(temperatureValues);
            EqualizeValues(humidityValues);

            for (int index = 0; index < _baseSites.Length; index++)
            {
                if (worldBiomeGraph.TryGetBiome(temperatureValues[index], humidityValues[index], out BiomeGraphEntry entry))
                {
                    siteColors[index] = (Color32)entry.Color;
                }
                else
                {
                    siteColors[index] = (Color32)voronoiBackgroundColor;
                }
            }

            JobHandle rasterHandle = new WorldGenPreviewJobs.VoronoiColorRasterJob
            {
                size = size,
                borderNoiseScale = borderNoiseScale,
                borderNoiseStrength = borderNoiseStrength,
                borderNoiseOctaves = borderNoiseOctaves,
                borderNoisePersistence = borderNoisePersistence,
                borderNoiseLacunarity = borderNoiseLacunarity,
                borderNoiseOffsetA = new float2(borderNoiseOffsetA.x, borderNoiseOffsetA.y),
                borderNoiseOffsetB = new float2(borderNoiseOffsetB.x, borderNoiseOffsetB.y),
                seedOffset = seedOffset,
                sites = sites,
                siteColors = siteColors,
                cellIndices = cellIndices,
                pixels = pixels,
            }.Schedule(pixelCount, 128);

            JobHandle boundaryHandle = new WorldGenPreviewJobs.VoronoiBoundaryJob
            {
                size = size,
                drawBoundaries = drawBoundaries,
                lineR = lineColor.r,
                lineG = lineColor.g,
                lineB = lineColor.b,
                lineA = lineColor.a,
                cellIndices = cellIndices,
                pixels = pixels,
            }.Schedule(pixelCount, 128, rasterHandle);

            boundaryHandle.Complete();

            if (drawSites)
            {
                OverlaySites(size, _baseSites, pixels);
            }

            _previewTexture.SetPixelData(pixels, mipLevel: 0);
            _previewTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            previewImage.texture = _previewTexture;

            float elapsedMs = (Time.realtimeSinceStartup - startTime) * 1000f;
            Debug.Log($"[WorldGenTest] World generated in {elapsedMs:F2} ms at {size}x{size}.");
        }
        finally
        {
            if (pixels.IsCreated)
            {
                pixels.Dispose();
            }

            if (cellIndices.IsCreated)
            {
                cellIndices.Dispose();
            }

            if (siteColors.IsCreated)
            {
                siteColors.Dispose();
            }

            if (humidityValues.IsCreated)
            {
                humidityValues.Dispose();
            }

            if (temperatureValues.IsCreated)
            {
                temperatureValues.Dispose();
            }

            if (sites.IsCreated)
            {
                sites.Dispose();
            }
        }
    }

    private static void EqualizeValues(NativeArray<float> values)
    {
        int count = values.Length;
        if (count <= 0)
        {
            return;
        }

        if (count == 1)
        {
            values[0] = 0.5f;
            return;
        }

        float[] sortedValues = new float[count];
        int[] sortedIndices = new int[count];
        float[] remappedValues = new float[count];

        for (int index = 0; index < count; index++)
        {
            sortedValues[index] = values[index];
            sortedIndices[index] = index;
        }

        Array.Sort(sortedValues, sortedIndices);

        int runStart = 0;
        while (runStart < count)
        {
            int runEnd = runStart;
            float runValue = sortedValues[runStart];

            while (runEnd + 1 < count && Mathf.Abs(sortedValues[runEnd + 1] - runValue) <= 0.000001f)
            {
                runEnd++;
            }

            float rank = ((runStart + runEnd) * 0.5f) / (count - 1f);
            for (int index = runStart; index <= runEnd; index++)
            {
                remappedValues[sortedIndices[index]] = rank;
            }

            runStart = runEnd + 1;
        }

        for (int index = 0; index < count; index++)
        {
            values[index] = remappedValues[index];
        }
    }

    private void EnsureBuffers(int size)
    {
        if (_previewTexture == null || _previewTexture.width != size || _previewTexture.height != size)
        {
            if (_previewTexture != null)
            {
                Destroy(_previewTexture);
            }

            _previewTexture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
            {
                name = "VoronoiPreview",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        if (_regionGridTexture == null || _regionGridTexture.width != size || _regionGridTexture.height != size)
        {
            if (_regionGridTexture != null)
            {
                Destroy(_regionGridTexture);
            }

            _regionGridTexture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
            {
                name = "VoronoiRegionGrid",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
        }

    }

    private void UpdateRegionGridOverlay(int size)
    {
        if (regionGridImage == null || _regionGridTexture == null)
        {
            return;
        }

        int pixelCount = size * size;
        NativeArray<Color32> pixels = new(pixelCount, Allocator.Temp);

        try
        {
            Color32 clear = new(0, 0, 0, 0);
            for (int index = 0; index < pixelCount; index++)
            {
                pixels[index] = clear;
            }

            if (drawRegionGrid)
            {
                OverlayRegionGrid(size, pixels);
            }

            _regionGridTexture.SetPixelData(pixels, mipLevel: 0);
            _regionGridTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            regionGridImage.texture = _regionGridTexture;
            regionGridImage.enabled = drawRegionGrid;
        }
        finally
        {
            if (pixels.IsCreated)
            {
                pixels.Dispose();
            }
        }
    }

    private void OverlaySites(int size, Vector2[] sites, NativeArray<Color32> pixels)
    {
        Color32 markerColor = siteColor;
        int radius = Mathf.Max(1, siteMarkerRadius);
        int radiusSquared = radius * radius;

        foreach (Vector2 site in sites)
        {
            int centerX = Mathf.RoundToInt(site.x);
            int centerY = Mathf.RoundToInt(site.y);

            for (int offsetY = -radius; offsetY <= radius; offsetY++)
            {
                for (int offsetX = -radius; offsetX <= radius; offsetX++)
                {
                    if ((offsetX * offsetX) + (offsetY * offsetY) > radiusSquared)
                    {
                        continue;
                    }

                    int x = centerX + offsetX;
                    int y = centerY + offsetY;

                    if (x < 0 || x >= size || y < 0 || y >= size)
                    {
                        continue;
                    }

                    pixels[(y * size) + x] = markerColor;
                }
            }
        }
    }

    private void OverlayRegionGrid(int size, NativeArray<Color32> pixels)
    {
        int cellSize = Mathf.Max(1, voronoiRegionCellSize);
        Color32 gridColor = regionGridColor;

        for (int x = 0; x < size; x += cellSize)
        {
            for (int y = 0; y < size; y++)
            {
                pixels[(y * size) + x] = gridColor;
            }
        }

        for (int y = 0; y < size; y += cellSize)
        {
            int rowStart = y * size;
            for (int x = 0; x < size; x++)
            {
                pixels[rowStart + x] = gridColor;
            }
        }
    }

}

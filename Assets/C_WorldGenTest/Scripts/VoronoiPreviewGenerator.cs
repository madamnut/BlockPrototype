using System;
using Unity.Collections;
using Unity.Jobs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Mathematics;

public sealed class VoronoiPreviewGenerator : MonoBehaviour
{
    private const int RandomSeedMin = -999999999;
    private const int RandomSeedMax = 1000000000;
    private const int FixedLloydIterations = 3;

    private enum GenerationMode
    {
        Voronoi,
        RelaxedVoronoi,
        ContinentalnessPerlin,
        ContinentalnessSimplex,
    }

    [Header("UI")]
    [SerializeField] private TMP_InputField seedInputField;
    [SerializeField] private RawImage previewImage;
    [SerializeField] private Button generateButton;
    [SerializeField] private Button voronoiButton;
    [SerializeField] private Button relaxedVoronoiButton;
    [SerializeField] private Button continentalnessPerlinButton;
    [SerializeField] private Button continentalnessSimplexButton;
    [SerializeField] private Button erosionButton;
    [SerializeField] private Button borderNoiseButton;

    [Header("Mode Button Colors")]
    [SerializeField] private Color modeButtonOffColor = Color.white;
    [SerializeField] private Color modeButtonOnColor = new(0.75f, 0.92f, 1f, 1f);

    [Header("Voronoi")]
    [SerializeField] private int textureSize = 1000;
    [SerializeField] private int pointCount = 36;
    [SerializeField] private bool drawBoundaries = true;
    [SerializeField] private Color boundaryColor = new(0.1f, 0.1f, 0.12f, 1f);
    [SerializeField] private bool drawSites = true;
    [SerializeField] private Color siteColor = new(1f, 1f, 1f, 1f);
    [SerializeField] private int siteMarkerRadius = 3;
    [SerializeField] private Color voronoiBackgroundColor = Color.white;

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

    [Header("Erosion")]
    [SerializeField] private float erosionScale = 0.004f;
    [SerializeField] private int erosionOctaves = 3;
    [SerializeField] private float erosionPersistence = 0.5f;
    [SerializeField] private float erosionLacunarity = 2f;
    [SerializeField] private Vector2 erosionOffset = new(83.1f, 27.4f);
    [SerializeField] private Color erosionSmoothColor = new(0.2f, 0.85f, 0.95f, 1f);
    [SerializeField] private Color erosionRuggedColor = new(0.95f, 0.45f, 0.15f, 1f);
    [SerializeField] [Range(0f, 1f)] private float erosionOverlayStrength = 0.55f;

    private Texture2D _previewTexture;
    private Vector2[] _baseSites;
    private GenerationMode _mode = GenerationMode.Voronoi;
    private bool _useBorderNoise;
    private bool _useErosion;

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

        if (voronoiButton != null)
        {
            voronoiButton.onClick.RemoveListener(SelectVoronoiMode);
        }

        if (relaxedVoronoiButton != null)
        {
            relaxedVoronoiButton.onClick.RemoveListener(SelectRelaxedVoronoiMode);
        }

        if (continentalnessPerlinButton != null)
        {
            continentalnessPerlinButton.onClick.RemoveListener(SelectContinentalnessPerlinMode);
        }

        if (continentalnessSimplexButton != null)
        {
            continentalnessSimplexButton.onClick.RemoveListener(SelectContinentalnessSimplexMode);
        }

        if (erosionButton != null)
        {
            erosionButton.onClick.RemoveListener(ToggleErosion);
        }

        if (borderNoiseButton != null)
        {
            borderNoiseButton.onClick.RemoveListener(ToggleBorderNoise);
        }

        if (_previewTexture != null)
        {
            Destroy(_previewTexture);
            _previewTexture = null;
        }
    }

    private void BindButtons()
    {
        if (generateButton != null)
        {
            generateButton.onClick.RemoveListener(GenerateFromCurrentInput);
            generateButton.onClick.AddListener(GenerateFromCurrentInput);
        }

        if (voronoiButton != null)
        {
            voronoiButton.onClick.RemoveListener(SelectVoronoiMode);
            voronoiButton.onClick.AddListener(SelectVoronoiMode);
        }

        if (relaxedVoronoiButton != null)
        {
            relaxedVoronoiButton.onClick.RemoveListener(SelectRelaxedVoronoiMode);
            relaxedVoronoiButton.onClick.AddListener(SelectRelaxedVoronoiMode);
        }

        if (continentalnessPerlinButton != null)
        {
            continentalnessPerlinButton.onClick.RemoveListener(SelectContinentalnessPerlinMode);
            continentalnessPerlinButton.onClick.AddListener(SelectContinentalnessPerlinMode);
        }

        if (continentalnessSimplexButton != null)
        {
            continentalnessSimplexButton.onClick.RemoveListener(SelectContinentalnessSimplexMode);
            continentalnessSimplexButton.onClick.AddListener(SelectContinentalnessSimplexMode);
        }

        if (erosionButton != null)
        {
            erosionButton.onClick.RemoveListener(ToggleErosion);
            erosionButton.onClick.AddListener(ToggleErosion);
        }

        if (borderNoiseButton != null)
        {
            borderNoiseButton.onClick.RemoveListener(ToggleBorderNoise);
            borderNoiseButton.onClick.AddListener(ToggleBorderNoise);
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

    public void SelectRelaxedVoronoiMode()
    {
        SetMode(GenerationMode.RelaxedVoronoi);
    }

    public void SelectContinentalnessPerlinMode()
    {
        SetMode(GenerationMode.ContinentalnessPerlin);
    }

    public void SelectContinentalnessSimplexMode()
    {
        SetMode(GenerationMode.ContinentalnessSimplex);
    }

    public void ToggleBorderNoise()
    {
        _useBorderNoise = !_useBorderNoise;
        UpdateModeButtonVisuals();
    }

    public void ToggleErosion()
    {
        _useErosion = !_useErosion;
        UpdateModeButtonVisuals();
    }

    private void SetMode(GenerationMode mode)
    {
        _mode = mode;
        UpdateModeButtonVisuals();
    }

    private void UpdateModeButtonVisuals()
    {
        SetButtonVisual(voronoiButton, _mode == GenerationMode.Voronoi);
        SetButtonVisual(relaxedVoronoiButton, _mode == GenerationMode.RelaxedVoronoi);
        SetButtonVisual(continentalnessPerlinButton, _mode == GenerationMode.ContinentalnessPerlin);
        SetButtonVisual(continentalnessSimplexButton, _mode == GenerationMode.ContinentalnessSimplex);
        SetButtonVisual(erosionButton, _useErosion);
        SetButtonVisual(borderNoiseButton, _useBorderNoise);
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

        int size = Mathf.Max(16, textureSize);
        int siteCount = Mathf.Max(1, pointCount);

        EnsureBuffers(size);

        if (_mode == GenerationMode.ContinentalnessPerlin)
        {
            PaintContinentalnessPerlinJob(seed, size);
            return;
        }

        if (_mode == GenerationMode.ContinentalnessSimplex)
        {
            PaintContinentalnessSimplexJob(seed, size);
            return;
        }

        _baseSites = new Vector2[siteCount];
        System.Random random = new(seed);
        for (int index = 0; index < siteCount; index++)
        {
            _baseSites[index] = new Vector2(random.Next(0, size), random.Next(0, size));
        }

        Vector2[] renderSites = CreateRenderSites(size);
        PaintVoronoiJob(seed, size, renderSites);
    }

    private Vector2[] CreateRenderSites(int size)
    {
        Vector2[] renderSites = new Vector2[_baseSites.Length];
        Array.Copy(_baseSites, renderSites, _baseSites.Length);

        if (_mode != GenerationMode.RelaxedVoronoi)
        {
            return renderSites;
        }

        NativeArray<float2> currentSites = new(renderSites.Length, Allocator.TempJob);
        NativeArray<float2> nextSites = new(renderSites.Length, Allocator.TempJob);
        NativeArray<float2> sums = new(renderSites.Length, Allocator.TempJob);
        NativeArray<int> counts = new(renderSites.Length, Allocator.TempJob);

        try
        {
            for (int index = 0; index < renderSites.Length; index++)
            {
                currentSites[index] = new float2(renderSites[index].x, renderSites[index].y);
            }

            for (int iteration = 0; iteration < FixedLloydIterations; iteration++)
            {
                WorldGenPreviewJobs.LloydRelaxationPassJob job = new()
                {
                    size = size,
                    inputSites = currentSites,
                    outputSites = nextSites,
                    sums = sums,
                    counts = counts,
                };

                job.Schedule().Complete();

                for (int index = 0; index < currentSites.Length; index++)
                {
                    currentSites[index] = nextSites[index];
                }
            }

            for (int index = 0; index < renderSites.Length; index++)
            {
                float2 site = currentSites[index];
                renderSites[index] = new Vector2(site.x, site.y);
            }
        }
        finally
        {
            if (counts.IsCreated)
            {
                counts.Dispose();
            }

            if (sums.IsCreated)
            {
                sums.Dispose();
            }

            if (nextSites.IsCreated)
            {
                nextSites.Dispose();
            }

            if (currentSites.IsCreated)
            {
                currentSites.Dispose();
            }
        }

        return renderSites;
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
                useBorderNoise = _useBorderNoise,
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

    private void PaintContinentalnessPerlinJob(int seed, int size)
    {
        int pixelCount = size * size;
        NativeArray<Color32> pixels = new(pixelCount, Allocator.TempJob);

        try
        {
            JobHandle handle = new WorldGenPreviewJobs.PerlinMapJob
            {
                size = size,
                scale = continentalnessScale,
            octaves = continentalnessOctaves,
            persistence = continentalnessPersistence,
            lacunarity = continentalnessLacunarity,
            offset = new float2(continentalnessOffset.x, continentalnessOffset.y),
            seedOffset = seed * 0.000031f,
            useErosion = _useErosion,
            erosionScale = erosionScale,
            erosionOctaves = erosionOctaves,
            erosionPersistence = erosionPersistence,
            erosionLacunarity = erosionLacunarity,
            erosionOffset = new float2(erosionOffset.x, erosionOffset.y),
            smoothColorR = erosionSmoothColor.r,
            smoothColorG = erosionSmoothColor.g,
            smoothColorB = erosionSmoothColor.b,
            ruggedColorR = erosionRuggedColor.r,
            ruggedColorG = erosionRuggedColor.g,
            ruggedColorB = erosionRuggedColor.b,
            erosionOverlayStrength = erosionOverlayStrength,
            pixels = pixels,
        }.Schedule(pixelCount, 128);

            handle.Complete();

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
        }
    }

    private void PaintContinentalnessSimplexJob(int seed, int size)
    {
        int pixelCount = size * size;
        NativeArray<Color32> pixels = new(pixelCount, Allocator.TempJob);

        try
        {
            JobHandle handle = new WorldGenPreviewJobs.SimplexMapJob
            {
                size = size,
                scale = continentalnessScale,
            octaves = continentalnessOctaves,
            persistence = continentalnessPersistence,
            lacunarity = continentalnessLacunarity,
            offset = new float2(continentalnessOffset.x, continentalnessOffset.y),
            seedOffset = seed * 0.000031f,
            useErosion = _useErosion,
            erosionScale = erosionScale,
            erosionOctaves = erosionOctaves,
            erosionPersistence = erosionPersistence,
            erosionLacunarity = erosionLacunarity,
            erosionOffset = new float2(erosionOffset.x, erosionOffset.y),
            smoothColorR = erosionSmoothColor.r,
            smoothColorG = erosionSmoothColor.g,
            smoothColorB = erosionSmoothColor.b,
            ruggedColorR = erosionRuggedColor.r,
            ruggedColorG = erosionRuggedColor.g,
            ruggedColorB = erosionRuggedColor.b,
            erosionOverlayStrength = erosionOverlayStrength,
            pixels = pixels,
        }.Schedule(pixelCount, 128);

            handle.Complete();

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

}

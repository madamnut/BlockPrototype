using TMPro;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WorldGenTest : MonoBehaviour
{
    private const int ClusterSizeInSectors = 16;
    private const int Phase1TextureSize = ClusterSizeInSectors;
    private const int Phase2TextureSize = ClusterSizeInSectors * 2;
    private const int Phase3TextureSize = ClusterSizeInSectors * 4;
    private const int Phase8TextureSize = ClusterSizeInSectors * 2;
    private const int Phase9TextureSize = ClusterSizeInSectors * 4;
    private const int Phase12TextureSize = ClusterSizeInSectors * 2;
    private const int Phase13TextureSize = ClusterSizeInSectors * 4;
    private const int GridPixelsPerCell = 32;

    private enum GenerationMode
    {
        None,
        Phase1,
        Phase2,
        Phase3,
        Phase4,
        Phase5,
        Phase6,
        Phase7,
        Phase8,
        Phase9,
        Phase10,
        Phase11,
        Phase12,
        Phase13,
        Phase14,
    }

    [Header("UI")]
    [SerializeField] private TMP_InputField seedInputField;
    [SerializeField] private RawImage previewImage;
    [FormerlySerializedAs("regionGridImage")]
    [SerializeField] private RawImage gridImage;
    [SerializeField] private Button generateButton;
    [SerializeField] private Button phase1Button;
    [SerializeField] private Button phase2Button;
    [SerializeField] private Button phase3Button;
    [SerializeField] private Button phase4Button;
    [SerializeField] private Button phase5Button;
    [SerializeField] private Button phase6Button;
    [SerializeField] private Button phase7Button;
    [SerializeField] private Button phase8Button;
    [SerializeField] private Button phase9Button;
    [SerializeField] private Button phase10Button;
    [SerializeField] private Button phase11Button;
    [SerializeField] private Button phase12Button;
    [SerializeField] private Button phase13Button;
    [SerializeField] private Button phase14Button;
    [FormerlySerializedAs("regionGridButton")]
    [SerializeField] private Button gridButton;

    [Header("Preview")]
    [SerializeField] private Color landColor = new(0.18f, 0.62f, 0.23f, 1f);
    [SerializeField] private Color seaColor = new(0.12f, 0.36f, 0.9f, 1f);
    [SerializeField, Range(0f, 1f)] private float landChance = 0.5f;

    [Header("Grid")]
    [FormerlySerializedAs("drawRegionGrid")]
    [SerializeField] private bool drawGrid = true;
    [FormerlySerializedAs("regionGridColor")]
    [SerializeField] private Color gridColor = new(1f, 1f, 1f, 0.7f);

    [Header("Cluster Index")]
    [SerializeField] private int clusterIndexX;
    [SerializeField] private int clusterIndexZ;

    [Header("Mode Button Colors")]
    [SerializeField] private Color modeButtonOffColor = Color.white;
    [SerializeField] private Color modeButtonOnColor = new(0.75f, 0.92f, 1f, 1f);

    private Texture2D previewTexture;
    private Texture2D gridTexture;
    private GenerationMode mode = GenerationMode.None;

    private readonly struct GenerationStats
    {
        public GenerationStats(int landCellCount, int seaCellCount)
        {
            LandCellCount = landCellCount;
            SeaCellCount = seaCellCount;
        }

        public int LandCellCount { get; }
        public int SeaCellCount { get; }
    }

    private void Awake()
    {
        BindButton(generateButton, GeneratePreview);
        BindButton(phase1Button, SelectPhase1Mode);
        BindButton(phase2Button, SelectPhase2Mode);
        BindButton(phase3Button, SelectPhase3Mode);
        BindButton(phase4Button, SelectPhase4Mode);
        BindButton(phase5Button, SelectPhase5Mode);
        BindButton(phase6Button, SelectPhase6Mode);
        BindButton(phase7Button, SelectPhase7Mode);
        BindButton(phase8Button, SelectPhase8Mode);
        BindButton(phase9Button, SelectPhase9Mode);
        BindButton(phase10Button, SelectPhase10Mode);
        BindButton(phase11Button, SelectPhase11Mode);
        BindButton(phase12Button, SelectPhase12Mode);
        BindButton(phase13Button, SelectPhase13Mode);
        BindButton(phase14Button, SelectPhase14Mode);
        BindButton(gridButton, ToggleGrid);
        UpdateButtonVisuals();
    }

    private void OnDestroy()
    {
        UnbindButton(generateButton, GeneratePreview);
        UnbindButton(phase1Button, SelectPhase1Mode);
        UnbindButton(phase2Button, SelectPhase2Mode);
        UnbindButton(phase3Button, SelectPhase3Mode);
        UnbindButton(phase4Button, SelectPhase4Mode);
        UnbindButton(phase5Button, SelectPhase5Mode);
        UnbindButton(phase6Button, SelectPhase6Mode);
        UnbindButton(phase7Button, SelectPhase7Mode);
        UnbindButton(phase8Button, SelectPhase8Mode);
        UnbindButton(phase9Button, SelectPhase9Mode);
        UnbindButton(phase10Button, SelectPhase10Mode);
        UnbindButton(phase11Button, SelectPhase11Mode);
        UnbindButton(phase12Button, SelectPhase12Mode);
        UnbindButton(phase13Button, SelectPhase13Mode);
        UnbindButton(phase14Button, SelectPhase14Mode);
        UnbindButton(gridButton, ToggleGrid);
        DestroyTexture(ref previewTexture);
        DestroyTexture(ref gridTexture);
    }

    private void GeneratePreview()
    {
        int seed = ResolveSeed();
        if (mode == GenerationMode.None)
        {
            Debug.Log("[WorldGenTest] No generation phase selected. Preview unchanged.");
            return;
        }

        EnsureTextures();
        GenerationStats stats = GeneratePreviewTexture(seed);
        UpdateGridOverlay();

        string phaseLabel = GetPhaseLabel(mode);
        Debug.Log($"[WorldGenTest] Generated {phaseLabel} cluster preview. Seed: {seed}, ClusterIndex: ({clusterIndexX}, {clusterIndexZ}), Texture: {previewTexture.width}x{previewTexture.height}, Cluster: {ClusterSizeInSectors}x{ClusterSizeInSectors} sectors.");
        float totalCellCount = stats.LandCellCount + stats.SeaCellCount;
        float landPercent = totalCellCount <= 0f ? 0f : (stats.LandCellCount / totalCellCount) * 100f;
        float seaPercent = 100f - landPercent;
        Debug.Log($"[WorldGenTest] {phaseLabel} stats. Land {landPercent:F1}% ({stats.LandCellCount}), Sea {seaPercent:F1}% ({stats.SeaCellCount})");
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

    private void SelectPhase1Mode()
    {
        mode = mode == GenerationMode.Phase1 ? GenerationMode.None : GenerationMode.Phase1;
        UpdateButtonVisuals();
    }

    private void SelectPhase2Mode()
    {
        mode = mode == GenerationMode.Phase2 ? GenerationMode.None : GenerationMode.Phase2;
        UpdateButtonVisuals();
    }

    private void SelectPhase3Mode()
    {
        mode = mode == GenerationMode.Phase3 ? GenerationMode.None : GenerationMode.Phase3;
        UpdateButtonVisuals();
    }

    private void SelectPhase4Mode()
    {
        mode = mode == GenerationMode.Phase4 ? GenerationMode.None : GenerationMode.Phase4;
        UpdateButtonVisuals();
    }

    private void SelectPhase5Mode()
    {
        mode = mode == GenerationMode.Phase5 ? GenerationMode.None : GenerationMode.Phase5;
        UpdateButtonVisuals();
    }

    private void SelectPhase6Mode()
    {
        mode = mode == GenerationMode.Phase6 ? GenerationMode.None : GenerationMode.Phase6;
        UpdateButtonVisuals();
    }

    private void SelectPhase7Mode()
    {
        mode = mode == GenerationMode.Phase7 ? GenerationMode.None : GenerationMode.Phase7;
        UpdateButtonVisuals();
    }

    private void SelectPhase8Mode()
    {
        mode = mode == GenerationMode.Phase8 ? GenerationMode.None : GenerationMode.Phase8;
        UpdateButtonVisuals();
    }

    private void SelectPhase9Mode()
    {
        mode = mode == GenerationMode.Phase9 ? GenerationMode.None : GenerationMode.Phase9;
        UpdateButtonVisuals();
    }

    private void SelectPhase10Mode()
    {
        mode = mode == GenerationMode.Phase10 ? GenerationMode.None : GenerationMode.Phase10;
        UpdateButtonVisuals();
    }

    private void SelectPhase11Mode()
    {
        mode = mode == GenerationMode.Phase11 ? GenerationMode.None : GenerationMode.Phase11;
        UpdateButtonVisuals();
    }

    private void SelectPhase12Mode()
    {
        mode = mode == GenerationMode.Phase12 ? GenerationMode.None : GenerationMode.Phase12;
        UpdateButtonVisuals();
    }

    private void SelectPhase13Mode()
    {
        mode = mode == GenerationMode.Phase13 ? GenerationMode.None : GenerationMode.Phase13;
        UpdateButtonVisuals();
    }

    private void SelectPhase14Mode()
    {
        mode = mode == GenerationMode.Phase14 ? GenerationMode.None : GenerationMode.Phase14;
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
        landChance = Mathf.Clamp01(landChance);

        int previewSize = GetPreviewSizeForMode();
        if (previewTexture == null || previewTexture.width != previewSize || previewTexture.height != previewSize)
        {
            DestroyTexture(ref previewTexture);
            previewTexture = CreateTexture("WorldGenTest_Preview", previewSize);
        }

        int gridSize = GetGridTextureSizeForMode();
        if (gridTexture == null || gridTexture.width != gridSize || gridTexture.height != gridSize)
        {
            DestroyTexture(ref gridTexture);
            gridTexture = CreateTexture("WorldGenTest_Grid", gridSize);
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

    private GenerationStats GeneratePreviewTexture(int seed)
    {
        int previewSize = previewTexture.width;
        NativeArray<Color32> pixels = new(previewSize * previewSize, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        try
        {
            JobHandle handle = new WorldGenTestJobs.PhasePreviewColorJob
            {
                size = previewSize,
                mode = (int)mode,
                seed = seed,
                clusterIndexX = clusterIndexX,
                clusterIndexZ = clusterIndexZ,
                landChance = landChance,
                landColor = (Color32)landColor,
                seaColor = (Color32)seaColor,
                pixels = pixels,
            }.Schedule(pixels.Length, 64);

            handle.Complete();
            GenerationStats stats = CountGenerationStats(pixels, (Color32)landColor);
            previewTexture.SetPixelData(pixels, 0);
            previewTexture.Apply(false, false);
            return stats;
        }
        finally
        {
            if (pixels.IsCreated)
            {
                pixels.Dispose();
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
        NativeArray<Color32> pixels = new(width * height, Allocator.TempJob, NativeArrayOptions.ClearMemory);

        try
        {
            if (drawGrid)
            {
                int divisions = GetGridDivisionCountForMode();
                int cellPixelSizeX = Mathf.Max(1, width / Mathf.Max(1, divisions));
                int cellPixelSizeY = Mathf.Max(1, height / Mathf.Max(1, divisions));

                JobHandle handle = new WorldGenTestJobs.GridOverlayJob
                {
                    width = width,
                    height = height,
                    divisionCount = divisions,
                    lineWidthX = Mathf.Clamp(cellPixelSizeX / 8, 1, cellPixelSizeX),
                    lineWidthY = Mathf.Clamp(cellPixelSizeY / 8, 1, cellPixelSizeY),
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
        SetButtonVisual(phase1Button, mode == GenerationMode.Phase1);
        SetButtonVisual(phase2Button, mode == GenerationMode.Phase2);
        SetButtonVisual(phase3Button, mode == GenerationMode.Phase3);
        SetButtonVisual(phase4Button, mode == GenerationMode.Phase4);
        SetButtonVisual(phase5Button, mode == GenerationMode.Phase5);
        SetButtonVisual(phase6Button, mode == GenerationMode.Phase6);
        SetButtonVisual(phase7Button, mode == GenerationMode.Phase7);
        SetButtonVisual(phase8Button, mode == GenerationMode.Phase8);
        SetButtonVisual(phase9Button, mode == GenerationMode.Phase9);
        SetButtonVisual(phase10Button, mode == GenerationMode.Phase10);
        SetButtonVisual(phase11Button, mode == GenerationMode.Phase11);
        SetButtonVisual(phase12Button, mode == GenerationMode.Phase12);
        SetButtonVisual(phase13Button, mode == GenerationMode.Phase13);
        SetButtonVisual(phase14Button, mode == GenerationMode.Phase14);
        SetButtonVisual(gridButton, drawGrid);
    }

    private int GetPreviewSizeForMode()
    {
        return mode switch
        {
            GenerationMode.Phase3 => Phase3TextureSize,
            GenerationMode.Phase4 => Phase3TextureSize,
            GenerationMode.Phase5 => Phase3TextureSize,
            GenerationMode.Phase6 => Phase3TextureSize,
            GenerationMode.Phase7 => Phase3TextureSize,
            GenerationMode.Phase8 => Phase8TextureSize,
            GenerationMode.Phase9 => Phase9TextureSize,
            GenerationMode.Phase10 => Phase9TextureSize,
            GenerationMode.Phase11 => Phase9TextureSize,
            GenerationMode.Phase12 => Phase12TextureSize,
            GenerationMode.Phase13 => Phase13TextureSize,
            GenerationMode.Phase14 => Phase13TextureSize,
            GenerationMode.Phase2 => Phase2TextureSize,
            _ => Phase1TextureSize,
        };
    }

    private static string GetPhaseLabel(GenerationMode generationMode)
    {
        return generationMode switch
        {
            GenerationMode.Phase14 => "Phase 14",
            GenerationMode.Phase13 => "Phase 13",
            GenerationMode.Phase12 => "Phase 12",
            GenerationMode.Phase11 => "Phase 11",
            GenerationMode.Phase10 => "Phase 10",
            GenerationMode.Phase9 => "Phase 9",
            GenerationMode.Phase8 => "Phase 8",
            GenerationMode.Phase7 => "Phase 7",
            GenerationMode.Phase6 => "Phase 6",
            GenerationMode.Phase5 => "Phase 5",
            GenerationMode.Phase4 => "Phase 4",
            GenerationMode.Phase3 => "Phase 3",
            GenerationMode.Phase2 => "Phase 2",
            _ => "Phase 1",
        };
    }

    private int GetGridDivisionCountForMode()
    {
        return GetPreviewSizeForMode();
    }

    private int GetGridTextureSizeForMode()
    {
        return GetGridDivisionCountForMode() * GridPixelsPerCell;
    }

    private void SetButtonVisual(Button button, bool isActive)
    {
        if (button == null || button.targetGraphic == null)
        {
            return;
        }

        button.targetGraphic.color = isActive ? modeButtonOnColor : modeButtonOffColor;
    }

    private static GenerationStats CountGenerationStats(NativeArray<Color32> pixels, Color32 land)
    {
        int landCellCount = 0;
        for (int index = 0; index < pixels.Length; index++)
        {
            if (pixels[index].Equals(land))
            {
                landCellCount++;
            }
        }

        return new GenerationStats(landCellCount, pixels.Length - landCellCount);
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

    private static void DestroyTexture(ref Texture2D texture)
    {
        if (texture == null)
        {
            return;
        }

        Destroy(texture);
        texture = null;
    }

    private static Texture2D CreateTexture(string textureName, int size)
    {
        return new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = textureName,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
    }
}

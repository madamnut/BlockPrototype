using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class WorldRuntime : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Material worldMaterial;
    [SerializeField] private Material fluidMaterial;
    [SerializeField] private Material foliageMaterial;
    [SerializeField] private BlockDatabase blockDatabase;
    [SerializeField] private Camera interactionCamera;
    [SerializeField] private GameObject chunkColumnPrefab;

    [Header("Terrain")]
    [FormerlySerializedAs("worldSizeInChunks")]
    [SerializeField, Min(1)] private int renderSizeInChunks = 9;
    [SerializeField, Min(0)] private int generationPaddingInChunks = 1;
    [SerializeField] private int seed = 24680;
    [SerializeField] private TerrainGenerationSettings terrainSettings;

    [Header("Streaming")]
    [SerializeField, Min(1)] private int completedChunkGenerationsPerFrame = 4;
    [SerializeField, Min(1)] private int chunkColumnsMeshedPerFrame = 2;
    [SerializeField, Min(1)] private int chunkGenerationRequestsPerFrame = 32;
    [SerializeField, Min(1)] private int maxPendingChunkGenerations = 128;

    [Header("Interaction")]
    [SerializeField, Min(0.5f)] private float interactionDistance = 8f;
    [SerializeField] private Color selectionColor = new(1f, 0.85f, 0.2f, 1f);

    [Header("Debug")]
    [SerializeField] private Color chunkBoundaryColor = new(0.2f, 0.9f, 1f, 0.16f);

    private readonly List<Vector2Int> _pendingMeshKeyBuffer = new(64);
    private readonly Dictionary<Vector2Int, VoxelMesher.PendingChunkColumnMesh> _pendingChunkColumnMeshes = new();

    private TerrainData _terrain;
    private Transform _worldRoot;
    private FlyCamera _playerController;
    private Camera _resolvedInteractionCamera;
    private Material _runtimeFoliageMaterial;
    private ChunkView _chunkView;
    private WorldDebug _worldDebug;
    private WorldInteraction _worldInteraction;
    private WorldStreaming _worldStreaming;
    private NativeArray<ushort> _faceTextureLookup;

    public bool HasSelectedBlock => _worldInteraction != null && _worldInteraction.HasSelection;
    public bool HasSelection => _worldInteraction != null && _worldInteraction.HasSelection;
    public Vector3Int SelectedBlockPosition => _worldInteraction != null ? _worldInteraction.SelectedBlockPosition : default;
    public ushort SelectedContentId => _worldInteraction != null ? _worldInteraction.SelectedContentId : (ushort)0;
    public bool SelectedIsFoliage => _worldInteraction != null && _worldInteraction.SelectedIsFoliage;
    public string SelectedContentName => _worldInteraction != null ? _worldInteraction.SelectedContentName : "None";
    public BlockType SelectedBlockType => _worldInteraction != null ? _worldInteraction.GetSelectedBlockType() : BlockType.Air;
    public int RenderSizeInChunks => renderSizeInChunks;
    public TerrainData Terrain => _terrain;

    public bool TryGetSelectedContinentalness(out float continentalness)
    {
        continentalness = 0f;
        if (!HasSelection || _terrain == null)
        {
            return false;
        }

        Vector3Int position = SelectedBlockPosition;
        continentalness = _terrain.SampleContinentalness(position.x, position.z);
        return true;
    }

    public bool TryGetGndAt(int worldX, int worldZ, out float gnd)
    {
        gnd = 0f;
        if (_terrain == null)
        {
            return false;
        }

        gnd = _terrain.SampleGnd(worldX, worldZ);
        return true;
    }

    public bool TryGetContinentalnessAt(int worldX, int worldZ, out float continentalness)
    {
        continentalness = 0f;
        if (_terrain == null)
        {
            return false;
        }

        continentalness = _terrain.SampleContinentalness(worldX, worldZ);
        return true;
    }

    public bool TryGetWeirdnessAt(int worldX, int worldZ, out float weirdness)
    {
        weirdness = 0f;
        if (_terrain == null)
        {
            return false;
        }

        weirdness = _terrain.SampleWeirdness(worldX, worldZ);
        return true;
    }

    public bool TryGetVeinAt(int worldX, int worldZ, out float vein)
    {
        vein = 0f;
        if (_terrain == null)
        {
            return false;
        }

        vein = _terrain.SampleVein(worldX, worldZ);
        return true;
    }

    public bool TryGetErosionAt(int worldX, int worldZ, out float erosion)
    {
        erosion = 0f;
        if (_terrain == null)
        {
            return false;
        }

        erosion = _terrain.SampleErosion(worldX, worldZ);
        return true;
    }

    public bool TryGetReliefAt(int worldX, int worldZ, out float relief)
    {
        relief = 0f;
        if (_terrain == null)
        {
            return false;
        }

        relief = _terrain.SampleRelief(worldX, worldZ);
        return true;
    }

    public bool TryGetPeaksAndValleysAt(int worldX, int worldZ, out float peaksAndValleys)
    {
        peaksAndValleys = 0f;
        if (_terrain == null)
        {
            return false;
        }

        peaksAndValleys = _terrain.SamplePeaksAndValleys(worldX, worldZ);
        return true;
    }

    public bool TryGetVeinFoldAt(int worldX, int worldZ, out float veinFold)
    {
        veinFold = 0f;
        if (_terrain == null)
        {
            return false;
        }

        veinFold = _terrain.SampleVeinFold(worldX, worldZ);
        return true;
    }

    public bool TryGetTemperatureAt(int worldX, int worldZ, out float temperature)
    {
        temperature = 0f;
        if (_terrain == null)
        {
            return false;
        }

        temperature = _terrain.SampleTemperature(worldX, worldZ);
        return true;
    }

    public bool TryGetPrecipitationAt(int worldX, int worldZ, out float precipitation)
    {
        precipitation = 0f;
        if (_terrain == null)
        {
            return false;
        }

        precipitation = _terrain.SamplePrecipitation(worldX, worldZ);
        return true;
    }

    public bool TryGetBiomeAt(int worldX, int worldZ, out BiomeKind biome)
    {
        biome = BiomeKind.Land;
        if (_terrain == null)
        {
            return false;
        }

        biome = _terrain.SampleBiome(worldX, worldZ);
        return true;
    }

    public bool TryGetBiomeNameAt(int worldX, int worldZ, out string biomeName)
    {
        biomeName = string.Empty;
        if (_terrain == null)
        {
            return false;
        }

        biomeName = _terrain.SampleBiomeName(worldX, worldZ);
        return true;
    }

    private void Reset()
    {
        EnsureRenderSizeIsOdd();
        EnsureTerrainSettingsInitialized();
    }

    private void Awake()
    {
        EnsureTerrainSettingsInitialized();
        EnsureRenderSizeIsOdd();

        if (!ValidateSceneReferences())
        {
            enabled = false;
            return;
        }

        ApplyPerformanceDefaults();
        SetupDebugOverlay();
        ResolveInteractionCamera();
        ResolvePlayerController();
        EnsureFoliageMaterial();
        BuildFaceTextureLookup();
        _worldStreaming = new WorldStreaming();
        _chunkView = new ChunkView(worldMaterial, fluidMaterial, foliageMaterial, chunkColumnPrefab);
        BuildWorld();
        _worldDebug = new WorldDebug(transform, worldMaterial, selectionColor, chunkBoundaryColor);
        _worldDebug.Initialize();
        _worldInteraction = new WorldInteraction(
            blockDatabase,
            interactionDistance,
            () => _terrain,
            () => _worldRoot,
            () => _playerController,
            () => _resolvedInteractionCamera,
            (chunkX, chunkZ) =>
            {
                Vector2Int chunkCoords = new(chunkX, chunkZ);
                return _worldStreaming != null &&
                       _worldStreaming.IsChunkVisible(chunkCoords) &&
                       _chunkView != null &&
                       _chunkView.ContainsChunkColumn(chunkCoords);
            },
            RefreshLoadedSubChunk,
            (visible, position) => _worldDebug?.SetSelection(_worldRoot, visible, position));
    }

    private void Update()
    {
        if (_terrain == null)
        {
            return;
        }

        ResolveInteractionCamera();
        ResolvePlayerController();
        UpdateVisibleChunks();
        ProcessChunkGenerationRequests();
        CompleteChunkGenerationJobs();
        ProcessChunkRefreshQueue();
        CompletePendingChunkMeshJobs();
        _worldDebug?.HandleDebugInput(_playerController, _worldStreaming != null && _worldStreaming.HasCenterChunk, _worldStreaming != null ? _worldStreaming.CurrentCenterChunk : default);
        _worldInteraction?.HandlePlacementInput();
        _worldInteraction?.UpdateSelection();
        _worldInteraction?.HandleEditingInput();
    }

    private void OnValidate()
    {
        EnsureTerrainSettingsInitialized();
        EnsureRenderSizeIsOdd();
    }

    private void OnDestroy()
    {
        DisposePendingChunkMeshJobs();
        _chunkView?.DestroyAll();

        if (_runtimeFoliageMaterial != null)
        {
            Destroy(_runtimeFoliageMaterial);
        }

        _worldDebug?.Dispose();
        _worldDebug = null;
        _worldInteraction = null;
        _chunkView = null;
        _worldStreaming = null;

        _terrain?.Dispose();
        _terrain = null;

        if (_faceTextureLookup.IsCreated)
        {
            _faceTextureLookup.Dispose();
        }
    }

    private void BuildWorld()
    {
        _terrain?.Dispose();
        _terrain = new TerrainData(seed, terrainSettings);

        DisposePendingChunkMeshJobs();
        _chunkView?.DestroyAll();

        if (_worldRoot != null)
        {
            Destroy(_worldRoot.gameObject);
        }

        _worldRoot = new GameObject("Voxel World").transform;
        _worldRoot.SetParent(transform, false);
        _chunkView?.SetWorldRoot(_worldRoot);
        _chunkView?.PrewarmChunkColumnPool((renderSizeInChunks * renderSizeInChunks) + (4 * renderSizeInChunks));

        _worldStreaming?.Reset();
        UpdateVisibleChunks(force: true);
    }

    private void UpdateVisibleChunks(bool force = false)
    {
        Vector2Int centerChunk = GetCenterChunkCoordinates();
        _worldStreaming?.UpdateVisibleChunks(
            centerChunk,
            force,
            renderSizeInChunks,
            generationPaddingInChunks,
            (chunkX, chunkZ) => _terrain.IsChunkColumnReady(chunkX, chunkZ),
            ReleaseChunkColumn);

        if (_worldStreaming != null && _worldStreaming.HasCenterChunk)
        {
            _worldDebug?.UpdateChunkBoundaries(true, _worldStreaming.CurrentCenterChunk);
        }
    }

    private void ProcessChunkGenerationRequests()
    {
        if (_terrain == null || _worldStreaming == null)
        {
            return;
        }

        _worldStreaming.ProcessChunkRequests(
            chunkGenerationRequestsPerFrame,
            maxPendingChunkGenerations,
            _terrain.PendingChunkColumnCount,
            (chunkX, chunkZ) => _terrain.IsChunkColumnReady(chunkX, chunkZ),
            (chunkX, chunkZ) => _terrain.RequestChunkColumn(chunkX, chunkZ));
    }

    private void CompleteChunkGenerationJobs()
    {
        _worldStreaming?.CompleteChunkGenerationJobs(_terrain, completedChunkGenerationsPerFrame);
    }

    private void ProcessChunkRefreshQueue()
    {
        _worldStreaming?.ProcessChunkRefreshQueue(_terrain, chunkColumnsMeshedPerFrame, ScheduleChunkColumnMesh);
    }

    private void ScheduleChunkColumnMesh(int chunkX, int chunkZ, int usedSubChunkCount)
    {
        Vector2Int chunkCoords = new(chunkX, chunkZ);
        if (_pendingChunkColumnMeshes.ContainsKey(chunkCoords))
        {
            return;
        }

        VoxelMesher.PendingChunkColumnMesh pendingColumn = VoxelMesher.ScheduleChunkColumnMesh(
            _terrain,
            _faceTextureLookup,
            chunkX,
            chunkZ,
            usedSubChunkCount);

        _pendingChunkColumnMeshes.Add(chunkCoords, pendingColumn);
    }

    private void CompletePendingChunkMeshJobs()
    {
        _pendingMeshKeyBuffer.Clear();
        foreach (Vector2Int chunkCoords in _pendingChunkColumnMeshes.Keys)
        {
            _pendingMeshKeyBuffer.Add(chunkCoords);
        }

        SortPendingChunkMeshesByDistance(_pendingMeshKeyBuffer, _worldStreaming != null ? _worldStreaming.CurrentCenterChunk : default);

        int completedCount = 0;
        for (int i = 0; i < _pendingMeshKeyBuffer.Count && completedCount < chunkColumnsMeshedPerFrame; i++)
        {
            Vector2Int chunkCoords = _pendingMeshKeyBuffer[i];
            if (!_pendingChunkColumnMeshes.TryGetValue(chunkCoords, out VoxelMesher.PendingChunkColumnMesh pendingColumn) || !pendingColumn.IsCompleted)
            {
                continue;
            }

            ApplyPendingChunkColumnMesh(chunkCoords, pendingColumn);
            pendingColumn.Dispose();
            _pendingChunkColumnMeshes.Remove(chunkCoords);
            completedCount++;
        }
    }

    private void ApplyPendingChunkColumnMesh(Vector2Int chunkCoords, VoxelMesher.PendingChunkColumnMesh pendingColumn)
    {
        if (_chunkView == null)
        {
            return;
        }

        if (_worldStreaming == null || !_worldStreaming.IsChunkVisible(chunkCoords))
        {
            return;
        }

        _chunkView.ApplyPendingChunkColumnMesh(
            chunkCoords,
            pendingColumn,
            _terrain,
            blockDatabase);
    }

    private void ReleaseChunkColumn(Vector2Int chunkCoords)
    {
        if (_pendingChunkColumnMeshes.TryGetValue(chunkCoords, out VoxelMesher.PendingChunkColumnMesh pendingColumn))
        {
            pendingColumn.Dispose();
            _pendingChunkColumnMeshes.Remove(chunkCoords);
        }

        _chunkView?.ReleaseChunkColumn(chunkCoords);
    }

    private void DisposePendingChunkMeshJobs()
    {
        foreach (KeyValuePair<Vector2Int, VoxelMesher.PendingChunkColumnMesh> pair in _pendingChunkColumnMeshes)
        {
            pair.Value.Dispose();
        }

        _pendingChunkColumnMeshes.Clear();
    }

    private void RefreshLoadedSubChunk(int chunkX, int subChunkY, int chunkZ)
    {
        Vector2Int chunkCoords = new(chunkX, chunkZ);
        if (_pendingChunkColumnMeshes.TryGetValue(chunkCoords, out VoxelMesher.PendingChunkColumnMesh pendingColumn))
        {
            pendingColumn.Dispose();
            _pendingChunkColumnMeshes.Remove(chunkCoords);
        }

        _chunkView?.RefreshLoadedSubChunk(chunkX, subChunkY, chunkZ, _terrain, blockDatabase);
    }

    private Vector2Int GetCenterChunkCoordinates()
    {
        Vector3 focusPosition = transform.position;
        if (_playerController != null)
        {
            focusPosition = _playerController.transform.position;
        }
        else if (_resolvedInteractionCamera != null)
        {
            focusPosition = _resolvedInteractionCamera.transform.position;
        }

        if (_worldRoot != null)
        {
            focusPosition = _worldRoot.InverseTransformPoint(focusPosition);
        }

        return new Vector2Int(
            FloorDiv(Mathf.FloorToInt(focusPosition.x), TerrainData.ChunkSize),
            FloorDiv(Mathf.FloorToInt(focusPosition.z), TerrainData.ChunkSize));
    }

    private void ResolveInteractionCamera()
    {
        if (interactionCamera != null)
        {
            _resolvedInteractionCamera = interactionCamera;
            return;
        }

        if (_resolvedInteractionCamera == null)
        {
            _resolvedInteractionCamera = Camera.main;
        }
    }

    private void ResolvePlayerController()
    {
        if (_playerController == null)
        {
            _playerController = FindAnyObjectByType<FlyCamera>();
        }
    }

    private void BuildFaceTextureLookup()
    {
        if (_faceTextureLookup.IsCreated)
        {
            _faceTextureLookup.Dispose();
        }

        if (blockDatabase == null)
        {
            return;
        }

        int lookupLength = (blockDatabase.MaxBlockId + 1) * 6;
        _faceTextureLookup = new NativeArray<ushort>(lookupLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        for (ushort blockId = 0; blockId <= blockDatabase.MaxBlockId; blockId++)
        {
            if (!blockDatabase.HasDefinition(blockId))
            {
                continue;
            }

            for (int face = 0; face < 6; face++)
            {
                _faceTextureLookup[(blockId * 6) + face] = blockDatabase.GetFaceTextureLayer(blockId, (BlockFace)face);
            }
        }
    }

    private static void SortPendingChunkMeshesByDistance(List<Vector2Int> chunkCoords, Vector2Int centerChunk)
    {
        chunkCoords.Sort((a, b) =>
        {
            int aDx = a.x - centerChunk.x;
            int aDz = a.y - centerChunk.y;
            int bDx = b.x - centerChunk.x;
            int bDz = b.y - centerChunk.y;

            int aDistance = (aDx * aDx) + (aDz * aDz);
            int bDistance = (bDx * bDx) + (bDz * bDz);
            int distanceComparison = aDistance.CompareTo(bDistance);
            if (distanceComparison != 0)
            {
                return distanceComparison;
            }

            int zComparison = a.y.CompareTo(b.y);
            return zComparison != 0 ? zComparison : a.x.CompareTo(b.x);
        });
    }

    private static int FloorDiv(int value, int divisor)
    {
        if (value >= 0)
        {
            return value / divisor;
        }

        return ((value + 1) / divisor) - 1;
    }

    private static void ApplyPerformanceDefaults()
    {
        GraphicsSettings.useScriptableRenderPipelineBatching = true;
        QualitySettings.shadows = UnityEngine.ShadowQuality.All;
        QualitySettings.shadowDistance = 120f;
        QualitySettings.shadowResolution = UnityEngine.ShadowResolution.High;
        QualitySettings.shadowProjection = ShadowProjection.StableFit;
        QualitySettings.shadowCascades = 2;
        QualitySettings.pixelLightCount = 0;
        QualitySettings.realtimeReflectionProbes = false;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
        RenderSettings.fog = false;
    }

    private bool ValidateSceneReferences()
    {
        bool isValid = true;

        if (worldMaterial == null)
        {
            Debug.LogError("WorldRuntime requires a World Material reference.", this);
            isValid = false;
        }

        if (fluidMaterial == null)
        {
            Debug.LogError("WorldRuntime requires a Fluid Material reference.", this);
            isValid = false;
        }

        if (blockDatabase == null)
        {
            Debug.LogError("WorldRuntime requires a Block Database reference.", this);
            isValid = false;
        }

        if (!terrainSettings.IsInitialized)
        {
            Debug.LogError("WorldRuntime requires valid terrain generation settings.", this);
            isValid = false;
        }

        if (worldMaterial != null && !worldMaterial.HasProperty("_BlockTextures"))
        {
            Debug.LogError("WorldRuntime world material must use a shader with a _BlockTextures Texture2DArray property.", this);
            isValid = false;
        }

        if (worldMaterial != null)
        {
            Texture texture = worldMaterial.GetTexture("_BlockTextures");
            if (texture is not Texture2DArray textureArray)
            {
                Debug.LogError("WorldRuntime world material requires a Texture2DArray assigned to _BlockTextures.", this);
                isValid = false;
            }
            else if (blockDatabase != null && blockDatabase.HighestTextureLayer >= textureArray.depth)
            {
                Debug.LogError(
                    $"WorldRuntime block database references texture layer {blockDatabase.HighestTextureLayer}, but the assigned Texture2DArray only has {textureArray.depth} layers.",
                    this);
                isValid = false;
            }
        }

        if (blockDatabase != null)
        {
            isValid &= ValidateRequiredBlockDefinition((ushort)BlockType.Rock);
            isValid &= ValidateRequiredBlockDefinition((ushort)BlockType.Bedrock);
        }

        return isValid;
    }

    private void SetupDebugOverlay()
    {
        WorldDebugOverlay overlay = FindAnyObjectByType<WorldDebugOverlay>();
        if (overlay == null)
        {
            overlay = gameObject.AddComponent<WorldDebugOverlay>();
        }

        overlay.BindWorldRuntime(this);
    }

    private bool ValidateRequiredBlockDefinition(ushort blockId)
    {
        if (blockDatabase.HasDefinition(blockId))
        {
            return true;
        }

        Debug.LogError($"WorldRuntime block database is missing required block id {blockId}.", this);
        return false;
    }

    private void EnsureTerrainSettingsInitialized()
    {
        if (terrainSettings.TerraWorldGen == null)
        {
            terrainSettings = TerrainGenerationSettings.Default;
        }
    }

    private void EnsureRenderSizeIsOdd()
    {
        if (renderSizeInChunks < 1)
        {
            renderSizeInChunks = 1;
        }

        if ((renderSizeInChunks & 1) == 0)
        {
            renderSizeInChunks += 1;
        }
    }

    private void EnsureFoliageMaterial()
    {
        if (foliageMaterial != null || worldMaterial == null)
        {
            return;
        }

        Shader foliageShader = Shader.Find("YD/Voxel Texture Array Cutout");
        if (foliageShader == null)
        {
            return;
        }

        _runtimeFoliageMaterial = new Material(foliageShader)
        {
            name = "Runtime Foliage Material",
        };

        if (worldMaterial.HasProperty("_BlockTextures"))
        {
            _runtimeFoliageMaterial.SetTexture("_BlockTextures", worldMaterial.GetTexture("_BlockTextures"));
        }

        if (worldMaterial.HasProperty("_BaseColor"))
        {
            _runtimeFoliageMaterial.SetColor("_BaseColor", worldMaterial.GetColor("_BaseColor"));
        }

        foliageMaterial = _runtimeFoliageMaterial;
    }
}

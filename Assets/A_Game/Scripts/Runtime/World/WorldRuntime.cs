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
    [SerializeField] private WorldGenSettingsAsset worldGenSettingsAsset;
    [FormerlySerializedAs("continentalnessCdfProfileAsset")]
    [SerializeField] private WorldGenRemapProfileAsset worldGenRemapProfileAsset;

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

    [Header("Fluid Reflection")]
    [SerializeField] private bool enablePlanarReflection = true;
    [SerializeField, Range(0.25f, 1f)] private float planarReflectionRenderScale = 0.5f;
    [SerializeField] private LayerMask planarReflectionCullingMask = ~0;
    [SerializeField, Min(0f)] private float planarReflectionClipPlaneOffset = 0.05f;
    [SerializeField] private bool planarReflectionRenderShadows = false;
    [SerializeField] private bool planarReflectionAllowHdr = false;
    [SerializeField, Min(10f)] private float planarReflectionFarClip = 180f;
    [SerializeField, Min(1)] private int planarReflectionUpdateInterval = 2;

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
    private WaterReflection _waterReflection;
    private WorldStreaming _worldStreaming;
    private NativeArray<ushort> _faceTextureLookup;
    private TerrainGenerationSettings terrainSettings;
    private bool _visibleFluidRendererCacheDirty = true;
    private SplineTreeAsset _fallbackOffsetSplineTree;
    private SplineTreeAsset _fallbackFactorSplineTree;
    private SplineTreeAsset _fallbackJaggednessSplineTree;

    public bool HasSelectedBlock => _worldInteraction != null && _worldInteraction.HasSelection;
    public bool HasSelection => _worldInteraction != null && _worldInteraction.HasSelection;
    public Vector3Int SelectedBlockPosition => _worldInteraction != null ? _worldInteraction.SelectedBlockPosition : default;
    public ushort SelectedContentId => _worldInteraction != null ? _worldInteraction.SelectedContentId : (ushort)0;
    public bool SelectedIsFoliage => _worldInteraction != null && _worldInteraction.SelectedIsFoliage;
    public string SelectedContentKindLabel => _worldInteraction != null ? _worldInteraction.SelectedContentKindLabel : "None";
    public string SelectedContentName => _worldInteraction != null ? _worldInteraction.SelectedContentName : "None";
    public BlockType SelectedBlockType => _worldInteraction != null ? _worldInteraction.GetSelectedBlockType() : BlockType.Air;
    public int RenderSizeInChunks => renderSizeInChunks;
    public TerrainData Terrain => _terrain;

    public bool TryGetWorldGenDebugInfo(out Vector2Int position, out TerrainData.WorldGenDebugSample sample)
    {
        position = default;
        sample = default;

        if (_terrain == null)
        {
            return false;
        }

        Vector3 samplePosition;
        if (_playerController != null)
        {
            samplePosition = _playerController.transform.position;
        }
        else if (_resolvedInteractionCamera != null)
        {
            samplePosition = _resolvedInteractionCamera.transform.position;
        }
        else
        {
            return false;
        }

        if (_worldRoot != null)
        {
            samplePosition = _worldRoot.InverseTransformPoint(samplePosition);
        }

        position = new Vector2Int(Mathf.FloorToInt(samplePosition.x), Mathf.FloorToInt(samplePosition.z));
        sample = _terrain.SampleWorldGen(position.x, position.y);
        return true;
    }

    private void Reset()
    {
        EnsureRenderSizeIsOdd();
        EnsureTerrainSettingsInitialized();
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
        _waterReflection?.ReleaseResources();
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
        _waterReflection = new WaterReflection(transform, fluidMaterial);
        ConfigureWaterReflection();
        _waterReflection.ApplyFallback();
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
        RefreshVisibleFluidRendererCacheIfNeeded();
        _worldDebug?.HandleDebugInput(_playerController, _worldStreaming != null && _worldStreaming.HasCenterChunk, _worldStreaming != null ? _worldStreaming.CurrentCenterChunk : default);
        _worldInteraction?.HandlePlacementInput();
        _worldInteraction?.UpdateSelection();
        _worldInteraction?.HandleEditingInput();
    }

    private void OnValidate()
    {
        EnsureTerrainSettingsInitialized();
        EnsureRenderSizeIsOdd();
        ConfigureWaterReflection();
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
        _waterReflection?.ReleaseResources();
        _waterReflection = null;
        _chunkView = null;
        _worldStreaming = null;
        DestroyFallbackSplineTrees();

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
        _terrain = new TerrainData(
            seed,
            terrainSettings,
            GetContinentalnessRemapLut(),
            GetErosionRemapLut(),
            GetRidgesRemapLut(),
            GetOffsetSplineNodes(),
            GetOffsetSplinePoints(),
            GetFactorSplineNodes(),
            GetFactorSplinePoints(),
            GetJaggednessSplineNodes(),
            GetJaggednessSplinePoints(),
            GetLegacyTerrainHeightSplineNodes(),
            GetLegacyTerrainHeightSplinePoints(),
            GetContinentalnessHeightLut());

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
        _waterReflection?.ResetVisibleFluidRenderers();
        _visibleFluidRendererCacheDirty = true;
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
        MarkVisibleFluidRendererCacheDirty();
    }

    private void ReleaseChunkColumn(Vector2Int chunkCoords)
    {
        if (_pendingChunkColumnMeshes.TryGetValue(chunkCoords, out VoxelMesher.PendingChunkColumnMesh pendingColumn))
        {
            pendingColumn.Dispose();
            _pendingChunkColumnMeshes.Remove(chunkCoords);
        }

        _chunkView?.ReleaseChunkColumn(chunkCoords);
        MarkVisibleFluidRendererCacheDirty();
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
        MarkVisibleFluidRendererCacheDirty();
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

    private void ConfigureWaterReflection()
    {
        if (_waterReflection == null)
        {
            return;
        }

        _waterReflection.UpdateSettings(
            enablePlanarReflection,
            planarReflectionRenderScale,
            planarReflectionCullingMask,
            planarReflectionClipPlaneOffset,
            planarReflectionRenderShadows,
            planarReflectionAllowHdr,
            planarReflectionFarClip,
            planarReflectionUpdateInterval);
    }

    private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (_terrain == null || _waterReflection == null)
        {
            return;
        }

        RefreshVisibleFluidRendererCacheIfNeeded();
        ResolveInteractionCamera();
        _waterReflection.TryRender(
            camera,
            _resolvedInteractionCamera,
            transform.position.y,
            () => _worldDebug != null ? _worldDebug.HideForReflection() : default,
            visibility => _worldDebug?.RestoreAfterReflection(visibility));
    }

    private void MarkVisibleFluidRendererCacheDirty()
    {
        _visibleFluidRendererCacheDirty = true;
    }

    private void RefreshVisibleFluidRendererCacheIfNeeded()
    {
        if (!_visibleFluidRendererCacheDirty || _waterReflection == null)
        {
            return;
        }

        _chunkView?.PopulateVisibleFluidRenderers(_waterReflection);
        _visibleFluidRendererCacheDirty = false;
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

        if (worldGenSettingsAsset == null)
        {
            Debug.LogError("WorldRuntime requires a World Gen Settings Asset reference.", this);
            isValid = false;
        }
        else if (!terrainSettings.IsInitialized)
        {
            Debug.LogError("WorldRuntime requires a valid initialized World Gen Settings Asset.", this);
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
        if (worldGenSettingsAsset != null)
        {
            terrainSettings = TerrainGenerationSettings.FromWorldGenSettings(
                worldGenSettingsAsset,
                UseContinentalnessRemap(),
                UseErosionRemap(),
                UseRidgesRemap());
            return;
        }

        terrainSettings = default;
    }

    private bool UseContinentalnessRemap()
    {
        return worldGenSettingsAsset != null &&
               worldGenSettingsAsset.UseContinentalnessRemap &&
               worldGenRemapProfileAsset != null &&
               worldGenRemapProfileAsset.HasContinentalnessRemap;
    }

    private float[] GetContinentalnessRemapLut()
    {
        return UseContinentalnessRemap()
            ? worldGenRemapProfileAsset.BakedContinentalnessRemapLut
            : null;
    }

    private bool UseErosionRemap()
    {
        return worldGenSettingsAsset != null &&
               worldGenSettingsAsset.UseErosionRemap &&
               worldGenRemapProfileAsset != null &&
               worldGenRemapProfileAsset.HasErosionRemap;
    }

    private float[] GetErosionRemapLut()
    {
        return UseErosionRemap()
            ? worldGenRemapProfileAsset.BakedErosionRemapLut
            : null;
    }

    private bool UseRidgesRemap()
    {
        return worldGenSettingsAsset != null &&
               worldGenSettingsAsset.UseRidgesRemap &&
               worldGenRemapProfileAsset != null &&
               worldGenRemapProfileAsset.HasRidgesRemap;
    }

    private float[] GetRidgesRemapLut()
    {
        return UseRidgesRemap()
            ? worldGenRemapProfileAsset.BakedRidgesRemapLut
            : null;
    }

    private SplineTreeBakedNode[] GetOffsetSplineNodes()
    {
        SplineTreeAsset asset = GetOffsetSplineTreeAsset();
        return asset != null ? asset.BakedNodes : null;
    }

    private SplineTreeBakedPoint[] GetOffsetSplinePoints()
    {
        SplineTreeAsset asset = GetOffsetSplineTreeAsset();
        return asset != null ? asset.BakedPoints : null;
    }

    private SplineTreeBakedNode[] GetFactorSplineNodes()
    {
        SplineTreeAsset asset = GetFactorSplineTreeAsset();
        return asset != null ? asset.BakedNodes : null;
    }

    private SplineTreeBakedPoint[] GetFactorSplinePoints()
    {
        SplineTreeAsset asset = GetFactorSplineTreeAsset();
        return asset != null ? asset.BakedPoints : null;
    }

    private SplineTreeBakedNode[] GetJaggednessSplineNodes()
    {
        SplineTreeAsset asset = GetJaggednessSplineTreeAsset();
        return asset != null ? asset.BakedNodes : null;
    }

    private SplineTreeBakedPoint[] GetJaggednessSplinePoints()
    {
        SplineTreeAsset asset = GetJaggednessSplineTreeAsset();
        return asset != null ? asset.BakedPoints : null;
    }

    private SplineTreeBakedNode[] GetLegacyTerrainHeightSplineNodes()
    {
        return UseLegacyTerrainHeightSplineTree()
            ? worldGenSettingsAsset.LegacyTerrainHeightSplineTree.BakedNodes
            : null;
    }

    private SplineTreeBakedPoint[] GetLegacyTerrainHeightSplinePoints()
    {
        return UseLegacyTerrainHeightSplineTree()
            ? worldGenSettingsAsset.LegacyTerrainHeightSplineTree.BakedPoints
            : null;
    }

    private float[] GetContinentalnessHeightLut()
    {
        return worldGenSettingsAsset != null &&
               worldGenSettingsAsset.LegacyContinentalnessHeightSpline != null &&
               worldGenSettingsAsset.LegacyContinentalnessHeightSpline.HasBakedLut
            ? worldGenSettingsAsset.LegacyContinentalnessHeightSpline.BakedLut
            : null;
    }

    private bool UseLegacyTerrainHeightSplineTree()
    {
        return worldGenSettingsAsset != null &&
               worldGenSettingsAsset.LegacyTerrainHeightSplineTree != null &&
               worldGenSettingsAsset.LegacyTerrainHeightSplineTree.HasBakedTree;
    }

    private SplineTreeAsset GetOffsetSplineTreeAsset()
    {
        if (worldGenSettingsAsset != null &&
            worldGenSettingsAsset.OffsetSplineTree != null &&
            worldGenSettingsAsset.OffsetSplineTree.HasBakedTree)
        {
            return worldGenSettingsAsset.OffsetSplineTree;
        }

        EnsureFallbackSplineTrees();
        return _fallbackOffsetSplineTree;
    }

    private SplineTreeAsset GetFactorSplineTreeAsset()
    {
        if (worldGenSettingsAsset != null &&
            worldGenSettingsAsset.FactorSplineTree != null &&
            worldGenSettingsAsset.FactorSplineTree.HasBakedTree)
        {
            return worldGenSettingsAsset.FactorSplineTree;
        }

        EnsureFallbackSplineTrees();
        return _fallbackFactorSplineTree;
    }

    private SplineTreeAsset GetJaggednessSplineTreeAsset()
    {
        if (worldGenSettingsAsset != null &&
            worldGenSettingsAsset.JaggednessSplineTree != null &&
            worldGenSettingsAsset.JaggednessSplineTree.HasBakedTree)
        {
            return worldGenSettingsAsset.JaggednessSplineTree;
        }

        EnsureFallbackSplineTrees();
        return _fallbackJaggednessSplineTree;
    }

    private void EnsureFallbackSplineTrees()
    {
        if (_fallbackOffsetSplineTree == null)
        {
            _fallbackOffsetSplineTree = CreateFallbackSplineTree(asset => asset.ResetToVanillaOverworldOffsetTree());
        }

        if (_fallbackFactorSplineTree == null)
        {
            _fallbackFactorSplineTree = CreateFallbackSplineTree(asset => asset.ResetToVanillaOverworldFactorTree());
        }

        if (_fallbackJaggednessSplineTree == null)
        {
            _fallbackJaggednessSplineTree = CreateFallbackSplineTree(asset => asset.ResetToVanillaOverworldJaggednessTree());
        }
    }

    private static SplineTreeAsset CreateFallbackSplineTree(System.Action<SplineTreeAsset> initializer)
    {
        SplineTreeAsset asset = ScriptableObject.CreateInstance<SplineTreeAsset>();
        initializer(asset);
        asset.Bake();
        return asset;
    }

    private void DestroyFallbackSplineTrees()
    {
        DestroySplineTree(ref _fallbackOffsetSplineTree);
        DestroySplineTree(ref _fallbackFactorSplineTree);
        DestroySplineTree(ref _fallbackJaggednessSplineTree);
    }

    private static void DestroySplineTree(ref SplineTreeAsset asset)
    {
        if (asset == null)
        {
            return;
        }

        Destroy(asset);
        asset = null;
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

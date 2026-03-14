using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public sealed class VoxelWorldRuntime : MonoBehaviour
{
    private struct SelectionHit
    {
        public Vector3Int position;
        public Vector3Int placementPosition;
        public ushort contentId;
        public bool isFoliage;
    }

    private sealed class SubChunkInstance
    {
        public GameObject GameObject;
        public MeshFilter MeshFilter;
        public MeshRenderer MeshRenderer;
        public MeshCollider MeshCollider;
        public Mesh Mesh;
    }

    private sealed class ChunkColumnInstance
    {
        public GameObject Root;
        public SubChunkInstance[] SolidSubChunks = new SubChunkInstance[VoxelTerrainData.SubChunkCountY];
        public SubChunkInstance[] FluidSubChunks = new SubChunkInstance[VoxelTerrainData.SubChunkCountY];
        public SubChunkInstance[] FoliageSubChunks = new SubChunkInstance[VoxelTerrainData.SubChunkCountY];
    }

    [Header("Scene References")]
    [SerializeField] private Material worldMaterial;
    [SerializeField] private Material fluidMaterial;
    [SerializeField] private Material foliageMaterial;
    [SerializeField] private BlockDatabase blockDatabase;
    [SerializeField] private BiomeGraph worldBiomeGraph;
    [SerializeField] private Camera interactionCamera;
    [SerializeField] private GameObject chunkColumnPrefab;

    [Header("Terrain")]
    [FormerlySerializedAs("worldSizeInChunks")]
    [SerializeField, Min(1)] private int renderSizeInChunks = 9;
    [SerializeField, Min(0)] private int generationPaddingInChunks = 1;
    [SerializeField] private int seed = 24680;
    [SerializeField] private WorldGenSettingsAsset worldGenSettingsAsset;
    [SerializeField] private ContinentalnessCdfProfileAsset continentalnessCdfProfileAsset;

    [Header("Streaming")]
    [SerializeField, Min(1)] private int completedChunkGenerationsPerFrame = 4;
    [SerializeField, Min(1)] private int chunkColumnsMeshedPerFrame = 2;

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

    private readonly List<Vector2Int> _completedChunkBuffer = new(16);
    private readonly List<Vector2Int> _pendingMeshKeyBuffer = new(64);
    private readonly List<Vector2Int> _visibleChunkKeyBuffer = new(256);
    private readonly List<Vector3Int> _subChunkRefreshBuffer = new(7);
    private readonly List<Vector2Int> _chunkPriorityBuffer = new(256);
    private readonly List<MeshRenderer> _reflectionHiddenRenderers = new(128);
    private readonly List<MeshRenderer> _visibleFluidRenderers = new(128);
    private readonly Plane[] _cameraFrustumPlanes = new Plane[6];
    private readonly Dictionary<Vector2Int, VoxelMesher.PendingChunkColumnMesh> _pendingChunkColumnMeshes = new();
    private readonly HashSet<Vector2Int> _queuedChunkColumns = new();
    private readonly HashSet<Vector2Int> _targetVisibleChunks = new();
    private readonly HashSet<Vector2Int> _visibleChunkColumns = new();
    private readonly List<Vector2Int> _chunkRefreshQueue = new(256);
    private readonly RaycastHit[] _interactionHits = new RaycastHit[16];
    private readonly Dictionary<Vector2Int, ChunkColumnInstance> _chunkColumnInstances = new();
    private readonly Stack<ChunkColumnInstance> _chunkColumnPool = new();

    private VoxelTerrainData _terrain;
    private Transform _worldRoot;
    private GameObject _selectionOutline;
    private Material _selectionMaterial;
    private GameObject _chunkBoundaryRoot;
    private Material _chunkBoundaryMaterial;
    private Mesh _chunkBoundaryMesh;
    private readonly GameObject[] _chunkBoundaryPlanes = new GameObject[8];
    private VoxelFlyCamera _playerController;
    private Camera _resolvedInteractionCamera;
    private Camera _planarReflectionCamera;
    private Skybox _planarReflectionSkybox;
    private UniversalAdditionalCameraData _planarReflectionCameraData;
    private UniversalRenderPipeline.SingleCameraRequest _planarReflectionRequest;
    private RenderTexture _planarReflectionTexture;
    private Material _runtimeFoliageMaterial;
    private NativeArray<ushort> _faceTextureLookup;
    private Vector3Int _selectedBlockPosition;
    private Vector3Int _placementBlockPosition;
    private ushort _selectedContentId;
    private bool _selectedIsFoliage;
    private Vector2Int _currentCenterChunk;
    private bool _hasSelection;
    private bool _hasCenterChunk;
    private bool _chunkBoundariesVisible;
    private bool _isRenderingPlanarReflection;
    private int _lastPlanarReflectionFrame = -999999;
    private BlockType _selectedPlacementBlock = BlockType.Dirt;
    private VoxelTerrainGenerationSettings terrainSettings;

    private static readonly int PlanarReflectionTextureId = Shader.PropertyToID("_PlanarReflectionTex");
    private static readonly int PlanarReflectionVpId = Shader.PropertyToID("_PlanarReflectionVP");

    public bool HasSelectedBlock => _hasSelection;
    public bool HasSelection => _hasSelection;
    public Vector3Int SelectedBlockPosition => _selectedBlockPosition;
    public ushort SelectedContentId => _hasSelection ? _selectedContentId : (ushort)0;
    public bool SelectedIsFoliage => _hasSelection && _selectedIsFoliage;
    public string SelectedContentKindLabel => !_hasSelection ? "None" : (_selectedIsFoliage ? "Foliage" : "Block");
    public string SelectedContentName => !_hasSelection || blockDatabase == null ? "None" : blockDatabase.GetDisplayName(_selectedContentId);
    public BlockType SelectedBlockType =>
        _hasSelection && !_selectedIsFoliage && _terrain != null
            ? _terrain.GetBlock(_selectedBlockPosition.x, _selectedBlockPosition.y, _selectedBlockPosition.z)
            : BlockType.Air;
    public int RenderSizeInChunks => renderSizeInChunks;
    public int SeaLevel => terrainSettings.seaLevel;

    public bool TryGetWorldGenDebugInfo(out Vector2Int position, out VoxelTerrainData.WorldGenDebugSample sample)
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

    public string GetWorldGenBiomeLabel(float temperature, float precipitation)
    {
        if (worldBiomeGraph != null && worldBiomeGraph.TryGetBiome(temperature, precipitation, out BiomeGraphEntry entry))
        {
            return string.IsNullOrWhiteSpace(entry.BiomeName) ? "Unnamed" : entry.BiomeName;
        }

        return "Unassigned";
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
        ReleasePlanarReflectionResources();
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
        BuildWorld();
        ApplyPlanarReflectionFallback();
        CreateSelectionOutline();
        CreateChunkBoundaryDebug();
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
        CompleteChunkGenerationJobs();
        ProcessChunkRefreshQueue();
        CompletePendingChunkMeshJobs();
        HandleDebugInput();
        HandleBlockSelectionInput();
        UpdateSelection();
        HandleBlockEditingInput();
    }

    private void OnValidate()
    {
        EnsureTerrainSettingsInitialized();
        EnsureRenderSizeIsOdd();
    }

    private void OnDestroy()
    {
        DisposePendingChunkMeshJobs();
        DestroyAllChunkColumnInstances();
        ReleasePlanarReflectionResources();

        if (_selectionOutline != null)
        {
            Mesh selectionMesh = _selectionOutline.GetComponent<MeshFilter>()?.sharedMesh;
            if (selectionMesh != null)
            {
                Destroy(selectionMesh);
            }
        }

        if (_selectionMaterial != null)
        {
            Destroy(_selectionMaterial);
        }

        if (_runtimeFoliageMaterial != null)
        {
            Destroy(_runtimeFoliageMaterial);
        }

        if (_chunkBoundaryMesh != null)
        {
            Destroy(_chunkBoundaryMesh);
        }

        if (_chunkBoundaryMaterial != null)
        {
            Destroy(_chunkBoundaryMaterial);
        }

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
        _terrain = new VoxelTerrainData(seed, terrainSettings, GetContinentalnessCdfLut());

        DisposePendingChunkMeshJobs();
        DestroyAllChunkColumnInstances();

        if (_worldRoot != null)
        {
            Destroy(_worldRoot.gameObject);
        }

        _worldRoot = new GameObject("Voxel World").transform;
        _worldRoot.SetParent(transform, false);

        _visibleChunkColumns.Clear();
        _queuedChunkColumns.Clear();
        _chunkRefreshQueue.Clear();
        _visibleFluidRenderers.Clear();
        _hasCenterChunk = false;
        UpdateVisibleChunks(force: true);
    }

    private void UpdateVisibleChunks(bool force = false)
    {
        Vector2Int centerChunk = GetCenterChunkCoordinates();
        if (!force && _hasCenterChunk && centerChunk == _currentCenterChunk)
        {
            return;
        }

        _currentCenterChunk = centerChunk;
        _hasCenterChunk = true;
        UpdateChunkBoundaryDebug();

        _targetVisibleChunks.Clear();
        int radius = renderSizeInChunks / 2;
        for (int offsetZ = -radius; offsetZ <= radius; offsetZ++)
        {
            for (int offsetX = -radius; offsetX <= radius; offsetX++)
            {
                _targetVisibleChunks.Add(new Vector2Int(centerChunk.x + offsetX, centerChunk.y + offsetZ));
            }
        }

        RequestChunksAroundCenter(centerChunk, radius + generationPaddingInChunks);

        _visibleChunkKeyBuffer.Clear();
        foreach (Vector2Int chunkCoords in _visibleChunkColumns)
        {
            _visibleChunkKeyBuffer.Add(chunkCoords);
        }

        for (int i = 0; i < _visibleChunkKeyBuffer.Count; i++)
        {
            Vector2Int chunkCoords = _visibleChunkKeyBuffer[i];
            if (_targetVisibleChunks.Contains(chunkCoords))
            {
                continue;
            }

            ReleaseChunkColumn(chunkCoords);
            _visibleChunkColumns.Remove(chunkCoords);
        }

        GetSortedChunkCoordinates(_targetVisibleChunks, centerChunk, _chunkPriorityBuffer);
        for (int i = 0; i < _chunkPriorityBuffer.Count; i++)
        {
            Vector2Int chunkCoords = _chunkPriorityBuffer[i];
            if (!_visibleChunkColumns.Add(chunkCoords))
            {
                continue;
            }

            if (_terrain.IsChunkColumnReady(chunkCoords.x, chunkCoords.y))
            {
                QueueChunkColumnRefresh(chunkCoords);
            }
        }
    }

    private void RequestChunksAroundCenter(Vector2Int centerChunk, int radius)
    {
        _chunkPriorityBuffer.Clear();
        for (int offsetZ = -radius; offsetZ <= radius; offsetZ++)
        {
            for (int offsetX = -radius; offsetX <= radius; offsetX++)
            {
                _chunkPriorityBuffer.Add(new Vector2Int(centerChunk.x + offsetX, centerChunk.y + offsetZ));
            }
        }

        SortChunkCoordinatesByDistance(_chunkPriorityBuffer, centerChunk);
        for (int i = 0; i < _chunkPriorityBuffer.Count; i++)
        {
            Vector2Int chunkCoords = _chunkPriorityBuffer[i];
            _terrain.RequestChunkColumn(chunkCoords.x, chunkCoords.y);
        }
    }

    private void CompleteChunkGenerationJobs()
    {
        _completedChunkBuffer.Clear();
        _terrain.CompletePendingChunkColumns(_completedChunkBuffer, completedChunkGenerationsPerFrame);

        for (int i = 0; i < _completedChunkBuffer.Count; i++)
        {
            Vector2Int chunkCoords = _completedChunkBuffer[i];
            if (!_visibleChunkColumns.Contains(chunkCoords))
            {
                continue;
            }

            QueueChunkAndNeighborsForRefresh(chunkCoords);
        }
    }

    private void ProcessChunkRefreshQueue()
    {
        int processedCount = 0;
        while (_chunkRefreshQueue.Count > 0 && processedCount < chunkColumnsMeshedPerFrame)
        {
            Vector2Int chunkCoords = _chunkRefreshQueue[0];
            _chunkRefreshQueue.RemoveAt(0);
            _queuedChunkColumns.Remove(chunkCoords);

            if (!_visibleChunkColumns.Contains(chunkCoords))
            {
                continue;
            }

            if (!_terrain.TryGetUsedSubChunkCount(chunkCoords.x, chunkCoords.y, out int usedSubChunkCount))
            {
                _terrain.RequestChunkColumn(chunkCoords.x, chunkCoords.y);
                continue;
            }

            ScheduleChunkColumnMesh(chunkCoords.x, chunkCoords.y, usedSubChunkCount);
            processedCount++;
        }
    }

    private void QueueChunkAndNeighborsForRefresh(Vector2Int chunkCoords)
    {
        QueueChunkColumnRefresh(chunkCoords);
        QueueChunkColumnRefresh(new Vector2Int(chunkCoords.x - 1, chunkCoords.y));
        QueueChunkColumnRefresh(new Vector2Int(chunkCoords.x + 1, chunkCoords.y));
        QueueChunkColumnRefresh(new Vector2Int(chunkCoords.x, chunkCoords.y - 1));
        QueueChunkColumnRefresh(new Vector2Int(chunkCoords.x, chunkCoords.y + 1));
    }

    private void QueueChunkColumnRefresh(Vector2Int chunkCoords)
    {
        if (!_visibleChunkColumns.Contains(chunkCoords))
        {
            return;
        }

        if (!_queuedChunkColumns.Add(chunkCoords))
        {
            return;
        }

        InsertChunkRefreshByPriority(chunkCoords);
    }

    private void InsertChunkRefreshByPriority(Vector2Int chunkCoords)
    {
        int insertIndex = _chunkRefreshQueue.Count;
        for (int i = 0; i < _chunkRefreshQueue.Count; i++)
        {
            if (CompareChunkPriority(chunkCoords, _chunkRefreshQueue[i], _currentCenterChunk) < 0)
            {
                insertIndex = i;
                break;
            }
        }

        _chunkRefreshQueue.Insert(insertIndex, chunkCoords);
    }

    private void GetSortedChunkCoordinates(HashSet<Vector2Int> source, Vector2Int centerChunk, List<Vector2Int> destination)
    {
        destination.Clear();
        foreach (Vector2Int chunkCoords in source)
        {
            destination.Add(chunkCoords);
        }

        SortChunkCoordinatesByDistance(destination, centerChunk);
    }

    private static void SortChunkCoordinatesByDistance(List<Vector2Int> chunkCoords, Vector2Int centerChunk)
    {
        chunkCoords.Sort((a, b) => CompareChunkPriority(a, b, centerChunk));
    }

    private static int CompareChunkPriority(Vector2Int a, Vector2Int b, Vector2Int centerChunk)
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
    }

    private void RefreshChunkColumn(int chunkX, int chunkZ, int usedSubChunkCount)
    {
        ChunkColumnInstance column = GetOrCreateChunkColumnInstance(chunkX, chunkZ);
        column.Root.SetActive(false);

        bool hasGeometry = false;
        for (int subChunkY = 0; subChunkY < VoxelTerrainData.SubChunkCountY; subChunkY++)
        {
            if (subChunkY >= usedSubChunkCount)
            {
                SetSubChunkVisible(column.SolidSubChunks[subChunkY], false);
                SetSubChunkVisible(column.FluidSubChunks[subChunkY], false);
                SetSubChunkVisible(column.FoliageSubChunks[subChunkY], false);
                continue;
            }

            hasGeometry |= RefreshSolidSubChunk(column, chunkX, subChunkY, chunkZ);
            hasGeometry |= RefreshFluidSubChunk(column, chunkX, subChunkY, chunkZ);
            hasGeometry |= RefreshFoliageSubChunk(column, chunkX, subChunkY, chunkZ);
        }

        column.Root.SetActive(hasGeometry);
        SyncSolidChunkColliders(column);
        RefreshVisibleFluidRendererCache();
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

        SortChunkCoordinatesByDistance(_pendingMeshKeyBuffer, _currentCenterChunk);

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
        if (!_visibleChunkColumns.Contains(chunkCoords))
        {
            return;
        }

        ChunkColumnInstance column = GetOrCreateChunkColumnInstance(chunkCoords.x, chunkCoords.y);
        column.Root.SetActive(false);

        bool hasGeometry = false;
        for (int subChunkY = 0; subChunkY < VoxelTerrainData.SubChunkCountY; subChunkY++)
        {
            SubChunkInstance solidSubChunk = column.SolidSubChunks[subChunkY];
            SubChunkInstance fluidSubChunk = column.FluidSubChunks[subChunkY];
            SubChunkInstance foliageSubChunk = column.FoliageSubChunks[subChunkY];
            VoxelMesher.PendingSubChunkMeshData pendingSubChunk = pendingColumn.subChunks[subChunkY];
            if (pendingSubChunk == null)
            {
                SetSubChunkVisible(solidSubChunk, false);
            }
            else
            {
                bool subChunkHasGeometry = VoxelMesher.ApplyPendingSubChunkMesh(
                    pendingSubChunk,
                    solidSubChunk.Mesh,
                    $"SubChunk_{chunkCoords.x}_{subChunkY}_{chunkCoords.y}");

                SetSubChunkVisible(solidSubChunk, subChunkHasGeometry);
                hasGeometry |= subChunkHasGeometry;
            }

            bool fluidHasGeometry = RefreshFluidSubChunk(column, chunkCoords.x, subChunkY, chunkCoords.y);
            hasGeometry |= fluidHasGeometry;
            bool foliageHasGeometry = RefreshFoliageSubChunk(column, chunkCoords.x, subChunkY, chunkCoords.y);
            hasGeometry |= foliageHasGeometry;
        }

        column.Root.SetActive(hasGeometry);
        SyncSolidChunkColliders(column);
    }

    private ChunkColumnInstance GetOrCreateChunkColumnInstance(int chunkX, int chunkZ)
    {
        Vector2Int chunkCoords = new(chunkX, chunkZ);
        if (_chunkColumnInstances.TryGetValue(chunkCoords, out ChunkColumnInstance column))
        {
            return column;
        }

        column = _chunkColumnPool.Count > 0 ? _chunkColumnPool.Pop() : CreateChunkColumnInstance();
        column.Root.transform.SetParent(_worldRoot, false);
        column.Root.transform.localPosition = new Vector3(chunkX * VoxelTerrainData.ChunkSize, 0f, chunkZ * VoxelTerrainData.ChunkSize);
        column.Root.name = $"ChunkColumn_{chunkX}_{chunkZ}";
        column.Root.SetActive(false);

        for (int subChunkY = 0; subChunkY < column.SolidSubChunks.Length; subChunkY++)
        {
            SubChunkInstance solidSubChunk = column.SolidSubChunks[subChunkY];
            SubChunkInstance fluidSubChunk = column.FluidSubChunks[subChunkY];
            SubChunkInstance foliageSubChunk = column.FoliageSubChunks[subChunkY];
            solidSubChunk.GameObject.transform.localPosition = new Vector3(0f, subChunkY * VoxelTerrainData.SubChunkSize, 0f);
            fluidSubChunk.GameObject.transform.localPosition = new Vector3(0f, subChunkY * VoxelTerrainData.SubChunkSize, 0f);
            foliageSubChunk.GameObject.transform.localPosition = new Vector3(0f, subChunkY * VoxelTerrainData.SubChunkSize, 0f);
            SetSubChunkVisible(solidSubChunk, false);
            SetSubChunkVisible(fluidSubChunk, false);
            SetSubChunkVisible(foliageSubChunk, false);
        }

        _chunkColumnInstances.Add(chunkCoords, column);
        return column;
    }

    private ChunkColumnInstance CreateChunkColumnInstance()
    {
        if (TryCreateChunkColumnInstanceFromPrefab(out ChunkColumnInstance prefabColumn))
        {
            prefabColumn.Root.SetActive(false);
            return prefabColumn;
        }

        GameObject rootObject = CreateChunkColumnRootObject();
        ChunkColumnInstance column = new() { Root = rootObject };

        for (int subChunkY = 0; subChunkY < VoxelTerrainData.SubChunkCountY; subChunkY++)
        {
            column.SolidSubChunks[subChunkY] = CreateSubChunkInstance(rootObject.transform, subChunkY, worldMaterial, true, true, true);
            column.FluidSubChunks[subChunkY] = CreateSubChunkInstance(rootObject.transform, subChunkY, fluidMaterial, false, false, false);
            column.FoliageSubChunks[subChunkY] = CreateSubChunkInstance(rootObject.transform, subChunkY, foliageMaterial, false, true, true);
        }

        rootObject.SetActive(false);
        return column;
    }

    private bool TryCreateChunkColumnInstanceFromPrefab(out ChunkColumnInstance column)
    {
        column = null;
        if (chunkColumnPrefab == null)
        {
            return false;
        }

        GameObject rootObject = CreateChunkColumnRootObject();
        Chunk binding = rootObject.GetComponent<Chunk>();
        if (binding == null || !HasValidPrefabBinding(binding))
        {
            Destroy(rootObject);
            return false;
        }

        column = new ChunkColumnInstance { Root = rootObject };
        for (int subChunkY = 0; subChunkY < VoxelTerrainData.SubChunkCountY; subChunkY++)
        {
            column.SolidSubChunks[subChunkY] = CreateSubChunkInstanceFromBinding(
                binding.SolidSubChunks[subChunkY],
                subChunkY,
                worldMaterial,
                createCollider: true,
                castShadows: true,
                receiveShadows: true);

            column.FluidSubChunks[subChunkY] = CreateSubChunkInstanceFromBinding(
                binding.FluidSubChunks[subChunkY],
                subChunkY,
                fluidMaterial,
                createCollider: false,
                castShadows: false,
                receiveShadows: false);

            column.FoliageSubChunks[subChunkY] = CreateSubChunkInstanceFromBinding(
                binding.FoliageSubChunks[subChunkY],
                subChunkY,
                foliageMaterial,
                createCollider: false,
                castShadows: true,
                receiveShadows: true);
        }

        return true;
    }

    private GameObject CreateChunkColumnRootObject()
    {
        if (chunkColumnPrefab != null)
        {
            GameObject instance = Instantiate(chunkColumnPrefab);
            instance.name = chunkColumnPrefab.name;
            return instance;
        }

        return new GameObject("ChunkColumn");
    }

    private static bool HasValidPrefabBinding(Chunk binding)
    {
        return HasValidBindingArray(binding.SolidSubChunks) &&
               HasValidBindingArray(binding.FluidSubChunks) &&
               HasValidBindingArray(binding.FoliageSubChunks);
    }

    private static bool HasValidBindingArray(SubChunk[] bindings)
    {
        if (bindings == null || bindings.Length != VoxelTerrainData.SubChunkCountY)
        {
            return false;
        }

        for (int i = 0; i < bindings.Length; i++)
        {
            if (bindings[i] == null || bindings[i].MeshFilter == null || bindings[i].MeshRenderer == null)
            {
                return false;
            }
        }

        return true;
    }

    private SubChunkInstance CreateSubChunkInstanceFromBinding(
        SubChunk binding,
        int subChunkY,
        Material material,
        bool createCollider,
        bool castShadows,
        bool receiveShadows)
    {
        MeshFilter meshFilter = binding.MeshFilter;
        MeshRenderer meshRenderer = binding.MeshRenderer;
        MeshCollider meshCollider = binding.MeshCollider;

        if (meshCollider == null && createCollider)
        {
            meshCollider = binding.gameObject.AddComponent<MeshCollider>();
            meshCollider.cookingOptions =
                MeshColliderCookingOptions.CookForFasterSimulation |
                MeshColliderCookingOptions.EnableMeshCleaning |
                MeshColliderCookingOptions.WeldColocatedVertices |
                MeshColliderCookingOptions.UseFastMidphase;
        }

        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        meshRenderer.receiveShadows = receiveShadows;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        meshRenderer.allowOcclusionWhenDynamic = false;
        meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

        Mesh mesh = new() { name = $"SubChunkMesh_{subChunkY}", indexFormat = IndexFormat.UInt32 };
        meshFilter.sharedMesh = mesh;

        SubChunkInstance instance = new()
        {
            GameObject = binding.gameObject,
            MeshFilter = meshFilter,
            MeshRenderer = meshRenderer,
            MeshCollider = meshCollider,
            Mesh = mesh,
        };

        SetSubChunkVisible(instance, false);
        return instance;
    }

    private SubChunkInstance CreateSubChunkInstance(Transform parent, int subChunkY, Material material, bool createCollider, bool castShadows, bool receiveShadows)
    {
        GameObject subChunkObject = new($"SubChunk_{subChunkY}");
        subChunkObject.transform.SetParent(parent, false);

        MeshFilter meshFilter = subChunkObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = subChunkObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        meshRenderer.receiveShadows = receiveShadows;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        meshRenderer.allowOcclusionWhenDynamic = false;
        meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

        MeshCollider meshCollider = null;
        if (createCollider)
        {
            meshCollider = subChunkObject.AddComponent<MeshCollider>();
            meshCollider.cookingOptions =
                MeshColliderCookingOptions.CookForFasterSimulation |
                MeshColliderCookingOptions.EnableMeshCleaning |
                MeshColliderCookingOptions.WeldColocatedVertices |
                MeshColliderCookingOptions.UseFastMidphase;
        }

        Mesh mesh = new() { name = $"SubChunkMesh_{subChunkY}", indexFormat = IndexFormat.UInt32 };
        meshFilter.sharedMesh = mesh;

        SubChunkInstance instance = new()
        {
            GameObject = subChunkObject,
            MeshFilter = meshFilter,
            MeshRenderer = meshRenderer,
            MeshCollider = meshCollider,
            Mesh = mesh,
        };

        SetSubChunkVisible(instance, false);
        return instance;
    }

    private bool RefreshSolidSubChunk(ChunkColumnInstance column, int chunkX, int subChunkY, int chunkZ)
    {
        if (!_terrain.HasSolidBlocksInSubChunk(chunkX, subChunkY, chunkZ))
        {
            SetSubChunkVisible(column.SolidSubChunks[subChunkY], false);
            return false;
        }

        SubChunkInstance subChunk = column.SolidSubChunks[subChunkY];
        bool hasGeometry = VoxelMesher.RebuildSubChunkMesh(subChunk.Mesh, _terrain, blockDatabase, chunkX, subChunkY, chunkZ);
        SetSubChunkVisible(subChunk, hasGeometry);
        return hasGeometry;
    }

    private bool RefreshFluidSubChunk(ChunkColumnInstance column, int chunkX, int subChunkY, int chunkZ)
    {
        if (!_terrain.HasFluidInSubChunk(chunkX, subChunkY, chunkZ))
        {
            SetSubChunkVisible(column.FluidSubChunks[subChunkY], false);
            return false;
        }

        SubChunkInstance subChunk = column.FluidSubChunks[subChunkY];
        bool hasGeometry = VoxelFluidMesher.RebuildSubChunkMesh(subChunk.Mesh, _terrain, chunkX, subChunkY, chunkZ);
        SetSubChunkVisible(subChunk, hasGeometry);
        return hasGeometry;
    }

    private bool RefreshFoliageSubChunk(ChunkColumnInstance column, int chunkX, int subChunkY, int chunkZ)
    {
        if (!_terrain.HasFoliageInSubChunk(chunkX, subChunkY, chunkZ))
        {
            SetSubChunkVisible(column.FoliageSubChunks[subChunkY], false);
            return false;
        }

        SubChunkInstance subChunk = column.FoliageSubChunks[subChunkY];
        bool hasGeometry = VoxelFoliageMesher.RebuildSubChunkMesh(subChunk.Mesh, _terrain, blockDatabase, chunkX, subChunkY, chunkZ);
        SetSubChunkVisible(subChunk, hasGeometry);
        return hasGeometry;
    }

    private static void SetSubChunkVisible(SubChunkInstance subChunk, bool visible)
    {
        if (subChunk == null)
        {
            return;
        }

        if (!visible)
        {
            subChunk.GameObject.SetActive(false);
            if (subChunk.MeshCollider != null)
            {
                subChunk.MeshCollider.sharedMesh = null;
            }

            if (subChunk.Mesh != null)
            {
                subChunk.Mesh.Clear();
            }

            return;
        }

        if (subChunk.MeshCollider != null)
        {
            subChunk.MeshCollider.sharedMesh = null;
            subChunk.MeshCollider.sharedMesh = subChunk.Mesh;
        }

        subChunk.GameObject.SetActive(true);
    }

    private static void SyncSolidChunkColliders(ChunkColumnInstance column)
    {
        if (column == null || column.Root == null || !column.Root.activeInHierarchy)
        {
            return;
        }

        for (int subChunkY = 0; subChunkY < column.SolidSubChunks.Length; subChunkY++)
        {
            SubChunkInstance subChunk = column.SolidSubChunks[subChunkY];
            if (!IsSubChunkVisible(subChunk) || subChunk.MeshCollider == null || subChunk.Mesh == null || subChunk.Mesh.vertexCount == 0)
            {
                continue;
            }

            subChunk.MeshCollider.sharedMesh = null;
            subChunk.MeshCollider.sharedMesh = subChunk.Mesh;
            subChunk.MeshCollider.enabled = true;
        }
    }

    private void ReleaseChunkColumn(Vector2Int chunkCoords)
    {
        if (_pendingChunkColumnMeshes.TryGetValue(chunkCoords, out VoxelMesher.PendingChunkColumnMesh pendingColumn))
        {
            pendingColumn.Dispose();
            _pendingChunkColumnMeshes.Remove(chunkCoords);
        }

        if (!_chunkColumnInstances.TryGetValue(chunkCoords, out ChunkColumnInstance column))
        {
            return;
        }

        column.Root.SetActive(false);
        for (int subChunkY = 0; subChunkY < column.SolidSubChunks.Length; subChunkY++)
        {
            SetSubChunkVisible(column.SolidSubChunks[subChunkY], false);
            SetSubChunkVisible(column.FluidSubChunks[subChunkY], false);
            SetSubChunkVisible(column.FoliageSubChunks[subChunkY], false);
        }

        _chunkColumnInstances.Remove(chunkCoords);
        _chunkColumnPool.Push(column);
        RefreshVisibleFluidRendererCache();
    }

    private void DestroyAllChunkColumnInstances()
    {
        foreach (KeyValuePair<Vector2Int, ChunkColumnInstance> pair in _chunkColumnInstances)
        {
            DestroyChunkColumnInstance(pair.Value);
        }

        _chunkColumnInstances.Clear();

        while (_chunkColumnPool.Count > 0)
        {
            DestroyChunkColumnInstance(_chunkColumnPool.Pop());
        }
    }

    private void DisposePendingChunkMeshJobs()
    {
        foreach (KeyValuePair<Vector2Int, VoxelMesher.PendingChunkColumnMesh> pair in _pendingChunkColumnMeshes)
        {
            pair.Value.Dispose();
        }

        _pendingChunkColumnMeshes.Clear();
    }

    private static void DestroyChunkColumnInstance(ChunkColumnInstance column)
    {
        if (column == null)
        {
            return;
        }

        for (int subChunkY = 0; subChunkY < column.SolidSubChunks.Length; subChunkY++)
        {
            DestroySubChunkInstance(column.SolidSubChunks[subChunkY]);
            DestroySubChunkInstance(column.FluidSubChunks[subChunkY]);
            DestroySubChunkInstance(column.FoliageSubChunks[subChunkY]);
        }

        if (column.Root != null)
        {
            Object.Destroy(column.Root);
        }
    }

    private static void DestroySubChunkInstance(SubChunkInstance subChunk)
    {
        if (subChunk == null)
        {
            return;
        }

        if (subChunk.MeshCollider != null)
        {
            subChunk.MeshCollider.sharedMesh = null;
        }

        if (subChunk.MeshFilter != null)
        {
            subChunk.MeshFilter.sharedMesh = null;
        }

        if (subChunk.Mesh != null)
        {
            Object.Destroy(subChunk.Mesh);
        }
    }

    private void HandleBlockSelectionInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.digit1Key.wasPressedThisFrame)
        {
            _selectedPlacementBlock = BlockType.Dirt;
        }
        else if (keyboard.digit2Key.wasPressedThisFrame)
        {
            _selectedPlacementBlock = BlockType.Rock;
        }
    }

    private void HandleDebugInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (!keyboard.f3Key.isPressed || !keyboard.gKey.wasPressedThisFrame)
        {
            return;
        }

        _chunkBoundariesVisible = !_chunkBoundariesVisible;
        UpdateChunkBoundaryDebug();
    }

    private void UpdateSelection()
    {
        if (_worldRoot == null)
        {
            SetSelectionVisible(false);
            return;
        }

        Ray ray;
        if (_playerController != null)
        {
            ray = _playerController.GetInteractionRay();
        }
        else if (_resolvedInteractionCamera != null)
        {
            ray = _resolvedInteractionCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        }
        else
        {
            SetSelectionVisible(false);
            return;
        }

        if (!TrySelectContent(ray, out SelectionHit hit))
        {
            SetSelectionVisible(false);
            return;
        }

        _selectedBlockPosition = hit.position;
        _placementBlockPosition = hit.placementPosition;
        _selectedContentId = hit.contentId;
        _selectedIsFoliage = hit.isFoliage;
        _hasSelection = true;

        _selectionOutline.transform.SetParent(_worldRoot, false);
        _selectionOutline.transform.localPosition = hit.position;
        _selectionOutline.SetActive(true);
    }

    private void HandleBlockEditingInput()
    {
        if (!_hasSelection || Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame)
        {
            TryDestroySelectedBlock();
        }

        if (mouse.rightButton.wasPressedThisFrame)
        {
            TryPlaceSelectedBlock();
        }
    }

    private void TryDestroySelectedBlock()
    {
        if (!_terrain.IsInBounds(_selectedBlockPosition.x, _selectedBlockPosition.y, _selectedBlockPosition.z))
        {
            return;
        }

        if (_selectedIsFoliage)
        {
            if (!_terrain.SetFoliageId(_selectedBlockPosition.x, _selectedBlockPosition.y, _selectedBlockPosition.z, 0))
            {
                return;
            }

            RefreshTouchedSubChunks(_selectedBlockPosition);
            SetSelectionVisible(false);
            return;
        }

        if (_terrain.GetBlock(_selectedBlockPosition.x, _selectedBlockPosition.y, _selectedBlockPosition.z) == BlockType.Air)
        {
            return;
        }

        if (!_terrain.SetBlock(_selectedBlockPosition.x, _selectedBlockPosition.y, _selectedBlockPosition.z, BlockType.Air))
        {
            return;
        }

        RefreshTouchedSubChunks(_selectedBlockPosition);
        SetSelectionVisible(false);
    }

    private void TryPlaceSelectedBlock()
    {
        if (!_terrain.IsInBounds(_placementBlockPosition.x, _placementBlockPosition.y, _placementBlockPosition.z))
        {
            return;
        }

        if (_terrain.GetBlock(_placementBlockPosition.x, _placementBlockPosition.y, _placementBlockPosition.z) != BlockType.Air)
        {
            return;
        }

        if (_terrain.GetFluid(_placementBlockPosition.x, _placementBlockPosition.y, _placementBlockPosition.z).Exists)
        {
            return;
        }

        if (!_terrain.SetBlock(_placementBlockPosition.x, _placementBlockPosition.y, _placementBlockPosition.z, _selectedPlacementBlock))
        {
            return;
        }

        RefreshTouchedSubChunks(_placementBlockPosition);
    }

    private void RefreshTouchedSubChunks(Vector3Int blockPosition)
    {
        _subChunkRefreshBuffer.Clear();

        int chunkX = FloorDiv(blockPosition.x, VoxelTerrainData.ChunkSize);
        int subChunkY = FloorDiv(blockPosition.y, VoxelTerrainData.SubChunkSize);
        int chunkZ = FloorDiv(blockPosition.z, VoxelTerrainData.ChunkSize);

        AddSubChunkForRefresh(chunkX, subChunkY, chunkZ);

        int localX = Mod(blockPosition.x, VoxelTerrainData.ChunkSize);
        int localY = Mod(blockPosition.y, VoxelTerrainData.SubChunkSize);
        int localZ = Mod(blockPosition.z, VoxelTerrainData.ChunkSize);

        if (localX == 0)
        {
            AddSubChunkForRefresh(chunkX - 1, subChunkY, chunkZ);
        }
        else if (localX == VoxelTerrainData.ChunkSize - 1)
        {
            AddSubChunkForRefresh(chunkX + 1, subChunkY, chunkZ);
        }

        if (localY == 0)
        {
            AddSubChunkForRefresh(chunkX, subChunkY - 1, chunkZ);
        }
        else if (localY == VoxelTerrainData.SubChunkSize - 1)
        {
            AddSubChunkForRefresh(chunkX, subChunkY + 1, chunkZ);
        }

        if (localZ == 0)
        {
            AddSubChunkForRefresh(chunkX, subChunkY, chunkZ - 1);
        }
        else if (localZ == VoxelTerrainData.ChunkSize - 1)
        {
            AddSubChunkForRefresh(chunkX, subChunkY, chunkZ + 1);
        }

        for (int i = 0; i < _subChunkRefreshBuffer.Count; i++)
        {
            Vector3Int coords = _subChunkRefreshBuffer[i];
            RefreshLoadedSubChunk(coords.x, coords.y, coords.z);
        }
    }

    private void AddSubChunkForRefresh(int chunkX, int subChunkY, int chunkZ)
    {
        if (subChunkY < 0 || subChunkY >= VoxelTerrainData.SubChunkCountY)
        {
            return;
        }

        Vector2Int chunkCoords = new(chunkX, chunkZ);
        if (!_visibleChunkColumns.Contains(chunkCoords) || !_chunkColumnInstances.ContainsKey(chunkCoords))
        {
            return;
        }

        Vector3Int coords = new(chunkX, subChunkY, chunkZ);
        for (int i = 0; i < _subChunkRefreshBuffer.Count; i++)
        {
            if (_subChunkRefreshBuffer[i] == coords)
            {
                return;
            }
        }

        _subChunkRefreshBuffer.Add(coords);
    }

    private void RefreshLoadedSubChunk(int chunkX, int subChunkY, int chunkZ)
    {
        Vector2Int chunkCoords = new(chunkX, chunkZ);
        if (_pendingChunkColumnMeshes.TryGetValue(chunkCoords, out VoxelMesher.PendingChunkColumnMesh pendingColumn))
        {
            pendingColumn.Dispose();
            _pendingChunkColumnMeshes.Remove(chunkCoords);
        }

        if (!_chunkColumnInstances.TryGetValue(chunkCoords, out ChunkColumnInstance column))
        {
            return;
        }

        RefreshSolidSubChunk(column, chunkX, subChunkY, chunkZ);
        RefreshFluidSubChunk(column, chunkX, subChunkY, chunkZ);
        RefreshFoliageSubChunk(column, chunkX, subChunkY, chunkZ);
        column.Root.SetActive(HasAnyVisibleGeometry(column));
        SyncSolidChunkColliders(column);
        RefreshVisibleFluidRendererCache();
    }

    private void CreateSelectionOutline()
    {
        _selectionOutline = new GameObject("Selection Outline");
        _selectionOutline.transform.SetParent(transform, false);

        MeshFilter meshFilter = _selectionOutline.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = CreateSelectionOutlineMesh();

        MeshRenderer meshRenderer = _selectionOutline.AddComponent<MeshRenderer>();
        _selectionMaterial = CreateSelectionMaterial();
        meshRenderer.sharedMaterial = _selectionMaterial;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        meshRenderer.allowOcclusionWhenDynamic = false;
        meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

        _selectionOutline.SetActive(false);
    }

    private void CreateChunkBoundaryDebug()
    {
        _chunkBoundaryRoot = new GameObject("Chunk Boundary Debug");
        _chunkBoundaryRoot.transform.SetParent(transform, false);

        _chunkBoundaryMesh = CreateChunkBoundaryPlaneMesh();
        _chunkBoundaryMaterial = CreateChunkBoundaryMaterial();

        for (int i = 0; i < _chunkBoundaryPlanes.Length; i++)
        {
            GameObject plane = new($"BoundaryPlane_{i}");
            plane.transform.SetParent(_chunkBoundaryRoot.transform, false);

            MeshFilter meshFilter = plane.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = _chunkBoundaryMesh;

            MeshRenderer meshRenderer = plane.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = _chunkBoundaryMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            meshRenderer.allowOcclusionWhenDynamic = false;
            meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            plane.SetActive(false);
            _chunkBoundaryPlanes[i] = plane;
        }

        _chunkBoundaryRoot.SetActive(false);
    }

    private Material CreateChunkBoundaryMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null && worldMaterial != null)
        {
            shader = worldMaterial.shader;
        }

        Material material = new(shader)
        {
            color = chunkBoundaryColor,
            name = "Chunk Boundary Debug Material",
            renderQueue = (int)RenderQueue.Overlay,
        };

        material.SetOverrideTag("RenderType", "Transparent");
        material.SetInt("_Surface", 1);
        material.SetInt("_Blend", 0);
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.SetInt("_ZTest", (int)CompareFunction.Always);
        material.SetInt("_Cull", (int)CullMode.Off);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return material;
    }

    private static Mesh CreateChunkBoundaryPlaneMesh()
    {
        Mesh mesh = new()
        {
            name = "Chunk Boundary Plane",
        };

        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
        };
        mesh.normals = new[]
        {
            Vector3.forward,
            Vector3.forward,
            Vector3.forward,
            Vector3.forward,
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();
        mesh.UploadMeshData(false);
        return mesh;
    }

    private void UpdateChunkBoundaryDebug()
    {
        if (_chunkBoundaryRoot == null)
        {
            return;
        }

        bool shouldShow = _chunkBoundariesVisible && _hasCenterChunk;
        _chunkBoundaryRoot.SetActive(shouldShow);
        if (!shouldShow)
        {
            return;
        }

        float chunkSize = VoxelTerrainData.ChunkSize;
        float totalSpan = chunkSize * 3f;
        float height = VoxelTerrainData.WorldHeight;
        int startChunkX = _currentCenterChunk.x - 1;
        int startChunkZ = _currentCenterChunk.y - 1;

        for (int i = 0; i < 4; i++)
        {
            float boundaryX = (startChunkX + i) * chunkSize;
            Transform plane = _chunkBoundaryPlanes[i].transform;
            plane.localPosition = new Vector3(boundaryX, height * 0.5f, (startChunkZ * chunkSize) + (totalSpan * 0.5f));
            plane.localRotation = Quaternion.Euler(0f, 90f, 0f);
            plane.localScale = new Vector3(totalSpan, height, 1f);
            _chunkBoundaryPlanes[i].SetActive(true);
        }

        for (int i = 0; i < 4; i++)
        {
            float boundaryZ = (startChunkZ + i) * chunkSize;
            Transform plane = _chunkBoundaryPlanes[4 + i].transform;
            plane.localPosition = new Vector3((startChunkX * chunkSize) + (totalSpan * 0.5f), height * 0.5f, boundaryZ);
            plane.localRotation = Quaternion.identity;
            plane.localScale = new Vector3(totalSpan, height, 1f);
            _chunkBoundaryPlanes[4 + i].SetActive(true);
        }
    }

    private Material CreateSelectionMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null && worldMaterial != null)
        {
            shader = worldMaterial.shader;
        }

        Material material = new(shader)
        {
            color = selectionColor,
            name = "Voxel Selection Material",
        };

        return material;
    }

    private static Mesh CreateSelectionOutlineMesh()
    {
        const float padding = 0.002f;

        Vector3[] vertices =
        {
            new(-padding, -padding, -padding),
            new(1f + padding, -padding, -padding),
            new(1f + padding, -padding, 1f + padding),
            new(-padding, -padding, 1f + padding),
            new(-padding, 1f + padding, -padding),
            new(1f + padding, 1f + padding, -padding),
            new(1f + padding, 1f + padding, 1f + padding),
            new(-padding, 1f + padding, 1f + padding),
        };

        int[] indices =
        {
            0, 1, 1, 2, 2, 3, 3, 0,
            4, 5, 5, 6, 6, 7, 7, 4,
            0, 4, 1, 5, 2, 6, 3, 7,
        };

        Mesh mesh = new() { name = "Voxel Selection Outline" };
        mesh.vertices = vertices;
        mesh.SetIndices(indices, MeshTopology.Lines, 0);
        mesh.RecalculateBounds();
        mesh.UploadMeshData(false);
        return mesh;
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
            FloorDiv(Mathf.FloorToInt(focusPosition.x), VoxelTerrainData.ChunkSize),
            FloorDiv(Mathf.FloorToInt(focusPosition.z), VoxelTerrainData.ChunkSize));
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
            _playerController = FindAnyObjectByType<VoxelFlyCamera>();
        }
    }

    private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (!enablePlanarReflection || _terrain == null || _isRenderingPlanarReflection)
        {
            return;
        }

        ResolveInteractionCamera();
        if (_resolvedInteractionCamera == null || camera != _resolvedInteractionCamera || camera == _planarReflectionCamera)
        {
            return;
        }

        RenderPlanarReflection(camera);
    }

    private void RenderPlanarReflection(Camera sourceCamera)
    {
        if (fluidMaterial == null || sourceCamera == null || sourceCamera.pixelWidth <= 0 || sourceCamera.pixelHeight <= 0)
        {
            ApplyPlanarReflectionFallback();
            return;
        }

        if (!HasVisibleFluidGeometry(sourceCamera))
        {
            ApplyPlanarReflectionFallback();
            return;
        }

        int updateInterval = Mathf.Max(1, planarReflectionUpdateInterval);
        if (_planarReflectionTexture != null && Time.frameCount - _lastPlanarReflectionFrame < updateInterval)
        {
            return;
        }

        EnsurePlanarReflectionCamera();
        EnsurePlanarReflectionTexture(sourceCamera);
        if (_planarReflectionCamera == null || _planarReflectionTexture == null)
        {
            ApplyPlanarReflectionFallback();
            return;
        }

        _planarReflectionCamera.CopyFrom(sourceCamera);
        _planarReflectionCamera.enabled = false;
        _planarReflectionCamera.targetTexture = _planarReflectionTexture;
        _planarReflectionCamera.cullingMask = sourceCamera.cullingMask & planarReflectionCullingMask.value;
        _planarReflectionCamera.useOcclusionCulling = sourceCamera.useOcclusionCulling;
        _planarReflectionCamera.allowHDR = planarReflectionAllowHdr;
        _planarReflectionCamera.allowMSAA = false;
        _planarReflectionCamera.depthTextureMode = DepthTextureMode.None;
        _planarReflectionCamera.farClipPlane = Mathf.Min(sourceCamera.farClipPlane, planarReflectionFarClip);
        _planarReflectionCamera.ResetWorldToCameraMatrix();
        _planarReflectionCamera.ResetProjectionMatrix();
        _planarReflectionCamera.ResetCullingMatrix();

        if (sourceCamera.TryGetComponent(out Skybox sourceSkybox))
        {
            _planarReflectionSkybox.enabled = sourceSkybox.enabled;
            _planarReflectionSkybox.material = sourceSkybox.material;
        }

        UniversalAdditionalCameraData sourceAdditionalData = sourceCamera.GetUniversalAdditionalCameraData();
        if (sourceAdditionalData != null && _planarReflectionCameraData != null)
        {
            _planarReflectionCameraData.renderType = CameraRenderType.Base;
            _planarReflectionCameraData.renderShadows = planarReflectionRenderShadows && sourceAdditionalData.renderShadows;
            _planarReflectionCameraData.renderPostProcessing = false;
            _planarReflectionCameraData.requiresColorOption = CameraOverrideOption.Off;
            _planarReflectionCameraData.requiresDepthOption = CameraOverrideOption.Off;
            _planarReflectionCameraData.allowXRRendering = false;
            _planarReflectionCameraData.volumeLayerMask = 0;
            _planarReflectionCameraData.volumeTrigger = null;
        }

        float planeY = GetPlanarReflectionPlaneY();
        Vector3 planePosition = new(0f, planeY, 0f);
        Vector3 planeNormal = Vector3.up;
        float planeOffset = -Vector3.Dot(planeNormal, planePosition) - planarReflectionClipPlaneOffset;
        Vector4 reflectionPlane = new(planeNormal.x, planeNormal.y, planeNormal.z, planeOffset);
        Matrix4x4 reflectionMatrix = CalculateReflectionMatrix(reflectionPlane);

        Vector3 sourcePosition = sourceCamera.transform.position;
        Vector3 reflectedPosition = reflectionMatrix.MultiplyPoint(sourcePosition);
        Vector3 sourceEuler = sourceCamera.transform.eulerAngles;
        Vector3 reflectedEuler = new(-sourceEuler.x, sourceEuler.y, sourceEuler.z);

        _planarReflectionCamera.transform.position = reflectedPosition;
        _planarReflectionCamera.transform.eulerAngles = reflectedEuler;
        _planarReflectionCamera.worldToCameraMatrix = sourceCamera.worldToCameraMatrix * reflectionMatrix;

        Vector4 clipPlane = CameraSpacePlane(_planarReflectionCamera, planePosition, planeNormal, 1f, planarReflectionClipPlaneOffset);
        _planarReflectionCamera.projectionMatrix = sourceCamera.CalculateObliqueMatrix(clipPlane);
        _planarReflectionCamera.cullingMatrix = _planarReflectionCamera.projectionMatrix * _planarReflectionCamera.worldToCameraMatrix;

        bool selectionWasActive = _selectionOutline != null && _selectionOutline.activeSelf;
        bool boundariesWereActive = _chunkBoundaryRoot != null && _chunkBoundaryRoot.activeSelf;
        HideFluidAndDebugRenderersForReflection(selectionWasActive, boundariesWereActive);

        _isRenderingPlanarReflection = true;
        bool previousInvertCulling = GL.invertCulling;

        try
        {
            GL.invertCulling = true;
            _planarReflectionRequest.destination = _planarReflectionTexture;
            _planarReflectionRequest.mipLevel = 0;
            _planarReflectionRequest.slice = 0;
            _planarReflectionRequest.face = CubemapFace.Unknown;

            if (RenderPipeline.SupportsRenderRequest(_planarReflectionCamera, _planarReflectionRequest))
            {
                RenderPipeline.SubmitRenderRequest(_planarReflectionCamera, _planarReflectionRequest);
            }
        }
        finally
        {
            GL.invertCulling = previousInvertCulling;
            _isRenderingPlanarReflection = false;
            RestoreRenderersAfterReflection(selectionWasActive, boundariesWereActive);
        }

        Matrix4x4 gpuProjection = GL.GetGPUProjectionMatrix(_planarReflectionCamera.projectionMatrix, true);
        Shader.SetGlobalTexture(PlanarReflectionTextureId, _planarReflectionTexture);
        Shader.SetGlobalMatrix(PlanarReflectionVpId, gpuProjection * _planarReflectionCamera.worldToCameraMatrix);
        fluidMaterial.SetTexture(PlanarReflectionTextureId, _planarReflectionTexture);
        fluidMaterial.SetMatrix(PlanarReflectionVpId, gpuProjection * _planarReflectionCamera.worldToCameraMatrix);
        _lastPlanarReflectionFrame = Time.frameCount;
    }

    private void EnsurePlanarReflectionCamera()
    {
        if (_planarReflectionCamera != null)
        {
            return;
        }

        GameObject reflectionCameraObject = new("Planar Reflection Camera")
        {
            hideFlags = HideFlags.HideAndDontSave,
        };

        reflectionCameraObject.transform.SetParent(transform, false);
        _planarReflectionCamera = reflectionCameraObject.AddComponent<Camera>();
        _planarReflectionCamera.enabled = false;
        _planarReflectionSkybox = _planarReflectionCamera.gameObject.AddComponent<Skybox>();
        _planarReflectionCameraData = _planarReflectionCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        _planarReflectionRequest = new UniversalRenderPipeline.SingleCameraRequest();
    }

    private void EnsurePlanarReflectionTexture(Camera sourceCamera)
    {
        int width = Mathf.Max(1, Mathf.RoundToInt(sourceCamera.pixelWidth * planarReflectionRenderScale));
        int height = Mathf.Max(1, Mathf.RoundToInt(sourceCamera.pixelHeight * planarReflectionRenderScale));

        if (_planarReflectionTexture != null && _planarReflectionTexture.width == width && _planarReflectionTexture.height == height)
        {
            return;
        }

        if (_planarReflectionTexture != null)
        {
            _planarReflectionTexture.Release();
            Destroy(_planarReflectionTexture);
        }

        RenderTextureFormat format = sourceCamera.allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.ARGB32;
        if (!planarReflectionAllowHdr)
        {
            format = RenderTextureFormat.ARGB32;
        }

        _planarReflectionTexture = new RenderTexture(width, height, 16, format)
        {
            name = "PlanarReflectionRT",
            hideFlags = HideFlags.HideAndDontSave,
            useMipMap = false,
            autoGenerateMips = false,
            antiAliasing = 1,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        _planarReflectionTexture.Create();
    }

    private bool HasVisibleFluidGeometry(Camera sourceCamera)
    {
        if (_visibleFluidRenderers.Count == 0 || sourceCamera == null)
        {
            return false;
        }

        GeometryUtility.CalculateFrustumPlanes(sourceCamera, _cameraFrustumPlanes);
        for (int i = 0; i < _visibleFluidRenderers.Count; i++)
        {
            MeshRenderer renderer = _visibleFluidRenderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (GeometryUtility.TestPlanesAABB(_cameraFrustumPlanes, renderer.bounds))
            {
                return true;
            }
        }

        return false;
    }

    private void HideFluidAndDebugRenderersForReflection(bool selectionWasActive, bool boundariesWereActive)
    {
        _reflectionHiddenRenderers.Clear();

        for (int i = 0; i < _visibleFluidRenderers.Count; i++)
        {
            MeshRenderer renderer = _visibleFluidRenderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            renderer.enabled = false;
            _reflectionHiddenRenderers.Add(renderer);
        }

        if (selectionWasActive)
        {
            _selectionOutline.SetActive(false);
        }

        if (boundariesWereActive)
        {
            _chunkBoundaryRoot.SetActive(false);
        }
    }

    private void RefreshVisibleFluidRendererCache()
    {
        _visibleFluidRenderers.Clear();

        foreach (ChunkColumnInstance column in _chunkColumnInstances.Values)
        {
            if (column == null)
            {
                continue;
            }

            for (int subChunkY = 0; subChunkY < column.FluidSubChunks.Length; subChunkY++)
            {
                SubChunkInstance subChunk = column.FluidSubChunks[subChunkY];
                if (IsSubChunkVisible(subChunk) && subChunk.MeshRenderer != null)
                {
                    _visibleFluidRenderers.Add(subChunk.MeshRenderer);
                }
            }
        }
    }

    private void RestoreRenderersAfterReflection(bool selectionWasActive, bool boundariesWereActive)
    {
        for (int i = 0; i < _reflectionHiddenRenderers.Count; i++)
        {
            if (_reflectionHiddenRenderers[i] != null)
            {
                _reflectionHiddenRenderers[i].enabled = true;
            }
        }

        _reflectionHiddenRenderers.Clear();

        if (selectionWasActive && _selectionOutline != null)
        {
            _selectionOutline.SetActive(true);
        }

        if (boundariesWereActive && _chunkBoundaryRoot != null)
        {
            _chunkBoundaryRoot.SetActive(true);
        }
    }

    private void ReleasePlanarReflectionResources()
    {
        ApplyPlanarReflectionFallback();

        if (_planarReflectionTexture != null)
        {
            _planarReflectionTexture.Release();
            Destroy(_planarReflectionTexture);
            _planarReflectionTexture = null;
        }

        if (_planarReflectionCamera != null)
        {
            Destroy(_planarReflectionCamera.gameObject);
            _planarReflectionCamera = null;
        }

        _planarReflectionSkybox = null;
        _planarReflectionCameraData = null;
        _planarReflectionRequest = null;
    }

    private void ApplyPlanarReflectionFallback()
    {
        Shader.SetGlobalTexture(PlanarReflectionTextureId, Texture2D.blackTexture);
        Shader.SetGlobalMatrix(PlanarReflectionVpId, Matrix4x4.identity);
        _lastPlanarReflectionFrame = -999999;
        if (fluidMaterial != null)
        {
            fluidMaterial.SetTexture(PlanarReflectionTextureId, Texture2D.blackTexture);
            fluidMaterial.SetMatrix(PlanarReflectionVpId, Matrix4x4.identity);
        }
    }

    private float GetPlanarReflectionPlaneY()
    {
        return transform.position.y + _terrain.SeaLevel;
    }

    private static Vector4 CameraSpacePlane(Camera camera, Vector3 position, Vector3 normal, float sideSign, float clipPlaneOffset)
    {
        Vector3 offsetPosition = position + (normal * clipPlaneOffset);
        Matrix4x4 worldToCameraMatrix = camera.worldToCameraMatrix;
        Vector3 cameraSpacePosition = worldToCameraMatrix.MultiplyPoint(offsetPosition);
        Vector3 cameraSpaceNormal = worldToCameraMatrix.MultiplyVector(normal).normalized * sideSign;
        return new Vector4(
            cameraSpaceNormal.x,
            cameraSpaceNormal.y,
            cameraSpaceNormal.z,
            -Vector3.Dot(cameraSpacePosition, cameraSpaceNormal));
    }

    private static Matrix4x4 CalculateReflectionMatrix(Vector4 plane)
    {
        Matrix4x4 reflection = Matrix4x4.zero;

        reflection.m00 = 1f - (2f * plane[0] * plane[0]);
        reflection.m01 = -2f * plane[0] * plane[1];
        reflection.m02 = -2f * plane[0] * plane[2];
        reflection.m03 = -2f * plane[3] * plane[0];

        reflection.m10 = -2f * plane[1] * plane[0];
        reflection.m11 = 1f - (2f * plane[1] * plane[1]);
        reflection.m12 = -2f * plane[1] * plane[2];
        reflection.m13 = -2f * plane[3] * plane[1];

        reflection.m20 = -2f * plane[2] * plane[0];
        reflection.m21 = -2f * plane[2] * plane[1];
        reflection.m22 = 1f - (2f * plane[2] * plane[2]);
        reflection.m23 = -2f * plane[3] * plane[2];

        reflection.m30 = 0f;
        reflection.m31 = 0f;
        reflection.m32 = 0f;
        reflection.m33 = 1f;

        return reflection;
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

    private bool TryRaycastWorld(Ray ray, out RaycastHit closestWorldHit)
    {
        closestWorldHit = default;
        int hitCount = Physics.RaycastNonAlloc(
            ray,
            _interactionHits,
            interactionDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        float closestDistance = float.MaxValue;
        bool found = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _interactionHits[i];
            Collider hitCollider = hit.collider;
            if (hitCollider == null || _worldRoot == null || !hitCollider.transform.IsChildOf(_worldRoot))
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestWorldHit = hit;
                found = true;
            }
        }

        return found;
    }

    private void SetSelectionVisible(bool visible)
    {
        _hasSelection = false;
        _selectedContentId = 0;
        _selectedIsFoliage = false;
        if (_selectionOutline != null)
        {
            _selectionOutline.SetActive(visible);
        }
    }

    private bool TrySelectContent(Ray ray, out SelectionHit selectionHit)
    {
        selectionHit = default;
        if (_terrain == null || _worldRoot == null)
        {
            return false;
        }

        Vector3 localOrigin = _worldRoot.InverseTransformPoint(ray.origin);
        Vector3 localDirection = _worldRoot.InverseTransformDirection(ray.direction).normalized;
        if (localDirection.sqrMagnitude < 0.000001f)
        {
            return false;
        }

        Vector3Int current = WorldPointToBlock(localOrigin);
        Vector3Int step = new(
            localDirection.x > 0f ? 1 : (localDirection.x < 0f ? -1 : 0),
            localDirection.y > 0f ? 1 : (localDirection.y < 0f ? -1 : 0),
            localDirection.z > 0f ? 1 : (localDirection.z < 0f ? -1 : 0));

        Vector3 tMax = new(
            CalculateInitialTMax(localOrigin.x, localDirection.x, current.x, step.x),
            CalculateInitialTMax(localOrigin.y, localDirection.y, current.y, step.y),
            CalculateInitialTMax(localOrigin.z, localDirection.z, current.z, step.z));

        Vector3 tDelta = new(
            CalculateTDelta(localDirection.x),
            CalculateTDelta(localDirection.y),
            CalculateTDelta(localDirection.z));

        float traveled = 0f;
        Vector3Int lastEmpty = current;
        bool hasLastEmpty = false;

        while (traveled <= interactionDistance)
        {
            if (_terrain.IsInBounds(current.x, current.y, current.z))
            {
                ushort foliageId = _terrain.GetFoliageId(current.x, current.y, current.z);
                if (foliageId != 0)
                {
                    selectionHit = new SelectionHit
                    {
                        position = current,
                        placementPosition = current,
                        contentId = foliageId,
                        isFoliage = true,
                    };
                    return true;
                }

                BlockType blockType = _terrain.GetBlock(current.x, current.y, current.z);
                if (blockType != BlockType.Air)
                {
                    selectionHit = new SelectionHit
                    {
                        position = current,
                        placementPosition = hasLastEmpty ? lastEmpty : current,
                        contentId = (ushort)blockType,
                        isFoliage = false,
                    };
                    return true;
                }

                lastEmpty = current;
                hasLastEmpty = true;
            }

            if (tMax.x < tMax.y)
            {
                if (tMax.x < tMax.z)
                {
                    current.x += step.x;
                    traveled = tMax.x;
                    tMax.x += tDelta.x;
                }
                else
                {
                    current.z += step.z;
                    traveled = tMax.z;
                    tMax.z += tDelta.z;
                }
            }
            else
            {
                if (tMax.y < tMax.z)
                {
                    current.y += step.y;
                    traveled = tMax.y;
                    tMax.y += tDelta.y;
                }
                else
                {
                    current.z += step.z;
                    traveled = tMax.z;
                    tMax.z += tDelta.z;
                }
            }
        }

        return false;
    }

    private static float CalculateInitialTMax(float originComponent, float directionComponent, int currentCell, int step)
    {
        if (step == 0 || Mathf.Abs(directionComponent) < 0.000001f)
        {
            return float.PositiveInfinity;
        }

        float nextBoundary = currentCell + (step > 0 ? 1f : 0f);
        return (nextBoundary - originComponent) / directionComponent;
    }

    private static float CalculateTDelta(float directionComponent)
    {
        return Mathf.Abs(directionComponent) < 0.000001f ? float.PositiveInfinity : Mathf.Abs(1f / directionComponent);
    }

    private static Vector3Int WorldPointToBlock(Vector3 point)
    {
        return new Vector3Int(
            Mathf.FloorToInt(point.x),
            Mathf.FloorToInt(point.y),
            Mathf.FloorToInt(point.z));
    }

    private static int Mod(int value, int modulus)
    {
        int result = value % modulus;
        return result < 0 ? result + modulus : result;
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
            Debug.LogError("VoxelWorldRuntime requires a World Material reference.", this);
            isValid = false;
        }

        if (fluidMaterial == null)
        {
            Debug.LogError("VoxelWorldRuntime requires a Fluid Material reference.", this);
            isValid = false;
        }

        if (blockDatabase == null)
        {
            Debug.LogError("VoxelWorldRuntime requires a Block Database reference.", this);
            isValid = false;
        }

        if (worldGenSettingsAsset == null)
        {
            Debug.LogError("VoxelWorldRuntime requires a World Gen Settings Asset reference.", this);
            isValid = false;
        }
        else if (!terrainSettings.IsInitialized)
        {
            Debug.LogError("VoxelWorldRuntime requires a valid initialized World Gen Settings Asset.", this);
            isValid = false;
        }

        if (worldMaterial != null && !worldMaterial.HasProperty("_BlockTextures"))
        {
            Debug.LogError("VoxelWorldRuntime world material must use a shader with a _BlockTextures Texture2DArray property.", this);
            isValid = false;
        }

        if (worldMaterial != null)
        {
            Texture texture = worldMaterial.GetTexture("_BlockTextures");
            if (texture is not Texture2DArray textureArray)
            {
                Debug.LogError("VoxelWorldRuntime world material requires a Texture2DArray assigned to _BlockTextures.", this);
                isValid = false;
            }
            else if (blockDatabase != null && blockDatabase.HighestTextureLayer >= textureArray.depth)
            {
                Debug.LogError(
                    $"VoxelWorldRuntime block database references texture layer {blockDatabase.HighestTextureLayer}, but the assigned Texture2DArray only has {textureArray.depth} layers.",
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

    private static bool HasAnyVisibleGeometry(ChunkColumnInstance column)
    {
        for (int i = 0; i < column.SolidSubChunks.Length; i++)
        {
            if (IsSubChunkVisible(column.SolidSubChunks[i]) || IsSubChunkVisible(column.FluidSubChunks[i]) || IsSubChunkVisible(column.FoliageSubChunks[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSubChunkVisible(SubChunkInstance subChunk)
    {
        return subChunk != null && subChunk.GameObject != null && subChunk.GameObject.activeSelf;
    }

    private void SetupDebugOverlay()
    {
        VoxelDebugOverlay overlay = FindAnyObjectByType<VoxelDebugOverlay>();
        if (overlay == null)
        {
            overlay = gameObject.AddComponent<VoxelDebugOverlay>();
        }

        overlay.BindWorldRuntime(this);
    }

    private bool ValidateRequiredBlockDefinition(ushort blockId)
    {
        if (blockDatabase.HasDefinition(blockId))
        {
            return true;
        }

        Debug.LogError($"VoxelWorldRuntime block database is missing required block id {blockId}.", this);
        return false;
    }

    private void EnsureTerrainSettingsInitialized()
    {
        if (worldGenSettingsAsset != null)
        {
            terrainSettings = VoxelTerrainGenerationSettings.FromWorldGenSettings(
                worldGenSettingsAsset,
                UseContinentalnessCdfRemap());
            return;
        }

        terrainSettings = default;
    }

    private bool UseContinentalnessCdfRemap()
    {
        return continentalnessCdfProfileAsset != null && continentalnessCdfProfileAsset.HasContinentalnessRemap;
    }

    private float[] GetContinentalnessCdfLut()
    {
        return UseContinentalnessCdfRemap()
            ? continentalnessCdfProfileAsset.BakedContinentalnessCdfLut
            : null;
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

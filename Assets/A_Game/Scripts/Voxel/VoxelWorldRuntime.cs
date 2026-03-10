using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class VoxelWorldRuntime : MonoBehaviour
{
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
        public SubChunkInstance[] SubChunks = new SubChunkInstance[VoxelTerrainData.SubChunkCountY];
    }

    [Header("Scene References")]
    [SerializeField] private Material worldMaterial;
    [SerializeField] private VoxelBlockDatabase blockDatabase;
    [SerializeField] private Camera interactionCamera;

    [Header("Terrain")]
    [FormerlySerializedAs("worldSizeInChunks")]
    [SerializeField, Min(1)] private int renderSizeInChunks = 9;
    [SerializeField, Min(0)] private int generationPaddingInChunks = 1;
    [SerializeField] private int seed = 24680;
    [SerializeField] private VoxelTerrainGenerationSettings terrainSettings = default;

    [Header("Streaming")]
    [SerializeField, Min(1)] private int completedChunkGenerationsPerFrame = 4;
    [SerializeField, Min(1)] private int chunkColumnsMeshedPerFrame = 2;

    [Header("Interaction")]
    [SerializeField, Min(0.5f)] private float interactionDistance = 8f;
    [SerializeField] private Color selectionColor = new(1f, 0.85f, 0.2f, 1f);

    [Header("Debug")]
    [SerializeField] private Color chunkBoundaryColor = new(0.2f, 0.9f, 1f, 0.16f);

    private readonly List<Vector2Int> _completedChunkBuffer = new(16);
    private readonly List<Vector2Int> _pendingMeshKeyBuffer = new(64);
    private readonly List<Vector2Int> _visibleChunkKeyBuffer = new(256);
    private readonly List<Vector3Int> _subChunkRefreshBuffer = new(7);
    private readonly Dictionary<Vector2Int, VoxelMesher.PendingChunkColumnMesh> _pendingChunkColumnMeshes = new();
    private readonly HashSet<Vector2Int> _queuedChunkColumns = new();
    private readonly HashSet<Vector2Int> _targetVisibleChunks = new();
    private readonly HashSet<Vector2Int> _visibleChunkColumns = new();
    private readonly Queue<Vector2Int> _chunkRefreshQueue = new();
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
    private NativeArray<ushort> _faceTextureLookup;
    private Vector3Int _selectedBlockPosition;
    private Vector3Int _placementBlockPosition;
    private Vector2Int _currentCenterChunk;
    private bool _hasSelection;
    private bool _hasCenterChunk;
    private bool _chunkBoundariesVisible;
    private VoxelBlockType _selectedPlacementBlock = VoxelBlockType.Dirt;

    public bool HasSelectedBlock => _hasSelection;
    public Vector3Int SelectedBlockPosition => _selectedBlockPosition;
    public VoxelBlockType SelectedBlockType =>
        _hasSelection && _terrain != null
            ? _terrain.GetBlock(_selectedBlockPosition.x, _selectedBlockPosition.y, _selectedBlockPosition.z)
            : VoxelBlockType.Air;
    public int RenderSizeInChunks => renderSizeInChunks;

    private void Reset()
    {
        if (terrainSettings.dirt.baseHeight == 0 && terrainSettings.rock.baseHeight == 0)
        {
            terrainSettings = VoxelTerrainGenerationSettings.Default;
        }

        EnsureRenderSizeIsOdd();
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
        BuildFaceTextureLookup();
        BuildWorld();
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
        _terrain = new VoxelTerrainData(seed, terrainSettings);

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

        foreach (Vector2Int chunkCoords in _targetVisibleChunks)
        {
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
        for (int offsetZ = -radius; offsetZ <= radius; offsetZ++)
        {
            for (int offsetX = -radius; offsetX <= radius; offsetX++)
            {
                _terrain.RequestChunkColumn(centerChunk.x + offsetX, centerChunk.y + offsetZ);
            }
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
            Vector2Int chunkCoords = _chunkRefreshQueue.Dequeue();
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

        _chunkRefreshQueue.Enqueue(chunkCoords);
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
                SetSubChunkVisible(column.SubChunks[subChunkY], false);
                continue;
            }

            if (RefreshSubChunk(column, chunkX, subChunkY, chunkZ))
            {
                hasGeometry = true;
            }
        }

        column.Root.SetActive(hasGeometry);
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
            SubChunkInstance subChunk = column.SubChunks[subChunkY];
            VoxelMesher.PendingSubChunkMeshData pendingSubChunk = pendingColumn.subChunks[subChunkY];
            if (pendingSubChunk == null)
            {
                SetSubChunkVisible(subChunk, false);
                continue;
            }

            bool subChunkHasGeometry = VoxelMesher.ApplyPendingSubChunkMesh(
                pendingSubChunk,
                subChunk.Mesh,
                $"SubChunk_{chunkCoords.x}_{subChunkY}_{chunkCoords.y}");

            SetSubChunkVisible(subChunk, subChunkHasGeometry);
            hasGeometry |= subChunkHasGeometry;
        }

        column.Root.SetActive(hasGeometry);
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

        for (int subChunkY = 0; subChunkY < column.SubChunks.Length; subChunkY++)
        {
            SubChunkInstance subChunk = column.SubChunks[subChunkY];
            subChunk.GameObject.transform.localPosition = new Vector3(0f, subChunkY * VoxelTerrainData.SubChunkSize, 0f);
            SetSubChunkVisible(subChunk, false);
        }

        _chunkColumnInstances.Add(chunkCoords, column);
        return column;
    }

    private ChunkColumnInstance CreateChunkColumnInstance()
    {
        GameObject rootObject = new("ChunkColumn");
        ChunkColumnInstance column = new() { Root = rootObject };

        for (int subChunkY = 0; subChunkY < VoxelTerrainData.SubChunkCountY; subChunkY++)
        {
            column.SubChunks[subChunkY] = CreateSubChunkInstance(rootObject.transform, subChunkY);
        }

        rootObject.SetActive(false);
        return column;
    }

    private SubChunkInstance CreateSubChunkInstance(Transform parent, int subChunkY)
    {
        GameObject subChunkObject = new($"SubChunk_{subChunkY}");
        subChunkObject.transform.SetParent(parent, false);

        MeshFilter meshFilter = subChunkObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = subChunkObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = worldMaterial;
        meshRenderer.shadowCastingMode = ShadowCastingMode.On;
        meshRenderer.receiveShadows = true;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        meshRenderer.allowOcclusionWhenDynamic = false;
        meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

        MeshCollider meshCollider = subChunkObject.AddComponent<MeshCollider>();
        meshCollider.cookingOptions =
            MeshColliderCookingOptions.CookForFasterSimulation |
            MeshColliderCookingOptions.EnableMeshCleaning |
            MeshColliderCookingOptions.WeldColocatedVertices |
            MeshColliderCookingOptions.UseFastMidphase;

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

    private bool RefreshSubChunk(ChunkColumnInstance column, int chunkX, int subChunkY, int chunkZ)
    {
        if (!_terrain.HasSolidBlocksInSubChunk(chunkX, subChunkY, chunkZ))
        {
            SetSubChunkVisible(column.SubChunks[subChunkY], false);
            return false;
        }

        SubChunkInstance subChunk = column.SubChunks[subChunkY];
        bool hasGeometry = VoxelMesher.RebuildSubChunkMesh(subChunk.Mesh, _terrain, blockDatabase, chunkX, subChunkY, chunkZ);
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
        for (int subChunkY = 0; subChunkY < column.SubChunks.Length; subChunkY++)
        {
            SetSubChunkVisible(column.SubChunks[subChunkY], false);
        }

        _chunkColumnInstances.Remove(chunkCoords);
        _chunkColumnPool.Push(column);
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

        for (int subChunkY = 0; subChunkY < column.SubChunks.Length; subChunkY++)
        {
            SubChunkInstance subChunk = column.SubChunks[subChunkY];
            if (subChunk == null)
            {
                continue;
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

        if (column.Root != null)
        {
            Object.Destroy(column.Root);
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
            _selectedPlacementBlock = VoxelBlockType.Dirt;
        }
        else if (keyboard.digit2Key.wasPressedThisFrame)
        {
            _selectedPlacementBlock = VoxelBlockType.Rock;
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

        if (!TryRaycastWorld(ray, out RaycastHit hit))
        {
            SetSelectionVisible(false);
            return;
        }

        Vector3 localHitPoint = _worldRoot.InverseTransformPoint(hit.point);
        Vector3 localHitNormal = _worldRoot.InverseTransformDirection(hit.normal).normalized;

        Vector3Int selectedBlock = WorldPointToBlock(localHitPoint - (localHitNormal * 0.01f));
        if (!_terrain.IsInBounds(selectedBlock.x, selectedBlock.y, selectedBlock.z) ||
            _terrain.GetBlock(selectedBlock.x, selectedBlock.y, selectedBlock.z) == VoxelBlockType.Air)
        {
            SetSelectionVisible(false);
            return;
        }

        _selectedBlockPosition = selectedBlock;
        _placementBlockPosition = WorldPointToBlock(localHitPoint + (localHitNormal * 0.01f));
        _hasSelection = true;

        _selectionOutline.transform.SetParent(_worldRoot, false);
        _selectionOutline.transform.localPosition = selectedBlock;
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

        if (_terrain.GetBlock(_selectedBlockPosition.x, _selectedBlockPosition.y, _selectedBlockPosition.z) == VoxelBlockType.Air)
        {
            return;
        }

        if (!_terrain.SetBlock(_selectedBlockPosition.x, _selectedBlockPosition.y, _selectedBlockPosition.z, VoxelBlockType.Air))
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

        if (_terrain.GetBlock(_placementBlockPosition.x, _placementBlockPosition.y, _placementBlockPosition.z) != VoxelBlockType.Air)
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

        RefreshSubChunk(column, chunkX, subChunkY, chunkZ);

        bool hasAnyGeometry = false;
        for (int i = 0; i < column.SubChunks.Length; i++)
        {
            if (column.SubChunks[i].GameObject.activeSelf)
            {
                hasAnyGeometry = true;
                break;
            }
        }

        column.Root.SetActive(hasAnyGeometry);
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
                _faceTextureLookup[(blockId * 6) + face] = blockDatabase.GetFaceTextureLayer(blockId, (VoxelBlockFace)face);
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
        if (_selectionOutline != null)
        {
            _selectionOutline.SetActive(visible);
        }
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
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowDistance = 120f;
        QualitySettings.shadowResolution = ShadowResolution.High;
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

        if (blockDatabase == null)
        {
            Debug.LogError("VoxelWorldRuntime requires a Block Database reference.", this);
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
            isValid &= ValidateRequiredBlockDefinition((ushort)VoxelBlockType.Dirt);
            isValid &= ValidateRequiredBlockDefinition((ushort)VoxelBlockType.Rock);
        }

        return isValid;
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
        if (terrainSettings.dirt.baseHeight != 0 || terrainSettings.rock.baseHeight != 0)
        {
            return;
        }

        terrainSettings = VoxelTerrainGenerationSettings.Default;
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
}

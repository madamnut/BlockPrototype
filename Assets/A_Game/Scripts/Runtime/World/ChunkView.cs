using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class ChunkView
{
    private sealed class CombinedMeshBuffers
    {
        public readonly List<Vector3> vertices = new(1024);
        public readonly List<Vector2> uvs = new(1024);
        public readonly List<Vector3> normals = new(1024);
        public readonly List<Color32> colors = new(1024);
        public readonly List<int>[] triangles =
        {
            new(1536),
            new(768),
            new(768),
        };

        public void Clear()
        {
            vertices.Clear();
            uvs.Clear();
            normals.Clear();
            colors.Clear();

            for (int i = 0; i < triangles.Length; i++)
            {
                triangles[i].Clear();
            }
        }
    }

    private sealed class SubChunkInstance
    {
        public GameObject GameObject;
        public MeshFilter MeshFilter;
        public MeshRenderer MeshRenderer;
        public Mesh Mesh;
        public bool HasFluidGeometry;
    }

    private sealed class ChunkColumnInstance
    {
        public GameObject Root;
        public SubChunkInstance[] SubChunks = new SubChunkInstance[TerrainData.SubChunkCountY];
    }

    private readonly Material _worldMaterial;
    private readonly Material _fluidMaterial;
    private readonly Material _foliageMaterial;
    private readonly GameObject _chunkColumnPrefab;
    private readonly Dictionary<Vector2Int, ChunkColumnInstance> _chunkColumnInstances = new();
    private readonly Stack<ChunkColumnInstance> _chunkColumnPool = new();
    private readonly CombinedMeshBuffers _combinedMeshBuffers = new();
    private Transform _worldRoot;
    private Vector2Int _currentCenterChunk;

    public ChunkView(Material worldMaterial, Material fluidMaterial, Material foliageMaterial, GameObject chunkColumnPrefab)
    {
        _worldMaterial = worldMaterial;
        _fluidMaterial = fluidMaterial;
        _foliageMaterial = foliageMaterial;
        _chunkColumnPrefab = chunkColumnPrefab;
    }

    public void SetWorldRoot(Transform worldRoot)
    {
        _worldRoot = worldRoot;
    }

    public bool ContainsChunkColumn(Vector2Int chunkCoords)
    {
        return _chunkColumnInstances.ContainsKey(TerrainData.WrapChunkCoords(chunkCoords.x, chunkCoords.y));
    }

    public void SetVisibleChunkCenter(Vector2Int centerChunk)
    {
        _currentCenterChunk = centerChunk;
        foreach (KeyValuePair<Vector2Int, ChunkColumnInstance> pair in _chunkColumnInstances)
        {
            UpdateChunkColumnTransform(pair.Key, pair.Value);
        }
    }

    public void PrewarmChunkColumnPool(int count)
    {
        int targetCount = Mathf.Max(0, count);
        while (_chunkColumnPool.Count < targetCount)
        {
            ChunkColumnInstance column = CreateChunkColumnInstance();
            if (column == null)
            {
                break;
            }

            column.Root.SetActive(false);
            _chunkColumnPool.Push(column);
        }
    }

    public void ReleaseChunkColumn(Vector2Int chunkCoords)
    {
        chunkCoords = TerrainData.WrapChunkCoords(chunkCoords.x, chunkCoords.y);
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

    public void DestroyAll()
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

    public void ApplyPendingChunkColumnMesh(
        Vector2Int chunkCoords,
        VoxelMesher.PendingChunkColumnMesh pendingColumn,
        TerrainData terrain,
        BlockDatabase blockDatabase)
    {
        chunkCoords = TerrainData.WrapChunkCoords(chunkCoords.x, chunkCoords.y);
        ChunkColumnInstance column = GetOrCreateChunkColumnInstance(chunkCoords.x, chunkCoords.y);
        if (column == null)
        {
            return;
        }

        column.Root.SetActive(false);

        bool hasGeometry = false;
        for (int subChunkY = 0; subChunkY < TerrainData.SubChunkCountY; subChunkY++)
        {
            SubChunkInstance subChunk = column.SubChunks[subChunkY];
            VoxelMesher.PendingSubChunkMeshData pendingSubChunk = pendingColumn.subChunks[subChunkY];
            _combinedMeshBuffers.Clear();
            bool hasSolidGeometry = AppendPendingSolidSubChunk(pendingSubChunk);
            bool hasFluidGeometry = AppendFluidSubChunk(terrain, chunkCoords.x, subChunkY, chunkCoords.y);
            bool hasFoliageGeometry = AppendFoliageSubChunk(terrain, blockDatabase, chunkCoords.x, subChunkY, chunkCoords.y);
            bool subChunkHasGeometry = ApplyCombinedSubChunkMesh(subChunk, $"SubChunk_{chunkCoords.x}_{subChunkY}_{chunkCoords.y}");
            subChunk.HasFluidGeometry = hasFluidGeometry;
            SetSubChunkVisible(subChunk, subChunkHasGeometry);
            hasGeometry |= hasSolidGeometry || hasFluidGeometry || hasFoliageGeometry;
        }

        column.Root.SetActive(hasGeometry);
    }

    public void RefreshLoadedSubChunk(int chunkX, int subChunkY, int chunkZ, TerrainData terrain, BlockDatabase blockDatabase)
    {
        Vector2Int chunkCoords = TerrainData.WrapChunkCoords(chunkX, chunkZ);
        if (!_chunkColumnInstances.TryGetValue(chunkCoords, out ChunkColumnInstance column))
        {
            return;
        }

        SubChunkInstance subChunk = column.SubChunks[subChunkY];
        _combinedMeshBuffers.Clear();
        bool hasSolidGeometry = AppendSolidSubChunk(terrain, blockDatabase, chunkX, subChunkY, chunkZ);
        bool hasFluidGeometry = AppendFluidSubChunk(terrain, chunkX, subChunkY, chunkZ);
        bool hasFoliageGeometry = AppendFoliageSubChunk(terrain, blockDatabase, chunkX, subChunkY, chunkZ);
        bool subChunkHasGeometry = ApplyCombinedSubChunkMesh(subChunk, $"SubChunk_{chunkX}_{subChunkY}_{chunkZ}");

        subChunk.HasFluidGeometry = hasFluidGeometry;
        SetSubChunkVisible(subChunk, subChunkHasGeometry);
        column.Root.SetActive(HasAnyVisibleGeometry(column));
    }

    public void PopulateVisibleFluidRenderers(WaterReflection waterReflection)
    {
        if (waterReflection == null)
        {
            return;
        }

        waterReflection.ResetVisibleFluidRenderers();
        foreach (ChunkColumnInstance column in _chunkColumnInstances.Values)
        {
            if (column == null)
            {
                continue;
            }

            for (int subChunkY = 0; subChunkY < column.SubChunks.Length; subChunkY++)
            {
                SubChunkInstance subChunk = column.SubChunks[subChunkY];
                if (IsSubChunkVisible(subChunk) && subChunk.MeshRenderer != null && subChunk.HasFluidGeometry)
                {
                    waterReflection.AddVisibleFluidRenderer(subChunk.MeshRenderer);
                }
            }
        }
    }

    private ChunkColumnInstance GetOrCreateChunkColumnInstance(int chunkX, int chunkZ)
    {
        Vector2Int chunkCoords = TerrainData.WrapChunkCoords(chunkX, chunkZ);
        if (_chunkColumnInstances.TryGetValue(chunkCoords, out ChunkColumnInstance column))
        {
            UpdateChunkColumnTransform(chunkCoords, column);
            return column;
        }

        column = _chunkColumnPool.Count > 0 ? _chunkColumnPool.Pop() : CreateChunkColumnInstance();
        if (column == null)
        {
            return null;
        }

        column.Root.transform.SetParent(_worldRoot, false);
        UpdateChunkColumnTransform(chunkCoords, column);
        column.Root.SetActive(false);

        for (int subChunkY = 0; subChunkY < column.SubChunks.Length; subChunkY++)
        {
            SubChunkInstance subChunk = column.SubChunks[subChunkY];
            subChunk.GameObject.transform.localPosition = new Vector3(0f, subChunkY * TerrainData.SubChunkSize, 0f);
            SetSubChunkVisible(subChunk, false);
        }

        _chunkColumnInstances.Add(chunkCoords, column);
        return column;
    }

    private void UpdateChunkColumnTransform(Vector2Int wrappedChunkCoords, ChunkColumnInstance column)
    {
        int displayChunkX = TerrainData.GetDisplayChunkCoord(wrappedChunkCoords.x, _currentCenterChunk.x);
        int displayChunkZ = TerrainData.GetDisplayChunkCoord(wrappedChunkCoords.y, _currentCenterChunk.y);
        column.Root.transform.localPosition = new Vector3(displayChunkX * TerrainData.ChunkSize, 0f, displayChunkZ * TerrainData.ChunkSize);
        column.Root.name = $"ChunkColumn_{wrappedChunkCoords.x}_{wrappedChunkCoords.y}";
    }

    private ChunkColumnInstance CreateChunkColumnInstance()
    {
        if (_chunkColumnPrefab != null)
        {
            if (TryCreateChunkColumnInstanceFromPrefab(out ChunkColumnInstance prefabColumn))
            {
                prefabColumn.Root.SetActive(false);
                return prefabColumn;
            }

            Debug.LogError("ChunkView chunkColumnPrefab does not match the new single-SubChunk binding layout. Regenerate or update the prefab instead of falling back.");
            return null;
        }

        GameObject rootObject = CreateChunkColumnRootObject(usePrefab: false);
        ChunkColumnInstance column = new() { Root = rootObject };

        for (int subChunkY = 0; subChunkY < TerrainData.SubChunkCountY; subChunkY++)
        {
            column.SubChunks[subChunkY] = CreateSubChunkInstance(rootObject.transform, subChunkY);
        }

        rootObject.SetActive(false);
        return column;
    }

    private bool TryCreateChunkColumnInstanceFromPrefab(out ChunkColumnInstance column)
    {
        column = null;
        if (_chunkColumnPrefab == null)
        {
            return false;
        }

        GameObject rootObject = CreateChunkColumnRootObject(usePrefab: true);
        Chunk binding = rootObject.GetComponent<Chunk>();
        if (binding == null)
        {
            Object.Destroy(rootObject);
            return false;
        }

        if (!HasValidPrefabBinding(binding))
        {
            binding.GenerateSubChunks();
            binding.RebindExistingSubChunks();
        }

        if (!HasValidPrefabBinding(binding))
        {
            Object.Destroy(rootObject);
            return false;
        }

        column = new ChunkColumnInstance { Root = rootObject };
        for (int subChunkY = 0; subChunkY < TerrainData.SubChunkCountY; subChunkY++)
        {
            column.SubChunks[subChunkY] = CreateSubChunkInstanceFromBinding(binding.SubChunks[subChunkY], subChunkY);
        }

        return true;
    }

    private GameObject CreateChunkColumnRootObject(bool usePrefab)
    {
        if (usePrefab && _chunkColumnPrefab != null)
        {
            GameObject instance = Object.Instantiate(_chunkColumnPrefab);
            instance.name = _chunkColumnPrefab.name;
            return instance;
        }

        return new GameObject("ChunkColumn");
    }

    private static bool HasValidPrefabBinding(Chunk binding)
    {
        return HasValidBindingArray(binding.SubChunks);
    }

    private static bool HasValidBindingArray(SubChunk[] bindings)
    {
        if (bindings == null || bindings.Length != TerrainData.SubChunkCountY)
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

    private SubChunkInstance CreateSubChunkInstanceFromBinding(SubChunk binding, int subChunkY)
    {
        MeshFilter meshFilter = binding.MeshFilter;
        MeshRenderer meshRenderer = binding.MeshRenderer;
        ConfigureSubChunkRenderer(meshRenderer);

        Mesh mesh = CreateRuntimeMesh($"SubChunkMesh_{subChunkY}");
        meshFilter.sharedMesh = mesh;

        SubChunkInstance instance = new()
        {
            GameObject = binding.gameObject,
            MeshFilter = meshFilter,
            MeshRenderer = meshRenderer,
            Mesh = mesh,
            HasFluidGeometry = false,
        };

        SetSubChunkVisible(instance, false);
        return instance;
    }

    private SubChunkInstance CreateSubChunkInstance(Transform parent, int subChunkY)
    {
        GameObject subChunkObject = new($"SubChunk_{subChunkY}");
        subChunkObject.transform.SetParent(parent, false);

        MeshFilter meshFilter = subChunkObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = subChunkObject.AddComponent<MeshRenderer>();
        ConfigureSubChunkRenderer(meshRenderer);

        Mesh mesh = CreateRuntimeMesh($"SubChunkMesh_{subChunkY}");
        meshFilter.sharedMesh = mesh;

        SubChunkInstance instance = new()
        {
            GameObject = subChunkObject,
            MeshFilter = meshFilter,
            MeshRenderer = meshRenderer,
            Mesh = mesh,
            HasFluidGeometry = false,
        };

        SetSubChunkVisible(instance, false);
        return instance;
    }

    private void ConfigureSubChunkRenderer(MeshRenderer meshRenderer)
    {
        meshRenderer.sharedMaterials = new[] { _worldMaterial, _fluidMaterial, _foliageMaterial };
        meshRenderer.shadowCastingMode = ShadowCastingMode.On;
        meshRenderer.receiveShadows = true;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        meshRenderer.allowOcclusionWhenDynamic = false;
        meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }

    private static Mesh CreateRuntimeMesh(string name)
    {
        return new Mesh
        {
            name = name,
            indexFormat = IndexFormat.UInt32,
        };
    }

    private bool AppendSolidSubChunk(TerrainData terrain, BlockDatabase blockDatabase, int chunkX, int subChunkY, int chunkZ)
    {
        if (!terrain.HasSolidBlocksInSubChunk(chunkX, subChunkY, chunkZ))
        {
            return false;
        }

        return VoxelMesher.AppendSubChunkGeometry(
            _combinedMeshBuffers.vertices,
            _combinedMeshBuffers.triangles[0],
            _combinedMeshBuffers.uvs,
            _combinedMeshBuffers.normals,
            _combinedMeshBuffers.colors,
            terrain,
            blockDatabase,
            chunkX,
            subChunkY,
            chunkZ);
    }

    private bool AppendPendingSolidSubChunk(VoxelMesher.PendingSubChunkMeshData pendingSubChunk)
    {
        return VoxelMesher.AppendPendingSubChunkGeometry(
            pendingSubChunk,
            _combinedMeshBuffers.vertices,
            _combinedMeshBuffers.triangles[0],
            _combinedMeshBuffers.uvs,
            _combinedMeshBuffers.normals,
            _combinedMeshBuffers.colors);
    }

    private bool AppendFluidSubChunk(TerrainData terrain, int chunkX, int subChunkY, int chunkZ)
    {
        if (!terrain.HasFluidInSubChunk(chunkX, subChunkY, chunkZ))
        {
            return false;
        }

        return VoxelFluidMesher.AppendSubChunkGeometry(
            _combinedMeshBuffers.vertices,
            _combinedMeshBuffers.triangles[1],
            _combinedMeshBuffers.uvs,
            _combinedMeshBuffers.normals,
            _combinedMeshBuffers.colors,
            terrain,
            chunkX,
            subChunkY,
            chunkZ);
    }

    private bool AppendFoliageSubChunk(TerrainData terrain, BlockDatabase blockDatabase, int chunkX, int subChunkY, int chunkZ)
    {
        if (!terrain.HasFoliageInSubChunk(chunkX, subChunkY, chunkZ))
        {
            return false;
        }

        return VoxelFoliageMesher.AppendSubChunkGeometry(
            _combinedMeshBuffers.vertices,
            _combinedMeshBuffers.triangles[2],
            _combinedMeshBuffers.uvs,
            _combinedMeshBuffers.normals,
            _combinedMeshBuffers.colors,
            terrain,
            blockDatabase,
            chunkX,
            subChunkY,
            chunkZ);
    }

    private bool ApplyCombinedSubChunkMesh(SubChunkInstance subChunk, string meshName)
    {
        Mesh mesh = subChunk.Mesh;
        mesh.Clear();
        if (_combinedMeshBuffers.vertices.Count == 0)
        {
            return false;
        }

        mesh.name = meshName;
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(_combinedMeshBuffers.vertices);
        mesh.SetUVs(0, _combinedMeshBuffers.uvs);
        mesh.SetNormals(_combinedMeshBuffers.normals);
        mesh.SetColors(_combinedMeshBuffers.colors);
        mesh.subMeshCount = 3;
        mesh.SetTriangles(_combinedMeshBuffers.triangles[0], 0, false);
        mesh.SetTriangles(_combinedMeshBuffers.triangles[1], 1, false);
        mesh.SetTriangles(_combinedMeshBuffers.triangles[2], 2, true);
        mesh.RecalculateBounds();
        mesh.UploadMeshData(false);
        return true;
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
            subChunk.HasFluidGeometry = false;
            if (subChunk.Mesh != null)
            {
                subChunk.Mesh.Clear();
            }

            return;
        }

        subChunk.GameObject.SetActive(true);
    }

    private static void DestroyChunkColumnInstance(ChunkColumnInstance column)
    {
        if (column == null)
        {
            return;
        }

        for (int subChunkY = 0; subChunkY < column.SubChunks.Length; subChunkY++)
        {
            DestroySubChunkInstance(column.SubChunks[subChunkY]);
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

        if (subChunk.MeshFilter != null)
        {
            subChunk.MeshFilter.sharedMesh = null;
        }

        if (subChunk.Mesh != null)
        {
            Object.Destroy(subChunk.Mesh);
        }
    }

    private static bool HasAnyVisibleGeometry(ChunkColumnInstance column)
    {
        if (column == null)
        {
            return false;
        }

        for (int i = 0; i < TerrainData.SubChunkCountY; i++)
        {
            if (IsSubChunkVisible(column.SubChunks[i]))
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
}

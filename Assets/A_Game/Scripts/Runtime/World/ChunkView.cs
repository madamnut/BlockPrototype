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
        public readonly List<Vector3> sourceVertices = new(1024);
        public readonly List<Vector2> sourceUvs = new(1024);
        public readonly List<Vector3> sourceNormals = new(1024);
        public readonly List<int> sourceTriangles = new(1536);
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
            sourceVertices.Clear();
            sourceUvs.Clear();
            sourceNormals.Clear();
            sourceTriangles.Clear();

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
        public Mesh SolidMesh;
        public Mesh FluidMesh;
        public Mesh FoliageMesh;
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

            bool hasSolidGeometry = false;
            if (pendingSubChunk != null)
            {
                pendingSubChunk.Complete();
                hasSolidGeometry = pendingSubChunk.HasGeometry;
            }

            subChunk.SolidMesh.Clear();
            bool hasFluidGeometry = RebuildFluidSubChunk(subChunk, terrain, chunkCoords.x, subChunkY, chunkCoords.y);
            bool hasFoliageGeometry = RebuildFoliageSubChunk(subChunk, terrain, blockDatabase, chunkCoords.x, subChunkY, chunkCoords.y);

            bool subChunkHasGeometry = RebuildCombinedSubChunkMesh(
                subChunk,
                pendingSubChunk,
                $"SubChunk_{chunkCoords.x}_{subChunkY}_{chunkCoords.y}");
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
        bool hasSolidGeometry = RefreshSolidSubChunk(subChunk, terrain, blockDatabase, chunkX, subChunkY, chunkZ);
        bool hasFluidGeometry = RebuildFluidSubChunk(subChunk, terrain, chunkX, subChunkY, chunkZ);
        bool hasFoliageGeometry = RebuildFoliageSubChunk(subChunk, terrain, blockDatabase, chunkX, subChunkY, chunkZ);
        bool subChunkHasGeometry = RebuildCombinedSubChunkMesh(subChunk, null, $"SubChunk_{chunkX}_{subChunkY}_{chunkZ}");

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
            SolidMesh = CreateRuntimeMesh($"SolidSubChunkMesh_{subChunkY}"),
            FluidMesh = CreateRuntimeMesh($"FluidSubChunkMesh_{subChunkY}"),
            FoliageMesh = CreateRuntimeMesh($"FoliageSubChunkMesh_{subChunkY}"),
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
            SolidMesh = CreateRuntimeMesh($"SolidSubChunkMesh_{subChunkY}"),
            FluidMesh = CreateRuntimeMesh($"FluidSubChunkMesh_{subChunkY}"),
            FoliageMesh = CreateRuntimeMesh($"FoliageSubChunkMesh_{subChunkY}"),
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

    private static bool RefreshSolidSubChunk(SubChunkInstance subChunk, TerrainData terrain, BlockDatabase blockDatabase, int chunkX, int subChunkY, int chunkZ)
    {
        if (!terrain.HasSolidBlocksInSubChunk(chunkX, subChunkY, chunkZ))
        {
            subChunk.SolidMesh.Clear();
            return false;
        }

        return VoxelMesher.RebuildSubChunkMesh(subChunk.SolidMesh, terrain, blockDatabase, chunkX, subChunkY, chunkZ);
    }

    private static bool RebuildFluidSubChunk(SubChunkInstance subChunk, TerrainData terrain, int chunkX, int subChunkY, int chunkZ)
    {
        if (!terrain.HasFluidInSubChunk(chunkX, subChunkY, chunkZ))
        {
            subChunk.FluidMesh.Clear();
            return false;
        }

        return VoxelFluidMesher.RebuildSubChunkMesh(subChunk.FluidMesh, terrain, chunkX, subChunkY, chunkZ);
    }

    private static bool RebuildFoliageSubChunk(SubChunkInstance subChunk, TerrainData terrain, BlockDatabase blockDatabase, int chunkX, int subChunkY, int chunkZ)
    {
        if (!terrain.HasFoliageInSubChunk(chunkX, subChunkY, chunkZ))
        {
            subChunk.FoliageMesh.Clear();
            return false;
        }

        return VoxelFoliageMesher.RebuildSubChunkMesh(subChunk.FoliageMesh, terrain, blockDatabase, chunkX, subChunkY, chunkZ);
    }

    private bool RebuildCombinedSubChunkMesh(SubChunkInstance subChunk, VoxelMesher.PendingSubChunkMeshData pendingSolidSubChunk, string meshName)
    {
        Mesh mesh = subChunk.Mesh;
        mesh.Clear();
        _combinedMeshBuffers.Clear();

        bool hasAnyGeometry = false;
        hasAnyGeometry |= AppendPendingSolidMesh(pendingSolidSubChunk, 0);
        hasAnyGeometry |= AppendSourceMesh(subChunk.SolidMesh, 0);
        hasAnyGeometry |= AppendSourceMesh(subChunk.FluidMesh, 1);
        hasAnyGeometry |= AppendSourceMesh(subChunk.FoliageMesh, 2);

        if (!hasAnyGeometry)
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

    private bool AppendPendingSolidMesh(VoxelMesher.PendingSubChunkMeshData pending, int subMeshIndex)
    {
        if (pending == null)
        {
            return false;
        }

        pending.Complete();
        if (!pending.HasGeometry)
        {
            return false;
        }

        int vertexOffset = _combinedMeshBuffers.vertices.Count;
        for (int i = 0; i < pending.vertices.Length; i++)
        {
            _combinedMeshBuffers.vertices.Add(pending.vertices[i]);
        }

        for (int i = 0; i < pending.uvs.Length; i++)
        {
            _combinedMeshBuffers.uvs.Add(pending.uvs[i]);
        }

        for (int i = 0; i < pending.normals.Length; i++)
        {
            _combinedMeshBuffers.normals.Add(pending.normals[i]);
        }

        for (int i = 0; i < pending.colors.Length; i++)
        {
            _combinedMeshBuffers.colors.Add(pending.colors[i]);
        }

        List<int> destinationTriangles = _combinedMeshBuffers.triangles[subMeshIndex];
        for (int i = 0; i < pending.indices.Length; i++)
        {
            destinationTriangles.Add(pending.indices[i] + vertexOffset);
        }

        return true;
    }

    private bool AppendSourceMesh(Mesh sourceMesh, int subMeshIndex)
    {
        if (sourceMesh == null || sourceMesh.vertexCount == 0)
        {
            return false;
        }

        sourceMesh.GetVertices(_combinedMeshBuffers.sourceVertices);
        List<Vector3> sourceVertices = _combinedMeshBuffers.sourceVertices;
        int vertexOffset = _combinedMeshBuffers.vertices.Count;
        _combinedMeshBuffers.vertices.AddRange(_combinedMeshBuffers.sourceVertices);

        sourceMesh.GetUVs(0, _combinedMeshBuffers.sourceUvs);
        if (_combinedMeshBuffers.sourceUvs.Count == sourceVertices.Count)
        {
            _combinedMeshBuffers.uvs.AddRange(_combinedMeshBuffers.sourceUvs);
        }
        else
        {
            for (int i = 0; i < sourceVertices.Count; i++)
            {
                _combinedMeshBuffers.uvs.Add(Vector2.zero);
            }
        }

        sourceMesh.GetNormals(_combinedMeshBuffers.sourceNormals);
        if (_combinedMeshBuffers.sourceNormals.Count == sourceVertices.Count)
        {
            _combinedMeshBuffers.normals.AddRange(_combinedMeshBuffers.sourceNormals);
        }
        else
        {
            for (int i = 0; i < sourceVertices.Count; i++)
            {
                _combinedMeshBuffers.normals.Add(Vector3.up);
            }
        }

        Color32[] sourceColors = sourceMesh.colors32;
        if (sourceColors != null && sourceColors.Length == sourceVertices.Count)
        {
            _combinedMeshBuffers.colors.AddRange(sourceColors);
        }
        else
        {
            for (int i = 0; i < sourceVertices.Count; i++)
            {
                _combinedMeshBuffers.colors.Add(new Color32(255, 255, 255, 255));
            }
        }

        sourceMesh.GetTriangles(_combinedMeshBuffers.sourceTriangles, 0, false);
        List<int> destinationTriangles = _combinedMeshBuffers.triangles[subMeshIndex];
        for (int i = 0; i < _combinedMeshBuffers.sourceTriangles.Count; i++)
        {
            destinationTriangles.Add(_combinedMeshBuffers.sourceTriangles[i] + vertexOffset);
        }

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

        if (subChunk.SolidMesh != null)
        {
            Object.Destroy(subChunk.SolidMesh);
        }

        if (subChunk.FluidMesh != null)
        {
            Object.Destroy(subChunk.FluidMesh);
        }

        if (subChunk.FoliageMesh != null)
        {
            Object.Destroy(subChunk.FoliageMesh);
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

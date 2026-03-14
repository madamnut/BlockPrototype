using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class ChunkView
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
        public SubChunkInstance[] SolidSubChunks = new SubChunkInstance[TerrainData.SubChunkCountY];
        public SubChunkInstance[] FluidSubChunks = new SubChunkInstance[TerrainData.SubChunkCountY];
        public SubChunkInstance[] FoliageSubChunks = new SubChunkInstance[TerrainData.SubChunkCountY];
    }

    private readonly Material _worldMaterial;
    private readonly Material _fluidMaterial;
    private readonly Material _foliageMaterial;
    private readonly GameObject _chunkColumnPrefab;
    private readonly Dictionary<Vector2Int, ChunkColumnInstance> _chunkColumnInstances = new();
    private readonly Stack<ChunkColumnInstance> _chunkColumnPool = new();
    private Transform _worldRoot;

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
        return _chunkColumnInstances.ContainsKey(chunkCoords);
    }

    public void ReleaseChunkColumn(Vector2Int chunkCoords)
    {
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
        ChunkColumnInstance column = GetOrCreateChunkColumnInstance(chunkCoords.x, chunkCoords.y);
        column.Root.SetActive(false);

        bool hasGeometry = false;
        for (int subChunkY = 0; subChunkY < TerrainData.SubChunkCountY; subChunkY++)
        {
            SubChunkInstance solidSubChunk = column.SolidSubChunks[subChunkY];
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

            hasGeometry |= RefreshFluidSubChunk(column, terrain, chunkCoords.x, subChunkY, chunkCoords.y);
            hasGeometry |= RefreshFoliageSubChunk(column, terrain, blockDatabase, chunkCoords.x, subChunkY, chunkCoords.y);
        }

        column.Root.SetActive(hasGeometry);
        SyncSolidChunkColliders(column);
    }

    public void ApplyGpuChunkColumnMesh(
        Vector2Int chunkCoords,
        TerrainGpuMesher.ChunkColumnMeshData meshData,
        TerrainData terrain,
        BlockDatabase blockDatabase)
    {
        ChunkColumnInstance column = GetOrCreateChunkColumnInstance(chunkCoords.x, chunkCoords.y);
        column.Root.SetActive(false);

        bool hasGeometry = false;
        for (int subChunkY = 0; subChunkY < TerrainData.SubChunkCountY; subChunkY++)
        {
            SubChunkInstance solidSubChunk = column.SolidSubChunks[subChunkY];
            TerrainGpuMesher.SubChunkMeshData gpuSubChunk = meshData != null ? meshData.subChunks[subChunkY] : null;
            bool subChunkHasGeometry = ApplyGpuSubChunkMesh(solidSubChunk.Mesh, gpuSubChunk, $"SubChunk_{chunkCoords.x}_{subChunkY}_{chunkCoords.y}");
            SetSubChunkVisible(solidSubChunk, subChunkHasGeometry);
            hasGeometry |= subChunkHasGeometry;

            hasGeometry |= RefreshFluidSubChunk(column, terrain, chunkCoords.x, subChunkY, chunkCoords.y);
            hasGeometry |= RefreshFoliageSubChunk(column, terrain, blockDatabase, chunkCoords.x, subChunkY, chunkCoords.y);
        }

        column.Root.SetActive(hasGeometry);
        SyncSolidChunkColliders(column);
    }

    public void RefreshLoadedSubChunk(int chunkX, int subChunkY, int chunkZ, TerrainData terrain, BlockDatabase blockDatabase)
    {
        Vector2Int chunkCoords = new(chunkX, chunkZ);
        if (!_chunkColumnInstances.TryGetValue(chunkCoords, out ChunkColumnInstance column))
        {
            return;
        }

        RefreshSolidSubChunk(column, terrain, blockDatabase, chunkX, subChunkY, chunkZ);
        RefreshFluidSubChunk(column, terrain, chunkX, subChunkY, chunkZ);
        RefreshFoliageSubChunk(column, terrain, blockDatabase, chunkX, subChunkY, chunkZ);
        column.Root.SetActive(HasAnyVisibleGeometry(column));
        SyncSolidChunkColliders(column);
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

            for (int subChunkY = 0; subChunkY < column.FluidSubChunks.Length; subChunkY++)
            {
                SubChunkInstance subChunk = column.FluidSubChunks[subChunkY];
                if (IsSubChunkVisible(subChunk) && subChunk.MeshRenderer != null)
                {
                    waterReflection.AddVisibleFluidRenderer(subChunk.MeshRenderer);
                }
            }
        }
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
        column.Root.transform.localPosition = new Vector3(chunkX * TerrainData.ChunkSize, 0f, chunkZ * TerrainData.ChunkSize);
        column.Root.name = $"ChunkColumn_{chunkX}_{chunkZ}";
        column.Root.SetActive(false);

        for (int subChunkY = 0; subChunkY < column.SolidSubChunks.Length; subChunkY++)
        {
            SubChunkInstance solidSubChunk = column.SolidSubChunks[subChunkY];
            SubChunkInstance fluidSubChunk = column.FluidSubChunks[subChunkY];
            SubChunkInstance foliageSubChunk = column.FoliageSubChunks[subChunkY];
            solidSubChunk.GameObject.transform.localPosition = new Vector3(0f, subChunkY * TerrainData.SubChunkSize, 0f);
            fluidSubChunk.GameObject.transform.localPosition = new Vector3(0f, subChunkY * TerrainData.SubChunkSize, 0f);
            foliageSubChunk.GameObject.transform.localPosition = new Vector3(0f, subChunkY * TerrainData.SubChunkSize, 0f);
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

        for (int subChunkY = 0; subChunkY < TerrainData.SubChunkCountY; subChunkY++)
        {
            column.SolidSubChunks[subChunkY] = CreateSubChunkInstance(rootObject.transform, subChunkY, _worldMaterial, true, true, true);
            column.FluidSubChunks[subChunkY] = CreateSubChunkInstance(rootObject.transform, subChunkY, _fluidMaterial, false, false, false);
            column.FoliageSubChunks[subChunkY] = CreateSubChunkInstance(rootObject.transform, subChunkY, _foliageMaterial, false, true, true);
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

        GameObject rootObject = CreateChunkColumnRootObject();
        Chunk binding = rootObject.GetComponent<Chunk>();
        if (binding == null || !HasValidPrefabBinding(binding))
        {
            Object.Destroy(rootObject);
            return false;
        }

        column = new ChunkColumnInstance { Root = rootObject };
        for (int subChunkY = 0; subChunkY < TerrainData.SubChunkCountY; subChunkY++)
        {
            column.SolidSubChunks[subChunkY] = CreateSubChunkInstanceFromBinding(
                binding.SolidSubChunks[subChunkY],
                subChunkY,
                _worldMaterial,
                createCollider: true,
                castShadows: true,
                receiveShadows: true);

            column.FluidSubChunks[subChunkY] = CreateSubChunkInstanceFromBinding(
                binding.FluidSubChunks[subChunkY],
                subChunkY,
                _fluidMaterial,
                createCollider: false,
                castShadows: false,
                receiveShadows: false);

            column.FoliageSubChunks[subChunkY] = CreateSubChunkInstanceFromBinding(
                binding.FoliageSubChunks[subChunkY],
                subChunkY,
                _foliageMaterial,
                createCollider: false,
                castShadows: true,
                receiveShadows: true);
        }

        return true;
    }

    private GameObject CreateChunkColumnRootObject()
    {
        if (_chunkColumnPrefab != null)
        {
            GameObject instance = Object.Instantiate(_chunkColumnPrefab);
            instance.name = _chunkColumnPrefab.name;
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

    private static SubChunkInstance CreateSubChunkInstanceFromBinding(
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

    private static SubChunkInstance CreateSubChunkInstance(Transform parent, int subChunkY, Material material, bool createCollider, bool castShadows, bool receiveShadows)
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

    private static bool RefreshSolidSubChunk(ChunkColumnInstance column, TerrainData terrain, BlockDatabase blockDatabase, int chunkX, int subChunkY, int chunkZ)
    {
        if (!terrain.HasSolidBlocksInSubChunk(chunkX, subChunkY, chunkZ))
        {
            SetSubChunkVisible(column.SolidSubChunks[subChunkY], false);
            return false;
        }

        SubChunkInstance subChunk = column.SolidSubChunks[subChunkY];
        bool hasGeometry = VoxelMesher.RebuildSubChunkMesh(subChunk.Mesh, terrain, blockDatabase, chunkX, subChunkY, chunkZ);
        SetSubChunkVisible(subChunk, hasGeometry);
        return hasGeometry;
    }

    private static bool ApplyGpuSubChunkMesh(Mesh mesh, TerrainGpuMesher.SubChunkMeshData meshData, string meshName)
    {
        if (mesh == null)
        {
            return false;
        }

        mesh.Clear();
        if (meshData == null || !meshData.HasGeometry)
        {
            return false;
        }

        mesh.name = meshName;
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = meshData.vertices;
        mesh.triangles = meshData.indices;
        mesh.uv = meshData.uvs;
        mesh.normals = meshData.normals;
        mesh.colors32 = meshData.colors;
        mesh.RecalculateBounds();
        mesh.UploadMeshData(false);
        return true;
    }

    private static bool RefreshFluidSubChunk(ChunkColumnInstance column, TerrainData terrain, int chunkX, int subChunkY, int chunkZ)
    {
        if (!terrain.HasFluidInSubChunk(chunkX, subChunkY, chunkZ))
        {
            SetSubChunkVisible(column.FluidSubChunks[subChunkY], false);
            return false;
        }

        SubChunkInstance subChunk = column.FluidSubChunks[subChunkY];
        bool hasGeometry = VoxelFluidMesher.RebuildSubChunkMesh(subChunk.Mesh, terrain, chunkX, subChunkY, chunkZ);
        SetSubChunkVisible(subChunk, hasGeometry);
        return hasGeometry;
    }

    private static bool RefreshFoliageSubChunk(ChunkColumnInstance column, TerrainData terrain, BlockDatabase blockDatabase, int chunkX, int subChunkY, int chunkZ)
    {
        if (!terrain.HasFoliageInSubChunk(chunkX, subChunkY, chunkZ))
        {
            SetSubChunkVisible(column.FoliageSubChunks[subChunkY], false);
            return false;
        }

        SubChunkInstance subChunk = column.FoliageSubChunks[subChunkY];
        bool hasGeometry = VoxelFoliageMesher.RebuildSubChunkMesh(subChunk.Mesh, terrain, blockDatabase, chunkX, subChunkY, chunkZ);
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

    private static bool HasAnyVisibleGeometry(ChunkColumnInstance column)
    {
        if (column == null)
        {
            return false;
        }

        for (int i = 0; i < TerrainData.SubChunkCountY; i++)
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
}

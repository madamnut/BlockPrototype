using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class VoxelWorldRuntime : MonoBehaviour
{
    private const int WorldSizeInChunks = 8;

    [Header("Scene References")]
    [SerializeField] private Material worldMaterial;
    [SerializeField] private VoxelBlockDatabase blockDatabase;

    private void Awake()
    {
        if (!ValidateSceneReferences())
        {
            enabled = false;
            return;
        }

        ApplyPerformanceDefaults();
        SetupDebugOverlay();
        BuildWorld();
    }

    private void BuildWorld()
    {
        int seed = 24680;
        VoxelTerrainData terrain = new(WorldSizeInChunks, seed);

        Transform worldRoot = new GameObject("Voxel World").transform;
        worldRoot.SetParent(transform, false);

        int renderedSubChunkCount = 0;
        int renderedChunkCount = 0;

        for (int chunkZ = 0; chunkZ < terrain.WorldSizeInChunks; chunkZ++)
        {
            for (int chunkX = 0; chunkX < terrain.WorldSizeInChunks; chunkX++)
            {
                bool chunkHasGeometry = false;
                int usedSubChunkCount = terrain.GetUsedSubChunkCount(chunkX, chunkZ);

                for (int subChunkY = 0; subChunkY < usedSubChunkCount; subChunkY++)
                {
                    Mesh mesh = VoxelMesher.BuildSubChunkMesh(terrain, blockDatabase, chunkX, subChunkY, chunkZ);
                    if (mesh == null)
                    {
                        continue;
                    }

                    chunkHasGeometry = true;
                    renderedSubChunkCount++;

                    GameObject subChunkObject = new($"SubChunk_{chunkX}_{subChunkY}_{chunkZ}");
                    subChunkObject.transform.SetParent(worldRoot, false);
                    subChunkObject.transform.localPosition = new Vector3(
                        chunkX * VoxelTerrainData.ChunkSize,
                        subChunkY * VoxelTerrainData.SubChunkSize,
                        chunkZ * VoxelTerrainData.ChunkSize);

                    MeshFilter meshFilter = subChunkObject.AddComponent<MeshFilter>();
                    meshFilter.sharedMesh = mesh;

                    MeshRenderer meshRenderer = subChunkObject.AddComponent<MeshRenderer>();
                    meshRenderer.sharedMaterial = worldMaterial;
                    meshRenderer.shadowCastingMode = ShadowCastingMode.On;
                    meshRenderer.receiveShadows = true;
                    meshRenderer.lightProbeUsage = LightProbeUsage.Off;
                    meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    meshRenderer.allowOcclusionWhenDynamic = false;
                    meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

                    MeshCollider meshCollider = subChunkObject.AddComponent<MeshCollider>();
                    meshCollider.sharedMesh = mesh;
                    meshCollider.cookingOptions =
                        MeshColliderCookingOptions.CookForFasterSimulation |
                        MeshColliderCookingOptions.EnableMeshCleaning |
                        MeshColliderCookingOptions.WeldColocatedVertices |
                        MeshColliderCookingOptions.UseFastMidphase;
                }

                if (chunkHasGeometry)
                {
                    renderedChunkCount++;
                }
            }
        }

        Debug.Log($"Voxel world generated. Chunks: {renderedChunkCount}, SubChunks: {renderedSubChunkCount}");
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
            isValid &= ValidateRequiredBlockDefinition((ushort)VoxelBlockType.Grass);
            isValid &= ValidateRequiredBlockDefinition((ushort)VoxelBlockType.Dirt);
            isValid &= ValidateRequiredBlockDefinition((ushort)VoxelBlockType.Stone);
        }

        return isValid;
    }

    private void SetupDebugOverlay()
    {
        if (GetComponent<VoxelDebugOverlay>() == null)
        {
            gameObject.AddComponent<VoxelDebugOverlay>();
        }
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
}

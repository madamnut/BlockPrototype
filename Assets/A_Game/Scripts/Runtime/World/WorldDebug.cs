using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class WorldDebug
{
    private readonly Transform _ownerTransform;
    private readonly Material _fallbackWorldMaterial;
    private readonly Color _selectionColor;
    private readonly Color _chunkBoundaryColor;
    private readonly GameObject[] _chunkBoundaryPlanes = new GameObject[8];

    private GameObject _selectionOutline;
    private GameObject _playerCollisionOutline;
    private Material _selectionMaterial;
    private GameObject _chunkBoundaryRoot;
    private Material _chunkBoundaryMaterial;
    private Mesh _chunkBoundaryMesh;
    private bool _chunkBoundariesVisible;
    private bool _playerCollisionVisible;
    private bool _hasCenterChunk;
    private Vector2Int _currentCenterChunk;

    public WorldDebug(Transform ownerTransform, Material fallbackWorldMaterial, Color selectionColor, Color chunkBoundaryColor)
    {
        _ownerTransform = ownerTransform;
        _fallbackWorldMaterial = fallbackWorldMaterial;
        _selectionColor = selectionColor;
        _chunkBoundaryColor = chunkBoundaryColor;
    }

    public void Initialize()
    {
        CreateSelectionOutline();
        CreatePlayerCollisionOutline();
        CreateChunkBoundaryDebug();
    }

    public void Dispose()
    {
        if (_selectionOutline != null)
        {
            Mesh selectionMesh = _selectionOutline.GetComponent<MeshFilter>()?.sharedMesh;
            if (selectionMesh != null)
            {
                Object.Destroy(selectionMesh);
            }
        }

        if (_playerCollisionOutline != null)
        {
            Mesh collisionMesh = _playerCollisionOutline.GetComponent<MeshFilter>()?.sharedMesh;
            if (collisionMesh != null)
            {
                Object.Destroy(collisionMesh);
            }
        }

        if (_selectionMaterial != null)
        {
            Object.Destroy(_selectionMaterial);
        }

        if (_chunkBoundaryMesh != null)
        {
            Object.Destroy(_chunkBoundaryMesh);
        }

        if (_chunkBoundaryMaterial != null)
        {
            Object.Destroy(_chunkBoundaryMaterial);
        }
    }

    public void HandleDebugInput(FlyCamera playerController, bool hasCenterChunk, Vector2Int centerChunk)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f3Key.isPressed && keyboard.gKey.wasPressedThisFrame)
        {
            _chunkBoundariesVisible = !_chunkBoundariesVisible;
        }

        if (keyboard != null && keyboard.f3Key.isPressed && keyboard.bKey.wasPressedThisFrame)
        {
            _playerCollisionVisible = !_playerCollisionVisible;
        }

        UpdateChunkBoundaries(hasCenterChunk, centerChunk);
        UpdatePlayerCollision(playerController);
    }

    public void UpdateChunkBoundaries(bool hasCenterChunk, Vector2Int centerChunk)
    {
        _hasCenterChunk = hasCenterChunk;
        _currentCenterChunk = centerChunk;

        if (_chunkBoundaryRoot == null)
        {
            return;
        }

        bool shouldShow = _chunkBoundariesVisible && hasCenterChunk;
        _chunkBoundaryRoot.SetActive(shouldShow);
        if (!shouldShow)
        {
            return;
        }

        float chunkSize = TerrainData.ChunkSize;
        float totalSpan = chunkSize * 3f;
        float height = TerrainData.WorldHeight;
        int startChunkX = centerChunk.x - 1;
        int startChunkZ = centerChunk.y - 1;

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

    public void SetSelection(Transform worldRoot, bool visible, Vector3Int blockPosition)
    {
        if (_selectionOutline == null)
        {
            return;
        }

        if (!visible || worldRoot == null)
        {
            _selectionOutline.SetActive(false);
            return;
        }

        _selectionOutline.transform.SetParent(worldRoot, false);
        _selectionOutline.transform.localPosition = blockPosition;
        _selectionOutline.SetActive(true);
    }

    private void CreateSelectionOutline()
    {
        _selectionOutline = new GameObject("Selection Outline");
        _selectionOutline.transform.SetParent(_ownerTransform, false);

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

    private void CreatePlayerCollisionOutline()
    {
        _playerCollisionOutline = new GameObject("Player Collision Outline");
        _playerCollisionOutline.transform.SetParent(_ownerTransform, false);

        MeshFilter meshFilter = _playerCollisionOutline.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = CreateSelectionOutlineMesh();

        MeshRenderer meshRenderer = _playerCollisionOutline.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = _selectionMaterial != null ? _selectionMaterial : CreateSelectionMaterial();
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        meshRenderer.allowOcclusionWhenDynamic = false;
        meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

        _playerCollisionOutline.SetActive(false);
    }

    private void CreateChunkBoundaryDebug()
    {
        _chunkBoundaryRoot = new GameObject("Chunk Boundary Debug");
        _chunkBoundaryRoot.transform.SetParent(_ownerTransform, false);

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

        if (shader == null && _fallbackWorldMaterial != null)
        {
            shader = _fallbackWorldMaterial.shader;
        }

        Material material = new(shader)
        {
            color = _chunkBoundaryColor,
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

        if (shader == null && _fallbackWorldMaterial != null)
        {
            shader = _fallbackWorldMaterial.shader;
        }

        Material material = new(shader)
        {
            color = _selectionColor,
            name = "Voxel Selection Material",
        };

        return material;
    }

    private void UpdatePlayerCollision(FlyCamera playerController)
    {
        if (_playerCollisionOutline == null)
        {
            return;
        }

        if (!_playerCollisionVisible || playerController == null || !playerController.TryGetCollisionBounds(out Bounds bounds))
        {
            _playerCollisionOutline.SetActive(false);
            return;
        }

        _playerCollisionOutline.transform.position = bounds.min;
        _playerCollisionOutline.transform.rotation = Quaternion.identity;
        _playerCollisionOutline.transform.localScale = bounds.size;
        _playerCollisionOutline.SetActive(true);
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
}

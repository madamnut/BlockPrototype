using UnityEngine;

[DisallowMultipleComponent]
public sealed class Chunk : MonoBehaviour
{
    private const string SolidRootName = "Solid";
    private const string FluidRootName = "Fluid";
    private const string FoliageRootName = "Foliage";

    [SerializeField] private SubChunk[] solidSubChunks = new SubChunk[VoxelTerrainData.SubChunkCountY];
    [SerializeField] private SubChunk[] fluidSubChunks = new SubChunk[VoxelTerrainData.SubChunkCountY];
    [SerializeField] private SubChunk[] foliageSubChunks = new SubChunk[VoxelTerrainData.SubChunkCountY];

    public SubChunk[] SolidSubChunks => solidSubChunks;
    public SubChunk[] FluidSubChunks => fluidSubChunks;
    public SubChunk[] FoliageSubChunks => foliageSubChunks;

    private void Reset()
    {
        GenerateSubChunks();
    }

    private void OnValidate()
    {
        EnsureArraySizes();
    }

    [ContextMenu("Generate Sub Chunks")]
    public void GenerateSubChunks()
    {
        EnsureArraySizes();

        Transform solidRoot = GetOrCreateContainerRoot(SolidRootName);
        Transform fluidRoot = GetOrCreateContainerRoot(FluidRootName);
        Transform foliageRoot = GetOrCreateContainerRoot(FoliageRootName);

        ClearContainerChildren(solidRoot);
        ClearContainerChildren(fluidRoot);
        ClearContainerChildren(foliageRoot);

        for (int subChunkY = 0; subChunkY < VoxelTerrainData.SubChunkCountY; subChunkY++)
        {
            solidSubChunks[subChunkY] = CreateSubChunkBinding(solidRoot, "Solid", subChunkY, createCollider: true);
            fluidSubChunks[subChunkY] = CreateSubChunkBinding(fluidRoot, "Fluid", subChunkY, createCollider: false);
            foliageSubChunks[subChunkY] = CreateSubChunkBinding(foliageRoot, "Foliage", subChunkY, createCollider: false);
        }
    }

    [ContextMenu("Rebind Existing Sub Chunks")]
    public void RebindExistingSubChunks()
    {
        EnsureArraySizes();
        BindExistingSubChunks(SolidRootName, solidSubChunks);
        BindExistingSubChunks(FluidRootName, fluidSubChunks);
        BindExistingSubChunks(FoliageRootName, foliageSubChunks);
    }

    private void EnsureArraySizes()
    {
        if (solidSubChunks == null || solidSubChunks.Length != VoxelTerrainData.SubChunkCountY)
        {
            solidSubChunks = new SubChunk[VoxelTerrainData.SubChunkCountY];
        }

        if (fluidSubChunks == null || fluidSubChunks.Length != VoxelTerrainData.SubChunkCountY)
        {
            fluidSubChunks = new SubChunk[VoxelTerrainData.SubChunkCountY];
        }

        if (foliageSubChunks == null || foliageSubChunks.Length != VoxelTerrainData.SubChunkCountY)
        {
            foliageSubChunks = new SubChunk[VoxelTerrainData.SubChunkCountY];
        }
    }

    private Transform GetOrCreateContainerRoot(string rootName)
    {
        Transform root = transform.Find(rootName);
        if (root != null)
        {
            return root;
        }

        GameObject rootObject = new(rootName);
        root = rootObject.transform;
        root.SetParent(transform, false);
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one;
        return root;
    }

    private static void ClearContainerChildren(Transform container)
    {
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Transform child = container.GetChild(i);
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Object.DestroyImmediate(child.gameObject);
            }
            else
#endif
            {
                Object.Destroy(child.gameObject);
            }
        }
    }

    private static SubChunk CreateSubChunkBinding(Transform parent, string prefix, int subChunkY, bool createCollider)
    {
        GameObject child = new($"{prefix}_SubChunk_{subChunkY}");
        Transform childTransform = child.transform;
        childTransform.SetParent(parent, false);
        childTransform.localPosition = new Vector3(0f, subChunkY * VoxelTerrainData.SubChunkSize, 0f);
        childTransform.localRotation = Quaternion.identity;
        childTransform.localScale = Vector3.one;

        child.AddComponent<MeshFilter>();
        child.AddComponent<MeshRenderer>();
        if (createCollider)
        {
            child.AddComponent<MeshCollider>();
        }

        SubChunk binding = child.AddComponent<SubChunk>();
        return binding;
    }

    private void BindExistingSubChunks(string rootName, SubChunk[] target)
    {
        Transform root = transform.Find(rootName);
        if (root == null)
        {
            return;
        }

        for (int i = 0; i < target.Length; i++)
        {
            target[i] = null;
        }

        foreach (Transform child in root)
        {
            SubChunk binding = child.GetComponent<SubChunk>();
            if (binding == null)
            {
                continue;
            }

            int subChunkIndex = Mathf.RoundToInt(child.localPosition.y / VoxelTerrainData.SubChunkSize);
            if (subChunkIndex < 0 || subChunkIndex >= target.Length)
            {
                continue;
            }

            target[subChunkIndex] = binding;
        }
    }
}

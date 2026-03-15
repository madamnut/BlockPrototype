using UnityEngine;

[DisallowMultipleComponent]
public sealed class Chunk : MonoBehaviour
{
    private const string SubChunksRootName = "SubChunks";
    private const string LegacySolidRootName = "Solid";
    private const string LegacyFluidRootName = "Fluid";
    private const string LegacyFoliageRootName = "Foliage";

    [SerializeField] private SubChunk[] subChunks = new SubChunk[TerrainData.SubChunkCountY];

    public SubChunk[] SubChunks => subChunks;

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

        Transform subChunksRoot = GetOrCreateContainerRoot(SubChunksRootName);
        DestroyLegacyContainerRoot(LegacySolidRootName);
        DestroyLegacyContainerRoot(LegacyFluidRootName);
        DestroyLegacyContainerRoot(LegacyFoliageRootName);
        ClearContainerChildren(subChunksRoot);

        for (int subChunkY = 0; subChunkY < TerrainData.SubChunkCountY; subChunkY++)
        {
            subChunks[subChunkY] = CreateSubChunkBinding(subChunksRoot, subChunkY);
        }
    }

    [ContextMenu("Rebind Existing Sub Chunks")]
    public void RebindExistingSubChunks()
    {
        EnsureArraySizes();
        BindExistingSubChunks(SubChunksRootName, subChunks);
    }

    private void EnsureArraySizes()
    {
        if (subChunks == null || subChunks.Length != TerrainData.SubChunkCountY)
        {
            subChunks = new SubChunk[TerrainData.SubChunkCountY];
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

    private void DestroyLegacyContainerRoot(string rootName)
    {
        Transform root = transform.Find(rootName);
        if (root == null)
        {
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Object.DestroyImmediate(root.gameObject);
            return;
        }
#endif
        Object.Destroy(root.gameObject);
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

    private static SubChunk CreateSubChunkBinding(Transform parent, int subChunkY)
    {
        GameObject child = new($"SubChunk_{subChunkY}");
        Transform childTransform = child.transform;
        childTransform.SetParent(parent, false);
        childTransform.localPosition = new Vector3(0f, subChunkY * TerrainData.SubChunkSize, 0f);
        childTransform.localRotation = Quaternion.identity;
        childTransform.localScale = Vector3.one;

        child.AddComponent<MeshFilter>();
        child.AddComponent<MeshRenderer>();

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

            int subChunkIndex = Mathf.RoundToInt(child.localPosition.y / TerrainData.SubChunkSize);
            if (subChunkIndex < 0 || subChunkIndex >= target.Length)
            {
                continue;
            }

            target[subChunkIndex] = binding;
        }
    }
}

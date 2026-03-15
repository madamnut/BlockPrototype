using UnityEngine;

[DisallowMultipleComponent]
public sealed class SubChunk : MonoBehaviour
{
    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private MeshRenderer meshRenderer;

    public MeshFilter MeshFilter => meshFilter;
    public MeshRenderer MeshRenderer => meshRenderer;

    private void Reset()
    {
        CacheComponents();
    }

    private void OnValidate()
    {
        CacheComponents();
    }

    private void CacheComponents()
    {
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
        }

        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }
    }
}

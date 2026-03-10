using UnityEngine;

[CreateAssetMenu(fileName = "VoxelBlockDatabase", menuName = "Voxel/Block Database")]
public sealed class VoxelBlockDatabase : ScriptableObject
{
    [SerializeField] private VoxelBlockDefinition[] definitions = new VoxelBlockDefinition[0];

    private VoxelBlockDefinition[] _definitionsById = new VoxelBlockDefinition[0];
    private bool[] _hasDefinition = new bool[0];
    private ushort _highestTextureLayer;

    public ushort HighestTextureLayer => _highestTextureLayer;
    public ushort MaxBlockId => _definitionsById.Length > 0 ? (ushort)(_definitionsById.Length - 1) : (ushort)0;

    private void OnEnable()
    {
        RebuildLookup();
    }

    private void OnValidate()
    {
        RebuildLookup();
    }

    public bool HasDefinition(ushort blockId)
    {
        return blockId < _hasDefinition.Length && _hasDefinition[blockId];
    }

    public ushort GetFaceTextureLayer(ushort blockId, VoxelBlockFace face)
    {
        if (!HasDefinition(blockId))
        {
            Debug.LogError($"VoxelBlockDatabase is missing a block definition for block id {blockId}.", this);
            return 0;
        }

        return _definitionsById[blockId].GetLayer(face);
    }

    private void RebuildLookup()
    {
        ushort maxBlockId = 0;
        _highestTextureLayer = 0;

        for (int i = 0; i < definitions.Length; i++)
        {
            if (definitions[i].BlockId > maxBlockId)
            {
                maxBlockId = definitions[i].BlockId;
            }

            if (definitions[i].HighestLayer > _highestTextureLayer)
            {
                _highestTextureLayer = definitions[i].HighestLayer;
            }
        }

        _definitionsById = new VoxelBlockDefinition[maxBlockId + 1];
        _hasDefinition = new bool[maxBlockId + 1];

        for (int i = 0; i < definitions.Length; i++)
        {
            ushort blockId = definitions[i].BlockId;
            _definitionsById[blockId] = definitions[i];
            _hasDefinition[blockId] = true;
        }
    }

#if UNITY_EDITOR
    public void SetDefinitionsForBake(VoxelBlockDefinition[] bakedDefinitions)
    {
        definitions = bakedDefinitions;
        RebuildLookup();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}

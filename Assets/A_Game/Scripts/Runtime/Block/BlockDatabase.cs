using UnityEngine;

[CreateAssetMenu(fileName = "BlockDatabase", menuName = "World/Block/Texture DB")]
public sealed class BlockDatabase : ScriptableObject
{
    [SerializeField] private BlockDefinition[] definitions = new BlockDefinition[0];

    private BlockDefinition[] _definitionsById = new BlockDefinition[0];
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

    public ushort GetFaceTextureLayer(ushort blockId, BlockFace face)
    {
        if (!HasDefinition(blockId))
        {
            Debug.LogError($"BlockDatabase is missing a block definition for block id {blockId}.", this);
            return 0;
        }

        return _definitionsById[blockId].GetLayer(face);
    }

    public ContentKind GetKind(ushort blockId)
    {
        if (!HasDefinition(blockId))
        {
            Debug.LogError($"BlockDatabase is missing a block definition for block id {blockId}.", this);
            return ContentKind.Block;
        }

        return _definitionsById[blockId].Kind;
    }

    public bool IsFoliage(ushort blockId)
    {
        return HasDefinition(blockId) && _definitionsById[blockId].IsFoliage;
    }

    public string GetDisplayName(ushort blockId)
    {
        if (!HasDefinition(blockId))
        {
            return $"Unknown ({blockId})";
        }

        string displayName = _definitionsById[blockId].DisplayName;
        return string.IsNullOrWhiteSpace(displayName) ? $"ID {blockId}" : displayName;
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

        _definitionsById = new BlockDefinition[maxBlockId + 1];
        _hasDefinition = new bool[maxBlockId + 1];

        for (int i = 0; i < definitions.Length; i++)
        {
            ushort blockId = definitions[i].BlockId;
            _definitionsById[blockId] = definitions[i];
            _hasDefinition[blockId] = true;
        }
    }

#if UNITY_EDITOR
    public void SetDefinitionsForBake(BlockDefinition[] bakedDefinitions)
    {
        definitions = bakedDefinitions;
        RebuildLookup();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}

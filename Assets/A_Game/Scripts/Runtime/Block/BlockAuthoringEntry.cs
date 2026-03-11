using System;
using UnityEngine;

[Serializable]
public struct BlockAuthoringEntry
{
    [SerializeField] private ushort blockId;
    [SerializeField] private string blockName;
    [SerializeField] private ContentKind kind;

    public ushort BlockId => blockId;
    public string BlockName => blockName;
    public ContentKind Kind => kind;
}

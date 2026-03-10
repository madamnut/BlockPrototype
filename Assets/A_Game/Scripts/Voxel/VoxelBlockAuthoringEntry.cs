using System;
using UnityEngine;

[Serializable]
public struct VoxelBlockAuthoringEntry
{
    [SerializeField] private ushort blockId;
    [SerializeField] private string blockName;

    public ushort BlockId => blockId;
    public string BlockName => blockName;
}

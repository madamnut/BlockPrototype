using System;
using UnityEngine;

[Serializable]
public struct VoxelBlockDefinition
{
    [SerializeField] private ushort blockId;
    [SerializeField] private ushort leftLayer;
    [SerializeField] private ushort rightLayer;
    [SerializeField] private ushort bottomLayer;
    [SerializeField] private ushort topLayer;
    [SerializeField] private ushort backLayer;
    [SerializeField] private ushort frontLayer;

    public VoxelBlockDefinition(
        ushort blockId,
        ushort leftLayer,
        ushort rightLayer,
        ushort bottomLayer,
        ushort topLayer,
        ushort backLayer,
        ushort frontLayer)
    {
        this.blockId = blockId;
        this.leftLayer = leftLayer;
        this.rightLayer = rightLayer;
        this.bottomLayer = bottomLayer;
        this.topLayer = topLayer;
        this.backLayer = backLayer;
        this.frontLayer = frontLayer;
    }

    public ushort BlockId => blockId;

    public readonly ushort GetLayer(VoxelBlockFace face)
    {
        return face switch
        {
            VoxelBlockFace.Left => leftLayer,
            VoxelBlockFace.Right => rightLayer,
            VoxelBlockFace.Bottom => bottomLayer,
            VoxelBlockFace.Top => topLayer,
            VoxelBlockFace.Back => backLayer,
            VoxelBlockFace.Front => frontLayer,
            _ => 0,
        };
    }

    public readonly ushort HighestLayer
    {
        get
        {
            ushort highest = leftLayer;
            if (rightLayer > highest) highest = rightLayer;
            if (bottomLayer > highest) highest = bottomLayer;
            if (topLayer > highest) highest = topLayer;
            if (backLayer > highest) highest = backLayer;
            if (frontLayer > highest) highest = frontLayer;
            return highest;
        }
    }
}

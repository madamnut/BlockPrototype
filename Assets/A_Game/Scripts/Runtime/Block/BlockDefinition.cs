using System;
using UnityEngine;

[Serializable]
public struct BlockDefinition
{
    [SerializeField] private ushort blockId;
    [SerializeField] private string displayName;
    [SerializeField] private ContentKind kind;
    [SerializeField] private ushort leftLayer;
    [SerializeField] private ushort rightLayer;
    [SerializeField] private ushort bottomLayer;
    [SerializeField] private ushort topLayer;
    [SerializeField] private ushort backLayer;
    [SerializeField] private ushort frontLayer;

    public BlockDefinition(
        ushort blockId,
        string displayName,
        ContentKind kind,
        ushort leftLayer,
        ushort rightLayer,
        ushort bottomLayer,
        ushort topLayer,
        ushort backLayer,
        ushort frontLayer)
    {
        this.blockId = blockId;
        this.displayName = displayName;
        this.kind = kind;
        this.leftLayer = leftLayer;
        this.rightLayer = rightLayer;
        this.bottomLayer = bottomLayer;
        this.topLayer = topLayer;
        this.backLayer = backLayer;
        this.frontLayer = frontLayer;
    }

    public ushort BlockId => blockId;
    public string DisplayName => displayName;
    public ContentKind Kind => kind;
    public bool IsFoliage => kind == ContentKind.Foliage;

    public readonly ushort GetLayer(BlockFace face)
    {
        return face switch
        {
            BlockFace.Left => leftLayer,
            BlockFace.Right => rightLayer,
            BlockFace.Bottom => bottomLayer,
            BlockFace.Top => topLayer,
            BlockFace.Back => backLayer,
            BlockFace.Front => frontLayer,
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

using System;

[Serializable]
public enum SplineTreeCoordinateSource : byte
{
    Continentalness = 0,
    Erosion = 1,
    PeaksValleys = 2,
    Weirdness = 3,
}

[Serializable]
public enum SplineTreePointValueType : byte
{
    Constant = 0,
    NestedSpline = 1,
}

[Serializable]
public struct SplineTreeBakedNode
{
    public SplineTreeCoordinateSource coordinate;
    public int firstPointIndex;
    public int pointCount;
}

[Serializable]
public struct SplineTreeBakedPoint
{
    public float location;
    public float derivative;
    public SplineTreePointValueType valueType;
    public float constantValue;
    public int childNodeIndex;
}

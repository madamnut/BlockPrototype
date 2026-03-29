using System;
using UnityEngine;

public enum NestedSplineAxis
{
    Gnd = 0,
    Relief = 1,
    VeinFold = 2,
}

[Serializable]
public sealed class NestedSplineLayer3
{
    public NestedSplineAxis axis = NestedSplineAxis.VeinFold;
    public SplineGraphAsset rootSpline;
}

[Serializable]
public struct NestedSplineLayer2Segment
{
    [Range(-1f, 1f)] public float endInput;
    public bool useChildLayer;
    public NestedSplineLayer3 childLayer;

    public float End => Mathf.Clamp(endInput, -1f, 1f);
}

[Serializable]
public sealed class NestedSplineLayer2
{
    public NestedSplineAxis axis = NestedSplineAxis.Relief;
    public SplineGraphAsset rootSpline;
    public NestedSplineLayer2Segment[] segments = Array.Empty<NestedSplineLayer2Segment>();

    public NestedSplineLayer2Segment[] GetSegmentsOrEmpty()
    {
        return segments ?? Array.Empty<NestedSplineLayer2Segment>();
    }

    public void SortSegments()
    {
        if (segments == null)
        {
            segments = Array.Empty<NestedSplineLayer2Segment>();
            return;
        }

        Array.Sort(segments, static (left, right) => left.End.CompareTo(right.End));
        for (int i = 0; i < segments.Length; i++)
        {
            segments[i].childLayer ??= new NestedSplineLayer3();
        }
    }
}

[Serializable]
public struct NestedSplineLayer1Segment
{
    [Range(-1f, 1f)] public float endInput;
    public bool useChildLayer;
    public NestedSplineLayer2 childLayer;

    public float End => Mathf.Clamp(endInput, -1f, 1f);
}

[Serializable]
public sealed class NestedSplineLayer1
{
    public NestedSplineAxis axis = NestedSplineAxis.Gnd;
    public SplineGraphAsset rootSpline;
    public NestedSplineLayer1Segment[] segments = Array.Empty<NestedSplineLayer1Segment>();

    public NestedSplineLayer1Segment[] GetSegmentsOrEmpty()
    {
        return segments ?? Array.Empty<NestedSplineLayer1Segment>();
    }

    public void SortSegments()
    {
        if (segments == null)
        {
            segments = Array.Empty<NestedSplineLayer1Segment>();
            return;
        }

        Array.Sort(segments, static (left, right) => left.End.CompareTo(right.End));
        for (int i = 0; i < segments.Length; i++)
        {
            segments[i].childLayer ??= new NestedSplineLayer2();
            segments[i].childLayer.SortSegments();
        }
    }
}

[CreateAssetMenu(fileName = "NestedSplineGraph", menuName = "World/Gen/Nested Spline Graph")]
public sealed class NestedSplineGraphAsset : ScriptableObject
{
    public NestedSplineLayer1 rootLayer = new();

    private void OnValidate()
    {
        rootLayer ??= new NestedSplineLayer1();
        rootLayer.SortSegments();
    }
}

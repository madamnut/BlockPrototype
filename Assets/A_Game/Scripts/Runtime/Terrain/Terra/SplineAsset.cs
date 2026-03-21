using System;
using UnityEngine;

public enum SplineCoordinateKind : byte
{
    Continentalness = 0,
    Erosion = 1,
    PeaksAndValleys = 2,
    Ridges = 3,
}

[Serializable]
public sealed class SplineValueReference
{
    [SerializeField] private bool useChildSpline;
    [SerializeField] private float constantValue;
    [SerializeField] private SplineAsset childSpline;

    public bool UseChildSpline
    {
        get => useChildSpline;
        set => useChildSpline = value;
    }

    public float ConstantValue
    {
        get => constantValue;
        set => constantValue = value;
    }

    public SplineAsset ChildSpline
    {
        get => childSpline;
        set => childSpline = value;
    }

    public float Evaluate(in SplineContext context)
    {
        if (!useChildSpline)
        {
            return constantValue;
        }

        if (childSpline == null)
        {
            throw new InvalidOperationException("Spline point is configured to use a child spline, but no child spline is assigned.");
        }

        return childSpline.Evaluate(context);
    }
}

[Serializable]
public sealed class SplinePointData
{
    [SerializeField] private float location;
    [SerializeField] private SplineValueReference value = new();
    [SerializeField] private float derivative;

    public float Location
    {
        get => location;
        set => location = value;
    }

    public SplineValueReference Value => value;

    public float Derivative
    {
        get => derivative;
        set => derivative = value;
    }
}

public readonly struct SplineContext
{
    public SplineContext(float continentalness, float peaksAndValleys)
        : this(continentalness, 0f, peaksAndValleys, 0f)
    {
    }

    public SplineContext(float continentalness, float erosion, float peaksAndValleys, float ridges)
    {
        Continentalness = continentalness;
        Erosion = erosion;
        PeaksAndValleys = peaksAndValleys;
        Ridges = ridges;
    }

    public float Continentalness { get; }
    public float Erosion { get; }
    public float PeaksAndValleys { get; }
    public float Ridges { get; }

    public float GetCoordinate(SplineCoordinateKind kind)
    {
        return kind switch
        {
            SplineCoordinateKind.Continentalness => Continentalness,
            SplineCoordinateKind.Erosion => Erosion,
            SplineCoordinateKind.PeaksAndValleys => PeaksAndValleys,
            SplineCoordinateKind.Ridges => Ridges,
            _ => 0f,
        };
    }
}

[CreateAssetMenu(fileName = "Spline", menuName = "World/Terra/DEEPSLATE Spline")]
public sealed class SplineAsset : ScriptableObject
{
    [SerializeField] private SplineCoordinateKind coordinateKind;
    [SerializeField] private SplinePointData[] points = Array.Empty<SplinePointData>();
    [SerializeField] private TextAsset runtimeJson;

    public SplineCoordinateKind CoordinateKind
    {
        get => coordinateKind;
        set => coordinateKind = value;
    }

    public SplinePointData[] Points => points;
    public TextAsset RuntimeJson => runtimeJson;

    public void ReplaceData(SplineCoordinateKind nextCoordinateKind, SplinePointData[] nextPoints)
    {
        coordinateKind = nextCoordinateKind;
        points = nextPoints ?? Array.Empty<SplinePointData>();
        NormalizePoints();
    }

    private void OnValidate()
    {
        NormalizePoints();
    }

    public float Evaluate(in SplineContext context)
    {
        if (points == null || points.Length == 0)
        {
            throw new InvalidOperationException($"{name} requires at least 1 spline point.");
        }

        NormalizePoints();

        float coordinate = context.GetCoordinate(coordinateKind);
        int pointIndex = GetPointIndex(coordinate);
        int lastIndex = points.Length - 1;

        if (pointIndex < 0)
        {
            SplinePointData point = points[0];
            return point.Value.Evaluate(context) + (point.Derivative * (coordinate - point.Location));
        }

        if (pointIndex >= lastIndex)
        {
            SplinePointData point = points[lastIndex];
            return point.Value.Evaluate(context) + (point.Derivative * (coordinate - point.Location));
        }

        SplinePointData left = points[pointIndex];
        SplinePointData right = points[pointIndex + 1];
        float locationDelta = right.Location - left.Location;
        float t = (coordinate - left.Location) / locationDelta;

        float leftValue = left.Value.Evaluate(context);
        float rightValue = right.Value.Evaluate(context);
        float leftDerivativeDelta = (left.Derivative * locationDelta) - (rightValue - leftValue);
        float rightDerivativeDelta = (-right.Derivative * locationDelta) + (rightValue - leftValue);

        return Mathf.Lerp(leftValue, rightValue, t) + ((t * (1f - t)) * Mathf.Lerp(leftDerivativeDelta, rightDerivativeDelta, t));
    }

    public SplinePointData AddPoint()
    {
        SplinePointData newPoint = new();
        newPoint.Location = 0f;
        newPoint.Derivative = 0f;

        SplinePointData[] next = new SplinePointData[(points?.Length ?? 0) + 1];
        if (points != null && points.Length > 0)
        {
            Array.Copy(points, next, points.Length);
        }

        next[next.Length - 1] = newPoint;
        points = next;
        NormalizePoints();
        return newPoint;
    }

    public void RemovePoint(SplinePointData point)
    {
        if (points == null || points.Length == 0 || point == null)
        {
            return;
        }

        int index = Array.IndexOf(points, point);
        if (index < 0)
        {
            return;
        }

        if (points.Length == 1)
        {
            points = Array.Empty<SplinePointData>();
            return;
        }

        SplinePointData[] next = new SplinePointData[points.Length - 1];
        if (index > 0)
        {
            Array.Copy(points, 0, next, 0, index);
        }

        if (index < points.Length - 1)
        {
            Array.Copy(points, index + 1, next, index, points.Length - index - 1);
        }

        points = next;
        NormalizePoints();
    }

    public void NormalizeForEditor()
    {
        NormalizePoints();
    }

    private int GetPointIndex(float coordinate)
    {
        int low = 0;
        int high = points.Length;
        while (low < high)
        {
            int mid = (low + high) >> 1;
            if (coordinate < points[mid].Location)
            {
                high = mid;
            }
            else
            {
                low = mid + 1;
            }
        }

        return low - 1;
    }

    private void NormalizePoints()
    {
        if (points == null || points.Length == 0)
        {
            return;
        }

        Array.Sort(points, static (a, b) => a.Location.CompareTo(b.Location));
        const float minSpacing = 0.0001f;

        for (int i = 1; i < points.Length; i++)
        {
            float adjusted = points[i].Location;
            if (adjusted <= points[i - 1].Location)
            {
                adjusted = points[i - 1].Location + minSpacing;
            }

            points[i].Location = adjusted;
        }
    }
}

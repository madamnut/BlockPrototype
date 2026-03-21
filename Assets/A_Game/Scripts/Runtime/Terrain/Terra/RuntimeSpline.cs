using System;

public sealed class RuntimeSplineValue
{
    private readonly float _constantValue;
    private readonly RuntimeSpline _childSpline;

    public RuntimeSplineValue(float constantValue)
    {
        _constantValue = constantValue;
        _childSpline = null;
    }

    public RuntimeSplineValue(RuntimeSpline childSpline)
    {
        _childSpline = childSpline ?? throw new ArgumentNullException(nameof(childSpline));
    }

    public bool UsesChildSpline => _childSpline != null;
    public float ConstantValue => _constantValue;
    public RuntimeSpline ChildSpline => _childSpline;

    public float Evaluate(in SplineContext context)
    {
        return _childSpline != null ? _childSpline.Evaluate(context) : _constantValue;
    }
}

public readonly struct RuntimeSplinePoint
{
    public RuntimeSplinePoint(float location, RuntimeSplineValue value, float derivative)
    {
        Location = location;
        Value = value ?? throw new ArgumentNullException(nameof(value));
        Derivative = derivative;
    }

    public float Location { get; }
    public RuntimeSplineValue Value { get; }
    public float Derivative { get; }
}

public sealed class RuntimeSpline
{
    private readonly SplineCoordinateKind _coordinateKind;
    private readonly RuntimeSplinePoint[] _points;

    public RuntimeSpline(SplineCoordinateKind coordinateKind, RuntimeSplinePoint[] points)
    {
        if (points == null || points.Length == 0)
        {
            throw new ArgumentException("Runtime spline requires at least one point.", nameof(points));
        }

        _coordinateKind = coordinateKind;
        _points = new RuntimeSplinePoint[points.Length];
        Array.Copy(points, _points, points.Length);
        Array.Sort(_points, static (a, b) => a.Location.CompareTo(b.Location));
        NormalizePointSpacing();
    }

    public SplineCoordinateKind CoordinateKind => _coordinateKind;
    public RuntimeSplinePoint[] Points => _points;

    public float Evaluate(in SplineContext context)
    {
        float coordinate = context.GetCoordinate(_coordinateKind);
        int pointIndex = GetPointIndex(coordinate);
        int lastIndex = _points.Length - 1;

        if (pointIndex < 0)
        {
            RuntimeSplinePoint point = _points[0];
            return point.Value.Evaluate(context) + (point.Derivative * (coordinate - point.Location));
        }

        if (pointIndex >= lastIndex)
        {
            RuntimeSplinePoint point = _points[lastIndex];
            return point.Value.Evaluate(context) + (point.Derivative * (coordinate - point.Location));
        }

        RuntimeSplinePoint left = _points[pointIndex];
        RuntimeSplinePoint right = _points[pointIndex + 1];
        float locationDelta = right.Location - left.Location;
        float t = (coordinate - left.Location) / locationDelta;

        float leftValue = left.Value.Evaluate(context);
        float rightValue = right.Value.Evaluate(context);
        float leftDerivativeDelta = (left.Derivative * locationDelta) - (rightValue - leftValue);
        float rightDerivativeDelta = (-right.Derivative * locationDelta) + (rightValue - leftValue);

        return Lerp(leftValue, rightValue, t) + ((t * (1f - t)) * Lerp(leftDerivativeDelta, rightDerivativeDelta, t));
    }

    private int GetPointIndex(float coordinate)
    {
        int low = 0;
        int high = _points.Length;
        while (low < high)
        {
            int mid = (low + high) >> 1;
            if (coordinate < _points[mid].Location)
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

    private void NormalizePointSpacing()
    {
        const float minSpacing = 0.0001f;
        for (int i = 1; i < _points.Length; i++)
        {
            if (_points[i].Location <= _points[i - 1].Location)
            {
                float adjusted = _points[i - 1].Location + minSpacing;
                _points[i] = new RuntimeSplinePoint(adjusted, _points[i].Value, _points[i].Derivative);
            }
        }
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + ((b - a) * t);
    }
}

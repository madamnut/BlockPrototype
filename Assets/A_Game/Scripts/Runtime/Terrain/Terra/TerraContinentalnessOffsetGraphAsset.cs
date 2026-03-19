using System;
using UnityEngine;

[Serializable]
public struct TerraContinentalnessControlPoint
{
    [Range(-1f, 1f)] public float x;
    public float y;

    public TerraContinentalnessControlPoint(float x, float y)
    {
        this.x = x;
        this.y = y;
    }
}

[CreateAssetMenu(fileName = "TerraContinentalnessOffsetGraph", menuName = "World/Terra/Continentalness Offset Graph")]
public sealed class TerraContinentalnessOffsetGraphAsset : ScriptableObject
{
    [SerializeField] private TerraContinentalnessControlPoint[] points = Array.Empty<TerraContinentalnessControlPoint>();

    [NonSerialized] private AnimationCurve _runtimeCurve;
    [NonSerialized] private bool _curveDirty = true;

    public TerraContinentalnessControlPoint[] Points => points;

    private void OnValidate()
    {
        NormalizePoints();
        MarkCurveDirty();
    }

    public float EvaluateOffset(float continentalness)
    {
        ThrowIfInvalid();
        NormalizePoints();
        return GetCurve().Evaluate(Mathf.Clamp(continentalness, -1f, 1f));
    }

    public AnimationCurve GetPreviewCurve()
    {
        ThrowIfInvalid();
        NormalizePoints();
        return GetCurve();
    }

    public void MarkCurveDirty()
    {
        _curveDirty = true;
    }

    private AnimationCurve GetCurve()
    {
        if (_curveDirty || _runtimeCurve == null)
        {
            _runtimeCurve = BuildCurve();
            _curveDirty = false;
        }

        return _runtimeCurve;
    }

    private AnimationCurve BuildCurve()
    {
        Keyframe[] keys = new Keyframe[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            keys[i] = new Keyframe(points[i].x, points[i].y);
        }

        return new AnimationCurve(keys);
    }

    private void ThrowIfInvalid()
    {
        if (points == null || points.Length < 2)
        {
            throw new InvalidOperationException($"{name} requires at least 2 continentalness control points.");
        }
    }

    private void NormalizePoints()
    {
        if (points == null || points.Length == 0)
        {
            return;
        }

        Array.Sort(points, static (a, b) => a.x.CompareTo(b.x));
        const float minSpacing = 0.0001f;

        TerraContinentalnessControlPoint first = points[0];
        first.x = Mathf.Clamp(first.x, -1f, 1f);
        points[0] = first;

        for (int i = 1; i < points.Length; i++)
        {
            TerraContinentalnessControlPoint point = points[i];
            point.x = Mathf.Clamp(point.x, -1f, 1f);
            if (point.x <= points[i - 1].x)
            {
                point.x = Mathf.Min(1f, points[i - 1].x + minSpacing);
            }

            points[i] = point;
        }
    }
}

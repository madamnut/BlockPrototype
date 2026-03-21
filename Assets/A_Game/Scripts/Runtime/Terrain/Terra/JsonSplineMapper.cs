using System;
using UnityEngine;

public sealed class JsonSplineMapper
{
    private readonly RuntimeSpline _spline;

    public JsonSplineMapper(TextAsset splineJson)
    {
        _spline = SplineJsonLoader.Load(splineJson ?? throw new ArgumentNullException(nameof(splineJson)));
    }

    public float Evaluate(in SplineContext context)
    {
        return _spline.Evaluate(context);
    }
}

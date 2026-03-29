using UnityEngine;

public readonly struct LiftRuntimeSettings
{
    public const int DefaultNestedLutResolution = 64;

    public readonly float[] nestedBaseHeightLut;
    public readonly int nestedBaseHeightResolution;

    public LiftRuntimeSettings(
        float[] nestedBaseHeightLut,
        int nestedBaseHeightResolution)
    {
        this.nestedBaseHeightLut = SanitizeNestedLut(nestedBaseHeightLut, nestedBaseHeightResolution, out this.nestedBaseHeightResolution);
    }

    public static LiftRuntimeSettings CreateDefault()
    {
        return new LiftRuntimeSettings(null, 0);
    }

    public static GndSplinePoint[] CreateDefaultLowSpline()
    {
        return new[]
        {
            new GndSplinePoint(-1f, 40f),
            new GndSplinePoint(-0.65f, 50f),
            new GndSplinePoint(-0.25f, 66f),
            new GndSplinePoint(0.1f, 86f),
            new GndSplinePoint(0.4f, 112f),
            new GndSplinePoint(0.7f, 148f),
            new GndSplinePoint(1f, 196f),
        };
    }
    private static float[] SanitizeNestedLut(float[] source, int resolution, out int sanitizedResolution)
    {
        if (source != null && resolution >= 2 && source.Length == resolution * resolution * resolution)
        {
            sanitizedResolution = resolution;
            return (float[])source.Clone();
        }

        sanitizedResolution = 0;
        return null;
    }
}

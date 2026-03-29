using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public static class NestedSplineGraphUtility
{
    public static float Evaluate(NestedSplineGraphAsset graph, float gnd, float relief)
    {
        return Evaluate(graph, gnd, relief, 0f);
    }

    public static float Evaluate(NestedSplineGraphAsset graph, float gnd, float relief, float veinFold)
    {
        if (graph == null || graph.rootLayer == null)
        {
            return 0f;
        }

        return EvaluateLayer1(graph.rootLayer, gnd, relief, veinFold);
    }

    public static float[] BakeLut2D(NestedSplineGraphAsset graph, int resolution)
    {
        int sanitizedResolution = Mathf.Max(2, resolution);
        float[] lut = new float[sanitizedResolution * sanitizedResolution];
        for (int reliefIndex = 0; reliefIndex < sanitizedResolution; reliefIndex++)
        {
            float relief = Mathf.Lerp(-1f, 1f, reliefIndex / (float)(sanitizedResolution - 1));
            for (int gndIndex = 0; gndIndex < sanitizedResolution; gndIndex++)
            {
                float gnd = Mathf.Lerp(-1f, 1f, gndIndex / (float)(sanitizedResolution - 1));
                lut[GetIndex(gndIndex, reliefIndex, sanitizedResolution)] = Evaluate(graph, gnd, relief);
            }
        }

        return lut;
    }

    public static float[] BakeLut3D(NestedSplineGraphAsset graph, int resolution)
    {
        int sanitizedResolution = Mathf.Max(2, resolution);
        float[] lut = new float[sanitizedResolution * sanitizedResolution * sanitizedResolution];
        for (int veinIndex = 0; veinIndex < sanitizedResolution; veinIndex++)
        {
            float veinFold = Mathf.Lerp(-1f, 1f, veinIndex / (float)(sanitizedResolution - 1));
            for (int reliefIndex = 0; reliefIndex < sanitizedResolution; reliefIndex++)
            {
                float relief = Mathf.Lerp(-1f, 1f, reliefIndex / (float)(sanitizedResolution - 1));
                for (int gndIndex = 0; gndIndex < sanitizedResolution; gndIndex++)
                {
                    float gnd = Mathf.Lerp(-1f, 1f, gndIndex / (float)(sanitizedResolution - 1));
                    lut[GetIndex(gndIndex, reliefIndex, veinIndex, sanitizedResolution)] = Evaluate(graph, gnd, relief, veinFold);
                }
            }
        }

        return lut;
    }

    public static float EvaluateLut2D(float[] lut, int resolution, float gnd, float relief)
    {
        if (lut == null || lut.Length != resolution * resolution || resolution < 2)
        {
            return 0f;
        }

        float normalizedGnd = Mathf.InverseLerp(-1f, 1f, gnd);
        float normalizedRelief = Mathf.InverseLerp(-1f, 1f, relief);
        return EvaluateLut2DInternal(lut, resolution, normalizedGnd, normalizedRelief);
    }

    public static float EvaluateLut2D(NativeArray<float> lut, int resolution, float gnd, float relief)
    {
        if (!lut.IsCreated || lut.Length != resolution * resolution || resolution < 2)
        {
            return 0f;
        }

        float normalizedGnd = math.saturate((gnd + 1f) * 0.5f);
        float normalizedRelief = math.saturate((relief + 1f) * 0.5f);
        return EvaluateLut2DInternal(lut, resolution, normalizedGnd, normalizedRelief);
    }

    public static float EvaluateLut3D(float[] lut, int resolution, float gnd, float relief, float veinFold)
    {
        if (lut == null || lut.Length != resolution * resolution * resolution || resolution < 2)
        {
            return 0f;
        }

        float normalizedGnd = Mathf.InverseLerp(-1f, 1f, gnd);
        float normalizedRelief = Mathf.InverseLerp(-1f, 1f, relief);
        float normalizedVeinFold = Mathf.InverseLerp(-1f, 1f, veinFold);
        return EvaluateLut3DInternal(lut, resolution, normalizedGnd, normalizedRelief, normalizedVeinFold);
    }

    public static float EvaluateLut3D(NativeArray<float> lut, int resolution, float gnd, float relief, float veinFold)
    {
        if (!lut.IsCreated || lut.Length != resolution * resolution * resolution || resolution < 2)
        {
            return 0f;
        }

        float normalizedGnd = math.saturate((gnd + 1f) * 0.5f);
        float normalizedRelief = math.saturate((relief + 1f) * 0.5f);
        float normalizedVeinFold = math.saturate((veinFold + 1f) * 0.5f);
        return EvaluateLut3DInternal(lut, resolution, normalizedGnd, normalizedRelief, normalizedVeinFold);
    }

    private static float EvaluateLayer1(NestedSplineLayer1 layer, float gnd, float relief, float veinFold)
    {
        float input = GetAxisValue(layer.axis, gnd, relief, veinFold);
        NestedSplineLayer1Segment[] segments = layer.GetSegmentsOrEmpty();
        if (TryGetContainingSegment(segments, input, out NestedSplineLayer1Segment segment, out float segmentStart, out float segmentEnd))
        {
            if (segment.useChildLayer && segment.childLayer != null)
            {
                float transformedInput = EvaluateLayer2(
                    segment.childLayer,
                    input,
                    segmentStart,
                    segmentEnd,
                    gnd,
                    relief,
                    veinFold);

                return EvaluateRootSpline(layer.rootSpline, transformedInput);
            }
        }

        return EvaluateRootSpline(layer.rootSpline, input);
    }

    private static float EvaluateLayer2(
        NestedSplineLayer2 layer,
        float parentInput,
        float parentSegmentStart,
        float parentSegmentEnd,
        float gnd,
        float relief,
        float veinFold)
    {
        float input = GetAxisValue(layer.axis, gnd, relief, veinFold);
        float scale = EvaluateRootSpline(layer.rootSpline, input);
        float transformedInput = ApplyChildScale(parentInput, scale, parentSegmentStart, parentSegmentEnd);

        NestedSplineLayer2Segment[] segments = layer.GetSegmentsOrEmpty();
        if (TryGetContainingSegment(segments, input, out NestedSplineLayer2Segment segment, out _, out _))
        {
            if (segment.useChildLayer && segment.childLayer != null)
            {
                return EvaluateLayer3(segment.childLayer, transformedInput, gnd, relief, veinFold);
            }
        }

        return transformedInput;
    }

    private static float EvaluateLayer3(NestedSplineLayer3 layer, float transformedInput, float gnd, float relief, float veinFold)
    {
        float input = GetAxisValue(layer.axis, gnd, relief, veinFold);
        float modifier = EvaluateRootSpline(layer.rootSpline, input);
        if (modifier < 0f)
        {
            return modifier;
        }

        return transformedInput * (1f + modifier);
    }

    private static float EvaluateRootSpline(SplineGraphAsset rootSpline, float input)
    {
        return rootSpline != null
            ? GndSplineUtility.Evaluate(rootSpline.GetPointsOrDefault(), input)
            : GndSplineUtility.Evaluate(LiftRuntimeSettings.CreateDefaultLowSpline(), input);
    }

    private static float ApplyChildScale(float input, float scale, float segmentStart, float segmentEnd)
    {
        float pivot = (segmentStart + segmentEnd) * 0.5f;
        float clampedScale = math.max(0f, scale);
        return pivot + ((input - pivot) * clampedScale);
    }

    private static bool TryGetContainingSegment(
        NestedSplineLayer1Segment[] segments,
        float input,
        out NestedSplineLayer1Segment segment,
        out float segmentStart,
        out float segmentEnd)
    {
        float currentStart = -1f;
        for (int i = 0; i < segments.Length; i++)
        {
            NestedSplineLayer1Segment candidate = segments[i];
            float candidateEnd = Mathf.Max(currentStart, candidate.End);
            bool isLastExplicitSegment = i == segments.Length - 1;
            bool isInsideSegment = input >= currentStart &&
                                   (input <= candidateEnd || (isLastExplicitSegment && candidateEnd >= 1f));
            if (isInsideSegment)
            {
                segment = candidate;
                segmentStart = currentStart;
                segmentEnd = candidateEnd;
                return true;
            }

            currentStart = candidateEnd;
        }

        segment = default;
        segmentStart = 0f;
        segmentEnd = 0f;
        return false;
    }

    private static bool TryGetContainingSegment(
        NestedSplineLayer2Segment[] segments,
        float input,
        out NestedSplineLayer2Segment segment,
        out float segmentStart,
        out float segmentEnd)
    {
        float currentStart = -1f;
        for (int i = 0; i < segments.Length; i++)
        {
            NestedSplineLayer2Segment candidate = segments[i];
            float candidateEnd = Mathf.Max(currentStart, candidate.End);
            bool isLastExplicitSegment = i == segments.Length - 1;
            bool isInsideSegment = input >= currentStart &&
                                   (input <= candidateEnd || (isLastExplicitSegment && candidateEnd >= 1f));
            if (isInsideSegment)
            {
                segment = candidate;
                segmentStart = currentStart;
                segmentEnd = candidateEnd;
                return true;
            }

            currentStart = candidateEnd;
        }

        segment = default;
        segmentStart = 0f;
        segmentEnd = 0f;
        return false;
    }

    private static float GetAxisValue(NestedSplineAxis axis, float gnd, float relief, float veinFold)
    {
        return axis switch
        {
            NestedSplineAxis.Relief => relief,
            NestedSplineAxis.VeinFold => veinFold,
            _ => gnd,
        };
    }

    private static float EvaluateLut2DInternal(float[] lut, int resolution, float normalizedGnd, float normalizedRelief)
    {
        float gndSample = normalizedGnd * (resolution - 1);
        float reliefSample = normalizedRelief * (resolution - 1);

        int gnd0 = Mathf.Clamp(Mathf.FloorToInt(gndSample), 0, resolution - 1);
        int gnd1 = Mathf.Min(gnd0 + 1, resolution - 1);
        int relief0 = Mathf.Clamp(Mathf.FloorToInt(reliefSample), 0, resolution - 1);
        int relief1 = Mathf.Min(relief0 + 1, resolution - 1);

        float tx = gndSample - gnd0;
        float ty = reliefSample - relief0;

        float h00 = lut[GetIndex(gnd0, relief0, resolution)];
        float h10 = lut[GetIndex(gnd1, relief0, resolution)];
        float h01 = lut[GetIndex(gnd0, relief1, resolution)];
        float h11 = lut[GetIndex(gnd1, relief1, resolution)];

        float hx0 = Mathf.Lerp(h00, h10, tx);
        float hx1 = Mathf.Lerp(h01, h11, tx);
        return Mathf.Lerp(hx0, hx1, ty);
    }

    private static float EvaluateLut2DInternal(NativeArray<float> lut, int resolution, float normalizedGnd, float normalizedRelief)
    {
        float gndSample = normalizedGnd * (resolution - 1);
        float reliefSample = normalizedRelief * (resolution - 1);

        int gnd0 = math.clamp((int)math.floor(gndSample), 0, resolution - 1);
        int gnd1 = math.min(gnd0 + 1, resolution - 1);
        int relief0 = math.clamp((int)math.floor(reliefSample), 0, resolution - 1);
        int relief1 = math.min(relief0 + 1, resolution - 1);

        float tx = gndSample - gnd0;
        float ty = reliefSample - relief0;

        float h00 = lut[GetIndex(gnd0, relief0, resolution)];
        float h10 = lut[GetIndex(gnd1, relief0, resolution)];
        float h01 = lut[GetIndex(gnd0, relief1, resolution)];
        float h11 = lut[GetIndex(gnd1, relief1, resolution)];

        float hx0 = math.lerp(h00, h10, tx);
        float hx1 = math.lerp(h01, h11, tx);
        return math.lerp(hx0, hx1, ty);
    }

    private static int GetIndex(int gndIndex, int reliefIndex, int resolution)
    {
        return (reliefIndex * resolution) + gndIndex;
    }

    private static float EvaluateLut3DInternal(float[] lut, int resolution, float normalizedGnd, float normalizedRelief, float normalizedVeinFold)
    {
        float gndSample = normalizedGnd * (resolution - 1);
        float reliefSample = normalizedRelief * (resolution - 1);
        float veinSample = normalizedVeinFold * (resolution - 1);

        int gnd0 = Mathf.Clamp(Mathf.FloorToInt(gndSample), 0, resolution - 1);
        int gnd1 = Mathf.Min(gnd0 + 1, resolution - 1);
        int relief0 = Mathf.Clamp(Mathf.FloorToInt(reliefSample), 0, resolution - 1);
        int relief1 = Mathf.Min(relief0 + 1, resolution - 1);
        int vein0 = Mathf.Clamp(Mathf.FloorToInt(veinSample), 0, resolution - 1);
        int vein1 = Mathf.Min(vein0 + 1, resolution - 1);

        float tx = gndSample - gnd0;
        float ty = reliefSample - relief0;
        float tz = veinSample - vein0;

        float h000 = lut[GetIndex(gnd0, relief0, vein0, resolution)];
        float h100 = lut[GetIndex(gnd1, relief0, vein0, resolution)];
        float h010 = lut[GetIndex(gnd0, relief1, vein0, resolution)];
        float h110 = lut[GetIndex(gnd1, relief1, vein0, resolution)];
        float h001 = lut[GetIndex(gnd0, relief0, vein1, resolution)];
        float h101 = lut[GetIndex(gnd1, relief0, vein1, resolution)];
        float h011 = lut[GetIndex(gnd0, relief1, vein1, resolution)];
        float h111 = lut[GetIndex(gnd1, relief1, vein1, resolution)];

        float hx00 = Mathf.Lerp(h000, h100, tx);
        float hx10 = Mathf.Lerp(h010, h110, tx);
        float hx01 = Mathf.Lerp(h001, h101, tx);
        float hx11 = Mathf.Lerp(h011, h111, tx);
        float hxy0 = Mathf.Lerp(hx00, hx10, ty);
        float hxy1 = Mathf.Lerp(hx01, hx11, ty);
        return Mathf.Lerp(hxy0, hxy1, tz);
    }

    private static float EvaluateLut3DInternal(NativeArray<float> lut, int resolution, float normalizedGnd, float normalizedRelief, float normalizedVeinFold)
    {
        float gndSample = normalizedGnd * (resolution - 1);
        float reliefSample = normalizedRelief * (resolution - 1);
        float veinSample = normalizedVeinFold * (resolution - 1);

        int gnd0 = math.clamp((int)math.floor(gndSample), 0, resolution - 1);
        int gnd1 = math.min(gnd0 + 1, resolution - 1);
        int relief0 = math.clamp((int)math.floor(reliefSample), 0, resolution - 1);
        int relief1 = math.min(relief0 + 1, resolution - 1);
        int vein0 = math.clamp((int)math.floor(veinSample), 0, resolution - 1);
        int vein1 = math.min(vein0 + 1, resolution - 1);

        float tx = gndSample - gnd0;
        float ty = reliefSample - relief0;
        float tz = veinSample - vein0;

        float h000 = lut[GetIndex(gnd0, relief0, vein0, resolution)];
        float h100 = lut[GetIndex(gnd1, relief0, vein0, resolution)];
        float h010 = lut[GetIndex(gnd0, relief1, vein0, resolution)];
        float h110 = lut[GetIndex(gnd1, relief1, vein0, resolution)];
        float h001 = lut[GetIndex(gnd0, relief0, vein1, resolution)];
        float h101 = lut[GetIndex(gnd1, relief0, vein1, resolution)];
        float h011 = lut[GetIndex(gnd0, relief1, vein1, resolution)];
        float h111 = lut[GetIndex(gnd1, relief1, vein1, resolution)];

        float hx00 = math.lerp(h000, h100, tx);
        float hx10 = math.lerp(h010, h110, tx);
        float hx01 = math.lerp(h001, h101, tx);
        float hx11 = math.lerp(h011, h111, tx);
        float hxy0 = math.lerp(hx00, hx10, ty);
        float hxy1 = math.lerp(hx01, hx11, ty);
        return math.lerp(hxy0, hxy1, tz);
    }

    private static int GetIndex(int gndIndex, int reliefIndex, int veinIndex, int resolution)
    {
        return ((veinIndex * resolution) + reliefIndex) * resolution + gndIndex;
    }
}

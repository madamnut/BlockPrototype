using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public static class GndSplineUtility
{
    public static GndSplinePoint[] SanitizePoints(GndSplinePoint[] source)
    {
        GndSplinePoint[] copy = source != null && source.Length >= 2
            ? (GndSplinePoint[])source.Clone()
            : LiftRuntimeSettings.CreateDefaultLowSpline();

        Array.Sort(copy, static (left, right) => left.input.CompareTo(right.input));
        return copy;
    }

    public static float Evaluate(GndSplinePoint[] source, float gnd)
    {
        GndSplinePoint[] spline = SanitizePoints(source);
        return EvaluateSanitized(spline, gnd);
    }

    public static float EvaluateSanitized(GndSplinePoint[] spline, float gnd)
    {
        if (gnd <= spline[0].input)
        {
            return spline[0].height;
        }

        int lastIndex = spline.Length - 1;
        if (gnd >= spline[lastIndex].input)
        {
            return spline[lastIndex].height;
        }

        for (int segmentIndex = 0; segmentIndex < lastIndex; segmentIndex++)
        {
            GndSplinePoint left = spline[segmentIndex];
            GndSplinePoint right = spline[segmentIndex + 1];
            if (gnd > right.input)
            {
                continue;
            }

            float segmentLength = right.input - left.input;
            float t = segmentLength > 0.0001f ? (gnd - left.input) / segmentLength : 0f;
            return Mathf.Lerp(left.height, right.height, t);
        }

        return spline[lastIndex].height;
    }

    public static float[] BakeLut(GndSplinePoint[] source, int resolution)
    {
        int sanitizedResolution = Mathf.Max(2, resolution);
        GndSplinePoint[] spline = SanitizePoints(source);
        float[] lut = new float[sanitizedResolution];
        for (int i = 0; i < lut.Length; i++)
        {
            float t = i / (float)(lut.Length - 1);
            float gnd = Mathf.Lerp(-1f, 1f, t);
            lut[i] = EvaluateSanitized(spline, gnd);
        }

        return lut;
    }

    public static float EvaluateLut(float[] lut, float gnd)
    {
        if (lut == null || lut.Length < 2)
        {
            return 0f;
        }

        float normalized = Mathf.InverseLerp(-1f, 1f, gnd);
        float sampleIndex = normalized * (lut.Length - 1);
        int leftIndex = Mathf.Clamp(Mathf.FloorToInt(sampleIndex), 0, lut.Length - 1);
        int rightIndex = Mathf.Min(leftIndex + 1, lut.Length - 1);
        float t = sampleIndex - leftIndex;
        return Mathf.Lerp(lut[leftIndex], lut[rightIndex], t);
    }

    public static float EvaluateLut(NativeArray<float> lut, float gnd)
    {
        if (!lut.IsCreated || lut.Length < 2)
        {
            return 0f;
        }

        float normalized = math.clamp((gnd + 1f) * 0.5f, 0f, 1f);
        float sampleIndex = normalized * (lut.Length - 1);
        int leftIndex = math.clamp((int)math.floor(sampleIndex), 0, lut.Length - 1);
        int rightIndex = math.min(leftIndex + 1, lut.Length - 1);
        float t = sampleIndex - leftIndex;
        return math.lerp(lut[leftIndex], lut[rightIndex], t);
    }

    public static float EvaluateSanitized(NativeArray<GndSplinePoint> spline, float gnd)
    {
        if (!spline.IsCreated || spline.Length == 0)
        {
            return 0f;
        }

        if (gnd <= spline[0].input)
        {
            return spline[0].height;
        }

        int lastIndex = spline.Length - 1;
        if (gnd >= spline[lastIndex].input)
        {
            return spline[lastIndex].height;
        }

        for (int segmentIndex = 0; segmentIndex < lastIndex; segmentIndex++)
        {
            GndSplinePoint left = spline[segmentIndex];
            GndSplinePoint right = spline[segmentIndex + 1];
            if (gnd > right.input)
            {
                continue;
            }

            float segmentLength = right.input - left.input;
            float t = segmentLength > 0.0001f ? (gnd - left.input) / segmentLength : 0f;
            return math.lerp(left.height, right.height, t);
        }

        return spline[lastIndex].height;
    }

    public static float GetMinHeight(GndSplinePoint[] source)
    {
        GndSplinePoint[] spline = SanitizePoints(source);
        float minHeight = spline[0].height;
        for (int i = 1; i < spline.Length; i++)
        {
            minHeight = Mathf.Min(minHeight, spline[i].height);
        }

        return minHeight;
    }

    public static float GetMaxHeight(GndSplinePoint[] source)
    {
        GndSplinePoint[] spline = SanitizePoints(source);
        float maxHeight = spline[0].height;
        for (int i = 1; i < spline.Length; i++)
        {
            maxHeight = Mathf.Max(maxHeight, spline[i].height);
        }

        return maxHeight;
    }

    public static int ComputeHash(GndSplinePoint[] source)
    {
        GndSplinePoint[] spline = SanitizePoints(source);
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + spline.Length;
            for (int i = 0; i < spline.Length; i++)
            {
                hash = (hash * 31) + BitConverter.SingleToInt32Bits(spline[i].input);
                hash = (hash * 31) + BitConverter.SingleToInt32Bits(spline[i].height);
            }

            return hash;
        }
    }

}

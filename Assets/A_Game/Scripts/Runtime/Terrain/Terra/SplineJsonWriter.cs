using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public static class SplineJsonWriter
{
    public static string Serialize(SplineAsset spline)
    {
        if (spline == null)
        {
            throw new ArgumentNullException(nameof(spline));
        }

        HashSet<SplineAsset> visited = new();
        StringBuilder builder = new(2048);
        WriteSpline(builder, spline, 0, visited);
        builder.AppendLine();
        return builder.ToString();
    }

    private static void WriteSpline(StringBuilder builder, SplineAsset spline, int indent, HashSet<SplineAsset> visited)
    {
        if (!visited.Add(spline))
        {
            throw new InvalidOperationException($"Recursive spline reference detected while serializing '{spline.name}'.");
        }

        string indentText = Indent(indent);
        string childIndent = Indent(indent + 1);
        string pointIndent = Indent(indent + 2);

        builder.AppendLine($"{indentText}{{");
        builder.AppendLine($"{childIndent}\"coordinate\": \"{CoordinateToJson(spline.CoordinateKind)}\",");
        builder.AppendLine($"{childIndent}\"points\": [");

        SplinePointData[] points = spline.Points;
        for (int i = 0; i < points.Length; i++)
        {
            SplinePointData point = points[i];
            bool hasChild = point.Value.UseChildSpline;

            builder.AppendLine($"{pointIndent}{{");
            builder.AppendLine($"{pointIndent}  \"location\": {FormatFloat(point.Location)},");
            builder.Append($"{pointIndent}  \"value\": ");
            if (hasChild)
            {
                builder.AppendLine();
                WriteSpline(builder, point.Value.ChildSpline, indent + 3, visited);
                builder.AppendLine(",");
                builder.AppendLine($"{pointIndent}  \"derivative\": {FormatFloat(point.Derivative)}");
            }
            else
            {
                builder.AppendLine($"{FormatFloat(point.Value.ConstantValue)},");
                builder.AppendLine($"{pointIndent}  \"derivative\": {FormatFloat(point.Derivative)}");
            }

            builder.Append($"{pointIndent}}}");
            if (i < points.Length - 1)
            {
                builder.Append(',');
            }

            builder.AppendLine();
        }

        builder.AppendLine($"{childIndent}]");
        builder.Append($"{indentText}}}");
        visited.Remove(spline);
    }

    private static string CoordinateToJson(SplineCoordinateKind coordinateKind)
    {
        return coordinateKind switch
        {
            SplineCoordinateKind.Continentalness => "continentalness",
            SplineCoordinateKind.Erosion => "erosion",
            SplineCoordinateKind.PeaksAndValleys => "peaks_and_valleys",
            SplineCoordinateKind.Ridges => "ridges",
            _ => throw new ArgumentOutOfRangeException(nameof(coordinateKind), coordinateKind, null),
        };
    }

    private static string FormatFloat(float value)
    {
        return value.ToString("0.########", CultureInfo.InvariantCulture);
    }

    private static string Indent(int level)
    {
        return new string(' ', level * 2);
    }
}

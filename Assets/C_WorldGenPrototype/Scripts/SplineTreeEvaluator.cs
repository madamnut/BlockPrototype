using Unity.Collections;
using Unity.Mathematics;

public static class SplineTreeEvaluator
{
    private const float CoordinateEpsilon = 0.000001f;

    private struct EvalFrame
    {
        public int nodeIndex;
        public byte stage;
        public int pointAIndex;
        public int pointBIndex;
        public float coordinate;
        public float locationA;
        public float locationB;
        public float derivativeA;
        public float derivativeB;
        public float blendT;
        public float valueA;
        public float valueB;
    }

    public static float EvaluateValue(
        float continentalness,
        float erosion,
        float weirdness,
        float peaksValleys,
        SplineTreeBakedNode[] nodes,
        SplineTreeBakedPoint[] points)
    {
        if (nodes == null || points == null || nodes.Length == 0 || points.Length == 0)
        {
            return 0f;
        }

        return Evaluate(
            continentalness,
            erosion,
            weirdness,
            peaksValleys,
            nodes,
            points);
    }

    public static float EvaluateValue(
        float continentalness,
        float erosion,
        float weirdness,
        float peaksValleys,
        NativeArray<SplineTreeBakedNode> nodes,
        NativeArray<SplineTreeBakedPoint> points)
    {
        if (!nodes.IsCreated || !points.IsCreated || nodes.Length == 0 || points.Length == 0)
        {
            return 0f;
        }

        return Evaluate(
            continentalness,
            erosion,
            weirdness,
            peaksValleys,
            nodes,
            points);
    }

    public static int EvaluateHeight(
        float continentalness,
        float erosion,
        float weirdness,
        float peaksValleys,
        SplineTreeBakedNode[] nodes,
        SplineTreeBakedPoint[] points,
        int worldHeight)
    {
        if (nodes == null || points == null || nodes.Length == 0 || points.Length == 0)
        {
            return 0;
        }

        float value = EvaluateValue(
            continentalness,
            erosion,
            weirdness,
            peaksValleys,
            nodes,
            points);
        return math.clamp((int)math.round(value), 0, worldHeight - 1);
    }

    public static int EvaluateHeight(
        float continentalness,
        float erosion,
        float weirdness,
        float peaksValleys,
        NativeArray<SplineTreeBakedNode> nodes,
        NativeArray<SplineTreeBakedPoint> points,
        int worldHeight)
    {
        if (!nodes.IsCreated || !points.IsCreated || nodes.Length == 0 || points.Length == 0)
        {
            return 0;
        }

        float value = EvaluateValue(
            continentalness,
            erosion,
            weirdness,
            peaksValleys,
            nodes,
            points);
        return math.clamp((int)math.round(value), 0, worldHeight - 1);
    }

    private static float Evaluate(
        float continentalness,
        float erosion,
        float weirdness,
        float peaksValleys,
        SplineTreeBakedNode[] nodes,
        SplineTreeBakedPoint[] points)
    {
        FixedList512Bytes<EvalFrame> stack = default;
        stack.Add(new EvalFrame
        {
            nodeIndex = 0,
            stage = 0,
        });

        float lastResult = 0f;
        while (stack.Length > 0)
        {
            int frameIndex = stack.Length - 1;
            EvalFrame frame = stack[frameIndex];
            switch (frame.stage)
            {
                case 0:
                {
                    SplineTreeBakedNode node = nodes[frame.nodeIndex];
                    if (node.pointCount <= 0)
                    {
                        lastResult = 0f;
                        stack.Length = frameIndex;
                        break;
                    }

                    frame.coordinate = SelectCoordinate(node.coordinate, continentalness, erosion, weirdness, peaksValleys);
                    int firstPointIndex = node.firstPointIndex;
                    int lastPointIndex = firstPointIndex + node.pointCount - 1;
                    int pointIndex = FindPointIndex(frame.coordinate, firstPointIndex, node.pointCount, points);

                    if (pointIndex < firstPointIndex)
                    {
                        frame.pointAIndex = firstPointIndex;
                        frame.stage = 20;
                    }
                    else if (pointIndex >= lastPointIndex)
                    {
                        frame.pointAIndex = lastPointIndex;
                        frame.stage = 20;
                    }
                    else
                    {
                        SplineTreeBakedPoint pointA = points[pointIndex];
                        SplineTreeBakedPoint pointB = points[pointIndex + 1];
                        frame.pointAIndex = pointIndex;
                        frame.pointBIndex = pointIndex + 1;
                        frame.locationA = pointA.location;
                        frame.locationB = pointB.location;
                        frame.derivativeA = pointA.derivative;
                        frame.derivativeB = pointB.derivative;
                        float delta = frame.locationB - frame.locationA;
                        frame.blendT = math.abs(delta) > CoordinateEpsilon
                            ? math.saturate((frame.coordinate - frame.locationA) / delta)
                            : 0f;
                        frame.stage = 10;
                    }

                    stack[frameIndex] = frame;
                    break;
                }

                case 10:
                {
                    SplineTreeBakedPoint pointA = points[frame.pointAIndex];
                    if (pointA.valueType == SplineTreePointValueType.NestedSpline && pointA.childNodeIndex >= 0)
                    {
                        frame.stage = 11;
                        stack[frameIndex] = frame;
                        stack.Add(new EvalFrame
                        {
                            nodeIndex = pointA.childNodeIndex,
                            stage = 0,
                        });
                    }
                    else
                    {
                        frame.valueA = pointA.constantValue;
                        frame.stage = 12;
                        stack[frameIndex] = frame;
                    }

                    break;
                }

                case 11:
                    frame.valueA = lastResult;
                    frame.stage = 12;
                    stack[frameIndex] = frame;
                    break;

                case 12:
                {
                    SplineTreeBakedPoint pointB = points[frame.pointBIndex];
                    if (pointB.valueType == SplineTreePointValueType.NestedSpline && pointB.childNodeIndex >= 0)
                    {
                        frame.stage = 13;
                        stack[frameIndex] = frame;
                        stack.Add(new EvalFrame
                        {
                            nodeIndex = pointB.childNodeIndex,
                            stage = 0,
                        });
                    }
                    else
                    {
                        frame.valueB = pointB.constantValue;
                        frame.stage = 14;
                        stack[frameIndex] = frame;
                    }

                    break;
                }

                case 13:
                    frame.valueB = lastResult;
                    frame.stage = 14;
                    stack[frameIndex] = frame;
                    break;

                case 14:
                {
                    float valueDelta = frame.valueB - frame.valueA;
                    float slopeA = (frame.derivativeA * (frame.locationB - frame.locationA)) - valueDelta;
                    float slopeB = (-frame.derivativeB * (frame.locationB - frame.locationA)) + valueDelta;
                    float t = frame.blendT;
                    lastResult = math.lerp(frame.valueA, frame.valueB, t) +
                                 (t * (1f - t) * math.lerp(slopeA, slopeB, t));
                    stack.Length = frameIndex;
                    break;
                }

                case 20:
                {
                    SplineTreeBakedPoint point = points[frame.pointAIndex];
                    frame.locationA = point.location;
                    frame.derivativeA = point.derivative;
                    if (point.valueType == SplineTreePointValueType.NestedSpline && point.childNodeIndex >= 0)
                    {
                        frame.stage = 21;
                        stack[frameIndex] = frame;
                        stack.Add(new EvalFrame
                        {
                            nodeIndex = point.childNodeIndex,
                            stage = 0,
                        });
                    }
                    else
                    {
                        lastResult = point.constantValue + (frame.derivativeA * (frame.coordinate - frame.locationA));
                        stack.Length = frameIndex;
                    }

                    break;
                }

                case 21:
                    lastResult = lastResult + (frame.derivativeA * (frame.coordinate - frame.locationA));
                    stack.Length = frameIndex;
                    break;
            }
        }

        return lastResult;
    }

    private static float Evaluate(
        float continentalness,
        float erosion,
        float weirdness,
        float peaksValleys,
        NativeArray<SplineTreeBakedNode> nodes,
        NativeArray<SplineTreeBakedPoint> points)
    {
        FixedList512Bytes<EvalFrame> stack = default;
        stack.Add(new EvalFrame
        {
            nodeIndex = 0,
            stage = 0,
        });

        float lastResult = 0f;
        while (stack.Length > 0)
        {
            int frameIndex = stack.Length - 1;
            EvalFrame frame = stack[frameIndex];
            switch (frame.stage)
            {
                case 0:
                {
                    SplineTreeBakedNode node = nodes[frame.nodeIndex];
                    if (node.pointCount <= 0)
                    {
                        lastResult = 0f;
                        stack.Length = frameIndex;
                        break;
                    }

                    frame.coordinate = SelectCoordinate(node.coordinate, continentalness, erosion, weirdness, peaksValleys);
                    int firstPointIndex = node.firstPointIndex;
                    int lastPointIndex = firstPointIndex + node.pointCount - 1;
                    int pointIndex = FindPointIndex(frame.coordinate, firstPointIndex, node.pointCount, points);

                    if (pointIndex < firstPointIndex)
                    {
                        frame.pointAIndex = firstPointIndex;
                        frame.stage = 20;
                    }
                    else if (pointIndex >= lastPointIndex)
                    {
                        frame.pointAIndex = lastPointIndex;
                        frame.stage = 20;
                    }
                    else
                    {
                        SplineTreeBakedPoint pointA = points[pointIndex];
                        SplineTreeBakedPoint pointB = points[pointIndex + 1];
                        frame.pointAIndex = pointIndex;
                        frame.pointBIndex = pointIndex + 1;
                        frame.locationA = pointA.location;
                        frame.locationB = pointB.location;
                        frame.derivativeA = pointA.derivative;
                        frame.derivativeB = pointB.derivative;
                        float delta = frame.locationB - frame.locationA;
                        frame.blendT = math.abs(delta) > CoordinateEpsilon
                            ? math.saturate((frame.coordinate - frame.locationA) / delta)
                            : 0f;
                        frame.stage = 10;
                    }

                    stack[frameIndex] = frame;
                    break;
                }

                case 10:
                {
                    SplineTreeBakedPoint pointA = points[frame.pointAIndex];
                    if (pointA.valueType == SplineTreePointValueType.NestedSpline && pointA.childNodeIndex >= 0)
                    {
                        frame.stage = 11;
                        stack[frameIndex] = frame;
                        stack.Add(new EvalFrame
                        {
                            nodeIndex = pointA.childNodeIndex,
                            stage = 0,
                        });
                    }
                    else
                    {
                        frame.valueA = pointA.constantValue;
                        frame.stage = 12;
                        stack[frameIndex] = frame;
                    }

                    break;
                }

                case 11:
                    frame.valueA = lastResult;
                    frame.stage = 12;
                    stack[frameIndex] = frame;
                    break;

                case 12:
                {
                    SplineTreeBakedPoint pointB = points[frame.pointBIndex];
                    if (pointB.valueType == SplineTreePointValueType.NestedSpline && pointB.childNodeIndex >= 0)
                    {
                        frame.stage = 13;
                        stack[frameIndex] = frame;
                        stack.Add(new EvalFrame
                        {
                            nodeIndex = pointB.childNodeIndex,
                            stage = 0,
                        });
                    }
                    else
                    {
                        frame.valueB = pointB.constantValue;
                        frame.stage = 14;
                        stack[frameIndex] = frame;
                    }

                    break;
                }

                case 13:
                    frame.valueB = lastResult;
                    frame.stage = 14;
                    stack[frameIndex] = frame;
                    break;

                case 14:
                {
                    float valueDelta = frame.valueB - frame.valueA;
                    float slopeA = (frame.derivativeA * (frame.locationB - frame.locationA)) - valueDelta;
                    float slopeB = (-frame.derivativeB * (frame.locationB - frame.locationA)) + valueDelta;
                    float t = frame.blendT;
                    lastResult = math.lerp(frame.valueA, frame.valueB, t) +
                                 (t * (1f - t) * math.lerp(slopeA, slopeB, t));
                    stack.Length = frameIndex;
                    break;
                }

                case 20:
                {
                    SplineTreeBakedPoint point = points[frame.pointAIndex];
                    frame.locationA = point.location;
                    frame.derivativeA = point.derivative;
                    if (point.valueType == SplineTreePointValueType.NestedSpline && point.childNodeIndex >= 0)
                    {
                        frame.stage = 21;
                        stack[frameIndex] = frame;
                        stack.Add(new EvalFrame
                        {
                            nodeIndex = point.childNodeIndex,
                            stage = 0,
                        });
                    }
                    else
                    {
                        lastResult = point.constantValue + (frame.derivativeA * (frame.coordinate - frame.locationA));
                        stack.Length = frameIndex;
                    }

                    break;
                }

                case 21:
                    lastResult = lastResult + (frame.derivativeA * (frame.coordinate - frame.locationA));
                    stack.Length = frameIndex;
                    break;
            }
        }

        return lastResult;
    }

    private static int FindPointIndex(
        float coordinate,
        int firstPointIndex,
        int pointCount,
        SplineTreeBakedPoint[] points)
    {
        int low = 0;
        int high = pointCount;
        while (low < high)
        {
            int mid = (low + high) >> 1;
            if (coordinate < points[firstPointIndex + mid].location)
            {
                high = mid;
            }
            else
            {
                low = mid + 1;
            }
        }

        return firstPointIndex + low - 1;
    }

    private static int FindPointIndex(
        float coordinate,
        int firstPointIndex,
        int pointCount,
        NativeArray<SplineTreeBakedPoint> points)
    {
        int low = 0;
        int high = pointCount;
        while (low < high)
        {
            int mid = (low + high) >> 1;
            if (coordinate < points[firstPointIndex + mid].location)
            {
                high = mid;
            }
            else
            {
                low = mid + 1;
            }
        }

        return firstPointIndex + low - 1;
    }

    private static float SelectCoordinate(
        SplineTreeCoordinateSource coordinate,
        float continentalness,
        float erosion,
        float weirdness,
        float peaksValleys)
    {
        switch (coordinate)
        {
            case SplineTreeCoordinateSource.Erosion:
                return erosion;
            case SplineTreeCoordinateSource.Weirdness:
                return weirdness;
            case SplineTreeCoordinateSource.PeaksValleys:
                return peaksValleys;
            default:
                return continentalness;
        }
    }
}

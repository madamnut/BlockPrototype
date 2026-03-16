using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "SplineTree",
    menuName = "World/WorldGen/Spline Tree")]
public sealed partial class SplineTreeAsset : ScriptableObject
{
    [System.Serializable]
    public sealed class PointAsset
    {
        [Range(-1f, 1f)] public float location;
        public float derivative;
        public SplineTreePointValueType valueType = SplineTreePointValueType.Constant;
        public float constantValue = 63f;
        [SerializeReference]
        public NodeAsset childNode;
    }

    [System.Serializable]
    public sealed class NodeAsset
    {
        public SplineTreeCoordinateSource coordinate = SplineTreeCoordinateSource.Continentalness;
        public List<PointAsset> points = new();
    }

    [SerializeField] private NodeAsset rootNode = CreateVanillaOverworldOffsetRootNode();
    [SerializeField, HideInInspector] private SplineTreeBakedNode[] bakedNodes = new SplineTreeBakedNode[0];
    [SerializeField, HideInInspector] private SplineTreeBakedPoint[] bakedPoints = new SplineTreeBakedPoint[0];
    [SerializeField, HideInInspector] private int bakeVersion;
    [SerializeField, HideInInspector] private string bakedSummary = "Not baked";

    public NodeAsset RootNode => rootNode;
    public SplineTreeBakedNode[] BakedNodes => bakedNodes;
    public SplineTreeBakedPoint[] BakedPoints => bakedPoints;
    public int BakeVersion => bakeVersion;
    public string BakedSummary => bakedSummary;
    public bool HasBakedTree => bakedNodes != null && bakedNodes.Length > 0 && bakedPoints != null && bakedPoints.Length > 0;

    public int EvaluateHeight(float continentalness, float erosion, float weirdness, float peaksValleys)
    {
        if (!HasBakedTree)
        {
            return 0;
        }

        return SplineTreeEvaluator.EvaluateHeight(
            continentalness,
            erosion,
            weirdness,
            peaksValleys,
            bakedNodes,
            bakedPoints,
            TerrainData.WorldHeight);
    }

    public float EvaluateValue(float continentalness, float erosion, float weirdness, float peaksValleys)
    {
        if (!HasBakedTree)
        {
            return 0f;
        }

        return SplineTreeEvaluator.EvaluateValue(
            continentalness,
            erosion,
            weirdness,
            peaksValleys,
            bakedNodes,
            bakedPoints);
    }

    public void Bake()
    {
        EnsureDefaults();
        NormalizeNode(rootNode);

        List<SplineTreeBakedNode> nodes = new();
        List<SplineTreeBakedPoint> points = new();
        HashSet<NodeAsset> path = new();

        AddNode(rootNode, nodes, points, path);

        bakedNodes = nodes.ToArray();
        bakedPoints = points.ToArray();
        bakeVersion++;
        bakedSummary = $"Nodes {bakedNodes.Length}, Points {bakedPoints.Length}";
    }

    public void ResetToDefaultTerrainTree()
    {
        rootNode = CreateVanillaOverworldOffsetRootNode();
    }

    public void ResetToVanillaOverworldOffsetTree()
    {
        rootNode = CreateVanillaOverworldOffsetRootNode();
    }

    public void ResetToVanillaOverworldFactorTree()
    {
        rootNode = CreateVanillaOverworldFactorRootNode();
    }

    public void ResetToVanillaOverworldJaggednessTree()
    {
        rootNode = CreateVanillaOverworldJaggednessRootNode();
    }

    private void OnValidate()
    {
        EnsureDefaults();
        NormalizeNode(rootNode);
    }

    private void EnsureDefaults()
    {
        rootNode ??= CreateVanillaOverworldOffsetRootNode();
        rootNode.points ??= new List<PointAsset>();
    }

    private static void NormalizeNode(NodeAsset node)
    {
        if (node == null)
        {
            return;
        }

        node.points ??= new List<PointAsset>();
        for (int i = 0; i < node.points.Count; i++)
        {
            PointAsset point = node.points[i] ?? new PointAsset();
            point.location = Mathf.Clamp(point.location, -1f, 1f);
            node.points[i] = point;

            if (point.childNode != null)
            {
                NormalizeNode(point.childNode);
            }
        }

        node.points.Sort((a, b) => a.location.CompareTo(b.location));
    }

    private static int AddNode(
        NodeAsset node,
        List<SplineTreeBakedNode> nodes,
        List<SplineTreeBakedPoint> points,
        HashSet<NodeAsset> path)
    {
        if (node == null)
        {
            return -1;
        }

        if (!path.Add(node))
        {
            return -1;
        }

        int nodeIndex = nodes.Count;
        nodes.Add(default);

        int firstPointIndex = points.Count;
        for (int i = 0; i < node.points.Count; i++)
        {
            PointAsset point = node.points[i];
            int childNodeIndex = -1;
            if (point.valueType == SplineTreePointValueType.NestedSpline && point.childNode != null)
            {
                childNodeIndex = AddNode(point.childNode, nodes, points, path);
            }

            points.Add(new SplineTreeBakedPoint
            {
                location = point.location,
                derivative = point.derivative,
                valueType = childNodeIndex >= 0 ? SplineTreePointValueType.NestedSpline : SplineTreePointValueType.Constant,
                constantValue = point.constantValue,
                childNodeIndex = childNodeIndex,
            });
        }

        nodes[nodeIndex] = new SplineTreeBakedNode
        {
            coordinate = node.coordinate,
            firstPointIndex = firstPointIndex,
            pointCount = node.points.Count,
        };

        path.Remove(node);
        return nodeIndex;
    }

    private static NodeAsset CreateNode(SplineTreeCoordinateSource coordinate, params PointAsset[] points)
    {
        return new NodeAsset
        {
            coordinate = coordinate,
            points = new List<PointAsset>(points),
        };
    }

    private static PointAsset ConstantPoint(float location, float constantValue, float derivative = 0f)
    {
        return new PointAsset
        {
            location = location,
            derivative = derivative,
            valueType = SplineTreePointValueType.Constant,
            constantValue = constantValue,
        };
    }

    private static PointAsset NestedPoint(float location, NodeAsset childNode, float derivative = 0f, float constantValue = 63f)
    {
        return new PointAsset
        {
            location = location,
            derivative = derivative,
            valueType = SplineTreePointValueType.NestedSpline,
            constantValue = constantValue,
            childNode = childNode,
        };
    }
}

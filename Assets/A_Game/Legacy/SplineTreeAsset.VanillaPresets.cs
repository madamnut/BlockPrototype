using UnityEngine;

public sealed partial class SplineTreeAsset
{
private static NodeAsset CreateVanillaOverworldOffsetRootNode()
{
            NodeAsset node2 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.08880186f, 0.38940096f),
                ConstantPoint(1.0f, 0.69000006f, 0.38940096f));
            NodeAsset node3 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.115760356f, 0.37788022f),
                ConstantPoint(1.0f, 0.6400001f, 0.37788022f));
            NodeAsset node4 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.2222f, 0.0f),
                ConstantPoint(-0.75f, -0.2222f, 0.0f),
                ConstantPoint(-0.65f, 0.0f, 0.0f),
                ConstantPoint(0.5954547f, 2.9802322e-08f, 0.0f),
                ConstantPoint(0.6054547f, 2.9802322e-08f, 0.2534563f),
                ConstantPoint(1.0f, 0.100000024f, 0.2534563f));
            NodeAsset node5 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.3f, 0.5f),
                ConstantPoint(-0.4f, 0.05f, 0.0f),
                ConstantPoint(0.0f, 0.05f, 0.0f),
                ConstantPoint(0.4f, 0.05f, 0.0f),
                ConstantPoint(1.0f, 0.060000002f, 0.007000001f));
            NodeAsset node6 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.15f, 0.5f),
                ConstantPoint(-0.4f, 0.0f, 0.0f),
                ConstantPoint(0.0f, 0.0f, 0.0f),
                ConstantPoint(0.4f, 0.05f, 0.1f),
                ConstantPoint(1.0f, 0.060000002f, 0.007000001f));
            NodeAsset node7 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.15f, 0.5f),
                ConstantPoint(-0.4f, 0.0f, 0.0f),
                ConstantPoint(0.0f, 0.0f, 0.0f),
                ConstantPoint(0.4f, 0.0f, 0.0f),
                ConstantPoint(1.0f, 0.0f, 0.0f));
            NodeAsset node8 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.02f, 0.0f),
                ConstantPoint(-0.4f, -0.03f, 0.0f),
                ConstantPoint(0.0f, -0.03f, 0.0f),
                ConstantPoint(0.4f, 0.0f, 0.06f),
                ConstantPoint(1.0f, 0.0f, 0.0f));
        NodeAsset node1 = CreateNode(
            SplineTreeCoordinateSource.Erosion,
            NestedPoint(-0.85f, node2, 0.0f, 0f),
            NestedPoint(-0.7f, node3, 0.0f, 0f),
            NestedPoint(-0.4f, node4, 0.0f, 0f),
            NestedPoint(-0.35f, node5, 0.0f, 0f),
            NestedPoint(-0.1f, node6, 0.0f, 0f),
            NestedPoint(0.2f, node7, 0.0f, 0f),
            NestedPoint(0.7f, node8, 0.0f, 0f));
            NodeAsset node10 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.08880186f, 0.38940096f),
                ConstantPoint(1.0f, 0.69000006f, 0.38940096f));
            NodeAsset node11 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.115760356f, 0.37788022f),
                ConstantPoint(1.0f, 0.6400001f, 0.37788022f));
            NodeAsset node12 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.2222f, 0.0f),
                ConstantPoint(-0.75f, -0.2222f, 0.0f),
                ConstantPoint(-0.65f, 0.0f, 0.0f),
                ConstantPoint(0.5954547f, 2.9802322e-08f, 0.0f),
                ConstantPoint(0.6054547f, 2.9802322e-08f, 0.2534563f),
                ConstantPoint(1.0f, 0.100000024f, 0.2534563f));
            NodeAsset node13 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.3f, 0.5f),
                ConstantPoint(-0.4f, 0.05f, 0.0f),
                ConstantPoint(0.0f, 0.05f, 0.0f),
                ConstantPoint(0.4f, 0.05f, 0.0f),
                ConstantPoint(1.0f, 0.060000002f, 0.007000001f));
            NodeAsset node14 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.15f, 0.5f),
                ConstantPoint(-0.4f, 0.0f, 0.0f),
                ConstantPoint(0.0f, 0.0f, 0.0f),
                ConstantPoint(0.4f, 0.05f, 0.1f),
                ConstantPoint(1.0f, 0.060000002f, 0.007000001f));
            NodeAsset node15 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.15f, 0.5f),
                ConstantPoint(-0.4f, 0.0f, 0.0f),
                ConstantPoint(0.0f, 0.0f, 0.0f),
                ConstantPoint(0.4f, 0.0f, 0.0f),
                ConstantPoint(1.0f, 0.0f, 0.0f));
            NodeAsset node16 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.02f, 0.0f),
                ConstantPoint(-0.4f, -0.03f, 0.0f),
                ConstantPoint(0.0f, -0.03f, 0.0f),
                ConstantPoint(0.4f, 0.0f, 0.06f),
                ConstantPoint(1.0f, 0.0f, 0.0f));
        NodeAsset node9 = CreateNode(
            SplineTreeCoordinateSource.Erosion,
            NestedPoint(-0.85f, node10, 0.0f, 0f),
            NestedPoint(-0.7f, node11, 0.0f, 0f),
            NestedPoint(-0.4f, node12, 0.0f, 0f),
            NestedPoint(-0.35f, node13, 0.0f, 0f),
            NestedPoint(-0.1f, node14, 0.0f, 0f),
            NestedPoint(0.2f, node15, 0.0f, 0f),
            NestedPoint(0.7f, node16, 0.0f, 0f));
            NodeAsset node18 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.08880186f, 0.38940096f),
                ConstantPoint(1.0f, 0.69000006f, 0.38940096f));
            NodeAsset node19 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.115760356f, 0.37788022f),
                ConstantPoint(1.0f, 0.6400001f, 0.37788022f));
            NodeAsset node20 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.2222f, 0.0f),
                ConstantPoint(-0.75f, -0.2222f, 0.0f),
                ConstantPoint(-0.65f, 0.0f, 0.0f),
                ConstantPoint(0.5954547f, 2.9802322e-08f, 0.0f),
                ConstantPoint(0.6054547f, 2.9802322e-08f, 0.2534563f),
                ConstantPoint(1.0f, 0.100000024f, 0.2534563f));
            NodeAsset node21 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.25f, 0.5f),
                ConstantPoint(-0.4f, 0.05f, 0.0f),
                ConstantPoint(0.0f, 0.05f, 0.0f),
                ConstantPoint(0.4f, 0.05f, 0.0f),
                ConstantPoint(1.0f, 0.060000002f, 0.007000001f));
            NodeAsset node22 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.1f, 0.5f),
                ConstantPoint(-0.4f, 0.001f, 0.01f),
                ConstantPoint(0.0f, 0.003f, 0.01f),
                ConstantPoint(0.4f, 0.05f, 0.094000004f),
                ConstantPoint(1.0f, 0.060000002f, 0.007000001f));
            NodeAsset node23 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.1f, 0.5f),
                ConstantPoint(-0.4f, 0.01f, 0.0f),
                ConstantPoint(0.0f, 0.01f, 0.0f),
                ConstantPoint(0.4f, 0.03f, 0.04f),
                ConstantPoint(1.0f, 0.1f, 0.049f));
            NodeAsset node24 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.02f, 0.0f),
                ConstantPoint(-0.4f, -0.03f, 0.0f),
                ConstantPoint(0.0f, -0.03f, 0.0f),
                ConstantPoint(0.4f, 0.03f, 0.12f),
                ConstantPoint(1.0f, 0.1f, 0.049f));
        NodeAsset node17 = CreateNode(
            SplineTreeCoordinateSource.Erosion,
            NestedPoint(-0.85f, node18, 0.0f, 0f),
            NestedPoint(-0.7f, node19, 0.0f, 0f),
            NestedPoint(-0.4f, node20, 0.0f, 0f),
            NestedPoint(-0.35f, node21, 0.0f, 0f),
            NestedPoint(-0.1f, node22, 0.0f, 0f),
            NestedPoint(0.2f, node23, 0.0f, 0f),
            NestedPoint(0.7f, node24, 0.0f, 0f));
            NodeAsset node26 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, 0.20235021f, 0.0f),
                ConstantPoint(0.0f, 0.7161751f, 0.5138249f),
                ConstantPoint(1.0f, 1.23f, 0.5138249f));
            NodeAsset node27 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, 0.2f, 0.0f),
                ConstantPoint(0.0f, 0.44682026f, 0.43317974f),
                ConstantPoint(1.0f, 0.88f, 0.43317974f));
            NodeAsset node28 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, 0.2f, 0.0f),
                ConstantPoint(0.0f, 0.30829495f, 0.3917051f),
                ConstantPoint(1.0f, 0.70000005f, 0.3917051f));
            NodeAsset node29 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.25f, 0.5f),
                ConstantPoint(-0.4f, 0.35f, 0.0f),
                ConstantPoint(0.0f, 0.35f, 0.0f),
                ConstantPoint(0.4f, 0.35f, 0.0f),
                ConstantPoint(1.0f, 0.42000002f, 0.049000014f));
            NodeAsset node30 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.1f, 0.5f),
                ConstantPoint(-0.4f, 0.0069999998f, 0.07f),
                ConstantPoint(0.0f, 0.021f, 0.07f),
                ConstantPoint(0.4f, 0.35f, 0.658f),
                ConstantPoint(1.0f, 0.42000002f, 0.049000014f));
            NodeAsset node31 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.1f, 0.5f),
                ConstantPoint(-0.4f, 0.01f, 0.0f),
                ConstantPoint(0.0f, 0.01f, 0.0f),
                ConstantPoint(0.4f, 0.03f, 0.04f),
                ConstantPoint(1.0f, 0.1f, 0.049f));
            NodeAsset node32 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.1f, 0.5f),
                ConstantPoint(-0.4f, 0.01f, 0.0f),
                ConstantPoint(0.0f, 0.01f, 0.0f),
                ConstantPoint(0.4f, 0.03f, 0.04f),
                ConstantPoint(1.0f, 0.1f, 0.049f));
                NodeAsset node34 = CreateNode(
                    SplineTreeCoordinateSource.PeaksValleys,
                    ConstantPoint(-1.0f, -0.1f, 0.5f),
                    ConstantPoint(-0.4f, 0.01f, 0.0f),
                    ConstantPoint(0.0f, 0.01f, 0.0f),
                    ConstantPoint(0.4f, 0.03f, 0.04f),
                    ConstantPoint(1.0f, 0.1f, 0.049f));
            NodeAsset node33 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.1f, 0.0f),
                NestedPoint(-0.4f, node34, 0.0f, 0f),
                ConstantPoint(0.0f, 0.17f, 0.0f));
                NodeAsset node36 = CreateNode(
                    SplineTreeCoordinateSource.PeaksValleys,
                    ConstantPoint(-1.0f, -0.1f, 0.5f),
                    ConstantPoint(-0.4f, 0.01f, 0.0f),
                    ConstantPoint(0.0f, 0.01f, 0.0f),
                    ConstantPoint(0.4f, 0.03f, 0.04f),
                    ConstantPoint(1.0f, 0.1f, 0.049f));
            NodeAsset node35 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.1f, 0.0f),
                NestedPoint(-0.4f, node36, 0.0f, 0f),
                ConstantPoint(0.0f, 0.17f, 0.0f));
            NodeAsset node37 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.1f, 0.5f),
                ConstantPoint(-0.4f, 0.01f, 0.0f),
                ConstantPoint(0.0f, 0.01f, 0.0f),
                ConstantPoint(0.4f, 0.03f, 0.04f),
                ConstantPoint(1.0f, 0.1f, 0.049f));
            NodeAsset node38 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.02f, 0.0f),
                ConstantPoint(-0.4f, -0.03f, 0.0f),
                ConstantPoint(0.0f, -0.03f, 0.0f),
                ConstantPoint(0.4f, 0.03f, 0.12f),
                ConstantPoint(1.0f, 0.1f, 0.049f));
        NodeAsset node25 = CreateNode(
            SplineTreeCoordinateSource.Erosion,
            NestedPoint(-0.85f, node26, 0.0f, 0f),
            NestedPoint(-0.7f, node27, 0.0f, 0f),
            NestedPoint(-0.4f, node28, 0.0f, 0f),
            NestedPoint(-0.35f, node29, 0.0f, 0f),
            NestedPoint(-0.1f, node30, 0.0f, 0f),
            NestedPoint(0.2f, node31, 0.0f, 0f),
            NestedPoint(0.4f, node32, 0.0f, 0f),
            NestedPoint(0.45f, node33, 0.0f, 0f),
            NestedPoint(0.55f, node35, 0.0f, 0f),
            NestedPoint(0.58f, node37, 0.0f, 0f),
            NestedPoint(0.7f, node38, 0.0f, 0f));
            NodeAsset node40 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, 0.34792626f, 0.0f),
                ConstantPoint(0.0f, 0.9239631f, 0.5760369f),
                ConstantPoint(1.0f, 1.5f, 0.5760369f));
            NodeAsset node41 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, 0.2f, 0.0f),
                ConstantPoint(0.0f, 0.5391705f, 0.4608295f),
                ConstantPoint(1.0f, 1.0f, 0.4608295f));
            NodeAsset node42 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, 0.2f, 0.0f),
                ConstantPoint(0.0f, 0.5391705f, 0.4608295f),
                ConstantPoint(1.0f, 1.0f, 0.4608295f));
            NodeAsset node43 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.2f, 0.5f),
                ConstantPoint(-0.4f, 0.5f, 0.0f),
                ConstantPoint(0.0f, 0.5f, 0.0f),
                ConstantPoint(0.4f, 0.5f, 0.0f),
                ConstantPoint(1.0f, 0.6f, 0.070000015f));
            NodeAsset node44 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.05f, 0.5f),
                ConstantPoint(-0.4f, 0.01f, 0.099999994f),
                ConstantPoint(0.0f, 0.03f, 0.099999994f),
                ConstantPoint(0.4f, 0.5f, 0.94f),
                ConstantPoint(1.0f, 0.6f, 0.070000015f));
            NodeAsset node45 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.05f, 0.5f),
                ConstantPoint(-0.4f, 0.01f, 0.0f),
                ConstantPoint(0.0f, 0.01f, 0.0f),
                ConstantPoint(0.4f, 0.03f, 0.04f),
                ConstantPoint(1.0f, 0.1f, 0.049f));
            NodeAsset node46 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.05f, 0.5f),
                ConstantPoint(-0.4f, 0.01f, 0.0f),
                ConstantPoint(0.0f, 0.01f, 0.0f),
                ConstantPoint(0.4f, 0.03f, 0.04f),
                ConstantPoint(1.0f, 0.1f, 0.049f));
                NodeAsset node48 = CreateNode(
                    SplineTreeCoordinateSource.PeaksValleys,
                    ConstantPoint(-1.0f, -0.05f, 0.5f),
                    ConstantPoint(-0.4f, 0.01f, 0.0f),
                    ConstantPoint(0.0f, 0.01f, 0.0f),
                    ConstantPoint(0.4f, 0.03f, 0.04f),
                    ConstantPoint(1.0f, 0.1f, 0.049f));
            NodeAsset node47 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.05f, 0.0f),
                NestedPoint(-0.4f, node48, 0.0f, 0f),
                ConstantPoint(0.0f, 0.17f, 0.0f));
                NodeAsset node50 = CreateNode(
                    SplineTreeCoordinateSource.PeaksValleys,
                    ConstantPoint(-1.0f, -0.05f, 0.5f),
                    ConstantPoint(-0.4f, 0.01f, 0.0f),
                    ConstantPoint(0.0f, 0.01f, 0.0f),
                    ConstantPoint(0.4f, 0.03f, 0.04f),
                    ConstantPoint(1.0f, 0.1f, 0.049f));
            NodeAsset node49 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.05f, 0.0f),
                NestedPoint(-0.4f, node50, 0.0f, 0f),
                ConstantPoint(0.0f, 0.17f, 0.0f));
            NodeAsset node51 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.05f, 0.5f),
                ConstantPoint(-0.4f, 0.01f, 0.0f),
                ConstantPoint(0.0f, 0.01f, 0.0f),
                ConstantPoint(0.4f, 0.03f, 0.04f),
                ConstantPoint(1.0f, 0.1f, 0.049f));
            NodeAsset node52 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-1.0f, -0.02f, 0.015f),
                ConstantPoint(-0.4f, 0.01f, 0.0f),
                ConstantPoint(0.0f, 0.01f, 0.0f),
                ConstantPoint(0.4f, 0.03f, 0.04f),
                ConstantPoint(1.0f, 0.1f, 0.049f));
        NodeAsset node39 = CreateNode(
            SplineTreeCoordinateSource.Erosion,
            NestedPoint(-0.85f, node40, 0.0f, 0f),
            NestedPoint(-0.7f, node41, 0.0f, 0f),
            NestedPoint(-0.4f, node42, 0.0f, 0f),
            NestedPoint(-0.35f, node43, 0.0f, 0f),
            NestedPoint(-0.1f, node44, 0.0f, 0f),
            NestedPoint(0.2f, node45, 0.0f, 0f),
            NestedPoint(0.4f, node46, 0.0f, 0f),
            NestedPoint(0.45f, node47, 0.0f, 0f),
            NestedPoint(0.55f, node49, 0.0f, 0f),
            NestedPoint(0.58f, node51, 0.0f, 0f),
            NestedPoint(0.7f, node52, 0.0f, 0f));
    NodeAsset node0 = CreateNode(
        SplineTreeCoordinateSource.Continentalness,
        ConstantPoint(-1.1f, 0.044f, 0.0f),
        ConstantPoint(-1.02f, -0.2222f, 0.0f),
        ConstantPoint(-0.51f, -0.2222f, 0.0f),
        ConstantPoint(-0.44f, -0.12f, 0.0f),
        ConstantPoint(-0.18f, -0.12f, 0.0f),
        NestedPoint(-0.16f, node1, 0.0f, 0f),
        NestedPoint(-0.15f, node9, 0.0f, 0f),
        NestedPoint(-0.1f, node17, 0.0f, 0f),
        NestedPoint(0.25f, node25, 0.0f, 0f),
        NestedPoint(1.0f, node39, 0.0f, 0f));
    return node0;
}

private static NodeAsset CreateVanillaOverworldFactorRootNode()
{
            NodeAsset node2 = CreateNode(
                SplineTreeCoordinateSource.Weirdness,
                ConstantPoint(-0.2f, 6.3f, 0.0f),
                ConstantPoint(0.2f, 6.25f, 0.0f));
            NodeAsset node3 = CreateNode(
                SplineTreeCoordinateSource.Weirdness,
                ConstantPoint(-0.05f, 6.3f, 0.0f),
                ConstantPoint(0.05f, 2.67f, 0.0f));
            NodeAsset node4 = CreateNode(
                SplineTreeCoordinateSource.Weirdness,
                ConstantPoint(-0.2f, 6.3f, 0.0f),
                ConstantPoint(0.2f, 6.25f, 0.0f));
            NodeAsset node5 = CreateNode(
                SplineTreeCoordinateSource.Weirdness,
                ConstantPoint(-0.2f, 6.3f, 0.0f),
                ConstantPoint(0.2f, 6.25f, 0.0f));
            NodeAsset node6 = CreateNode(
                SplineTreeCoordinateSource.Weirdness,
                ConstantPoint(-0.05f, 2.67f, 0.0f),
                ConstantPoint(0.05f, 6.3f, 0.0f));
            NodeAsset node7 = CreateNode(
                SplineTreeCoordinateSource.Weirdness,
                ConstantPoint(-0.2f, 6.3f, 0.0f),
                ConstantPoint(0.2f, 6.25f, 0.0f));
                NodeAsset node9 = CreateNode(
                    SplineTreeCoordinateSource.Weirdness,
                    ConstantPoint(0.0f, 6.25f, 0.0f),
                    ConstantPoint(0.1f, 0.625f, 0.0f));
            NodeAsset node8 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-0.9f, 6.25f, 0.0f),
                NestedPoint(-0.69f, node9, 0.0f, 0f));
                NodeAsset node11 = CreateNode(
                    SplineTreeCoordinateSource.Weirdness,
                    ConstantPoint(0.0f, 6.25f, 0.0f),
                    ConstantPoint(0.1f, 0.625f, 0.0f));
            NodeAsset node10 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-0.9f, 6.25f, 0.0f),
                NestedPoint(-0.69f, node11, 0.0f, 0f));
        NodeAsset node1 = CreateNode(
            SplineTreeCoordinateSource.Erosion,
            NestedPoint(-0.6f, node2, 0.0f, 0f),
            NestedPoint(-0.5f, node3, 0.0f, 0f),
            NestedPoint(-0.35f, node4, 0.0f, 0f),
            NestedPoint(-0.25f, node5, 0.0f, 0f),
            NestedPoint(-0.1f, node6, 0.0f, 0f),
            NestedPoint(0.03f, node7, 0.0f, 0f),
            ConstantPoint(0.35f, 6.25f, 0.0f),
            NestedPoint(0.45f, node8, 0.0f, 0f),
            NestedPoint(0.55f, node10, 0.0f, 0f),
            ConstantPoint(0.62f, 6.25f, 0.0f));
            NodeAsset node13 = CreateNode(
                SplineTreeCoordinateSource.Weirdness,
                ConstantPoint(-0.2f, 6.3f, 0.0f),
                ConstantPoint(0.2f, 5.47f, 0.0f));
            NodeAsset node14 = CreateNode(
                SplineTreeCoordinateSource.Weirdness,
                ConstantPoint(-0.05f, 6.3f, 0.0f),
                ConstantPoint(0.05f, 2.67f, 0.0f));
            NodeAsset node15 = CreateNode(
                SplineTreeCoordinateSource.Weirdness,
                ConstantPoint(-0.2f, 6.3f, 0.0f),
                ConstantPoint(0.2f, 5.47f, 0.0f));
            NodeAsset node16 = CreateNode(
                SplineTreeCoordinateSource.Weirdness,
                ConstantPoint(-0.2f, 6.3f, 0.0f),
                ConstantPoint(0.2f, 5.47f, 0.0f));
            NodeAsset node17 = CreateNode(
                SplineTreeCoordinateSource.Weirdness,
                ConstantPoint(-0.05f, 2.67f, 0.0f),
                ConstantPoint(0.05f, 6.3f, 0.0f));
            NodeAsset node18 = CreateNode(
                SplineTreeCoordinateSource.Weirdness,
                ConstantPoint(-0.2f, 6.3f, 0.0f),
                ConstantPoint(0.2f, 5.47f, 0.0f));
                NodeAsset node20 = CreateNode(
                    SplineTreeCoordinateSource.Weirdness,
                    ConstantPoint(0.0f, 5.47f, 0.0f),
                    ConstantPoint(0.1f, 0.625f, 0.0f));
            NodeAsset node19 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-0.9f, 5.47f, 0.0f),
                NestedPoint(-0.69f, node20, 0.0f, 0f));
                NodeAsset node22 = CreateNode(
                    SplineTreeCoordinateSource.Weirdness,
                    ConstantPoint(0.0f, 5.47f, 0.0f),
                    ConstantPoint(0.1f, 0.625f, 0.0f));
            NodeAsset node21 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-0.9f, 5.47f, 0.0f),
                NestedPoint(-0.69f, node22, 0.0f, 0f));
        NodeAsset node12 = CreateNode(
            SplineTreeCoordinateSource.Erosion,
            NestedPoint(-0.6f, node13, 0.0f, 0f),
            NestedPoint(-0.5f, node14, 0.0f, 0f),
            NestedPoint(-0.35f, node15, 0.0f, 0f),
            NestedPoint(-0.25f, node16, 0.0f, 0f),
            NestedPoint(-0.1f, node17, 0.0f, 0f),
            NestedPoint(0.03f, node18, 0.0f, 0f),
            ConstantPoint(0.35f, 5.47f, 0.0f),
            NestedPoint(0.45f, node19, 0.0f, 0f),
            NestedPoint(0.55f, node21, 0.0f, 0f),
            ConstantPoint(0.62f, 5.47f, 0.0f));
            NodeAsset node24 = CreateNode(
                SplineTreeCoordinateSource.Weirdness,
                ConstantPoint(-0.2f, 6.3f, 0.0f),
                ConstantPoint(0.2f, 5.08f, 0.0f));
            NodeAsset node25 = CreateNode(
                SplineTreeCoordinateSource.Weirdness,
                ConstantPoint(-0.05f, 6.3f, 0.0f),
                ConstantPoint(0.05f, 2.67f, 0.0f));
            NodeAsset node26 = CreateNode(
                SplineTreeCoordinateSource.Weirdness,
                ConstantPoint(-0.2f, 6.3f, 0.0f),
                ConstantPoint(0.2f, 5.08f, 0.0f));
            NodeAsset node27 = CreateNode(
                SplineTreeCoordinateSource.Weirdness,
                ConstantPoint(-0.2f, 6.3f, 0.0f),
                ConstantPoint(0.2f, 5.08f, 0.0f));
            NodeAsset node28 = CreateNode(
                SplineTreeCoordinateSource.Weirdness,
                ConstantPoint(-0.05f, 2.67f, 0.0f),
                ConstantPoint(0.05f, 6.3f, 0.0f));
            NodeAsset node29 = CreateNode(
                SplineTreeCoordinateSource.Weirdness,
                ConstantPoint(-0.2f, 6.3f, 0.0f),
                ConstantPoint(0.2f, 5.08f, 0.0f));
                NodeAsset node31 = CreateNode(
                    SplineTreeCoordinateSource.Weirdness,
                    ConstantPoint(0.0f, 5.08f, 0.0f),
                    ConstantPoint(0.1f, 0.625f, 0.0f));
            NodeAsset node30 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-0.9f, 5.08f, 0.0f),
                NestedPoint(-0.69f, node31, 0.0f, 0f));
                NodeAsset node33 = CreateNode(
                    SplineTreeCoordinateSource.Weirdness,
                    ConstantPoint(0.0f, 5.08f, 0.0f),
                    ConstantPoint(0.1f, 0.625f, 0.0f));
            NodeAsset node32 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(-0.9f, 5.08f, 0.0f),
                NestedPoint(-0.69f, node33, 0.0f, 0f));
        NodeAsset node23 = CreateNode(
            SplineTreeCoordinateSource.Erosion,
            NestedPoint(-0.6f, node24, 0.0f, 0f),
            NestedPoint(-0.5f, node25, 0.0f, 0f),
            NestedPoint(-0.35f, node26, 0.0f, 0f),
            NestedPoint(-0.25f, node27, 0.0f, 0f),
            NestedPoint(-0.1f, node28, 0.0f, 0f),
            NestedPoint(0.03f, node29, 0.0f, 0f),
            ConstantPoint(0.35f, 5.08f, 0.0f),
            NestedPoint(0.45f, node30, 0.0f, 0f),
            NestedPoint(0.55f, node32, 0.0f, 0f),
            ConstantPoint(0.62f, 5.08f, 0.0f));
            NodeAsset node35 = CreateNode(
                SplineTreeCoordinateSource.Weirdness,
                ConstantPoint(-0.2f, 6.3f, 0.0f),
                ConstantPoint(0.2f, 4.69f, 0.0f));
            NodeAsset node36 = CreateNode(
                SplineTreeCoordinateSource.Weirdness,
                ConstantPoint(-0.05f, 6.3f, 0.0f),
                ConstantPoint(0.05f, 2.67f, 0.0f));
            NodeAsset node37 = CreateNode(
                SplineTreeCoordinateSource.Weirdness,
                ConstantPoint(-0.2f, 6.3f, 0.0f),
                ConstantPoint(0.2f, 4.69f, 0.0f));
            NodeAsset node38 = CreateNode(
                SplineTreeCoordinateSource.Weirdness,
                ConstantPoint(-0.2f, 6.3f, 0.0f),
                ConstantPoint(0.2f, 4.69f, 0.0f));
            NodeAsset node39 = CreateNode(
                SplineTreeCoordinateSource.Weirdness,
                ConstantPoint(-0.05f, 2.67f, 0.0f),
                ConstantPoint(0.05f, 6.3f, 0.0f));
            NodeAsset node40 = CreateNode(
                SplineTreeCoordinateSource.Weirdness,
                ConstantPoint(-0.2f, 6.3f, 0.0f),
                ConstantPoint(0.2f, 4.69f, 0.0f));
                NodeAsset node42 = CreateNode(
                    SplineTreeCoordinateSource.Weirdness,
                    ConstantPoint(-0.2f, 6.3f, 0.0f),
                    ConstantPoint(0.2f, 4.69f, 0.0f));
            NodeAsset node41 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                NestedPoint(0.45f, node42, 0.0f, 0f),
                ConstantPoint(0.7f, 1.56f, 0.0f));
                NodeAsset node44 = CreateNode(
                    SplineTreeCoordinateSource.Weirdness,
                    ConstantPoint(-0.2f, 6.3f, 0.0f),
                    ConstantPoint(0.2f, 4.69f, 0.0f));
            NodeAsset node43 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                NestedPoint(0.45f, node44, 0.0f, 0f),
                ConstantPoint(0.7f, 1.56f, 0.0f));
                NodeAsset node46 = CreateNode(
                    SplineTreeCoordinateSource.Weirdness,
                    ConstantPoint(-0.2f, 6.3f, 0.0f),
                    ConstantPoint(0.2f, 4.69f, 0.0f));
            NodeAsset node45 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                NestedPoint(-0.7f, node46, 0.0f, 0f),
                ConstantPoint(-0.15f, 1.37f, 0.0f));
                NodeAsset node48 = CreateNode(
                    SplineTreeCoordinateSource.Weirdness,
                    ConstantPoint(-0.2f, 6.3f, 0.0f),
                    ConstantPoint(0.2f, 4.69f, 0.0f));
            NodeAsset node47 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                NestedPoint(-0.7f, node48, 0.0f, 0f),
                ConstantPoint(-0.15f, 1.37f, 0.0f));
        NodeAsset node34 = CreateNode(
            SplineTreeCoordinateSource.Erosion,
            NestedPoint(-0.6f, node35, 0.0f, 0f),
            NestedPoint(-0.5f, node36, 0.0f, 0f),
            NestedPoint(-0.35f, node37, 0.0f, 0f),
            NestedPoint(-0.25f, node38, 0.0f, 0f),
            NestedPoint(-0.1f, node39, 0.0f, 0f),
            NestedPoint(0.03f, node40, 0.0f, 0f),
            NestedPoint(0.05f, node41, 0.0f, 0f),
            NestedPoint(0.4f, node43, 0.0f, 0f),
            NestedPoint(0.45f, node45, 0.0f, 0f),
            NestedPoint(0.55f, node47, 0.0f, 0f),
            ConstantPoint(0.58f, 4.69f, 0.0f));
    NodeAsset node0 = CreateNode(
        SplineTreeCoordinateSource.Continentalness,
        ConstantPoint(-0.19f, 3.95f, 0.0f),
        NestedPoint(-0.15f, node1, 0.0f, 0f),
        NestedPoint(-0.1f, node12, 0.0f, 0f),
        NestedPoint(0.03f, node23, 0.0f, 0f),
        NestedPoint(0.06f, node34, 0.0f, 0f));
    return node0;
}

private static NodeAsset CreateVanillaOverworldJaggednessRootNode()
{
                NodeAsset node3 = CreateNode(
                    SplineTreeCoordinateSource.Weirdness,
                    ConstantPoint(-0.01f, 0.63f, 0.0f),
                    ConstantPoint(0.01f, 0.3f, 0.0f));
            NodeAsset node2 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(0.19999999f, 0.0f, 0.0f),
                ConstantPoint(0.44999996f, 0.0f, 0.0f),
                NestedPoint(1.0f, node3, 0.0f, 0f));
                NodeAsset node5 = CreateNode(
                    SplineTreeCoordinateSource.Weirdness,
                    ConstantPoint(-0.01f, 0.315f, 0.0f),
                    ConstantPoint(0.01f, 0.15f, 0.0f));
            NodeAsset node4 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(0.19999999f, 0.0f, 0.0f),
                ConstantPoint(0.44999996f, 0.0f, 0.0f),
                NestedPoint(1.0f, node5, 0.0f, 0f));
                NodeAsset node7 = CreateNode(
                    SplineTreeCoordinateSource.Weirdness,
                    ConstantPoint(-0.01f, 0.315f, 0.0f),
                    ConstantPoint(0.01f, 0.15f, 0.0f));
            NodeAsset node6 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(0.19999999f, 0.0f, 0.0f),
                ConstantPoint(0.44999996f, 0.0f, 0.0f),
                NestedPoint(1.0f, node7, 0.0f, 0f));
        NodeAsset node1 = CreateNode(
            SplineTreeCoordinateSource.Erosion,
            NestedPoint(-1.0f, node2, 0.0f, 0f),
            NestedPoint(-0.78f, node4, 0.0f, 0f),
            NestedPoint(-0.5775f, node6, 0.0f, 0f),
            ConstantPoint(-0.375f, 0.0f, 0.0f));
                NodeAsset node10 = CreateNode(
                    SplineTreeCoordinateSource.Weirdness,
                    ConstantPoint(-0.01f, 0.63f, 0.0f),
                    ConstantPoint(0.01f, 0.3f, 0.0f));
                NodeAsset node11 = CreateNode(
                    SplineTreeCoordinateSource.Weirdness,
                    ConstantPoint(-0.01f, 0.63f, 0.0f),
                    ConstantPoint(0.01f, 0.3f, 0.0f));
            NodeAsset node9 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(0.19999999f, 0.0f, 0.0f),
                NestedPoint(0.44999996f, node10, 0.0f, 0f),
                NestedPoint(1.0f, node11, 0.0f, 0f));
                NodeAsset node13 = CreateNode(
                    SplineTreeCoordinateSource.Weirdness,
                    ConstantPoint(-0.01f, 0.63f, 0.0f),
                    ConstantPoint(0.01f, 0.3f, 0.0f));
            NodeAsset node12 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(0.19999999f, 0.0f, 0.0f),
                ConstantPoint(0.44999996f, 0.0f, 0.0f),
                NestedPoint(1.0f, node13, 0.0f, 0f));
                NodeAsset node15 = CreateNode(
                    SplineTreeCoordinateSource.Weirdness,
                    ConstantPoint(-0.01f, 0.63f, 0.0f),
                    ConstantPoint(0.01f, 0.3f, 0.0f));
            NodeAsset node14 = CreateNode(
                SplineTreeCoordinateSource.PeaksValleys,
                ConstantPoint(0.19999999f, 0.0f, 0.0f),
                ConstantPoint(0.44999996f, 0.0f, 0.0f),
                NestedPoint(1.0f, node15, 0.0f, 0f));
        NodeAsset node8 = CreateNode(
            SplineTreeCoordinateSource.Erosion,
            NestedPoint(-1.0f, node9, 0.0f, 0f),
            NestedPoint(-0.78f, node12, 0.0f, 0f),
            NestedPoint(-0.5775f, node14, 0.0f, 0f),
            ConstantPoint(-0.375f, 0.0f, 0.0f));
    NodeAsset node0 = CreateNode(
        SplineTreeCoordinateSource.Continentalness,
        ConstantPoint(-0.11f, 0.0f, 0.0f),
        NestedPoint(0.03f, node1, 0.0f, 0f),
        NestedPoint(0.65f, node8, 0.0f, 0f));
    return node0;
}
}

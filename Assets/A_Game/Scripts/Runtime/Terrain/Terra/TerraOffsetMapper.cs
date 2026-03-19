using UnityEngine;

public sealed class TerraOffsetMapper
{
    private readonly TerraContinentalnessOffsetGraphAsset _graph;

    public TerraOffsetMapper(TerraContinentalnessOffsetGraphAsset graph)
    {
        _graph = graph != null
            ? graph
            : throw new System.ArgumentNullException(nameof(graph), "TerraOffsetMapper requires an offset graph asset.");
    }

    public float Map(float continentalness)
    {
        return _graph.EvaluateOffset(continentalness);
    }
}

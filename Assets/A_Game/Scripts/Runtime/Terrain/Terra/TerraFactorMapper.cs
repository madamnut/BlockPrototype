using UnityEngine;

public sealed class TerraFactorMapper
{
    private readonly TerraContinentalnessFactorGraphAsset _graph;

    public TerraFactorMapper(TerraContinentalnessFactorGraphAsset graph)
    {
        _graph = graph != null
            ? graph
            : throw new System.ArgumentNullException(nameof(graph), "TerraFactorMapper requires a factor graph asset.");
    }

    public float Map(float continentalness)
    {
        return _graph.EvaluateFactor(continentalness);
    }
}

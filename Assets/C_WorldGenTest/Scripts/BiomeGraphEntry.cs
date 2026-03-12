using System;
using UnityEngine;

[Serializable]
public struct BiomeGraphEntry
{
    [SerializeField] private string biomeName;
    [SerializeField] private Color color;
    [SerializeField] private Vector2 climatePoint;

    public readonly string BiomeName => biomeName;
    public readonly Color Color => color;
    public readonly Vector2 ClimatePoint => climatePoint;
}

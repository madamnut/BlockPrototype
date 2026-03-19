using UnityEngine;

public readonly struct TerraDensityContext
{
    public readonly int worldX;
    public readonly int worldY;
    public readonly int worldZ;
    public readonly int surfaceHeight;
    public readonly float continentalness;
    public readonly float erosion;
    public readonly float weirdness;
    public readonly float peaksAndValleys;
    public readonly float offset;
    public readonly float factor;
    public readonly float jaggedness;

    public TerraDensityContext(
        int worldX,
        int worldY,
        int worldZ,
        int surfaceHeight,
        float continentalness,
        float erosion,
        float weirdness,
        float peaksAndValleys,
        float offset,
        float factor,
        float jaggedness)
    {
        this.worldX = worldX;
        this.worldY = worldY;
        this.worldZ = worldZ;
        this.surfaceHeight = surfaceHeight;
        this.continentalness = continentalness;
        this.erosion = erosion;
        this.weirdness = weirdness;
        this.peaksAndValleys = peaksAndValleys;
        this.offset = offset;
        this.factor = factor;
        this.jaggedness = jaggedness;
    }
}

public enum TerraShapeChannel
{
    Continentalness,
    Erosion,
    Weirdness,
    PeaksAndValleys,
    Offset,
    Factor,
    Jaggedness,
}

public interface ITerraDensityFunction
{
    float Evaluate(in TerraDensityContext context);
}

public sealed class TerraConstantDensityFunction : ITerraDensityFunction
{
    private readonly float _value;

    public TerraConstantDensityFunction(float value)
    {
        _value = value;
    }

    public float Evaluate(in TerraDensityContext context)
    {
        return _value;
    }
}

public sealed class TerraAddDensityFunction : ITerraDensityFunction
{
    private readonly ITerraDensityFunction _left;
    private readonly ITerraDensityFunction _right;

    public TerraAddDensityFunction(ITerraDensityFunction left, ITerraDensityFunction right)
    {
        _left = left;
        _right = right;
    }

    public float Evaluate(in TerraDensityContext context)
    {
        return _left.Evaluate(in context) + _right.Evaluate(in context);
    }
}

public sealed class TerraSubtractDensityFunction : ITerraDensityFunction
{
    private readonly ITerraDensityFunction _left;
    private readonly ITerraDensityFunction _right;

    public TerraSubtractDensityFunction(ITerraDensityFunction left, ITerraDensityFunction right)
    {
        _left = left;
        _right = right;
    }

    public float Evaluate(in TerraDensityContext context)
    {
        return _left.Evaluate(in context) - _right.Evaluate(in context);
    }
}

public sealed class TerraMultiplyDensityFunction : ITerraDensityFunction
{
    private readonly ITerraDensityFunction _left;
    private readonly ITerraDensityFunction _right;

    public TerraMultiplyDensityFunction(ITerraDensityFunction left, ITerraDensityFunction right)
    {
        _left = left;
        _right = right;
    }

    public float Evaluate(in TerraDensityContext context)
    {
        return _left.Evaluate(in context) * _right.Evaluate(in context);
    }
}

public sealed class TerraAbsDensityFunction : ITerraDensityFunction
{
    private readonly ITerraDensityFunction _input;

    public TerraAbsDensityFunction(ITerraDensityFunction input)
    {
        _input = input;
    }

    public float Evaluate(in TerraDensityContext context)
    {
        return Mathf.Abs(_input.Evaluate(in context));
    }
}

public sealed class TerraClampDensityFunction : ITerraDensityFunction
{
    private readonly ITerraDensityFunction _input;
    private readonly float _min;
    private readonly float _max;

    public TerraClampDensityFunction(ITerraDensityFunction input, float min, float max)
    {
        _input = input;
        _min = min;
        _max = max;
    }

    public float Evaluate(in TerraDensityContext context)
    {
        return Mathf.Clamp(_input.Evaluate(in context), _min, _max);
    }
}

public sealed class TerraYDensityFunction : ITerraDensityFunction
{
    public float Evaluate(in TerraDensityContext context)
    {
        return context.worldY;
    }
}

public sealed class TerraVerticalRangeMaskDensityFunction : ITerraDensityFunction
{
    private readonly float _minY;
    private readonly float _maxY;
    private readonly float _fade;

    public TerraVerticalRangeMaskDensityFunction(float minY, float maxY, float fade)
    {
        _minY = minY;
        _maxY = maxY;
        _fade = Mathf.Max(0.001f, fade);
    }

    public float Evaluate(in TerraDensityContext context)
    {
        float y = context.worldY;
        if (y <= _minY - _fade || y >= _maxY + _fade)
        {
            return 0f;
        }

        if (y < _minY)
        {
            return Mathf.InverseLerp(_minY - _fade, _minY, y);
        }

        if (y > _maxY)
        {
            return Mathf.InverseLerp(_maxY + _fade, _maxY, y);
        }

        return 1f;
    }
}

public sealed class TerraShapeChannelDensityFunction : ITerraDensityFunction
{
    private readonly TerraShapeChannel _channel;

    public TerraShapeChannelDensityFunction(TerraShapeChannel channel)
    {
        _channel = channel;
    }

    public float Evaluate(in TerraDensityContext context)
    {
        return _channel switch
        {
            TerraShapeChannel.Continentalness => context.continentalness,
            TerraShapeChannel.Erosion => context.erosion,
            TerraShapeChannel.Weirdness => context.weirdness,
            TerraShapeChannel.PeaksAndValleys => context.peaksAndValleys,
            TerraShapeChannel.Offset => context.offset,
            TerraShapeChannel.Factor => context.factor,
            TerraShapeChannel.Jaggedness => context.jaggedness,
            _ => 0f,
        };
    }
}

public sealed class TerraSurfaceHeightMinusYDensityFunction : ITerraDensityFunction
{
    public float Evaluate(in TerraDensityContext context)
    {
        return context.surfaceHeight - context.worldY;
    }
}

public sealed class TerraSignedNoise3DDensityFunction : ITerraDensityFunction
{
    private readonly int _seed;
    private readonly float _scaleXZ;
    private readonly float _scaleY;
    private readonly float _amplitude;
    private readonly float _seedOffset;

    public TerraSignedNoise3DDensityFunction(int seed, float scaleXZ, float scaleY, float amplitude, float seedOffset)
    {
        _seed = seed;
        _scaleXZ = scaleXZ;
        _scaleY = scaleY;
        _amplitude = amplitude;
        _seedOffset = seedOffset;
    }

    public float Evaluate(in TerraDensityContext context)
    {
        float x = (context.worldX + (_seed * 0.137f) + _seedOffset) * _scaleXZ;
        float y = (context.worldY - (_seed * 0.071f) + (_seedOffset * 0.53f)) * _scaleY;
        float z = (context.worldZ - (_seed * 0.091f) + (_seedOffset * 1.7f)) * _scaleXZ;

        float xy = Mathf.PerlinNoise(x + 10000f, y + 10000f);
        float yz = Mathf.PerlinNoise(y + 20000f, z + 20000f);
        float xz = Mathf.PerlinNoise(x + 30000f, z + 30000f);
        float combined = (xy + yz + xz) / 3f;
        return ((combined * 2f) - 1f) * _amplitude;
    }
}

using System;
using UnityEngine;

public readonly struct SurfaceRuleContext
{
    public SurfaceRuleContext(
        int worldX,
        int worldZ,
        int worldY,
        int seaLevel,
        int preliminarySurfaceY,
        int minSurfaceLevel,
        int surfaceY,
        int waterHeight,
        int stoneDepthAbove,
        int stoneDepthBelow,
        int surfaceDepth,
        float surfaceNoise,
        float surfaceSecondary,
        bool steep)
    {
        WorldX = worldX;
        WorldZ = worldZ;
        WorldY = worldY;
        SeaLevel = seaLevel;
        PreliminarySurfaceY = preliminarySurfaceY;
        MinSurfaceLevel = minSurfaceLevel;
        SurfaceY = surfaceY;
        WaterHeight = waterHeight;
        StoneDepthAbove = stoneDepthAbove;
        StoneDepthBelow = stoneDepthBelow;
        SurfaceDepth = surfaceDepth;
        SurfaceNoise = surfaceNoise;
        SurfaceSecondary = surfaceSecondary;
        Steep = steep;
    }

    public int WorldX { get; }
    public int WorldZ { get; }
    public int WorldY { get; }
    public int SeaLevel { get; }
    public int PreliminarySurfaceY { get; }
    public int MinSurfaceLevel { get; }
    public int SurfaceY { get; }
    public int WaterHeight { get; }
    public int StoneDepthAbove { get; }
    public int StoneDepthBelow { get; }
    public int SurfaceDepth { get; }
    public float SurfaceNoise { get; }
    public float SurfaceSecondary { get; }
    public bool Steep { get; }
    public bool AbovePreliminarySurface => WorldY >= MinSurfaceLevel;

    public bool IsYAbove(int absoluteY, bool addStoneDepth, int surfaceDepthMultiplier)
    {
        return WorldY + (addStoneDepth ? StoneDepthAbove : 0)
            >= absoluteY + (SurfaceDepth * surfaceDepthMultiplier);
    }

    public bool IsWater(int offset, bool addStoneDepth, int surfaceDepthMultiplier)
    {
        return WaterHeight == int.MinValue
            || WorldY + (addStoneDepth ? StoneDepthAbove : 0)
                >= WaterHeight + offset + (SurfaceDepth * surfaceDepthMultiplier);
    }

    public bool MatchesBiome(string biomeName)
    {
        return false;
    }

    public bool IsHole()
    {
        return SurfaceDepth <= 0;
    }

    public int GetSecondaryDepthOffset(int secondaryDepthRange)
    {
        if (secondaryDepthRange == 0)
        {
            return 0;
        }

        return (int)Mathf.Lerp(0f, secondaryDepthRange, Mathf.InverseLerp(-1f, 1f, SurfaceSecondary));
    }
}

public abstract class SurfaceRule
{
    public abstract bool TryResolve(in SurfaceRuleContext context, out BlockType blockType);
}

public sealed class SurfaceRuleSequence : SurfaceRule
{
    private readonly SurfaceRule[] _rules;

    public SurfaceRuleSequence(params SurfaceRule[] rules)
    {
        _rules = rules ?? Array.Empty<SurfaceRule>();
    }

    public override bool TryResolve(in SurfaceRuleContext context, out BlockType blockType)
    {
        for (int i = 0; i < _rules.Length; i++)
        {
            if (_rules[i] != null && _rules[i].TryResolve(context, out blockType))
            {
                return true;
            }
        }

        blockType = BlockType.Air;
        return false;
    }
}

public sealed class SurfaceRuleCondition : SurfaceRule
{
    private readonly Func<SurfaceRuleContext, bool> _predicate;
    private readonly SurfaceRule _thenRule;

    public SurfaceRuleCondition(Func<SurfaceRuleContext, bool> predicate, SurfaceRule thenRule)
    {
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        _thenRule = thenRule ?? throw new ArgumentNullException(nameof(thenRule));
    }

    public override bool TryResolve(in SurfaceRuleContext context, out BlockType blockType)
    {
        if (_predicate(context))
        {
            return _thenRule.TryResolve(context, out blockType);
        }

        blockType = BlockType.Air;
        return false;
    }
}

public sealed class SurfaceRuleBlock : SurfaceRule
{
    private readonly BlockType _blockType;

    public SurfaceRuleBlock(BlockType blockType)
    {
        _blockType = blockType;
    }

    public override bool TryResolve(in SurfaceRuleContext context, out BlockType blockType)
    {
        blockType = _blockType;
        return true;
    }
}

public static class SurfaceRules
{
    public static SurfaceRule CreateVanillaLikeFloorRules(int seaLevel)
    {
        SurfaceRule topSurfaceRule = new SurfaceRuleSequence(
            new SurfaceRuleCondition(ctx => IsDeepUnderwater(ctx) && ctx.SurfaceNoise < -0.45f, new SurfaceRuleBlock(BlockType.Mud)),
            new SurfaceRuleCondition(ctx => IsDeepUnderwater(ctx) && ctx.SurfaceNoise > 0.35f, new SurfaceRuleBlock(BlockType.Clay)),
            new SurfaceRuleCondition(IsBeachBand, new SurfaceRuleBlock(BlockType.Sand)),
            new SurfaceRuleCondition(ctx => IsShallowUnderwater(ctx) && ctx.SurfaceNoise < 0f, new SurfaceRuleBlock(BlockType.Sand)),
            new SurfaceRuleCondition(IsUnderwater, new SurfaceRuleBlock(BlockType.Gravel)),
            new SurfaceRuleBlock(BlockType.Grass));

        SurfaceRule subsurfaceRule = new SurfaceRuleSequence(
            new SurfaceRuleCondition(ctx => IsDeepUnderwater(ctx) && ctx.SurfaceNoise < -0.45f, new SurfaceRuleBlock(BlockType.Mud)),
            new SurfaceRuleCondition(ctx => IsDeepUnderwater(ctx) && ctx.SurfaceNoise > 0.35f, new SurfaceRuleBlock(BlockType.Clay)),
            new SurfaceRuleCondition(IsBeachBand, new SurfaceRuleBlock(BlockType.Sand)),
            new SurfaceRuleCondition(ctx => IsShallowUnderwater(ctx) && ctx.SurfaceNoise < 0f, new SurfaceRuleBlock(BlockType.Sand)),
            new SurfaceRuleCondition(IsUnderwater, new SurfaceRuleBlock(BlockType.Gravel)),
            new SurfaceRuleBlock(BlockType.Dirt));

        return new SurfaceRuleSequence(
            new SurfaceRuleCondition(
                ctx => ctx.AbovePreliminarySurface && ctx.StoneDepthAbove <= 1,
                topSurfaceRule),
            new SurfaceRuleCondition(
                ctx => ctx.StoneDepthAbove > 1 && ctx.StoneDepthAbove <= 4,
                subsurfaceRule));
    }

    public static SurfaceRule CreateBasicFloorRules(int seaLevel)
    {
        return CreateVanillaLikeFloorRules(seaLevel);
    }

    private static bool IsBeachBand(SurfaceRuleContext context)
    {
        return context.IsYAbove(context.SeaLevel - 1, false, 0)
            && !context.IsYAbove(context.SeaLevel + 3, false, 0)
            && !context.IsWater(0, false, 0);
    }

    private static bool IsUnderwater(SurfaceRuleContext context)
    {
        return context.IsWater(1, false, 0) || context.SurfaceY < context.SeaLevel;
    }

    private static bool IsShallowUnderwater(SurfaceRuleContext context)
    {
        return context.SurfaceY >= context.SeaLevel - 6
            && context.SurfaceY < context.SeaLevel;
    }

    private static bool IsDeepUnderwater(SurfaceRuleContext context)
    {
        return context.SurfaceY < context.SeaLevel - 16;
    }
}

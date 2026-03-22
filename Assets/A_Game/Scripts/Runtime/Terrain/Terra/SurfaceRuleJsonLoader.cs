using System;
using System.Collections.Generic;
using UnityEngine;

public static class SurfaceRuleJsonLoader
{
    public static SurfaceRule Load(TextAsset jsonAsset, int worldSeed)
    {
        if (jsonAsset == null)
        {
            throw new ArgumentNullException(nameof(jsonAsset), "Surface rule JSON asset is required.");
        }

        VanillaPositionalRandom randomFactory = VanillaXoroshiroRandom.Create(worldSeed).ForkPositional();

        object raw = SplineJsonLoader.DeserializeRaw(jsonAsset.text);
        if (raw is not Dictionary<string, object> rootObject)
        {
            throw new InvalidOperationException($"Surface rule JSON '{jsonAsset.name}' must contain an object root.");
        }

        if (rootObject.TryGetValue("surface_rule", out object wrappedRule))
        {
            return ParseRule(wrappedRule, $"{jsonAsset.name}.surface_rule", randomFactory);
        }

        return ParseRule(rootObject, jsonAsset.name, randomFactory);
    }

    private static SurfaceRule ParseRule(object rawRule, string debugPath, VanillaPositionalRandom randomFactory)
    {
        if (rawRule is not Dictionary<string, object> ruleObject)
        {
            throw new InvalidOperationException($"Surface rule '{debugPath}' must be an object.");
        }

        string type = ReadRequiredString(ruleObject, "type", debugPath);
        return type switch
        {
            "minecraft:sequence" => ParseSequence(ruleObject, debugPath, randomFactory),
            "minecraft:condition" => ParseCondition(ruleObject, debugPath, randomFactory),
            "minecraft:block" => ParseBlock(ruleObject, debugPath),
            "minecraft:bandlands" => new SurfaceRuleBlock(BlockType.Rock),
            _ => throw new InvalidOperationException($"Unsupported surface rule type '{type}' at '{debugPath}'."),
        };
    }

    private static SurfaceRule ParseSequence(Dictionary<string, object> ruleObject, string debugPath, VanillaPositionalRandom randomFactory)
    {
        List<object> sequenceArray = ReadRequiredArray(ruleObject, "sequence", debugPath);
        SurfaceRule[] rules = new SurfaceRule[sequenceArray.Count];
        for (int i = 0; i < sequenceArray.Count; i++)
        {
            rules[i] = ParseRule(sequenceArray[i], $"{debugPath}.sequence[{i}]", randomFactory);
        }

        return new SurfaceRuleSequence(rules);
    }

    private static SurfaceRule ParseCondition(Dictionary<string, object> ruleObject, string debugPath, VanillaPositionalRandom randomFactory)
    {
        if (!ruleObject.TryGetValue("if_true", out object ifTrueRaw))
        {
            throw new InvalidOperationException($"Surface rule '{debugPath}' is missing if_true.");
        }

        if (!ruleObject.TryGetValue("then_run", out object thenRunRaw))
        {
            throw new InvalidOperationException($"Surface rule '{debugPath}' is missing then_run.");
        }

        Func<SurfaceRuleContext, bool> predicate = ParsePredicate(ifTrueRaw, $"{debugPath}.if_true", randomFactory);
        SurfaceRule thenRule = ParseRule(thenRunRaw, $"{debugPath}.then_run", randomFactory);
        return new SurfaceRuleCondition(predicate, thenRule);
    }

    private static SurfaceRule ParseBlock(Dictionary<string, object> ruleObject, string debugPath)
    {
        if (!ruleObject.TryGetValue("result_state", out object resultStateRaw) || resultStateRaw is not Dictionary<string, object> resultStateObject)
        {
            throw new InvalidOperationException($"Surface rule '{debugPath}' is missing result_state.");
        }

        string blockName = ReadRequiredString(resultStateObject, "Name", $"{debugPath}.result_state");
        if (string.Equals(blockName, "minecraft:bedrock", StringComparison.Ordinal))
        {
            return new SurfaceRuleCondition(
                ctx => ctx.WorldY == TerrainData.MinecraftMinY,
                new SurfaceRuleBlock(BlockType.Bedrock));
        }

        return new SurfaceRuleBlock(ParseBlockType(blockName));
    }

    private static Func<SurfaceRuleContext, bool> ParsePredicate(object rawPredicate, string debugPath, VanillaPositionalRandom randomFactory)
    {
        if (rawPredicate is not Dictionary<string, object> predicateObject)
        {
            throw new InvalidOperationException($"Surface predicate '{debugPath}' must be an object.");
        }

        string type = ReadRequiredString(predicateObject, "type", debugPath);
        return type switch
        {
            "minecraft:above_preliminary_surface" => ctx => ctx.AbovePreliminarySurface,
            "minecraft:stone_depth" => ParseStoneDepthPredicate(predicateObject),
            "minecraft:y_above" => ParseYAbovePredicate(predicateObject, debugPath),
            "minecraft:water" => ParseWaterPredicate(predicateObject, debugPath),
            "minecraft:noise_threshold" => ParseNoiseThresholdPredicate(predicateObject, debugPath),
            "minecraft:not" => ParseNotPredicate(predicateObject, debugPath, randomFactory),
            "minecraft:biome" => ParseBiomePredicate(predicateObject, debugPath),
            "minecraft:hole" => ctx => ctx.IsHole(),
            "minecraft:temperature" => ctx => false,
            "minecraft:steep" => ctx => ctx.Steep,
            "minecraft:vertical_gradient" => ParseVerticalGradientPredicate(predicateObject, debugPath, randomFactory),
            _ => throw new InvalidOperationException($"Unsupported surface predicate type '{type}' at '{debugPath}'."),
        };
    }

    private static Func<SurfaceRuleContext, bool> ParseStoneDepthPredicate(Dictionary<string, object> predicateObject)
    {
        bool addSurfaceDepth = ReadOptionalBool(predicateObject, "add_surface_depth", false);
        int offset = ReadOptionalInt(predicateObject, "offset", 0);
        int secondaryDepthRange = ReadOptionalInt(predicateObject, "secondary_depth_range", 0);
        string surfaceType = ReadRequiredString(predicateObject, "surface_type", "stone_depth");

        return ctx =>
        {
            bool isCeiling = string.Equals(surfaceType, "ceiling", StringComparison.Ordinal);
            bool isFloor = string.Equals(surfaceType, "floor", StringComparison.Ordinal);
            if (!isFloor && !isCeiling)
            {
                return false;
            }

            int stoneDepth = isCeiling ? ctx.StoneDepthBelow : ctx.StoneDepthAbove;
            int surfaceDepth = addSurfaceDepth ? ctx.SurfaceDepth : 0;
            int secondaryDepth = ctx.GetSecondaryDepthOffset(secondaryDepthRange);
            return stoneDepth <= 1 + offset + surfaceDepth + secondaryDepth;
        };
    }

    private static Func<SurfaceRuleContext, bool> ParseYAbovePredicate(Dictionary<string, object> predicateObject, string debugPath)
    {
        bool addStoneDepth = ReadOptionalBool(predicateObject, "add_stone_depth", false);
        int surfaceDepthMultiplier = ReadOptionalInt(predicateObject, "surface_depth_multiplier", 0);
        Func<SurfaceRuleContext, int> anchorResolver = ParseAnchorResolver(predicateObject, debugPath);
        return ctx => ctx.IsYAbove(anchorResolver(ctx), addStoneDepth, surfaceDepthMultiplier);
    }

    private static Func<SurfaceRuleContext, bool> ParseWaterPredicate(Dictionary<string, object> predicateObject, string debugPath)
    {
        bool addStoneDepth = ReadOptionalBool(predicateObject, "add_stone_depth", false);
        int surfaceDepthMultiplier = ReadOptionalInt(predicateObject, "surface_depth_multiplier", 0);
        int offset = ReadOptionalInt(predicateObject, "offset", 0);
        return ctx => ctx.IsWater(offset, addStoneDepth, surfaceDepthMultiplier);
    }

    private static Func<SurfaceRuleContext, bool> ParseNoiseThresholdPredicate(Dictionary<string, object> predicateObject, string debugPath)
    {
        string noiseName = ReadRequiredString(predicateObject, "noise", debugPath);
        float minThreshold = ReadOptionalFloat(predicateObject, "min_threshold", float.NegativeInfinity);
        float maxThreshold = ReadOptionalFloat(predicateObject, "max_threshold", float.PositiveInfinity);
        return ctx =>
        {
            float value = noiseName switch
            {
                "minecraft:surface" => ctx.SurfaceNoise,
                _ => float.NegativeInfinity,
            };

            return value >= minThreshold && value <= maxThreshold;
        };
    }

    private static Func<SurfaceRuleContext, bool> ParseNotPredicate(Dictionary<string, object> predicateObject, string debugPath, VanillaPositionalRandom randomFactory)
    {
        if (!predicateObject.TryGetValue("invert", out object invertRaw))
        {
            throw new InvalidOperationException($"Surface predicate '{debugPath}' is missing invert.");
        }

        Func<SurfaceRuleContext, bool> invertPredicate = ParsePredicate(invertRaw, $"{debugPath}.invert", randomFactory);
        return ctx => !invertPredicate(ctx);
    }

    private static Func<SurfaceRuleContext, bool> ParseBiomePredicate(Dictionary<string, object> predicateObject, string debugPath)
    {
        if (!predicateObject.TryGetValue("biome_is", out object biomeRaw) || biomeRaw is not List<object> biomeArray)
        {
            throw new InvalidOperationException($"Surface predicate '{debugPath}' is missing biome_is.");
        }

        string[] names = new string[biomeArray.Count];
        for (int i = 0; i < biomeArray.Count; i++)
        {
            names[i] = biomeArray[i] as string ?? string.Empty;
        }

        return ctx =>
        {
            for (int i = 0; i < names.Length; i++)
            {
                if (ctx.MatchesBiome(names[i]))
                {
                    return true;
                }
            }

            return false;
        };
    }

    private static Func<SurfaceRuleContext, int> ParseAnchorResolver(Dictionary<string, object> predicateObject, string debugPath)
    {
        if (!predicateObject.TryGetValue("anchor", out object anchorRaw) || anchorRaw is not Dictionary<string, object> anchorObject)
        {
            throw new InvalidOperationException($"Surface predicate '{debugPath}' is missing anchor.");
        }

        if (anchorObject.TryGetValue("absolute", out object absoluteValue))
        {
            int absoluteY = ConvertToInt(absoluteValue, $"{debugPath}.anchor.absolute");
            return _ => absoluteY;
        }

        if (anchorObject.TryGetValue("above_bottom", out object aboveBottomValue))
        {
            int aboveBottom = ConvertToInt(aboveBottomValue, $"{debugPath}.anchor.above_bottom");
            return _ => TerrainData.MinecraftMinY + aboveBottom;
        }

        if (anchorObject.TryGetValue("below_top", out object belowTopValue))
        {
            int belowTop = ConvertToInt(belowTopValue, $"{debugPath}.anchor.below_top");
            return _ => (TerrainData.MinecraftMaxYExclusive - 1) - belowTop;
        }

        throw new InvalidOperationException($"Surface predicate '{debugPath}' has unsupported anchor type.");
    }

    private static Func<SurfaceRuleContext, bool> ParseVerticalGradientPredicate(
        Dictionary<string, object> predicateObject,
        string debugPath,
        VanillaPositionalRandom randomFactory)
    {
        if (!predicateObject.TryGetValue("random_name", out object randomNameRaw) || randomNameRaw is not string randomName)
        {
            throw new InvalidOperationException($"Surface predicate '{debugPath}' is missing random_name.");
        }

        if (!predicateObject.TryGetValue("true_at_and_below", out object trueAtAndBelowRaw) || trueAtAndBelowRaw is not Dictionary<string, object> trueAtAndBelow)
        {
            throw new InvalidOperationException($"Surface predicate '{debugPath}' is missing true_at_and_below.");
        }

        if (!predicateObject.TryGetValue("false_at_and_above", out object falseAtAndAboveRaw) || falseAtAndAboveRaw is not Dictionary<string, object> falseAtAndAbove)
        {
            throw new InvalidOperationException($"Surface predicate '{debugPath}' is missing false_at_and_above.");
        }

        int lowerY = ResolveVerticalAnchor(trueAtAndBelow, $"{debugPath}.true_at_and_below");
        int upperY = ResolveVerticalAnchor(falseAtAndAbove, $"{debugPath}.false_at_and_above");
        VanillaPositionalRandom positional = randomFactory.FromHashOf(randomName).ForkPositional();
        return ctx =>
        {
            int y = ctx.WorldY;
            if (y <= lowerY)
            {
                return true;
            }

            if (y >= upperY)
            {
                return false;
            }

            double chance = Mathf.Lerp(1f, 0f, Mathf.InverseLerp(lowerY, upperY, y));
            return positional.At(ctx.WorldX, y, ctx.WorldZ).NextFloat() < chance;
        };
    }

    private static int ResolveVerticalAnchor(Dictionary<string, object> anchorObject, string debugPath)
    {
        if (anchorObject.TryGetValue("absolute", out object absoluteValue))
        {
            return ConvertToInt(absoluteValue, $"{debugPath}.absolute");
        }

        if (anchorObject.TryGetValue("above_bottom", out object aboveBottomValue))
        {
            return TerrainData.MinecraftMinY + ConvertToInt(aboveBottomValue, $"{debugPath}.above_bottom");
        }

        if (anchorObject.TryGetValue("below_top", out object belowTopValue))
        {
            return (TerrainData.MinecraftMaxYExclusive - 1) - ConvertToInt(belowTopValue, $"{debugPath}.below_top");
        }

        throw new InvalidOperationException($"Surface vertical gradient anchor '{debugPath}' has unsupported type.");
    }

    private static BlockType ParseBlockType(string blockName)
    {
        return blockName switch
        {
            "minecraft:air" => BlockType.Air,
            "minecraft:grass_block" => BlockType.Grass,
            "minecraft:dirt" => BlockType.Dirt,
            "minecraft:stone" => BlockType.Rock,
            "minecraft:gravel" => BlockType.Gravel,
            "minecraft:mud" => BlockType.Mud,
            "minecraft:clay" => BlockType.Clay,
            "minecraft:sand" => BlockType.Sand,
            "minecraft:sandstone" => BlockType.Sand,
            "minecraft:bedrock" => BlockType.Bedrock,
            "minecraft:water" => BlockType.Air,
            _ => BlockType.Rock,
        };
    }

    private static string ReadRequiredString(Dictionary<string, object> root, string key, string debugPath)
    {
        if (!root.TryGetValue(key, out object value) || value is not string stringValue)
        {
            throw new InvalidOperationException($"Surface rule '{debugPath}' is missing string field '{key}'.");
        }

        return stringValue;
    }

    private static List<object> ReadRequiredArray(Dictionary<string, object> root, string key, string debugPath)
    {
        if (!root.TryGetValue(key, out object value) || value is not List<object> arrayValue)
        {
            throw new InvalidOperationException($"Surface rule '{debugPath}' is missing array field '{key}'.");
        }

        return arrayValue;
    }

    private static int ReadRequiredInt(Dictionary<string, object> root, string key, string debugPath)
    {
        if (!root.TryGetValue(key, out object value))
        {
            throw new InvalidOperationException($"Surface rule '{debugPath}' is missing int field '{key}'.");
        }

        return ConvertToInt(value, $"{debugPath}.{key}");
    }

    private static int ReadOptionalInt(Dictionary<string, object> root, string key, int defaultValue)
    {
        return root.TryGetValue(key, out object value)
            ? ConvertToInt(value, key)
            : defaultValue;
    }

    private static float ReadOptionalFloat(Dictionary<string, object> root, string key, float defaultValue)
    {
        return root.TryGetValue(key, out object value)
            ? ConvertToFloat(value, key)
            : defaultValue;
    }

    private static bool ReadOptionalBool(Dictionary<string, object> root, string key, bool defaultValue)
    {
        if (!root.TryGetValue(key, out object value))
        {
            return defaultValue;
        }

        return value switch
        {
            bool boolValue => boolValue,
            _ => defaultValue,
        };
    }

    private static int ConvertToInt(object value, string debugPath)
    {
        return value switch
        {
            long longValue => (int)longValue,
            int intValue => intValue,
            double doubleValue => (int)doubleValue,
            float floatValue => (int)floatValue,
            _ => throw new InvalidOperationException($"Surface rule value '{debugPath}' must be integer-like."),
        };
    }

    private static float ConvertToFloat(object value, string debugPath)
    {
        return value switch
        {
            double doubleValue => (float)doubleValue,
            float floatValue => floatValue,
            long longValue => longValue,
            int intValue => intValue,
            _ => throw new InvalidOperationException($"Surface rule value '{debugPath}' must be numeric."),
        };
    }
}

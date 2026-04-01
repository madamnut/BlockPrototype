using System;
using System.Collections.Generic;

// Minecraft-style overworld climate routing, compressed into a smaller set of
// biome families after the climate-point nearest match is resolved.
public static class OverworldBiomeGroupClassifier
{
    private readonly struct ClimateRange
    {
        public ClimateRange(float min, float max)
        {
            Min = min;
            Max = max;
        }

        public float Min { get; }
        public float Max { get; }

        public float Distance(float value)
        {
            if (value < Min)
            {
                return Min - value;
            }

            if (value > Max)
            {
                return value - Max;
            }

            return 0f;
        }
    }

    private readonly struct ClimatePoint
    {
        public ClimatePoint(
            ClimateRange temperature,
            ClimateRange humidity,
            ClimateRange continentalness,
            ClimateRange erosion,
            ClimateRange depth,
            ClimateRange weirdness,
            float offset,
            BiomeGroupKind biomeGroup)
        {
            Temperature = temperature;
            Humidity = humidity;
            Continentalness = continentalness;
            Erosion = erosion;
            Depth = depth;
            Weirdness = weirdness;
            Offset = offset;
            BiomeGroup = biomeGroup;
        }

        public ClimateRange Temperature { get; }
        public ClimateRange Humidity { get; }
        public ClimateRange Continentalness { get; }
        public ClimateRange Erosion { get; }
        public ClimateRange Depth { get; }
        public ClimateRange Weirdness { get; }
        public float Offset { get; }
        public BiomeGroupKind BiomeGroup { get; }

        public float Fitness(float temperature, float humidity, float continentalness, float erosion, float depth, float weirdness, float offset)
        {
            float tempDistance = Temperature.Distance(temperature);
            float humidityDistance = Humidity.Distance(humidity);
            float continentalnessDistance = Continentalness.Distance(continentalness);
            float erosionDistance = Erosion.Distance(erosion);
            float depthDistance = Depth.Distance(depth);
            float weirdnessDistance = Weirdness.Distance(weirdness);
            float offsetDistance = Offset - offset;

            return (tempDistance * tempDistance) +
                   (humidityDistance * humidityDistance) +
                   (continentalnessDistance * continentalnessDistance) +
                   (erosionDistance * erosionDistance) +
                   (depthDistance * depthDistance) +
                   (weirdnessDistance * weirdnessDistance) +
                   (offsetDistance * offsetDistance);
        }
    }

    private static readonly ClimateRange FullRange = Range(-1f, 1f);
    private static readonly ClimateRange[] Temperatures =
    {
        Range(-1f, -0.45f),
        Range(-0.45f, -0.15f),
        Range(-0.15f, 0.2f),
        Range(0.2f, 0.55f),
        Range(0.55f, 1f),
    };
    private static readonly ClimateRange[] Humidities =
    {
        Range(-1f, -0.35f),
        Range(-0.35f, -0.1f),
        Range(-0.1f, 0.1f),
        Range(0.1f, 0.3f),
        Range(0.3f, 1f),
    };
    private static readonly ClimateRange[] Erosions =
    {
        Range(-1f, -0.78f),
        Range(-0.78f, -0.375f),
        Range(-0.375f, -0.2225f),
        Range(-0.2225f, 0.05f),
        Range(0.05f, 0.45f),
        Range(0.45f, 0.55f),
        Range(0.55f, 1f),
    };

    private static readonly ClimateRange FrozenRange = Temperatures[0];
    private static readonly ClimateRange UnfrozenRange = Span(Temperatures[1], Temperatures[4]);
    private static readonly ClimateRange MushroomFieldsContinentalness = Range(-1.2f, -1.05f);
    private static readonly ClimateRange DeepOceanContinentalness = Range(-1.05f, -0.455f);
    private static readonly ClimateRange OceanContinentalness = Range(-0.455f, -0.19f);
    private static readonly ClimateRange CoastContinentalness = Range(-0.19f, -0.11f);
    private static readonly ClimateRange InlandContinentalness = Range(-0.11f, 0.55f);
    private static readonly ClimateRange NearInlandContinentalness = Range(-0.11f, 0.03f);
    private static readonly ClimateRange MidInlandContinentalness = Range(0.03f, 0.3f);
    private static readonly ClimateRange FarInlandContinentalness = Range(0.3f, 1f);

    private static readonly BiomeGroupKind[][] MiddleBiomeGroups =
    {
        new[] { BiomeGroupKind.Plains, BiomeGroupKind.Plains, BiomeGroupKind.Plains, BiomeGroupKind.Taiga, BiomeGroupKind.Taiga },
        new[] { BiomeGroupKind.Plains, BiomeGroupKind.Plains, BiomeGroupKind.Forest, BiomeGroupKind.Taiga, BiomeGroupKind.Taiga },
        new[] { BiomeGroupKind.Forest, BiomeGroupKind.Plains, BiomeGroupKind.Forest, BiomeGroupKind.Forest, BiomeGroupKind.Forest },
        new[] { BiomeGroupKind.Savanna, BiomeGroupKind.Savanna, BiomeGroupKind.Forest, BiomeGroupKind.Jungle, BiomeGroupKind.Jungle },
        new[] { BiomeGroupKind.Desert, BiomeGroupKind.Desert, BiomeGroupKind.Desert, BiomeGroupKind.Desert, BiomeGroupKind.Desert },
    };

    private static readonly BiomeGroupKind?[][] MiddleBiomeVariants =
    {
        new BiomeGroupKind?[] { BiomeGroupKind.Mountain, null, BiomeGroupKind.Taiga, null, null },
        new BiomeGroupKind?[] { null, null, null, null, BiomeGroupKind.Taiga },
        new BiomeGroupKind?[] { BiomeGroupKind.Plains, null, null, BiomeGroupKind.Forest, null },
        new BiomeGroupKind?[] { null, null, BiomeGroupKind.Plains, BiomeGroupKind.Jungle, BiomeGroupKind.Jungle },
        new BiomeGroupKind?[] { null, null, null, null, null },
    };

    private static readonly BiomeGroupKind[][] PlateauBiomeGroups =
    {
        new[] { BiomeGroupKind.Plains, BiomeGroupKind.Plains, BiomeGroupKind.Plains, BiomeGroupKind.Taiga, BiomeGroupKind.Taiga },
        new[] { BiomeGroupKind.Mountain, BiomeGroupKind.Mountain, BiomeGroupKind.Forest, BiomeGroupKind.Taiga, BiomeGroupKind.Taiga },
        new[] { BiomeGroupKind.Mountain, BiomeGroupKind.Mountain, BiomeGroupKind.Mountain, BiomeGroupKind.Mountain, BiomeGroupKind.Forest },
        new[] { BiomeGroupKind.Savanna, BiomeGroupKind.Savanna, BiomeGroupKind.Forest, BiomeGroupKind.Forest, BiomeGroupKind.Jungle },
        new[] { BiomeGroupKind.Badlands, BiomeGroupKind.Badlands, BiomeGroupKind.Badlands, BiomeGroupKind.Badlands, BiomeGroupKind.Badlands },
    };

    private static readonly BiomeGroupKind?[][] PlateauBiomeVariants =
    {
        new BiomeGroupKind?[] { BiomeGroupKind.Mountain, null, null, null, null },
        new BiomeGroupKind?[] { BiomeGroupKind.Mountain, null, BiomeGroupKind.Mountain, BiomeGroupKind.Mountain, BiomeGroupKind.Taiga },
        new BiomeGroupKind?[] { BiomeGroupKind.Mountain, BiomeGroupKind.Mountain, BiomeGroupKind.Forest, BiomeGroupKind.Forest, null },
        new BiomeGroupKind?[] { null, null, null, null, null },
        new BiomeGroupKind?[] { BiomeGroupKind.Badlands, BiomeGroupKind.Badlands, null, null, null },
    };

    private static readonly BiomeGroupKind?[][] ShatteredBiomeGroups =
    {
        new BiomeGroupKind?[] { BiomeGroupKind.Mountain, BiomeGroupKind.Mountain, BiomeGroupKind.Mountain, BiomeGroupKind.Mountain, BiomeGroupKind.Mountain },
        new BiomeGroupKind?[] { BiomeGroupKind.Mountain, BiomeGroupKind.Mountain, BiomeGroupKind.Mountain, BiomeGroupKind.Mountain, BiomeGroupKind.Mountain },
        new BiomeGroupKind?[] { BiomeGroupKind.Mountain, BiomeGroupKind.Mountain, BiomeGroupKind.Mountain, BiomeGroupKind.Mountain, BiomeGroupKind.Mountain },
        new BiomeGroupKind?[] { null, null, null, null, null },
        new BiomeGroupKind?[] { null, null, null, null, null },
    };

    private static readonly ClimatePoint[] ClimatePoints = BuildClimatePoints();

    public static BiomeGroupKind Classify(float temperature, float humidity, float continentalness, float erosion, float weirdness)
    {
        float bestFitness = float.PositiveInfinity;
        BiomeGroupKind bestGroup = BiomeGroupKind.Plains;
        for (int i = 0; i < ClimatePoints.Length; i++)
        {
            ClimatePoint point = ClimatePoints[i];
            float fitness = point.Fitness(temperature, humidity, continentalness, erosion, 0f, weirdness, 0f);
            if (fitness < bestFitness)
            {
                bestFitness = fitness;
                bestGroup = point.BiomeGroup;
            }
        }

        return bestGroup;
    }

    private static ClimatePoint[] BuildClimatePoints()
    {
        List<ClimatePoint> points = new(1024);
        AddOffCoastBiomes(points);
        AddInlandBiomes(points);
        return points.ToArray();
    }

    private static void AddOffCoastBiomes(List<ClimatePoint> points)
    {
        AddSurfaceBiome(points, FullRange, FullRange, MushroomFieldsContinentalness, FullRange, FullRange, 0f, BiomeGroupKind.Special);

        for (int i = 0; i < Temperatures.Length; i++)
        {
            ClimateRange temperature = Temperatures[i];
            AddSurfaceBiome(points, temperature, FullRange, DeepOceanContinentalness, FullRange, FullRange, 0f, BiomeGroupKind.Ocean);
            AddSurfaceBiome(points, temperature, FullRange, OceanContinentalness, FullRange, FullRange, 0f, BiomeGroupKind.Ocean);
        }
    }

    private static void AddInlandBiomes(List<ClimatePoint> points)
    {
        AddMidSlice(points, Range(-1f, -0.93333334f));
        AddHighSlice(points, Range(-0.93333334f, -0.7666667f));
        AddPeaks(points, Range(-0.7666667f, -0.56666666f));
        AddHighSlice(points, Range(-0.56666666f, -0.4f));
        AddMidSlice(points, Range(-0.4f, -0.26666668f));
        AddLowSlice(points, Range(-0.26666668f, -0.05f));
        AddValleys(points, Range(-0.05f, 0.05f));
        AddLowSlice(points, Range(0.05f, 0.26666668f));
        AddMidSlice(points, Range(0.26666668f, 0.4f));
        AddHighSlice(points, Range(0.4f, 0.56666666f));
        AddPeaks(points, Range(0.56666666f, 0.7666667f));
        AddHighSlice(points, Range(0.7666667f, 0.93333334f));
        AddMidSlice(points, Range(0.93333334f, 1f));
    }

    private static void AddPeaks(List<ClimatePoint> points, ClimateRange weirdness)
    {
        for (int temperatureIndex = 0; temperatureIndex < Temperatures.Length; temperatureIndex++)
        {
            ClimateRange temperature = Temperatures[temperatureIndex];
            for (int humidityIndex = 0; humidityIndex < Humidities.Length; humidityIndex++)
            {
                ClimateRange humidity = Humidities[humidityIndex];
                BiomeGroupKind middle = PickMiddleBiome(temperatureIndex, humidityIndex, weirdness);
                BiomeGroupKind middleOrBadlands = PickMiddleBiomeOrBadlandsIfHot(temperatureIndex, humidityIndex, weirdness);
                BiomeGroupKind middleOrSlope = PickMiddleBiomeOrBadlandsIfHotOrSlopeIfCold(temperatureIndex, humidityIndex, weirdness);
                BiomeGroupKind plateau = PickPlateauBiome(temperatureIndex, humidityIndex, weirdness);
                BiomeGroupKind shattered = PickShatteredBiome(temperatureIndex, humidityIndex, weirdness);
                BiomeGroupKind windsweptShattered = MaybePickWindsweptSavannaBiome(temperatureIndex, humidityIndex, weirdness, shattered);
                BiomeGroupKind peak = PickPeakBiome(temperatureIndex, humidityIndex, weirdness);

                AddSurfaceBiome(points, temperature, humidity, Span(CoastContinentalness, FarInlandContinentalness), Erosions[0], weirdness, 0f, peak);
                AddSurfaceBiome(points, temperature, humidity, Span(CoastContinentalness, NearInlandContinentalness), Erosions[1], weirdness, 0f, middleOrSlope);
                AddSurfaceBiome(points, temperature, humidity, Span(MidInlandContinentalness, FarInlandContinentalness), Erosions[1], weirdness, 0f, peak);
                AddSurfaceBiome(points, temperature, humidity, Span(CoastContinentalness, NearInlandContinentalness), Span(Erosions[2], Erosions[3]), weirdness, 0f, middle);
                AddSurfaceBiome(points, temperature, humidity, Span(MidInlandContinentalness, FarInlandContinentalness), Erosions[2], weirdness, 0f, plateau);
                AddSurfaceBiome(points, temperature, humidity, MidInlandContinentalness, Erosions[3], weirdness, 0f, middleOrBadlands);
                AddSurfaceBiome(points, temperature, humidity, FarInlandContinentalness, Erosions[3], weirdness, 0f, plateau);
                AddSurfaceBiome(points, temperature, humidity, Span(CoastContinentalness, FarInlandContinentalness), Erosions[4], weirdness, 0f, middle);
                AddSurfaceBiome(points, temperature, humidity, Span(CoastContinentalness, NearInlandContinentalness), Erosions[5], weirdness, 0f, windsweptShattered);
                AddSurfaceBiome(points, temperature, humidity, Span(MidInlandContinentalness, FarInlandContinentalness), Erosions[5], weirdness, 0f, shattered);
                AddSurfaceBiome(points, temperature, humidity, Span(CoastContinentalness, FarInlandContinentalness), Erosions[6], weirdness, 0f, middle);
            }
        }
    }

    private static void AddHighSlice(List<ClimatePoint> points, ClimateRange weirdness)
    {
        for (int temperatureIndex = 0; temperatureIndex < Temperatures.Length; temperatureIndex++)
        {
            ClimateRange temperature = Temperatures[temperatureIndex];
            for (int humidityIndex = 0; humidityIndex < Humidities.Length; humidityIndex++)
            {
                ClimateRange humidity = Humidities[humidityIndex];
                BiomeGroupKind middle = PickMiddleBiome(temperatureIndex, humidityIndex, weirdness);
                BiomeGroupKind middleOrBadlands = PickMiddleBiomeOrBadlandsIfHot(temperatureIndex, humidityIndex, weirdness);
                BiomeGroupKind middleOrSlope = PickMiddleBiomeOrBadlandsIfHotOrSlopeIfCold(temperatureIndex, humidityIndex, weirdness);
                BiomeGroupKind plateau = PickPlateauBiome(temperatureIndex, humidityIndex, weirdness);
                BiomeGroupKind shattered = PickShatteredBiome(temperatureIndex, humidityIndex, weirdness);
                BiomeGroupKind windsweptMiddle = MaybePickWindsweptSavannaBiome(temperatureIndex, humidityIndex, weirdness, middle);
                BiomeGroupKind slope = PickSlopeBiome(temperatureIndex, humidityIndex, weirdness);
                BiomeGroupKind peak = PickPeakBiome(temperatureIndex, humidityIndex, weirdness);

                AddSurfaceBiome(points, temperature, humidity, CoastContinentalness, Span(Erosions[0], Erosions[1]), weirdness, 0f, middle);
                AddSurfaceBiome(points, temperature, humidity, NearInlandContinentalness, Erosions[0], weirdness, 0f, slope);
                AddSurfaceBiome(points, temperature, humidity, Span(MidInlandContinentalness, FarInlandContinentalness), Erosions[0], weirdness, 0f, peak);
                AddSurfaceBiome(points, temperature, humidity, NearInlandContinentalness, Erosions[1], weirdness, 0f, middleOrSlope);
                AddSurfaceBiome(points, temperature, humidity, Span(MidInlandContinentalness, FarInlandContinentalness), Erosions[1], weirdness, 0f, slope);
                AddSurfaceBiome(points, temperature, humidity, Span(CoastContinentalness, NearInlandContinentalness), Span(Erosions[2], Erosions[3]), weirdness, 0f, middle);
                AddSurfaceBiome(points, temperature, humidity, Span(MidInlandContinentalness, FarInlandContinentalness), Erosions[2], weirdness, 0f, plateau);
                AddSurfaceBiome(points, temperature, humidity, MidInlandContinentalness, Erosions[3], weirdness, 0f, middleOrBadlands);
                AddSurfaceBiome(points, temperature, humidity, FarInlandContinentalness, Erosions[3], weirdness, 0f, plateau);
                AddSurfaceBiome(points, temperature, humidity, Span(CoastContinentalness, FarInlandContinentalness), Erosions[4], weirdness, 0f, middle);
                AddSurfaceBiome(points, temperature, humidity, Span(CoastContinentalness, NearInlandContinentalness), Erosions[5], weirdness, 0f, windsweptMiddle);
                AddSurfaceBiome(points, temperature, humidity, Span(MidInlandContinentalness, FarInlandContinentalness), Erosions[5], weirdness, 0f, shattered);
                AddSurfaceBiome(points, temperature, humidity, Span(CoastContinentalness, FarInlandContinentalness), Erosions[6], weirdness, 0f, middle);
            }
        }
    }

    private static void AddMidSlice(List<ClimatePoint> points, ClimateRange weirdness)
    {
        AddSurfaceBiome(points, FullRange, FullRange, CoastContinentalness, Span(Erosions[0], Erosions[2]), weirdness, 0f, BiomeGroupKind.Coast);
        AddSurfaceBiome(points, Span(Temperatures[1], Temperatures[2]), FullRange, Span(NearInlandContinentalness, FarInlandContinentalness), Erosions[6], weirdness, 0f, BiomeGroupKind.Swamp);
        AddSurfaceBiome(points, Span(Temperatures[3], Temperatures[4]), FullRange, Span(NearInlandContinentalness, FarInlandContinentalness), Erosions[6], weirdness, 0f, BiomeGroupKind.Swamp);

        for (int temperatureIndex = 0; temperatureIndex < Temperatures.Length; temperatureIndex++)
        {
            ClimateRange temperature = Temperatures[temperatureIndex];
            for (int humidityIndex = 0; humidityIndex < Humidities.Length; humidityIndex++)
            {
                ClimateRange humidity = Humidities[humidityIndex];
                BiomeGroupKind middle = PickMiddleBiome(temperatureIndex, humidityIndex, weirdness);
                BiomeGroupKind middleOrBadlands = PickMiddleBiomeOrBadlandsIfHot(temperatureIndex, humidityIndex, weirdness);
                BiomeGroupKind middleOrSlope = PickMiddleBiomeOrBadlandsIfHotOrSlopeIfCold(temperatureIndex, humidityIndex, weirdness);
                BiomeGroupKind shattered = PickShatteredBiome(temperatureIndex, humidityIndex, weirdness);
                BiomeGroupKind plateau = PickPlateauBiome(temperatureIndex, humidityIndex, weirdness);
                BiomeGroupKind beach = PickBeachBiome(temperatureIndex, humidityIndex);
                BiomeGroupKind windsweptMiddle = MaybePickWindsweptSavannaBiome(temperatureIndex, humidityIndex, weirdness, middle);
                BiomeGroupKind shatteredCoast = PickShatteredCoastBiome(temperatureIndex, humidityIndex, weirdness);
                BiomeGroupKind slope = PickSlopeBiome(temperatureIndex, humidityIndex, weirdness);

                AddSurfaceBiome(points, temperature, humidity, Span(NearInlandContinentalness, FarInlandContinentalness), Erosions[0], weirdness, 0f, slope);
                AddSurfaceBiome(points, temperature, humidity, Span(NearInlandContinentalness, MidInlandContinentalness), Erosions[1], weirdness, 0f, middleOrSlope);
                AddSurfaceBiome(points, temperature, humidity, FarInlandContinentalness, Erosions[1], weirdness, 0f, temperatureIndex == 0 ? slope : plateau);
                AddSurfaceBiome(points, temperature, humidity, NearInlandContinentalness, Erosions[2], weirdness, 0f, middle);
                AddSurfaceBiome(points, temperature, humidity, MidInlandContinentalness, Erosions[2], weirdness, 0f, middleOrBadlands);
                AddSurfaceBiome(points, temperature, humidity, FarInlandContinentalness, Erosions[2], weirdness, 0f, plateau);
                AddSurfaceBiome(points, temperature, humidity, Span(CoastContinentalness, NearInlandContinentalness), Erosions[3], weirdness, 0f, middle);
                AddSurfaceBiome(points, temperature, humidity, Span(MidInlandContinentalness, FarInlandContinentalness), Erosions[3], weirdness, 0f, middleOrBadlands);

                if (weirdness.Max < 0f)
                {
                    AddSurfaceBiome(points, temperature, humidity, CoastContinentalness, Erosions[4], weirdness, 0f, beach);
                    AddSurfaceBiome(points, temperature, humidity, Span(NearInlandContinentalness, FarInlandContinentalness), Erosions[4], weirdness, 0f, middle);
                }
                else
                {
                    AddSurfaceBiome(points, temperature, humidity, Span(CoastContinentalness, FarInlandContinentalness), Erosions[4], weirdness, 0f, middle);
                }

                AddSurfaceBiome(points, temperature, humidity, CoastContinentalness, Erosions[5], weirdness, 0f, shatteredCoast);
                AddSurfaceBiome(points, temperature, humidity, NearInlandContinentalness, Erosions[5], weirdness, 0f, windsweptMiddle);
                AddSurfaceBiome(points, temperature, humidity, Span(MidInlandContinentalness, FarInlandContinentalness), Erosions[5], weirdness, 0f, shattered);

                if (weirdness.Max < 0f)
                {
                    AddSurfaceBiome(points, temperature, humidity, CoastContinentalness, Erosions[6], weirdness, 0f, beach);
                }
                else
                {
                    AddSurfaceBiome(points, temperature, humidity, CoastContinentalness, Erosions[6], weirdness, 0f, middle);
                }

                if (temperatureIndex == 0)
                {
                    AddSurfaceBiome(points, temperature, humidity, Span(NearInlandContinentalness, FarInlandContinentalness), Erosions[6], weirdness, 0f, middle);
                }
            }
        }
    }

    private static void AddLowSlice(List<ClimatePoint> points, ClimateRange weirdness)
    {
        AddSurfaceBiome(points, FullRange, FullRange, CoastContinentalness, Span(Erosions[0], Erosions[2]), weirdness, 0f, BiomeGroupKind.Coast);
        AddSurfaceBiome(points, Span(Temperatures[1], Temperatures[2]), FullRange, Span(NearInlandContinentalness, FarInlandContinentalness), Erosions[6], weirdness, 0f, BiomeGroupKind.Swamp);
        AddSurfaceBiome(points, Span(Temperatures[3], Temperatures[4]), FullRange, Span(NearInlandContinentalness, FarInlandContinentalness), Erosions[6], weirdness, 0f, BiomeGroupKind.Swamp);

        for (int temperatureIndex = 0; temperatureIndex < Temperatures.Length; temperatureIndex++)
        {
            ClimateRange temperature = Temperatures[temperatureIndex];
            for (int humidityIndex = 0; humidityIndex < Humidities.Length; humidityIndex++)
            {
                ClimateRange humidity = Humidities[humidityIndex];
                BiomeGroupKind middle = PickMiddleBiome(temperatureIndex, humidityIndex, weirdness);
                BiomeGroupKind middleOrBadlands = PickMiddleBiomeOrBadlandsIfHot(temperatureIndex, humidityIndex, weirdness);
                BiomeGroupKind middleOrSlope = PickMiddleBiomeOrBadlandsIfHotOrSlopeIfCold(temperatureIndex, humidityIndex, weirdness);
                BiomeGroupKind beach = PickBeachBiome(temperatureIndex, humidityIndex);
                BiomeGroupKind windsweptMiddle = MaybePickWindsweptSavannaBiome(temperatureIndex, humidityIndex, weirdness, middle);
                BiomeGroupKind shatteredCoast = PickShatteredCoastBiome(temperatureIndex, humidityIndex, weirdness);

                AddSurfaceBiome(points, temperature, humidity, NearInlandContinentalness, Span(Erosions[0], Erosions[1]), weirdness, 0f, middleOrBadlands);
                AddSurfaceBiome(points, temperature, humidity, Span(MidInlandContinentalness, FarInlandContinentalness), Span(Erosions[0], Erosions[1]), weirdness, 0f, middleOrSlope);
                AddSurfaceBiome(points, temperature, humidity, NearInlandContinentalness, Span(Erosions[2], Erosions[3]), weirdness, 0f, middle);
                AddSurfaceBiome(points, temperature, humidity, Span(MidInlandContinentalness, FarInlandContinentalness), Span(Erosions[2], Erosions[3]), weirdness, 0f, middleOrBadlands);
                AddSurfaceBiome(points, temperature, humidity, CoastContinentalness, Span(Erosions[3], Erosions[4]), weirdness, 0f, beach);
                AddSurfaceBiome(points, temperature, humidity, Span(NearInlandContinentalness, FarInlandContinentalness), Erosions[4], weirdness, 0f, middle);
                AddSurfaceBiome(points, temperature, humidity, CoastContinentalness, Erosions[5], weirdness, 0f, shatteredCoast);
                AddSurfaceBiome(points, temperature, humidity, NearInlandContinentalness, Erosions[5], weirdness, 0f, windsweptMiddle);
                AddSurfaceBiome(points, temperature, humidity, Span(MidInlandContinentalness, FarInlandContinentalness), Erosions[5], weirdness, 0f, middle);
                AddSurfaceBiome(points, temperature, humidity, CoastContinentalness, Erosions[6], weirdness, 0f, beach);

                if (temperatureIndex == 0)
                {
                    AddSurfaceBiome(points, temperature, humidity, Span(NearInlandContinentalness, FarInlandContinentalness), Erosions[6], weirdness, 0f, middle);
                }
            }
        }
    }

    private static void AddValleys(List<ClimatePoint> points, ClimateRange weirdness)
    {
        AddSurfaceBiome(points, FrozenRange, FullRange, CoastContinentalness, Span(Erosions[0], Erosions[1]), weirdness, 0f, weirdness.Max < 0f ? BiomeGroupKind.Coast : BiomeGroupKind.River);
        AddSurfaceBiome(points, UnfrozenRange, FullRange, CoastContinentalness, Span(Erosions[0], Erosions[1]), weirdness, 0f, weirdness.Max < 0f ? BiomeGroupKind.Coast : BiomeGroupKind.River);
        AddSurfaceBiome(points, FrozenRange, FullRange, NearInlandContinentalness, Span(Erosions[0], Erosions[1]), weirdness, 0f, BiomeGroupKind.River);
        AddSurfaceBiome(points, UnfrozenRange, FullRange, NearInlandContinentalness, Span(Erosions[0], Erosions[1]), weirdness, 0f, BiomeGroupKind.River);
        AddSurfaceBiome(points, FrozenRange, FullRange, Span(CoastContinentalness, FarInlandContinentalness), Span(Erosions[2], Erosions[5]), weirdness, 0f, BiomeGroupKind.River);
        AddSurfaceBiome(points, UnfrozenRange, FullRange, Span(CoastContinentalness, FarInlandContinentalness), Span(Erosions[2], Erosions[5]), weirdness, 0f, BiomeGroupKind.River);
        AddSurfaceBiome(points, FrozenRange, FullRange, CoastContinentalness, Erosions[6], weirdness, 0f, BiomeGroupKind.River);
        AddSurfaceBiome(points, UnfrozenRange, FullRange, CoastContinentalness, Erosions[6], weirdness, 0f, BiomeGroupKind.River);
        AddSurfaceBiome(points, Span(Temperatures[1], Temperatures[2]), FullRange, Span(InlandContinentalness, FarInlandContinentalness), Erosions[6], weirdness, 0f, BiomeGroupKind.Swamp);
        AddSurfaceBiome(points, Span(Temperatures[3], Temperatures[4]), FullRange, Span(InlandContinentalness, FarInlandContinentalness), Erosions[6], weirdness, 0f, BiomeGroupKind.Swamp);
        AddSurfaceBiome(points, FrozenRange, FullRange, Span(InlandContinentalness, FarInlandContinentalness), Erosions[6], weirdness, 0f, BiomeGroupKind.River);

        for (int temperatureIndex = 0; temperatureIndex < Temperatures.Length; temperatureIndex++)
        {
            ClimateRange temperature = Temperatures[temperatureIndex];
            for (int humidityIndex = 0; humidityIndex < Humidities.Length; humidityIndex++)
            {
                ClimateRange humidity = Humidities[humidityIndex];
                BiomeGroupKind middleOrBadlands = PickMiddleBiomeOrBadlandsIfHot(temperatureIndex, humidityIndex, weirdness);
                AddSurfaceBiome(points, temperature, humidity, Span(MidInlandContinentalness, FarInlandContinentalness), Span(Erosions[0], Erosions[1]), weirdness, 0f, middleOrBadlands);
            }
        }
    }

    private static void AddSurfaceBiome(
        List<ClimatePoint> points,
        ClimateRange temperature,
        ClimateRange humidity,
        ClimateRange continentalness,
        ClimateRange erosion,
        ClimateRange weirdness,
        float offset,
        BiomeGroupKind group)
    {
        points.Add(new ClimatePoint(temperature, humidity, continentalness, erosion, Point(0f), weirdness, offset, group));
        points.Add(new ClimatePoint(temperature, humidity, continentalness, erosion, Point(1f), weirdness, offset, group));
    }

    private static BiomeGroupKind PickMiddleBiome(int temperatureIndex, int humidityIndex, ClimateRange weirdness)
    {
        if (weirdness.Max < 0f)
        {
            return MiddleBiomeGroups[temperatureIndex][humidityIndex];
        }

        BiomeGroupKind? variant = MiddleBiomeVariants[temperatureIndex][humidityIndex];
        return variant ?? MiddleBiomeGroups[temperatureIndex][humidityIndex];
    }

    private static BiomeGroupKind PickMiddleBiomeOrBadlandsIfHot(int temperatureIndex, int humidityIndex, ClimateRange weirdness)
    {
        return temperatureIndex == 4 ? PickBadlandsBiome(humidityIndex, weirdness) : PickMiddleBiome(temperatureIndex, humidityIndex, weirdness);
    }

    private static BiomeGroupKind PickMiddleBiomeOrBadlandsIfHotOrSlopeIfCold(int temperatureIndex, int humidityIndex, ClimateRange weirdness)
    {
        return temperatureIndex == 0 ? PickSlopeBiome(temperatureIndex, humidityIndex, weirdness) : PickMiddleBiomeOrBadlandsIfHot(temperatureIndex, humidityIndex, weirdness);
    }

    private static BiomeGroupKind MaybePickWindsweptSavannaBiome(int temperatureIndex, int humidityIndex, ClimateRange weirdness, BiomeGroupKind fallback)
    {
        return temperatureIndex > 1 && humidityIndex < 4 && weirdness.Max >= 0f ? BiomeGroupKind.Savanna : fallback;
    }

    private static BiomeGroupKind PickShatteredCoastBiome(int temperatureIndex, int humidityIndex, ClimateRange weirdness)
    {
        BiomeGroupKind fallback = weirdness.Max >= 0f ? PickMiddleBiome(temperatureIndex, humidityIndex, weirdness) : PickBeachBiome(temperatureIndex, humidityIndex);
        return MaybePickWindsweptSavannaBiome(temperatureIndex, humidityIndex, weirdness, fallback);
    }

    private static BiomeGroupKind PickBeachBiome(int temperatureIndex, int humidityIndex)
    {
        if (temperatureIndex == 0)
        {
            return BiomeGroupKind.Coast;
        }

        return temperatureIndex == 4 ? BiomeGroupKind.Desert : BiomeGroupKind.Coast;
    }

    private static BiomeGroupKind PickBadlandsBiome(int humidityIndex, ClimateRange weirdness)
    {
        return BiomeGroupKind.Badlands;
    }

    private static BiomeGroupKind PickPlateauBiome(int temperatureIndex, int humidityIndex, ClimateRange weirdness)
    {
        if (weirdness.Max >= 0f)
        {
            BiomeGroupKind? variant = PlateauBiomeVariants[temperatureIndex][humidityIndex];
            if (variant.HasValue)
            {
                return variant.Value;
            }
        }

        return PlateauBiomeGroups[temperatureIndex][humidityIndex];
    }

    private static BiomeGroupKind PickPeakBiome(int temperatureIndex, int humidityIndex, ClimateRange weirdness)
    {
        if (temperatureIndex <= 2)
        {
            return BiomeGroupKind.Mountain;
        }

        return temperatureIndex == 3 ? BiomeGroupKind.Mountain : PickBadlandsBiome(humidityIndex, weirdness);
    }

    private static BiomeGroupKind PickSlopeBiome(int temperatureIndex, int humidityIndex, ClimateRange weirdness)
    {
        if (temperatureIndex >= 3)
        {
            return PickPlateauBiome(temperatureIndex, humidityIndex, weirdness);
        }

        return BiomeGroupKind.Mountain;
    }

    private static BiomeGroupKind PickShatteredBiome(int temperatureIndex, int humidityIndex, ClimateRange weirdness)
    {
        BiomeGroupKind? shattered = ShatteredBiomeGroups[temperatureIndex][humidityIndex];
        return shattered ?? PickMiddleBiome(temperatureIndex, humidityIndex, weirdness);
    }

    private static ClimateRange Point(float value)
    {
        return new ClimateRange(value, value);
    }

    private static ClimateRange Range(float min, float max)
    {
        return new ClimateRange(min, max);
    }

    private static ClimateRange Span(ClimateRange minRange, ClimateRange maxRange)
    {
        return new ClimateRange(minRange.Min, maxRange.Max);
    }
}

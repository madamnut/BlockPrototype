using Unity.Collections;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ContinentalnessCdfProfileAsset))]
public sealed class ContinentalnessCdfProfileAssetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        ContinentalnessCdfProfileAsset asset = (ContinentalnessCdfProfileAsset)target;
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(asset.BakedSummary, MessageType.Info);

        using (new EditorGUI.DisabledScope(asset.SourceSettingsAsset == null))
        {
            if (GUILayout.Button("Bake CDF LUTs"))
            {
                Bake(asset);
            }
        }

        if (asset.SourceSettingsAsset == null)
        {
            EditorGUILayout.HelpBox("Assign a WorldGenSettingsAsset to bake a CDF LUT.", MessageType.Warning);
        }
    }

    private static void Bake(ContinentalnessCdfProfileAsset asset)
    {
        const int sampleBatchSize = 32768;
        WorldGenSettingsAsset settingsAsset = asset.SourceSettingsAsset;
        if (settingsAsset == null)
        {
            Debug.LogError("[ContinentalnessCdfProfile] Bake failed. Source settings asset is not assigned.");
            return;
        }

        ContinentalnessSettings settings = settingsAsset.ToSettings();
        ErosionSettings erosionSettings = settingsAsset.ToErosionSettings();
        RidgesSettings ridgesSettings = settingsAsset.ToRidgesSettings();
        TemperatureSettings temperatureSettings = settingsAsset.ToTemperatureSettings();
        PrecipitationSettings precipitationSettings = settingsAsset.ToPrecipitationSettings();
        int sampleCount = asset.SampleCount;
        int lutResolution = asset.LutResolution;
        int sectorRange = asset.SectorRange;
        NativeArray<float> continentalnessSamples = new NativeArray<float>(sampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> erosionSamples = new NativeArray<float>(sampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> ridgesSamples = new NativeArray<float>(sampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> temperatureSamples = new NativeArray<float>(sampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> precipitationSamples = new NativeArray<float>(sampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> continentalnessStats = new NativeArray<float>(3, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> erosionStats = new NativeArray<float>(3, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> ridgesStats = new NativeArray<float>(3, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> temperatureStats = new NativeArray<float>(3, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> precipitationStats = new NativeArray<float>(3, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        try
        {
            bool completedContinentalness = BakeSamples(
                "Continentalness",
                sampleBatchSize,
                sampleCount,
                lutResolution,
                continentalnessSamples,
                continentalnessStats,
                (slice, startIndex, batchCount) => new WorldGenPrototypeJobs.RawContinentalnessSampleJob
                {
                    sampleSeed = asset.BakeRandomSeed + startIndex,
                    sectorRange = sectorRange,
                    settings = settings,
                    samples = slice,
                    startIndex = startIndex,
                }.Schedule(batchCount, 128),
                out float[] continentalnessLut,
                out float continentalnessRawMin,
                out float continentalnessRawAverage,
                out float continentalnessRawMax);
            if (!completedContinentalness)
            {
                Debug.LogWarning("[ContinentalnessCdfProfile] Bake canceled.");
                return;
            }

            bool completedErosion = BakeSamples(
                "Erosion",
                sampleBatchSize,
                sampleCount,
                lutResolution,
                erosionSamples,
                erosionStats,
                (slice, startIndex, batchCount) => new WorldGenPrototypeJobs.RawErosionSampleJob
                {
                    sampleSeed = asset.BakeRandomSeed + startIndex,
                    sectorRange = sectorRange,
                    settings = erosionSettings,
                    samples = slice,
                    startIndex = startIndex,
                }.Schedule(batchCount, 128),
                out float[] erosionLut,
                out float erosionRawMin,
                out float erosionRawAverage,
                out float erosionRawMax);
            if (!completedErosion)
            {
                Debug.LogWarning("[ContinentalnessCdfProfile] Bake canceled.");
                return;
            }

            bool completedRidges = BakeSamples(
                "Peaks/Ridges",
                sampleBatchSize,
                sampleCount,
                lutResolution,
                ridgesSamples,
                ridgesStats,
                (slice, startIndex, batchCount) => new WorldGenPrototypeJobs.RawRidgesSampleJob
                {
                    sampleSeed = asset.BakeRandomSeed + startIndex,
                    sectorRange = sectorRange,
                    settings = ridgesSettings,
                    samples = slice,
                    startIndex = startIndex,
                }.Schedule(batchCount, 128),
                out float[] ridgesLut,
                out float ridgesRawMin,
                out float ridgesRawAverage,
                out float ridgesRawMax);
            if (!completedRidges)
            {
                Debug.LogWarning("[ContinentalnessCdfProfile] Bake canceled.");
                return;
            }

            bool completedTemperature = BakeSamples(
                "Temperature",
                sampleBatchSize,
                sampleCount,
                lutResolution,
                temperatureSamples,
                temperatureStats,
                (slice, startIndex, batchCount) => new WorldGenPrototypeJobs.RawTemperatureSampleJob
                {
                    sampleSeed = asset.BakeRandomSeed + startIndex,
                    sectorRange = sectorRange,
                    settings = temperatureSettings,
                    samples = slice,
                    startIndex = startIndex,
                }.Schedule(batchCount, 128),
                out float[] temperatureLut,
                out float temperatureRawMin,
                out float temperatureRawAverage,
                out float temperatureRawMax);
            if (!completedTemperature)
            {
                Debug.LogWarning("[ContinentalnessCdfProfile] Bake canceled.");
                return;
            }

            bool completedPrecipitation = BakeSamples(
                "Precipitation",
                sampleBatchSize,
                sampleCount,
                lutResolution,
                precipitationSamples,
                precipitationStats,
                (slice, startIndex, batchCount) => new WorldGenPrototypeJobs.RawPrecipitationSampleJob
                {
                    sampleSeed = asset.BakeRandomSeed + startIndex,
                    sectorRange = sectorRange,
                    settings = precipitationSettings,
                    samples = slice,
                    startIndex = startIndex,
                }.Schedule(batchCount, 128),
                out float[] precipitationLut,
                out float precipitationRawMin,
                out float precipitationRawAverage,
                out float precipitationRawMax);
            if (!completedPrecipitation)
            {
                Debug.LogWarning("[ContinentalnessCdfProfile] Bake canceled.");
                return;
            }

            asset.StoreBakedLuts(
                continentalnessLut,
                sampleCount,
                continentalnessRawMin,
                continentalnessRawAverage,
                continentalnessRawMax,
                erosionLut,
                sampleCount,
                erosionRawMin,
                erosionRawAverage,
                erosionRawMax,
                ridgesLut,
                sampleCount,
                ridgesRawMin,
                ridgesRawAverage,
                ridgesRawMax,
                temperatureLut,
                sampleCount,
                temperatureRawMin,
                temperatureRawAverage,
                temperatureRawMax,
                precipitationLut,
                sampleCount,
                precipitationRawMin,
                precipitationRawAverage,
                precipitationRawMax);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ContinentalnessCdfProfile] Baked LUTs.\n{asset.BakedSummary}");
        }
        finally
        {
            if (continentalnessSamples.IsCreated)
            {
                continentalnessSamples.Dispose();
            }

            if (erosionSamples.IsCreated)
            {
                erosionSamples.Dispose();
            }

            if (ridgesSamples.IsCreated)
            {
                ridgesSamples.Dispose();
            }

            if (temperatureSamples.IsCreated)
            {
                temperatureSamples.Dispose();
            }

            if (precipitationSamples.IsCreated)
            {
                precipitationSamples.Dispose();
            }

            if (continentalnessStats.IsCreated)
            {
                continentalnessStats.Dispose();
            }

            if (erosionStats.IsCreated)
            {
                erosionStats.Dispose();
            }

            if (ridgesStats.IsCreated)
            {
                ridgesStats.Dispose();
            }

            if (temperatureStats.IsCreated)
            {
                temperatureStats.Dispose();
            }

            if (precipitationStats.IsCreated)
            {
                precipitationStats.Dispose();
            }

            EditorUtility.ClearProgressBar();
        }
    }

    private delegate JobHandle ScheduleBatchDelegate(NativeSlice<float> slice, int startIndex, int batchCount);

    private static bool BakeSamples(
        string label,
        int sampleBatchSize,
        int sampleCount,
        int lutResolution,
        NativeArray<float> samples,
        NativeArray<float> stats,
        ScheduleBatchDelegate scheduleBatch,
        out float[] lut,
        out float rawMin,
        out float rawAverage,
        out float rawMax)
    {
        int sampledCount = 0;
        while (sampledCount < sampleCount)
        {
            int currentBatchCount = Mathf.Min(sampleBatchSize, sampleCount - sampledCount);
            NativeSlice<float> batchSlice = new NativeSlice<float>(samples, sampledCount, currentBatchCount);
            float progress = sampledCount / (float)sampleCount;
            if (EditorUtility.DisplayCancelableProgressBar(
                    "Bake Noise CDF",
                    $"Sampling raw {label.ToLowerInvariant()} {sampledCount}/{sampleCount}",
                    progress * 0.4f))
            {
                lut = null;
                rawMin = 0f;
                rawAverage = 0f;
                rawMax = 0f;
                return false;
            }

            JobHandle sampleHandle = scheduleBatch(batchSlice, sampledCount, currentBatchCount);
            sampleHandle.Complete();
            sampledCount += currentBatchCount;
        }

        EditorUtility.DisplayProgressBar("Bake Noise CDF", $"Calculating raw {label.ToLowerInvariant()} stats {sampleCount}/{sampleCount}", 0.45f);
        JobHandle statsHandle = new WorldGenPrototypeJobs.FloatStatsJob
        {
            values = samples,
            stats = stats,
        }.Schedule();
        statsHandle.Complete();

        EditorUtility.DisplayProgressBar("Bake Noise CDF", $"Sorting raw {label.ToLowerInvariant()} samples {sampleCount}/{sampleCount}", 0.5f);
        NativeSortExtension.Sort(samples);

        rawMin = stats[0];
        rawMax = stats[1];
        rawAverage = stats[2];
        lut = new float[lutResolution];
        int cursor = 0;

        for (int i = 0; i < lutResolution; i++)
        {
            if ((i & 31) == 0)
            {
                float progress = 0.5f + ((i / (float)lutResolution) * 0.5f);
                EditorUtility.DisplayProgressBar("Bake Noise CDF", $"Building {label.ToLowerInvariant()} CDF LUT {i}/{lutResolution}", progress);
            }

            float raw = i / (float)(lutResolution - 1);
            while (cursor < samples.Length && samples[cursor] <= raw)
            {
                cursor++;
            }

            lut[i] = cursor / (float)samples.Length;
        }

        return true;
    }
}

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
            if (GUILayout.Button("Bake CDF LUT"))
            {
                Bake(asset);
            }
        }

        if (asset.SourceSettingsAsset == null)
        {
            EditorGUILayout.HelpBox("Assign a ContinentalnessSettingsAsset to bake a CDF LUT.", MessageType.Warning);
        }
    }

    private static void Bake(ContinentalnessCdfProfileAsset asset)
    {
        const int sampleBatchSize = 32768;
        ContinentalnessSettingsAsset settingsAsset = asset.SourceSettingsAsset;
        if (settingsAsset == null)
        {
            Debug.LogError("[ContinentalnessCdfProfile] Bake failed. Source settings asset is not assigned.");
            return;
        }

        ContinentalnessSettings settings = settingsAsset.ToSettings();
        int sampleCount = asset.SampleCount;
        int lutResolution = asset.LutResolution;
        int sectorRange = asset.SectorRange;
        NativeArray<float> samples = new NativeArray<float>(sampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> stats = new NativeArray<float>(3, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        try
        {
            int sampledCount = 0;
            while (sampledCount < sampleCount)
            {
                int currentBatchCount = Mathf.Min(sampleBatchSize, sampleCount - sampledCount);
                NativeSlice<float> batchSlice = new NativeSlice<float>(samples, sampledCount, currentBatchCount);
                float progress = sampledCount / (float)sampleCount;
                if (EditorUtility.DisplayCancelableProgressBar(
                        "Bake Continentalness CDF",
                        $"Sampling raw continentalness {sampledCount}/{sampleCount}",
                        progress))
                {
                    Debug.LogWarning("[ContinentalnessCdfProfile] Bake canceled.");
                    return;
                }

                var sampleHandle = new WorldGenPrototypeJobs.RawContinentalnessSampleJob
                {
                    sampleSeed = asset.BakeRandomSeed + sampledCount,
                    sectorRange = sectorRange,
                    settings = settings,
                    samples = batchSlice,
                    startIndex = sampledCount,
                }.Schedule(currentBatchCount, 128);

                sampleHandle.Complete();
                sampledCount += currentBatchCount;
            }

            EditorUtility.DisplayProgressBar("Bake Continentalness CDF", $"Calculating raw sample stats {sampleCount}/{sampleCount}", 0.7f);
            var statsHandle = new WorldGenPrototypeJobs.FloatStatsJob
            {
                values = samples,
                stats = stats,
            }.Schedule();

            statsHandle.Complete();

            EditorUtility.DisplayProgressBar("Bake Continentalness CDF", $"Sorting raw continentalness samples {sampleCount}/{sampleCount}", 0.8f);
            NativeSortExtension.Sort(samples);

            float rawMin = stats[0];
            float rawMax = stats[1];
            float rawAverage = stats[2];
            float[] lut = new float[lutResolution];
            int cursor = 0;

            for (int i = 0; i < lutResolution; i++)
            {
                if ((i & 31) == 0)
                {
                    float progress = 0.8f + ((i / (float)lutResolution) * 0.2f);
                    EditorUtility.DisplayProgressBar("Bake Continentalness CDF", $"Building CDF LUT {i}/{lutResolution}", progress);
                }

                float raw = i / (float)(lutResolution - 1);
                while (cursor < samples.Length && samples[cursor] <= raw)
                {
                    cursor++;
                }

                lut[i] = cursor / (float)samples.Length;
            }

            asset.StoreBakedLut(lut, sampleCount, rawMin, rawAverage, rawMax);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ContinentalnessCdfProfile] Baked LUT. {asset.BakedSummary}");
        }
        finally
        {
            if (samples.IsCreated)
            {
                samples.Dispose();
            }

            if (stats.IsCreated)
            {
                stats.Dispose();
            }

            EditorUtility.ClearProgressBar();
        }
    }
}

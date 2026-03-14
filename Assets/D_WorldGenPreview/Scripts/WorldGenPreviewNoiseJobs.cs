using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

[BurstCompile]
public static class WorldGenPreviewNoiseJobs
{
    [BurstCompile]
    public struct WorldSpacePerlinContinentalnessColorJob : IJobParallelFor
    {
        public int size;
        public int seed;
        public float blocksPerPixel;
        public ContinentalnessSettings settings;
        public float oceanDeepR;
        public float oceanDeepG;
        public float oceanDeepB;
        public float oceanShallowR;
        public float oceanShallowG;
        public float oceanShallowB;
        public float landLowR;
        public float landLowG;
        public float landLowB;
        public float landHighR;
        public float landHighG;
        public float landHighB;

        [WriteOnly] public NativeArray<Color32> pixels;

        public void Execute(int index)
        {
            int x = index % size;
            int y = index / size;
            float worldBlockX = (x + 0.5f) * blocksPerPixel;
            float worldBlockZ = (y + 0.5f) * blocksPerPixel;
            float worldRegionX = worldBlockX / WorldGenPrototypeJobs.RegionSizeInBlocks;
            float worldRegionZ = worldBlockZ / WorldGenPrototypeJobs.RegionSizeInBlocks;
            float clampedValue = WorldGenPrototypeJobs.SampleContinentalness(seed, worldRegionX, worldRegionZ, settings);

            float3 color;
            if (clampedValue < 0f)
            {
                float ocean01 = saturate(clampedValue + 1f);
                color = lerp(
                    float3(oceanDeepR, oceanDeepG, oceanDeepB),
                    float3(oceanShallowR, oceanShallowG, oceanShallowB),
                    ocean01);
            }
            else
            {
                float land01 = saturate(clampedValue);
                color = lerp(
                    float3(landLowR, landLowG, landLowB),
                    float3(landHighR, landHighG, landHighB),
                    land01);
            }

            pixels[index] = new Color32(
                (byte)round(saturate(color.x) * 255f),
                (byte)round(saturate(color.y) * 255f),
                (byte)round(saturate(color.z) * 255f),
                255);
        }
    }
}

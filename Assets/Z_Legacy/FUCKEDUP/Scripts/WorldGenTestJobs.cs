using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

[BurstCompile]
public static class WorldGenTestJobs
{
    private const int Phase1Size = 16;
    private const int Phase2Size = 32;
    private const int Phase64Size = 64;
    private const int Phase32Size = 32;
    [BurstCompile]
    public struct PhasePreviewColorJob : IJobParallelFor
    {
        public int size;
        public int mode;
        public int seed;
        public int clusterIndexX;
        public int clusterIndexZ;
        public float landChance;
        public Color32 landColor;
        public Color32 seaColor;

        [WriteOnly] public NativeArray<Color32> pixels;

        public void Execute(int index)
        {
            int x = index % size;
            int z = index / size;

            bool isLand = mode switch
            {
                14 => SamplePhase14CellIsLand(seed, clusterIndexX, clusterIndexZ, size, x, z, landChance),
                13 => SamplePhase13CellIsLand(seed, clusterIndexX, clusterIndexZ, size, x, z, landChance),
                12 => SamplePhase12CellIsLand(seed, clusterIndexX, clusterIndexZ, size, x, z, landChance),
                11 => SamplePhase11CellIsLand(seed, clusterIndexX, clusterIndexZ, size, x, z, landChance),
                10 => SamplePhase10CellIsLand(seed, clusterIndexX, clusterIndexZ, size, x, z, landChance),
                9 => SamplePhase9CellIsLand(seed, clusterIndexX, clusterIndexZ, size, x, z, landChance),
                8 => SamplePhase8CellIsLand(seed, clusterIndexX, clusterIndexZ, size, x, z, landChance),
                7 => SamplePhase7CellIsLand(seed, clusterIndexX, clusterIndexZ, size, x, z, landChance),
                6 => SamplePhase6CellIsLand(seed, clusterIndexX, clusterIndexZ, size, x, z, landChance),
                5 => SamplePhase5CellIsLand(seed, clusterIndexX, clusterIndexZ, size, x, z, landChance),
                4 => SamplePhase4CellIsLand(seed, clusterIndexX, clusterIndexZ, size, x, z, landChance),
                3 => SamplePhase3CellIsLand(seed, clusterIndexX, clusterIndexZ, size, x, z, landChance),
                2 => SamplePhase2CellIsLand(seed, clusterIndexX, clusterIndexZ, size, x, z, landChance),
                _ => SamplePhase1CellIsLand(seed, clusterIndexX, clusterIndexZ, size, x, z, landChance),
            };

            pixels[index] = isLand ? landColor : seaColor;
        }
    }

    [BurstCompile]
    public struct GridOverlayJob : IJobParallelFor
    {
        public int width;
        public int height;
        public int divisionCount;
        public int lineWidthX;
        public int lineWidthY;
        public Color32 lineColor;

        [WriteOnly] public NativeArray<Color32> pixels;

        public void Execute(int index)
        {
            int x = index % width;
            int y = index / width;
            bool onVertical = IsOnGridLine(x, width, divisionCount, lineWidthX);
            bool onHorizontal = IsOnGridLine(y, height, divisionCount, lineWidthY);
            pixels[index] = onVertical || onHorizontal ? lineColor : default;
        }
    }

    private static bool SamplePhase1CellIsLand(int seed, int clusterIndexX, int clusterIndexZ, int size, int localX, int localZ, float landChance)
    {
        int worldSectorX = (clusterIndexX * size) + localX;
        int worldSectorZ = (clusterIndexZ * size) + localZ;
        return SamplePhase1IsLand(seed, worldSectorX, worldSectorZ, landChance);
    }

    private static bool SamplePhase2CellIsLand(int seed, int clusterIndexX, int clusterIndexZ, int size, int localX, int localZ, float landChance)
    {
        int worldCellX = (clusterIndexX * size) + localX;
        int worldCellZ = (clusterIndexZ * size) + localZ;
        return SamplePhase2CellIsLandWorld(seed, worldCellX, worldCellZ, landChance);
    }

    private static bool SamplePhase3CellIsLand(int seed, int clusterIndexX, int clusterIndexZ, int size, int localX, int localZ, float landChance)
    {
        int worldCellX = (clusterIndexX * size) + localX;
        int worldCellZ = (clusterIndexZ * size) + localZ;
        return SamplePhase3CellIsLandWorld(seed, worldCellX, worldCellZ, landChance);
    }

    private static bool SamplePhase4CellIsLand(int seed, int clusterIndexX, int clusterIndexZ, int size, int localX, int localZ, float landChance)
    {
        int worldCellX = (clusterIndexX * size) + localX;
        int worldCellZ = (clusterIndexZ * size) + localZ;
        return SamplePhase4CellIsLandWorld(seed, worldCellX, worldCellZ, landChance);
    }

    private static bool SamplePhase5CellIsLand(int seed, int clusterIndexX, int clusterIndexZ, int size, int localX, int localZ, float landChance)
    {
        int worldCellX = (clusterIndexX * size) + localX;
        int worldCellZ = (clusterIndexZ * size) + localZ;
        return SamplePhase5CellIsLandWorld(seed, worldCellX, worldCellZ, landChance);
    }

    private static bool SamplePhase6CellIsLand(int seed, int clusterIndexX, int clusterIndexZ, int size, int localX, int localZ, float landChance)
    {
        int worldCellX = (clusterIndexX * size) + localX;
        int worldCellZ = (clusterIndexZ * size) + localZ;
        return SamplePhase6CellIsLandWorld(seed, worldCellX, worldCellZ, landChance);
    }

    private static bool SamplePhase7CellIsLand(int seed, int clusterIndexX, int clusterIndexZ, int size, int localX, int localZ, float landChance)
    {
        int worldCellX = (clusterIndexX * size) + localX;
        int worldCellZ = (clusterIndexZ * size) + localZ;
        return SamplePhase7CellIsLandWorld(seed, worldCellX, worldCellZ, landChance);
    }

    private static bool SamplePhase8CellIsLand(int seed, int clusterIndexX, int clusterIndexZ, int size, int localX, int localZ, float landChance)
    {
        int worldCellX = (clusterIndexX * size) + localX;
        int worldCellZ = (clusterIndexZ * size) + localZ;
        return SamplePhase8CellIsLandWorld(seed, worldCellX, worldCellZ, landChance);
    }

    private static bool SamplePhase9CellIsLand(int seed, int clusterIndexX, int clusterIndexZ, int size, int localX, int localZ, float landChance)
    {
        int worldCellX = (clusterIndexX * size) + localX;
        int worldCellZ = (clusterIndexZ * size) + localZ;
        return SamplePhase9CellIsLandWorld(seed, worldCellX, worldCellZ, landChance);
    }

    private static bool SamplePhase10CellIsLand(int seed, int clusterIndexX, int clusterIndexZ, int size, int localX, int localZ, float landChance)
    {
        int worldCellX = (clusterIndexX * size) + localX;
        int worldCellZ = (clusterIndexZ * size) + localZ;
        return SamplePhase10CellIsLandWorld(seed, worldCellX, worldCellZ, landChance);
    }

    private static bool SamplePhase11CellIsLand(int seed, int clusterIndexX, int clusterIndexZ, int size, int localX, int localZ, float landChance)
    {
        int worldCellX = (clusterIndexX * size) + localX;
        int worldCellZ = (clusterIndexZ * size) + localZ;
        return SamplePhase11CellIsLandWorld(seed, worldCellX, worldCellZ, landChance);
    }

    private static bool SamplePhase12CellIsLand(int seed, int clusterIndexX, int clusterIndexZ, int size, int localX, int localZ, float landChance)
    {
        int worldCellX = (clusterIndexX * size) + localX;
        int worldCellZ = (clusterIndexZ * size) + localZ;
        return SamplePhase12CellIsLandWorld(seed, worldCellX, worldCellZ, landChance);
    }

    private static bool SamplePhase13CellIsLand(int seed, int clusterIndexX, int clusterIndexZ, int size, int localX, int localZ, float landChance)
    {
        int worldCellX = (clusterIndexX * size) + localX;
        int worldCellZ = (clusterIndexZ * size) + localZ;
        return SamplePhase13CellIsLandWorld(seed, worldCellX, worldCellZ, landChance);
    }

    private static bool SamplePhase14CellIsLand(int seed, int clusterIndexX, int clusterIndexZ, int size, int localX, int localZ, float landChance)
    {
        int worldCellX = (clusterIndexX * size) + localX;
        int worldCellZ = (clusterIndexZ * size) + localZ;
        return SamplePhase14CellIsLandWorld(seed, worldCellX, worldCellZ, landChance);
    }

    private static bool SamplePhase2CellIsLandWorld(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        if (SamplePhase2BaseCell(seed, worldCellX, worldCellZ, landChance))
        {
            return true;
        }

        if (!IsAllSeaAroundPhase2Base(seed, worldCellX, worldCellZ, landChance))
        {
            return false;
        }

        return SampleChance50(seed, worldCellX, worldCellZ, 0x24D3A511u);
    }

    private static bool SamplePhase3CellIsLandWorld(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        if (!SamplePhase3BaseCell(seed, worldCellX, worldCellZ, landChance))
        {
            return false;
        }

        if (CountSeaNeighborsPhase3Base(seed, worldCellX, worldCellZ, landChance) < 2)
        {
            return true;
        }

        return !SampleChance50(seed, worldCellX, worldCellZ, 0x9E3779B9u);
    }

    private static bool SamplePhase4CellIsLandWorld(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        if (SamplePhase4BaseCell(seed, worldCellX, worldCellZ, landChance))
        {
            return true;
        }

        if (CountLandNeighborsPhase4Base(seed, worldCellX, worldCellZ, landChance) < 2)
        {
            return false;
        }

        return SampleChance50(seed, worldCellX, worldCellZ, 0x85EBCA6Bu);
    }

    private static bool SamplePhase5CellIsLandWorld(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        if (SamplePhase5BaseCell(seed, worldCellX, worldCellZ, landChance))
        {
            return true;
        }

        if (CountLandNeighborsPhase5Base(seed, worldCellX, worldCellZ, landChance) < 2)
        {
            return false;
        }

        return SampleChance50(seed, worldCellX, worldCellZ, 0xD1B54A35u);
    }

    private static bool SamplePhase6CellIsLandWorld(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        if (!SamplePhase6BaseCell(seed, worldCellX, worldCellZ, landChance))
        {
            return false;
        }

        if (CountSeaNeighborsPhase6Base(seed, worldCellX, worldCellZ, landChance) != 1)
        {
            return true;
        }

        return !SampleChance25(seed, worldCellX, worldCellZ, 0xA24BAED4u);
    }

    private static bool SamplePhase7CellIsLandWorld(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        if (SamplePhase7BaseCell(seed, worldCellX, worldCellZ, landChance))
        {
            return true;
        }

        if (CountLandNeighborsPhase7Base(seed, worldCellX, worldCellZ, landChance) != 1)
        {
            return false;
        }

        return SampleChance25(seed, worldCellX, worldCellZ, 0xB7E15162u);
    }

    private static bool SamplePhase8CellIsLandWorld(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        int clusterIndexX = FloorDiv(worldCellX, Phase32Size);
        int clusterIndexZ = FloorDiv(worldCellZ, Phase32Size);
        int localCellX = worldCellX - (clusterIndexX * Phase32Size);
        int localCellZ = worldCellZ - (clusterIndexZ * Phase32Size);
        int worldPhase7CellX = (clusterIndexX * Phase64Size) + localCellX;
        int worldPhase7CellZ = (clusterIndexZ * Phase64Size) + localCellZ;
        return SamplePhase7CellIsLandWorld(seed, worldPhase7CellX, worldPhase7CellZ, landChance);
    }

    private static bool SamplePhase9CellIsLandWorld(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        if (!SamplePhase9BaseCell(seed, worldCellX, worldCellZ, landChance))
        {
            return false;
        }

        if (CountSeaNeighborsPhase9Base(seed, worldCellX, worldCellZ, landChance) != 2)
        {
            return true;
        }

        return false;
    }

    private static bool SamplePhase10CellIsLandWorld(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        if (SamplePhase10BaseCell(seed, worldCellX, worldCellZ, landChance))
        {
            return true;
        }

        if (CountLandNeighborsPhase10Base(seed, worldCellX, worldCellZ, landChance) != 2)
        {
            return false;
        }

        return true;
    }

    private static bool SamplePhase11CellIsLandWorld(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        if (SamplePhase11BaseCell(seed, worldCellX, worldCellZ, landChance))
        {
            return true;
        }

        if (CountLandNeighborsPhase11Base(seed, worldCellX, worldCellZ, landChance) != 1)
        {
            return false;
        }

        return SampleChance25(seed, worldCellX, worldCellZ, 0x7FEB352Du);
    }

    private static bool SamplePhase12CellIsLandWorld(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        int clusterIndexX = FloorDiv(worldCellX, Phase32Size);
        int clusterIndexZ = FloorDiv(worldCellZ, Phase32Size);
        int localCellX = worldCellX - (clusterIndexX * Phase32Size);
        int localCellZ = worldCellZ - (clusterIndexZ * Phase32Size);
        int worldPhase11CellX = (clusterIndexX * Phase64Size) + localCellX;
        int worldPhase11CellZ = (clusterIndexZ * Phase64Size) + localCellZ;
        return SamplePhase11CellIsLandWorld(seed, worldPhase11CellX, worldPhase11CellZ, landChance);
    }

    private static bool SamplePhase13CellIsLandWorld(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        if (!SamplePhase13BaseCell(seed, worldCellX, worldCellZ, landChance))
        {
            return false;
        }

        if (CountSeaNeighborsPhase13Base(seed, worldCellX, worldCellZ, landChance) < 2)
        {
            return true;
        }

        return !SampleChance50(seed, worldCellX, worldCellZ, 0x165667B1u);
    }

    private static bool SamplePhase14CellIsLandWorld(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        return SamplePhase14BaseCell(seed, worldCellX, worldCellZ, landChance);
    }

    private static bool SamplePhase13BaseCell(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        int clusterIndexX = FloorDiv(worldCellX, Phase64Size);
        int clusterIndexZ = FloorDiv(worldCellZ, Phase64Size);
        int localCellX = worldCellX - (clusterIndexX * Phase64Size);
        int localCellZ = worldCellZ - (clusterIndexZ * Phase64Size);
        int worldPhase12CellX = (clusterIndexX * Phase32Size) + FloorDiv(localCellX, 2);
        int worldPhase12CellZ = (clusterIndexZ * Phase32Size) + FloorDiv(localCellZ, 2);
        return SamplePhase12CellIsLandWorld(seed, worldPhase12CellX, worldPhase12CellZ, landChance);
    }

    private static bool SamplePhase14BaseCell(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        return SamplePhase13CellIsLandWorld(seed, worldCellX, worldCellZ, landChance);
    }

    private static bool SamplePhase2BaseCell(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        int worldSectorX = FloorDiv(worldCellX, 2);
        int worldSectorZ = FloorDiv(worldCellZ, 2);
        return SamplePhase1IsLand(seed, worldSectorX, worldSectorZ, landChance);
    }

    private static bool SamplePhase3BaseCell(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        int worldPhase2CellX = FloorDiv(worldCellX, 2);
        int worldPhase2CellZ = FloorDiv(worldCellZ, 2);
        return SamplePhase2CellIsLandWorld(seed, worldPhase2CellX, worldPhase2CellZ, landChance);
    }

    private static bool SamplePhase4BaseCell(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        return SamplePhase3CellIsLandWorld(seed, worldCellX, worldCellZ, landChance);
    }

    private static bool SamplePhase5BaseCell(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        return SamplePhase4CellIsLandWorld(seed, worldCellX, worldCellZ, landChance);
    }

    private static bool SamplePhase6BaseCell(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        return SamplePhase5CellIsLandWorld(seed, worldCellX, worldCellZ, landChance);
    }

    private static bool SamplePhase7BaseCell(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        return SamplePhase6CellIsLandWorld(seed, worldCellX, worldCellZ, landChance);
    }

    private static bool SamplePhase9BaseCell(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        int clusterIndexX = FloorDiv(worldCellX, Phase64Size);
        int clusterIndexZ = FloorDiv(worldCellZ, Phase64Size);
        int localCellX = worldCellX - (clusterIndexX * Phase64Size);
        int localCellZ = worldCellZ - (clusterIndexZ * Phase64Size);
        int worldPhase8CellX = (clusterIndexX * Phase32Size) + FloorDiv(localCellX, 2);
        int worldPhase8CellZ = (clusterIndexZ * Phase32Size) + FloorDiv(localCellZ, 2);
        return SamplePhase8CellIsLandWorld(seed, worldPhase8CellX, worldPhase8CellZ, landChance);
    }

    private static bool SamplePhase10BaseCell(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        return SamplePhase9CellIsLandWorld(seed, worldCellX, worldCellZ, landChance);
    }

    private static bool SamplePhase11BaseCell(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        return SamplePhase10CellIsLandWorld(seed, worldCellX, worldCellZ, landChance);
    }

    private static int CountLandNeighborsPhase4Base(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        int count = 0;
        if (SamplePhase4BaseCell(seed, worldCellX, worldCellZ - 1, landChance))
        {
            count++;
        }

        if (SamplePhase4BaseCell(seed, worldCellX, worldCellZ + 1, landChance))
        {
            count++;
        }

        if (SamplePhase4BaseCell(seed, worldCellX - 1, worldCellZ, landChance))
        {
            count++;
        }

        if (SamplePhase4BaseCell(seed, worldCellX + 1, worldCellZ, landChance))
        {
            count++;
        }

        return count;
    }

    private static int CountLandNeighborsPhase5Base(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        int count = 0;
        if (SamplePhase5BaseCell(seed, worldCellX, worldCellZ - 1, landChance))
        {
            count++;
        }

        if (SamplePhase5BaseCell(seed, worldCellX, worldCellZ + 1, landChance))
        {
            count++;
        }

        if (SamplePhase5BaseCell(seed, worldCellX - 1, worldCellZ, landChance))
        {
            count++;
        }

        if (SamplePhase5BaseCell(seed, worldCellX + 1, worldCellZ, landChance))
        {
            count++;
        }

        return count;
    }

    private static int CountLandNeighborsPhase7Base(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        int count = 0;
        if (SamplePhase7BaseCell(seed, worldCellX, worldCellZ - 1, landChance))
        {
            count++;
        }

        if (SamplePhase7BaseCell(seed, worldCellX, worldCellZ + 1, landChance))
        {
            count++;
        }

        if (SamplePhase7BaseCell(seed, worldCellX - 1, worldCellZ, landChance))
        {
            count++;
        }

        if (SamplePhase7BaseCell(seed, worldCellX + 1, worldCellZ, landChance))
        {
            count++;
        }

        return count;
    }

    private static int CountLandNeighborsPhase11Base(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        int count = 0;
        if (SamplePhase11BaseCell(seed, worldCellX, worldCellZ - 1, landChance))
        {
            count++;
        }

        if (SamplePhase11BaseCell(seed, worldCellX, worldCellZ + 1, landChance))
        {
            count++;
        }

        if (SamplePhase11BaseCell(seed, worldCellX - 1, worldCellZ, landChance))
        {
            count++;
        }

        if (SamplePhase11BaseCell(seed, worldCellX + 1, worldCellZ, landChance))
        {
            count++;
        }

        return count;
    }

    private static int CountLandNeighborsPhase10Base(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        int count = 0;
        if (SamplePhase10BaseCell(seed, worldCellX, worldCellZ - 1, landChance))
        {
            count++;
        }

        if (SamplePhase10BaseCell(seed, worldCellX, worldCellZ + 1, landChance))
        {
            count++;
        }

        if (SamplePhase10BaseCell(seed, worldCellX - 1, worldCellZ, landChance))
        {
            count++;
        }

        if (SamplePhase10BaseCell(seed, worldCellX + 1, worldCellZ, landChance))
        {
            count++;
        }

        return count;
    }

    private static int CountSeaNeighborsPhase13Base(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        int count = 0;
        if (!SamplePhase13BaseCell(seed, worldCellX, worldCellZ - 1, landChance))
        {
            count++;
        }

        if (!SamplePhase13BaseCell(seed, worldCellX, worldCellZ + 1, landChance))
        {
            count++;
        }

        if (!SamplePhase13BaseCell(seed, worldCellX - 1, worldCellZ, landChance))
        {
            count++;
        }

        if (!SamplePhase13BaseCell(seed, worldCellX + 1, worldCellZ, landChance))
        {
            count++;
        }

        return count;
    }

    private static int CountSeaNeighborsPhase3Base(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        int count = 0;
        if (!SamplePhase3BaseCell(seed, worldCellX, worldCellZ - 1, landChance))
        {
            count++;
        }

        if (!SamplePhase3BaseCell(seed, worldCellX, worldCellZ + 1, landChance))
        {
            count++;
        }

        if (!SamplePhase3BaseCell(seed, worldCellX - 1, worldCellZ, landChance))
        {
            count++;
        }

        if (!SamplePhase3BaseCell(seed, worldCellX + 1, worldCellZ, landChance))
        {
            count++;
        }

        return count;
    }

    private static int CountSeaNeighborsPhase6Base(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        int count = 0;
        if (!SamplePhase6BaseCell(seed, worldCellX, worldCellZ - 1, landChance))
        {
            count++;
        }

        if (!SamplePhase6BaseCell(seed, worldCellX, worldCellZ + 1, landChance))
        {
            count++;
        }

        if (!SamplePhase6BaseCell(seed, worldCellX - 1, worldCellZ, landChance))
        {
            count++;
        }

        if (!SamplePhase6BaseCell(seed, worldCellX + 1, worldCellZ, landChance))
        {
            count++;
        }

        return count;
    }

    private static int CountSeaNeighborsPhase9Base(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        int count = 0;
        if (!SamplePhase9BaseCell(seed, worldCellX, worldCellZ - 1, landChance))
        {
            count++;
        }

        if (!SamplePhase9BaseCell(seed, worldCellX, worldCellZ + 1, landChance))
        {
            count++;
        }

        if (!SamplePhase9BaseCell(seed, worldCellX - 1, worldCellZ, landChance))
        {
            count++;
        }

        if (!SamplePhase9BaseCell(seed, worldCellX + 1, worldCellZ, landChance))
        {
            count++;
        }

        return count;
    }

    private static int CountSeaNeighborsPhase10Base(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        int count = 0;
        if (!SamplePhase10BaseCell(seed, worldCellX, worldCellZ - 1, landChance))
        {
            count++;
        }

        if (!SamplePhase10BaseCell(seed, worldCellX, worldCellZ + 1, landChance))
        {
            count++;
        }

        if (!SamplePhase10BaseCell(seed, worldCellX - 1, worldCellZ, landChance))
        {
            count++;
        }

        if (!SamplePhase10BaseCell(seed, worldCellX + 1, worldCellZ, landChance))
        {
            count++;
        }

        return count;
    }

    private static bool IsAllSeaAroundPhase2Base(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        return !SamplePhase2BaseCell(seed, worldCellX, worldCellZ - 1, landChance)
            && !SamplePhase2BaseCell(seed, worldCellX, worldCellZ + 1, landChance)
            && !SamplePhase2BaseCell(seed, worldCellX - 1, worldCellZ, landChance)
            && !SamplePhase2BaseCell(seed, worldCellX + 1, worldCellZ, landChance);
    }

    private static bool IsAllSeaAroundPhase11Base(int seed, int worldCellX, int worldCellZ, float landChance)
    {
        return !SamplePhase11BaseCell(seed, worldCellX, worldCellZ - 1, landChance)
            && !SamplePhase11BaseCell(seed, worldCellX, worldCellZ + 1, landChance)
            && !SamplePhase11BaseCell(seed, worldCellX - 1, worldCellZ, landChance)
            && !SamplePhase11BaseCell(seed, worldCellX + 1, worldCellZ, landChance);
    }

    private static bool SamplePhase1IsLand(int seed, int worldSectorX, int worldSectorZ, float landChance)
    {
        unchecked
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)seed) * 16777619u;
            hash = (hash ^ (uint)worldSectorX) * 16777619u;
            hash = (hash ^ (uint)worldSectorZ) * 16777619u;
            hash ^= hash >> 13;
            hash *= 1274126177u;
            hash ^= hash >> 16;

            float normalized = hash / (float)uint.MaxValue;
            return normalized < landChance;
        }
    }

    private static bool SampleChance50(int seed, int worldCellX, int worldCellZ, uint salt)
    {
        return (ComputeHash(seed, worldCellX, worldCellZ, salt) & 1u) == 0u;
    }

    private static bool SampleChance25(int seed, int worldCellX, int worldCellZ, uint salt)
    {
        return (ComputeHash(seed, worldCellX, worldCellZ, salt) & 3u) == 0u;
    }

    private static uint ComputeHash(int seed, int worldCellX, int worldCellZ, uint salt)
    {
        unchecked
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)seed) * 16777619u;
            hash = (hash ^ (uint)worldCellX) * 16777619u;
            hash = (hash ^ (uint)worldCellZ) * 16777619u;
            hash = (hash ^ salt) * 16777619u;
            hash ^= hash >> 16;
            hash *= 2246822519u;
            hash ^= hash >> 13;
            return hash;
        }
    }

    private static bool IsOnGridLine(int position, int max, int divisions, int lineWidth)
    {
        int safeDivisions = divisions < 1 ? 1 : divisions;
        for (int division = 0; division <= safeDivisions; division++)
        {
            int lineStart = (division * max) / safeDivisions;
            if (lineStart >= max)
            {
                lineStart = max - 1;
            }

            if (position >= lineStart && position < lineStart + lineWidth)
            {
                return true;
            }
        }

        return false;
    }

    private static int FloorDiv(int value, int divisor)
    {
        int quotient = value / divisor;
        int remainder = value % divisor;
        if (remainder != 0 && ((remainder < 0) != (divisor < 0)))
        {
            quotient--;
        }

        return quotient;
    }
}

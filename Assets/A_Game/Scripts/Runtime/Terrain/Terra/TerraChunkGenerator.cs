using UnityEngine;

public sealed class TerraChunkGenerator
{
    private readonly int _seaLevel;
    private readonly TerraContinentalnessSampler _continentalnessSampler;
    private readonly TerraOffsetMapper _offsetMapper;
    private readonly TerraFactorMapper _factorMapper;

    public TerraChunkGenerator(int seaLevel, TerraContinentalnessSampler continentalnessSampler, TerraOffsetMapper offsetMapper, TerraFactorMapper factorMapper)
    {
        _seaLevel = Mathf.Clamp(seaLevel, 0, TerrainData.WorldHeight - 1);
        _continentalnessSampler = continentalnessSampler ?? throw new System.ArgumentNullException(nameof(continentalnessSampler));
        _offsetMapper = offsetMapper ?? throw new System.ArgumentNullException(nameof(offsetMapper));
        _factorMapper = factorMapper ?? throw new System.ArgumentNullException(nameof(factorMapper));
    }

    public int GenerateChunkColumn(int chunkX, int chunkZ, BlockType[] blocks, VoxelFluid[] fluids, int[] columnHeights)
    {
        int maxHeight = -1;

        for (int localZ = 0; localZ < TerrainData.ChunkSize; localZ++)
        {
            for (int localX = 0; localX < TerrainData.ChunkSize; localX++)
            {
                int worldX = (chunkX * TerrainData.ChunkSize) + localX;
                int worldZ = (chunkZ * TerrainData.ChunkSize) + localZ;

                // Sample continentalness once per column and reuse it for the full Y stack.
                float continentalness = _continentalnessSampler.Sample(worldX, worldZ);
                float offset = _offsetMapper.Map(continentalness);
                float factor = _factorMapper.Map(continentalness);
                int columnIndex = (localZ * TerrainData.ChunkSize) + localX;
                int highestSolid = FillSolidColumn(blocks, localX, localZ, offset, factor);
                columnHeights[columnIndex] = highestSolid;

                PaintSurfaceLayers(blocks, localX, localZ, highestSolid);
                FillWaterColumn(blocks, fluids, localX, localZ, highestSolid);

                maxHeight = Mathf.Max(maxHeight, Mathf.Max(highestSolid, _seaLevel));
            }
        }

        return maxHeight;
    }

    private int FillSolidColumn(BlockType[] blocks, int localX, int localZ, float offset, float factor)
    {
        int highestSolid = -1;
        for (int worldY = 0; worldY < TerrainData.WorldHeight; worldY++)
        {
            float density = GetDensity(worldY, offset, factor);
            if (density <= 0f)
            {
                continue;
            }

            int index = GetIndex(localX, worldY, localZ);
            blocks[index] = BlockType.Rock;
            highestSolid = worldY;
        }

        return highestSolid;
    }

    private void FillWaterColumn(BlockType[] blocks, VoxelFluid[] fluids, int localX, int localZ, int surfaceHeight)
    {
        int waterStart = Mathf.Max(surfaceHeight + 1, 0);
        for (int worldY = waterStart; worldY <= _seaLevel; worldY++)
        {
            int index = GetIndex(localX, worldY, localZ);
            if (blocks[index] == BlockType.Air)
            {
                fluids[index] = VoxelFluid.Water(100);
            }
        }
    }

    private static void PaintSurfaceLayers(BlockType[] blocks, int localX, int localZ, int highestSolid)
    {
        if (highestSolid < 0)
        {
            return;
        }

        int soilDepth = 0;
        for (int worldY = highestSolid; worldY >= 0; worldY--)
        {
            int index = GetIndex(localX, worldY, localZ);
            if (blocks[index] == BlockType.Air)
            {
                if (soilDepth > 0)
                {
                    break;
                }

                continue;
            }

            if (soilDepth == 0)
            {
                blocks[index] = BlockType.Grass;
            }
            else if (soilDepth <= 3)
            {
                blocks[index] = BlockType.Dirt;
            }
            else
            {
                break;
            }

            soilDepth++;
        }
    }

    private static int GetIndex(int localX, int worldY, int localZ)
    {
        return ((worldY * TerrainData.ChunkSize) + localZ) * TerrainData.ChunkSize + localX;
    }

    private float GetDensity(int worldY, float offset, float factor)
    {
        float yGradient = _seaLevel - worldY;
        float depth = yGradient + offset;
        return depth * factor;
    }
}

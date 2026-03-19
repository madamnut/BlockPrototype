using UnityEngine;

public sealed class TerraChunkGenerator
{
    private readonly int _seaLevel;
    private readonly TerraDensitySampler _densitySampler;

    public TerraChunkGenerator(int seed, int seaLevel, TerraNoiseSettings noiseSettings)
    {
        _seaLevel = Mathf.Clamp(seaLevel, 0, TerrainData.WorldHeight - 1);
        TerraHeightSampler heightSampler = new(seed, _seaLevel, noiseSettings);
        _densitySampler = new TerraDensitySampler(seed, _seaLevel, noiseSettings, heightSampler, TerraDensitySettings.Default);
    }

    public TerraSurfaceSample SampleSurface(int worldX, int worldZ)
    {
        return _densitySampler.SampleSurface(worldX, worldZ);
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
                TerraSurfaceSample surface = _densitySampler.SampleSurface(worldX, worldZ);
                int columnIndex = (localZ * TerrainData.ChunkSize) + localX;
                int highestSolid = -1;

                for (int worldY = 0; worldY < TerrainData.WorldHeight; worldY++)
                {
                    float density = _densitySampler.SampleDensity(surface, worldX, worldY, worldZ);
                    if (density <= 0f)
                    {
                        continue;
                    }

                    int index = GetIndex(localX, worldY, localZ);
                    blocks[index] = BlockType.Rock;
                    highestSolid = worldY;
                }

                columnHeights[columnIndex] = highestSolid;
                PaintSurfaceLayers(blocks, localX, localZ, highestSolid);

                int waterStart = Mathf.Max(highestSolid + 1, 0);
                for (int worldY = waterStart; worldY <= _seaLevel; worldY++)
                {
                    int index = GetIndex(localX, worldY, localZ);
                    if (blocks[index] == BlockType.Air)
                    {
                        fluids[index] = VoxelFluid.Water(100);
                    }
                }

                maxHeight = Mathf.Max(maxHeight, Mathf.Max(highestSolid, _seaLevel));
            }
        }

        return maxHeight;
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
}

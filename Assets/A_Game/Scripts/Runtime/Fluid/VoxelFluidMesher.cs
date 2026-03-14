using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class VoxelFluidMesher
{
    private const float SurfaceHeightScale = 0.9f;

    private sealed class MeshBuildBuffers
    {
        public readonly List<Vector3> vertices = new(256);
        public readonly List<int> indices = new(384);
        public readonly List<Vector2> uvs = new(256);
        public readonly List<Vector3> normals = new(256);
        public readonly List<Color32> colors = new(256);

        public void Clear()
        {
            vertices.Clear();
            indices.Clear();
            uvs.Clear();
            normals.Clear();
            colors.Clear();
        }
    }

    [System.ThreadStatic] private static MeshBuildBuffers s_buffers;

    public static bool RebuildSubChunkMesh(Mesh mesh, TerrainData terrain, int chunkX, int subChunkY, int chunkZ)
    {
        if (mesh == null)
        {
            return false;
        }

        MeshBuildBuffers buffers = GetBuffers();
        buffers.Clear();

        int startX = chunkX * TerrainData.ChunkSize;
        int startY = subChunkY * TerrainData.SubChunkSize;
        int startZ = chunkZ * TerrainData.ChunkSize;

        for (int localY = 0; localY < TerrainData.SubChunkSize; localY++)
        {
            int worldY = startY + localY;
            for (int localZ = 0; localZ < TerrainData.ChunkSize; localZ++)
            {
                int worldZ = startZ + localZ;
                for (int localX = 0; localX < TerrainData.ChunkSize; localX++)
                {
                    int worldX = startX + localX;
                    VoxelFluid fluid = terrain.GetFluid(worldX, worldY, worldZ);
                    if (!fluid.Exists)
                    {
                        continue;
                    }

                    float height = GetFluidHeight(fluid);
                    if (height <= 0f)
                    {
                        continue;
                    }

                    AddFluidCell(buffers, terrain, worldX, worldY, worldZ, localX, localY, localZ, height);
                }
            }
        }

        mesh.Clear();
        if (buffers.vertices.Count == 0)
        {
            return false;
        }

        mesh.name = $"FluidSubChunk_{chunkX}_{subChunkY}_{chunkZ}";
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(buffers.vertices);
        mesh.SetTriangles(buffers.indices, 0, true);
        mesh.SetUVs(0, buffers.uvs);
        mesh.SetNormals(buffers.normals);
        mesh.SetColors(buffers.colors);
        mesh.RecalculateBounds();
        mesh.UploadMeshData(false);
        return true;
    }

    private static void AddFluidCell(
        MeshBuildBuffers buffers,
        TerrainData terrain,
        int worldX,
        int worldY,
        int worldZ,
        int localX,
        int localY,
        int localZ,
        float height)
    {
        bool topBlocked = IsTopBlocked(terrain, worldX, worldY, worldZ);
        float renderedHeight = GetRenderedFluidHeight(height, topBlocked);
        float neighborHeightLeft = GetVisibleNeighborHeight(terrain, worldX - 1, worldY, worldZ);
        float neighborHeightRight = GetVisibleNeighborHeight(terrain, worldX + 1, worldY, worldZ);
        float neighborHeightBack = GetVisibleNeighborHeight(terrain, worldX, worldY, worldZ - 1);
        float neighborHeightFront = GetVisibleNeighborHeight(terrain, worldX, worldY, worldZ + 1);

        Vector3 basePosition = new(localX, localY, localZ);
        if (!topBlocked)
        {
            AddTopFace(buffers, basePosition, renderedHeight);
        }

        if (neighborHeightLeft < renderedHeight)
        {
            AddSideFace(buffers, basePosition, 0, neighborHeightLeft, renderedHeight);
        }

        if (neighborHeightRight < renderedHeight)
        {
            AddSideFace(buffers, basePosition, 1, neighborHeightRight, renderedHeight);
        }

        if (neighborHeightBack < renderedHeight)
        {
            AddSideFace(buffers, basePosition, 2, neighborHeightBack, renderedHeight);
        }

        if (neighborHeightFront < renderedHeight)
        {
            AddSideFace(buffers, basePosition, 3, neighborHeightFront, renderedHeight);
        }
    }

    private static float GetVisibleNeighborHeight(TerrainData terrain, int worldX, int worldY, int worldZ)
    {
        if (terrain.GetBlock(worldX, worldY, worldZ) != BlockType.Air)
        {
            return 1f;
        }

        VoxelFluid neighbor = terrain.GetFluid(worldX, worldY, worldZ);
        if (!neighbor.Exists)
        {
            return 0f;
        }

        float neighborHeight = GetFluidHeight(neighbor);
        return GetRenderedFluidHeight(neighborHeight, IsTopBlocked(terrain, worldX, worldY, worldZ));
    }

    private static float GetFluidHeight(VoxelFluid fluid)
    {
        return Mathf.Clamp01(fluid.amount / 100f);
    }

    private static bool IsTopBlocked(TerrainData terrain, int worldX, int worldY, int worldZ)
    {
        return terrain.GetBlock(worldX, worldY + 1, worldZ) != BlockType.Air || terrain.GetFluid(worldX, worldY + 1, worldZ).Exists;
    }

    private static float GetRenderedFluidHeight(float rawHeight, bool topBlocked)
    {
        return topBlocked ? rawHeight : Mathf.Min(rawHeight, SurfaceHeightScale);
    }

    private static void AddTopFace(MeshBuildBuffers buffers, Vector3 basePosition, float height)
    {
        int start = buffers.vertices.Count;
        Vector3 normal = Vector3.up;
        Color32 color = new(255, 255, 255, 255);

        buffers.vertices.Add(basePosition + new Vector3(0f, height, 0f));
        buffers.vertices.Add(basePosition + new Vector3(0f, height, 1f));
        buffers.vertices.Add(basePosition + new Vector3(1f, height, 1f));
        buffers.vertices.Add(basePosition + new Vector3(1f, height, 0f));

        buffers.uvs.Add(new Vector2(0f, 0f));
        buffers.uvs.Add(new Vector2(0f, 1f));
        buffers.uvs.Add(new Vector2(1f, 1f));
        buffers.uvs.Add(new Vector2(1f, 0f));

        buffers.normals.Add(normal);
        buffers.normals.Add(normal);
        buffers.normals.Add(normal);
        buffers.normals.Add(normal);

        buffers.colors.Add(color);
        buffers.colors.Add(color);
        buffers.colors.Add(color);
        buffers.colors.Add(color);

        buffers.indices.Add(start + 0);
        buffers.indices.Add(start + 1);
        buffers.indices.Add(start + 2);
        buffers.indices.Add(start + 0);
        buffers.indices.Add(start + 2);
        buffers.indices.Add(start + 3);
    }

    private static void AddSideFace(MeshBuildBuffers buffers, Vector3 basePosition, int direction, float bottomHeight, float topHeight)
    {
        if (topHeight <= bottomHeight)
        {
            return;
        }

        Vector3[] vertices;
        Vector3 normal;

        switch (direction)
        {
            case 0:
                vertices = new[]
                {
                    basePosition + new Vector3(0f, bottomHeight, 0f),
                    basePosition + new Vector3(0f, bottomHeight, 1f),
                    basePosition + new Vector3(0f, topHeight, 1f),
                    basePosition + new Vector3(0f, topHeight, 0f),
                };
                normal = Vector3.left;
                break;
            case 1:
                vertices = new[]
                {
                    basePosition + new Vector3(1f, bottomHeight, 1f),
                    basePosition + new Vector3(1f, bottomHeight, 0f),
                    basePosition + new Vector3(1f, topHeight, 0f),
                    basePosition + new Vector3(1f, topHeight, 1f),
                };
                normal = Vector3.right;
                break;
            case 2:
                vertices = new[]
                {
                    basePosition + new Vector3(1f, bottomHeight, 0f),
                    basePosition + new Vector3(0f, bottomHeight, 0f),
                    basePosition + new Vector3(0f, topHeight, 0f),
                    basePosition + new Vector3(1f, topHeight, 0f),
                };
                normal = Vector3.back;
                break;
            default:
                vertices = new[]
                {
                    basePosition + new Vector3(0f, bottomHeight, 1f),
                    basePosition + new Vector3(1f, bottomHeight, 1f),
                    basePosition + new Vector3(1f, topHeight, 1f),
                    basePosition + new Vector3(0f, topHeight, 1f),
                };
                normal = Vector3.forward;
                break;
        }

        int start = buffers.vertices.Count;
        Color32 color = new(255, 255, 255, 255);
        buffers.vertices.Add(vertices[0]);
        buffers.vertices.Add(vertices[1]);
        buffers.vertices.Add(vertices[2]);
        buffers.vertices.Add(vertices[3]);

        buffers.uvs.Add(new Vector2(0f, bottomHeight));
        buffers.uvs.Add(new Vector2(1f, bottomHeight));
        buffers.uvs.Add(new Vector2(1f, topHeight));
        buffers.uvs.Add(new Vector2(0f, topHeight));

        buffers.normals.Add(normal);
        buffers.normals.Add(normal);
        buffers.normals.Add(normal);
        buffers.normals.Add(normal);

        buffers.colors.Add(color);
        buffers.colors.Add(color);
        buffers.colors.Add(color);
        buffers.colors.Add(color);

        buffers.indices.Add(start + 0);
        buffers.indices.Add(start + 1);
        buffers.indices.Add(start + 2);
        buffers.indices.Add(start + 0);
        buffers.indices.Add(start + 2);
        buffers.indices.Add(start + 3);
    }

    private static MeshBuildBuffers GetBuffers()
    {
        s_buffers ??= new MeshBuildBuffers();
        return s_buffers;
    }
}

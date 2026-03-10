using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public static class VoxelMesher
{
    private sealed class MeshBuildBuffers
    {
        public readonly List<Vector3> vertices = new(512);
        public readonly List<int> indices = new(768);
        public readonly List<Vector2> uvs = new(512);
        public readonly List<Vector3> normals = new(512);
        public readonly List<Color32> colors = new(512);
        public readonly MaskCell[] mask = new MaskCell[VoxelTerrainData.SubChunkSize * VoxelTerrainData.SubChunkSize];
        public readonly int[] dims = { VoxelTerrainData.SubChunkSize, VoxelTerrainData.SubChunkSize, VoxelTerrainData.SubChunkSize };
        public readonly int[] x = new int[3];
        public readonly int[] q = new int[3];

        public void Clear()
        {
            vertices.Clear();
            indices.Clear();
            uvs.Clear();
            normals.Clear();
            colors.Clear();
            Array.Clear(mask, 0, mask.Length);
        }
    }

    private sealed class MeshApplyBuffers
    {
        public readonly List<Vector3> vertices = new(512);
        public readonly List<int> indices = new(768);
        public readonly List<Vector2> uvs = new(512);
        public readonly List<Vector3> normals = new(512);
        public readonly List<Color32> colors = new(512);

        public void Clear()
        {
            vertices.Clear();
            indices.Clear();
            uvs.Clear();
            normals.Clear();
            colors.Clear();
        }
    }

    public sealed class PendingSubChunkMeshData : IDisposable
    {
        public readonly int subChunkY;
        public readonly NativeArray<ushort> sampledBlocks;
        public readonly NativeArray<JobMaskCell> mask;
        public readonly NativeList<Vector3> vertices;
        public readonly NativeList<int> indices;
        public readonly NativeList<Vector2> uvs;
        public readonly NativeList<Vector3> normals;
        public readonly NativeList<Color32> colors;

        public JobHandle handle;
        public bool scheduled;

        public PendingSubChunkMeshData(int subChunkY)
        {
            this.subChunkY = subChunkY;
            sampledBlocks = new NativeArray<ushort>(SampledBlockSize * SampledBlockSize * SampledBlockSize, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            mask = new NativeArray<JobMaskCell>(VoxelTerrainData.SubChunkSize * VoxelTerrainData.SubChunkSize, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            vertices = new NativeList<Vector3>(512, Allocator.Persistent);
            indices = new NativeList<int>(768, Allocator.Persistent);
            uvs = new NativeList<Vector2>(512, Allocator.Persistent);
            normals = new NativeList<Vector3>(512, Allocator.Persistent);
            colors = new NativeList<Color32>(512, Allocator.Persistent);
            handle = default;
            scheduled = false;
        }

        public bool HasGeometry => vertices.IsCreated && vertices.Length > 0;

        public void Complete()
        {
            if (scheduled)
            {
                handle.Complete();
            }
        }

        public void Dispose()
        {
            Complete();
            if (sampledBlocks.IsCreated)
            {
                sampledBlocks.Dispose();
            }

            if (mask.IsCreated)
            {
                mask.Dispose();
            }

            if (vertices.IsCreated)
            {
                vertices.Dispose();
            }

            if (indices.IsCreated)
            {
                indices.Dispose();
            }

            if (uvs.IsCreated)
            {
                uvs.Dispose();
            }

            if (normals.IsCreated)
            {
                normals.Dispose();
            }

            if (colors.IsCreated)
            {
                colors.Dispose();
            }
        }
    }

    public sealed class PendingChunkColumnMesh : IDisposable
    {
        public readonly int chunkX;
        public readonly int chunkZ;
        public readonly int usedSubChunkCount;
        public readonly PendingSubChunkMeshData[] subChunks = new PendingSubChunkMeshData[VoxelTerrainData.SubChunkCountY];

        public JobHandle combinedHandle;
        public bool hasScheduledJobs;

        public PendingChunkColumnMesh(int chunkX, int chunkZ, int usedSubChunkCount)
        {
            this.chunkX = chunkX;
            this.chunkZ = chunkZ;
            this.usedSubChunkCount = usedSubChunkCount;
            combinedHandle = default;
            hasScheduledJobs = false;
        }

        public bool IsCompleted => !hasScheduledJobs || combinedHandle.IsCompleted;

        public void Complete()
        {
            if (hasScheduledJobs)
            {
                combinedHandle.Complete();
            }
        }

        public void Dispose()
        {
            Complete();
            for (int i = 0; i < subChunks.Length; i++)
            {
                subChunks[i]?.Dispose();
                subChunks[i] = null;
            }
        }
    }

    private struct MaskCell
    {
        public ushort TextureLayer;
        public bool Exists;
        public bool BackFace;

        public readonly bool Matches(MaskCell other)
        {
            return Exists == other.Exists && TextureLayer == other.TextureLayer && BackFace == other.BackFace;
        }
    }

    public struct JobMaskCell
    {
        public ushort TextureLayer;
        public byte Exists;
        public byte BackFace;

        public readonly bool Matches(JobMaskCell other)
        {
            return Exists == other.Exists && TextureLayer == other.TextureLayer && BackFace == other.BackFace;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    private struct BuildSubChunkMeshJob : IJob
    {
        [ReadOnly] public NativeArray<ushort> sampledBlocks;
        [ReadOnly] public NativeArray<ushort> faceTextureLookup;

        public NativeArray<JobMaskCell> mask;
        public NativeList<Vector3> vertices;
        public NativeList<int> indices;
        public NativeList<Vector2> uvs;
        public NativeList<Vector3> normals;
        public NativeList<Color32> colors;

        public void Execute()
        {
            for (int d = 0; d < 3; d++)
            {
                int u = (d + 1) % 3;
                int v = (d + 2) % 3;
                int3 q = SetAxis(int3.zero, d, 1);
                int3 x = SetAxis(int3.zero, d, -1);

                while (GetAxis(x, d) < VoxelTerrainData.SubChunkSize)
                {
                    int n = 0;

                    for (int vCoord = 0; vCoord < VoxelTerrainData.SubChunkSize; vCoord++)
                    {
                        x = SetAxis(x, v, vCoord);
                        for (int uCoord = 0; uCoord < VoxelTerrainData.SubChunkSize; uCoord++)
                        {
                            x = SetAxis(x, u, uCoord);

                            ushort a = SampleBlock(sampledBlocks, x.x, x.y, x.z);
                            int3 next = x + q;
                            ushort b = SampleBlock(sampledBlocks, next.x, next.y, next.z);

                            bool aSolid = a != 0;
                            bool bSolid = b != 0;

                            if (aSolid == bSolid)
                            {
                                mask[n] = default;
                            }
                            else if (aSolid)
                            {
                                mask[n] = new JobMaskCell
                                {
                                    Exists = 1,
                                    TextureLayer = GetFaceTextureLayer(faceTextureLookup, a, d, false),
                                    BackFace = 0,
                                };
                            }
                            else
                            {
                                mask[n] = new JobMaskCell
                                {
                                    Exists = 1,
                                    TextureLayer = GetFaceTextureLayer(faceTextureLookup, b, d, true),
                                    BackFace = 1,
                                };
                            }

                            n++;
                        }
                    }

                    x = SetAxis(x, d, GetAxis(x, d) + 1);
                    n = 0;

                    for (int j = 0; j < VoxelTerrainData.SubChunkSize; j++)
                    {
                        for (int i = 0; i < VoxelTerrainData.SubChunkSize;)
                        {
                            JobMaskCell current = mask[n];
                            if (current.Exists == 0)
                            {
                                i++;
                                n++;
                                continue;
                            }

                            int width = 1;
                            while (i + width < VoxelTerrainData.SubChunkSize && mask[n + width].Matches(current))
                            {
                                width++;
                            }

                            int height = 1;
                            bool stop = false;
                            while (j + height < VoxelTerrainData.SubChunkSize && !stop)
                            {
                                for (int k = 0; k < width; k++)
                                {
                                    if (!mask[n + k + (height * VoxelTerrainData.SubChunkSize)].Matches(current))
                                    {
                                        stop = true;
                                        break;
                                    }
                                }

                                if (!stop)
                                {
                                    height++;
                                }
                            }

                            x = SetAxis(x, u, i);
                            x = SetAxis(x, v, j);

                            int3 du = SetAxis(int3.zero, u, width);
                            int3 dv = SetAxis(int3.zero, v, height);
                            AddQuad(vertices, indices, uvs, normals, colors, x, du, dv, current.BackFace != 0, current.TextureLayer);

                            for (int l = 0; l < height; l++)
                            {
                                int row = n + (l * VoxelTerrainData.SubChunkSize);
                                for (int k = 0; k < width; k++)
                                {
                                    mask[row + k] = default;
                                }
                            }

                            i += width;
                            n += width;
                        }
                    }
                }
            }
        }
    }

    private const int SampledBlockSize = VoxelTerrainData.SubChunkSize + 2;
    private const int FaceCount = 6;

    [ThreadStatic] private static MeshBuildBuffers s_buildBuffers;
    [ThreadStatic] private static MeshApplyBuffers s_applyBuffers;

    public static PendingChunkColumnMesh ScheduleChunkColumnMesh(
        VoxelTerrainData terrain,
        NativeArray<ushort> faceTextureLookup,
        int chunkX,
        int chunkZ,
        int usedSubChunkCount)
    {
        PendingChunkColumnMesh pendingColumn = new(chunkX, chunkZ, usedSubChunkCount);

        JobHandle combinedHandle = default;
        bool hasScheduledJobs = false;

        for (int subChunkY = 0; subChunkY < usedSubChunkCount; subChunkY++)
        {
            if (!terrain.HasSolidBlocksInSubChunk(chunkX, subChunkY, chunkZ))
            {
                continue;
            }

            PendingSubChunkMeshData pendingSubChunk = new(subChunkY);
            CopySampledBlocks(terrain, chunkX, subChunkY, chunkZ, pendingSubChunk.sampledBlocks);

            BuildSubChunkMeshJob job = new()
            {
                sampledBlocks = pendingSubChunk.sampledBlocks,
                faceTextureLookup = faceTextureLookup,
                mask = pendingSubChunk.mask,
                vertices = pendingSubChunk.vertices,
                indices = pendingSubChunk.indices,
                uvs = pendingSubChunk.uvs,
                normals = pendingSubChunk.normals,
                colors = pendingSubChunk.colors,
            };

            pendingSubChunk.handle = job.Schedule();
            pendingSubChunk.scheduled = true;
            pendingColumn.subChunks[subChunkY] = pendingSubChunk;

            combinedHandle = hasScheduledJobs ? JobHandle.CombineDependencies(combinedHandle, pendingSubChunk.handle) : pendingSubChunk.handle;
            hasScheduledJobs = true;
        }

        pendingColumn.combinedHandle = combinedHandle;
        pendingColumn.hasScheduledJobs = hasScheduledJobs;
        return pendingColumn;
    }

    public static bool ApplyPendingSubChunkMesh(PendingSubChunkMeshData pending, Mesh mesh, string meshName)
    {
        if (mesh == null)
        {
            return false;
        }

        mesh.Clear();
        if (pending == null)
        {
            return false;
        }

        pending.Complete();
        if (!pending.HasGeometry)
        {
            return false;
        }

        MeshApplyBuffers buffers = GetApplyBuffers();
        buffers.Clear();

        CopyNativeList(pending.vertices, buffers.vertices);
        CopyNativeList(pending.indices, buffers.indices);
        CopyNativeList(pending.uvs, buffers.uvs);
        CopyNativeList(pending.normals, buffers.normals);
        CopyNativeList(pending.colors, buffers.colors);

        mesh.name = meshName;
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

    public static bool RebuildSubChunkMesh(Mesh mesh, VoxelTerrainData terrain, VoxelBlockDatabase blockDatabase, int chunkX, int subChunkY, int chunkZ)
    {
        if (mesh == null)
        {
            return false;
        }

        MeshBuildBuffers buffers = GetBuildBuffers();
        buffers.Clear();

        List<Vector3> vertices = buffers.vertices;
        List<int> indices = buffers.indices;
        List<Vector2> uvs = buffers.uvs;
        List<Vector3> normals = buffers.normals;
        List<Color32> colors = buffers.colors;
        MaskCell[] mask = buffers.mask;
        int[] dims = buffers.dims;
        int[] x = buffers.x;
        int[] q = buffers.q;

        for (int d = 0; d < 3; d++)
        {
            int u = (d + 1) % 3;
            int v = (d + 2) % 3;

            q[0] = 0;
            q[1] = 0;
            q[2] = 0;
            q[d] = 1;

            for (x[d] = -1; x[d] < dims[d];)
            {
                int n = 0;

                for (x[v] = 0; x[v] < dims[v]; x[v]++)
                {
                    for (x[u] = 0; x[u] < dims[u]; x[u]++)
                    {
                        VoxelBlockType a = SampleBlock(terrain, chunkX, subChunkY, chunkZ, x[0], x[1], x[2]);
                        VoxelBlockType b = SampleBlock(terrain, chunkX, subChunkY, chunkZ, x[0] + q[0], x[1] + q[1], x[2] + q[2]);

                        bool aSolid = a != VoxelBlockType.Air;
                        bool bSolid = b != VoxelBlockType.Air;

                        if (aSolid == bSolid)
                        {
                            mask[n] = default;
                        }
                        else if (aSolid)
                        {
                            mask[n] = new MaskCell
                            {
                                Exists = true,
                                TextureLayer = blockDatabase.GetFaceTextureLayer((ushort)a, GetFace(d, false)),
                                BackFace = false,
                            };
                        }
                        else
                        {
                            mask[n] = new MaskCell
                            {
                                Exists = true,
                                TextureLayer = blockDatabase.GetFaceTextureLayer((ushort)b, GetFace(d, true)),
                                BackFace = true,
                            };
                        }

                        n++;
                    }
                }

                x[d]++;
                n = 0;

                for (int j = 0; j < dims[v]; j++)
                {
                    for (int i = 0; i < dims[u];)
                    {
                        MaskCell current = mask[n];
                        if (!current.Exists)
                        {
                            i++;
                            n++;
                            continue;
                        }

                        int width = 1;
                        while (i + width < dims[u] && mask[n + width].Matches(current))
                        {
                            width++;
                        }

                        int height = 1;
                        bool stop = false;
                        while (j + height < dims[v] && !stop)
                        {
                            for (int k = 0; k < width; k++)
                            {
                                if (!mask[n + k + (height * dims[u])].Matches(current))
                                {
                                    stop = true;
                                    break;
                                }
                            }

                            if (!stop)
                            {
                                height++;
                            }
                        }

                        x[u] = i;
                        x[v] = j;

                        int[] du = { 0, 0, 0 };
                        int[] dv = { 0, 0, 0 };
                        du[u] = width;
                        dv[v] = height;

                        AddQuad(vertices, indices, uvs, normals, colors, x, du, dv, current.BackFace, current.TextureLayer);

                        for (int l = 0; l < height; l++)
                        {
                            int row = n + (l * dims[u]);
                            for (int k = 0; k < width; k++)
                            {
                                mask[row + k] = default;
                            }
                        }

                        i += width;
                        n += width;
                    }
                }
            }
        }

        mesh.Clear();
        if (vertices.Count == 0)
        {
            return false;
        }

        mesh.name = $"SubChunk_{chunkX}_{subChunkY}_{chunkZ}";
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(indices, 0, true);
        mesh.SetUVs(0, uvs);
        mesh.SetNormals(normals);
        mesh.SetColors(colors);
        mesh.RecalculateBounds();
        mesh.UploadMeshData(false);
        return true;
    }

    private static void CopySampledBlocks(VoxelTerrainData terrain, int chunkX, int subChunkY, int chunkZ, NativeArray<ushort> sampledBlocks)
    {
        int startWorldX = chunkX * VoxelTerrainData.ChunkSize;
        int startWorldY = subChunkY * VoxelTerrainData.SubChunkSize;
        int startWorldZ = chunkZ * VoxelTerrainData.ChunkSize;

        for (int sampleZ = -1; sampleZ <= VoxelTerrainData.SubChunkSize; sampleZ++)
        {
            for (int sampleY = -1; sampleY <= VoxelTerrainData.SubChunkSize; sampleY++)
            {
                for (int sampleX = -1; sampleX <= VoxelTerrainData.SubChunkSize; sampleX++)
                {
                    int worldX = startWorldX + sampleX;
                    int worldY = startWorldY + sampleY;
                    int worldZ = startWorldZ + sampleZ;

                    sampledBlocks[GetSampleIndex(sampleX, sampleY, sampleZ)] = (ushort)terrain.GetBlock(worldX, worldY, worldZ);
                }
            }
        }
    }

    private static MeshBuildBuffers GetBuildBuffers()
    {
        s_buildBuffers ??= new MeshBuildBuffers();
        return s_buildBuffers;
    }

    private static MeshApplyBuffers GetApplyBuffers()
    {
        s_applyBuffers ??= new MeshApplyBuffers();
        return s_applyBuffers;
    }

    private static void CopyNativeList<T>(NativeList<T> source, List<T> destination) where T : unmanaged
    {
        destination.Capacity = Mathf.Max(destination.Capacity, source.Length);
        for (int i = 0; i < source.Length; i++)
        {
            destination.Add(source[i]);
        }
    }

    private static ushort SampleBlock(NativeArray<ushort> sampledBlocks, int localX, int localY, int localZ)
    {
        return sampledBlocks[GetSampleIndex(localX, localY, localZ)];
    }

    private static int GetSampleIndex(int localX, int localY, int localZ)
    {
        int sampleX = localX + 1;
        int sampleY = localY + 1;
        int sampleZ = localZ + 1;
        return ((sampleY * SampledBlockSize) + sampleZ) * SampledBlockSize + sampleX;
    }

    private static ushort GetFaceTextureLayer(NativeArray<ushort> faceTextureLookup, ushort blockId, int axis, bool backFace)
    {
        int faceIndex = axis switch
        {
            0 => backFace ? (int)VoxelBlockFace.Left : (int)VoxelBlockFace.Right,
            1 => backFace ? (int)VoxelBlockFace.Bottom : (int)VoxelBlockFace.Top,
            2 => backFace ? (int)VoxelBlockFace.Back : (int)VoxelBlockFace.Front,
            _ => (int)VoxelBlockFace.Front,
        };

        int lookupIndex = (blockId * FaceCount) + faceIndex;
        return lookupIndex >= 0 && lookupIndex < faceTextureLookup.Length ? faceTextureLookup[lookupIndex] : (ushort)0;
    }

    private static int GetAxis(int3 value, int axis)
    {
        return axis switch
        {
            0 => value.x,
            1 => value.y,
            _ => value.z,
        };
    }

    private static int3 SetAxis(int3 value, int axis, int component)
    {
        switch (axis)
        {
            case 0:
                value.x = component;
                break;
            case 1:
                value.y = component;
                break;
            default:
                value.z = component;
                break;
        }

        return value;
    }

    private static VoxelBlockType SampleBlock(VoxelTerrainData terrain, int chunkX, int subChunkY, int chunkZ, int localX, int localY, int localZ)
    {
        int worldX = (chunkX * VoxelTerrainData.ChunkSize) + localX;
        int worldY = (subChunkY * VoxelTerrainData.SubChunkSize) + localY;
        int worldZ = (chunkZ * VoxelTerrainData.ChunkSize) + localZ;
        return terrain.GetBlock(worldX, worldY, worldZ);
    }

    private static void AddQuad(
        NativeList<Vector3> vertices,
        NativeList<int> indices,
        NativeList<Vector2> uvs,
        NativeList<Vector3> normals,
        NativeList<Color32> colors,
        int3 position,
        int3 du,
        int3 dv,
        bool backFace,
        ushort textureLayer)
    {
        Vector3 origin = new(position.x, position.y, position.z);
        Vector3 uVector = new(du.x, du.y, du.z);
        Vector3 vVector = new(dv.x, dv.y, dv.z);
        int vertexStart = vertices.Length;
        Vector3 faceNormal = Vector3.Cross(uVector, vVector).normalized;
        Color32 encodedLayer = EncodeTextureLayer(textureLayer);
        if (backFace)
        {
            faceNormal = -faceNormal;
        }

        float uvWidth = Mathf.Max(1f, Mathf.Abs(du.x) + Mathf.Abs(du.y) + Mathf.Abs(du.z));
        float uvHeight = Mathf.Max(1f, Mathf.Abs(dv.x) + Mathf.Abs(dv.y) + Mathf.Abs(dv.z));

        if (backFace)
        {
            vertices.Add(origin);
            vertices.Add(origin + vVector);
            vertices.Add(origin + uVector + vVector);
            vertices.Add(origin + uVector);

            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(0f, uvHeight));
            uvs.Add(new Vector2(uvWidth, uvHeight));
            uvs.Add(new Vector2(uvWidth, 0f));
        }
        else
        {
            vertices.Add(origin);
            vertices.Add(origin + uVector);
            vertices.Add(origin + uVector + vVector);
            vertices.Add(origin + vVector);

            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(uvWidth, 0f));
            uvs.Add(new Vector2(uvWidth, uvHeight));
            uvs.Add(new Vector2(0f, uvHeight));
        }

        normals.Add(faceNormal);
        normals.Add(faceNormal);
        normals.Add(faceNormal);
        normals.Add(faceNormal);

        colors.Add(encodedLayer);
        colors.Add(encodedLayer);
        colors.Add(encodedLayer);
        colors.Add(encodedLayer);

        indices.Add(vertexStart + 0);
        indices.Add(vertexStart + 1);
        indices.Add(vertexStart + 2);
        indices.Add(vertexStart + 0);
        indices.Add(vertexStart + 2);
        indices.Add(vertexStart + 3);
    }

    private static void AddQuad(
        List<Vector3> vertices,
        List<int> indices,
        List<Vector2> uvs,
        List<Vector3> normals,
        List<Color32> colors,
        int[] position,
        int[] du,
        int[] dv,
        bool backFace,
        ushort textureLayer)
    {
        Vector3 origin = new(position[0], position[1], position[2]);
        Vector3 uVector = new(du[0], du[1], du[2]);
        Vector3 vVector = new(dv[0], dv[1], dv[2]);
        int vertexStart = vertices.Count;
        Vector3 faceNormal = Vector3.Normalize(Vector3.Cross(uVector, vVector));
        Color32 encodedLayer = EncodeTextureLayer(textureLayer);
        if (backFace)
        {
            faceNormal = -faceNormal;
        }

        float uvWidth = Mathf.Max(1f, Mathf.Abs(du[0]) + Mathf.Abs(du[1]) + Mathf.Abs(du[2]));
        float uvHeight = Mathf.Max(1f, Mathf.Abs(dv[0]) + Mathf.Abs(dv[1]) + Mathf.Abs(dv[2]));

        if (backFace)
        {
            vertices.Add(origin);
            vertices.Add(origin + vVector);
            vertices.Add(origin + uVector + vVector);
            vertices.Add(origin + uVector);

            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(0f, uvHeight));
            uvs.Add(new Vector2(uvWidth, uvHeight));
            uvs.Add(new Vector2(uvWidth, 0f));
        }
        else
        {
            vertices.Add(origin);
            vertices.Add(origin + uVector);
            vertices.Add(origin + uVector + vVector);
            vertices.Add(origin + vVector);

            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(uvWidth, 0f));
            uvs.Add(new Vector2(uvWidth, uvHeight));
            uvs.Add(new Vector2(0f, uvHeight));
        }

        normals.Add(faceNormal);
        normals.Add(faceNormal);
        normals.Add(faceNormal);
        normals.Add(faceNormal);

        colors.Add(encodedLayer);
        colors.Add(encodedLayer);
        colors.Add(encodedLayer);
        colors.Add(encodedLayer);

        indices.Add(vertexStart + 0);
        indices.Add(vertexStart + 1);
        indices.Add(vertexStart + 2);
        indices.Add(vertexStart + 0);
        indices.Add(vertexStart + 2);
        indices.Add(vertexStart + 3);
    }

    private static VoxelBlockFace GetFace(int axis, bool backFace)
    {
        return axis switch
        {
            0 => backFace ? VoxelBlockFace.Left : VoxelBlockFace.Right,
            1 => backFace ? VoxelBlockFace.Bottom : VoxelBlockFace.Top,
            2 => backFace ? VoxelBlockFace.Back : VoxelBlockFace.Front,
            _ => VoxelBlockFace.Front,
        };
    }

    private static Color32 EncodeTextureLayer(ushort textureLayer)
    {
        byte low = (byte)(textureLayer & 0xFF);
        byte high = (byte)((textureLayer >> 8) & 0xFF);
        return new Color32(low, high, 0, 255);
    }
}

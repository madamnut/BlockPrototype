using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class TerrainGpuMesher : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct GpuTerrainVertex
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector2 uv;
        public uint packedColor;
    }

    internal sealed class PendingSubChunkMesh
    {
        public readonly int subChunkY;
        public readonly ComputeBuffer vertexBuffer;
        public readonly ComputeBuffer indexBuffer;
        public readonly ComputeBuffer counterBuffer;
        internal GpuTerrainVertex[] vertexReadback;
        internal int[] indexReadback;
        internal int vertexCount;
        internal int indexCount;
        internal bool verticesReady;
        internal bool indicesReady;
        internal bool completed;

        public PendingSubChunkMesh(int subChunkY, ComputeBuffer vertexBuffer, ComputeBuffer indexBuffer, ComputeBuffer counterBuffer)
        {
            this.subChunkY = subChunkY;
            this.vertexBuffer = vertexBuffer;
            this.indexBuffer = indexBuffer;
            this.counterBuffer = counterBuffer;
        }

        public void ReleaseBuffers()
        {
            vertexBuffer?.Release();
            indexBuffer?.Release();
            counterBuffer?.Release();
        }
    }

    public sealed class SubChunkMeshData
    {
        public readonly int subChunkY;
        public readonly Vector3[] vertices;
        public readonly int[] indices;
        public readonly Vector2[] uvs;
        public readonly Vector3[] normals;
        public readonly Color32[] colors;

        public SubChunkMeshData(
            int subChunkY,
            Vector3[] vertices,
            int[] indices,
            Vector2[] uvs,
            Vector3[] normals,
            Color32[] colors)
        {
            this.subChunkY = subChunkY;
            this.vertices = vertices;
            this.indices = indices;
            this.uvs = uvs;
            this.normals = normals;
            this.colors = colors;
        }

        public bool HasGeometry => vertices != null && vertices.Length > 0;
    }

    public sealed class ChunkColumnMeshData
    {
        public readonly int chunkX;
        public readonly int chunkZ;
        public readonly int usedSubChunkCount;
        public readonly SubChunkMeshData[] subChunks = new SubChunkMeshData[TerrainData.SubChunkCountY];

        public ChunkColumnMeshData(int chunkX, int chunkZ, int usedSubChunkCount)
        {
            this.chunkX = chunkX;
            this.chunkZ = chunkZ;
            this.usedSubChunkCount = usedSubChunkCount;
        }
    }

    public sealed class PendingChunkColumnMesh : IDisposable
    {
        private readonly PendingSubChunkMesh[] _subChunks;
        private ComputeBuffer _heightBuffer;
        private int _remainingSubChunkCount;
        private bool _disposed;

        public PendingChunkColumnMesh(int chunkX, int chunkZ, int usedSubChunkCount, ComputeBuffer heightBuffer)
        {
            ChunkX = chunkX;
            ChunkZ = chunkZ;
            Result = new ChunkColumnMeshData(chunkX, chunkZ, usedSubChunkCount);
            _heightBuffer = heightBuffer;
            _subChunks = new PendingSubChunkMesh[usedSubChunkCount];
            _remainingSubChunkCount = usedSubChunkCount;
        }

        public int ChunkX { get; }
        public int ChunkZ { get; }
        public bool IsCompleted => !_disposed && _remainingSubChunkCount <= 0;
        public ChunkColumnMeshData Result { get; }

        public bool IsDisposed => _disposed;

        internal void SetSubChunk(int index, PendingSubChunkMesh subChunk)
        {
            _subChunks[index] = subChunk;
        }

        internal void CompleteSubChunk(PendingSubChunkMesh subChunk, SubChunkMeshData meshData = null)
        {
            if (_disposed || subChunk == null || subChunk.completed)
            {
                return;
            }

            subChunk.completed = true;
            if (meshData != null)
            {
                Result.subChunks[subChunk.subChunkY] = meshData;
            }

            subChunk.ReleaseBuffers();
            _remainingSubChunkCount--;
            if (_remainingSubChunkCount <= 0)
            {
                _heightBuffer?.Release();
                _heightBuffer = null;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            for (int i = 0; i < _subChunks.Length; i++)
            {
                _subChunks[i]?.ReleaseBuffers();
                _subChunks[i] = null;
            }

            _heightBuffer?.Release();
            _heightBuffer = null;
        }
    }

    private const string KernelName = "BuildSolidSubChunkMesh";
    private const int NeighborhoodSize = TerrainData.ChunkSize + 2;
    private const int MaxQuadsPerSubChunk = TerrainData.ChunkSize * TerrainData.ChunkSize * 9;
    private const int MaxVerticesPerSubChunk = MaxQuadsPerSubChunk * 4;
    private const int MaxIndicesPerSubChunk = MaxQuadsPerSubChunk * 6;
    private static readonly uint[] CounterReset = new uint[2];

    private readonly ComputeShader _computeShader;
    private readonly int _kernelIndex = -1;
    private readonly int[] _heightNeighborhood = new int[NeighborhoodSize * NeighborhoodSize];
    private readonly int _vertexStride = Marshal.SizeOf<GpuTerrainVertex>();

    public TerrainGpuMesher(ComputeShader computeShader)
    {
        if (computeShader == null || !SystemInfo.supportsComputeShaders || !computeShader.HasKernel(KernelName))
        {
            return;
        }

        _computeShader = computeShader;
        _kernelIndex = _computeShader.FindKernel(KernelName);
    }

    public bool TryScheduleChunkColumnMesh(
        TerrainData terrain,
        BlockDatabase blockDatabase,
        int chunkX,
        int chunkZ,
        int usedSubChunkCount,
        out PendingChunkColumnMesh pending)
    {
        pending = null;
        if (_computeShader == null || _kernelIndex < 0 || terrain == null || blockDatabase == null || usedSubChunkCount <= 0)
        {
            return false;
        }

        ushort grassTopLayer = blockDatabase.GetFaceTextureLayer((ushort)BlockType.Grass, BlockFace.Top);
        ushort grassRightLayer = blockDatabase.GetFaceTextureLayer((ushort)BlockType.Grass, BlockFace.Right);
        ushort grassFrontLayer = blockDatabase.GetFaceTextureLayer((ushort)BlockType.Grass, BlockFace.Front);
        ushort rockRightLayer = blockDatabase.GetFaceTextureLayer((ushort)BlockType.Rock, BlockFace.Right);
        ushort rockFrontLayer = blockDatabase.GetFaceTextureLayer((ushort)BlockType.Rock, BlockFace.Front);

        PopulateHeightNeighborhood(terrain, chunkX, chunkZ);
        ComputeBuffer heightBuffer = new ComputeBuffer(_heightNeighborhood.Length, sizeof(int));
        heightBuffer.SetData(_heightNeighborhood);

        pending = new PendingChunkColumnMesh(chunkX, chunkZ, usedSubChunkCount, heightBuffer);
        PendingChunkColumnMesh scheduledPending = pending;
        for (int subChunkY = 0; subChunkY < usedSubChunkCount; subChunkY++)
        {
            ComputeBuffer vertexBuffer = new ComputeBuffer(MaxVerticesPerSubChunk, _vertexStride);
            ComputeBuffer indexBuffer = new ComputeBuffer(MaxIndicesPerSubChunk, sizeof(int));
            ComputeBuffer counterBuffer = new ComputeBuffer(2, sizeof(uint));
            counterBuffer.SetData(CounterReset);

            PendingSubChunkMesh subChunk = new(subChunkY, vertexBuffer, indexBuffer, counterBuffer);
            scheduledPending.SetSubChunk(subChunkY, subChunk);

            _computeShader.SetInt("_SubChunkY", subChunkY);
            _computeShader.SetInt("_ChunkSize", TerrainData.ChunkSize);
            _computeShader.SetInt("_SubChunkSize", TerrainData.SubChunkSize);
            _computeShader.SetInt("_NeighborhoodSize", NeighborhoodSize);
            _computeShader.SetInt("_GrassTopLayer", grassTopLayer);
            _computeShader.SetInt("_GrassRightLayer", grassRightLayer);
            _computeShader.SetInt("_GrassFrontLayer", grassFrontLayer);
            _computeShader.SetInt("_RockRightLayer", rockRightLayer);
            _computeShader.SetInt("_RockFrontLayer", rockFrontLayer);
            _computeShader.SetBuffer(_kernelIndex, "_HeightNeighborhood", heightBuffer);
            _computeShader.SetBuffer(_kernelIndex, "_Vertices", vertexBuffer);
            _computeShader.SetBuffer(_kernelIndex, "_Indices", indexBuffer);
            _computeShader.SetBuffer(_kernelIndex, "_Counters", counterBuffer);
            _computeShader.Dispatch(_kernelIndex, TerrainData.ChunkSize / 8, TerrainData.ChunkSize / 8, 1);

            AsyncGPUReadback.Request(counterBuffer, request => OnCounterReadback(scheduledPending, subChunk, request));
        }

        return true;
    }

    public void Dispose()
    {
    }

    private void PopulateHeightNeighborhood(TerrainData terrain, int chunkX, int chunkZ)
    {
        int startWorldX = (chunkX * TerrainData.ChunkSize) - 1;
        int startWorldZ = (chunkZ * TerrainData.ChunkSize) - 1;

        for (int sampleZ = 0; sampleZ < NeighborhoodSize; sampleZ++)
        {
            for (int sampleX = 0; sampleX < NeighborhoodSize; sampleX++)
            {
                int worldX = startWorldX + sampleX;
                int worldZ = startWorldZ + sampleZ;
                _heightNeighborhood[(sampleZ * NeighborhoodSize) + sampleX] = terrain.GetColumnHeight(worldX, worldZ);
            }
        }
    }

    private void OnCounterReadback(PendingChunkColumnMesh pending, PendingSubChunkMesh subChunk, AsyncGPUReadbackRequest request)
    {
        if (pending == null || pending.IsDisposed || subChunk == null || subChunk.completed)
        {
            return;
        }

        if (request.hasError)
        {
            pending.CompleteSubChunk(subChunk);
            return;
        }

        var counts = request.GetData<uint>();
        subChunk.vertexCount = Mathf.Min((int)counts[0], MaxVerticesPerSubChunk);
        subChunk.indexCount = Mathf.Min((int)counts[1], MaxIndicesPerSubChunk);
        if (subChunk.vertexCount <= 0 || subChunk.indexCount <= 0)
        {
            pending.CompleteSubChunk(subChunk);
            return;
        }

        AsyncGPUReadback.Request(subChunk.vertexBuffer, vertexRequest => OnVertexReadback(pending, subChunk, vertexRequest));
        AsyncGPUReadback.Request(subChunk.indexBuffer, indexRequest => OnIndexReadback(pending, subChunk, indexRequest));
    }

    private void OnVertexReadback(PendingChunkColumnMesh pending, PendingSubChunkMesh subChunk, AsyncGPUReadbackRequest request)
    {
        if (pending == null || pending.IsDisposed || subChunk == null || subChunk.completed)
        {
            return;
        }

        if (request.hasError)
        {
            pending.CompleteSubChunk(subChunk);
            return;
        }

        var data = request.GetData<GpuTerrainVertex>();
        subChunk.vertexReadback = new GpuTerrainVertex[subChunk.vertexCount];
        for (int i = 0; i < subChunk.vertexCount; i++)
        {
            subChunk.vertexReadback[i] = data[i];
        }

        subChunk.verticesReady = true;
        TryFinalizeSubChunk(pending, subChunk);
    }

    private void OnIndexReadback(PendingChunkColumnMesh pending, PendingSubChunkMesh subChunk, AsyncGPUReadbackRequest request)
    {
        if (pending == null || pending.IsDisposed || subChunk == null || subChunk.completed)
        {
            return;
        }

        if (request.hasError)
        {
            pending.CompleteSubChunk(subChunk);
            return;
        }

        var data = request.GetData<int>();
        subChunk.indexReadback = new int[subChunk.indexCount];
        for (int i = 0; i < subChunk.indexCount; i++)
        {
            subChunk.indexReadback[i] = data[i];
        }

        subChunk.indicesReady = true;
        TryFinalizeSubChunk(pending, subChunk);
    }

    private void TryFinalizeSubChunk(PendingChunkColumnMesh pending, PendingSubChunkMesh subChunk)
    {
        if (pending == null || pending.IsDisposed || subChunk == null || subChunk.completed)
        {
            return;
        }

        if (!subChunk.verticesReady || !subChunk.indicesReady)
        {
            return;
        }

        pending.CompleteSubChunk(subChunk, CreateSubChunkMeshData(subChunk));
    }

    private static SubChunkMeshData CreateSubChunkMeshData(PendingSubChunkMesh subChunk)
    {
        Vector3[] vertices = new Vector3[subChunk.vertexCount];
        Vector3[] normals = new Vector3[subChunk.vertexCount];
        Vector2[] uvs = new Vector2[subChunk.vertexCount];
        Color32[] colors = new Color32[subChunk.vertexCount];
        int[] indices = new int[subChunk.indexCount];

        for (int i = 0; i < subChunk.vertexCount; i++)
        {
            GpuTerrainVertex vertex = subChunk.vertexReadback[i];
            vertices[i] = vertex.position;
            normals[i] = vertex.normal;
            uvs[i] = vertex.uv;
            colors[i] = DecodePackedColor(vertex.packedColor);
        }

        Array.Copy(subChunk.indexReadback, indices, subChunk.indexCount);
        return new SubChunkMeshData(subChunk.subChunkY, vertices, indices, uvs, normals, colors);
    }

    private static Color32 DecodePackedColor(uint packedColor)
    {
        return new Color32(
            (byte)(packedColor & 0xFFu),
            (byte)((packedColor >> 8) & 0xFFu),
            (byte)((packedColor >> 16) & 0xFFu),
            (byte)((packedColor >> 24) & 0xFFu));
    }
}

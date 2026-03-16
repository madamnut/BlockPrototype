using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class WorldStreaming
{
    private readonly List<TerrainData.CompletedChunkColumnInfo> _completedChunkBuffer = new(16);
    private readonly List<Vector2Int> _visibleChunkKeyBuffer = new(256);
    private readonly List<Vector2Int> _chunkPriorityBuffer = new(256);
    private readonly List<Vector2Int> _requestQueue = new(256);
    private readonly HashSet<Vector2Int> _queuedChunkColumns = new();
    private readonly HashSet<Vector2Int> _queuedChunkRequests = new();
    private readonly HashSet<Vector2Int> _targetGenerationChunks = new();
    private readonly HashSet<Vector2Int> _targetVisibleChunks = new();
    private readonly HashSet<Vector2Int> _visibleChunkColumns = new();
    private readonly List<Vector2Int> _chunkRefreshQueue = new(256);
    private int _chunkRefreshQueueHead;
    private int _requestQueueHead;

    public bool HasCenterChunk { get; private set; }
    public Vector2Int CurrentCenterChunk { get; private set; }
    public int VisibleChunkCount => _visibleChunkColumns.Count;
    public int PendingRefreshCount => _chunkRefreshQueue.Count - _chunkRefreshQueueHead;
    public int QueuedChunkCount => _queuedChunkColumns.Count;
    public int PendingRequestCount => _requestQueue.Count - _requestQueueHead;

    public void Reset()
    {
        _visibleChunkColumns.Clear();
        _queuedChunkColumns.Clear();
        _queuedChunkRequests.Clear();
        _chunkRefreshQueue.Clear();
        _chunkRefreshQueueHead = 0;
        _requestQueue.Clear();
        _requestQueueHead = 0;
        _targetGenerationChunks.Clear();
        _targetVisibleChunks.Clear();
        _visibleChunkKeyBuffer.Clear();
        _chunkPriorityBuffer.Clear();
        _completedChunkBuffer.Clear();
        HasCenterChunk = false;
        CurrentCenterChunk = default;
    }

    public bool IsChunkVisible(Vector2Int chunkCoords)
    {
        return _visibleChunkColumns.Contains(chunkCoords);
    }

    public void UpdateVisibleChunks(
        Vector2Int centerChunk,
        bool force,
        int renderSizeInChunks,
        int generationPaddingInChunks,
        Func<int, int, bool> isChunkColumnReady,
        Action<Vector2Int> releaseChunkColumn)
    {
        if (!force && HasCenterChunk && centerChunk == CurrentCenterChunk)
        {
            return;
        }

        CurrentCenterChunk = centerChunk;
        HasCenterChunk = true;

        _targetVisibleChunks.Clear();
        _targetGenerationChunks.Clear();
        int radius = renderSizeInChunks / 2;
        for (int offsetZ = -radius; offsetZ <= radius; offsetZ++)
        {
            for (int offsetX = -radius; offsetX <= radius; offsetX++)
            {
                _targetVisibleChunks.Add(new Vector2Int(centerChunk.x + offsetX, centerChunk.y + offsetZ));
            }
        }

        int generationRadius = radius + generationPaddingInChunks;
        for (int offsetZ = -generationRadius; offsetZ <= generationRadius; offsetZ++)
        {
            for (int offsetX = -generationRadius; offsetX <= generationRadius; offsetX++)
            {
                _targetGenerationChunks.Add(new Vector2Int(centerChunk.x + offsetX, centerChunk.y + offsetZ));
            }
        }

        RebuildRequestQueue(centerChunk, isChunkColumnReady);

        _visibleChunkKeyBuffer.Clear();
        foreach (Vector2Int chunkCoords in _visibleChunkColumns)
        {
            _visibleChunkKeyBuffer.Add(chunkCoords);
        }

        for (int i = 0; i < _visibleChunkKeyBuffer.Count; i++)
        {
            Vector2Int chunkCoords = _visibleChunkKeyBuffer[i];
            if (_targetVisibleChunks.Contains(chunkCoords))
            {
                continue;
            }

            releaseChunkColumn?.Invoke(chunkCoords);
            _visibleChunkColumns.Remove(chunkCoords);
        }

        GetSortedChunkCoordinates(_targetVisibleChunks, centerChunk, _chunkPriorityBuffer);
        for (int i = 0; i < _chunkPriorityBuffer.Count; i++)
        {
            Vector2Int chunkCoords = _chunkPriorityBuffer[i];
            if (!_visibleChunkColumns.Add(chunkCoords))
            {
                continue;
            }

            if (isChunkColumnReady != null && isChunkColumnReady(chunkCoords.x, chunkCoords.y))
            {
                QueueChunkColumnRefresh(chunkCoords);
            }
        }
    }

    public void ProcessChunkRequests(
        int maxRequestsPerFrame,
        int maxPendingChunkGenerations,
        int currentPendingChunkGenerations,
        Func<int, int, bool> isChunkColumnReady,
        Func<int, int, bool> requestChunkColumn)
    {
        if (maxRequestsPerFrame <= 0 || maxPendingChunkGenerations <= 0)
        {
            return;
        }

        int requestsStarted = 0;
        while (_requestQueueHead < _requestQueue.Count && requestsStarted < maxRequestsPerFrame)
        {
            if (currentPendingChunkGenerations >= maxPendingChunkGenerations)
            {
                break;
            }

            Vector2Int chunkCoords = _requestQueue[_requestQueueHead];
            _requestQueueHead++;
            _queuedChunkRequests.Remove(chunkCoords);

            if (!_targetGenerationChunks.Contains(chunkCoords))
            {
                continue;
            }

            if (isChunkColumnReady != null && isChunkColumnReady(chunkCoords.x, chunkCoords.y))
            {
                continue;
            }

            if (requestChunkColumn != null && requestChunkColumn(chunkCoords.x, chunkCoords.y))
            {
                requestsStarted++;
                currentPendingChunkGenerations++;
            }
        }

        TrimProcessedRequestQueue();
    }

    public void CompleteChunkGenerationJobs(TerrainData terrain, int completedChunkGenerationsPerFrame)
    {
        _completedChunkBuffer.Clear();
        terrain.CompletePendingChunkColumns(_completedChunkBuffer, completedChunkGenerationsPerFrame);

        for (int i = 0; i < _completedChunkBuffer.Count; i++)
        {
            Vector2Int chunkCoords = _completedChunkBuffer[i].chunkCoords;
            if (!terrain.IsChunkColumnReady(chunkCoords.x, chunkCoords.y))
            {
                continue;
            }

            QueueChunkAndNeighborsForRefresh(chunkCoords);
        }
    }

    public void ProcessChunkRefreshQueue(TerrainData terrain, int chunkColumnsMeshedPerFrame, Action<int, int, int> scheduleChunkColumnMesh)
    {
        int processedCount = 0;
        while (_chunkRefreshQueueHead < _chunkRefreshQueue.Count && processedCount < chunkColumnsMeshedPerFrame)
        {
            Vector2Int chunkCoords = _chunkRefreshQueue[_chunkRefreshQueueHead];
            _chunkRefreshQueueHead++;
            _queuedChunkColumns.Remove(chunkCoords);

            if (!_visibleChunkColumns.Contains(chunkCoords))
            {
                continue;
            }

            if (!terrain.TryGetUsedSubChunkCount(chunkCoords.x, chunkCoords.y, out int usedSubChunkCount))
            {
                terrain.RequestChunkColumn(chunkCoords.x, chunkCoords.y);
                continue;
            }

            scheduleChunkColumnMesh?.Invoke(chunkCoords.x, chunkCoords.y, usedSubChunkCount);
            processedCount++;
        }

        TrimProcessedRefreshQueue();
    }

    private void QueueChunkAndNeighborsForRefresh(Vector2Int chunkCoords)
    {
        QueueChunkColumnRefresh(chunkCoords);
        QueueChunkColumnRefresh(new Vector2Int(chunkCoords.x - 1, chunkCoords.y));
        QueueChunkColumnRefresh(new Vector2Int(chunkCoords.x + 1, chunkCoords.y));
        QueueChunkColumnRefresh(new Vector2Int(chunkCoords.x, chunkCoords.y - 1));
        QueueChunkColumnRefresh(new Vector2Int(chunkCoords.x, chunkCoords.y + 1));
    }

    private void QueueChunkColumnRefresh(Vector2Int chunkCoords)
    {
        if (!_visibleChunkColumns.Contains(chunkCoords))
        {
            return;
        }

        if (!_queuedChunkColumns.Add(chunkCoords))
        {
            return;
        }

        InsertChunkRefreshByPriority(chunkCoords);
    }

    private void InsertChunkRefreshByPriority(Vector2Int chunkCoords)
    {
        int insertIndex = _chunkRefreshQueue.Count;
        for (int i = _chunkRefreshQueueHead; i < _chunkRefreshQueue.Count; i++)
        {
            if (CompareChunkPriority(chunkCoords, _chunkRefreshQueue[i], CurrentCenterChunk) < 0)
            {
                insertIndex = i;
                break;
            }
        }

        _chunkRefreshQueue.Insert(insertIndex, chunkCoords);
    }

    private void RebuildRequestQueue(Vector2Int centerChunk, Func<int, int, bool> isChunkColumnReady)
    {
        _chunkPriorityBuffer.Clear();
        foreach (Vector2Int chunkCoords in _targetGenerationChunks)
        {
            if (isChunkColumnReady != null && isChunkColumnReady(chunkCoords.x, chunkCoords.y))
            {
                continue;
            }

            _chunkPriorityBuffer.Add(chunkCoords);
        }

        SortChunkCoordinatesByDistance(_chunkPriorityBuffer, centerChunk);
        _requestQueue.Clear();
        _queuedChunkRequests.Clear();
        _requestQueueHead = 0;

        for (int i = 0; i < _chunkPriorityBuffer.Count; i++)
        {
            Vector2Int chunkCoords = _chunkPriorityBuffer[i];
            _requestQueue.Add(chunkCoords);
            _queuedChunkRequests.Add(chunkCoords);
        }
    }

    private void TrimProcessedRefreshQueue()
    {
        if (_chunkRefreshQueueHead <= 0)
        {
            return;
        }

        if (_chunkRefreshQueueHead >= _chunkRefreshQueue.Count)
        {
            _chunkRefreshQueue.Clear();
            _chunkRefreshQueueHead = 0;
            return;
        }

        if (_chunkRefreshQueueHead < 64 && _chunkRefreshQueueHead * 2 < _chunkRefreshQueue.Count)
        {
            return;
        }

        _chunkRefreshQueue.RemoveRange(0, _chunkRefreshQueueHead);
        _chunkRefreshQueueHead = 0;
    }

    private void TrimProcessedRequestQueue()
    {
        if (_requestQueueHead <= 0)
        {
            return;
        }

        if (_requestQueueHead >= _requestQueue.Count)
        {
            _requestQueue.Clear();
            _requestQueueHead = 0;
            return;
        }

        if (_requestQueueHead < 64 && _requestQueueHead * 2 < _requestQueue.Count)
        {
            return;
        }

        _requestQueue.RemoveRange(0, _requestQueueHead);
        _requestQueueHead = 0;
    }

    private static void GetSortedChunkCoordinates(HashSet<Vector2Int> source, Vector2Int centerChunk, List<Vector2Int> destination)
    {
        destination.Clear();
        foreach (Vector2Int chunkCoords in source)
        {
            destination.Add(chunkCoords);
        }

        SortChunkCoordinatesByDistance(destination, centerChunk);
    }

    private static void SortChunkCoordinatesByDistance(List<Vector2Int> chunkCoords, Vector2Int centerChunk)
    {
        chunkCoords.Sort((a, b) => CompareChunkPriority(a, b, centerChunk));
    }

    private static int CompareChunkPriority(Vector2Int a, Vector2Int b, Vector2Int centerChunk)
    {
        int aDx = a.x - centerChunk.x;
        int aDz = a.y - centerChunk.y;
        int bDx = b.x - centerChunk.x;
        int bDz = b.y - centerChunk.y;

        int aDistance = (aDx * aDx) + (aDz * aDz);
        int bDistance = (bDx * bDx) + (bDz * bDz);
        int distanceComparison = aDistance.CompareTo(bDistance);
        if (distanceComparison != 0)
        {
            return distanceComparison;
        }

        int zComparison = a.y.CompareTo(b.y);
        return zComparison != 0 ? zComparison : a.x.CompareTo(b.x);
    }
}

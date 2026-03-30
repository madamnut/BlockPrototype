using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class WorldInteraction
{
    private struct SelectionHit
    {
        public Vector3Int position;
        public Vector3Int placementPosition;
        public ushort contentId;
        public bool isFoliage;
    }

    private readonly BlockDatabase _blockDatabase;
    private readonly float _interactionDistance;
    private readonly Func<TerrainData> _terrainProvider;
    private readonly Func<Transform> _worldRootProvider;
    private readonly Func<FlyCamera> _playerControllerProvider;
    private readonly Func<Camera> _interactionCameraProvider;
    private readonly Func<int, int, bool> _canRefreshChunkColumn;
    private readonly Action<int, int, int> _refreshLoadedSubChunk;
    private readonly Action<bool, Vector3Int> _setSelectionVisual;
    private readonly List<Vector3Int> _subChunkRefreshBuffer = new(7);

    private Vector3Int _selectedBlockPosition;
    private Vector3Int _placementBlockPosition;
    private ushort _selectedContentId;
    private bool _selectedIsFoliage;
    private bool _hasSelection;
    private BlockType _selectedPlacementBlock = BlockType.Dirt;

    public WorldInteraction(
        BlockDatabase blockDatabase,
        float interactionDistance,
        Func<TerrainData> terrainProvider,
        Func<Transform> worldRootProvider,
        Func<FlyCamera> playerControllerProvider,
        Func<Camera> interactionCameraProvider,
        Func<int, int, bool> canRefreshChunkColumn,
        Action<int, int, int> refreshLoadedSubChunk,
        Action<bool, Vector3Int> setSelectionVisual)
    {
        _blockDatabase = blockDatabase;
        _interactionDistance = interactionDistance;
        _terrainProvider = terrainProvider;
        _worldRootProvider = worldRootProvider;
        _playerControllerProvider = playerControllerProvider;
        _interactionCameraProvider = interactionCameraProvider;
        _canRefreshChunkColumn = canRefreshChunkColumn;
        _refreshLoadedSubChunk = refreshLoadedSubChunk;
        _setSelectionVisual = setSelectionVisual;
    }

    public bool HasSelection => _hasSelection;
    public Vector3Int SelectedBlockPosition => _selectedBlockPosition;
    public ushort SelectedContentId => _hasSelection ? _selectedContentId : (ushort)0;
    public bool SelectedIsFoliage => _hasSelection && _selectedIsFoliage;
    public string SelectedContentKindLabel => !_hasSelection ? "None" : (_selectedIsFoliage ? "Foliage" : "Block");
    public string SelectedContentName => !_hasSelection || _blockDatabase == null ? "None" : _blockDatabase.GetDisplayName(_selectedContentId);

    public BlockType GetSelectedBlockType()
    {
        TerrainData terrain = _terrainProvider?.Invoke();
        return _hasSelection && !_selectedIsFoliage && terrain != null
            ? terrain.GetBlock(_selectedBlockPosition.x, _selectedBlockPosition.y, _selectedBlockPosition.z)
            : BlockType.Air;
    }

    public void HandlePlacementInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.digit1Key.wasPressedThisFrame)
        {
            _selectedPlacementBlock = BlockType.Dirt;
        }
        else if (keyboard.digit2Key.wasPressedThisFrame)
        {
            _selectedPlacementBlock = BlockType.Rock;
        }
    }

    public void UpdateSelection()
    {
        TerrainData terrain = _terrainProvider?.Invoke();
        Transform worldRoot = _worldRootProvider?.Invoke();
        if (terrain == null || worldRoot == null)
        {
            SetSelectionVisible(false);
            return;
        }

        Ray ray;
        FlyCamera playerController = _playerControllerProvider?.Invoke();
        if (playerController != null)
        {
            ray = playerController.GetInteractionRay();
        }
        else
        {
            Camera interactionCamera = _interactionCameraProvider?.Invoke();
            if (interactionCamera == null)
            {
                SetSelectionVisible(false);
                return;
            }

            ray = interactionCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        }

        if (!TrySelectContent(ray, terrain, worldRoot, out SelectionHit hit))
        {
            SetSelectionVisible(false);
            return;
        }

        _selectedBlockPosition = hit.position;
        _placementBlockPosition = hit.placementPosition;
        _selectedContentId = hit.contentId;
        _selectedIsFoliage = hit.isFoliage;
        _hasSelection = true;
        _setSelectionVisual?.Invoke(true, hit.position);
    }

    public void HandleEditingInput()
    {
        if (!_hasSelection || Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        TerrainData terrain = _terrainProvider?.Invoke();
        if (terrain == null)
        {
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame)
        {
            TryDestroySelectedBlock(terrain);
        }

        if (mouse.rightButton.wasPressedThisFrame)
        {
            TryPlaceSelectedBlock(terrain);
        }
    }

    private void TryDestroySelectedBlock(TerrainData terrain)
    {
        if (!terrain.IsInBounds(_selectedBlockPosition.x, _selectedBlockPosition.y, _selectedBlockPosition.z))
        {
            return;
        }

        if (_selectedIsFoliage)
        {
            if (!terrain.SetFoliageId(_selectedBlockPosition.x, _selectedBlockPosition.y, _selectedBlockPosition.z, 0))
            {
                return;
            }

            RefreshTouchedSubChunks(_selectedBlockPosition);
            SetSelectionVisible(false);
            return;
        }

        if (terrain.GetBlock(_selectedBlockPosition.x, _selectedBlockPosition.y, _selectedBlockPosition.z) == BlockType.Air)
        {
            return;
        }

        if (!terrain.SetBlock(_selectedBlockPosition.x, _selectedBlockPosition.y, _selectedBlockPosition.z, BlockType.Air))
        {
            return;
        }

        RefreshTouchedSubChunks(_selectedBlockPosition);
        SetSelectionVisible(false);
    }

    private void TryPlaceSelectedBlock(TerrainData terrain)
    {
        if (!terrain.IsInBounds(_placementBlockPosition.x, _placementBlockPosition.y, _placementBlockPosition.z))
        {
            return;
        }

        if (terrain.GetBlock(_placementBlockPosition.x, _placementBlockPosition.y, _placementBlockPosition.z) != BlockType.Air)
        {
            return;
        }

        if (terrain.GetFluid(_placementBlockPosition.x, _placementBlockPosition.y, _placementBlockPosition.z).Exists)
        {
            return;
        }

        if (!terrain.SetBlock(_placementBlockPosition.x, _placementBlockPosition.y, _placementBlockPosition.z, _selectedPlacementBlock))
        {
            return;
        }

        RefreshTouchedSubChunks(_placementBlockPosition);
    }

    private void RefreshTouchedSubChunks(Vector3Int blockPosition)
    {
        _subChunkRefreshBuffer.Clear();

        int chunkX = FloorDiv(blockPosition.x, TerrainData.ChunkSize);
        int subChunkY = FloorDiv(blockPosition.y, TerrainData.SubChunkSize);
        int chunkZ = FloorDiv(blockPosition.z, TerrainData.ChunkSize);

        AddSubChunkForRefresh(chunkX, subChunkY, chunkZ);

        int localX = Mod(blockPosition.x, TerrainData.ChunkSize);
        int localY = Mod(blockPosition.y, TerrainData.SubChunkSize);
        int localZ = Mod(blockPosition.z, TerrainData.ChunkSize);

        if (localX == 0)
        {
            AddSubChunkForRefresh(chunkX - 1, subChunkY, chunkZ);
        }
        else if (localX == TerrainData.ChunkSize - 1)
        {
            AddSubChunkForRefresh(chunkX + 1, subChunkY, chunkZ);
        }

        if (localY == 0)
        {
            AddSubChunkForRefresh(chunkX, subChunkY - 1, chunkZ);
        }
        else if (localY == TerrainData.SubChunkSize - 1)
        {
            AddSubChunkForRefresh(chunkX, subChunkY + 1, chunkZ);
        }

        if (localZ == 0)
        {
            AddSubChunkForRefresh(chunkX, subChunkY, chunkZ - 1);
        }
        else if (localZ == TerrainData.ChunkSize - 1)
        {
            AddSubChunkForRefresh(chunkX, subChunkY, chunkZ + 1);
        }

        for (int i = 0; i < _subChunkRefreshBuffer.Count; i++)
        {
            Vector3Int coords = _subChunkRefreshBuffer[i];
            _refreshLoadedSubChunk?.Invoke(coords.x, coords.y, coords.z);
        }
    }

    private void AddSubChunkForRefresh(int chunkX, int subChunkY, int chunkZ)
    {
        if (subChunkY < 0 || subChunkY >= TerrainData.SubChunkCountY)
        {
            return;
        }

        Vector2Int wrappedChunkCoords = TerrainData.WrapChunkCoords(chunkX, chunkZ);
        chunkX = wrappedChunkCoords.x;
        chunkZ = wrappedChunkCoords.y;

        if (_canRefreshChunkColumn == null || !_canRefreshChunkColumn(chunkX, chunkZ))
        {
            return;
        }

        Vector3Int coords = new(chunkX, subChunkY, chunkZ);
        for (int i = 0; i < _subChunkRefreshBuffer.Count; i++)
        {
            if (_subChunkRefreshBuffer[i] == coords)
            {
                return;
            }
        }

        _subChunkRefreshBuffer.Add(coords);
    }

    private void SetSelectionVisible(bool visible)
    {
        _hasSelection = false;
        _selectedContentId = 0;
        _selectedIsFoliage = false;
        _setSelectionVisual?.Invoke(visible, _selectedBlockPosition);
    }

    private bool TrySelectContent(Ray ray, TerrainData terrain, Transform worldRoot, out SelectionHit selectionHit)
    {
        selectionHit = default;

        Vector3 localOrigin = worldRoot.InverseTransformPoint(ray.origin);
        Vector3 localDirection = worldRoot.InverseTransformDirection(ray.direction).normalized;
        if (localDirection.sqrMagnitude < 0.000001f)
        {
            return false;
        }

        Vector3Int current = WorldPointToBlock(localOrigin);
        Vector3Int step = new(
            localDirection.x > 0f ? 1 : (localDirection.x < 0f ? -1 : 0),
            localDirection.y > 0f ? 1 : (localDirection.y < 0f ? -1 : 0),
            localDirection.z > 0f ? 1 : (localDirection.z < 0f ? -1 : 0));

        Vector3 tMax = new(
            CalculateInitialTMax(localOrigin.x, localDirection.x, current.x, step.x),
            CalculateInitialTMax(localOrigin.y, localDirection.y, current.y, step.y),
            CalculateInitialTMax(localOrigin.z, localDirection.z, current.z, step.z));

        Vector3 tDelta = new(
            CalculateTDelta(localDirection.x),
            CalculateTDelta(localDirection.y),
            CalculateTDelta(localDirection.z));

        float traveled = 0f;
        Vector3Int lastEmpty = current;
        bool hasLastEmpty = false;

        while (traveled <= _interactionDistance)
        {
            if (terrain.IsInBounds(current.x, current.y, current.z))
            {
                ushort foliageId = terrain.GetFoliageId(current.x, current.y, current.z);
                if (foliageId != 0)
                {
                    selectionHit = new SelectionHit
                    {
                        position = current,
                        placementPosition = current,
                        contentId = foliageId,
                        isFoliage = true,
                    };
                    return true;
                }

                BlockType blockType = terrain.GetBlock(current.x, current.y, current.z);
                if (blockType != BlockType.Air)
                {
                    selectionHit = new SelectionHit
                    {
                        position = current,
                        placementPosition = hasLastEmpty ? lastEmpty : current,
                        contentId = (ushort)blockType,
                        isFoliage = false,
                    };
                    return true;
                }

                lastEmpty = current;
                hasLastEmpty = true;
            }

            if (tMax.x < tMax.y)
            {
                if (tMax.x < tMax.z)
                {
                    current.x += step.x;
                    traveled = tMax.x;
                    tMax.x += tDelta.x;
                }
                else
                {
                    current.z += step.z;
                    traveled = tMax.z;
                    tMax.z += tDelta.z;
                }
            }
            else
            {
                if (tMax.y < tMax.z)
                {
                    current.y += step.y;
                    traveled = tMax.y;
                    tMax.y += tDelta.y;
                }
                else
                {
                    current.z += step.z;
                    traveled = tMax.z;
                    tMax.z += tDelta.z;
                }
            }
        }

        return false;
    }

    private static float CalculateInitialTMax(float originComponent, float directionComponent, int currentCell, int step)
    {
        if (step == 0 || Mathf.Abs(directionComponent) < 0.000001f)
        {
            return float.PositiveInfinity;
        }

        float nextBoundary = currentCell + (step > 0 ? 1f : 0f);
        return (nextBoundary - originComponent) / directionComponent;
    }

    private static float CalculateTDelta(float directionComponent)
    {
        return Mathf.Abs(directionComponent) < 0.000001f ? float.PositiveInfinity : Mathf.Abs(1f / directionComponent);
    }

    private static Vector3Int WorldPointToBlock(Vector3 point)
    {
        return new Vector3Int(
            Mathf.FloorToInt(point.x),
            Mathf.FloorToInt(point.y),
            Mathf.FloorToInt(point.z));
    }

    private static int Mod(int value, int modulus)
    {
        int result = value % modulus;
        return result < 0 ? result + modulus : result;
    }

    private static int FloorDiv(int value, int divisor)
    {
        if (value >= 0)
        {
            return value / divisor;
        }

        return ((value + 1) / divisor) - 1;
    }
}

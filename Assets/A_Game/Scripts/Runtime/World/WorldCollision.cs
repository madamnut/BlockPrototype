using UnityEngine;

public static class WorldCollision
{
    private const float CellEpsilon = 0.0001f;

    public readonly struct MoveResult
    {
        public readonly Vector3 position;
        public readonly bool grounded;
        public readonly bool hitCeiling;

        public MoveResult(Vector3 position, bool grounded, bool hitCeiling)
        {
            this.position = position;
            this.grounded = grounded;
            this.hitCeiling = hitCeiling;
        }
    }

    public static MoveResult MoveAabb(
        TerrainData terrain,
        Vector3 position,
        Vector3 displacement,
        float width,
        float height,
        float skinWidth)
    {
        if (terrain == null)
        {
            return new MoveResult(position + displacement, false, false);
        }

        bool grounded = false;
        bool hitCeiling = false;

        position = MoveAlongAxis(terrain, position, displacement.y, Axis.Y, width, height, skinWidth, ref grounded, ref hitCeiling);
        position = MoveAlongAxis(terrain, position, displacement.x, Axis.X, width, height, skinWidth, ref grounded, ref hitCeiling);
        position = MoveAlongAxis(terrain, position, displacement.z, Axis.Z, width, height, skinWidth, ref grounded, ref hitCeiling);
        return new MoveResult(position, grounded, hitCeiling);
    }

    public static bool IsGrounded(TerrainData terrain, Vector3 position, float width, float height, float distance)
    {
        if (terrain == null)
        {
            return false;
        }

        Bounds bounds = GetBounds(position, width, height);
        bounds.center += Vector3.down * (distance * 0.5f);
        bounds.size += new Vector3(0f, distance, 0f);
        return OverlapsSolid(terrain, bounds);
    }

    public static bool RaycastSolid(TerrainData terrain, Vector3 origin, Vector3 direction, float maxDistance, out float hitDistance)
    {
        hitDistance = 0f;
        if (terrain == null || maxDistance <= 0f)
        {
            return false;
        }

        Vector3 normalized = direction.normalized;
        if (normalized.sqrMagnitude <= 0.000001f)
        {
            return false;
        }

        Vector3Int current = new(
            Mathf.FloorToInt(origin.x),
            Mathf.FloorToInt(origin.y),
            Mathf.FloorToInt(origin.z));
        Vector3Int step = new(
            normalized.x > 0f ? 1 : (normalized.x < 0f ? -1 : 0),
            normalized.y > 0f ? 1 : (normalized.y < 0f ? -1 : 0),
            normalized.z > 0f ? 1 : (normalized.z < 0f ? -1 : 0));

        Vector3 tMax = new(
            CalculateInitialTMax(origin.x, normalized.x, current.x, step.x),
            CalculateInitialTMax(origin.y, normalized.y, current.y, step.y),
            CalculateInitialTMax(origin.z, normalized.z, current.z, step.z));
        Vector3 tDelta = new(
            CalculateTDelta(normalized.x),
            CalculateTDelta(normalized.y),
            CalculateTDelta(normalized.z));

        float traveled = 0f;
        while (traveled <= maxDistance)
        {
            if (terrain.IsInBounds(current.x, current.y, current.z) &&
                terrain.GetBlock(current.x, current.y, current.z) != BlockType.Air)
            {
                hitDistance = traveled;
                return true;
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

    private static Vector3 MoveAlongAxis(
        TerrainData terrain,
        Vector3 position,
        float delta,
        Axis axis,
        float width,
        float height,
        float skinWidth,
        ref bool grounded,
        ref bool hitCeiling)
    {
        if (Mathf.Abs(delta) <= 0.000001f)
        {
            return position;
        }

        Bounds bounds = GetBounds(position, width, height);
        float allowed = delta;

        switch (axis)
        {
            case Axis.X:
                allowed = SweepX(terrain, bounds, delta, skinWidth);
                position.x += allowed;
                break;

            case Axis.Y:
                allowed = SweepY(terrain, bounds, delta, skinWidth);
                if (delta < 0f && allowed > delta)
                {
                    grounded = true;
                }
                else if (delta > 0f && allowed < delta)
                {
                    hitCeiling = true;
                }

                position.y += allowed;
                break;

            default:
                allowed = SweepZ(terrain, bounds, delta, skinWidth);
                position.z += allowed;
                break;
        }

        return position;
    }

    private static float SweepX(TerrainData terrain, Bounds bounds, float delta, float skinWidth)
    {
        int minY = Mathf.FloorToInt(bounds.min.y + CellEpsilon);
        int maxY = Mathf.FloorToInt(bounds.max.y - CellEpsilon);
        int minZ = Mathf.FloorToInt(bounds.min.z + CellEpsilon);
        int maxZ = Mathf.FloorToInt(bounds.max.z - CellEpsilon);

        if (delta > 0f)
        {
            int startX = Mathf.FloorToInt(bounds.max.x - CellEpsilon) + 1;
            int endX = Mathf.FloorToInt(bounds.max.x + delta - CellEpsilon);
            float allowed = delta;

            for (int x = startX; x <= endX; x++)
            {
                if (!HasSolidInRange(terrain, x, x, minY, maxY, minZ, maxZ))
                {
                    continue;
                }

                allowed = Mathf.Min(allowed, x - bounds.max.x - skinWidth);
                break;
            }

            return Mathf.Max(allowed, 0f);
        }

        {
            int startX = Mathf.FloorToInt(bounds.min.x + CellEpsilon) - 1;
            int endX = Mathf.FloorToInt(bounds.min.x + delta + CellEpsilon);
            float allowed = delta;

            for (int x = startX; x >= endX; x--)
            {
                if (!HasSolidInRange(terrain, x, x, minY, maxY, minZ, maxZ))
                {
                    continue;
                }

                allowed = Mathf.Max(allowed, (x + 1f) - bounds.min.x + skinWidth);
                break;
            }

            return Mathf.Min(allowed, 0f);
        }
    }

    private static float SweepY(TerrainData terrain, Bounds bounds, float delta, float skinWidth)
    {
        int minX = Mathf.FloorToInt(bounds.min.x + CellEpsilon);
        int maxX = Mathf.FloorToInt(bounds.max.x - CellEpsilon);
        int minZ = Mathf.FloorToInt(bounds.min.z + CellEpsilon);
        int maxZ = Mathf.FloorToInt(bounds.max.z - CellEpsilon);

        if (delta > 0f)
        {
            int startY = Mathf.FloorToInt(bounds.max.y - CellEpsilon) + 1;
            int endY = Mathf.FloorToInt(bounds.max.y + delta - CellEpsilon);
            float allowed = delta;

            for (int y = startY; y <= endY; y++)
            {
                if (!HasSolidInRange(terrain, minX, maxX, y, y, minZ, maxZ))
                {
                    continue;
                }

                allowed = Mathf.Min(allowed, y - bounds.max.y - skinWidth);
                break;
            }

            return Mathf.Max(allowed, 0f);
        }

        {
            int startY = Mathf.FloorToInt(bounds.min.y + CellEpsilon) - 1;
            int endY = Mathf.FloorToInt(bounds.min.y + delta + CellEpsilon);
            float allowed = delta;

            for (int y = startY; y >= endY; y--)
            {
                if (!HasSolidInRange(terrain, minX, maxX, y, y, minZ, maxZ))
                {
                    continue;
                }

                allowed = Mathf.Max(allowed, (y + 1f) - bounds.min.y + skinWidth);
                break;
            }

            return Mathf.Min(allowed, 0f);
        }
    }

    private static float SweepZ(TerrainData terrain, Bounds bounds, float delta, float skinWidth)
    {
        int minX = Mathf.FloorToInt(bounds.min.x + CellEpsilon);
        int maxX = Mathf.FloorToInt(bounds.max.x - CellEpsilon);
        int minY = Mathf.FloorToInt(bounds.min.y + CellEpsilon);
        int maxY = Mathf.FloorToInt(bounds.max.y - CellEpsilon);

        if (delta > 0f)
        {
            int startZ = Mathf.FloorToInt(bounds.max.z - CellEpsilon) + 1;
            int endZ = Mathf.FloorToInt(bounds.max.z + delta - CellEpsilon);
            float allowed = delta;

            for (int z = startZ; z <= endZ; z++)
            {
                if (!HasSolidInRange(terrain, minX, maxX, minY, maxY, z, z))
                {
                    continue;
                }

                allowed = Mathf.Min(allowed, z - bounds.max.z - skinWidth);
                break;
            }

            return Mathf.Max(allowed, 0f);
        }

        {
            int startZ = Mathf.FloorToInt(bounds.min.z + CellEpsilon) - 1;
            int endZ = Mathf.FloorToInt(bounds.min.z + delta + CellEpsilon);
            float allowed = delta;

            for (int z = startZ; z >= endZ; z--)
            {
                if (!HasSolidInRange(terrain, minX, maxX, minY, maxY, z, z))
                {
                    continue;
                }

                allowed = Mathf.Max(allowed, (z + 1f) - bounds.min.z + skinWidth);
                break;
            }

            return Mathf.Min(allowed, 0f);
        }
    }

    private static bool OverlapsSolid(TerrainData terrain, Bounds bounds)
    {
        int minX = Mathf.FloorToInt(bounds.min.x + CellEpsilon);
        int maxX = Mathf.FloorToInt(bounds.max.x - CellEpsilon);
        int minY = Mathf.FloorToInt(bounds.min.y + CellEpsilon);
        int maxY = Mathf.FloorToInt(bounds.max.y - CellEpsilon);
        int minZ = Mathf.FloorToInt(bounds.min.z + CellEpsilon);
        int maxZ = Mathf.FloorToInt(bounds.max.z - CellEpsilon);
        return HasSolidInRange(terrain, minX, maxX, minY, maxY, minZ, maxZ);
    }

    private static bool HasSolidInRange(TerrainData terrain, int minX, int maxX, int minY, int maxY, int minZ, int maxZ)
    {
        for (int y = minY; y <= maxY; y++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (terrain.GetBlock(x, y, z) != BlockType.Air)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static Bounds GetBounds(Vector3 footPosition, float width, float height)
    {
        Vector3 size = new(width, height, width);
        Vector3 center = footPosition + (Vector3.up * (height * 0.5f));
        return new Bounds(center, size);
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

    private enum Axis : byte
    {
        X = 0,
        Y = 1,
        Z = 2,
    }
}

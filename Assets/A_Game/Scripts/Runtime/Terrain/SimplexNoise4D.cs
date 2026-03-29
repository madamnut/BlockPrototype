using System;

public sealed class SimplexNoise4D
{
    private const float F4 = 0.30901699437494745f;
    private const float G4 = 0.1381966011250105f;

    private static readonly int[][] Grad4 =
    {
        new[] { 0, 1, 1, 1 }, new[] { 0, 1, 1, -1 }, new[] { 0, 1, -1, 1 }, new[] { 0, 1, -1, -1 },
        new[] { 0, -1, 1, 1 }, new[] { 0, -1, 1, -1 }, new[] { 0, -1, -1, 1 }, new[] { 0, -1, -1, -1 },
        new[] { 1, 0, 1, 1 }, new[] { 1, 0, 1, -1 }, new[] { 1, 0, -1, 1 }, new[] { 1, 0, -1, -1 },
        new[] { -1, 0, 1, 1 }, new[] { -1, 0, 1, -1 }, new[] { -1, 0, -1, 1 }, new[] { -1, 0, -1, -1 },
        new[] { 1, 1, 0, 1 }, new[] { 1, 1, 0, -1 }, new[] { 1, -1, 0, 1 }, new[] { 1, -1, 0, -1 },
        new[] { -1, 1, 0, 1 }, new[] { -1, 1, 0, -1 }, new[] { -1, -1, 0, 1 }, new[] { -1, -1, 0, -1 },
        new[] { 1, 1, 1, 0 }, new[] { 1, 1, -1, 0 }, new[] { 1, -1, 1, 0 }, new[] { 1, -1, -1, 0 },
        new[] { -1, 1, 1, 0 }, new[] { -1, 1, -1, 0 }, new[] { -1, -1, 1, 0 }, new[] { -1, -1, -1, 0 },
    };

    private readonly int[] _perm = new int[512];

    public SimplexNoise4D(int seed)
    {
        int[] source = new int[256];
        for (int i = 0; i < source.Length; i++)
        {
            source[i] = i;
        }

        Random random = new(seed);
        for (int i = source.Length - 1; i >= 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            (source[i], source[swapIndex]) = (source[swapIndex], source[i]);
        }

        for (int i = 0; i < _perm.Length; i++)
        {
            _perm[i] = source[i & 255];
        }
    }

    public float Sample(float x, float y, float z, float w)
    {
        float s = (x + y + z + w) * F4;
        int i = FastFloor(x + s);
        int j = FastFloor(y + s);
        int k = FastFloor(z + s);
        int l = FastFloor(w + s);

        float t = (i + j + k + l) * G4;
        float x0 = x - (i - t);
        float y0 = y - (j - t);
        float z0 = z - (k - t);
        float w0 = w - (l - t);

        int rankX = 0;
        int rankY = 0;
        int rankZ = 0;
        int rankW = 0;

        if (x0 > y0) rankX++; else rankY++;
        if (x0 > z0) rankX++; else rankZ++;
        if (x0 > w0) rankX++; else rankW++;
        if (y0 > z0) rankY++; else rankZ++;
        if (y0 > w0) rankY++; else rankW++;
        if (z0 > w0) rankZ++; else rankW++;

        int i1 = rankX >= 3 ? 1 : 0;
        int j1 = rankY >= 3 ? 1 : 0;
        int k1 = rankZ >= 3 ? 1 : 0;
        int l1 = rankW >= 3 ? 1 : 0;
        int i2 = rankX >= 2 ? 1 : 0;
        int j2 = rankY >= 2 ? 1 : 0;
        int k2 = rankZ >= 2 ? 1 : 0;
        int l2 = rankW >= 2 ? 1 : 0;
        int i3 = rankX >= 1 ? 1 : 0;
        int j3 = rankY >= 1 ? 1 : 0;
        int k3 = rankZ >= 1 ? 1 : 0;
        int l3 = rankW >= 1 ? 1 : 0;

        float x1 = x0 - i1 + G4;
        float y1 = y0 - j1 + G4;
        float z1 = z0 - k1 + G4;
        float w1 = w0 - l1 + G4;
        float x2 = x0 - i2 + (2f * G4);
        float y2 = y0 - j2 + (2f * G4);
        float z2 = z0 - k2 + (2f * G4);
        float w2 = w0 - l2 + (2f * G4);
        float x3 = x0 - i3 + (3f * G4);
        float y3 = y0 - j3 + (3f * G4);
        float z3 = z0 - k3 + (3f * G4);
        float w3 = w0 - l3 + (3f * G4);
        float x4 = x0 - 1f + (4f * G4);
        float y4 = y0 - 1f + (4f * G4);
        float z4 = z0 - 1f + (4f * G4);
        float w4 = w0 - 1f + (4f * G4);

        int ii = i & 255;
        int jj = j & 255;
        int kk = k & 255;
        int ll = l & 255;

        float n0 = CornerNoise(ii, jj, kk, ll, 0, 0, 0, 0, x0, y0, z0, w0);
        float n1 = CornerNoise(ii, jj, kk, ll, i1, j1, k1, l1, x1, y1, z1, w1);
        float n2 = CornerNoise(ii, jj, kk, ll, i2, j2, k2, l2, x2, y2, z2, w2);
        float n3 = CornerNoise(ii, jj, kk, ll, i3, j3, k3, l3, x3, y3, z3, w3);
        float n4 = CornerNoise(ii, jj, kk, ll, 1, 1, 1, 1, x4, y4, z4, w4);

        return 27f * (n0 + n1 + n2 + n3 + n4);
    }

    private float CornerNoise(int ii, int jj, int kk, int ll, int iOffset, int jOffset, int kOffset, int lOffset, float x, float y, float z, float w)
    {
        float t = 0.6f - (x * x) - (y * y) - (z * z) - (w * w);
        if (t < 0f)
        {
            return 0f;
        }

        t *= t;
        int gradientIndex = _perm[ii + iOffset + _perm[jj + jOffset + _perm[kk + kOffset + _perm[ll + lOffset]]]] & 31;
        return t * t * Dot(Grad4[gradientIndex], x, y, z, w);
    }

    private static int FastFloor(float value)
    {
        int truncated = (int)value;
        return value < truncated ? truncated - 1 : truncated;
    }

    private static float Dot(int[] gradient, float x, float y, float z, float w)
    {
        return (gradient[0] * x) + (gradient[1] * y) + (gradient[2] * z) + (gradient[3] * w);
    }
}

namespace AbstractImg.Core.Rendering;

/// <summary>
/// Seeded 2D simplex noise (after Stefan Gustavson) with fractal helpers. Output is roughly -1..1.
/// </summary>
public sealed class SimplexNoise
{
    private static readonly (float X, float Y)[] Gradients =
    [
        (1, 1), (-1, 1), (1, -1), (-1, -1),
        (1, 0), (-1, 0), (0, 1), (0, -1),
        (0.7071f, 0.7071f), (-0.7071f, 0.7071f), (0.7071f, -0.7071f), (-0.7071f, -0.7071f)
    ];

    private const float F2 = 0.36602540378f; // (sqrt(3) - 1) / 2
    private const float G2 = 0.21132486540f; // (3 - sqrt(3)) / 6

    private readonly byte[] _perm = new byte[512];

    public SimplexNoise(int seed)
    {
        var p = new byte[256];
        for (var i = 0; i < 256; i++)
            p[i] = (byte)i;
        var rng = new Random(seed);
        for (var i = 255; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (p[i], p[j]) = (p[j], p[i]);
        }
        for (var i = 0; i < 512; i++)
            _perm[i] = p[i & 255];
    }

    public float Noise(float x, float y)
    {
        var s = (x + y) * F2;
        var i = FastFloor(x + s);
        var j = FastFloor(y + s);
        var t = (i + j) * G2;
        var x0 = x - (i - t);
        var y0 = y - (j - t);

        int i1, j1;
        if (x0 > y0)
        {
            i1 = 1;
            j1 = 0;
        }
        else
        {
            i1 = 0;
            j1 = 1;
        }

        var x1 = x0 - i1 + G2;
        var y1 = y0 - j1 + G2;
        var x2 = x0 - 1f + 2f * G2;
        var y2 = y0 - 1f + 2f * G2;

        var ii = i & 255;
        var jj = j & 255;

        var n = Corner(_perm[ii + _perm[jj]] % 12, x0, y0)
              + Corner(_perm[ii + i1 + _perm[jj + j1]] % 12, x1, y1)
              + Corner(_perm[ii + 1 + _perm[jj + 1]] % 12, x2, y2);
        return 70f * n;
    }

    private static float Corner(int gi, float x, float y)
    {
        var t = 0.5f - x * x - y * y;
        if (t < 0)
            return 0;
        t *= t;
        var g = Gradients[gi];
        return t * t * (g.X * x + g.Y * y);
    }

    /// <summary>Fractal Brownian motion, roughly -1..1.</summary>
    public float Fbm(float x, float y, int octaves = 4, float lacunarity = 2f, float gain = 0.5f)
    {
        float sum = 0, amp = 1, norm = 0;
        for (var o = 0; o < octaves; o++)
        {
            sum += Noise(x, y) * amp;
            norm += amp;
            amp *= gain;
            x *= lacunarity;
            y *= lacunarity;
        }
        return sum / norm;
    }

    /// <summary>Ridged multifractal, 0..1 with sharp crests.</summary>
    public float Ridged(float x, float y, int octaves = 4)
    {
        float sum = 0, amp = 0.5f, norm = 0;
        for (var o = 0; o < octaves; o++)
        {
            var n = 1f - MathF.Abs(Noise(x, y));
            sum += n * n * amp;
            norm += amp;
            amp *= 0.5f;
            x *= 2.03f;
            y *= 2.03f;
        }
        return sum / norm;
    }

    private static int FastFloor(float x)
    {
        var xi = (int)x;
        return x < xi ? xi - 1 : xi;
    }
}

using Pixelery.Core.Palettes;
using Pixelery.Core.Rendering;
using Pixelery.Core.Styles;
using SkiaSharp;

namespace Pixelery.Core.Tests;

public sealed class TruchetTests
{
    [Theory]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void MultiRingArcsMeetAcrossHorizontalAndVerticalTileBoundaries(int rings, bool alternateFlips)
    {
        const int size = 384;
        const int tile = 64;
        using var bitmap = new SKBitmap(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        var palette = new Palette("Contrast", SKColors.Black, [SKColors.White, SKColors.White], BackgroundTone.Dark);
        var context = new RenderContext(canvas, size, size, new RingRandom(rings, alternateFlips), palette, 0);
        new TruchetStyle().Render(context);
        canvas.Flush();

        var crossings = 0;
        for (var boundary = tile; boundary < size; boundary += tile)
        {
            var left = Crossings(offset => bitmap.GetPixel(boundary - 1, offset).Red);
            var right = Crossings(offset => bitmap.GetPixel(boundary, offset).Red);
            var top = Crossings(offset => bitmap.GetPixel(offset, boundary - 1).Red);
            var bottom = Crossings(offset => bitmap.GetPixel(offset, boundary).Red);
            AssertAligned(left, right);
            AssertAligned(top, bottom);
            crossings += left.Count + top.Count;
        }
        Assert.True(crossings > 50, "The seam check must include actual arc crossings.");

        static List<(int Start, int End)> Crossings(Func<int, byte> sample)
        {
            var result = new List<(int, int)>();
            var start = -1;
            for (var offset = 0; offset <= size; offset++)
            {
                var foreground = offset < size && sample(offset) >= 128;
                if (foreground && start < 0)
                    start = offset;
                else if (!foreground && start >= 0)
                {
                    result.Add((start, offset - 1));
                    start = -1;
                }
            }
            return result;
        }

        static void AssertAligned(List<(int Start, int End)> before, List<(int Start, int End)> after)
        {
            Assert.Equal(before.Count, after.Count);
            for (var i = 0; i < before.Count; i++)
            {
                // Rasterization may shift a half-covered edge by one pixel.
                Assert.InRange(Math.Abs(before[i].Start - after[i].Start), 0, 1);
                Assert.InRange(Math.Abs(before[i].End - after[i].End), 0, 1);
            }
        }
    }

    // Select the multi-ring variant and exercise both uniform and mixed tile orientations.
    private sealed class RingRandom(int rings, bool alternateFlips) : Random
    {
        private int _doubleCalls;
        public override int Next(int minValue, int maxValue) => (minValue, maxValue) switch
        {
            (0, 4) => 3,
            (2, 4) => rings,
            _ => minValue
        };
        public override int Next(int maxValue) => 0;
        public override double NextDouble()
        {
            _doubleCalls++;
            return alternateFlips && _doubleCalls > 3 && _doubleCalls % 2 == 0 ? 0.25 : 0.75;
        }
    }
}

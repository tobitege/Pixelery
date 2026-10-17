using Pixelery.Core.Palettes;
using Pixelery.Core.Rendering;
using Pixelery.Core.Styles;
using SkiaSharp;

namespace Pixelery.Core.Tests;

public sealed class HalftoneTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EqualFieldStepsProduceEvenTonalStepsOnDarkAndLightBackgrounds(bool light)
    {
        const int size = 512;
        var background = light ? SKColors.White : SKColors.Black;
        var ink = light ? SKColors.Black : SKColors.White;
        var palette = new Palette("Contrast", background, [ink, ink], light ? BackgroundTone.Light : BackgroundTone.Dark);
        using var bitmap = new SKBitmap(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        var context = new RenderContext(canvas, size, size, new RadialScreenRandom(), palette, 1);

        new HalftoneStyle().Render(context);
        canvas.Flush();

        // The radial field decreases linearly; average full rings to remove the dot-grid phase.
        var near = AverageCoverage(32);
        var middle = AverageCoverage(128);
        var far = AverageCoverage(224);
        var firstStep = near - middle;
        var secondStep = middle - far;
        Assert.True(firstStep > 15 && secondStep > 15, "The screen must retain visible tonal contrast.");
        Assert.InRange(Math.Abs(firstStep - secondStep) / Math.Max(firstStep, secondStep), 0, 0.1);
        // Field strengths are 0.9375 and 0.5625, so far/near coverage should stay close to 0.6.
        Assert.InRange(far / near, 0.55, 0.65);

        double AverageCoverage(double radius)
        {
            long sum = 0;
            var count = 0;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x + 0.5 - size / 2;
                    var dy = y + 0.5 - size / 2;
                    if (Math.Abs(Math.Sqrt(dx * dx + dy * dy) - radius) > 6)
                        continue;
                    var red = bitmap.GetPixel(x, y).Red;
                    sum += light ? 255 - red : red;
                    count++;
                }
            }
            return sum / (double)count;
        }
    }

    private sealed class RadialScreenRandom : Random
    {
        public override int Next(int minValue, int maxValue) => (minValue, maxValue) switch
        {
            (1, 4) => 1,
            (0, 3) => 1,
            _ => minValue
        };

        public override double NextDouble() => 0.5;
    }
}

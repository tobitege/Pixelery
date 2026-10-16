using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Pixelery.Core.Generation;
using Pixelery.Core.Output;
using Pixelery.Core.Palettes;
using Pixelery.Core.Styles;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace Pixelery.Services
{
    /// <summary>
    /// Renders style previews in the background (bounded parallelism) and converts them to bitmaps.
    /// </summary>
    internal static class PreviewRenderer
    {
        private static readonly SemaphoreSlim Gate = new(Math.Max(2, Environment.ProcessorCount - 1));

        /// <summary>Renders a JPEG preview using the enabled palettes and tones of <paramref name="settings"/>.</summary>
        public static async Task<byte[]> RenderAsync(ArtStyle style, int seed, GeneratorSettings settings, bool followColors, int width, int height, CancellationToken token)
        {
            await Gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                return await Task.Run(() =>
                {
                    var rng = new Random(seed);
                    var families = followColors && settings.Palettes.Count > 0 ? settings.Palettes : [.. Enum.GetValues<PaletteFamily>()];
                    var tones = followColors && settings.Tones.Count > 0 ? settings.Tones : [.. Enum.GetValues<BackgroundTone>()];
                    var details = settings.Details.Count > 0 ? settings.Details : [DetailLevel.Medium];
                    var (image, _) = ImageGenerator.Render(style, rng.Next(), families[rng.Next(families.Count)], tones[rng.Next(tones.Count)],
                        details[rng.Next(details.Count)], width, height, settings.Grain, settings.Vignette);
                    using (image)
                        return ImageEncoder.Encode(image, OutputFormat.Jpeg, 90, null);
                }, token).ConfigureAwait(false);
            }
            finally
            {
                Gate.Release();
            }
        }

        /// <summary>Creates a bitmap from encoded image bytes. Call on the UI thread.</summary>
        public static async Task<BitmapImage> ToBitmapAsync(byte[] bytes)
        {
            var bitmap = new BitmapImage();
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);
            await bitmap.SetSourceAsync(stream);
            return bitmap;
        }
    }
}

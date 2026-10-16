using System.Buffers.Binary;
using System.Text;
using Pixelery.Core.Generation;
using SkiaSharp;

namespace Pixelery.Core.Output;

/// <summary>
/// Encodes rendered images and optionally embeds a minimal EXIF block (date taken, make, model) into JPEG files
/// so photo apps can sort and group the generated images by date.
/// </summary>
public static class ImageEncoder
{
    public static string Extension(OutputFormat format) => format switch
    {
        OutputFormat.Png => ".png",
        OutputFormat.Webp => ".webp",
        _ => ".jpg"
    };

    public static byte[] Encode(SKImage image, OutputFormat format, int quality, ExifInfo? exif)
    {
        var skFormat = format switch
        {
            OutputFormat.Png => SKEncodedImageFormat.Png,
            OutputFormat.Webp => SKEncodedImageFormat.Webp,
            _ => SKEncodedImageFormat.Jpeg
        };
        using var data = image.Encode(skFormat, Math.Clamp(quality, 1, 100))
            ?? throw new InvalidOperationException($"Encoding to {format} failed.");
        var bytes = data.ToArray();
        return format == OutputFormat.Jpeg && exif is { } info ? InsertExif(bytes, info) : bytes;
    }

    /// <summary>Small JPEG preview (longest edge <paramref name="maxEdge"/>) for the progress display.</summary>
    public static byte[] Thumbnail(SKImage image, int maxEdge)
    {
        var scale = Math.Min(1f, maxEdge / (float)Math.Max(image.Width, image.Height));
        var w = Math.Max(1, (int)(image.Width * scale));
        var h = Math.Max(1, (int)(image.Height * scale));
        using var surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.DrawImage(image, new SKRect(0, 0, w, h), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        using var small = surface.Snapshot();
        using var data = small.Encode(SKEncodedImageFormat.Jpeg, 85);
        return data.ToArray();
    }

    private static byte[] InsertExif(byte[] jpeg, ExifInfo info)
    {
        if (jpeg.Length < 4 || jpeg[0] != 0xFF || jpeg[1] != 0xD8)
            return jpeg;

        var segment = BuildExifSegment(info);
        // Keep a JFIF APP0 segment first if the encoder wrote one.
        var insertAt = 2;
        if (jpeg[2] == 0xFF && jpeg[3] == 0xE0 && jpeg.Length > 6)
            insertAt = 4 + BinaryPrimitives.ReadUInt16BigEndian(jpeg.AsSpan(4));
        var result = new byte[jpeg.Length + segment.Length];
        Buffer.BlockCopy(jpeg, 0, result, 0, insertAt);
        Buffer.BlockCopy(segment, 0, result, insertAt, segment.Length);
        Buffer.BlockCopy(jpeg, insertAt, result, insertAt + segment.Length, jpeg.Length - insertAt);
        return result;
    }

    private static byte[] BuildExifSegment(ExifInfo info)
    {
        var date = Ascii(info.Date.ToString("yyyy:MM:dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture));
        var make = Ascii(info.Make);
        var model = Ascii(info.Model);
        var software = Ascii(info.Software);

        // TIFF layout (little endian): header, IFD0 (5 entries), Exif IFD (2 entries), then the value area.
        const int ifd0Offset = 8;
        const int ifd0Entries = 5;
        const int exifEntries = 2;
        var exifIfdOffset = ifd0Offset + 2 + ifd0Entries * 12 + 4;
        var dataOffset = exifIfdOffset + 2 + exifEntries * 12 + 4;

        var values = new List<byte>();
        int Place(byte[] value)
        {
            var offset = dataOffset + values.Count;
            values.AddRange(value);
            if (values.Count % 2 == 1)
                values.Add(0);
            return offset;
        }

        var makeOffset = Place(make);
        var modelOffset = Place(model);
        var softwareOffset = Place(software);
        var dateOffset = Place(date);
        var originalOffset = Place(date);
        var digitizedOffset = Place(date);

        var tiff = new byte[dataOffset + values.Count];
        var span = tiff.AsSpan();
        span[0] = (byte)'I';
        span[1] = (byte)'I';
        BinaryPrimitives.WriteUInt16LittleEndian(span[2..], 42);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], ifd0Offset);

        var p = ifd0Offset;
        BinaryPrimitives.WriteUInt16LittleEndian(span[p..], ifd0Entries);
        p += 2;
        p = Entry(span, p, 0x010F, 2, (uint)make.Length, (uint)makeOffset);
        p = Entry(span, p, 0x0110, 2, (uint)model.Length, (uint)modelOffset);
        p = Entry(span, p, 0x0131, 2, (uint)software.Length, (uint)softwareOffset);
        p = Entry(span, p, 0x0132, 2, (uint)date.Length, (uint)dateOffset);
        p = Entry(span, p, 0x8769, 4, 1, (uint)exifIfdOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(span[p..], 0);

        p = exifIfdOffset;
        BinaryPrimitives.WriteUInt16LittleEndian(span[p..], exifEntries);
        p += 2;
        p = Entry(span, p, 0x9003, 2, (uint)date.Length, (uint)originalOffset);
        p = Entry(span, p, 0x9004, 2, (uint)date.Length, (uint)digitizedOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(span[p..], 0);

        values.CopyTo(tiff, dataOffset);

        var header = "Exif\0\0"u8;
        var length = 2 + header.Length + tiff.Length;
        var segment = new byte[2 + length];
        segment[0] = 0xFF;
        segment[1] = 0xE1;
        BinaryPrimitives.WriteUInt16BigEndian(segment.AsSpan(2), (ushort)length);
        header.CopyTo(segment.AsSpan(4));
        tiff.CopyTo(segment, 4 + header.Length);
        return segment;
    }

    private static int Entry(Span<byte> span, int p, ushort tag, ushort type, uint count, uint value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(span[p..], tag);
        BinaryPrimitives.WriteUInt16LittleEndian(span[(p + 2)..], type);
        BinaryPrimitives.WriteUInt32LittleEndian(span[(p + 4)..], count);
        BinaryPrimitives.WriteUInt32LittleEndian(span[(p + 8)..], value);
        return p + 12;
    }

    /// <summary>Null-terminated ASCII, padded to more than four bytes so the value always lives in the data area.</summary>
    private static byte[] Ascii(string text)
    {
        var clean = new string([.. text.Select(c => c is >= ' ' and <= '~' ? c : '_')]);
        if (clean.Length < 4)
            clean = clean.PadRight(4);
        return [.. Encoding.ASCII.GetBytes(clean), 0];
    }
}

public readonly record struct ExifInfo(DateTime Date, string Make, string Model, string Software);

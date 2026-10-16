namespace Pixelery.Core.Watermark;

public enum WatermarkLayerKind
{
    Text,
    Image
}

/// <summary>How a layer is placed on the image.</summary>
public enum WatermarkPlacement
{
    /// <summary>Once, at <see cref="WatermarkLayer.Anchor"/>.</summary>
    Single,

    /// <summary>Repeated across the whole image.</summary>
    Tiled,

    /// <summary>Once, in a corner picked per image (harder to crop away).</summary>
    RandomCorner
}

public enum WatermarkAnchor
{
    TopLeft,
    Top,
    TopRight,
    Left,
    Center,
    Right,
    BottomLeft,
    Bottom,
    BottomRight
}

public enum WatermarkBlend
{
    Normal,
    Multiply,
    Screen,
    Overlay,
    SoftLight,
    Difference
}

/// <summary>
/// One watermark layer: a text or an image with its look and placement. Sizes are relative to the image
/// (percent of the shorter edge or of the width) so the watermark looks the same on every image size.
/// Colors are "#AARRGGBB".
/// </summary>
public sealed class WatermarkLayer
{
    public WatermarkLayerKind Kind { get; set; } = WatermarkLayerKind.Text;

    public bool Enabled { get; set; } = true;

    // Text

    /// <summary>
    /// Text with optional placeholders ({now:yyyy} current year, {date:yyyy} image date, {style}, {seed}, {n}, {w}, {h}, …);
    /// new lines allowed.
    /// </summary>
    public string Text { get; set; } = "© {now:yyyy}";

    public string FontFamily { get; set; } = "Segoe UI";

    public bool Bold { get; set; } = true;

    public bool Italic { get; set; }

    public bool Uppercase { get; set; }

    /// <summary>Font size in percent of the shorter image edge.</summary>
    public double Size { get; set; } = 4;

    /// <summary>Extra space between letters in percent of the font size.</summary>
    public double LetterSpacing { get; set; }

    public string Color { get; set; } = "#FFFFFFFF";

    public bool Outline { get; set; }

    public string OutlineColor { get; set; } = "#FF000000";

    /// <summary>Outline width in percent of the font size.</summary>
    public double OutlineWidth { get; set; } = 6;

    public bool Shadow { get; set; } = true;

    public string ShadowColor { get; set; } = "#99000000";

    /// <summary>Shadow blur and offset in percent of the font size.</summary>
    public double ShadowSize { get; set; } = 8;

    public bool Plate { get; set; }

    public string PlateColor { get; set; } = "#80000000";

    /// <summary>Space around the text on the plate, in percent of the font size.</summary>
    public double PlatePadding { get; set; } = 40;

    /// <summary>Plate corner radius in percent of the plate height (50 = pill).</summary>
    public double PlateRadius { get; set; } = 25;

    // Image

    public string ImagePath { get; set; } = string.Empty;

    /// <summary>Image width in percent of the image width.</summary>
    public double ImageWidth { get; set; } = 15;

    public bool Grayscale { get; set; }

    /// <summary>Fills the image with one color, keeping its transparency (stamp look).</summary>
    public bool Tint { get; set; }

    public string TintColor { get; set; } = "#FFFFFFFF";

    // Common

    /// <summary>0..100.</summary>
    public double Opacity { get; set; } = 80;

    public WatermarkBlend Blend { get; set; } = WatermarkBlend.Normal;

    public WatermarkPlacement Placement { get; set; } = WatermarkPlacement.Single;

    public WatermarkAnchor Anchor { get; set; } = WatermarkAnchor.BottomRight;

    /// <summary>Distance to the image edge in percent of the shorter edge.</summary>
    public double Margin { get; set; } = 3;

    /// <summary>Rotation in degrees.</summary>
    public double Rotation { get; set; }

    /// <summary>Gap between tiles in percent of the tile size.</summary>
    public double TileSpacing { get; set; } = 80;

    public WatermarkLayer Clone() => (WatermarkLayer)MemberwiseClone();

    public static WatermarkLayer NewText() => new();

    public static WatermarkLayer NewImage() => new()
    {
        Kind = WatermarkLayerKind.Image,
        Anchor = WatermarkAnchor.TopLeft,
        Shadow = false,
        Opacity = 90
    };
}

/// <summary>All watermark layers, drawn in order on top of every generated image.</summary>
public sealed class WatermarkSettings
{
    public bool Enabled { get; set; }

    /// <summary>Also draw the watermark in the slideshow.</summary>
    public bool ShowInSlideshow { get; set; }

    public List<WatermarkLayer> Layers { get; set; } = [WatermarkLayer.NewText()];

    public bool IsActive => Enabled && Layers.Any(l => l.Enabled);

    public WatermarkSettings Clone()
    {
        var clone = (WatermarkSettings)MemberwiseClone();
        clone.Layers = [.. Layers.Select(l => l.Clone())];
        return clone;
    }
}

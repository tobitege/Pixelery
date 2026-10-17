using System.Globalization;
using Pixelery.Core.Generation;

namespace Pixelery.Core.Tests;

public sealed class SizeInputTests
{
    [Theory]
    [InlineData("")]
    [InlineData("16:")]
    [InlineData(":9")]
    [InlineData("7:5")]
    [InlineData("16:9")]
    [InlineData("1,85")]
    [InlineData("1.85")]
    [InlineData("1.85:1.2")]
    [InlineData(" 16 : 9 ")]
    [InlineData("16x9")]
    [InlineData("16X9")]
    [InlineData("16×9")]
    [InlineData("16/9")]
    public void RatioInputAllowsVariableLengthsAndEditing(string text) => Assert.True(SizeCatalog.IsRatioInput(text));

    [Theory]
    [InlineData("abc")]
    [InlineData("16:9abc")]
    [InlineData("16::9")]
    [InlineData("16:9:1")]
    [InlineData("1.,85")]
    [InlineData("1.8.5")]
    [InlineData("-16:9")]
    [InlineData("1e3")]
    [InlineData("16:9\n")]
    [InlineData("16\t9")]
    [InlineData("١٦:٩")]
    public void RatioInputRejectsInvalidText(string text) => Assert.False(SizeCatalog.IsRatioInput(text));

    [Theory]
    [InlineData("", true)]
    [InlineData("1", true)]
    [InlineData("1920", true)]
    [InlineData("16384", true)]
    [InlineData("1920px", false)]
    [InlineData("1.920", false)]
    [InlineData("1920,5", false)]
    [InlineData("192 0", false)]
    [InlineData("+1920", false)]
    [InlineData("-1920", false)]
    [InlineData("123456", false)]
    [InlineData("1920\n", false)]
    public void ResolutionInputAllowsOnlyUpToFiveDigits(string text, bool accepted) =>
        Assert.Equal(accepted, SizeCatalog.IsEdgeInput(text));

    [Theory]
    [InlineData("")]
    [InlineData("1920x")]
    [InlineData("x1080")]
    [InlineData("64x32")]
    [InlineData("16384x16384")]
    [InlineData("1920X1080")]
    [InlineData("1920 × 1080")]
    [InlineData("1920*1080")]
    [InlineData("1920,1080")]
    [InlineData("1920 1080")]
    public void FixedSizeInputAllowsDifferentSeparatorsAndEditing(string text) => Assert.True(SizeCatalog.IsSizeInput(text));

    [Theory]
    [InlineData("1920x1080px")]
    [InlineData("1920xx1080")]
    [InlineData("1920x1080x720")]
    [InlineData("1920.5x1080")]
    [InlineData("1920:1080")]
    [InlineData("-1920x1080")]
    [InlineData("123456x1080")]
    [InlineData("1920x123456")]
    [InlineData("1920x1080\n")]
    public void FixedSizeInputRejectsInvalidText(string text) => Assert.False(SizeCatalog.IsSizeInput(text));

    [Theory]
    [InlineData("16:")]
    [InlineData(":9")]
    [InlineData("16::9")]
    [InlineData("16x")]
    [InlineData("/9")]
    public void RatioSubmissionRejectsMissingParts(string text) => Assert.False(SizeCatalog.TryParseRatio(text, out _, out _));

    [Fact]
    public void IncompleteOrOutOfRangeEditsStillFailSubmission()
    {
        Assert.True(SizeCatalog.IsRatioInput("16:"));
        Assert.False(SizeCatalog.TryParseRatio("16:", out _, out _));
        Assert.True(SizeCatalog.IsEdgeInput("16385"));
        Assert.False(SizeCatalog.TryParseEdge("16385", out _));
        Assert.True(SizeCatalog.IsSizeInput("1920x1"));
        Assert.False(SizeCatalog.TryParseSize("1920x1", out _, out _));
        Assert.False(SizeCatalog.IsRatioInput(new string('1', 65)));
        Assert.False(SizeCatalog.IsSizeInput(new string(' ', 65)));
    }

    [Theory]
    [InlineData("0.001:0.002", "0.002:0.001", 960)]
    [InlineData("1.2345:1", "1.2345:1", 1555)]
    public void CustomRatiosKeepTheirPrecisionThroughSelectionAndSharing(string input, string expected, int height)
    {
        Assert.True(SizeCatalog.IsRatioInput(input));
        Assert.True(SizeCatalog.TryParseRatio(input, out var width, out var ratioHeight));
        var ratio = SizeCatalog.NormalizeRatio(width, ratioHeight);
        Assert.Equal(expected, ratio);
        var settings = new GeneratorSettings { CustomAspectRatios = [ratio] };
        settings.SelectRatio(ratio, true);

        GeneratorSettings[] copies = [settings, SettingsJson.Deserialize(SettingsJson.Serialize(settings)),
            SettingsJson.Import(SettingsJson.Export(settings, "0.3.0"), new GeneratorSettings())];
        foreach (var copy in copies)
        {
            Assert.DoesNotContain("Validation_Sizes", copy.Validate());
            var size = Assert.Single(copy.BuildSizePool());
            Assert.Equal((1920, height), (size.Width, size.Height));
            Assert.Equal(expected.Replace(':', 'x'), size.Ratio);
        }
    }

    [Theory]
    [InlineData("de", "1,85:1,2")]
    [InlineData("en", "1.85:1.2")]
    [InlineData("fr", "1,85:1,2")]
    [InlineData("es", "1,85:1,2")]
    [InlineData("it", "1,85:1,2")]
    [InlineData("ja", "1.85:1.2")]
    [InlineData("ko", "1.85:1.2")]
    [InlineData("zh-CN", "1.85:1.2")]
    [InlineData("ar", "1٫85:1٫2")]
    [InlineData("tr", "1,85:1,2")]
    [InlineData("uk", "1,85:1,2")]
    [InlineData("he", "1.85:1.2")]
    public void LocalizedRatiosKeepTheSameStoredValueAndDimensions(string language, string display)
    {
        var culture = CultureInfo.GetCultureInfo(language);
        foreach (var input in new[] { display, "1,85:1,2", "1.85:1.2" })
        {
            Assert.True(SizeCatalog.IsRatioInput(input));
            Assert.True(SizeCatalog.TryParseRatio(input, out var width, out var height));
            var stored = SizeCatalog.NormalizeRatio(width, height);
            Assert.Equal("1.85:1.2", stored);
            Assert.Equal(display, SizeCatalog.FormatRatio(stored, culture));
            Assert.Equal(display, SizeCatalog.FormatRatio(input, culture));
            Assert.Equal("16:9", SizeCatalog.FormatRatio("16:9", culture));

            var settings = new GeneratorSettings { CustomAspectRatios = [stored] };
            settings.SelectRatio(stored, true);
            var copy = SettingsJson.Import(SettingsJson.Export(settings, "0.3.0"), new GeneratorSettings());
            Assert.Equal(stored, Assert.Single(copy.AspectRatios));
            Assert.Equal(stored, Assert.Single(copy.CustomAspectRatios));
            Assert.Equal("1.85:1.2", SizeCatalog.FormatRatio(copy.AspectRatios[0], CultureInfo.GetCultureInfo("en")));
            var size = Assert.Single(copy.BuildSizePool());
            Assert.Equal((1920, 1245, "1.85x1.2"), (size.Width, size.Height, size.Ratio));
        }
    }
}

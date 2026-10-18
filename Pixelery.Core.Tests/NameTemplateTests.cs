using Pixelery.Core.Generation;

namespace Pixelery.Core.Tests;

public sealed class NameTemplateTests
{
    [Theory]
    [InlineData("C:\\outside\\{n}")]
    [InlineData("C:outside\\{n}")]
    [InlineData("\\\\server\\share\\{n}")]
    [InlineData("/{n}")]
    [InlineData("{unknown}")]
    [InlineData("{rand:0}")]
    [InlineData("{rand:33}")]
    [InlineData("{style:invalid}")]
    [InlineData("{unfinished")]
    [InlineData("../{n}")]
    [InlineData("folder/./{n}")]
    [InlineData("NUL.txt/{n}")]
    [InlineData("bad|name_{n}")]
    [InlineData("bad\tname_{n}")]
    [InlineData("{n:D999999999}")]
    public void ParseRejectsRootedPathsAndInvalidTokens(string pattern)
    {
        Assert.False(NameTemplate.TryParse(pattern, out var template, out var error));
        Assert.Null(template);
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Theory]
    [InlineData("../..//CON/./name?. ", "_CON/name_")]
    [InlineData("..\\NUL\\COM1\\file*<>|", "_NUL/_COM1/file____")]
    [InlineData("./../ . /", "image")]
    [InlineData("CON.txt/file", "_CON.txt/file")]
    public void SanitizeRemovesTraversalAndWindowsSpecialNames(string input, string expected)
    {
        var actual = NameTemplate.Sanitize(input);
        Assert.Equal(expected.Replace('/', Path.DirectorySeparatorChar), actual);
        Assert.False(Path.IsPathRooted(actual));
    }

    [Fact]
    public void FormatKeepsSubfoldersAndSanitizesTokenValues()
    {
        Assert.True(NameTemplate.TryParse("{date:yyyy}/{palette}/{style:upper}_{n:000}_{w}x{h}", out var template, out _));
        var values = new NameValues(7, 10, "truchet", "geometric", 320, 180, "16x9", "landscape",
            "../../CON", 42, new DateTime(2026, 1, 2), new DateTime(2026, 1, 3), "png");
        var relative = template!.Format(values, new Random(1));
        Assert.Equal(Path.Combine("2026", "_CON", "TRUCHET_007_320x180"), relative);
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Pixelery-output"));
        var fullPath = Path.GetFullPath(Path.Combine(root, relative));
        Assert.StartsWith(root + Path.DirectorySeparatorChar, fullPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseBoundsPatternLengthWithoutRejectingFormattedDates()
    {
        Assert.False(NameTemplate.TryParse(new string('x', NameTemplate.MaxPatternLength + 1), out _, out _));
        Assert.True(NameTemplate.TryParse("{date:yyyy/MM/dd}/{n:00000}", out var template, out _));
        var values = new NameValues(7, 10, "truchet", "geometric", 320, 180, "16x9", "landscape",
            "neon", 42, new DateTime(2026, 1, 2), new DateTime(2026, 1, 3), "png");
        Assert.Equal(Path.Combine("2026", "01", "02", "00007"), template!.Format(values, new Random(1)));
    }
}

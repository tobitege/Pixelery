using System.Globalization;
using System.Text;
using System.Text.Json;
using Pixelery.Core.Generation;
using Pixelery.Core.Styles;

namespace Pixelery.Core.Tests;

public sealed class DateRangeTests
{
    [Fact]
    public void NewSettingsKeepActualTimestampsAndHaveAValidRange()
    {
        var settings = new GeneratorSettings();
        Assert.False(settings.SpreadDates);
        Assert.True(settings.HasValidDateRange());
        var now = new DateTime(2000, 5, 17, 12, 34, 56);
        Assert.Equal(now, Recipe(settings, 42, now).Date);
    }

    [Theory]
    [InlineData(2024, 2, 29)]
    [InlineData(2023, 2, 28)]
    [InlineData(2024, 4, 30)]
    [InlineData(2024, 12, 31)]
    [InlineData(9999, 12, 31)]
    public void AWholeMonthIncludesItsFirstAndLastDay(int year, int month, int days)
    {
        var settings = new GeneratorSettings
        {
            SpreadDates = true, SpreadFrom = new DateOnly(year, month, 1), SpreadTo = new DateOnly(year, month, 1)
        };
        var dates = Enumerable.Range(0, 512).Select(seed => Recipe(settings, seed, DateTime.MinValue).Date).ToList();
        Assert.All(dates, date =>
        {
            Assert.Equal(year, date.Year);
            Assert.Equal(month, date.Month);
            Assert.InRange(date.Day, 1, days);
        });
        Assert.Contains(dates, date => date.Day == 1);
        Assert.Contains(dates, date => date.Day == days);
    }

    [Fact]
    public void PeriodsCanSpanYearsAndDoNotDependOnTheReferenceDate()
    {
        var settings = new GeneratorSettings
        {
            SpreadDates = true, SpreadFrom = new DateOnly(2023, 12, 1), SpreadTo = new DateOnly(2024, 2, 1)
        };
        var dates = Enumerable.Range(0, 512).Select(seed =>
        {
            var date = Recipe(settings, seed, new DateTime(2000, 1, 1)).Date;
            Assert.Equal(date, Recipe(settings, seed, new DateTime(2099, 1, 1)).Date);
            return DateOnly.FromDateTime(date);
        }).ToList();
        Assert.All(dates, date => Assert.InRange(date, new DateOnly(2023, 12, 1), new DateOnly(2024, 2, 29)));
        Assert.Contains(new DateOnly(2023, 12, 1), dates);
        Assert.Contains(new DateOnly(2024, 2, 29), dates);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CloneSaveLoadAndSharingPreserveThePeriod(bool enabled)
    {
        var settings = new GeneratorSettings
        {
            SpreadDates = enabled, SpreadFrom = new DateOnly(2019, 11, 1), SpreadTo = new DateOnly(2024, 2, 1)
        };
        var file = Path.Combine(TestData.NewDirectory(), "settings.json");
        SettingsJson.Save(file, settings);
        GeneratorSettings[] copies = [settings.Clone(), SettingsJson.Load(file),
            SettingsJson.Import(SettingsJson.Export(settings, "0.3.0"), new GeneratorSettings())];
        Assert.All(copies, copy =>
        {
            Assert.Equal(enabled, copy.SpreadDates);
            Assert.Equal(settings.SpreadFrom, copy.SpreadFrom);
            Assert.Equal(settings.SpreadTo, copy.SpreadTo);
        });
    }

    [Theory]
    [InlineData("2024-03-01", "2024-02-01")]
    [InlineData("2024-02-02", "2024-03-01")]
    [InlineData("2024-02-01", "2024-03-02")]
    [InlineData("1600-01-01", "2024-03-01")]
    [InlineData("0001-01-01", "0001-01-01")]
    public void InvalidRangesAreRejectedBeforeCreatingOutput(string from, string to)
    {
        var settings = new GeneratorSettings
        {
            SpreadDates = true,
            SpreadFrom = DateOnly.ParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            SpreadTo = DateOnly.ParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            OutputFolder = Path.Combine(TestData.NewDirectory(), "must-not-be-created")
        };
        Assert.Contains("Validation_DateRange", settings.Validate());
        Assert.Throws<JsonException>(() => SettingsJson.Deserialize(SettingsJson.Serialize(settings)));
        Assert.Throws<JsonException>(() => SettingsJson.Import(SettingsJson.Export(settings, "0.3.0"), new GeneratorSettings()));
        Assert.Throws<ArgumentException>(() => ImageGenerator.Start(settings, CancellationToken.None, out _));
        Assert.Throws<ArgumentException>(() => Recipe(settings, 42, DateTime.Now));
        Assert.False(Directory.Exists(settings.OutputFolder));
    }

    [Fact]
    public async Task GeneratedFileNamesExifAndFileTimestampsUseTheSameSelectedDate()
    {
        var settings = new GeneratorSettings
        {
            OutputFolder = TestData.NewDirectory(), Count = 4, Threads = 2, Seed = "42",
            Styles = ["truchet"], AspectRatios = [], LongEdges = [], FixedSizes = ["96x64"],
            SpreadDates = true, SpreadFrom = new DateOnly(2024, 2, 1), SpreadTo = new DateOnly(2024, 2, 1),
            ReferenceDate = new DateOnly(2000, 1, 1), Formats = [OutputFormat.Jpeg], WriteExif = true,
            NamePattern = "{n:00}_{date:yyyy-MM-dd_HH-mm-ss}"
        };
        var run = ImageGenerator.Start(settings, CancellationToken.None, out var completion);
        await completion;
        Assert.Equal(0, run.Failed);
        Assert.Equal(settings.Count, Directory.GetFiles(settings.OutputFolder).Length);
        for (var i = 0; i < settings.Count; i++)
        {
            var seed = ImageGenerator.ImageSeed(42, i);
            var date = Recipe(settings, seed, settings.ReferenceTime()).Date;
            var name = $"{settings.StartIndex + i:00}_{date.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture)}.jpg";
            var file = Path.Combine(settings.OutputFolder, name);
            Assert.True(File.Exists(file), name);
            Assert.InRange(Math.Abs((File.GetCreationTime(file) - date).TotalMilliseconds), 0, 1);
            Assert.InRange(Math.Abs((File.GetLastWriteTime(file) - date).TotalMilliseconds), 0, 1);
            var exifDate = Encoding.ASCII.GetBytes(date.ToString("yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture));
            Assert.True(File.ReadAllBytes(file).AsSpan().IndexOf(exifDate) >= 0, "EXIF capture date must match the file timestamp.");
        }
    }

    private static ImageRecipe Recipe(GeneratorSettings settings, int seed, DateTime now) =>
        ImageGenerator.CreateRecipe(settings, settings.Styles.Select(StyleCatalog.Find).OfType<ArtStyle>().ToList(),
            settings.BuildSizePool(), seed, settings.StartIndex, now);
}

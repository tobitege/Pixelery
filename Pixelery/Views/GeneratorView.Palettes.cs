using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Automation;
using Pixelery.Core.Palettes;
using Pixelery.Localization;
using Pixelery.Services;
using static Pixelery.Views.UiHelpers;

namespace Pixelery.Views;

public sealed partial class GeneratorView
{
    private WrapPanel? _paletteSamples;
    private readonly List<DaisyButton> _paletteActions = [];
    private bool _paletteFileBusy;

    private StackPanel BuildPaletteSamples()
    {
        if (Settings.PaletteSet.Entries.Count == 0)
            Settings.PaletteSet.Shuffle(Settings.Palettes, Settings.Tones);
        _paletteSamples = new WrapPanel { MinItemWidth = 180, HorizontalSpacing = 10, VerticalSpacing = 10 };
        var toolbar = new WrapPanel { HorizontalSpacing = 8 };
        var shuffle = PaletteAction("Colors_ShuffleSamples", "PaletteShuffle", Symbol.Shuffle);
        shuffle.Variant = DaisyButtonVariant.Primary;
        shuffle.Click += (_, _) => ShufflePaletteSamples();
        var load = PaletteAction("Colors_LoadSet", "PaletteLoad", Symbol.OpenFile);
        load.Click += async (_, _) => await PaletteFileAsync(save: false);
        var save = PaletteAction("Colors_SaveSet", "PaletteSave", Symbol.Save);
        save.Click += async (_, _) => await PaletteFileAsync(save: true);
        toolbar.Children.Add(shuffle);
        toolbar.Children.Add(load);
        toolbar.Children.Add(save);
        var use = new DaisyCheckBox { IsChecked = Settings.UsePaletteSet, Variant = DaisyCheckBoxVariant.Primary };
        AutomationProperties.SetAutomationId(use, "UsePaletteSet");
        Localize(() => use.Content = L("Colors_UseSet"));
        use.Click += (_, _) =>
        {
            Settings.UsePaletteSet = use.IsChecked == true;
            PaletteSetChanged();
        };
        var body = new StackPanel { Spacing = 10 };
        body.Children.Add(toolbar);
        body.Children.Add(_paletteSamples);
        body.Children.Add(use);
        Localize(UpdatePaletteSamples);
        return body;
    }

    private DaisyButton PaletteAction(string key, string id, Symbol icon)
    {
        var button = new DaisyButton { Size = DaisySize.Medium, IconSymbol = icon, MinHeight = 40 };
        AutomationProperties.SetAutomationId(button, id);
        Localize(() => button.Content = L(key));
        _paletteActions.Add(button);
        return button;
    }

    private void ShufflePaletteSamples()
    {
        Settings.PaletteSet.Shuffle(Settings.Palettes, Settings.Tones);
        UpdatePaletteSamples();
        PaletteSetChanged();
    }

    private void PaletteSetChanged()
    {
        OnSettingsChanged();
        UpdateNamePreview();
        SchedulePreviewRefresh();
    }

    private void UpdatePaletteSamples()
    {
        if (_paletteSamples == null)
            return;
        _paletteSamples.Children.Clear();
        for (var i = 0; i < Settings.PaletteSet.Entries.Count; i++)
        {
            var index = i;
            var sample = Settings.PaletteSet.Entries[i];
            var palette = sample.ToPalette();
            var strip = new Grid { ColumnSpacing = 3, Height = 28 };
            foreach (var color in palette.Colors)
            {
                strip.ColumnDefinitions.Add(new ColumnDefinition());
                var swatch = new Border { CornerRadius = new CornerRadius(3), Background = PaletteBrush(color) };
                Grid.SetColumn(swatch, strip.Children.Count);
                ToolTipService.SetToolTip(swatch, color.ToString());
                strip.Children.Add(swatch);
            }
            var preview = new Border
            {
                Child = strip, Background = PaletteBrush(palette.Background), Padding = new Thickness(6),
                CornerRadius = new CornerRadius(6)
            };
            var name = Text(sample.Name, ResponsiveFontTier.Secondary);
            name.TextWrapping = TextWrapping.NoWrap;
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            var keep = new DaisyCheckBox { Content = L("Colors_Keep"), IsChecked = sample.Keep, Variant = DaisyCheckBoxVariant.Primary };
            AutomationProperties.SetAutomationId(keep, $"PaletteKeep{index + 1}");
            AutomationProperties.SetName(keep, $"{L("Colors_Keep")} · {index + 1} · {sample.Name}");
            var content = new StackPanel { Spacing = 6 };
            content.Children.Add(preview);
            content.Children.Add(name);
            content.Children.Add(keep);
            var frame = new Border { Child = content, Padding = new Thickness(8), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1) };
            void UpdateBorder() => frame.BorderBrush = DaisyResourceLookup.GetBrush(keep.IsChecked == true ? "DaisyPrimaryBrush" : "DaisyBase300Brush");
            UpdateBorder();
            keep.Click += (_, _) =>
            {
                Settings.PaletteSet.Entries[index] = Settings.PaletteSet.Entries[index] with { Keep = keep.IsChecked == true };
                UpdateBorder();
                UpdatePaletteActions();
                OnSettingsChanged();
            };
            ToolTipService.SetToolTip(preview, $"{sample.Name} · {L($"Palette_{sample.Family}")} · {L($"Tone_{sample.Tone}")}");
            _paletteSamples.Children.Add(frame);
        }
        UpdatePaletteActions();
    }

    private static SolidColorBrush PaletteBrush(SkiaSharp.SKColor color) => new(Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue));

    private void UpdatePaletteActions()
    {
        if (_paletteActions.Count != 3)
            return;
        _paletteActions[0].IsEnabled = !_paletteFileBusy && Settings.Palettes.Count > 0 && Settings.Tones.Count > 0
            && (Settings.PaletteSet.Entries.Count < PaletteSet.Size || Settings.PaletteSet.Entries.Any(entry => !entry.Keep));
        _paletteActions[1].IsEnabled = !_paletteFileBusy;
        _paletteActions[2].IsEnabled = !_paletteFileBusy && Settings.PaletteSet.IsValid();
    }

    private async Task PaletteFileAsync(bool save)
    {
        if (_shell == null || _paletteFileBusy)
            return;
        _paletteFileBusy = true;
        UpdatePaletteActions();
        try
        {
            var path = await _shell.PickPaletteSetFileAsync(save);
            if (path == null)
                return;
            if (save)
            {
                await Settings.PaletteSet.Clone().SaveAsync(path);
                AppSettings.RememberPaletteDirectory(path);
            }
            else
            {
                var loaded = await PaletteSet.LoadAsync(path);
                Settings.PaletteSet = loaded;
                UpdatePaletteSamples();
                PaletteSetChanged();
            }
            _shell.ShowToast(L(save ? "Colors_SetSaved" : "Colors_SetLoaded"), DaisyAlertVariant.Success);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            _shell.ShowToast(L(save ? "Colors_SetSaveError" : "Colors_SetLoadError"), DaisyAlertVariant.Error);
        }
        finally
        {
            _paletteFileBusy = false;
            UpdatePaletteActions();
        }
    }
}

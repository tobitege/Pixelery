<p align="center">
  <img src="Pixelery/Assets/app_icon.png" width="80" height="80" alt="Pixelery logo">
</p>

<h1 align="center">Pixelery</h1>

<p align="center">
  <img src="https://img.shields.io/badge/Windows-10%20%2F%2011%20%C2%B7%20x64-0078D4" alt="Platform: Windows 10 / 11, x64">
  <a href="https://github.com/tobitege/Pixelery/actions/workflows/ci.yml"><img src="https://github.com/tobitege/Pixelery/actions/workflows/ci.yml/badge.svg?branch=main" alt="Windows CI"></a>
  <a href="https://www.nuget.org/packages/Flowery.WinUI/1.6.0"><img src="https://img.shields.io/badge/NuGet-Flowery.WinUI%201.6.0-004880?logo=nuget&amp;logoColor=white" alt="NuGet: Flowery.WinUI 1.6.0"></a>
  <a href="https://github.com/tobitege/Pixelery/releases"><img src="https://img.shields.io/github/downloads/tobitege/Pixelery/total?logo=github" alt="Downloads"></a>
</p>

<p align="center">
  <span lang="en">Available in 12 languages</span> · <span lang="de">In 12 Sprachen verfügbar</span> · <span lang="fr">Disponible en 12 langues</span><br>
  <span lang="es">Disponible en 12 idiomas</span> · <span lang="it">Disponibile in 12 lingue</span> · <span lang="tr">12 dilde kullanılabilir</span><br>
  <span lang="ja">12言語に対応</span> · <span lang="ko">12개 언어 지원</span> · <span lang="zh-CN">支持 12 种语言</span><br>
  <span lang="uk">Доступно 12 мовами</span> · <span lang="ar" dir="rtl">متوفر بـ 12 لغة</span> · <span lang="he" dir="rtl">זמין ב־12 שפות</span>
</p>

![Pixelery: styles with live previews, generation settings and the run panel](banner.png)

Mass generator for abstract images: pick styles, colors and sizes, then write hundreds or thousands of images to a folder, for example as test data for galleries and photo viewers such as Flowery Photos. Add your own watermark, run an endless slideshow, or drive everything from the command line.

Built with [Flowery.WinUI](https://github.com/tobitege/Flowery.WinUI) (NuGet) and SkiaSharp.

## Projects

| Project | Purpose |
| --- | --- |
| `Pixelery` | Flowery.WinUI app, based on WinUI 3 (Daisy controls, themes, sizes, 12 languages) |
| `Pixelery.Core` | Generator engine without UI dependencies: styles, palettes, sizes, file name schema, watermarks, encoding, parallel runner |
| `Pixelery.Cli` | `Pixelery.Cli.exe` command line tool: generation, single images, desktop background, screen saver |
| `Pixelery.Core.Tests` | Tests for palette files, settings files, sizes, date ranges, safe file names, Truchet and halftone rendering, and signed updates |

## Features at a glance

- 37 styles in six categories, each with its own random variants
- Seven palette families and three background tones
- Random choices for styles, colors and formats; sizes can stay fixed, cycle in order or vary randomly
- Sizes from aspect ratios × orientations × long edges, or fixed presets and your own values
- Saved file name patterns with placeholders; `\` or `/` creates subfolders
- JPEG, PNG and WebP; optional file timestamp distribution and EXIF capture dates / camera model so photo apps can group by date
- Watermarks with any number of text and image layers
- Reproducible: the master seed plus the image index define every image; a settings file recreates a gallery on another PC
- Cancellable progress with speed, remaining time and a live thumbnail of the latest image
- Endless slideshow in full screen or a window, nothing saved
- Full command line control, including desktop background and screen saver

## Styles

Every style derives all decisions from the image seed, so the same seed gives the same image again.

| Category | Styles |
| --- | --- |
| Gradients & light | Mesh Gradient, Aurora, Gradient Field (linear, radial, conic, banded), Bokeh, Nebula |
| Geometric | Bauhaus, Truchet, Low Poly, Subdivision (Mondrian or modern tiles), Circle Packing, Memphis, Isometric Cubes, Halftone, Kaleidoscope |
| Organic | Flow Field, Marble, Topographic, Watercolor, Paper Cut, Ridgelines, Stained Glass |
| Procedural | Plasma, Fractal (Julia sets and Mandelbrot close-ups), Harmonograph (and spirographs) |
| Retro & digital | Synthwave, Glitch, Pixel Mosaic, Neon Glow |
| Optical illusions | Op Art, Rotating Snakes, Café Wall, Scintillating Grid (and Hermann grid), Fraser Spiral, Zöllner, Ouchi, Kanizsa, Impossible Figures (Penrose tribars) |

Cards and the preview list use bundled thumbnails at startup. You can regenerate them with the selected colors. The eye button opens a large preview with a full screen view (→ shows more variants, Space runs a slideshow of that style).

## Colors and finish

These options decide the colors and look of every image, regardless of its style.
Select one or more entries per group. For each image, Pixelery picks one palette family, one background and one level of detail at random from your selection.
The family creates a new palette for that image, and the image seed makes the choice repeatable.
The background sets the canvas: dark and light are tinted with the hue of a palette color, while vivid uses one of the most saturated palette colors, slightly darkened. Colors too close to the background are left out.
The level of detail controls how many shapes, lines or particles a style draws.
Film grain and vignette are applied on top of every finished image when enabled.

Instead of palette families, you can also use a palette set: eight saved palettes with their exact colors and backgrounds.
Each image then uses one of these eight. Keep individual palettes while shuffling the others, and share sets as JSON files.

- **Palette families:** curated (36 hand-picked palettes), color harmony (analogous, complementary, split, triadic, tetradic), cosine gradients, pastel, neon, earth tones, monochrome
- **Background:** dark, light or vivid
- **Level of detail:** low, medium, high
- **Finish:** film grain, vignette

## Dimensions

By default, select exactly one size: either a fixed preset or one aspect ratio combined with a horizontal resolution.
**Multiple selection** adds more choices and cycles through their combinations in order.
Enable **Random** to choose a size randomly for each image. This option requires multiple selection.
Each selection mode remembers its own choices when you switch modes or restart the app.

- **Orientation:** landscape by default, or portrait; applies to fixed presets as well as aspect ratios. Square images stay square.
- **Aspect ratios:** 1:1, 5:4, 4:3, 3:2, 16:10, 16:9, 2:1, 21:9, 32:9 and your own (e.g. 7:5 or 1.85)
- **Horizontal resolution:** 640 to 7,680 px and your own. This is the width in landscape mode and the height in portrait mode.
- **Fixed dimensions:** presets from Thumbnail to 24 MP, phone, portrait 4:5, square, A4 at 300 dpi, and your own (width × height)

## Output and file names

Select a saved file name pattern, or enter a new pattern and click **Add**.
You can remove saved patterns while keeping at least one. Duplicate entries are ignored after trimming surrounding spaces.
The app stores up to 100 patterns in your local settings.

| Placeholder | Value |
| --- | --- |
| `{n}`, `{n:00000}` | Running number (start number configurable), with zero padding |
| `{style}`, `{category}`, `{palette}` | Style id, style category, palette name (`:upper`, `:lower`, `:title` change the case) |
| `{w}`, `{h}`, `{ratio}`, `{orient}` | Width, height, aspect ratio (e.g. `16x9`, `7x5`), landscape/portrait/square |
| `{seed}` | Image seed (hex, `:d` for decimal) to reproduce the image |
| `{date:yyyy-MM-dd}` | Image date (random within the selected month range if enabled), .NET date format |
| `{now:yyyyMMdd_HHmmss}` | Time of the run, or the end of the reference date |
| `{rand:6}`, `{guid}` | Random letters and digits, unique id |
| `{fmt}`, `{total}` | File format, number of images in the run |

Example: `{date:yyyy}\{date:MM}\{style}_{n:00000}` creates year and month folders. The extension is added automatically; existing files are overwritten or get a numeric suffix.

Further options include formats (JPEG, PNG, WebP), quality, EXIF capture dates and camera model (JPEG), file replacement, and a reference date.
If you select several formats, each image uses a random format.
The reference date replaces the day of the run for `{now}`.
When timestamp distribution is off, it also sets the image date, EXIF dates and `{date}`.

**Spread file timestamps** is off by default. Files keep their actual generation timestamps.
Enable it to create test images with random file timestamps and capture dates between **From** and **To**, each with month and year.
Both months are included in full, including leap days. The period is independent of the reference date and can include future months.
If you move one endpoint past the other, the other endpoint moves to the same month.

## Sharing settings

**Export** in the Generate panel writes generation settings to a JSON file, including both remembered size selections.
The file excludes the target folder, thread count and local pattern collection. It includes only the selected file name pattern.
**Import** keeps this PC's folder, threads and pattern collection. It validates the imported pattern and adds it if it is new.
With the same file and Pixelery version, another PC creates the same images, names and image dates.
Filesystem timestamps remain the actual generation times unless **Spread file timestamps** is enabled.

Settings files require `FormatVersion: 1`. Unversioned files and other format versions are rejected; there is no migration.
If the app's local generator settings use the old format, the app starts with the new defaults.

- Export fixes an empty seed and an empty reference date: it picks a random seed and today's date, and sets both in the app as well.
- Import takes the styles exactly as written. A newer Pixelery does not add its new styles, as it does for its own saved settings.
- Import warns when the file comes from another Pixelery version, whose styles and palettes can look different, and when a watermark font is not installed. A missing watermark image shows up as a validation error.
- `{guid}` in the file name schema is random by design; `{rand}` follows the image seed.

## Watermarks

Any number of layers, drawn in order on top of every generated image (and optionally in the slideshow). All sizes are relative to the image, so the watermark looks the same on every image size. The editor shows a live preview on a sample image in landscape, portrait or square format.

**Text layers**

| Option | Details |
| --- | --- |
| Text | Several lines; placeholders filled in per image, e.g. `© {now:yyyy} Your Name`, `{style}`, `{seed}`, `#{n}`, `{w}×{h}` |
| Font | Any installed font; bold, italic, uppercase; right-to-left scripts and ligatures are shaped with HarfBuzz |
| Size | Font size in % of the shorter image edge |
| Letter spacing | Extra space between letters |
| Color | Any color with transparency |
| Outline | Color and width |
| Shadow | Color and size |
| Background plate | Color with transparency, padding, corner rounding (up to a pill shape) |

**Image layers**

| Option | Details |
| --- | --- |
| Image | Your logo or signature; PNG with transparency works best |
| Width | In % of the image width |
| Grayscale | Removes the colors |
| Single color (stamp) | Fills the logo with one color, keeping its transparency |
| Shadow | Soft drop shadow |

**For both**

| Option | Details |
| --- | --- |
| Opacity | 0 to 100 % |
| Blending | Normal, multiply, screen, overlay, soft light, difference |
| Position | Fixed (3 × 3 anchor grid), tiled across the whole image, or a random corner per image (harder to crop away) |
| Distance to the edge | For fixed positions and random corners |
| Tile spacing | For tiled watermarks |
| Rotation | −180° to 180° |

## Slideshow

The **Slideshow** tab shows newly generated images one after another with the selected styles and colors, without saving anything.

- Interval in seconds, full screen or window, cross-fade, info overlay (style, palette, seed), seed for a repeatable sequence
- The next image is rendered while the current one is shown, at the window's resolution
- Keys: **Esc** end, **Space** pause, **← →** previous / next, **I** info, **F** or double-click full screen

## Command line

`Pixelery.Cli.exe` generates images with the same engine and settings as the app, for example in scripts or scheduled tasks.
It can also set a desktop background or a screen saver image. The installer places it beside `Pixelery.exe`.

```powershell
.\Pixelery.Cli.exe generate --app-config -n 500                 # 500 images with the settings last used in the app
.\Pixelery.Cli.exe generate -c gallery.json -o D:\TestImages     # a shared settings file
.\Pixelery.Cli.exe setbackground -s aurora,nebula --tones dark   # new desktop background
```

See [CLI.md](CLI.md) for all commands and options.

## Build and run

```bash
dotnet build Pixelery.slnx
bin/Debug/Pixelery/net10.0-windows10.0.19041/win-x64/Pixelery.exe
```

Debug builds include [Chrome.DevTools.WinUI](https://github.com/wieslawsoltes/CDP) for UI inspection and automation.
The CDP server starts with the app on loopback port `9225` and stops when the main window closes.
Open `http://127.0.0.1:9225/json/list` to discover the window and its WebSocket endpoint.
Release builds exclude the CDP package and server.

## Release builds

Each GitHub release contains a per-user installer, `Pixelery-Setup-<version>-x64.exe`, and portable ZIP files for the app and the CLI.
Both are Native AOT builds and need no .NET or Windows App SDK installation.

The installer needs no administrator rights and installs the app and CLI to `%LOCALAPPDATA%\Programs\Pixelery`.
Use `/S` for a silent installation and `/D=<path>` as the last argument, without quotes, for another directory.
For a portable copy, extract a complete ZIP and keep all files beside the executable.
Settings are stored in `%LOCALAPPDATA%\Pixelery` (override with `PIXELERY_STATE_ROOT`) and survive updates and removal.

To build a release locally, you need the .NET SDK from `global.json` and Visual Studio C++ build tools with a Windows SDK (PowerShell 7):

```powershell
dotnet publish Pixelery/Pixelery.csproj -c Release -r win-x64 -p:Platform=x64 -o publish
dotnet publish Pixelery.Cli/Pixelery.Cli.csproj -c Release -r win-x64 -o publish-cli
$nsis = ./tools/Get-Nsis.ps1 -Directory "$env:LOCALAPPDATA/Pixelery/Tools"
./tools/New-Installer.ps1 -AppDirectory ./publish -CliDirectory ./publish-cli -OutputDirectory ./artifacts/packages -NsisPath $nsis
```

Run the tests and the CLI smoke test:

```powershell
dotnet test Pixelery.Core.Tests/Pixelery.Core.Tests.csproj -c Release
./tools/Test-CliSmoke.ps1 -CliPath ./publish-cli/Pixelery.Cli.exe -OutputDirectory "$env:LOCALAPPDATA/Pixelery/Tests/Smoke"
```

CI does the same for every pull request. A restore fails if any NuGet package has a known vulnerability.
Releases are created manually: Actions → **Pixelery Release** → Run workflow → version.

## Updates

Open **Settings → Updates** to check for a new release, download it and select **Install and restart**.
Pixelery never checks for or downloads updates in the background, and it verifies the signature of every download before installing it.
The update installs both the app and the CLI, keeps your settings and restarts Pixelery. Finish a running generation first.
With a portable copy, the update installs Pixelery for your user account and leaves the extracted files untouched.

## License

MIT License. Copyright (c) 2026 by @tobitege. See [LICENSE](LICENSE).

Repository: <https://github.com/tobitege/Pixelery>

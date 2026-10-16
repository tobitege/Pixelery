# Pixelery

![Pixelery: styles with live previews, generation settings and the run panel](banner.png)

Mass generator for abstract images: pick styles, colors and sizes, then write hundreds or thousands of images to a folder, for example as test data for galleries and photo viewers such as Flowery Photos. Add your own watermark, run an endless slideshow, or drive everything from the command line.

Built with [Flowery.WinUI](https://github.com/tobitege/Flowery.WinUI) (NuGet) and SkiaSharp.

## Projects

| Project | Purpose |
| --- | --- |
| `Pixelery` | WinUI 3 app (Daisy controls, themes, sizes, 12 languages) |
| `Pixelery.Core` | Generator engine without UI dependencies: styles, palettes, sizes, file name schema, watermarks, encoding, parallel runner |
| `Pixelery.Cli` | `pixelery` command line tool: generation, single images, desktop background, screen saver |

## Features at a glance

- 37 styles in six categories, each with its own random variants
- Seven palette families and three background tones
- Multiple choice everywhere: with several options selected, each image picks one at random
- Sizes from aspect ratios × orientations × long edges, plus fixed presets and your own values
- File name schema with placeholders; `\` or `/` creates subfolders
- JPEG, PNG and WebP; optional spread file dates and EXIF date taken / camera model so photo apps can group by date
- Watermarks with any number of text and image layers
- Reproducible: the master seed plus the image index define every image
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

Style cards in the app show live previews with the selected colors; the eye button opens a large preview with a full screen view (→ shows more variants, Space runs a slideshow of that style).

## Colors and finish

- **Palette families:** curated (36 hand-picked palettes), color harmony (analogous, complementary, split, triadic, tetradic), cosine gradients, pastel, neon, earth tones, monochrome
- **Background:** dark, light or vivid
- **Level of detail:** low, medium, high
- **Finish:** film grain, vignette

## Dimensions

All selected combinations form a pool; each image picks one size at random.

- **Orientation:** landscape, portrait (for all non-square ratios)
- **Aspect ratios:** 1:1, 5:4, 4:3, 3:2, 16:10, 16:9, 2:1, 21:9, 32:9 and your own (e.g. 7:5 or 1.85)
- **Long edge:** 640 to 7,680 px and your own
- **Fixed dimensions:** presets from Thumbnail to 24 MP, phone, portrait 4:5, square, A4 at 300 dpi, and your own (width × height)

## Output and file names

| Placeholder | Value |
| --- | --- |
| `{n}`, `{n:00000}` | Running number (start number configurable), with zero padding |
| `{style}`, `{category}`, `{palette}` | Style id, style category, palette name (`:upper`, `:lower`, `:title` change the case) |
| `{w}`, `{h}`, `{ratio}`, `{orient}` | Width, height, aspect ratio (e.g. `16x9`, `7x5`), landscape/portrait/square |
| `{seed}` | Image seed (hex, `:d` for decimal) to reproduce the image |
| `{date:yyyy-MM-dd}` | Image date (spread over the past years if enabled), .NET date format |
| `{now:yyyyMMdd_HHmmss}` | Time of the run |
| `{rand:6}`, `{guid}` | Random letters and digits, unique id |
| `{fmt}`, `{total}` | File format, number of images in the run |

Example: `{date:yyyy}\{date:MM}\{style}_{n:00000}` creates year and month folders. The extension is added automatically; existing files are overwritten or get a numeric suffix.

Further options: formats (JPEG, PNG, WebP; several pick at random), quality, spread file dates over N years, EXIF date taken and camera model (JPEG), overwrite existing files.

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

`pixelery` (project `Pixelery.Cli`) drives the same engine with the same settings as the app:

```bash
pixelery generate -o D:\TestImages -n 10000 -j max               # bulk generation with progress and Ctrl+C
pixelery generate --app-config -n 500                            # reuse the settings last used in the app
pixelery generate -s marble,aurora --palettes neon --tones dark --sizes "4K UHD" --formats png
pixelery generate -c my.json --dry-run                           # plan and example file names only
pixelery generate -n 500 --watermark-text "© {now:yyyy} Your Name" --watermark-position random
pixelery render marble.png -s marble --seed 42                   # one image into a file
pixelery setbackground -s aurora,nebula --tones dark             # one image at screen size as desktop background
pixelery setscreensaver -s geometric --activate                  # one image for the Windows Photos screen saver
pixelery list styles|palettes|sizes|placeholders
pixelery <command> --help
```

| Area | Options |
| --- | --- |
| Settings source | `-c/--config <file>`, `--app-config`, `--save-config <file>` |
| Styles and colors | `-s/--styles`, `--exclude`, `--palettes`, `--tones`, `--detail`, `--grain`, `--vignette` (lists are comma separated, `all` selects everything) |
| Sizes | `--orientation`, `--ratios`, `--edges`, `--sizes` (given size options replace the sizes of a loaded configuration) |
| Output | `-o/--out`, `-n/--count`, `-j/--threads` (`max` for all cores), `--seed`, `--formats`, `--quality`, `--name`, `--start`, `--overwrite`, `--spread-dates`, `--spread-years`, `--exif` |
| Watermark | `--watermark`/`--no-watermark`, `--watermark-text`, `--watermark-image`, `--watermark-position`, `--watermark-size`, `--watermark-opacity`, `--watermark-rotation`, `--watermark-color`, `--watermark-font` (everything else, such as several layers, outline, plate or blend modes, via `--config`) |
| Run | `--dry-run`, `-q/--quiet`, `--json` |

Switches can be negated with `--no-…`. Exit codes: 0 done, 1 failures, 2 invalid options, 130 cancelled.

`render`, `setbackground` and `setscreensaver` produce exactly one image and accept the style, color, size and watermark options. Without size options they render at the primary screen resolution. Backgrounds are kept in `%LOCALAPPDATA%\Pixelery\Wallpaper`; each screen saver image gets its own folder under `%LOCALAPPDATA%\Pixelery\Screensaver`.

Developer helpers: `pixelery sheet <folder> [seed] [styleId]` (contact sheets) and `pixelery bench [w] [h] [n]` (render time per style).

## Build and run

```bash
dotnet build Pixelery.slnx
bin/Debug/Pixelery/net10.0-windows10.0.19041/win-x64/Pixelery.exe
```

## Release builds

Both projects publish to a single self-contained, compressed exe (no .NET or Windows App SDK install needed):

```bash
dotnet publish Pixelery/Pixelery.csproj -c Release -r win-x64 -p:Platform=x64 -o publish        # Pixelery.exe, ~100 MB
dotnet publish Pixelery.Cli/Pixelery.Cli.csproj -c Release -r win-x64 -o publish-cli            # pixelery.exe, ~42 MB
```

GitHub releases are created manually: Actions → **Pixelery Release** → Run workflow → version (e.g. `0.1.0`).
The release gets `Pixelery-Windows-x64.exe` and `pixelery-cli-Windows-x64.exe`.

Settings are stored in `%LOCALAPPDATA%\Pixelery` (override with `PIXELERY_STATE_ROOT`).

## License

MIT License. Copyright (c) 2026 by @tobitege. See [LICENSE](LICENSE).

Repository: <https://github.com/tobitege/Pixelery>

# AbstractImg

Mass generator for abstract images: pick styles, colors and sizes, then write hundreds or thousands of images to a folder, for example as test data for galleries and photo viewers such as Flowery Photos.

Built with [Flowery.WinUI](https://github.com/tobitege/Flowery.WinUI) (NuGet) and SkiaSharp.

## Projects

| Project | Purpose |
| --- | --- |
| `AbstractImg` | WinUI 3 app (Daisy controls, themes, sizes, 12 languages) |
| `AbstractImg.Core` | Generator engine without UI dependencies: styles, palettes, sizes, file name schema, encoding, parallel runner |
| `AbstractImg.Cli` | Headless helper: contact sheets, per-style timing and command line generation |

## Features

- 29 styles in five categories (gradients & light, geometric, organic, procedural, retro & digital), each with internal random variants
- Palette families: curated, color harmony, cosine gradients, pastel, neon, earth tones, monochrome; dark, light or vivid backgrounds
- Multiple choice everywhere: with several options selected, each image picks one at random
- Sizes from aspect ratios × orientations × long edges, plus fixed presets and custom values
- File name schema with placeholders (`{n:00000}`, `{style}`, `{w}`, `{h}`, `{ratio}`, `{seed}`, `{date:yyyy}`, …); `\` creates subfolders
- JPEG, PNG and WebP; optional spread file dates and EXIF date taken / camera model (JPEG) so photo apps can group by date
- Reproducible: the master seed plus the image index define every image; `{seed}` puts the image seed into the file name
- Cancellable progress with speed, remaining time and a live thumbnail of the latest image

## Build and run

```bash
dotnet build AbstractImg.slnx
bin/Debug/AbstractImg/net10.0-windows10.0.19041/win-x64/AbstractImg.exe
```

CLI:

```bash
dotnet run --project AbstractImg.Cli -- sheet <folder> [seed] [styleId]   # contact sheets per category
dotnet run --project AbstractImg.Cli -- bench [width] [height] [n]        # render time per style
dotnet run --project AbstractImg.Cli -- gen <folder> [count]              # generate with default settings
```

Release build (one self-contained, compressed `AbstractImg.exe`, ~100 MB, no .NET or Windows App SDK install needed):

```bash
dotnet publish AbstractImg/AbstractImg.csproj -c Release -r win-x64 -p:Platform=x64 -o publish
```

GitHub releases are created manually: Actions → **AbstractImg Release** → Run workflow → version (e.g. `0.1.0`). The release gets `AbstractImg-Windows-x64.exe`.

Settings are stored in `%LOCALAPPDATA%\AbstractImg` (override with `ABSTRACTIMG_STATE_ROOT`).

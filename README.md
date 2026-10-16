# AbstractImg

Mass generator for abstract images: pick styles, colors and sizes, then write hundreds or thousands of images to a folder, for example as test data for galleries and photo viewers such as Flowery Photos.

Built with [Flowery.WinUI](https://github.com/tobitege/Flowery.WinUI) (NuGet) and SkiaSharp.

## Projects

| Project | Purpose |
| --- | --- |
| `AbstractImg` | WinUI 3 app (Daisy controls, themes, sizes, 12 languages) |
| `AbstractImg.Core` | Generator engine without UI dependencies: styles, palettes, sizes, file name schema, encoding, parallel runner |
| `AbstractImg.Cli` | `abstractimg` command line tool: generation, single images, desktop background, screen saver |

## Features

- 37 styles in six categories (gradients & light, geometric, organic, procedural, retro & digital, optical illusions), each with internal random variants
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

## Command line

`abstractimg` (project `AbstractImg.Cli`) drives the same engine with the same settings as the app:

```bash
abstractimg generate -o D:\TestImages -n 10000 -j max               # bulk generation with progress and Ctrl+C
abstractimg generate --app-config -n 500                            # reuse the settings last used in the app
abstractimg generate -s marble,aurora --palettes neon --tones dark --sizes "4K UHD" --formats png
abstractimg generate -c my.json --dry-run                           # plan and example file names only
abstractimg render marble.png -s marble --seed 42                   # one image into a file
abstractimg setbackground -s aurora,nebula --tones dark             # one image at screen size as desktop background
abstractimg setscreensaver -s geometric --activate                  # one image for the Windows Photos screen saver
abstractimg list styles|palettes|sizes|placeholders
abstractimg <command> --help
```

Every app option has a command line counterpart (`--styles`, `--exclude`, `--palettes`, `--tones`, `--detail`, `--grain`, `--vignette`,
`--orientation`, `--ratios`, `--edges`, `--sizes`, `--formats`, `--quality`, `--name`, `--start`, `--overwrite`, `--spread-years`, `--exif`,
`--seed`, `--count`, `--threads`). `--save-config` writes the effective settings as JSON in the app's format; `--json` prints a summary for scripts.
Exit codes: 0 done, 1 failures, 2 invalid options, 130 cancelled.

Developer helpers: `abstractimg sheet <folder> [seed] [styleId]` (contact sheets) and `abstractimg bench [w] [h] [n]` (render time per style).

## Release builds

Both projects publish to a single self-contained, compressed exe (no .NET or Windows App SDK install needed):

```bash
dotnet publish AbstractImg/AbstractImg.csproj -c Release -r win-x64 -p:Platform=x64 -o publish        # AbstractImg.exe, ~100 MB
dotnet publish AbstractImg.Cli/AbstractImg.Cli.csproj -c Release -r win-x64 -o publish-cli            # abstractimg.exe, ~42 MB
```

GitHub releases are created manually: Actions → **AbstractImg Release** → Run workflow → version (e.g. `0.1.0`).
The release gets `AbstractImg-Windows-x64.exe` and `abstractimg-cli-Windows-x64.exe`.

Settings are stored in `%LOCALAPPDATA%\AbstractImg` (override with `ABSTRACTIMG_STATE_ROOT`).

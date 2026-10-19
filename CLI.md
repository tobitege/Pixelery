# Pixelery command line

`Pixelery.Cli.exe` generates images with the same engine and settings as the [Pixelery app](README.md).
Use it for scripts and scheduled tasks, or to set a desktop background or screen saver image.
The installer places it beside `Pixelery.exe`; it is also available as a separate portable ZIP.

In PowerShell, run it as `.\Pixelery.Cli.exe` from the executable's directory.

## Examples

```bash
Pixelery.Cli.exe generate -o D:\TestImages -n 10000 -j max               # bulk generation with progress and Ctrl+C
Pixelery.Cli.exe generate --app-config -n 500                            # reuse the settings last used in the app
Pixelery.Cli.exe generate -s marble,aurora --palettes neon --tones dark --sizes "4K UHD" --formats png
Pixelery.Cli.exe generate -c my.json --dry-run                           # plan and example file names only
Pixelery.Cli.exe generate -n 500 --watermark-text "© {now:yyyy} Your Name" --watermark-position random
Pixelery.Cli.exe render marble.png -s marble --seed 42                   # one image into a file
Pixelery.Cli.exe setbackground -s aurora,nebula --tones dark             # one image at screen size as desktop background
Pixelery.Cli.exe setscreensaver -s geometric --activate                  # one image for the Windows Photos screen saver
Pixelery.Cli.exe list styles|palettes|sizes|placeholders
Pixelery.Cli.exe <command> --help
```

## Options

| Area | Options |
| --- | --- |
| Settings source | `-c/--config <file>`, `--app-config`, `--save-config <file>` |
| Styles and colors | `-s/--styles`, `--exclude`, `--palettes`, `--tones`, `--detail`, `--grain`, `--vignette` (lists are comma separated, `all` selects everything) |
| Sizes | `--multiple-sizes`, `--orientation`, `--ratios`, `--edges`, `--sizes`, `--random-sizes` |
| Output | `-o/--out`, `-n/--count`, `-j/--threads` (`max` for all cores), `--seed`, `--formats`, `--quality`, `--name`, `--start`, `--overwrite`, `--spread-dates`, `--spread-from` / `--spread-to` (`yyyy-MM`, inclusive), `--exif` |
| Watermark | `--watermark`/`--no-watermark`, `--watermark-text`, `--watermark-image`, `--watermark-position`, `--watermark-size`, `--watermark-opacity`, `--watermark-rotation`, `--watermark-color`, `--watermark-font` |
| Run | `--dry-run`, `-q/--quiet`, `--json` |

Switches can be negated with `--no-…`. Exit codes: 0 done, 1 failures, 2 invalid options, 130 cancelled.

## Settings files

Without a settings source, the CLI starts from the default settings. Options on the command line change the loaded settings.

- `-c/--config <file>` reads a file exported in the app (**Export** in the Generate panel). It takes the styles exactly as written, so the same file and Pixelery version create the same images, names and image dates as on any other PC.
- `--app-config` uses the settings last used in the app and, like the app, adds styles that are new in this version.
- `--save-config <file>` saves the resulting settings, including both remembered size selections and the active mode.

The reference date for `{now}` and the image dates can only be set through a settings file.

## File names

`--name <pattern>` sets the file name pattern with the same placeholders as the app; see [Output and file names](README.md#output-and-file-names) or run `list placeholders`.
`--start` sets the first value of `{n}`, and `--overwrite` replaces existing files instead of adding a numeric suffix.
`--spread-from` and `--spread-to` enable random file timestamps between two months, both included.

## Watermarks

`--watermark-text` and `--watermark-image` change the first text or image layer, add one if needed, and turn the watermark on.
`--watermark-text` accepts the file name placeholders and `\n` for a new line.
The other watermark options apply to those layers, or to all layers when neither is given.
`--watermark` and `--no-watermark` turn the configured watermark on or off.
For several layers, outlines, background plates or blend modes, design the watermark in the app, export the settings and use `--config`.

## Sizes

Size options replace the sizes of loaded settings; multiple choices cycle in order by default.
Use `--multiple-sizes` or `--no-multiple-sizes` to restore the remembered selection for that mode, just as in the app.
Then `--sizes`, `--ratios`, `--edges` and `--orientation` replace the choices in that mode.
Without a mode switch, explicit size choices determine whether the CLI uses single or multiple selection.
`--no-multiple-sizes` requires exactly one size and one orientation. Conflicting choices are rejected.
`--random-sizes` requires multiple selection; `--no-random-sizes` cycles through the choices in order.

```powershell
.\Pixelery.Cli.exe generate --multiple-sizes --sizes 640x480,1920x1080 --no-random-sizes --count 10
.\Pixelery.Cli.exe generate --config gallery.json --multiple-sizes --random-sizes
.\Pixelery.Cli.exe generate --config gallery.json --no-multiple-sizes --save-config single.json --dry-run
```

## Single images

`render`, `setbackground` and `setscreensaver` produce exactly one image and accept the style, color, size and watermark options.
Without size options they render at the primary screen resolution.
Backgrounds are kept in `%LOCALAPPDATA%\Pixelery\Wallpaper`; each screen saver image gets its own folder under `%LOCALAPPDATA%\Pixelery\Screensaver`.

## Developer helpers

`Pixelery.Cli.exe sheet <folder> [seed] [styleId]` creates contact sheets, and `Pixelery.Cli.exe bench [w] [h] [n]` measures the render time per style.

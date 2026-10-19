# Pixelery User Manual

Pixelery is a mass generator for abstract images for Windows. You pick styles, colors and sizes, and Pixelery writes hundreds or thousands of images into a folder — for example as test data for galleries and photo viewers such as Flowery Photos. You can add your own watermark, run an endless slideshow, or drive everything from the command line.

This manual describes the app. The command line tool has its own reference in [CLI.md](CLI.md).

---

## Contents

- [1. System requirements](#1-system-requirements)
- [2. Installation](#2-installation)
- [3. First steps](#3-first-steps)
- [4. The main window](#4-the-main-window)
- [5. Styles](#5-styles)
- [6. Colors and finish](#6-colors-and-finish)
- [7. Image dimensions](#7-image-dimensions)
- [8. Output and file names](#8-output-and-file-names)
- [9. Watermarks](#9-watermarks)
- [10. Generating images](#10-generating-images)
- [11. Slideshow](#11-slideshow)
- [12. Sharing settings](#12-sharing-settings)
- [13. App settings](#13-app-settings)
- [14. Updates](#14-updates)
- [15. Command line](#15-command-line)
- [16. Where Pixelery stores data](#16-where-pixelery-stores-data)
- [17. Troubleshooting](#17-troubleshooting)
- [18. Keyboard shortcuts](#18-keyboard-shortcuts)
- [19. License](#19-license)

---

## 1. System requirements

- Windows 10 or Windows 11, 64-bit (x64).
- No .NET runtime or Windows App SDK installation is required. The released app and command line tool are Native AOT builds.
- About 100 MB of free disk space for the installation, plus space for the images you generate.

## 2. Installation

### Installer

Download `Pixelery-Setup-<version>-x64.exe` from the [releases page](https://github.com/tobitege/Pixelery/releases).

- The installer runs for your user account only and needs **no administrator rights**.
- It installs the app and the command line tool to `%LOCALAPPDATA%\Programs\Pixelery`.
- It creates a Start menu shortcut and an uninstall entry.
- Command line options:
  - `/S` installs silently.
  - `/D=<path>` installs to another directory. This must be the **last** argument and written without quotes.

### Portable ZIP

- `Pixelery-Windows-x64.zip` — the app.
- `Pixelery.Cli-Windows-x64.zip` — the command line tool.

Extract a complete ZIP and keep all files beside the executable. The app and CLI do not need installation.

**Important:** Keep all files of a ZIP together. Do not move single files out of the folder.

## 3. First steps

1. Start Pixelery. The main window opens with a summary card, the configuration sections, and a run panel on the right.
2. Choose one or more **styles** in the Styles section (all styles are selected by default).
3. Check the **Colors** section: palette families, background tone, level of detail, and finish.
4. Check the **Dimensions** section. By default one size is selected: landscape, 16:9, 1920 px on the long edge.
5. In the run panel on the right, set the **number of images**, **threads**, and optionally a **seed**.
6. Click the **Start** button at the bottom of the run panel.

The first run writes images to `Pictures\Pixelery` (see [Section 8](#8-output-and-file-names) to change this).

## 4. The main window

The window has three areas:

- **Left icon bar** — jumps to Home (the configuration page) and to each configuration section.
- **Center** — the configuration: Styles, Colors, Dimensions, Watermark, Output. A footer shows the version, license and the GitHub repository link.
- **Right panel** — two tabs:
  - **Generate**: output folder, image count, threads, seed, settings file import/export, and live progress.
  - **Slideshow**: options for the endless slideshow (see [Section 11](#11-slideshow)).
  - The big **Start** button below the tabs starts the selected tab's action (generation or slideshow).

The title bar contains a running indicator, a GitHub button, and the **Settings** button (or `Ctrl+,`).

## 5. Styles

Pixelery includes **37 styles in six categories**. Every style derives all decisions from the image seed, so the same seed always produces the same image.

| Category | Styles |
| --- | --- |
| Gradients & light | Mesh Gradient, Aurora, Gradient Field (linear, radial, conic, banded), Bokeh, Nebula |
| Geometric | Bauhaus, Truchet, Low Poly, Subdivision (Mondrian or modern tiles), Circle Packing, Memphis, Isometric Cubes, Halftone, Kaleidoscope |
| Organic | Flow Field, Marble, Topographic, Watercolor, Paper Cut, Ridgelines, Stained Glass |
| Procedural | Plasma, Fractal (Julia sets and Mandelbrot close-ups), Harmonograph (and spirographs) |
| Retro & digital | Synthwave, Glitch, Pixel Mosaic, Neon Glow |
| Optical illusions | Op Art, Rotating Snakes, Café Wall, Scintillating Grid (and Hermann grid), Fraser Spiral, Zöllner, Ouchi, Kanizsa, Impossible Figures (Penrose tribars) |

### Selecting and previewing styles

- Select a style with its **selection indicator** in the top-left corner of the card (or the checkbox in list layouts).
- Clicking the thumbnail, title or the **eye button** opens a large preview.
- Each style can show multiple variants: click the **shuffle button** (→) in the preview to see more. Press **Space** to start a slideshow of that style.
- The preview has a full screen button; it opens on the monitor of the window that starts it.
- You can switch the layout of the style list in Settings: **Cards**, **Preview list** (default), **Text list**, or a **compact text layout** with two columns.

The small thumbnails are bundled with the app. The option "Previews follow colors" (Settings → Generation) regenerates them with your selected colors.

## 6. Colors and finish

These options decide the colors and look of every image, independent of the style. For each image, Pixelery picks one palette family, one background tone and one level of detail at random from your selection.

### Palette families

| Family | Description |
| --- | --- |
| Curated | 36 hand-picked palettes |
| Color harmony | Analogous, complementary, split, triadic, tetradic harmonies |
| Cosine gradients | Palettes derived from cosine gradient functions |
| Pastel | Soft pastel colors |
| Neon | Bright neon tones |
| Earth tones | Natural, muted colors |
| Monochrome | Single-hue palettes |

Each family creates a new palette for each image; the image seed makes the choice repeatable.

### Palette sets

Instead of (or in combination with the selection of) families, you can use a **palette set**: eight saved palettes with their exact colors and backgrounds. Each image then uses one of these eight.

- Use the **Keep** control on individual palettes to keep them while shuffling the others.
- Sets can be exported to and imported from **JSON files** (the app remembers the last export folder).

### Background tone

The background sets the canvas:

- **Dark** / **Light** — tinted with the hue of a palette color.
- **Vivid** — uses one of the most saturated palette colors, slightly darkened.

Colors too close to the background are left out.

### Level of detail

Controls how many shapes, lines or particles a style draws: **Low**, **Medium**, or **High**.

### Finish

- **Film grain** (on by default) — adds a subtle grain texture.
- **Vignette** (off by default) — darkens the edges.

Both are applied on top of every finished image.

## 7. Image dimensions

### Selection modes

- **Single selection** (default): pick exactly one size — a fixed preset, or one aspect ratio combined with a horizontal resolution.
- **Multiple selection**: add more choices. Pixelery cycles through their combinations in order.
- **Random**: with multiple selection enabled, each image gets a random size.

Each selection mode remembers its own choices when you switch modes or restart the app.

### Orientation

- **Landscape** (default) or **Portrait**.
- Orientation applies to fixed presets and to aspect ratios. Square images stay square.
- The "horizontal resolution" is the width in landscape mode and the height in portrait mode.

### Aspect ratios

Presets: `1:1, 5:4, 4:3, 3:2, 16:10, 16:9, 2:1, 21:9, 32:9`.

You can add your own ratios, for example `7:5` or `1.85` (interpreted as 1.85:1). Invalid characters are rejected while typing.

### Horizontal resolution (long edge)

Presets: `640, 1024, 1280, 1920, 2560, 3840, 5120, 7680` pixels. Custom values between 16 and 16,384 px are allowed.

### Fixed dimensions

Presets: Thumbnail (320×240), VGA (640×480), HD (1280×720), Full HD (1920×1080), QHD (2560×1440), 4K UHD (3840×2160), 8 MP, 12 MP, 24 MP, Phone, Portrait 4:5, Square, A4 at 300 dpi. Custom dimensions (width × height) are allowed, each side between 16 and 16,384 px.

## 8. Output and file names

### Output folder

The default folder is `Pictures\Pixelery`.

- Use the **Browse** button to pick another folder.
- The **Open** button opens the current folder in Explorer.
- The Generate tab shows the current folder; the arrow button next to it jumps to the Output section.

### File name patterns

Select a saved pattern, or type a new one and click **Add**. The app stores up to 100 patterns in your local settings. You can remove patterns as long as at least one remains. Duplicate entries are ignored after trimming surrounding spaces.

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

Example: `{date:yyyy}\{date:MM}\{style}_{n:00000}` creates year and month folders.

- The file extension is added automatically.
- Existing files are overwritten (if **Overwrite** is on) or get a numeric suffix.
- The app shows a live preview of the resulting file names below the pattern editor.

### Formats and quality

- **JPEG**, **PNG**, **WebP** — you can select several; each image then uses a random format.
- **Quality** (JPEG/WebP): 40–100 %, default 90.

### Dates and metadata

- **Write EXIF data** (on by default, JPEG only): capture date and camera model, so photo apps can group by date.
- **Spread file timestamps** (off by default): creates test images with random file timestamps and capture dates between **From** and **To** (month and year). Both months are included in full, including leap days. The period is independent of the reference date and can include future months. If you move one endpoint past the other, the other endpoint moves to the same month.
- **Reference date**: replaces the day of the run for `{now}`. When timestamp distribution is off, it also sets the image date, EXIF dates and `{date}`. Empty means the day of the run.
- **Start number**: first value of `{n}` (default 1).

## 9. Watermarks

You can add any number of watermark layers, drawn in order on top of every generated image (and optionally in the slideshow). All sizes are relative to the image, so the watermark looks the same on every image size. The editor shows a live preview on a sample image in landscape, portrait or square format.

The watermark is disabled by default. Enable **Apply watermark** in the Watermark section and click **Edit** to open the editor.

### Text layers

| Option | Details |
| --- | --- |
| Text | Several lines; placeholders are filled per image, e.g. `© {now:yyyy} Your Name`, `{style}`, `{seed}`, `#{n}`, `{w}×{h}` |
| Font | Any installed font; bold, italic, uppercase; right-to-left scripts and ligatures are shaped with HarfBuzz |
| Size | Font size in % of the shorter image edge |
| Letter spacing | Extra space between letters |
| Color | Any color with transparency |
| Outline | Color and width |
| Shadow | Color and size |
| Background plate | Color with transparency, padding, corner rounding (up to a pill shape) |

### Image layers

| Option | Details |
| --- | --- |
| Image | Your logo or signature; PNG with transparency works best |
| Width | In % of the image width |
| Grayscale | Removes the colors |
| Single color (stamp) | Fills the logo with one color, keeping its transparency |
| Shadow | Soft drop shadow |

### Common options

| Option | Details |
| --- | --- |
| Opacity | 0 to 100 % |
| Blending | Normal, multiply, screen, overlay, soft light, difference |
| Position | Fixed (3 × 3 anchor grid), tiled across the whole image, or a random corner per image (harder to crop away) |
| Distance to the edge | For fixed positions and random corners |
| Tile spacing | For tiled watermarks |
| Rotation | −180° to 180° |

## 10. Generating images

In the **Generate** tab:

- **Count** — 1 to 1,000,000 images. Preset buttons: 10, 100, 1,000, 10,000.
- **Threads** — number of parallel workers. The hint shows how many CPU cores your machine has. The default is half your core count; values up to 256 are allowed.
- **Seed** — the master seed. Empty means a new random seed for every run. The dice button picks a random seed. The master seed plus the image index define every image, so a saved settings file recreates a gallery on another PC.
- **Import / Export** — see [Section 12](#12-sharing-settings).
- A summary box shows what the current settings will produce, and validation messages appear if something is missing.

Click **Start**. The configuration is locked while a run is active; the Start button becomes **Cancel**.

### Progress display

- Progress bar, percentage, and count (`done / total`).
- Live thumbnail of the latest image (if "Live preview" is enabled in Settings).
- Statistics: **Rate** (images/second), **Elapsed**, **Remaining**, **Written** (bytes), **Per image** (average ms), **Failed**.
- The result is shown as a summary at the end: done, failed, cancelled, or an error message. If **Open folder when done** is enabled in Settings, the output folder opens automatically.

Generation is cancellable. To finish a partial run safely, finish or cancel it before installing an update (see [Section 14](#14-updates)).

## 11. Slideshow

The **Slideshow** tab shows newly generated images one after another with the selected styles and colors, **without saving anything**.

Options:

- **Interval** — seconds between images (1–3600, default 8).
- **Display** — full screen (default) or a window.
- **Crossfade** — smooth transition between images (on by default).
- **Info overlay** — shows style, palette and seed of the current image (off by default).
- **Seed** — a seed for a repeatable sequence; empty means a new random sequence every time.
- The watermark is drawn on slideshow images if "Also in slideshow" is enabled in the Watermark section.

The next image is rendered while the current one is shown, at the window's resolution.

**Keys:** `Esc` end, `Space` pause, `←` `→` previous/next, `I` info, `F` or double-click full screen.

## 12. Sharing settings

- **Export** (in the Generate panel) writes the generation settings to a JSON file. It includes both remembered size selections, but **excludes** the target folder, thread count and the local pattern collection — only the selected file name pattern is included.
- **Import** reads such a file. It keeps this PC's folder, threads and pattern collection; it validates the imported pattern and adds it if it is new.

With the same file and the same Pixelery version, another PC creates the same images, names and image dates. Filesystem timestamps remain the actual generation times unless "Spread file timestamps" is enabled.

Notes:

- Settings files require `FormatVersion: 1`. Unversioned files and other format versions are rejected; there is no migration.
- Export fixes an empty seed and an empty reference date: it picks a random seed and today's date, and sets both in the app as well.
- Import takes the styles exactly as written. A newer Pixelery does not add its new styles, as it does for its own saved settings.
- Import warns when the file comes from another Pixelery version (styles and palettes can look different) and when a watermark font is not installed. A missing watermark image shows up as a validation error.
- `{guid}` in the file name schema is random by design; `{rand}` follows the image seed.

## 13. App settings

Open **Settings** (gear icon in the title bar, or `Ctrl+,`). The dialog has five sections:

### Appearance

- **Theme** — Fluent Dark and Fluent Light at the top, plus the other built-in themes. Native controls and dropdowns follow the selected theme's light or dark mode.
- **Use Windows accent** — in the Fluent themes, use the Windows accent color instead of the standard blue.
- **Size** — global interface size (small/medium/large).
- **Language** — one of 12 languages: English, German, French, Spanish, Italian, Turkish, Japanese, Korean, Simplified Chinese, Ukrainian, Arabic, Hebrew. The app restarts with the new language.

### Generation

- **Live preview** — show a thumbnail of the latest image while generating (on by default). You can also adjust how often the preview updates (50–5000 ms).
- **Open folder when done** — automatically open the output folder after a finished run.
- **Previews follow colors** — regenerate style thumbnails with your selected palette families and tones (on by default).
- **Style view** — layout of the style list: Cards, Preview list, Text list, or compact text.

### Shortcuts

A summary of the global keyboard shortcuts (see [Section 18](#18-keyboard-shortcuts)).

### Updates

See [Section 14](#14-updates).

### About

Version, license and links: GitHub repository, Flowery.WinUI, and the **Open data folder** button (see [Section 16](#16-where-pixelery-stores-data)).

## 14. Updates

1. Open **Settings → Updates** and click **Check for updates**.
2. If a new release exists, click **Download**, then **Install and restart**.

Important facts:

- Pixelery **never** checks for or downloads updates in the background.
- Every download is **signature-verified** before installation.
- The update installs **both** the app and the command line tool, keeps your settings, and restarts Pixelery.
- **Finish a running generation first** — the update requires the app to close.
- With a portable copy, the update installs Pixelery for your user account and leaves the extracted files untouched.

## 15. Command line

`Pixelery.Cli.exe` uses the same generation engine and settings as the app. It is placed beside `Pixelery.exe` by the installer and is also available as a separate portable ZIP. Use it in scripts or scheduled tasks, or to set a desktop background or screen saver image.

Examples:

```powershell
.\Pixelery.Cli.exe generate --app-config -n 500                 # 500 images with the settings last used in the app
.\Pixelery.Cli.exe generate -c gallery.json -o D:\TestImages     # a shared settings file
.\Pixelery.Cli.exe setbackground -s aurora,nebula --tones dark   # new desktop background
```

Exit codes: 0 done, 1 failures, 2 invalid options, 130 cancelled.
All commands and options: see [CLI.md](CLI.md).

## 16. Where Pixelery stores data

- App and CLI settings are stored in `%LOCALAPPDATA%\Pixelery`. This includes the generator configuration (`generator.json`), app options, and the palette folder.
- Settings survive updates and uninstall (the uninstaller does not delete them).
- You can override the folder with the environment variable `PIXELERY_STATE_ROOT`.
- Wallpapers are kept in `%LOCALAPPDATA%\Pixelery\Wallpaper`; screen saver images get their own folders under `%LOCALAPPDATA%\Pixelery\Screensaver`.

## 17. Troubleshooting

| Problem | Solution |
| --- | --- |
| The Start button is disabled | The validation panel above the button names what is missing (e.g. no style, no palette, no size, no format, invalid pattern, invalid date range). |
| Files keep getting numeric suffixes | Enable **Overwrite** in the Output section, or clear the target folder. |
| Photos do not group by date | EXIF data is written for JPEG only; enable **Write EXIF data** and check the format. |
| Timestamps are all "now" | Enable **Spread file timestamps** and set the From/To months. |
| Imported settings look different | The file may come from another Pixelery version; check the warning shown during import. |
| A watermark image is missing | The watermark editor reports a validation error; choose an existing image file. |
| The app starts with fresh defaults | The local generator settings may use an old format; Pixelery then starts with the new defaults. Your options (theme, language, size) are kept. |
| The update fails | Finish or cancel any running generation, then retry. The app checks every download's signature. |

## 18. Keyboard shortcuts

| Shortcut | Action |
| --- | --- |
| `Ctrl+Enter` | Start generation (or slideshow, depending on the selected tab) |
| `Esc` | Cancel a running generation |
| `Ctrl+,` | Open / close Settings |
| `Space` | Toggle the selected style (in the style list) |

In the style preview: `Space` starts a slideshow of that style, `→` shows more variants.
In the slideshow: `Esc` end, `Space` pause, `←` `→` previous/next, `I` info, `F` full screen.

## 19. License

Pixelery is released under the MIT License. Copyright (c) 2026 by @tobitege. See [LICENSE](LICENSE).

Repository: <https://github.com/tobitege/Pixelery>

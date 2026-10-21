# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Added a guided tour on first start (#14). It points at the styles, colors, dimensions, watermark and output sections, the run panel, the slideshow tab and the settings, in all 12 languages. Completing or skipping it counts as seen; **Show tour again** in the settings replays it.

### Changed

- Update Flowery.WinUI to 1.7.0 for its `DaisyTour` control.

## [0.4.0] - 2026-10-19

### Added

- Audit direct and transitive NuGet packages on every restore and in a weekly workflow, and submit the resolved packages to the GitHub dependency graph.
- Added a downloads badge to the README.

### Changed

- Update Flowery.WinUI to 1.6.0.
- Update Microsoft.Windows.SDK.BuildTools to 10.0.28000.2705, as required by Flowery.WinUI 1.6.0.
- Refresh the README screenshot and badges. Explain how colors and finish apply, and shorten the release build and update notes for end users.
- Move the command line reference into `CLI.md`, including its use of settings files, file name patterns and watermarks. The README describes these features in the app.

## [0.3.0] - 2026-10-17

### Added

- Added a new Pixelery icon for the app, CLI and installer.
- Added Fluent Dark and Fluent Light at the top of the theme list, using the standard blue WinUI palette with an optional Windows accent. Native controls and dropdowns follow the selected theme's light or dark mode.
- Added export and import of the generator settings, so another PC can create the same gallery (#13). The file leaves out the target folder and the thread count. Export fixes an empty seed and an empty reference date; import warns about another Pixelery version or a missing watermark font.
- Added a reference date for `{now}` and, when timestamp distribution is off, the image date, EXIF dates and `{date}`.
- Added separate single and multiple size selections that survive mode changes and restarts, with ordered cycling or optional random selection.
- Added CLI switches for single/multiple size selection and ordered/random generation, including saved selections and mode details in the generation plan.
- Added a saved filename pattern list with add/remove actions, duplicate prevention and validation of imported patterns. Shared settings include only the selected pattern.
- Added translations for the new controls in all 12 languages.

### Changed

- Select styles only through their selection indicator; clicking the thumbnail, title or eye opens the preview. Cards place the larger selection indicator at the top left and the eye at the top right.
- `--config` reads the styles exactly as written; styles added in a newer Pixelery are no longer selected automatically. `--app-config` still selects them, like the app.
- Warning messages stay on screen for six seconds, like errors.
- Default to one landscape size. Orientation now applies to fixed presets as well as aspect ratios.
- Show the selected image size in one line below the size-mode controls. Use aligned dropdowns for ratios, resolution and presets, with checkboxes for multiple selection and compact inputs for custom values. Explain how orientation affects the horizontal resolution.
- Require settings format version 1 for local and shared generator settings; unversioned files are no longer accepted.
- Replace the past-years setting with an inclusive month/year period for file timestamps and capture dates, independent of the reference date. Timestamp distribution is off by default. Save and share both endpoints; the CLI accepts `--spread-from` and `--spread-to`.
- Show the start number and reference date in one row each.
- Add the app logo, platform and package badges, CI status and all 12 supported language notices to the README header.

### Fixed

- Open the full-screen style preview and slideshow on the monitor of the window that starts them.
- Reject invalid characters immediately when typing or pasting custom ratios, resolutions and dimensions.
- Preserve decimal precision in custom aspect ratios when selecting, saving and sharing settings. Display the decimal separator for the selected language while keeping stored values independent of the language.
- Smooth one-pixel style outlines and selection circles, with a softer selected color.
- Adjust the optical text alignment in buttons and badges through shared app padding tokens.
- Text action buttons and image-count presets now use the selected interface size from startup. Count presets wrap when they need more space.

## [0.2.0] - 2026-10-17

### Added

- Added a fixed icon sidebar to jump to Home and each configuration section.
- Show the output folder in the Generate sidebar, with a shortcut to its settings.
- Show the GitHub icon beside the repository link in the footer.
- Added all 12 installer languages and initialized the selected app language without replacing existing preferences.
- Added signed updates in Settings, with download verification, per-user installation, and automatic restart.
- Added signed release feeds and update tests, including tampered downloads and installation of both applications.
- Added four style layouts: Cards, preview list, text list, and a compact text layout with two columns.
- Added automatic slideshows in the preview dialog and fullscreen, with intervals from 1 to 999 seconds and a three-second default.
- Added reusable sets of eight palettes with individual Keep controls, JSON import and export, and optional use for image generation.
- Added translations for the new controls in all 12 languages.
- Added a local CDP server for UI inspection and automation in Debug builds only.
- Added an NSIS installer for the current user, with the app and command line tool in one directory, a Start menu shortcut, and uninstall support.
- Added installer build and lifecycle tests to CI and release workflows while keeping both portable ZIP downloads.

### Changed

- Upgrade SkiaSharp and SkiaSharp.HarfBuzz together to 4.153.1 and migrate rendering to the current path and image APIs.
- Update Flowery.WinUI to 1.4.0.
- Always install `Pixelery.exe` and `Pixelery.Cli.exe` together at the installation root. Upgrade previous installations from their separate component folders.
- Renamed the command line executable to `Pixelery.Cli.exe` and its portable archive to `Pixelery.Cli-Windows-x64.zip`.
- Compiled the Windows app and CLI with Native AOT. Release ZIP files include the required native libraries and assets.
- Reduced the top and left padding of the main content to 8 pixels.
- Added a 10-image shortcut to the generation count presets.
- Add a Start/Stop button for the preview slideshow, translated into all 12 languages. Preserve its pause state when returning from fullscreen.
- Made the preview list the default style layout and added a compact view selector.
- Included thumbnails for all 37 styles to avoid generation at startup. Added a CLI command to regenerate these thumbnails.
- Enlarged preview buttons and adjusted their size for each layout. Unselected styles now show an empty selection indicator.
- Replaced preview dimming with a one-pixel border in the secondary theme color during generation.
- Made palette controls larger and preserved Keep selections during palette regeneration.
- Remembered the last successful palette export folder across restarts, with the app data folder as the default location.
- Wrapped long output filenames, separated image details, and adapted sidebar statistics to the available width.

### Fixed

- Use continuous opacity for Halftone color transitions instead of changing subpixel dot sizes.
- Prevent concurrent installation and removal from changing the same files or registration.
- Show automatic update failures and restart the previous app after a successful rollback.
- Preserve update downloads while their ownership files are being created.
- Restore the previous installation if a locked file or failed replacement interrupts an update.
- Refresh style thumbnails when switching either to or from a saved palette set.
- Remove cancelled, invalid, superseded, and abandoned update downloads while preserving active installers.
- Stopped the header from growing after each background regeneration.
- Removed persistent focus rings after closing a style preview and used theme colors for selection borders.
- Kept progress percentages clear of the sidebar scrollbar.
- Aligned the arcs of neighboring Truchet tiles in patterns with multiple rings.
- Prevented crashes when reopening settings or changing the language after a style preview.
- Prevent settings, previews, and the watermark editor from opening over another dialog. Close settings dropdowns when their dialog closes.

### Security

- Reject installer and uninstaller manifest paths that traverse directory links outside the installation.
- Validated saved directory paths and limited automatic palette folder creation to the app data directory.

## [0.1.1] - 2026-10-16

### Changed

- Updated Flowery.WinUI to 1.3.2. The color picker dialog (watermark colors) is now translated into all 12 languages and no longer cuts off its right side in low windows.

## [0.1.0] - 2026-10-16

### Added

- First version: WinUI 3 app and `pixelery` command line tool for generating abstract images in bulk, with 37 styles in six categories, palettes, sizes, file name schema, watermarks, slideshow, desktop background and screen saver, and 12 languages.

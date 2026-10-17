# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0] - 2026-10-17

### Added

- Added all 12 installer languages and initialized the selected app language without replacing existing preferences.
- Added signed updates in Settings, with download verification, per-user installation, and automatic restart.
- Added signed release feeds and update tests, including tampered downloads and preservation of the optional CLI.
- Added four style layouts: Cards, preview list, text list, and a compact text layout with two columns.
- Added automatic slideshows in the preview dialog and fullscreen, with intervals from 1 to 999 seconds and a three-second default.
- Added reusable sets of eight palettes with individual Keep controls, JSON import and export, and optional use for image generation.
- Added translations for the new controls in all 12 languages.
- Added a local CDP server for UI inspection and automation in Debug builds only.
- Added an NSIS installer for the current user, with an optional command line tool, a Start menu shortcut, and uninstall support.
- Added installer build and lifecycle tests to CI and release workflows while keeping both portable ZIP downloads.

### Changed

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

- Stopped the header from growing after each background regeneration.
- Removed persistent focus rings after closing a style preview and used theme colors for selection borders.
- Kept progress percentages clear of the sidebar scrollbar.
- Aligned the arcs of neighboring Truchet tiles in patterns with multiple rings.
- Prevented crashes when reopening settings or changing the language after a style preview.
- Prevent settings, previews, and the watermark editor from opening over another dialog. Close settings dropdowns when their dialog closes.

### Security

- Validated saved directory paths and limited automatic palette folder creation to the app data directory.

## [0.1.1] - 2026-10-16

### Changed

- Updated Flowery.WinUI to 1.3.2. The color picker dialog (watermark colors) is now translated into all 12 languages and no longer cuts off its right side in low windows.

## [0.1.0] - 2026-10-16

### Added

- First version: WinUI 3 app and `pixelery` command line tool for generating abstract images in bulk, with 37 styles in six categories, palettes, sizes, file name schema, watermarks, slideshow, desktop background and screen saver, and 12 languages.

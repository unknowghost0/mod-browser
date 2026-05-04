# Mod Browser - Release Notes

## Version 3.4.7

### New Features
- **Staged External Updater**: Installs and updates are now staged in `!Mods\ModBrowser\PendingUpdates` and applied after the game closes.
- **Visible Update Console**: The updater opens a console window showing progress for each extraction and copy step.
- **Automatic Game Restart**: After updates finish, the updater restarts CastleMiner Z and closes the console automatically.
- **UPDATE ALL Button**: Added an `UPDATE ALL` action for updating every available mod update in the current browser source tab.
- **Source-Aware Updates**: `UPDATE ALL` respects the selected source tab, so `Official`, `Community`, and `All` update only the expected visible set.
- **Post-Update Verification**: When Mod Browser opens after an update, it checks installed DLLs and reports whether updates appear to be applied.

### Improvements
- **ZIP Update Support**: ZIP packages are extracted after the game closes, then copied into `!Mods`.
- **Locked DLL Handling**: Updates no longer try to replace loaded DLLs while CastleMiner Z is running.
- **One-Button Update Prompt**: Successful installs/updates show a single `OK` prompt that closes the game and starts the updater.
- **Partial Failure Handling**: Broken or missing downloads are skipped during bulk updates instead of failing the entire update run.
- **Clearer Status Messages**: Update messages now report staged updates, skipped mods, and verification results.
- **Safer Batch Flow**: The updater waits for the game to close, applies files, restarts the game, checks that the process started, and exits.

### Bug Fixes
- Fixed update-all behavior so it does not update community mods while only the official tab is selected, and vice versa.
- Fixed silent updater behavior by launching the batch through a visible `cmd.exe` window.
- Fixed ZIP extraction reliability by splitting archive extraction and file copying into separate checked steps.
- Fixed console staying open after successful updates.
- Fixed reload prompt behavior by replacing the old reload notice with the close-and-apply update flow.
- Fixed build error caused by missing `System.Globalization` import.

### Technical Changes
- Added staged package downloads for `.dll` and `.zip` assets.
- Added `ApplyModUpdates.bat` generation.
- Added external updater prompt state and UI rendering.
- Added source filtering helpers for update-all.
- Added batch error handling for failed extraction/copy steps.
- Added installed-version verification on Browser open.

---

## Version 2.1.0

### New Features
- **Improved Create Mod Workflow**: Added warning dialog when entering Create Mod tab to alert users that the feature is still in testing
- **Visual Studio Integration**: Enhanced "OPEN VS" button to properly locate Visual Studio and open mod projects (.csproj files)
- **Better Error Messages**: More informative status messages for common issues

### Bug Fixes
- **Fixed Cursor Positioning Bug**: Corrected critical issue where cursor position was calculated incorrectly when scrolling through long files
  - Cursor now stays on the correct line when scrolling
  - Typing position is now accurate
  - Text insertion matches visual cursor position
- **Optimized Text Clipping**: Replaced inefficient linear character-by-character clipping with binary search algorithm
  - Eliminated lag when rendering long lines
  - Dramatically improved performance with large files
- **Fixed Line Rendering**: Text no longer extends beyond editor boundaries
- **Improved Click Handling**: Fixed weird click behavior in the editor

### Known Issues
- Create Mod editor text rendering still has limitations with very long lines
- .csproj file editing is not recommended (use Visual Studio instead)

### Improvements
- Better mod template detection in Steam directory
- Cleaner status messages and warnings
- More robust error handling for missing mod folders

### Technical Changes
- Refactored cursor drawing logic to account for vertical scroll offset
- Improved text measurement caching
- Enhanced scissor rectangle handling for text rendering

---

## Version 2.0.0
Initial release with core mod browsing and management features.

### Features
- Browse official and community mods
- Search and filter mods
- Download and install mods
- View installed mods
- Create Mod (beta) - experimental mod creation tools

---

## Installation & Update

To update to v3.4.7:
1. Download the latest ModBrowser.dll
2. Replace the old DLL in your `!Mods\ModBrowser\` folder
3. Restart CastleForge

No configuration changes required.

---

## Support

For issues or feature requests, visit: https://github.com/unknowghost0/mod-browser

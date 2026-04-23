# Mod Browser - Release Notes

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

To update to v2.1.0:
1. Download the latest ModBrowser.dll
2. Replace the old DLL in your `!Mods\ModBrowser\` folder
3. Restart CastleForge

No configuration changes required.

---

## Support

For issues or feature requests, visit: https://github.com/unknowghost0/mod-browser

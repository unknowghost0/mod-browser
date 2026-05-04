# Mod Browser

GitHub-backed in-game browser and creator flow for CastleForge community content.

## What it does
- Adds `Mod Browser` to the main menu and pause menu.
- Loads the live CastleForge community catalog from GitHub.
- Shows mod details and preview images inside the game.
- Downloads and updates installable `.dll` and `.zip` mod packages from catalog release/download links.
- Stages installs and updates in `!Mods\ModBrowser\PendingUpdates`, then applies them with an external updater after the game closes.
- Adds `UPDATE ALL` support for updating all visible mods in the current source tab.
- Includes a `Create Mod` workspace that scaffolds:
  - a local CastleForge mod source folder
  - a PR-ready community entry folder for `CastleForge-CommunityMods`

## Live browser config
Generated at:

`<GameRoot>\!Mods\ModBrowser\ModBrowser.Config.ini`

Keys:
- `CatalogUrl`
- `HttpTimeoutSeconds`

For the live community browser, `CatalogUrl` should point to the raw generated index used by the community repo.

Example:

`https://raw.githubusercontent.com/RussDev7/CastleForge-CommunityMods/main/Index/mods.json`

## Install and update flow
Mod Browser v3.4.7 uses a staged updater for mod installs and updates.

When you install a mod or press `UPDATE ALL`:

1. Mod Browser resolves each mod's release/download URL.
2. Download packages are staged under:
   `<GameRoot>\!Mods\ModBrowser\PendingUpdates\<timestamp>\`
3. Mod Browser creates `ApplyModUpdates.bat`.
4. The game shows a one-button `OK` prompt.
5. Pressing `OK` opens a visible updater console and closes the game.
6. The updater waits for `CastleMinerZ.exe` to close.
7. It extracts `.zip` packages and copies `.dll`, `.json`, `.ini`, `.txt`, and `.md` files into `!Mods`.
8. It restarts CastleMiner Z.
9. The console closes itself after the game starts.

This flow avoids replacing loaded DLLs while the game is running.

## Update All behavior
- `UPDATE ALL` only updates mods that belong to the currently selected source tab:
  - `Official`
  - `Community`
  - `All`
- Mods that cannot resolve or download are skipped instead of stopping the whole update.
- Skipped mods are reported in the status message.
- The updater shows visible progress for each extraction/copy step.
- On the next Mod Browser open, installed mods are verified against the catalog and a status message reports whether anything still appears missing.

## Community repo workflow
The publish flow changed.

The community repo is now:

`https://github.com/RussDev7/CastleForge-CommunityMods`

It no longer uses the old one-file `submission.json` merge workflow for normal contributors.

Each community entry now lives in its own folder inside one category:

- `Mods/<ProjectName>/`
- `TexturePacks/<ProjectName>/`
- `WeaponAddons/<ProjectName>/`

Each entry folder should include:

- `mod.json`
- `README.md`
- `preview.png` or `preview.gif`

## What `Create Mod` now generates
When you press `CREATE FILES` in the in-game `Create Mod` view, Mod Browser now creates two things:

### 1. Local mod source scaffold
Created under:

`CastleForge-main\CastleForge\Mods\<YourModName>\`

Files include:

- `<YourModName>.csproj`
- `<YourModName>.cs`
- `Patching\GamePatches.cs`
- `Properties\AssemblyInfo.cs`
- `README.md`

### 2. Community PR entry scaffold
Created under:

`<GameRoot>\!Mods\ModBrowser\Publisher\CommunityEntries\<CategoryFolder>\<YourModName>\`

Files include:

- `mod.json`
- `README.md`
- `preview.png` or `preview.gif`
- `OPEN-PR.txt`

That folder is meant to be copied into your fork of `CastleForge-CommunityMods` before opening your PR.

## Community entry fields
The in-game creator now fills the modern community template fields, including:

- content type / category
- name
- slug
- author
- maintainers
- summary
- game version
- CastleForge version
- license
- source repo
- releases URL
- tags
- preview file name

## How to publish now
1. Open `Create Mod` in Mod Browser.
2. Fill in your mod info and press `CREATE FILES`.
3. Open the generated folder in:
   `!Mods\ModBrowser\Publisher\CommunityEntries\...`
4. Replace the placeholder preview with your real `preview.png` or `preview.gif`.
5. Fork `RussDev7/CastleForge-CommunityMods`.
6. Copy the generated entry folder into the matching category in your fork.
7. Commit your changes.
8. Open a pull request.

## Notes
- The browser side still reads the repo's generated catalog JSON.
- The creator side now targets the repo's folder-per-entry PR workflow.
- If a catalog entry only provides docs/source links and no direct DLL or ZIP asset, install may not be available from inside the browser.
- ZIP packages are extracted by the external updater after the game closes so locked DLLs can be safely replaced.

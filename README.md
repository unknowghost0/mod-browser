# Mod Browser

GitHub-backed in-game browser and creator flow for CastleForge community content.

## What it does
- Adds `Mod Browser` to the main menu and pause menu.
- Loads the live CastleForge community catalog from GitHub.
- Shows mod details and preview images inside the game.
- Downloads installable `.dll` files into the live `!Mods` folder when a catalog entry exposes a release/download link.
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
- If a catalog entry only provides docs/source links and no direct DLL asset, install may not be available from inside the browser.

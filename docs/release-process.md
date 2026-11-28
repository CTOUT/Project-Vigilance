# Project Vigilance: End-to-End Release Process

This document defines the authoritative, repeatable release procedure for **Project Vigilance** modules and suites across GitHub and the Steam Workshop.

---

## 1. Release Architecture & Channels

Each release publishes synchronised artifacts across two primary channels:

1. **GitHub Releases:**
   - Source code tag (`vX.Y.Z`).
   - Clean modular distribution archives (`dist/ProjectVigilance-Suite-vX.Y.Z.zip` and per-module packages `Core`, `Geoscape`, `Orbital`, `Aquatics`, `Thermal`) containing clean staged runtime assets (`About/`, `1.7/Assemblies/`, `1.7/Defs/`, `1.7/Patches/`, `1.7/Textures/`, `1.7/Languages/`, `LICENSE`, `README.md` — zero `Source/`, build caches, or `.pdb` symbol files).
   - Markdown release notes extracted directly from [CHANGELOG.md](../CHANGELOG.md).
2. **Steam Workshop & Steam Collection:**
   - **Steam Workshop Suite Items:**
     - **Core Framework ([#3847194681](https://steamcommunity.com/sharedfiles/filedetails/?id=3847194681)):** `Core/About/PublishedFileId.txt`
     - **Geoscape & Early Warning ([#3822378226](https://steamcommunity.com/sharedfiles/filedetails/?id=3822378226)):** `Geoscape/About/PublishedFileId.txt` (anchor item — never delete or overwrite)
     - **Orbital Logistics & Satellites ([#3847195514](https://steamcommunity.com/sharedfiles/filedetails/?id=3847195514)):** `Orbital/About/PublishedFileId.txt`
     - **Aquatic Operations & Sonar ([#3847195338](https://steamcommunity.com/sharedfiles/filedetails/?id=3847195338)):** `Aquatics/About/PublishedFileId.txt`
     - **Thermal Integrity & Materials ([#3847195139](https://steamcommunity.com/sharedfiles/filedetails/?id=3847195139)):** `Thermal/About/PublishedFileId.txt`
   - **Mod Descriptions & Artwork:** Per-module `About/Preview.png` (`< 1 MB`, `16:9`), `About/ModIcon.png` (`64×64`), and BBCode descriptions ([`docs/workshop_description_geoscape.bbcode`](workshop_description_geoscape.bbcode) + `_core`, `_orbital`, `_aquatics`, `_thermal`).
   - **Change Notes:** Versioned changelog entry in BBCode (`docs/workshop_changenotes_vX.Y.Z.txt`).
   - **Collection Description ([#3822385427](https://steamcommunity.com/sharedfiles/filedetails/?id=3822385427)):** BBCode overview of active and planned suite modules ([`docs/workshop_collection_description.bbcode`](workshop_collection_description.bbcode)).

---

## 2. Pre-Release Checklist

Before creating a release, ensure all items in the target milestone are complete:

- [ ] All milestone features implemented and verified in-game against `docs/milestone_X_Y_test_plan.md`.
- [ ] Distinct module artwork ready (`About/Preview.png` `16:9` `< 1 MB`, `About/ModIcon.png` `64×64`) for all 5 packages (`Core`, `Geoscape`, `Orbital`, `Aquatics`, `Thermal`) plus Steam Workshop gallery screenshots.
- [ ] Zero compilation warnings or errors: `dotnet build Geoscape/Source/Geoscape.slnx -c Release`.
- [ ] All automated unit tests passing: `dotnet test Geoscape/Source/Geoscape.slnx -c Release`.
- [ ] Zero `.pdb` files in `*/1.7/Assemblies/` or `dist/ProjectVigilance-*`.
- [ ] Zero spellcheck or formatting errors: `npm run spellcheck` and `npx prettier --check`.
- [ ] Working branch (`feat/milestone-X.Y`) merged or fast-forwarded into `main`.

---

## 3. Version Bump & Synchronisation

Update version numbers across the following files (or run `scripts/prepare-release.ps1 -Version X.Y.Z`):

| File                                                                        | Target Property / Location                                                            |
| :-------------------------------------------------------------------------- | :------------------------------------------------------------------------------------ |
| [`Directory.Build.props`](../Directory.Build.props)                         | Centralised `<Version>`, `<AssemblyVersion>`, `<FileVersion>` across all 5 packages   |
| [`package.json`](../package.json)                                           | `"version": "X.Y.Z"`                                                                  |
| [`CHANGELOG.md`](../CHANGELOG.md)                                           | Convert `## [Unreleased]` into `## [X.Y.Z] - YYYY-MM-DD` and add footer compare links |
| [`TODO.md`](../TODO.md)                                                     | Move shipped milestone items to `## Completed Milestones` and prune active roadmap    |
| [`README.md`](../README.md) & [`Geoscape/README.md`](../Geoscape/README.md) | Release badge (`Release-vX.Y.Z`) and playtest header links                            |
| [`scripts/package-release.ps1`](../scripts/package-release.ps1)             | Default `$Version = 'X.Y.Z'`                                                          |

---

## 4. Steam Workshop Copy Preparation

Maintain synchronization for community-facing Steam portals:

1. **Mod Descriptions ([`docs/workshop_description_geoscape.bbcode`](workshop_description_geoscape.bbcode) & sibling `workshop_description_*.bbcode`):**
   - Update version tags (e.g. `vX.Y LIVE`).
   - Add bullet points under **Key Features** for new capabilities.
   - Verify **Reporting Bugs & Feedback** directs users to GitHub Issues (preferred) and Steam Discussions, specifying `Player.log` and reproduction steps.
2. **Change Notes ([`docs/workshop_changenotes_vX.Y.Z.txt`](workshop_changenotes_v0.5.0.txt)):**
   - Create a dedicated BBCode change note formatted with `[h2]`, `[list]`, `[*]`, `[b]`, and `[i]`.
   - Organize into feature categories: new mechanics, tactical behaviors, fixes, and performance optimizations.
3. **Collection Description ([`docs/workshop_collection_description.bbcode`](workshop_collection_description.bbcode)):**
   - Update active module entry (e.g. `[vX.Y.Z LIVE]`).
   - Update upcoming modules roadmap.

---

## 5. Automated Build & Packaging

Run the automated release packaging pipeline:

```powershell
$ErrorActionPreference = 'Stop'
./scripts/prepare-release.ps1 -Version 'X.Y.Z'
```

This pipeline will:

1. Rebuild the release DLL (`Geoscape.dll`), redirecting `bin/` and `obj/` build caches to root `build/` via [`Directory.Build.props`](../Directory.Build.props).
2. Automatically run the MSBuild `StageCleanDistribution` target and copy clean runtime files (`About/`, `1.7/Assemblies/`, `1.7/Defs/`, `1.7/Patches/`, `1.7/Textures/`, `1.7/Languages/`, `LICENSE`, `README.md` — excluding `Source/`, `bin/`, `obj/`, `*.old`, and `Assets.zip*`) into `dist/ProjectVigilance-OverlandIncursions/`.
3. Run `npm run spellcheck`.
4. Generate the distribution ZIP archive: `dist/ProjectVigilance-OverlandIncursions-vX.Y.Z.zip`.
5. Validate asset sizes (e.g. `Preview.png` < 1 MB).
6. Extract release notes from `CHANGELOG.md` to `dist/release-notes-vX.Y.Z.md`.
7. Automatically switch the local RimWorld `Mods/ProjectVigilance-Geoscape` junction on Windows to `dist/ProjectVigilance-OverlandIncursions/` (~2.3 MB) unless `-SkipJunctionUpdate` is passed (`deploy-geoscape.ps1` and `deploy-geoscape.sh` also target clean `dist/` staging by default; pass `-Dev` / `--dev` to target raw `Geoscape/`).

---

## 6. Git Commit, Tag & GitHub Release

1. **Commit and Push on `main`:**

   ```powershell
   git add -A
   git commit -m "chore(release): prepare vX.Y.Z"
   git push origin main
   ```

2. **Create and Push Annotated Git Tag:**

   ```powershell
   git tag -a vX.Y.Z -m "Release vX.Y.Z: <Milestone Title>"
   git push origin vX.Y.Z
   ```

3. **Publish GitHub Release via GitHub CLI:**
   ```powershell
   gh release create vX.Y.Z dist/ProjectVigilance-OverlandIncursions-vX.Y.Z.zip `
     --title "Project Vigilance: Overland Incursions vX.Y.Z (Milestone X.Y)" `
     --notes-file dist/release-notes-vX.Y.Z.md
   ```

---

## 7. Steam Workshop Update

1. **Upload Mod Files via RimWorld:**
   - Ensure the local `Mods/ProjectVigilance-Geoscape` link points to clean `dist/ProjectVigilance-OverlandIncursions/` (~2.3 MB, no `Source/` or build caches — set automatically by `package-release.ps1`, `deploy-geoscape.ps1`, `deploy-geoscape.sh`, or `./scripts/switch-mod-junction.ps1 -Mode Workshop`).
   - Launch RimWorld 1.7.
   - Navigate to **Mods** -> Select **Project Vigilance - Overland Incursions**.
   - Click **Upload to Steam Workshop**.
   - Ensure the upload completes with exit code success.
   - _(Optional)_ After uploading, switch back to live repo development if needed via `./scripts/switch-mod-junction.ps1 -Mode Dev` (or `deploy-geoscape.ps1 -Dev` / `deploy-geoscape.sh --dev`).
2. **Update Workshop Metadata via Steam:**
   - Visit each Workshop Item Page: [Core (#3847194681)](https://steamcommunity.com/sharedfiles/filedetails/?id=3847194681), [Geoscape (#3822378226)](https://steamcommunity.com/sharedfiles/filedetails/?id=3822378226), [Orbital (#3847195514)](https://steamcommunity.com/sharedfiles/filedetails/?id=3847195514), [Aquatics (#3847195338)](https://steamcommunity.com/sharedfiles/filedetails/?id=3847195338), and [Thermal (#3847195139)](https://steamcommunity.com/sharedfiles/filedetails/?id=3847195139).
   - Click **Edit title & description** -> Paste contents of each module's `docs/workshop_description_<module>.bbcode` (e.g. [`docs/workshop_description_geoscape.bbcode`](workshop_description_geoscape.bbcode)).
   - Click **Add / edit change notes** -> Edit the auto-generated update entry and paste contents of [`docs/workshop_changenotes_vX.Y.Z.txt`](workshop_changenotes_v0.5.0.txt).
3. **Update Steam Collection:**
   - Visit the [Steam Collection Page](https://steamcommunity.com/sharedfiles/filedetails/?id=3822385427).
   - Click **Edit title and description** -> Paste contents of [`docs/workshop_collection_description.bbcode`](workshop_collection_description.bbcode).

---

## 8. Post-Release: Milestone Branch Orchestration

After completing the release on `main`:

```powershell
# Create or rebase the next milestone branch
git checkout -b feat/milestone-X.Y main
git push origin feat/milestone-X.Y
```

All ongoing development work for the next milestone should happen on this feature branch until ready for promotion to `main`.

---

## 9. RimWorld 1.7 (Waylith) References & Verification CLI

- **Isolated Mod Launch:** `RimWorldWin64.exe -mod=brrainz.harmony;ProjectVigilance.Core;ProjectVigilance.Geoscape` (tests exact mod sets without modifying `ModsConfig.xml`).
- **Log Path:** `AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Logs\Player.log`.
- **Official Modding References:**
  - [Modding Tutorials — RimWorld Wiki](https://rimworldwiki.com/wiki/Modding_Tutorials)
  - [RimWorld 1.7 Mod Updates — RimWorld Wiki](https://rimworldwiki.com/wiki/Modding_Tutorials/RimWorld_1.7_Mod_Updates)
  - [Modder Primer for 1.7 (Ludeon)](https://docs.google.com/document/d/e/2PACX-1vSykw5_DYaaw42nEznI9zmBZVT_Vgg2bxYq_v2XWvAsHC3QSKFuDK2OyiQ5WC9jvVRUcrjpFt7xLLF5/pub)

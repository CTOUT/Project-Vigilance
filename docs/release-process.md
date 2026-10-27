# Project Vigilance: End-to-End Release Process

This document defines the authoritative, repeatable release procedure for **Project Vigilance** modules and suites across GitHub and the Steam Workshop.

---

## 1. Release Architecture & Channels

Each release publishes synchronised artifacts across two primary channels:

1. **GitHub Releases:**
   - Source code tag (`vX.Y.Z`).
   - Distribution archive (`ProjectVigilance-OverlandIncursions-vX.Y.Z.zip`) containing clean staged assets (`About/`, `1.7/Assemblies/`, `1.7/Defs/`, `1.7/Patches/`, `LICENSE`, `README.md`).
   - Markdown release notes extracted directly from [CHANGELOG.md](../CHANGELOG.md).
2. **Steam Workshop & Steam Collection:**
   - **Mod Item ([#3822378226](https://steamcommunity.com/sharedfiles/filedetails/?id=3822378226)):** Built assemblies, Defs, Patches, and About metadata uploaded via RimWorld's in-game mod uploader.
   - **Mod Description:** BBCode formatted overview ([`docs/workshop_description.bbcode`](workshop_description.bbcode)).
   - **Change Notes:** Versioned changelog entry in BBCode ([`docs/workshop_changenotes_vX.Y.Z.txt`](workshop_changenotes_v0.3.0.txt)).
   - **Collection Description ([#3822385427](https://steamcommunity.com/sharedfiles/filedetails/?id=3822385427)):** BBCode overview of active and planned suite modules ([`docs/workshop_collection_description.bbcode`](workshop_collection_description.bbcode)).

---

## 2. Pre-Release Checklist

Before creating a release, ensure all items in the target milestone are complete:

- [ ] All milestone features implemented and tested.
- [ ] No compilation warnings or errors: `dotnet build Geoscape/Source/Geoscape/Geoscape.csproj -c Release`.
- [ ] No spellcheck errors: `npm run spellcheck`.
- [ ] Working branch (`feat/milestone-X.Y`) merged or fast-forwarded into `main`.

---

## 3. Version Bump & Synchronization

Update version numbers across the following files (or run `scripts/prepare-release.ps1 -Version X.Y.Z`):

| File                                                                                              | Target Property / Location                                                            |
| :------------------------------------------------------------------------------------------------ | :------------------------------------------------------------------------------------ |
| [`package.json`](../package.json)                                                                 | `"version": "X.Y.Z"`                                                                  |
| [`Geoscape/Source/Geoscape/Geoscape.csproj`](../Geoscape/Source/Geoscape/Geoscape.csproj)         | `<Version>`, `<AssemblyVersion>`, `<FileVersion>`                                     |
| [`Geoscape/Source/Geoscape/GeoscapeSettings.cs`](../Geoscape/Source/Geoscape/GeoscapeSettings.cs) | In-game active module string                                                          |
| [`CHANGELOG.md`](../CHANGELOG.md)                                                                 | Convert `## [Unreleased]` into `## [X.Y.Z] - YYYY-MM-DD` and add footer compare links |
| [`TODO.md`](../TODO.md)                                                                           | Move shipped milestone items to `## Completed Milestones` and prune active roadmap    |
| [`README.md`](../README.md) & [`Geoscape/README.md`](../Geoscape/README.md)                       | Release badge (`Release-vX.Y.Z`) and playtest header links                            |
| [`scripts/package-release.ps1`](../scripts/package-release.ps1)                                   | Default `$Version = 'X.Y.Z'`                                                          |

---

## 4. Steam Workshop Copy Preparation

Maintain synchronization for community-facing Steam portals:

1. **Mod Description ([`docs/workshop_description.bbcode`](workshop_description.bbcode)):**
   - Update version tags (e.g. `vX.Y LIVE`).
   - Add bullet points under **Key Features** for new capabilities.
   - Verify **Reporting Bugs & Feedback** directs users to GitHub Issues (preferred) and Steam Discussions, specifying `Player.log` and reproduction steps.
2. **Change Notes ([`docs/workshop_changenotes_vX.Y.Z.txt`](workshop_changenotes_v0.3.0.txt)):**
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

1. Rebuild the release DLL (`Geoscape.dll`).
2. Run `npm run spellcheck`.
3. Create a clean distribution staging directory and ZIP archive: `dist/ProjectVigilance-OverlandIncursions-vX.Y.Z.zip`.
4. Validate asset sizes (e.g. `Preview.png` < 1 MB).
5. Extract release notes from `CHANGELOG.md` to `dist/release-notes-vX.Y.Z.md`.

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
   - Launch RimWorld 1.7.
   - Navigate to **Mods** -> Select **Project Vigilance - Overland Incursions**.
   - Click **Upload to Steam Workshop**.
   - Ensure the upload completes with exit code success.
2. **Update Workshop Metadata via Steam:**
   - Visit the [Workshop Item Page](https://steamcommunity.com/sharedfiles/filedetails/?id=3822378226).
   - Click **Edit title & description** -> Paste contents of [`docs/workshop_description.bbcode`](workshop_description.bbcode).
   - Click **Add / edit change notes** -> Edit the auto-generated update entry and paste contents of [`docs/workshop_changenotes_vX.Y.Z.txt`](workshop_changenotes_v0.3.0.txt).
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

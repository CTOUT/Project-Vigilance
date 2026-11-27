# Project Vigilance — Milestone 1.5 In-Game Verification & Test Plan

This test plan provides step-by-step procedures to validate the improvements implemented on `feat/milestone-1.5` (`v0.4.2`), including the unified Mod Options host, Radar & Sensory UI polish, and **Waves 0–3** of the [2026-11-26 Comprehensive Audit](audits/2026-11-26-comprehensive-audit.md) (security hardening, Rule 5/14/16 compliance, hot-path performance optimisations, WCAG 2.2 AA accessibility, Release `.pdb` stripping, and UI/float-menu consolidation).

## Current Status & Next Tests

- **Passed (In-Game):** `TC-01` (single `Project Vigilance` button in `Mod options`, child modules accessible from `Mods`, no scrollbar on `Radar & Visuals`, greyed-out ID horizon cap), `TC-02` (all 8 themes, CVD/High Contrast auto-disabling `Use Player Faction Colour`, non-CVD presets leaving unchecked state intact), `TC-03` (shroud chunk intact after vantage expansion, smooth slider drags and `Reduce Shroud Motion` toggles at ~200 FPS / `5.0 ms` median with zero warnings/errors), `TC-04` (consolidated caravan & colony menus with zero duplicates; Sonar hidden inland and when Development mode is off, shown on coastal tiles only when Development mode and Aquatics are both active).
- **Passed (Automated Test Suite & Pipeline):** `TC-10` verified passing via `dotnet test Geoscape/Source/Geoscape.slnx -c Release` (`504/504` tests, `0` failed, `0` skipped, `0` warnings under `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`), `npm run spellcheck` (`0` issues), `npx prettier --check` (all formatted), `scripts/deploy-geoscape.ps1`, and zero `.pdb` files in Release assemblies or `dist/ProjectVigilance-*`.
- **Pending In-Game Verification:** `TC-05` through `TC-09`.

---

## Testing Priority & Focus Guide

| Status Badge            | Priority Tier                        | Description & Tester Action                                                                                      |
| :---------------------- | :----------------------------------- | :--------------------------------------------------------------------------------------------------------------- |
| `🔴 Focus 1: Immediate` | **Priority 1: Active In-Game Focus** | Mod Options UI, WCAG AA colourblind themes, `Reduce Shroud Motion`, slider responsiveness, and float-menu flows. |
| `🟡 Focus 2: Playtest`  | **Priority 2: Core Colony Playtest** | Orbital visibility semantics, save-load migration/sanitisation, overland ambush HUD, and diagnostics debug tool. |
| `🟢 Automated Pass`     | **Priority 3: Automated CI Gates**   | Headless MSTest suite (`502/502`), Rule 14 reflection audit, WCAG contrast math, spelling, formatting, no PDBs.  |

### Recommended Testing Sequence

1. **Step 1: Mod Options, Tabs & Horizon Sliders** (`🔴 TC-01`)
   Open **Options** -> **Mod options** and **Mods** management screen; check unified `Project Vigilance` entry, `Radar & Visuals` scrollbar-free layout, and greyed-out identification horizon cap on `Sensory & Visibility`.
2. **Step 2: WCAG AA Themes & Colourblind Auto-Override** (`🔴 TC-02`)
   Cycle curated themes (`Protanopia / Deuteranopia`, `Tritanopia`, `High Contrast`, `Tactical Cyan`); confirm `Use Player Faction Colour` auto-disables on CVD/High Contrast presets and sonar/scout perimeters update live on the globe.
3. **Step 3: Motion Safety, Slider Responsiveness & GPU Memory** (`🔴 TC-03`)
   Toggle `Reduce Shroud Motion` with active incursions and outposts under construction; drag radar opacity and cloud opacity sliders continuously and verify smooth frame pacing with no GPU material growth.
4. **Step 4: Consolidated Outpost Float Menus & Sonar Gating** (`🔴 TC-04`)
   Test caravan immediate deploy, travel-and-deploy, impassable-tile nearest deploy, colony construction dispatch, and beacon material submenu; confirm Sonar options remain hidden when Aquatics and DevMode are off.
5. **Step 5: Orbital Visibility Semantics (Rule 16)** (`🟡 TC-05`)
   Switch to `Planetary Surveillance` and verify non-player satellites/incursions stay hidden outside active sensor coverage.
6. **Step 6: Unified Renderer Diagnostics Tool** (`🟡 TC-06`)
   Run `Dump World Renderer & Shroud Diagnostics` from DevMode Actions and verify legacy `S1a`–`S4` spike entries are gone.
7. **Step 7: Save-Load Integrity, Rich-Text Sanitisation & Cache Reset** (`🟡 TC-07`)
   Load a pre-Wave-1 save (v2 ledger signature), save/reload (v3 ledger signature), verify stripped rich-text tags in integrity warnings/migration modal, and load a second save to verify clean static cache reset.
8. **Step 8: Overland Ambush HUD & Runtime Performance** (`🟡 TC-08`)
   Engage an overland incursion ambush and verify banners, IMGUI colour/font isolation, and smooth TPS/FPS with zero red errors.
9. **Step 9: Dynamic UI Sizing & Localisation Fallbacks** (`🟡 TC-09`)
   Inspect `Dialog_ScavengeAftermath`, `Dialog_VigilanceModuleMigration`, and `Diagnostics` embedded Core fallback note for zero text truncation.

---

## Test Matrix Overview

| Test Case | Feature Under Test                                     | Expected Behaviour                                                                                                                                    | Status                 | Primary Screen / Tool                 |
| :-------- | :----------------------------------------------------- | :---------------------------------------------------------------------------------------------------------------------------------------------------- | :--------------------- | :------------------------------------ |
| **TC-01** | Unified Mod Options, Radar Layout & Horizon Cap        | Single `Project Vigilance` entry in `Mod options`; child modules still accessible in `Mods` manager; no scrollbar on `Radar & Visuals`; greyed ID cap | `🟢 In-Game Pass`      | Mod Options / Mods Manager            |
| **TC-02** | WCAG AA Themes, CVD Auto-Override & Globe Perimeters   | All 8 themes legible (`>= 4.5:1`); CVD/High Contrast auto-disables faction colour; sonar & scout rings follow active theme on the globe               | `🟢 In-Game Pass`      | Mod Settings (`Radar & Visuals`)      |
| **TC-03** | `Reduce Shroud Motion` & Slider / GPU Material Hygiene | Incursion & outpost pulses freeze cleanly when `Reduce Shroud Motion` is on; radar/cloud slider drags stay fluid with no material leak                | `🟢 In-Game Pass`      | World Map / Mod Settings / Profiler   |
| **TC-04** | Consolidated Outpost Float Menus & Sonar Gating        | Caravan & colony outpost menus deploy/dispatch identically with zero duplicates; Sonar hidden without Aquatics and DevMode                            | `🟢 In-Game Pass`      | World Map Right-Click Context Menus   |
| **TC-05** | Rule 16 Visibility Semantics & Orbital Detection       | `Planetary Surveillance` reveals static terrain only; dynamic contacts & non-player orbital entities require active sensor coverage                   | `🟡 Focus 2: Playtest` | World Map / Orbit View                |
| **TC-06** | Unified Renderer & Shroud Diagnostics Debug Action     | Single read-only `Dump World Renderer & Shroud Diagnostics` tool logs full shroud/layer telemetry; legacy `S1a`–`S4` spike buttons absent             | `🟡 Focus 2: Playtest` | DevMode Debug Actions / Log           |
| **TC-07** | Save-Load Signing (v2/v3), Sanitisation & Cache Reset  | Pre-v3 saves load cleanly without false tamper flags; rich-text tags stripped in UI; loading another save leaves no stale sensory/expedition state    | `🟡 Focus 2: Playtest` | Save/Load / Diagnostics / Migration   |
| **TC-08** | Overland Ambush HUD, IMGUI Isolation & Hot-Path Perf   | Ambush banners, world search masks, and landmark icons do not bleed `GUI.color`/`Text.Font`; smooth TPS during active ambushes and concealed quests   | `🟡 Focus 2: Playtest` | Tactical Ambush Map / World Search    |
| **TC-09** | Dynamic Text Layout & `VigilanceLoc.Tr` Fallbacks      | No clipped buttons/badges in `Dialog_ScavengeAftermath`, `Dialog_VigilanceModuleMigration`, or `Diagnostics` embedded Core note                       | `🟡 Focus 2: Playtest` | Migration / Aftermath / Diagnostics   |
| **TC-10** | Automated Suite, Spellcheck, Formatting & Zero PDBs    | `502/502` Release tests pass (`0` warnings); `cspell` `0` issues; `prettier` clean; zero `.pdb` files in assemblies or `dist/ProjectVigilance-*`      | `🟢 Automated Pass`    | `dotnet test` / `cspell` / `prettier` |

---

## Detailed Test Cases

### TC-01: Unified Mod Options, Radar Layout & Horizon Cap

- **Priority:** `🔴 Focus 1: Immediate`
- **Automated Status:** `Dialog_Options_ModFilter_Patch` filter logic & `GeoscapeRuleSet.ClampIdentificationRadius` covered in unit tests; visual layout requires in-game check.

**Preconditions:** Launch RimWorld 1.7 with `Core`, `Geoscape`, `Orbital`, `Aquatics`, and `Thermal` enabled.

**Steps:**

1. Open **Options** -> **Mod options**.
2. Confirm only **Project Vigilance** appears (not separate `Project Vigilance — Geoscape / Orbital / Aquatics / Thermal` buttons).
3. Open **Mods** management screen and select a child module (e.g. `Project Vigilance - Thermal`) -> click **Mod options** to verify it opens the unified window directly to that module's tab.
4. Open **Sensory & Visibility**: set `Colony scout horizon` to `2` hexes and inspect `Colony identification horizon`.
5. Open **Radar & Visuals** and check whether the vertical scrollbar is absent at standard `1920x1080` (`1x` UI scale).

**Expected Results:**

- **Mod options** shows a single `Project Vigilance` entry; **Mods** screen still opens the unified options window from any child package.
- On **Sensory & Visibility**, `Colony identification horizon` and `Caravan identification horizon` retain full slider range (`0–14` / `0–9`) with the locked upper portion greyed out and an explanatory hover tooltip.
- **Radar & Visuals** fits cleanly without an unwanted vertical scrollbar.

---

### TC-02: WCAG AA Sensory Themes, CVD Auto-Override & Globe Perimeters

- **Priority:** `🔴 Focus 1: Immediate`
- **Automated Status:** `AuditWave3AccessibilityTests` verifies all `32/32` theme swatches achieve `>= 4.5:1` on `#21252B`, CVD auto-disable of `usePlayerFactionColor`, and dynamic `SonarPerimeterColorRgb` / `VisualPerimeterColorRgb`.

**Preconditions:** Active save with at least one colony, a radar/outpost, a caravan (scout ring), and (with Aquatics or DevMode) a coastal sonar station.

**Steps:**

1. Open **Mod Settings** -> **Radar & Visuals** with `Use Player Faction Colour` enabled (`✓`).
2. Select **Theme: Protanopia / Deuteranopia** from the theme dropdown.
3. Re-enable `Use Player Faction Colour`, then select **Theme: Tritanopia**, and repeat for **Theme: High Contrast**.
4. Re-enable `Use Player Faction Colour` and select **Theme: Tactical Cyan (Default)**.
5. Close Mod Settings and inspect radar, identification, scout, and sonar perimeter rings on the World Map across themes (including `Crimson Alert` and `Ghost White`).

**Expected Results:**

- Selecting `Protanopia / Deuteranopia`, `Tritanopia`, or `High Contrast` automatically sets `Use Player Faction Colour` to unchecked (`✗`).
- Selecting `Tactical Cyan` preserves the current `Use Player Faction Colour` checkbox state.
- On the World Map, scout (visual) and sonar perimeters change colour to match the selected theme rather than staying stuck on hardcoded cyan/cream RGB values.
- Disabled headers/controls and red campaign warning banners remain clearly legible against dark panel backgrounds.

---

### TC-03: `Reduce Shroud Motion`, Slider Responsiveness & GPU Material Hygiene

- **Priority:** `🔴 Focus 1: Immediate`
- **Automated Status:** `AuditWave1RegressionTests` verifies `reduceShroudMotion` static colour behaviour.

**Preconditions:** Active save with at least one moving `WorldObject_Incursion` and one `WorldObject_ListeningPost` currently under construction.

**Steps:**

1. Observe the pulsing amber icon on the incursion and pulsing construction icon on the listening post with `Reduce Shroud Motion` disabled (`✗`).
2. Open **Mod Settings** -> **Radar & Visuals** and enable **Reduce Shroud Motion** (`✓`).
3. Return to the World Map and observe both icons for 5 seconds.
4. Re-open **Radar & Visuals** and continuously drag **Perimeter Intensity**, **Honeycomb Grid Intensity**, **Identification Intensity**, and **Zoomed-In Cloud Opacity** back and forth.
5. Open Unity / Dubs Performance Analyzer (if installed) or monitor memory/FPS while tracking an incursion on the World Map for 2–3 minutes.

**Expected Results:**

- With `Reduce Shroud Motion` enabled, both the incursion and constructing outpost icons render at a steady, non-pulsing colour.
- Dragging radar sliders updates smoothly without freezing `OnGUI`; dragging `Zoomed-In Cloud Opacity` does not trigger radar mesh rebuilds.
- No continuous memory/GPU `Material` growth occurs while viewing animated incursions on the globe.

---

### TC-04: Consolidated Outpost Float Menus & Sonar Gating

- **Priority:** `🔴 Focus 1: Immediate`
- **Automated Status:** `CaravanExpeditionTests` & `AuditWave1RegressionTests` verify option deduplication and `SonarGatingUtility` gating.

**Preconditions:** Active save with a player settlement stocked with Steel, Components, and Wood/Stone blocks, plus a player caravan carrying outpost supplies.

**Steps:**

1. **With DevMode OFF (or Aquatics inactive):** right-click a coastal tile (both with a caravan selected and via intelligent right-click with nothing selected).
2. **With DevMode ON and Aquatics active:** right-click the same coastal tile.
3. Test all 4 outpost deployment paths:
   - Caravan on tile -> immediate `Deploy Automated Listening Post` / `Construct Signal Beacon` / `Deploy Automated Sonar Station`.
   - Caravan distant from passable tile -> `Travel and deploy ...`.
   - Caravan distant from impassable mountain/ocean tile -> `Travel and deploy ... (Nearest: Tile X)`.
   - Colony dispatch -> `Dispatch Construction Expedition: ...` including the `Signal Beacon... >` multi-material submenu and `< Back` navigation.

**Expected Results:**

- Zero Sonar deployment options appear in any caravan or colony menu when both Aquatics and DevMode are off.
- Sonar options appear cleanly on coastal tiles when Aquatics or DevMode is active.
- No duplicate menu entries appear in any context menu, and all deployed outposts spawn with faction ownership and `2500`-tick construction progress.

---

### TC-05: Rule 16 Visibility Semantics & Orbital Detection

- **Priority:** `🟡 Focus 2: Playtest`
- **Automated Status:** `AuditWave1RegressionTests.IsOrbitalEntityDetected_UnhookedFallback_RequiresOmniscientForNonPlayer` passing.

**Preconditions:** Active save with `Project Vigilance - Orbital` enabled (and optionally tested with `Geoscape` absent/unhooked).

**Steps:**

1. Set Visibility Mode to **Planetary Surveillance** (`Classic X-COM`).
2. Spawn a non-player orbital satellite or hostile overland incursion outside active colony/outpost sensor range via DevMode.
3. Observe whether the non-player contact is concealed until it enters active sensor range, then leaves range again.

**Expected Results:**

- Static planetary terrain and settlements remain visible in `Planetary Surveillance`.
- Non-player satellites and mobile incursions remain hidden outside active sensor coverage and automatically deselect from `WorldSelector` when leaving range.

---

### TC-06: Unified Renderer & Shroud Diagnostics Debug Action

- **Priority:** `🟡 Focus 2: Playtest`
- **Automated Status:** `FogOfWarOptimisationTests` updated and passing.

**Preconditions:** Active save on the World Map with DevMode enabled.

**Steps:**

1. Open DevMode **Debug Actions** -> `Project Vigilance` category.
2. Verify legacy `S1a`, `S1b`, `S2`, `S3`, and `S4` shroud spike actions are no longer listed.
3. Click **Dump World Renderer & Shroud Diagnostics** and inspect the DevMode log.

**Expected Results:**

- Only the single unified **Dump World Renderer & Shroud Diagnostics** action appears for shroud/renderer inspection.
- Executing it logs a structured read-only summary (surface draw layers, terrain sub-mesh finalized counts, shroud shell/haze status, and cull index state) without altering live map visuals or throwing errors.

---

### TC-07: Save-Load Integrity, Rich-Text Sanitisation & Cache Reset

- **Priority:** `🟡 Focus 2: Playtest`
- **Automated Status:** `AuditWave1RegressionTests` verifies v2/v3 ledger signing, rich-text stripping (`SanitizeDisplayText`), and `EnsureExploredArray` bitset clamping.

**Preconditions:** One pre-existing `v0.4.1` or early `v0.4.2` save (v2 ledger signature) plus a second distinct save.

**Steps:**

1. Load the pre-existing save and open **Mod Settings** -> **Diagnostics** -> **Active Campaign Governance**.
2. Confirm `Campaign Integrity` reports valid (no false `ExternalModification` warning from v2 -> v3 upgrade).
3. Save the game and reload it; confirm `Campaign Integrity` remains valid under v3 signing.
4. Load the second distinct save directly from in-game (without restarting RimWorld) and verify fog-of-war exploration, masked landmarks, and pending caravan expeditions belong strictly to the second save.

**Expected Results:**

- Legacy v2 saves load cleanly with zero false tamper warnings and seamlessly re-sign on next save.
- No stale exploration masks, pending expedition directives, or red Scribe console errors bleed across save loads.

---

### TC-08: Overland Ambush HUD, IMGUI Isolation & Hot-Path Performance

- **Priority:** `🟡 Focus 2: Playtest`
- **Automated Status:** `AuditWave2PerfAndRule14Tests` enforces zero `PlanetTile` hash keys across all 5 assemblies; `CaravanExpeditionTests` & `MapComponent_OverlandAmbushTests` passing.

**Preconditions:** Active save with an overland caravan intercepting or ambushing a hostile incursion, plus concealed Haze sites/quests.

**Steps:**

1. Enter an overland ambush map while colonists hold fire (`Concealment: Holding Fire` banner) and then spring the ambush and clear the sector (`Sector Clear` / `Reform Caravan` banner).
2. Open **World Search** (`Z`) on the World Map while unrevealed man-made landmarks and sites exist.
3. Inspect UI text colours, fonts, and alignments across adjacent RimWorld HUD elements and monitor TPS/FPS on `3x` speed.

**Expected Results:**

- Tactical ambush banners, World Search masked icons, and World Map landmark icons render cleanly without tinting or altering subsequent vanilla UI widgets.
- Tick performance remains smooth with zero red exceptions in the Debug Log.

---

### TC-09: Dynamic Text Layout & `VigilanceLoc.Tr` Fallbacks

- **Priority:** `🟡 Focus 2: Playtest`
- **Automated Status:** `AuditWave3AccessibilityTests` verifies `VigilanceLoc.Tr` fallback and `ImguiStateScope` restoration.

**Preconditions:** Active save with a scavenge aftermath site (or DevMode spawn) and a save triggering `Dialog_VigilanceModuleMigration` (e.g. temporarily disable an optional module).

**Steps:**

1. Open `Dialog_ScavengeAftermath` on an aftermath site and inspect the `All` button and item mass column alignment across multiple items.
2. Open `Dialog_VigilanceModuleMigration` and verify module status badges (`INSTALLED`, `EMBEDDED FALLBACK`, `NOT INSTALLED`) and `Steam Workshop` buttons do not truncate or overlap.
3. If running Geoscape without standalone Core, open **Diagnostics** and confirm the yellow embedded-Core note wraps cleanly inside the module table frame.

**Expected Results:**

- All labels, badges, buttons, and wrapped notes fit cleanly within their containers with zero clipping or overlap.

---

### TC-10: Automated Suite, Spellcheck, Formatting & Release Symbol Hygiene

- **Priority:** `🟢 Automated Pass`
- **Command Checklist:**

```powershell
dotnet test Geoscape/Source/Geoscape.slnx -c Release
npm run spellcheck
npx prettier --check "docs/audits/2026-11-26-comprehensive-audit.md" "docs/milestone_1_5_test_plan.md" "TODO.md" ".github/workflows/*.yml" ".github/dependabot.yml"
pwsh -File ./scripts/deploy-geoscape.ps1
Get-ChildItem -Path Core/1.7/Assemblies,Geoscape/1.7/Assemblies,Orbital/1.7/Assemblies,Aquatics/1.7/Assemblies,Thermal/1.7/Assemblies,dist/ProjectVigilance-Core,dist/ProjectVigilance-Geoscape,dist/ProjectVigilance-Orbital,dist/ProjectVigilance-Aquatics,dist/ProjectVigilance-Thermal -Filter *.pdb -Recurse -ErrorAction SilentlyContinue
```

**Expected Results:**

- `dotnet test`: `502` passed, `0` failed, `0` skipped, `0` warnings.
- `npm run spellcheck`: `0` issues (`en-GB`).
- `npx prettier --check`: all matched files use Prettier code style.
- `Get-ChildItem ... -Filter *.pdb`: empty output (zero `.pdb` files in Release assemblies or staged packages).

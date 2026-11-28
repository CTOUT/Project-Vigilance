# Milestone 1.6 — Quality Assurance & Playtest Plan

**Branch:** `feat/milestone-1.6`  
**Scope (Phase 0 — Foundation & Refactoring):** Deterministic Git build numbers, Wave 4B monolith decomposition (`GeoscapeSettings`, `WorldObject_Visibility_Patch`, `SensoryManager`, `WorldObject_Incursion`), Wave 4A Keyed XML localisation (`Core`, `Geoscape`, `Orbital`, `Aquatics`, `Thermal`), `FogOfWar` terminology alignment & Scribe backwards compatibility, and automated hygiene guards.

---

## Automated Verification Baseline

- [x] **Release Build:** `dotnet build Geoscape/Source/Geoscape.slnx -c Release` — `0` warnings, `0` errors (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`).
- [x] **Automated Unit & Guard Suite:** `dotnet test Geoscape/Source/Geoscape.slnx -c Release` — `515/515` passed (includes `BuildInfoTests` and `HygieneAndLocalizationGuardTests`: `0` orphan XML keys, `0` missing `PV_*` keys, legacy enum parse compatibility, `1,200`-line monolith ceiling).
- [x] **Formatting & Spellcheck:** `npx prettier --check "."` and `npm run spellcheck` — `0` issues.

---

## In-Game Verification Checklist (Phase 0)

| ID    | Area                              | Steps                                                                                                                                                                                                          | Expected Result                                                                                                                                                                                                   | Status                          |
| ----- | --------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------- |
| TC-01 | Deterministic Build Provenance    | Launch RimWorld -> open `Options -> Mod options -> Project Vigilance -> Diagnostics` tab and check `Logs/Player.log` startup banner.                                                                           | Geoscape shows `v0.5.0.<build>+<sha> \| Release \| YYYY-MM-DD` (no wall-clock `UTC` timestamp); sibling modules show `v0.5.0.<build>`.                                                                            | `[x]` Pass (clipping fixed)     |
| TC-02 | Decomposed Mod Options Tabs       | Open `Project Vigilance` Mod Options and click through all 5 tabs (`Sensory`, `Radar`, `Incursions`, `Geoscape`, `Diagnostics`).                                                                               | Every tab renders cleanly with zero layout overlap, red console errors, or missing controls; `Diagnostics` remains pinned last.                                                                                   | `[x]` Pass                      |
| TC-03 | Keyed XML UI Strings (All 5 Mods) | Inspect: (a) campaign profile tooltips in `Page_GeoscapeRules`, (b) caravan outpost/incursion right-click float menus, (c) Orbital satellite/station inspect pane, (d) coastal sonar placement on inland tile. | All labels render clean English (no raw `PV_*` keys or broken `{0}` tokens); inland sonar placement displays coastal-access rejection message.                                                                    | `[x]` Pass                      |
| TC-04 | Decomposed World Visibility & FoW | Start a new **Fog of War** campaign; inspect explored vs shrouded tiles, landmarks, world features, and an unidentified site; toggle to **Planetary Surveillance** and back.                                   | Shroud shell, terrain culling, landmark masking (`?`), selection pruning, and inspect panes behave identically to v0.5.0 with zero console errors.                                                                | `[x]` Pass (ocean tint tuned)   |
| TC-05 | Decomposed Incursions & Outposts  | Spawn a hostile incursion and a trade caravan (DevMode); verify movement, path overlay, inspect string, intercept/hail float menu, and outpost sabotage/repair.                                                | Smooth path interpolation, upright world markers, correct inspect telemetry, and working float menus with zero console errors.                                                                                    | `[x]` Pass (scout aura aligned) |
| TC-06 | Legacy Save Scribe Compatibility  | Load an existing v0.5.0 (or earlier) savegame that stored `ClassicHazeOfWar` / `ClassicHaze` in XML.                                                                                                           | Save loads with zero red Scribe errors; `Diagnostics` tab reports active profile as **Fog of War** with intact explored/identified fog state.                                                                     | `[x]` Pass                      |
| TC-07 | GPU Shroud Shader & Limb AA       | In **Fog of War**, drag `Radar & Visuals -> Shroud Nebula Intensity` from `0%` to `100%`, toggle `Reduce Shroud Motion`, and inspect the sun-lit planet limb against `PlanetGlow`.                             | Smooth per-pixel GPU nebula scales instantaneously (`0%` flat obsidian -> `100%` vivid nebula); `Reduce Shroud Motion` freezes drift; sun-lit limb shows smooth Fresnel feathering with no jagged stair-stepping. | `[x]` Pass                      |

---

## Upcoming Milestone 1.6 Feature Test Cases (To Be Added Per Stream)

- **TC-07+:** Blind Landing Drop-Zone Biome Filter.
- **TC-08+:** Planetary Expedition Phase, Orbital Staging Reserves & Colony Beacon Claiming.
- **TC-09+:** Odyssey DLC Non-Destructive Gravcore Takeoff.

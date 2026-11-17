# Project Vigilance Engineering Rules

## 1. WorldGrid Mathematics & Visual Alignment

- Never use custom spherical approximations or arbitrary degree-to-hex constants (e.g., `1.8f`) for WorldMap distances.
- Always use native `Find.WorldGrid.ApproxDistanceInTiles(angularDistance)` or `Find.WorldGrid.ApproxDistanceInTiles(tileA, tileB)`.
- Ensure all footprint, sensor, and effect range checks align 1:1 with `GenDraw.DrawWorldRadiusRing` flood-fill geometry.

## 2. Harmony UI Postfix Deduplication

- Any Harmony postfix that augments `IEnumerable` UI targets (e.g., Comms Console contacts, gizmos, float menus) must maintain a local `HashSet<T>` or ID cache to guarantee no duplicate targets are yielded.
- Always verify operational conditions (e.g., `CanTradeNow`, line-of-sight, faction hostility) before yielding targets to the UI.

## 3. Debug Tooling Hygiene

- Every debug action that spawns WorldObjects or Pawns must assign a unique suffix or identifier (e.g., tick timestamp or hash).
- Every spawn debug action must have an accompanying "Clear / Despawn All" debug tool to preserve save game hygiene during testing.

## 4. Milestone Gating & Scope Discipline

- Keep release milestones strictly focused on verified core features.
- Incomplete, experimental, or unpolished mechanics (e.g., non-standard satellite roles, coastal sonar) must remain gated behind `Prefs.DevMode` or debug-only menus until their dedicated milestone.

## 5. Headless Test Safe Lifecycle

- When overriding WorldObject or Thing lifecycle methods, never assume `Find.World` implies `Find.WorldObjects` or `Find.FactionManager` are initialised.
- Guard against null sub-managers to allow headless test runners to execute without mocking entire engine singletons.

## 6. Project Documentation & Repository Colocation

- All project architectural plans, design specifications, audit reports, and test plans must reside directly within the repository (`docs/`) rather than external ephemeral storage.
- Keep documentation version-controlled alongside corresponding code changes.

## 7. High-Frequency Component Tick Fast-Bail ($O(1)$)

- Any `MapComponent` or `WorldComponent` overriding `MapComponentTick()` or `WorldComponentTick()` must perform an $O(1)$ state check at the top of the method before iterating collections or evaluating game state.
- When idle (no active hostiles, raids, ambushes, or tracked targets), the tick method must exit immediately (`return;`) in < 50 ns to eliminate CPU overhead and GC churn on peaceful maps.
- Avoid LINQ expressions (e.g., `list.Any(...)`, `list.Where(...)`) in per-tick loops; use zero-allocation indexed for-loops or cached boolean flags.

## 8. Keyed Localisation Mandatory (`"PV_*".Translate()`)

- Every user-facing string in C# code (letters, dialogs, float menus, inspect strings, mod settings, alert notifications, gizmos) MUST use `"PV_...".Translate(...)` with an accompanying English entry in `Languages/English/Keyed/*.xml`.
- Never use raw English string literals in user-visible UI, inspection, or dialogue code.
- Dynamic tokens must use numbered replacement syntax (`{0}`, `{1}`) and RimWorld grammar formatting helpers (`CapitalizeFirst`, gender, and pluralisation rules).
- Internal developer diagnostics, assertion errors, and debug log statements may remain in plain English.

## 9. Mod Settings & Feature Gating Required

- Every major new simulation mechanic, post-def rule processor, or visual overlay MUST expose a user-configurable toggle or parameter in `GeoscapeSettings`.
- Simulation mechanics must support clean disabling at runtime without throwing null-reference exceptions or corrupting save state.
- Default settings must represent the intended design balance while giving players agency to tailor simulation fidelity.

## 10. Extensibility & Modder Lifecycle Hooks

- Any new sensory building, outpost, or vehicle MUST support configuration via XML `DefModExtension` (`DefModExtension_Sensor`, `DefModExtension_Detectable`) without requiring external modders to write C# code.
- Major operational lifecycle events (incursion spawns, departures, contact detections, classifications, interceptions, retreats, and settlement incarcerations) MUST invoke public static C# event delegates.
- Never tightly couple core logic to internal concrete types when an interface (`ISensorProvider`, `IDetectableTarget`, `IOrbitalEntity`) can represent the contract.

## 11. Sensory Palette Customisation & A11y Standards

- Any new visual perimeter, overlay ring, or HUD indicator MUST support curated colour themes and adhere to WCAG AA contrast standards (>4.5:1).
- All visual systems must be compatible with Red-Green (_Protanopia / Deuteranopia_) and Blue-Yellow (_Tritanopia_) colourblind presets.
- Overlays must pair colour with geometric or silhouette distinctions (e.g. solid rings, dashed contours, cell hatching) to prevent reliance on colour alone.
- In Mod Settings, present curated theme presets by default, providing an opt-in toggle to reveal granular RGB/HSV sliders.

## 12. Modular Separation & Unified Options Architecture

- Optional domain mechanics (e.g. coastal sonar, bathymetry, tactical battlescape) must not introduce hard assembly compile-time dependencies into `ProjectVigilance.Core`.
- Use interface contracts and soft mod checks (`ModLister.HasActiveModWithIdentifier`) for inter-module integration.
- Sub-modules must register their options tabs dynamically into Core's unified `ISettingsTabProvider` registry rather than creating separate cluttered entries in RimWorld's Mod Options list.

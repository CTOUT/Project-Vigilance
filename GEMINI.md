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

## 5. Headless Test Safe Lifecycle & Parallel Isolation

- When overriding WorldObject or Thing lifecycle methods, never assume `Find.World` implies `Find.WorldObjects`, `Find.FactionManager`, or `PlanetLayer.WorldDrawLayers` are initialised.
- Guard against null sub-managers (including `layer.WorldDrawLayers != null` before `layer.GetLayer<T>()`) so headless test runners execute without mocking entire engine singletons.
- Never invoke `WorldDrawLayerBase.Render()` or `WorldRendererUtility.WorldRendered` in headless tests (triggers Waylith `ShaderRegistry` static initialisation); extract frame-advance work into testable internal helpers called by `Render()`.
- Because `MSTestSettings.cs` enables method-level parallelisation, every `[TestClass]` that calls `GeoscapeTestHarness.ResetAllStaticState()`, sets `VigilanceModuleManager` test overrides, or mutates static singletons (`SensoryManager.Instance`, global hooks) MUST be annotated with `[DoNotParallelize]`.

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

- Optional domain mechanics (e.g. orbital logistics, coastal sonar, thermal integrity, tactical battlescape) must not introduce hard compile-time assembly references between domain modules or into `ProjectVigilance.Core`.
- Declare `brrainz.harmony` ONLY in `Core/About/About.xml`; domain sub-modules declare only `ProjectVigilance.Core` in `<modDependencies>` and reference `ProjectVigilance.Core.csproj` with `<Private>false</Private>`.
- Use Core interfaces, `VigilanceModuleManager` soft hooks registered via `[StaticConstructorOnStartup]` (never `[ModuleInitializer]`, which violates `CA2255`), and XML `PatchOperationFindMod` for conditional cross-module integration.
- Only `VigilanceCoreMod` returns `"Project Vigilance"` from `SettingsCategory()`; domain sub-modules return `string.Empty`, register tabs into `VigilanceSettingsRegistry` via `ISettingsTabProvider`, and subscribe to `VigilanceSettingsRegistry.OnWriteAllSettings`.

## 13. Modular Savegame Migration & Workshop Continuity

- Never delete or overwrite `Geoscape/About/PublishedFileId.txt` (`3822378226`); existing subscribers receive modular updates through the anchor package.
- Whenever public types move into `ProjectVigilance.Core`, preserve their CLR namespaces where appropriate and emit `[assembly: TypeForwardedTo(...)]` entries in `Geoscape/Source/Geoscape/AssemblyForwarders.cs`.
- Whenever Defs or WorldComponents extract out of an existing package into an optional module, add safe Scribe redirection mappings in `BackCompatibilityConverter_VigilanceModular` (`MissingModuleWorldObjectStub` / `DeprecatedWorldComponentStub`) so pre-split or partial-module saves load with zero red console errors.
- Wire extracted modules into `VigilanceMigrationTracker` and `Dialog_VigilanceModuleMigration` so players receive clear missing-module advisories, Workshop links, and one-click stub retirement (`RuleChangeKind.ModuleMigration`).

## 14. Waylith 1.7 WorldDrawLayer & Mesh Overlay Standards

- `WorldDrawLayerBase.Render()` checks `LayerSubMesh.finalized` and ignores `LayerSubMesh.disabled`. Hide terrain or empty overlay chunks via `LayerSubMesh.finalized = false` (preserving original finalized flags for safe restoration and leaving `MeshCollider` picking intact), and never cull terrain unless the covering shroud layer reports `IsGeometryBuilt`.
- `WorldVertexColorTransparent` uses Vulkan `Back`-face culling with `depthTest=false` and `depthWrite=false`; verify outward winding (`Vector3.Dot(Vector3.Cross(v1 - v0, v2 - v0), outwardDir) > 0f`) on every triangle, omit UVs (`0` UVs), and enforce draw ordering strictly via explicit render queues (`Dimming 3544 < ShroudShell 3545 < Haze 3546 < ShadowWash 3547 < Outline 3548 < WorldObjects 3550`) and altitude separation.
- Never use `PlanetTile` as a `HashSet<T>` or `Dictionary<TKey, TValue>` key (`PlanetTile.GetHashCode()` dereferences `Find.WorldGrid`); always key spatial state tables by `int tileId`.

## 15. Scribe Save-Load Deferred Audits & Migration Timing

- Never run world-object deduplication, orphan cleanup, or faction-infrastructure audits inside `ExposeData()` (loading or `PostLoadInit` phases), where Scribe cross-references (`Faction`, `WorldObject`) may still be unresolved.
- Defer cross-reference-dependent post-load audits and legacy migrations to `FinalizeInit()` / `LoadedGame()`, log a warning whenever legacy save cleanup executes, and queue player migration dialogs cleanly after `MapInitializing`.

## 16. Visibility Mode Semantics & Selection Hygiene

- `PlanetarySurveillance` reveals static planetary geography ("the map as it was"), not live moving entities. Dynamic contacts (incursions, satellites, mobile targets) require active sensor coverage in both `FogOfWar` and `PlanetarySurveillance`; only `Omniscient` bypasses detection checks.
- Whenever a tracked contact or orbital entity leaves sensor detection range, actively deselect it from `Find.WorldSelector` so players cannot keep tracking hidden contacts via a sticky selection bracket or inspect pane.

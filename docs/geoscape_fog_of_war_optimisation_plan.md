# Geoscape Fog of War Performance Optimisation Plan (v2)

## 1. Problem Statement & Context

- **Reference:** GitHub Issue #2 (`[Performance] World map slowdown reported under Fog of War`, reported by `@JayHandle`)
- **Affected Configurations:** Primary impact in `ClassicHazeOfWar` (`Fog of War`) and secondary impact in `PlanetarySurveillance`, scaling non-linearly with planet coverage (`30%`, `50%`, `100%` worlds reaching ~250,000 surface hex tiles).
- **Engine Constraints (RimWorld 1.7 / Waylith Engine):**
  - RimWorld 1.7 executes on Waylith's Vulkan renderer while exposing `UnityEngine.Mesh`, `Material`, and `LayerSubMesh` via `UnityStubs.dll` + `Waylith.UnityCompat.dll` (`Assembly-CSharp.dll` targets those interfaces).
  - Built-in GLSL shaders reside in `Data/Core/Shaders/World/` (`Shaders.World.xml`); custom Unity AssetBundle shaders are unsupported, so all rendering operates cleanly on `LayerSubMesh` buffers and built-in materials (`World/WorldVertexColorTransparent` and `World/WorldVertexColor`).
  - `WorldDrawLayerBase.Render()` guards drawing on `LayerSubMesh.finalized` (not `LayerSubMesh.disabled`), so hiding a submesh requires toggling `finalized = false` or clearing `tris`.

---

## 2. Root Cause Audit & Tiered Architecture

| Priority          | Location                                                                                                                                                                                                    | Bottleneck                                                                                                                                                                          | Impact                                                                                         |
| :---------------- | :---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | :---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | :--------------------------------------------------------------------------------------------- |
| **P0 — Critical** | [`WorldDrawLayer_RadarCoverage.cs#L170-L243`](../Geoscape/Source/Geoscape/Sensory/WorldDrawLayer_RadarCoverage.cs#L170-L243)                                                                                | Background `surveillanceDimming` rebuilds a **second full-globe transparent hex mesh** (~1.75M verts / ~1.5M tris on 100% worlds) synchronously on every `SetDirty()` caravan step. | Severe frame spikes on caravan movement + double full-globe alpha overdraw under `Fog of War`. |
| **P0 — Critical** | [`WorldDrawLayer_HazeOfWar.cs#L219-L359`](../Geoscape/Source/Geoscape/Sensory/WorldDrawLayer_HazeOfWar.cs#L219-L359)                                                                                        | Renders 7-vert / 6-tri fans across all tiles (~1.75M verts) with unused UVs, per-tile `MatLoader` lookups, sequential non-spatial 38k chunks, and opaque tiles in alpha queues.     | Heavy fragment overdraw, large Waylith `FlushCompat` buffer repacks, and poor frustum culling. |
| **P0 — Critical** | Vanilla `WorldDrawLayer_Terrain`                                                                                                                                                                            | Draws full globe terrain triangles underneath 100%-opaque black shroud tiles.                                                                                                       | Wasted GPU vertex/fragment work on unexplored early-game tiles.                                |
| **P1 — High**     | [`WorldObject_Visibility_Patch.cs#L481-L500`](../Geoscape/Source/Geoscape/Patches/WorldObject_Visibility_Patch.cs#L481-L500)                                                                                | `LandmarksToShow_Patch` runs `.Where(...).ToList()` on every GUI pass.                                                                                                              | Continuous per-frame heap allocations & GC churn in World view.                                |
| **P1 — High**     | [`SensoryManager.cs#L1563-L1638`](../Geoscape/Source/Geoscape/Sensory/SensoryManager.cs#L1563-L1638) & [`SensoryManager.cs#L1968-L2003`](../Geoscape/Source/Geoscape/Sensory/SensoryManager.cs#L1968-L2003) | `UpdateHazeActiveVision()` re-BFSes all stationary sensors on every 5-tick caravan step; `MarkSurroundingTilesIdentified()` scans `AllWorldObjects` inside tile BFS loops.          | Avoidable BFS work on caravan transit & quadratic world-object scans on dense maps.            |
| **P2 — Medium**   | [`WorldObject_Visibility_Patch.cs`](../Geoscape/Source/Geoscape/Patches/WorldObject_Visibility_Patch.cs) & [`SensoryManager.cs`](../Geoscape/Source/Geoscape/Sensory/SensoryManager.cs)                     | `IsFeatureExplored`, `ShouldHideWorldObject`, and `ShouldMaskWorldObjectAsUnknown` evaluate repeatedly per visible object/label each frame.                                         | Redundant main-thread lookups across labels, icons, materials, and gizmos.                     |

### Hierarchical 3-Tier Rendering Design

1. **Tier S — Geodesic Shroud Shell (`WorldDrawLayer_ShroudShell`):**
   - Subdivision-5 icosphere (`20,480` triangles, `10,242` shared vertices across `20` base-face submeshes) blanketing unexplored terrain cleanly without dips (`sagitta ≈ 0.02`).
   - A face is drawn as a shell triangle iff all tiles in `own(F) ∪ ring(F)` (1-ring neighbourhood) are in `Shroud` state.
2. **Tier F — Hex Frontier (`WorldDrawLayer_HazeOfWar`):**
   - Corner-only triangle fans (`vertCount` vertices, `vertCount - 2` triangles, zero UVs), spatially bucketed into `80` level-1 shell chunks (`~3.1k` hexes max each).
   - Fine per-hex rendering applies only to explored, active-vision, and 1-ring adjacent frontier tiles; all-clear or shell-covered chunks skip rendering (`finalized = false`).
   - Provable no-hole invariant: any overlap is strictly opaque Shroud on opaque Shroud (invisible); translucent explored Haze always stays per-hex.
3. **Tier T — Vanilla Terrain Cull (`WorldDrawLayer_Terrain_Cull`, spike-gated):**
   - Maps terrain submeshes via `triangleIndexToTileID`, tracking `visibleTileCount` from `tileFineMembership` so fully shrouded terrain submeshes skip drawing (`finalized = false`), while preserving collider picking (`meshCollidersInOrder`).

### Confirmed User Decisions & Settings

- **Effects:** Default `ShroudEffectLevel.Subtle` (drifting nebula vertex-colour tint + 1-ring frontier glow), plus `Reduce motion` toggle (freezes animated effects) and `Off` option (plain black shroud). Contrast & colourblind rules follow `SensoryTheme` (Rule 11).
- **Terrain Cull:** Default-on `optimiseUnexploredTerrain = true` (gated by Phase 0 runtime spikes and clean runtime disabling per Rule 9).
- **Custom Shader (Track B):** Parked in favour of native Waylith built-in shaders.
- **Haze Interior & Ring Depth:** Explored-but-unmonitored Haze stays per-hex; frontier margin fixed at 1 ring.
- **`PlanetarySurveillance`:** Uses Phase 1 cached corner-fan dimming + 3 ms time-slicing without shell seams.
- **Target Branch:** Delivered in two stages on `feat/milestone-1.5`.

---

## 3. Phased Execution Plan

### Stage 1: Independent High-Impact Optimisations (Phases 1, 3, 4)

#### Phase 1: Eliminate Double-Globe Dimming Overdraw & Cached Corner-Fan Dimming (`WorldDrawLayer_RadarCoverage.cs`)

1. Disable background `surveillanceDimming` geometry completely when `GeoscapeRules.VisibilityMode == VisibilityMode.ClassicHazeOfWar` (zero `DimmingRenderQueue` submeshes).
2. Under `PlanetarySurveillance`, replace the 7-vertex / 6-triangle dimming fan with a **corner-only fan** (`vertCount` verts, `vertCount - 2` tris, zero UVs, winding sign evaluated once at `k == 0`) with hoisted material resolution.
3. Time-slice initial dimming geometry construction (3 ms coroutine budget) and retain `TileDimLocation[]` + cached dimming submeshes so sensor/caravan updates mutate `MeshParts.Colors` in-place rather than rebuilding 250k triangles synchronously.

#### Phase 3: Zero-Allocation GUI & Per-`SweepRevision` Visibility Caches (`WorldObject_Visibility_Patch.cs`, `SensoryManager.cs`)

1. Replace `.Where(...).ToList()` in `LandmarksToShow_Patch` with a pooled `List<PlanetTile>` cache invalidated on `(SweepRevision, SensoryManager.Instance, sourceList, sourceCount)`.
2. Warm `EnsureFeatureCenterTilesIndexed()` during `SensoryManager.FinalizeInit(bool)` and cache `IsFeatureExplored` by `(feature.uniqueID, SweepRevision, VisibilityMode)`.
3. Cache `ShouldHideWorldObject` and `ShouldMaskWorldObjectAsUnknown` by `((long)wo.ID << 32) | (uint)wo.Tile.tileId` per `(SweepRevision, VisibilityMode)`, bypassing `IOrbitalEntity` and test overrides safely.

#### Phase 4: Sensory BFS & Sweep Hot-Path Optimisations (`SensoryManager.cs`)

1. Replace per-tile `Find.World.worldObjects.AllWorldObjects` linear scans inside `MarkSurroundingTilesIdentified()` with an $O(1)$ tile-indexed `Dictionary<int, List<int>> unidentifiedObjectIdsByTile` lookup (populated via `MarkWorldObjectUnidentified`/`Identified`/`Notify_WorldObjectRemoved` and lazily rebuilt after save load).
2. Partition `UpdateHazeActiveVision()` with a **stationary vision cache** (`stationaryVisionTiles`, `stationaryIdentifiedTiles`, `stationaryTileDomains`), re-evaluated on full sweeps / sensor registration changes so 5-tick `CheckPlayerCaravanMovement()` updates only run BFS on mobile caravan/satellite sensors.

---

### Stage 2: Hierarchical Shroud Shell, Frontier, Effects & Terrain Cull (Phases 0, 2A–2D, 5)

#### Phase 0: Waylith Runtime Spikes (DevMode-Only Diagnostics)

- **S1:** Verify runtime behaviour of toggling `LayerSubMesh.finalized` vs `tris` on vanilla terrain submeshes during render.
- **S2:** Verify terrain submesh-to-tile mapping via `triangleIndexToTileID` round-trip accuracy.
- **S3:** Benchmark Waylith `FlushCompat` (`FinalizeMesh(MeshParts.Colors)`) cost on a single 512-vertex shell chunk to lock nebula animation Hz.
- **S4:** Sample maximum globe tile vertex radii to lock shell radial clearance (`sagitta` + ε).

#### Phases 2A–2D

- **2A (Core Shell & Frontier):** Implement `ShroudGeometry.cs`, `ShroudHierarchy.cs`, `WorldDrawLayer_ShroudShell.cs`, convert `WorldDrawLayer_HazeOfWar` to 80-chunk corner-fan frontier rendering (`Patch` via `PlanetLayerPatch.xml`), and route `SensoryManager` transitions through `ShroudHierarchy`.
- **2B (Lazy Fine Chunks):** Defer allocation/triangulation of uninhabited frontier chunks until a coarse face approaches exploration (+1 chunk margin).
- **2C (Atmosphere Effects & Settings):** Add `ShroudEffectLevel`, `shroudEffects`, `reduceShroudMotion`, `optimiseUnexploredTerrain`, and keyed UI strings (`Languages/English/Keyed/Geoscape_Shroud.xml`).
- **2D (Terrain Cull):** Add `Patches/WorldDrawLayer_Terrain_Cull.cs` gated behind Phase 0 checks and `optimiseUnexploredTerrain`.

---

## 4. Verification & Regression Suite

1. **Headless Unit & Allocation Tests (`Geoscape.Tests`):**
   - `FogOfWarOptimisationTests.cs`: Verify zero dimming triangles in `ClassicHazeOfWar`, corner-only fan topology + cached dimming color refresh in `PlanetarySurveillance`, `0`-byte GC allocation (`GC.GetAllocatedBytesForCurrentThread()`) in `LandmarksToShow_Patch`, `IsFeatureExplored`, `ShouldHideWorldObject`, `ShouldMaskWorldObjectAsUnknown`, stationary-cache preservation on mobile steps, and `MarkSurroundingTilesIdentified` scaling.
   - Stage 2 (`ShroudHierarchyTests.cs`): Icosphere counts/ancestors, partition ownership, no-hole invariant across 10k random transitions, zero-alloc steady updates, and terrain cull counts.
2. **Pre-Commit Pipeline (`pipeline_validation`):**
   - `dotnet test Geoscape/Source/Geoscape.slnx -c Release`
   - `npx prettier --check "."`
   - `npx cspell "**"`

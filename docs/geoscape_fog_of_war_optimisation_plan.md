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

#### Phase 0: Waylith Runtime Spikes (DevMode-Only Diagnostics — Measured `119,904`-Tile Globe)

- **S1 (`S1a` / `S1b`):** Confirmed `LayerSubMesh.finalized = false` hides terrain geometry cleanly while preserving `MeshCollider` tile hover and right-click selection; `LayerSubMesh.disabled` is ignored by `WorldDrawLayerBase.Render()`.
- **S2 (`TerrainMapping`):** Measured `71/71` aligned terrain submeshes (`479,615` tris covering `119,904/119,904` tiles, `0` mismatches); early-game cull yield was `66/71` fully shrouded submeshes (`400,904` tris, `83.6%`).
- **S3 (`FlushCompat` Benchmark):** `FinalizeMesh(MeshParts.Colors)` measured `0.001 ms` median (`p95=0.007 ms`, `2,072 B/call`) at `512v / 1,024t` and `0.003 ms` median (`p95=0.003 ms`, `8,216 B/call`) at `2,048v / 4,096t`, confirming ~20 Hz round-robin nebula drift headroom.
- **S4 (`GlobeRadii` & Baseline):** Measured `TileCenterMag=[99.9995, 99.9999]`, `TileVertexMag=[100.0000, 100.0000]`, `TerrainMeshVertMag=[100.0000, 100.0000]` (`RecommendedMinShellRadius >= 100.0250`); baseline Stage 1 `HazeOfWar` emitted `23` finalized submeshes, `839,327` verts, `719,423` tris, and `839,327` UVs.

#### Phases 2A–2D (Implemented)

- **2A (Core Shell & Frontier):** Implemented `ShroudGeometry.cs` (`Subdivisions=5`, `20,480` faces, `10,242` vertices across `20` shell / `320` Level-2 fine chunks), `ShroudHierarchy.cs` (zero-allocation CSR state machine), and `WorldDrawLayer_ShroudShell.cs` (altitude `+0.062f`, queue `3545`, `0` UVs). Refactored `WorldDrawLayer_HazeOfWar` (altitude `+0.065f`, queue `3546`) to corner-only fans (`vertCount` verts, `vertCount - 2` tris, `0` UVs) registered via `PlanetLayerPatch.xml` and `PlanetLayer_InitializeLayer_Patch.cs`, with `SensoryManager` and `RuleTransitionExecutor` routing through `NotifyTileState` / `FlushShroudUpdates`.
- **2B (Lazy Fine Chunks):** Deferred `WorldDrawLayer_HazeOfWar` mesh allocation across `320` Level-2 chunks until needed (`fineChunkVisibleTileCount > 0`), pumping a `+1`-chunk edge-adjacent prewarm ring (skipping uninhabited void chunks) inside `WorldDrawLayer_ShroudShell.Render()` and hiding all-clear chunks (`finalized = false`).
- **2C (Atmosphere Effects & Settings):** Added `ShroudEffectLevel` (`Off`, `Subtle`, `Atmospheric`), `ShroudAtmosphere.cs` (continuous 3D nebula drift and WCAG AA >8:1 frontier glow across all curated `SensoryTheme` presets), Mod Settings (`shroudEffects`, `reduceShroudMotion`, `optimiseUnexploredTerrain`) in `GeoscapeSettings.cs`, and British English (`en-GB`) keyed strings in `Languages/English/Keyed/Geoscape_Shroud.xml`.
- **2D (Terrain Cull):** Added `Patches/WorldDrawLayer_Terrain_Cull.cs` (`TerrainCullIndex` + `WorldDrawLayerBase.FinalizeMesh` postfix) gating `LayerSubMesh.finalized` safely behind `optimiseUnexploredTerrain && ClassicHazeOfWar && ShroudShell.IsGeometryBuilt`, restoring original finalized states when toggled off or switching visibility modes.

#### Verified Stage 2 Telemetry (`1.7.5014 rev1195` Waylith `S4` Benchmarks)

| Metric                                     | 30% World (`119,904` tiles)                                                                      | 100% World (`590,492` tiles)                                                                         |
| ------------------------------------------ | ------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------- |
| **ShroudShellLayer Drawn**                 | `11,220` verts / `20,475` tris / `0` UVs (`20/20` chunks)                                        | `11,220` verts / `20,474` tris / `0` UVs (`20/20` chunks)                                            |
| **HazeOfWarLayer Built / Finalised**       | `6/320` built / `1` finalized                                                                    | `14/320` built / `2` finalized                                                                       |
| **HazeOfWarLayer Drawn (Alloc)**           | `10,854` (`67,271`) verts / `7,236` (`44,847`) tris / `0` UVs                                    | `23,597` (`151,787`) verts / `15,731` (`101,191`) tris / `0` UVs                                     |
| **Combined Drawn Fog + Shroud**            | `22,074` verts (`-97.4%` vs `839,327` baseline) / `27,711` tris (`-96.1%` vs `719,423` baseline) | `34,817` verts (`-99.2%` vs ~`4.13M` unoptimised) / `36,205` tris (`-99.0%` vs ~`3.54M` unoptimised) |
| **TerrainCull (ON)**                       | `65/68` submeshes / `442,095 / 479,615` tris (`-92.2%`)                                          | `139/166` submeshes / `1,674,878 / 2,361,956` tris (`-70.9%`)                                        |
| **TerrainCull (OFF)**                      | `0/68` submeshes / `0 / 479,615` tris                                                            | `0/166` submeshes / `0 / 2,361,956` tris                                                             |
| **Mode Switches (`Fog` ↔ `Surveillance`)** | `0` warnings / `0` errors                                                                        | `0` warnings / `0` errors                                                                            |

---

## 4. Verification & Regression Suite

1. **Headless Unit & Allocation Tests (`Geoscape.Tests`):**
   - `FogOfWarOptimisationTests.cs`: Verify zero dimming triangles in `ClassicHazeOfWar`, corner-only fan topology + cached dimming colour refresh in `PlanetarySurveillance`, `0`-byte GC allocation (`GC.GetAllocatedBytesForCurrentThread()`) in `LandmarksToShow_Patch`, `IsFeatureExplored`, `ShouldHideWorldObject`, `ShouldMaskWorldObjectAsUnknown`, stationary-cache preservation on mobile steps, and `MarkSurroundingTilesIdentified` scaling.
   - `ShroudHierarchyTests.cs`: Verify icosphere counts/outward winding/`FindOwningFace` accuracy and zero-alloc queries, `own(F)`/`ring(F)` disjointness and inverse consistency, 10,000-op brute-force classification and frontier parity, no-hole and shroud-on-shroud apron invariants, zero-alloc steady-state transitions, `ShroudAtmosphere` motion/contrast guarantees across all themes, end-to-end `ShroudShell` + lazy `HazeOfWar` corner fans and mode-switch round-trips, and `TerrainCullIndex` incremental cull/restore correctness.
2. **Pre-Commit Pipeline (`pipeline_validation`):**
   - `dotnet test Geoscape/Source/Geoscape.slnx -c Release`
   - `npx prettier --check "."`
   - `npx cspell "**"`

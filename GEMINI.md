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

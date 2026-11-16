# Learning Record: RimWorld Development Patterns & Milestone Discipline

## Executive Summary

Milestone 1.3 playtesting and automated testing revealed key architectural edge cases, mathematical discrepancies, and workflow challenges. This document records these learnings and the resulting workspace rules.

---

## Identified Learnings & Root Causes

### 1. RimWorld WorldGrid Distance & Visual Alignment
- **Symptom:** Satellites with a 24-tile footprint boosted caravans far outside their visual overlay or across the globe.
- **Root Cause:** Using custom spherical trigonometry with a constant angular degree estimate (`ApproxDegreesPerHexTile = 1.8f`) diverged radically from RimWorld's non-uniform hexagonal world grid mesh.
- **Rule/Standard:** Always use `Find.WorldGrid.ApproxDistanceInTiles(...)` rather than manual degree conversions or euclidean 3D math. Calculations must strictly match `GenDraw.DrawWorldRadiusRing` flood-fill geometry.

### 2. Harmony UI Postfix Deduplication & Eligibility
- **Symptom:** Comms Console displayed multiple duplicate entries for orbital contacts and allowed trade with unverified or hostile targets.
- **Root Cause:** Postfix patches concatenating IEnumerable targets without checking existing list state or verifying operational prerequisites (`CanTradeNow`, faction hostility).
- **Rule/Standard:** Harmony postfixes yielding UI targets (comms, menus, gizmos) must use a local `HashSet<T>` or ID cache to guarantee deduplication and explicitly validate operational criteria before yielding.

### 3. Debug Action Idempotency & Cleanup Hygiene
- **Symptom:** Repeated testing created clusters of duplicate satellites with identical callsigns, polluting save states and UI menus.
- **Root Cause:** Spawner debug actions lacked unique suffix generation and had no companion mass-despawn tools.
- **Rule/Standard:** Any debug action spawning entities must append a unique identifier (tick timestamp or hash) and must always be paired with a corresponding "Clear All / Despawn All" action.

### 4. Milestone Scope Discipline & Feature Gating
- **Symptom:** Complex satellite roles (Navigation speed boost, Recon line-of-sight, Sonar arrays) introduced balance questions and unpolished mechanics during final milestone stabilization.
- **Root Cause:** Including experimental sub-features in a core milestone before their gameplay loop was fully specified.
- **Rule/Standard:** Release milestones must remain scoped strictly to verified core features (Trade Satellites, Radar Dishes, Listening Posts). Any experimental or partially implemented feature must be gated behind `Prefs.DevMode` or deferred to a dedicated milestone.

### 5. Headless Test Safe WorldObject Lifecycle
- **Symptom:** Headless unit tests failed with `NullReferenceException` when accessing `Find.World` because `Find.WorldObjects` or `Find.FactionManager` were uninitialized.
- **Rule/Standard:** Lifecycle and helper methods in WorldObjects must perform null checks across all nested RimWorld managers (`Find.World`, `Find.WorldObjects`, `Find.FactionManager`) before delegating to base game engine methods.

---

## Implementation Reference

The resulting workspace rules are codified in:
[rimworld-development.md](file:///D:/Repos/GitHub/Project-Vigilance/.agents/rules/rimworld-development.md)

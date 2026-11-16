# Optimization Plan: Code, Test, Documentation, Security & Accessibility (Milestone 1.3)

## Goal Description

Perform a comprehensive post-milestone optimization pass across **Project Vigilance (Milestone 1.3)** to clean, harden, and polish the codebase prior to release. This pass applies:

- **DRY (Don't Repeat Yourself):** Consolidate repetitive coastal sonar gating, Harmony UI gizmo deduplication, and test setup boilerplate.
- **KISS (Keep It Simple, Stupid) & YAGNI (You Aren't Gonna Need It):** Streamline orbital calculation hot paths, remove lingering temporary stubs, and eliminate LINQ delegate allocations in UI actions.
- **Security:** Replace variable-time string comparisons with constant-time cryptographic verification (`CryptographicOperations.FixedTimeEquals`) in `RuleLedgerSigner`, and enforce defensive string length clamps on deserialised XML records.
- **Accessibility:** Increase disabled UI element contrast from `0.38f` to `0.55f` to satisfy WCAG AA standards (>4.5:1 ratio) on RimWorld dark dialogs, ensure dynamic line-height calculation across custom dialogs under UI scaling, and audit gizmo tooltips.
- **Documentation:** Standardise cross-document links, table headers, and headings across `docs/`, ensuring 100% compliance with repository standards.

---

## User Review Required

> [!IMPORTANT]
> **No Functional Regression:** All changes are strictly non-breaking refactorings, optimizations, and security hardenings. The 424 unit tests will continue to pass without behavioural changes.

> [!NOTE]
> **Cryptographic Hash Consistency:** `RuleLedgerSigner` payload format and HMAC-SHA256 hash generation remain 100% identical and backward-compatible with existing savegames. Only the comparison mechanism changes to constant-time verification.

---

## Open Questions

None. All proposed refactorings adhere to established project rules in [GEMINI.md](../GEMINI.md).

---

## Proposed Changes

### 1. DRY & Architectural Consolidation

#### [NEW] [SonarGatingUtility.cs](../Geoscape/Source/Geoscape/Sensory/SonarGatingUtility.cs)

Extract duplicated coastal sonar access and DevMode gating checks repeated 6 times across [FloatMenuMakerWorld_ChoicesAtFor_Patch.cs](../Geoscape/Source/Geoscape/Patches/FloatMenuMakerWorld_ChoicesAtFor_Patch.cs) and [WorldSelector_HandleWorldClicks_Patch.cs](../Geoscape/Source/Geoscape/Patches/WorldSelector_HandleWorldClicks_Patch.cs):

```csharp
namespace ProjectVigilance.Geoscape.Sensory;

public static class SonarGatingUtility
{
    /// <summary>
    /// Centralized gating condition for offering sonar deployment actions in Milestone 1.3.
    /// Requires DevMode and active coastal water adjacency.
    /// </summary>
    public static bool ShouldOfferSonarDeployment(PlanetTile tile)
    {
        return Prefs.DevMode
            && SonarTraversalUtility.CanEvaluateWorldGrid()
            && SonarTraversalUtility.HasCoastalAccess(tile);
    }
}
```

#### [MODIFY] [FloatMenuMakerWorld_ChoicesAtFor_Patch.cs](../Geoscape/Source/Geoscape/Patches/FloatMenuMakerWorld_ChoicesAtFor_Patch.cs) & [WorldSelector_HandleWorldClicks_Patch.cs](../Geoscape/Source/Geoscape/Patches/WorldSelector_HandleWorldClicks_Patch.cs)

Replace inline duplicated checks with `SonarGatingUtility.ShouldOfferSonarDeployment(...)`.

#### [MODIFY] [Building_CommsConsole_GetCommTargets_Patch.cs](../Geoscape/Source/Geoscape/Patches/Building_CommsConsole_GetCommTargets_Patch.cs)

1. In `Building_CommsConsole_GetGizmos_Patch`, add a local `HashSet<string> yieldedCallsigns = new();` to guarantee no duplicate gizmos are yielded, adhering strictly to Rule 2 of [GEMINI.md](../GEMINI.md).
2. Replace LINQ delegate allocation in the gizmo click handler with a fast static helper `FindBestNegotiator(Map map, Building_CommsConsole console)`:

```csharp
private static Pawn? FindBestNegotiator(Map map, Building_CommsConsole console)
{
    Pawn? best = null;
    int bestSkill = -1;
    List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
    for (int i = 0; i < colonists.Count; i++)
    {
        Pawn p = colonists[i];
        if (!p.Downed && !p.InMentalState && p.CanReach(console, PathEndMode.InteractionCell, Danger.Some))
        {
            int skill = p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
            if (skill > bestSkill)
            {
                bestSkill = skill;
                best = p;
            }
        }
    }
    return best;
}
```

---

### 2. Security Hardening

#### [MODIFY] [RuleLedgerSigner.cs](../Geoscape/Source/Geoscape/Rules/RuleLedgerSigner.cs)

Harden `VerifyRecordHash` against potential side-channel timing attacks using `CryptographicOperations.FixedTimeEquals`:

```csharp
public static bool VerifyRecordHash(
    RuleChangeRecord record,
    string? prevHash,
    string? campaignId)
{
    if (record == null || string.IsNullOrEmpty(record.hash))
    {
        return false;
    }

    string expected = ComputeRecordHash(
        prevHash,
        record.tick,
        record.kind,
        record.ruleKey,
        record.from,
        record.to,
        record.note,
        campaignId);

    byte[] a = Encoding.UTF8.GetBytes(record.hash.ToLowerInvariant());
    byte[] b = Encoding.UTF8.GetBytes(expected.ToLowerInvariant());

    return CryptographicOperations.FixedTimeEquals(a, b);
}
```

#### [MODIFY] [RuleChangeRecord.cs](../Geoscape/Source/Geoscape/Rules/RuleChangeRecord.cs)

Enforce defensive length limits upon deserialisation in `ExposeData()` to protect against corrupted or maliciously oversized XML input:

- Clamp `ruleKey` to 128 characters.
- Clamp `from` and `to` to 256 characters.
- Clamp `note` to 1024 characters.
- Clamp `campaignId` to 128 characters.

---

### 3. Accessibility & UI Contrast Hardening

#### [MODIFY] [CampaignRuleWidgets.cs](../Geoscape/Source/Geoscape/Rules/CampaignRuleWidgets.cs)

1. Increase disabled control opacity from `0.38f` to `0.55f`:

```csharp
// WCAG AA compliant contrast ratio (>4.5:1) on dark slate backgrounds
private static readonly Color DisabledTextColor = new(1f, 1f, 1f, 0.55f);
```

2. Apply `DisabledTextColor` consistently across `DrawBool`, `DrawFloat`, and `DrawEnum`.

#### [MODIFY] [CampaignRulesPanel.cs](../Geoscape/Source/Geoscape/Rules/CampaignRulesPanel.cs)

Ensure text descriptions compute dynamic line heights via `Text.CalcHeight` rather than fixed row offsets, preventing truncation under UI scaling > 1.0x.

---

### 4. Test Suite Optimization

#### [NEW] [GeoscapeTestBase.cs](../Geoscape/Source/Geoscape.Tests/GeoscapeTestBase.cs)

Extract common `[TestInitialize]` and `[TestCleanup]` test harness lifecycle methods repeated across 15+ test classes into a shared, reusable abstract base class:

```csharp
namespace Geoscape.Tests;

public abstract class GeoscapeTestBase
{
    protected static PlanetTile MakeTile(int id) => GeoscapeTestHarness.MakeTile(id);

    [TestInitialize]
    public virtual void Setup()
    {
        GeoscapeTestHarness.EnsureEnvironmentInitialised();
        GeoscapeTestHarness.InitMockWorld(out _);
    }

    [TestCleanup]
    public virtual void Cleanup()
    {
        GeoscapeTestHarness.ResetAllStaticState();
    }
}
```

---

### 5. Documentation Cleanup & Spellcheck Verification

- Verify all internal relative markdown links across [docs/](.) resolve cleanly.
- Ensure all diagrams and tables conform to GitHub-flavoured markdown and WCAG text contrast requirements.
- Run `npm run spellcheck` and `npm run format:check` to ensure 0 errors.

---

## Verification Plan

### Automated Tests

1. **Compilation Check:**
   ```powershell
   dotnet build Geoscape/Source/Geoscape.slnx -c Release
   ```
2. **Unit Test Suite:**
   ```powershell
   dotnet test Geoscape/Source/Geoscape.slnx
   ```
   Verify all 424 tests pass without regressions.
3. **Spellcheck Verification:**
   ```powershell
   npm run spellcheck
   ```
   Verify 0 spelling errors across 32 files.
4. **Code Style & Formatting:**
   ```powershell
   npm run format:check
   ```
   Verify all files conform to Prettier rules.

### Manual Verification

1. Open Comms Console in-game: verify overhead trade satellites appear cleanly without duplicates.
2. Open Mod Settings / Campaign Rules: observe improved disabled text legibility on dark background.
3. Test XML load with altered record: verify HMAC validation correctly rejects untracked edits in constant time.

using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.World;

/// <summary>
/// Data container for algebraic background skirmish participant parameters.
/// </summary>
public struct SkirmishSideData
{
    public float CombatPoints;
    public TechLevel TechLevel;
    public string FactionName;
    public bool IsTradeCaravan;
}

/// <summary>
/// Result container for algebraic background skirmish resolution outcomes.
/// </summary>
public struct SkirmishResolution
{
    public bool SideAWon;
    public float SideAStrength;
    public float SideBStrength;
    public float SideACasualtyFraction;
    public float SideBCasualtyFraction;
    public float SideARemainingPoints;
    public float SideBRemainingPoints;
    public bool SideARoutedOrDestroyed;
    public bool SideBRoutedOrDestroyed;
    public float SalvageLootValue;
    public int WoundedSurvivorCount;
}

/// <summary>
/// WorldComponent managing inter-faction world map collision detection in O(M) time via tile bucketing
/// and resolving automated background skirmishes between hostile and rival travelling forces.
/// </summary>
public class InterFactionEncounterManager : WorldComponent
{
    public const int CheckIntervalTicks = 250; // Evaluated on TickRare (250 ticks)
    public const int SkirmishCooldownTicks = 15000; // Cooldown after skirmish before engaging again (~6h in-game)

    private readonly Dictionary<int, List<WorldObject_Incursion>> tileBuckets = new(256);
    private readonly List<List<WorldObject_Incursion>> listPool = new(256);
    private readonly HashSet<WorldObject_Incursion> engagedThisPass = new(128);

    public static InterFactionEncounterManager? Instance { get; set; }

    public IReadOnlyDictionary<int, List<WorldObject_Incursion>> TileBuckets => tileBuckets;

    public InterFactionEncounterManager(RimWorld.Planet.World world) : base(world)
    {
        Instance = this;
        for (int i = 0; i < 256; i++)
        {
            listPool.Add(new List<WorldObject_Incursion>(4));
        }
    }

    public override void FinalizeInit(bool fromLoad)
    {
        base.FinalizeInit(fromLoad);
        Instance = this;
    }

    public override void WorldComponentTick()
    {
        base.WorldComponentTick();

        if (Current.Game?.tickManager is { } tm && tm.TicksGame % CheckIntervalTicks == 0)
        {
            DeepProfiler.Start("ProjectVigilance.InterFactionEncounters");
            try
            {
                CheckInterFactionCollisions();
            }
            finally
            {
                DeepProfiler.End();
            }
        }
    }

    /// <summary>
    /// Executes the complete O(M) tile-bucketed collision pass and resolves any detected skirmishes.
    /// </summary>
    public void CheckInterFactionCollisions()
    {
        DeepProfiler.Start("ProjectVigilance.InterFactionCollisions");
        try
        {
            engagedThisPass.Clear();
            ClearBuckets();
            IndexAllIncursions();
            ProcessCollisions();
        }
        finally
        {
            DeepProfiler.End();
        }
    }

    /// <summary>
    /// Re-pools allocated bucket lists, ensuring zero garbage generation per sweep pass.
    /// </summary>
    public void ClearBuckets()
    {
        foreach (var kvp in tileBuckets)
        {
            kvp.Value.Clear();
            listPool.Add(kvp.Value);
        }
        tileBuckets.Clear();
    }

    /// <summary>
    /// Optional override for incursion indexing in test / benchmark scenarios where WorldObjects is uninitialized.
    /// </summary>
    public static IReadOnlyList<WorldObject_Incursion>? IncursionProviderOverride { get; set; }

    /// <summary>
    /// Indexes all active travelling incursions on the world into tile buckets in O(M) time.
    /// </summary>
    public void IndexAllIncursions()
    {
        var overrideList = IncursionProviderOverride;
        if (overrideList != null)
        {
            for (int i = 0; i < overrideList.Count; i++)
            {
                var inc = overrideList[i];
                if (inc != null && (Find.World == null || inc.Spawned) && !inc.Destroyed && !inc.IsEngagedInTacticalMap)
                {
                    IndexIncursion(inc);
                }
            }
            return;
        }

        if (Find.World?.worldObjects == null) return;
        var worldObjects = Find.World.worldObjects.AllWorldObjects;

        for (int i = 0; i < worldObjects.Count; i++)
        {
            if (worldObjects[i] is WorldObject_Incursion incursion &&
                (Find.World == null || incursion.Spawned) &&
                !incursion.Destroyed &&
                !incursion.IsEngagedInTacticalMap)
            {
                IndexIncursion(incursion);
            }
        }
    }

    /// <summary>
    /// Registers a single incursion into its corresponding tile bucket.
    /// </summary>
    public void IndexIncursion(WorldObject_Incursion incursion)
    {
        if (incursion == null) return;
        int tileId = incursion.Tile.tileId;
        if (tileId < 0) return;

        if (!tileBuckets.TryGetValue(tileId, out var list))
        {
            if (listPool.Count > 0)
            {
                list = listPool[listPool.Count - 1];
                listPool.RemoveAt(listPool.Count - 1);
            }
            else
            {
                list = new List<WorldObject_Incursion>(4);
            }
            tileBuckets[tileId] = list;
        }

        list.Add(incursion);
    }

    /// <summary>
    /// Inspects tile buckets containing 2 or more incursions, triggering background skirmishes
    /// strictly when hostile or rival forces occupy identical tiles.
    /// </summary>
    public void ProcessCollisions()
    {
        foreach (var kvp in tileBuckets)
        {
            var bucket = kvp.Value;
            if (bucket.Count < 2) continue;

            for (int i = 0; i < bucket.Count - 1; i++)
            {
                var incA = bucket[i];
                if (incA.Destroyed || incA.IsEngagedInTacticalMap || incA.IsSkirmishOnCooldown || engagedThisPass.Contains(incA) || (Find.World != null && !incA.Spawned)) continue;

                for (int j = i + 1; j < bucket.Count; j++)
                {
                    var incB = bucket[j];
                    if (incB.Destroyed || incB.IsEngagedInTacticalMap || incB.IsSkirmishOnCooldown || engagedThisPass.Contains(incB) || (Find.World != null && !incB.Spawned)) continue;

                    // Collisions trigger strictly when hostile or rival incursions occupy identical tiles
                    if (incA.Faction != null && incB.Faction != null && incA.Faction.HostileTo(incB.Faction))
                    {
                        engagedThisPass.Add(incA);
                        engagedThisPass.Add(incB);
                        ExecuteBackgroundSkirmish(incA, incB);
                        break; // incA engaged, advance to next
                    }
                }
            }
        }
    }

    /// <summary>
    /// Resolves an automated overland background skirmish between two colliding incursions algebraically.
    /// </summary>
    public void ExecuteBackgroundSkirmish(WorldObject_Incursion incA, WorldObject_Incursion incB)
    {
        DeepProfiler.Start("ProjectVigilance.SkirmishResolution");
        try
        {
            // Enforce skirmish cooldown on both participants to avoid repeated 250-tick skirmish re-triggers
            incA.SetSkirmishCooldown(SkirmishCooldownTicks);
            incB.SetSkirmishCooldown(SkirmishCooldownTicks);

            var dataA = new SkirmishSideData
            {
                CombatPoints = (incA.IncidentParms?.points > 0) ? incA.IncidentParms.points : 400f,
                TechLevel = incA.Faction?.def?.techLevel ?? TechLevel.Industrial,
                FactionName = incA.Faction?.Name ?? "Participant A",
                IsTradeCaravan = incA.IsUnderlyingTradeCaravan
            };

            var dataB = new SkirmishSideData
            {
                CombatPoints = (incB.IncidentParms?.points > 0) ? incB.IncidentParms.points : 400f,
                TechLevel = incB.Faction?.def?.techLevel ?? TechLevel.Industrial,
                FactionName = incB.Faction?.Name ?? "Participant B",
                IsTradeCaravan = incB.IsUnderlyingTradeCaravan
            };

            var res = ResolveSkirmishAlgebraic(dataA, dataB);

            WorldObject_Incursion winner = res.SideAWon ? incA : incB;
            WorldObject_Incursion loser = res.SideAWon ? incB : incA;
            float winnerRemaining = res.SideAWon ? res.SideARemainingPoints : res.SideBRemainingPoints;
            float loserRemaining = res.SideAWon ? res.SideBRemainingPoints : res.SideARemainingPoints;
            float winnerCasualties = res.SideAWon ? res.SideACasualtyFraction : res.SideBCasualtyFraction;
            float loserCasualties = res.SideAWon ? res.SideBCasualtyFraction : res.SideACasualtyFraction;
            bool loserDestroyed = res.SideAWon ? res.SideBRoutedOrDestroyed : res.SideARoutedOrDestroyed;

            PlanetTile skirmishTile = incA.Tile;

            // 1. Apply casualties and harvest real casualty pawns directly
            var winnerCasualtyPawns = winner.ApplySkirmishCasualties(winnerCasualties, winnerRemaining);
            var loserCasualtyPawns = loser.ApplySkirmishCasualties(loserCasualties, loserRemaining);

            List<Pawn> casualties = new(loserCasualtyPawns.Count + winnerCasualtyPawns.Count);
            casualties.AddRange(loserCasualtyPawns);
            casualties.AddRange(winnerCasualtyPawns);

            // 2. Spawn Encounter Aftermath site on world map BEFORE destroying loser
            // (Ensures trade stock loot and casualty pawns are salvaged before loser's TradeStock is cleared)
            SpawnAftermathSite(skirmishTile, winner.Faction, loser.Faction, res, incA, incB, casualties);

            // 3. Handle loser outcome: destroyed/wiped out vs retreating
            if (loserDestroyed)
            {
                ContactRegistry.Instance?.Notify_ContactDestroyed(
                    loser,
                    $"Destroyed in skirmish against {winner.Faction?.Name ?? "enemy forces"}");
                loser.Destroy();
            }
            else
            {
                loser.ReverseCourse($"routed after defeat against {winner.Faction?.Name ?? "enemy forces"}");
            }

            // 4. Handle winner outcome: continue march vs retreat from excessive damage
            if (winnerCasualties >= 0.45f || winner.IsTradeMissionAborted || winnerRemaining < 100f)
            {
                winner.ReverseCourse($"retreating after heavy combat casualties against {loser.Faction?.Name ?? "enemy forces"}");
            }
            else
            {
                winner.AdvancePathTile();
            }

            // 5. Narrative Geoscape notification if sector is monitored
            NotifyPlayerOfSkirmish(skirmishTile, winner, loser, res);
        }
        finally
        {
            DeepProfiler.End();
        }
    }

    private void SpawnAftermathSite(
        PlanetTile tile,
        Faction? victor,
        Faction? defeated,
        SkirmishResolution res,
        WorldObject_Incursion incA,
        WorldObject_Incursion incB,
        List<Pawn>? casualties = null)
    {
        var def = DefDatabase<WorldObjectDef>.GetNamedSilentFail("Vigilance_EncounterAftermath");
        if (def == null) return;

        var aftermath = (WorldObject_EncounterAftermath)WorldObjectMaker.MakeWorldObject(def);
        aftermath.Tile = tile;
        aftermath.SetupAftermath(victor, defeated, res, incA, incB, casualties);

        Find.WorldObjects?.Add(aftermath);

        // Conceal in Haze of War if located in unmonitored hex
        if (HazeReconnaissanceManager.Instance != null &&
            SensoryManager.Instance != null &&
            !SensoryManager.Instance.HasActiveVision(tile))
        {
            HazeReconnaissanceManager.Instance.RegisterConcealedSite(aftermath);
        }
    }

    private void NotifyPlayerOfSkirmish(
        PlanetTile tile,
        WorldObject_Incursion winner,
        WorldObject_Incursion loser,
        SkirmishResolution res)
    {
        bool hasSensoryVision = SensoryManager.Instance != null && SensoryManager.Instance.HasActiveVision(tile);
        bool isOmniscient = GeoscapeMod.Settings.visibilityMode == VisibilityMode.Omniscient;

        if (hasSensoryVision || isOmniscient)
        {
            string winFaction = winner.Faction?.Name ?? "Rival Forces";
            string loseFaction = loser.Faction?.Name ?? "Hostile Forces";
            string title = "Geoscape Skirmish: " + winFaction;
            string text = $"Sensor telemetry detected an overland clash between {winFaction} and {loseFaction} in sector {tile}.\n\n" +
                          $"{winFaction} emerged victorious. An Encounter Aftermath site containing salvageable trade loot and wounded survivors is located at the skirmish coordinates.";

            LetterDef letterDef = (winner.Faction?.HostileTo(Faction.OfPlayer) == true)
                ? LetterDefOf.ThreatSmall
                : LetterDefOf.NeutralEvent;

            Find.LetterStack?.ReceiveLetter(title, text, letterDef, new GlobalTargetInfo(tile));
        }
    }

    /// <summary>
    /// Pure algebraic skirmish resolver based on combat points, tech level, and RNG.
    /// Can be tested deterministically via forced RNG arguments.
    /// </summary>
    public static SkirmishResolution ResolveSkirmishAlgebraic(
        SkirmishSideData sideA,
        SkirmishSideData sideB,
        float? forcedRngA = null,
        float? forcedRngB = null)
    {
        float multA = GetTechMultiplier(sideA.TechLevel);
        float multB = GetTechMultiplier(sideB.TechLevel);

        float rngA = forcedRngA ?? (Rand.Value * 0.30f + 0.85f); // 0.85 to 1.15
        float rngB = forcedRngB ?? (Rand.Value * 0.30f + 0.85f);

        float strengthA = Math.Max(10f, sideA.CombatPoints * multA * rngA);
        float strengthB = Math.Max(10f, sideB.CombatPoints * multB * rngB);

        bool sideAWon = strengthA >= strengthB;
        float winStrength = sideAWon ? strengthA : strengthB;
        float lossStrength = sideAWon ? strengthB : strengthA;
        float ratio = winStrength / Math.Max(1f, lossStrength);

        // Winner casualties: proportional to loser strength relative to winner (5% to 45%)
        float winCasualtyFrac = Math.Clamp(0.35f * (lossStrength / winStrength), 0.05f, 0.45f);

        // Loser casualties:
        // >= 2.0x strength: overwhelming stomp (90% casualties)
        // >= 1.4x strength: decisive victory (75% casualties)
        // < 1.4x strength: close pyrrhic battle (55% casualties)
        float lossCasualtyFrac = ratio switch
        {
            >= 2.0f => 0.90f,
            >= 1.4f => 0.75f,
            _ => 0.55f
        };

        float casualtyA = sideAWon ? winCasualtyFrac : lossCasualtyFrac;
        float casualtyB = sideAWon ? lossCasualtyFrac : winCasualtyFrac;

        float remainingA = Math.Max(0f, sideA.CombatPoints * (1f - casualtyA));
        float remainingB = Math.Max(0f, sideB.CombatPoints * (1f - casualtyB));

        // Destruction / rout threshold: casualties >= 80% or remaining combat points < 100
        bool routedOrDestroyedA = casualtyA >= 0.80f || remainingA < 100f;
        bool routedOrDestroyedB = casualtyB >= 0.80f || remainingB < 100f;

        // Procedural salvage loot: 25% of defeated force combat points + bonus for merchants
        float baseLoot = (sideAWon ? sideB.CombatPoints : sideA.CombatPoints) * 0.25f;
        if (sideA.IsTradeCaravan || sideB.IsTradeCaravan)
        {
            baseLoot += 350f;
        }

        int woundedCount = ratio >= 2.0f ? 1 : 2;
        if (sideA.CombatPoints + sideB.CombatPoints > 1500f)
        {
            woundedCount++;
        }

        return new SkirmishResolution
        {
            SideAWon = sideAWon,
            SideAStrength = strengthA,
            SideBStrength = strengthB,
            SideACasualtyFraction = casualtyA,
            SideBCasualtyFraction = casualtyB,
            SideARemainingPoints = remainingA,
            SideBRemainingPoints = remainingB,
            SideARoutedOrDestroyed = routedOrDestroyedA,
            SideBRoutedOrDestroyed = routedOrDestroyedB,
            SalvageLootValue = baseLoot,
            WoundedSurvivorCount = woundedCount
        };
    }

    /// <summary>
    /// Computes the tech-level strength multiplier used in algebraic skirmish resolution.
    /// </summary>
    public static float GetTechMultiplier(TechLevel techLevel)
    {
        return techLevel switch
        {
            TechLevel.Animal => 0.5f,
            TechLevel.Neolithic => 0.8f,
            TechLevel.Medieval => 1.0f,
            TechLevel.Industrial => 1.25f,
            TechLevel.Spacer => 1.5f,
            TechLevel.Ultra => 1.75f,
            TechLevel.Archotech => 2.0f,
            _ => 1.0f
        };
    }
}

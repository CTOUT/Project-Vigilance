using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.World;

/// <summary>
/// Historical telemetry and operations log entry preserved after an incursion is resolved,
/// defeated, retreated, or completed its journey.
/// </summary>
public class ContactArchiveRecord : IExposable
{
    public int trackingId;
    public string contactLabel = "Unknown Contact";
    public Faction? faction;
    public string factionName = "Unknown Faction";
    public IncursionGroupKind groupKind = IncursionGroupKind.GenericNeutral;
    public PlanetTile originTile;
    public PlanetTile destinationTile;
    public int createdTick;
    public int resolvedTick;
    public string outcome = "Unknown";
    public int initialHeadcount;
    public int finalHeadcount;
    public int humanlikeCount;
    public int animalCount;
    public int mechCount;
    public int captivesTaken;
    public List<string> memberSummary = new();
    public List<string> assetSummary = new();

    public ContactArchiveRecord() { }

    public ContactArchiveRecord(WorldObject_Incursion incursion, string outcomeReason)
    {
        trackingId = incursion.TrackingId;
        contactLabel = incursion.LabelCap;
        faction = incursion.Faction;
        factionName = incursion.Faction?.Name ?? "Unknown Faction";
        groupKind = incursion.GroupKind;
        originTile = incursion.OriginTile;
        destinationTile = incursion.DestinationTile;
        createdTick = incursion.creationGameTicks;
        resolvedTick = Find.TickManager?.TicksGame ?? 0;
        outcome = outcomeReason;

        var pawns = incursion.CommittedPawns ?? incursion.RetreatingPawns;
        if (pawns != null)
        {
            humanlikeCount = pawns.Count(p => p != null && p.RaceProps.Humanlike);
            animalCount = pawns.Count(p => p != null && p.RaceProps.Animal);
            mechCount = pawns.Count(p => p != null && p.RaceProps.IsMechanoid);
            finalHeadcount = pawns.Count(p => p != null && !p.Dead && !p.Destroyed);
            initialHeadcount = pawns.Count;

            memberSummary = pawns
                .Where(p => p != null)
                .Take(20)
                .Select(p => $"{p.LabelShort} ({p.KindLabel})")
                .ToList();

            var packAnimals = pawns
                .Where(p => p != null && p.RaceProps.Animal)
                .Select(p => p.KindLabel)
                .Distinct()
                .ToList();

            var mechs = pawns
                .Where(p => p != null && p.RaceProps.IsMechanoid)
                .Select(p => p.KindLabel)
                .Distinct()
                .ToList();

            if (packAnimals.Count > 0)
            {
                assetSummary.Add($"Animals: {string.Join(", ", packAnimals)}");
            }
            if (mechs.Count > 0)
            {
                assetSummary.Add($"Mechs: {string.Join(", ", mechs)}");
            }
        }

        captivesTaken = incursion.KidnappedColonists?.Count ?? 0;
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref trackingId, "trackingId", 0);
        Scribe_Values.Look(ref contactLabel, "contactLabel", "Unknown Contact");
        Scribe_References.Look(ref faction, "faction");
        Scribe_Values.Look(ref factionName, "factionName", "Unknown Faction");
        Scribe_Values.Look(ref groupKind, "groupKind", IncursionGroupKind.GenericNeutral);
        
        int originId = originTile.tileId;
        Scribe_Values.Look(ref originId, "originTileId", 0);
        if (Scribe.mode == LoadSaveMode.LoadingVars) originTile = (PlanetTile)originId;

        int destId = destinationTile.tileId;
        Scribe_Values.Look(ref destId, "destinationTileId", 0);
        if (Scribe.mode == LoadSaveMode.LoadingVars) destinationTile = (PlanetTile)destId;

        Scribe_Values.Look(ref createdTick, "createdTick", 0);
        Scribe_Values.Look(ref resolvedTick, "resolvedTick", 0);
        Scribe_Values.Look(ref outcome, "outcome", "Unknown");
        Scribe_Values.Look(ref initialHeadcount, "initialHeadcount", 0);
        Scribe_Values.Look(ref finalHeadcount, "finalHeadcount", 0);
        Scribe_Values.Look(ref humanlikeCount, "humanlikeCount", 0);
        Scribe_Values.Look(ref animalCount, "animalCount", 0);
        Scribe_Values.Look(ref mechCount, "mechCount", 0);
        Scribe_Values.Look(ref captivesTaken, "captivesTaken", 0);
        Scribe_Collections.Look(ref memberSummary, "memberSummary", LookMode.Value);
        Scribe_Collections.Look(ref assetSummary, "assetSummary", LookMode.Value);
    }
}

/// <summary>
/// High-fidelity structural breakdown of an incursion's force makeup, health condition,
/// carried equipment, and assets.
/// </summary>
public class ContactRosterBreakdown
{
    public int TrackingId { get; set; }
    public string Label { get; set; } = string.Empty;
    public IncursionGroupKind GroupKind { get; set; }
    public Faction? Faction { get; set; }
    public string FactionName { get; set; } = string.Empty;
    public IntelligenceTier IntelligenceTier { get; set; }

    // Headcounts
    public int HumanlikeCount { get; set; }
    public int AnimalCount { get; set; }
    public int MechanoidCount { get; set; }
    public int TotalHeadcount => HumanlikeCount + AnimalCount + MechanoidCount;

    // Health & Combat Status
    public int ConsciousCount { get; set; }
    public int DownedCount { get; set; }
    public int InjuredCount { get; set; }
    public int BleedingCount { get; set; }

    // Captives
    public int KidnappedColonistCount { get; set; }

    // Pawns
    public List<Pawn> AllPawns { get; } = new();
    public List<Pawn> Humanlikes { get; } = new();
    public List<Pawn> Animals { get; } = new();
    public List<Pawn> Mechanoids { get; } = new();
    public List<Pawn> Captives { get; } = new();

    // Gear & Assets Summary
    public List<string> DistinctWeapons { get; } = new();
    public List<string> DistinctPackAnimals { get; } = new();
    public List<string> DistinctMechanoidTypes { get; } = new();
    public int CarriedMedicineCount { get; set; }
    public int CarriedFoodCount { get; set; }
    public int TradeGoodsItemCount { get; set; }
}

/// <summary>
/// WorldComponent database and central registry tracking all active, engaged, retreating,
/// and historical planetary contacts by unique Tracking ID. Provides fast indexing, roster queries,
/// and permanent operational archives.
/// </summary>
public class ContactRegistry : WorldComponent
{
    public const int MaxHistoricalRecords = 150;

    private int nextContactTrackingId = 1;
    private readonly Dictionary<int, WorldObject_Incursion> activeContacts = new();
    private List<ContactArchiveRecord> historicalRecords = new();

    public static ContactRegistry? Instance => Find.World?.GetComponent<ContactRegistry>();

    public int NextContactTrackingId => nextContactTrackingId;

    public void SetNextTrackingId(int val)
    {
        if (val > nextContactTrackingId)
        {
            nextContactTrackingId = val;
        }
    }

    public ContactRegistry(RimWorld.Planet.World world) : base(world)
    {
    }

    /// <summary>
    /// Allocates a permanent, globally unique sequential tracking ID (e.g. Contact-1, Contact-2).
    /// </summary>
    public int AllocateTrackingId()
    {
        return nextContactTrackingId++;
    }

    // =========================================================================
    // Query API
    // =========================================================================

    public bool TryGetContact(int trackingId, out WorldObject_Incursion? contact)
    {
        return activeContacts.TryGetValue(trackingId, out contact);
    }

    public WorldObject_Incursion? GetContact(int trackingId)
    {
        activeContacts.TryGetValue(trackingId, out var contact);
        return contact;
    }

    public IReadOnlyList<WorldObject_Incursion> AllActiveContacts => activeContacts.Values.ToList();

    public int ActiveContactCount => activeContacts.Count;

    public IEnumerable<WorldObject_Incursion> HostileContacts =>
        activeContacts.Values.Where(c => c != null && c.Spawned && c.IsHostileToPlayer);

    public IEnumerable<WorldObject_Incursion> NeutralOrAlliedContacts =>
        activeContacts.Values.Where(c => c != null && c.Spawned && !c.IsHostileToPlayer);

    public IEnumerable<WorldObject_Incursion> RetreatingContacts =>
        activeContacts.Values.Where(c => c != null && c.Spawned && c.IsRetreating);

    public IEnumerable<WorldObject_Incursion> EngagedContacts =>
        activeContacts.Values.Where(c => c != null && c.Spawned && c.IsEngagedInTacticalMap);

    public IReadOnlyList<ContactArchiveRecord> HistoricalRecords => historicalRecords;

    public bool TryGetArchiveRecord(int trackingId, out ContactArchiveRecord? record)
    {
        record = historicalRecords.FirstOrDefault(r => r.trackingId == trackingId);
        return record != null;
    }

    /// <summary>
    /// Generates a comprehensive breakdown of an incursion's force makeup, health status,
    /// animals, mechs, carried weapons, and inventory.
    /// </summary>
    public ContactRosterBreakdown GetRosterBreakdown(WorldObject_Incursion incursion)
    {
        var breakdown = new ContactRosterBreakdown
        {
            TrackingId = incursion.TrackingId,
            Label = incursion.LabelCap,
            GroupKind = incursion.GroupKind,
            Faction = incursion.Faction,
            FactionName = incursion.Faction?.Name ?? "Unknown Faction",
            IntelligenceTier = incursion.CurrentIntelligenceTier,
            KidnappedColonistCount = incursion.KidnappedColonists?.Count ?? 0
        };

        if (incursion.KidnappedColonists != null)
        {
            breakdown.Captives.AddRange(incursion.KidnappedColonists.Where(p => p != null));
        }

        var pawns = (incursion.CommittedPawns != null && incursion.CommittedPawns.Count > 0)
            ? incursion.CommittedPawns
            : incursion.RetreatingPawns;

        if (pawns == null || pawns.Count == 0)
        {
            return breakdown;
        }

        HashSet<string> weaponsSet = new();
        HashSet<string> packAnimalsSet = new();
        HashSet<string> mechsSet = new();

        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn p = pawns[i];
            if (p == null || p.Destroyed) continue;

            breakdown.AllPawns.Add(p);

            // Race & Category Classification
            if (p.RaceProps.Humanlike)
            {
                breakdown.HumanlikeCount++;
                breakdown.Humanlikes.Add(p);
            }
            else if (p.RaceProps.Animal)
            {
                breakdown.AnimalCount++;
                breakdown.Animals.Add(p);
                packAnimalsSet.Add(p.KindLabel);
            }
            else if (p.RaceProps.IsMechanoid)
            {
                breakdown.MechanoidCount++;
                breakdown.Mechanoids.Add(p);
                mechsSet.Add(p.KindLabel);
            }

            // Health Evaluation
            if (p.Dead)
            {
                continue;
            }

            if (p.Downed)
            {
                breakdown.DownedCount++;
            }
            else
            {
                breakdown.ConsciousCount++;
            }

            if (p.health != null)
            {
                if (p.health.hediffSet != null && p.health.hediffSet.hediffs.Any(h => h.Visible && h.def.isBad))
                {
                    breakdown.InjuredCount++;
                }

                if (p.health.hediffSet != null && p.health.hediffSet.BleedRateTotal > 0.01f)
                {
                    breakdown.BleedingCount++;
                }
            }

            // Weapons
            if (p.equipment?.Primary != null)
            {
                weaponsSet.Add(p.equipment.Primary.LabelCapNoCount);
            }

            // Inventory Provisions
            if (p.inventory?.innerContainer != null)
            {
                foreach (Thing item in p.inventory.innerContainer)
                {
                    if (item is Medicine med)
                    {
                        breakdown.CarriedMedicineCount += med.stackCount;
                    }
                    else if (item.def.IsNutritionGivingIngestible)
                    {
                        breakdown.CarriedFoodCount += item.stackCount;
                    }
                }
            }
        }

        // Trade Stock
        if (incursion.TradeStock != null)
        {
            breakdown.TradeGoodsItemCount = incursion.TradeStock.Count;
        }

        breakdown.DistinctWeapons.AddRange(weaponsSet.OrderBy(w => w));
        breakdown.DistinctPackAnimals.AddRange(packAnimalsSet.OrderBy(a => a));
        breakdown.DistinctMechanoidTypes.AddRange(mechsSet.OrderBy(m => m));

        return breakdown;
    }

    public ContactRosterBreakdown? GetRosterBreakdown(int trackingId)
    {
        if (TryGetContact(trackingId, out var incursion) && incursion != null)
        {
            return GetRosterBreakdown(incursion);
        }
        return null;
    }

    // =========================================================================
    // Lifecycle Management
    // =========================================================================

    public void RegisterContact(WorldObject_Incursion incursion)
    {
        if (incursion == null) return;

        if (incursion.TrackingId <= 0)
        {
            incursion.TrackingId = AllocateTrackingId();
        }

        activeContacts[incursion.TrackingId] = incursion;
    }

    public void DeregisterContact(WorldObject_Incursion incursion, string outcomeReason = "Disbanded")
    {
        if (incursion == null) return;

        if (activeContacts.Remove(incursion.TrackingId))
        {
            ArchiveContact(incursion, outcomeReason);
        }
    }

    public void Notify_ContactDestroyed(WorldObject_Incursion incursion, string outcomeReason = "Neutralized")
    {
        if (incursion == null) return;

        DeregisterContact(incursion, outcomeReason);
    }

    public void Notify_ContactArrival(WorldObject_Incursion incursion, Map? targetMap)
    {
        if (incursion == null) return;

        string outcome = targetMap?.IsPlayerHome == true ? "AssaultedColony" : "AssaultedOutpost";
        DeregisterContact(incursion, outcome);
    }

    public void Notify_ContactReturnedToBase(WorldObject_Incursion incursion)
    {
        if (incursion == null) return;

        DeregisterContact(incursion, "ReturnedToBaseSafely");
    }

    public void Notify_ContactRemoved(WorldObject_Incursion incursion)
    {
        if (incursion == null) return;

        activeContacts.Remove(incursion.TrackingId);
    }

    private void ArchiveContact(WorldObject_Incursion incursion, string outcomeReason)
    {
        try
        {
            var record = new ContactArchiveRecord(incursion, outcomeReason);
            historicalRecords.Add(record);

            if (historicalRecords.Count > MaxHistoricalRecords)
            {
                historicalRecords.RemoveAt(0);
            }

            Log.Message($"[Project Vigilance] Archived Contact #{incursion.TrackingId} ({incursion.LabelCap}). Outcome: {outcomeReason}. Headcount: {record.finalHeadcount}/{record.initialHeadcount}.");
        }
        catch (Exception ex)
        {
            Log.Warning($"[Project Vigilance] Failed to archive incursion #{incursion.TrackingId}: {ex.Message}");
        }
    }

    // =========================================================================
    // Save / Load & Initialization
    // =========================================================================

    public override void FinalizeInit(bool fromLoad)
    {
        base.FinalizeInit(fromLoad);

        // Re-index all spawned incursions into the registry
        activeContacts.Clear();
        if (Find.WorldObjects != null)
        {
            var allIncursions = Find.WorldObjects.AllWorldObjects.OfType<WorldObject_Incursion>().ToList();
            for (int i = 0; i < allIncursions.Count; i++)
            {
                var inc = allIncursions[i];
                if (inc.Spawned)
                {
                    if (inc.TrackingId <= 0)
                    {
                        inc.TrackingId = AllocateTrackingId();
                    }
                    activeContacts[inc.TrackingId] = inc;
                }
            }
        }

        // Backward compatibility sync with SensoryManager
        if (SensoryManager.Instance != null && SensoryManager.Instance.NextContactTrackingId > nextContactTrackingId)
        {
            nextContactTrackingId = SensoryManager.Instance.NextContactTrackingId;
        }

        Log.Message($"[Project Vigilance] ContactRegistry initialized. Active contacts: {activeContacts.Count}, Historical records: {historicalRecords.Count}, Next ID: {nextContactTrackingId}.");
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref nextContactTrackingId, "nextContactTrackingId", 1);
        Scribe_Collections.Look(ref historicalRecords, "historicalRecords", LookMode.Deep);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            historicalRecords ??= new List<ContactArchiveRecord>();
        }
    }
}

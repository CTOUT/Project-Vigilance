using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Orbital;

/// <summary>
/// WorldComponent managing the global orbital ecosystem, orbital stations, drifting satellite constellations,
/// and orbital faction operational networks.
/// </summary>
public class OrbitalManager : WorldComponent
{
    public const int MaxAutoSpawnedNpcStations = 6;
    private const int InfrastructureAuditIntervalTicks = 15000;

    internal static Action<WorldObject>? RemoveWorldObjectOverride { get; set; }

    private readonly struct FactionRoleKey : IEquatable<FactionRoleKey>
    {
        public readonly Faction Faction;
        public readonly SatelliteRole Role;

        public FactionRoleKey(Faction faction, SatelliteRole role)
        {
            Faction = faction;
            Role = role;
        }

        public bool Equals(FactionRoleKey other) =>
            ReferenceEquals(Faction, other.Faction) && Role == other.Role;

        public override bool Equals(object? obj) =>
            obj is FactionRoleKey other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(RuntimeHelpers.GetHashCode(Faction), (int)Role);
    }

    private readonly List<WorldObject_OrbitalStation> stations = new(16);
    private readonly List<WorldObject_Satellite> satellites = new(32);
    private readonly List<Satellite_Trade> tmpTradeSatellites = new(8);
    private bool pendingPostLoadReconciliation;

    internal bool PendingPostLoadReconciliation => pendingPostLoadReconciliation;

    [ThreadStatic]
    private static OrbitalManager? testInstance;
    private static OrbitalManager? cachedInstance;

    public static OrbitalManager? Instance
    {
        get
        {
            if (testInstance != null) return testInstance;
            RimWorld.Planet.World? currentWorld = Find.World;
            if (currentWorld == null) return null;
            if (cachedInstance == null || cachedInstance.world != currentWorld)
            {
                cachedInstance = currentWorld.GetComponent<OrbitalManager>();
            }
            return cachedInstance;
        }
        set => testInstance = value;
    }

    public IReadOnlyList<WorldObject_OrbitalStation> Stations => stations;
    public IReadOnlyList<WorldObject_Satellite> Satellites => satellites;

    public OrbitalManager(RimWorld.Planet.World world) : base(world)
    {
    }

    public override void FinalizeInit(bool fromLoad)
    {
        base.FinalizeInit(fromLoad);
        RescanWorldObjects();

        // World.FinalizeInit(fromLoad: true) runs before ScribeLoader.FinalizeLoading() resolves
        // cross-references (WorldObject.Faction and Settlement.Faction are still null here).
        // Defer pruning and infrastructure checks until GameComponent_OrbitalLifecycle.LoadedGame().
        if (fromLoad || Scribe.mode != LoadSaveMode.Inactive)
        {
            pendingPostLoadReconciliation = true;
            return;
        }

        pendingPostLoadReconciliation = false;
        PruneDuplicateNpcOrbitalEntities("WorldInit");
        EnsureOrbitalFactionInfrastructure();
    }

    /// <summary>
    /// Executes deferred save-load deduplication and infrastructure reconciliation once
    /// ScribeLoader.FinalizeLoading() has resolved all WorldObject and Settlement Faction references.
    /// </summary>
    public void CompletePostLoadReconciliation()
    {
        if (!pendingPostLoadReconciliation || Scribe.mode != LoadSaveMode.Inactive)
        {
            return;
        }

        pendingPostLoadReconciliation = false;
        RescanWorldObjects();
        PruneDuplicateNpcOrbitalEntities("SaveLoad");
        EnsureOrbitalFactionInfrastructure();
    }

    public override void WorldComponentTick()
    {
        base.WorldComponentTick();

        if (pendingPostLoadReconciliation && Scribe.mode == LoadSaveMode.Inactive)
        {
            CompletePostLoadReconciliation();
        }

        int ticks = Current.Game?.tickManager?.TicksGame ?? 0;
        if (ticks > 0 && ticks % InfrastructureAuditIntervalTicks == 0)
        {
            EnsureOrbitalFactionInfrastructure();
        }
    }

    public void RegisterStation(WorldObject_OrbitalStation station)
    {
        if (station != null && !stations.Contains(station))
        {
            stations.Add(station);
        }
    }

    public void DeregisterStation(WorldObject_OrbitalStation station)
    {
        if (station != null)
        {
            stations.Remove(station);
        }
    }

    public void RegisterSatellite(WorldObject_Satellite satellite)
    {
        if (satellite != null && !satellites.Contains(satellite))
        {
            satellites.Add(satellite);
        }
    }

    public void DeregisterSatellite(WorldObject_Satellite satellite)
    {
        if (satellite != null)
        {
            satellites.Remove(satellite);
        }
    }

    public WorldObject_Satellite? GetSatelliteById(int id)
    {
        for (int i = 0; i < satellites.Count; i++)
        {
            if (satellites[i].ID == id) return satellites[i];
        }
        return null;
    }

    public WorldObject_OrbitalStation? GetStationById(int id)
    {
        for (int i = 0; i < stations.Count; i++)
        {
            if (stations[i].ID == id) return stations[i];
        }
        return null;
    }

    public void RescanWorldObjects()
    {
        stations.Clear();
        satellites.Clear();

        if (Find.WorldObjects == null) return;

        var allObjects = Find.WorldObjects.AllWorldObjects;
        for (int i = 0; i < allObjects.Count; i++)
        {
            var wo = allObjects[i];
            if (wo is WorldObject_OrbitalStation station)
            {
                stations.Add(station);
            }
            else if (wo is WorldObject_Satellite sat)
            {
                satellites.Add(sat);
            }
        }
    }

    /// <summary>
    /// Scans for orbital-only factions (such as TradersGuild or factions with no ground settlements)
    /// and ensures they maintain an operational persistent orbital station and satellite network.
    /// Also guarantees a minimum global trade fleet (2-3 vessels) to align with vanilla storyteller cadences.
    /// Pre-indexes ground settlements in O(S) so faction classification is O(1).
    /// </summary>
    public void EnsureOrbitalFactionInfrastructure()
    {
        if (Scribe.mode != LoadSaveMode.Inactive) return;

        FactionManager? factionManager = Find.FactionManager;
        if (factionManager == null || Find.WorldObjects == null) return;

        // Pre-gather all factions with surface settlements once in O(S) so per-faction checks are O(1)
        var settlements = Find.WorldObjects.Settlements;
        HashSet<Faction>? factionsWithSettlements = null;
        if (settlements != null && settlements.Count > 0)
        {
            factionsWithSettlements = new HashSet<Faction>(settlements.Count);
            for (int i = 0; i < settlements.Count; i++)
            {
                if (settlements[i].Faction != null)
                {
                    factionsWithSettlements.Add(settlements[i].Faction);
                }
            }
        }

        foreach (Faction faction in factionManager.AllFactionsVisible)
        {
            if (IsOrbitalFaction(faction, factionsWithSettlements))
            {
                EnsureStationForFaction(faction);
            }
        }

        // Maintain at least 2 global trade satellites across friendly/civil factions
        // to provide a physical trade pass cadence of ~8 to 12 days per colony.
        int tradeSatCount = 0;
        for (int i = 0; i < satellites.Count; i++)
        {
            if (satellites[i].Role == SatelliteRole.Trade)
            {
                tradeSatCount++;
            }
        }

        if (tradeSatCount < 2 && CountNonPlayerOrbitalStations() < MaxAutoSpawnedNpcStations)
        {
            Faction? playerFaction = (Find.World != null) ? Faction.OfPlayerSilentFail : null;
            foreach (Faction f in factionManager.AllFactionsVisible)
            {
                bool isHostile = playerFaction != null ? f.HostileTo(playerFaction) : (f.def?.permanentEnemy ?? false);
                if (!f.IsPlayer && !f.defeated && !f.Hidden && !f.temporary && !isHostile)
                {
                    if (EnsureStationForFaction(f) != null)
                    {
                        tradeSatCount++;
                    }
                    if (tradeSatCount >= 2 || CountNonPlayerOrbitalStations() >= MaxAutoSpawnedNpcStations) break;
                }
            }
        }
    }

    private int CountNonPlayerOrbitalStations()
    {
        int count = 0;
        for (int i = 0; i < stations.Count; i++)
        {
            WorldObject_OrbitalStation? st = stations[i];
            if (st != null && st.Faction?.IsPlayer != true)
            {
                count++;
            }
        }

        if (count > 0 || Find.World?.worldObjects == null)
        {
            return count;
        }

        var all = Find.World.worldObjects.AllWorldObjects;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] is WorldObject_OrbitalStation st && st.Faction?.IsPlayer != true)
            {
                count++;
            }
        }

        return count;
    }

    public static bool IsOrbitalFaction(Faction? faction, HashSet<Faction>? factionsWithSettlements = null)
    {
        if (faction == null || faction.defeated || faction.temporary) return false;

        // TradersGuild or factions with def name indicating orbital/space nature
        string defName = faction.def?.defName ?? string.Empty;
        if (defName.Equals("TradersGuild", StringComparison.OrdinalIgnoreCase) ||
            defName.IndexOf("Orbital", StringComparison.OrdinalIgnoreCase) >= 0 ||
            defName.IndexOf("Space", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        // Factions with 0 surface settlements that are non-hidden, non-defeated, non-temporary, and at least industrial tech
        if (!faction.IsPlayer &&
            !faction.Hidden &&
            (faction.def == null || faction.def.techLevel >= TechLevel.Industrial))
        {
            if (factionsWithSettlements != null)
            {
                return !factionsWithSettlements.Contains(faction);
            }

            int settlementCount = 0;
            if (Find.WorldObjects != null)
            {
                var settlements = Find.WorldObjects.Settlements;
                for (int i = 0; i < settlements.Count; i++)
                {
                    if (settlements[i].Faction == faction)
                    {
                        settlementCount++;
                        break;
                    }
                }
            }
            return settlementCount == 0;
        }

        return false;
    }

    /// <summary>
    /// Searches for any existing orbital station or space settlement belonging to the faction,
    /// prioritising world-generation sites (such as Odyssey's SpaceSettlement on the Orbit layer)
    /// before falling back to custom orbital stations.
    /// </summary>
    public WorldObject? FindExistingOrbitalStationForFaction(Faction? faction)
    {
        if (faction == null || Find.World?.worldObjects == null) return null;

        // 1. Look for existing world-generation orbital settlements (e.g. Odyssey SpaceSettlement)
        var settlements = Find.World.worldObjects.Settlements;
        if (settlements != null)
        {
            for (int i = 0; i < settlements.Count; i++)
            {
                var s = settlements[i];
                if (s.Faction == faction && (s.def?.defName == "SpaceSettlement" || s.Tile.Layer?.Def?.defName == "Orbit"))
                {
                    return s;
                }
            }
        }

        // 2. Look for existing custom WorldObject_OrbitalStation instances in cache
        for (int i = 0; i < stations.Count; i++)
        {
            if (stations[i] != null && stations[i].Faction == faction)
            {
                return stations[i];
            }
        }

        // 3. Fallback scan of AllWorldObjects in case a loaded/unregistered station exists
        var all = Find.World.worldObjects.AllWorldObjects;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] is WorldObject_OrbitalStation st && st.Faction == faction)
            {
                RegisterStation(st);
                return st;
            }
        }

        return null;
    }

    /// <summary>
    /// Identifies a valid, populated ground tile within active world generation coverage to anchor
    /// an orbital station's initial orbit ground track, prioritizing faction settlements, player home,
    /// or populated world hexes.
    /// </summary>
    public static PlanetTile FindPopulatedCoverageAnchorTile(Faction? faction)
    {
        if (Find.World?.grid == null)
        {
            return PlanetTile.Invalid;
        }

        // 1. Faction's own settlement
        if (faction != null && Find.WorldObjects != null)
        {
            var settlements = Find.WorldObjects.Settlements;
            for (int i = 0; i < settlements.Count; i++)
            {
                if (settlements[i].Faction == faction && settlements[i].Tile.Valid)
                {
                    return settlements[i].Tile;
                }
            }
        }

        // 2. Player colony map
        if (Find.AnyPlayerHomeMap != null)
        {
            int playerTile = Find.AnyPlayerHomeMap.Tile;
            if (playerTile >= 0)
            {
                return new PlanetTile(playerTile, Find.World.grid.Surface);
            }
        }

        // 3. Any populated settlement on the globe
        if (Find.WorldObjects != null && Find.WorldObjects.Settlements.Count > 0)
        {
            var anySettlement = Find.WorldObjects.Settlements.RandomElement();
            if (anySettlement != null && anySettlement.Tile.Valid)
            {
                return anySettlement.Tile;
            }
        }

        // 4. Sample generated surface tiles with valid non-ocean biomes (coverage aware)
        for (int attempt = 0; attempt < 100; attempt++)
        {
            int candidate = Rand.Range(0, Find.World.grid.TilesCount);
            var tileObj = Find.World.grid[candidate];
            if (tileObj != null && !tileObj.WaterCovered && tileObj.PrimaryBiome != null && !tileObj.PrimaryBiome.impassable)
            {
                return new PlanetTile(candidate, Find.World.grid.Surface);
            }
        }

        return new PlanetTile(0, Find.World.grid.Surface);
    }

    public WorldObject? EnsureStationForFaction(Faction faction)
    {
        WorldObject? existing = FindExistingOrbitalStationForFaction(faction);
        if (existing != null)
        {
            EnsureConstellationForStation(existing);
            return existing;
        }

        if (CountNonPlayerOrbitalStations() >= MaxAutoSpawnedNpcStations)
        {
            return null;
        }

        // If no station exists from world generation (e.g. base game without Odyssey),
        // spawn a persistent fallback WorldObject_OrbitalStation for this faction.
        WorldObjectDef? stationDef = OrbitalDefOf.Vigilance_OrbitalStation ??
            DefDatabase<WorldObjectDef>.GetNamedSilentFail("Vigilance_OrbitalStation");

        if (stationDef == null) return null;

        var station = (WorldObject_OrbitalStation)WorldObjectMaker.MakeWorldObject(stationDef);
        station.SetFaction(faction);

        // Place on a valid populated anchor tile within world generation coverage
        PlanetTile initialTile = FindPopulatedCoverageAnchorTile(faction);
        if (initialTile.Valid && Find.WorldGrid != null)
        {
            float stationAltitude = Rand.Range(7000f, 9500f);
            int stationPeriod = Rand.Range(74000, 84000);
            float extraInc = Rand.Range(10f, 20f);
            float driftRate = Rand.Range(1.2f, 2.2f);

            OrbitalParameters stationOrbit = OrbitalFlightModel.CreateOverheadOrbitParameters(
                initialTile,
                altitudeKm: stationAltitude,
                periodTicks: stationPeriod,
                extraInclinationDeg: extraInc,
                precessionRateDegPerDay: -1.5f,
                orbitalDriftDegPerDay: driftRate);

            station.InitializeStation($"{faction.Name} Logistics Hub", stationOrbit);
        }

        station.Tile = initialTile;
        Find.WorldObjects.Add(station);

        EnsureConstellationForStation(station);
        return station;
    }

    public void EnsureConstellationForStation(WorldObject station)
    {
        if (station == null || station.Faction == null) return;

        // Count active trade satellites linked to this parent station or owning faction
        bool hasTrade = false;

        for (int i = 0; i < satellites.Count; i++)
        {
            var sat = satellites[i];
            if (sat != null &&
                sat.Role == SatelliteRole.Trade &&
                (sat.ParentStationId == station.ID || sat.Faction == station.Faction))
            {
                hasTrade = true;
                break;
            }
        }

        if (!hasTrade && Find.World?.worldObjects != null)
        {
            var all = Find.World.worldObjects.AllWorldObjects;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] is WorldObject_Satellite sat &&
                    sat.Role == SatelliteRole.Trade &&
                    (sat.ParentStationId == station.ID || sat.Faction == station.Faction))
                {
                    RegisterSatellite(sat);
                    hasTrade = true;
                    break;
                }
            }
        }

        if (!hasTrade)
        {
            SpawnSatelliteForStation(station, SatelliteRole.Trade);
        }
    }

    /// <summary>
    /// Deduplicates NPC orbital stations by faction and NPC satellites by (faction, role),
    /// removing orphaned or excess duplicates caused by legacy save inflation while preserving
    /// all player assets and Odyssey world-gen SpaceSettlements. Logs a warning whenever objects
    /// are pruned so repeated occurrences can be diagnosed immediately.
    /// </summary>
    public (int prunedStations, int prunedSatellites) PruneDuplicateNpcOrbitalEntities(
        string triggerReason = "SaveLoad",
        bool logWarningOnPrune = true)
    {
        if (Scribe.mode != LoadSaveMode.Inactive)
        {
            return (0, 0);
        }

        var all = Find.World?.worldObjects?.AllWorldObjects;
        if (all == null || all.Count == 0)
        {
            return (0, 0);
        }

        var odysseyFactions = new HashSet<Faction>(ReferenceEqualityComparer.Instance);
        var npcStations = new List<WorldObject_OrbitalStation>(16);
        var npcSatellites = new List<WorldObject_Satellite>(32);

        for (int i = 0; i < all.Count; i++)
        {
            WorldObject wo = all[i];
            if (wo == null)
            {
                continue;
            }

            if (wo is WorldObject_OrbitalStation st)
            {
                if (st.Faction?.IsPlayer != true)
                {
                    npcStations.Add(st);
                }
                continue;
            }

            if (wo is WorldObject_Satellite sat)
            {
                if (sat.Faction?.IsPlayer != true)
                {
                    npcSatellites.Add(sat);
                }
                continue;
            }

            if (wo.Faction != null &&
                !wo.Faction.IsPlayer &&
                (wo.def?.defName == "SpaceSettlement" || wo.Tile.Layer?.Def?.defName == "Orbit"))
            {
                odysseyFactions.Add(wo.Faction);
            }
        }

        if (npcStations.Count == 0 && npcSatellites.Count == 0)
        {
            return (0, 0);
        }

        var survivingStationsByFaction = new Dictionary<Faction, WorldObject_OrbitalStation>(ReferenceEqualityComparer.Instance);
        var stationsToRemove = new List<WorldObject_OrbitalStation>();

        for (int i = 0; i < npcStations.Count; i++)
        {
            WorldObject_OrbitalStation st = npcStations[i];
            Faction? faction = st.Faction;
            if (faction == null || faction.defeated || faction.temporary || odysseyFactions.Contains(faction))
            {
                stationsToRemove.Add(st);
                continue;
            }

            if (!survivingStationsByFaction.TryGetValue(faction, out WorldObject_OrbitalStation? existing))
            {
                survivingStationsByFaction[faction] = st;
            }
            else if (st.ID < existing.ID)
            {
                stationsToRemove.Add(existing);
                survivingStationsByFaction[faction] = st;
            }
            else
            {
                stationsToRemove.Add(st);
            }
        }

        var survivingSatsByKey = new Dictionary<FactionRoleKey, WorldObject_Satellite>();
        var satellitesToRemove = new List<WorldObject_Satellite>();

        for (int i = 0; i < npcSatellites.Count; i++)
        {
            WorldObject_Satellite sat = npcSatellites[i];
            Faction? faction = sat.Faction;
            if (faction == null || faction.defeated || faction.temporary)
            {
                satellitesToRemove.Add(sat);
                continue;
            }

            var key = new FactionRoleKey(faction, sat.Role);
            if (!survivingSatsByKey.TryGetValue(key, out WorldObject_Satellite? existing))
            {
                survivingSatsByKey[key] = sat;
                continue;
            }

            survivingStationsByFaction.TryGetValue(faction, out WorldObject_OrbitalStation? factionHub);
            bool satPreferred = factionHub != null && sat.ParentStationId == factionHub.ID;
            bool existingPreferred = factionHub != null && existing.ParentStationId == factionHub.ID;

            if ((satPreferred && !existingPreferred) ||
                (satPreferred == existingPreferred && sat.ID < existing.ID))
            {
                satellitesToRemove.Add(existing);
                survivingSatsByKey[key] = sat;
            }
            else
            {
                satellitesToRemove.Add(sat);
            }
        }

        var validSatIds = new HashSet<int>();
        var tradeSatByFaction = new Dictionary<Faction, int>(ReferenceEqualityComparer.Instance);
        foreach (WorldObject_Satellite survivorSat in survivingSatsByKey.Values)
        {
            validSatIds.Add(survivorSat.ID);
            Faction? satFaction = survivorSat.Faction;
            if (satFaction == null)
            {
                continue;
            }

            if (survivingStationsByFaction.TryGetValue(satFaction, out WorldObject_OrbitalStation? hub) &&
                survivorSat.ParentStationId != hub.ID)
            {
                survivorSat.ReassignParentStation(hub.ID);
            }

            if (survivorSat.Role == SatelliteRole.Trade)
            {
                tradeSatByFaction[satFaction] = survivorSat.ID;
            }
        }

        foreach (WorldObject_OrbitalStation hub in survivingStationsByFaction.Values)
        {
            int? attachedTradeId = hub.Faction != null && tradeSatByFaction.TryGetValue(hub.Faction, out int satId)
                ? satId
                : null;
            hub.ReconcileChildSatellites(validSatIds, attachedTradeId);
        }

        if (stationsToRemove.Count == 0 && satellitesToRemove.Count == 0)
        {
            return (0, 0);
        }

        for (int i = 0; i < stationsToRemove.Count; i++)
        {
            WorldObject_OrbitalStation st = stationsToRemove[i];
            DeregisterStation(st);
            RemoveOrbitalWorldObject(st);
        }

        for (int i = 0; i < satellitesToRemove.Count; i++)
        {
            WorldObject_Satellite sat = satellitesToRemove[i];
            DeregisterSatellite(sat);
            RemoveOrbitalWorldObject(sat);
        }

        RescanWorldObjects();
        OrbitalVisibilityUtility.ClearDetectionCaches();

        if (logWarningOnPrune)
        {
            Log.Warning(
                $"[Project Vigilance] Orbital deduplication ({triggerReason}) pruned {stationsToRemove.Count} duplicate/orphaned NPC station(s) " +
                $"and {satellitesToRemove.Count} duplicate/orphaned NPC satellite(s); remaining active NPC hubs={survivingStationsByFaction.Count}, " +
                $"remaining NPC satellites={survivingSatsByKey.Count}. This cleanup should only appear once when migrating an affected legacy save — " +
                "please report a bug if this warning repeats on subsequent loads.");
        }

        return (stationsToRemove.Count, satellitesToRemove.Count);
    }

    private static void RemoveOrbitalWorldObject(WorldObject obj)
    {
        if (RemoveWorldObjectOverride != null)
        {
            RemoveWorldObjectOverride(obj);
            return;
        }

        Find.World?.worldObjects?.Remove(obj);
    }

    public WorldObject_Satellite? SpawnSatelliteForStation(WorldObject station, SatelliteRole role)
    {
        WorldObjectDef? def = role switch
        {
            SatelliteRole.Trade => OrbitalDefOf.Vigilance_Satellite_Trade ?? DefDatabase<WorldObjectDef>.GetNamedSilentFail("Vigilance_Satellite_Trade"),
            SatelliteRole.Reconnaissance => OrbitalDefOf.Vigilance_Satellite_Recon ?? DefDatabase<WorldObjectDef>.GetNamedSilentFail("Vigilance_Satellite_Recon"),
            SatelliteRole.Navigation => OrbitalDefOf.Vigilance_Satellite_Nav ?? DefDatabase<WorldObjectDef>.GetNamedSilentFail("Vigilance_Satellite_Nav"),
            SatelliteRole.Communication => OrbitalDefOf.Vigilance_Satellite_Comms ?? DefDatabase<WorldObjectDef>.GetNamedSilentFail("Vigilance_Satellite_Comms"),
            SatelliteRole.Military => OrbitalDefOf.Vigilance_Satellite_Military ?? DefDatabase<WorldObjectDef>.GetNamedSilentFail("Vigilance_Satellite_Military"),
            _ => OrbitalDefOf.Vigilance_Satellite_Recon
        };

        if (def == null) return null;

        var sat = (WorldObject_Satellite)WorldObjectMaker.MakeWorldObject(def);
        sat.SetFaction(station.Faction);

        PlanetTile anchorTile = station.Tile.Valid ? station.Tile : FindPopulatedCoverageAnchorTile(station.Faction);
        OrbitalParameters orbitParams = CreateOrbitalParametersForRole(role, anchorTile);
        string callsign = $"{station.Faction.Name} {role} Relay";
        float footprint = role switch
        {
            SatelliteRole.Navigation => 24f,
            SatelliteRole.Communication => 32f,
            SatelliteRole.Military => 18f,
            SatelliteRole.Trade => 16f,
            _ => 14f
        };

        sat.InitializeSatellite(role, orbitParams, station.ID, callsign, footprint);
        Find.WorldObjects.Add(sat);

        if (station is WorldObject_OrbitalStation customStation)
        {
            customStation.RegisterChildSatellite(sat);
        }

        return sat;
    }

    /// <summary>
    /// Gets the default orbital period in ticks for each satellite role, calibrated for playable tactical timescales
    /// and storyteller incident frequencies.
    /// </summary>
    public static int GetDefaultPeriodForRole(SatelliteRole role) => role switch
    {
        SatelliteRole.Trade => 62500, // 25.0 in-game hours: storyteller-aligned (~8-12 days revisit)
        SatelliteRole.Reconnaissance => 45000, // 18.0 in-game hours: polar sweeping
        SatelliteRole.Military => 75000, // 30.0 in-game hours: tactical deterrence
        SatelliteRole.Navigation => 120000, // 48.0 in-game hours (2 days): MEO constellation
        SatelliteRole.Communication => 180000, // 72.0 in-game hours (3 days): semi-geosynchronous relay
        _ => 60000
    };

    public static OrbitalParameters CreateOrbitalParametersForRole(SatelliteRole role, PlanetTile? anchorTile = null)
    {
        int now = Current.Game?.tickManager?.TicksGame ?? 0;
        float altitudeKm = role switch
        {
            SatelliteRole.Trade => Rand.Range(650f, 950f),
            SatelliteRole.Reconnaissance => Rand.Range(400f, 550f),
            SatelliteRole.Military => Rand.Range(1200f, 1800f),
            SatelliteRole.Navigation => Rand.Range(10500f, 13500f),
            SatelliteRole.Communication => Rand.Range(32000f, 35786f),
            _ => 600f
        };
        int periodTicks = GetDefaultPeriodForRole(role);
        float inclinationDeg = role switch
        {
            SatelliteRole.Trade => Rand.Range(35f, 48f),
            SatelliteRole.Reconnaissance => Rand.Range(85f, 98f), // Polar / Sun-synchronous
            SatelliteRole.Military => Rand.Range(55f, 65f),
            SatelliteRole.Navigation => Rand.Range(50f, 60f),
            SatelliteRole.Communication => Rand.Range(20f, 35f),
            _ => 45f
        };
        float precessionRate = role switch
        {
            SatelliteRole.Trade => -3.5f,
            SatelliteRole.Reconnaissance => -2.8f,
            SatelliteRole.Military => -3.1f,
            SatelliteRole.Navigation => -1.2f,
            SatelliteRole.Communication => -0.4f,
            _ => -1.5f
        };
        float driftRate = role switch
        {
            SatelliteRole.Trade => 0.8f,
            SatelliteRole.Reconnaissance => 0.6f,
            SatelliteRole.Military => 0.5f,
            SatelliteRole.Navigation => 0.4f,
            SatelliteRole.Communication => 0.2f,
            _ => 0.5f
        };

        if (anchorTile.HasValue && anchorTile.Value.Valid && Find.WorldGrid != null)
        {
            float targetLat = Find.WorldGrid.LongLatOf(anchorTile.Value).y;
            float targetLon = Find.WorldGrid.LongLatOf(anchorTile.Value).x;
            float extraInc = Mathf.Max(5f, inclinationDeg - Mathf.Abs(targetLat));

            return OrbitalFlightModel.CreateOverheadOrbitParameters(
                targetLat,
                targetLon,
                now,
                altitudeKm,
                periodTicks,
                extraInclinationDeg: extraInc,
                precessionRateDegPerDay: precessionRate,
                orbitalDriftDegPerDay: driftRate);
        }

        return new OrbitalParameters(
            altitudeKm: altitudeKm,
            periodTicks: periodTicks,
            inclinationDeg: inclinationDeg,
            ascendingNodeDeg: Rand.Range(0f, 360f),
            precessionRateDegPerDay: precessionRate,
            meanAnomalyEpochDeg: Rand.Range(0f, 360f),
            orbitalDriftDegPerDay: driftRate,
            epochTick: now);
    }

    public bool HasNavigationCoverage(PlanetTile tile)
    {
        if (!tile.Valid) return false;
        for (int i = 0; i < satellites.Count; i++)
        {
            var sat = satellites[i];
            if (sat.Role == SatelliteRole.Navigation && sat.CoversTile(tile))
            {
                return true;
            }
        }
        return false;
    }

    public bool HasCommunicationCoverage(PlanetTile tile)
    {
        if (!tile.Valid) return false;
        for (int i = 0; i < satellites.Count; i++)
        {
            var sat = satellites[i];
            if (sat.Role == SatelliteRole.Communication && sat.CoversTile(tile))
            {
                return true;
            }
        }
        return false;
    }

    public List<Satellite_Trade> GetActiveTradeSatellitesOverTile(PlanetTile tile)
    {
        tmpTradeSatellites.Clear();
        if (!tile.Valid) return tmpTradeSatellites;

        for (int i = 0; i < satellites.Count; i++)
        {
            if (satellites[i] is Satellite_Trade tradeSat && tradeSat.CoversTile(tile))
            {
                tmpTradeSatellites.Add(tradeSat);
            }
        }

        return tmpTradeSatellites;
    }
}

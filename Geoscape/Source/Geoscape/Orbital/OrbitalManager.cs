using System;
using System.Collections.Generic;
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
    private readonly List<WorldObject_OrbitalStation> stations = new(16);
    private readonly List<WorldObject_Satellite> satellites = new(32);
    private readonly List<Satellite_Trade> tmpTradeSatellites = new(8);

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
        EnsureOrbitalFactionInfrastructure();
    }

    public override void WorldComponentTick()
    {
        base.WorldComponentTick();

        int ticks = Current.Game?.tickManager?.TicksGame ?? 0;
        if (ticks % 1000 == 0)
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
    /// </summary>
    public void EnsureOrbitalFactionInfrastructure()
    {
        FactionManager? factionManager = Find.FactionManager;
        if (factionManager == null || Find.WorldObjects == null) return;

        foreach (Faction faction in factionManager.AllFactionsVisible)
        {
            if (IsOrbitalFaction(faction))
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

        if (tradeSatCount < 2)
        {
            Faction? playerFaction = (Find.World != null) ? Faction.OfPlayerSilentFail : null;
            foreach (Faction f in factionManager.AllFactionsVisible)
            {
                bool isHostile = playerFaction != null ? f.HostileTo(playerFaction) : (f.def?.permanentEnemy ?? false);
                if (!f.IsPlayer && !f.defeated && !f.Hidden && !isHostile)
                {
                    EnsureStationForFaction(f);
                    tradeSatCount++;
                    if (tradeSatCount >= 2) break;
                }
            }
        }
    }

    public static bool IsOrbitalFaction(Faction? faction)
    {
        if (faction == null) return false;

        // TradersGuild or factions with def name indicating orbital/space nature
        string defName = faction.def?.defName ?? string.Empty;
        if (defName.Equals("TradersGuild", StringComparison.OrdinalIgnoreCase) ||
            defName.IndexOf("Orbital", StringComparison.OrdinalIgnoreCase) >= 0 ||
            defName.IndexOf("Space", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        // Factions with 0 surface settlements that are non-hidden and non-defeated
        if (!faction.IsPlayer && !faction.defeated && !faction.Hidden)
        {
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

        // 2. Look for existing custom WorldObject_OrbitalStation instances
        for (int i = 0; i < stations.Count; i++)
        {
            if (stations[i].Faction == faction)
            {
                return stations[i];
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

        // Count active satellites linked to this parent station
        bool hasTrade = false;

        for (int i = 0; i < satellites.Count; i++)
        {
            var sat = satellites[i];
            if (sat.ParentStationId == station.ID)
            {
                if (sat.Role == SatelliteRole.Trade) hasTrade = true;
            }
        }

        if (!hasTrade)
        {
            SpawnSatelliteForStation(station, SatelliteRole.Trade);
        }
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

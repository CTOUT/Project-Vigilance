using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using ProjectVigilance.Geoscape.World;

namespace ProjectVigilance.Geoscape.Orbital;

/// <summary>
/// Persistent orbital operational centre and logistics hub for orbital factions (e.g. TradersGuild).
/// Manages satellite constellations, maintenance cycles, and automated satellite replenishment.
/// </summary>
public class WorldObject_OrbitalStation : WorldObject, IOrbitalEntity
{
    private OrbitalParameters orbit = new(
        altitudeKm: 8500f,
        periodTicks: 78000,
        inclinationDeg: 28f,
        ascendingNodeDeg: 45f,
        precessionRateDegPerDay: -1.5f,
        meanAnomalyEpochDeg: 90f,
        orbitalDriftDegPerDay: 2.0f,
        epochTick: 0);

    private string stationCallsign = "Logistics Hub";
    private float serviceAreaAngularRadiusDeg = 35f;
    private List<int> childSatelliteIds = new(8);
    private int replenishmentCountdownTicks = -1;
    private SatelliteRole pendingReplenishmentRole = SatelliteRole.Trade;

    public OrbitalParameters? Orbit => orbit;
    public string OrbitalCallsign => stationCallsign;
    public float ServiceAreaAngularRadiusDeg => serviceAreaAngularRadiusDeg;
    public IReadOnlyList<int> ChildSatelliteIds => childSatelliteIds;
    public bool InServiceArea => true;
    public int ParentStationId => ID;

    public void InitializeStation(string callsign, OrbitalParameters? customOrbit = null, float serviceAreaRadius = 35f)
    {
        stationCallsign = callsign;
        if (customOrbit != null)
        {
            orbit = customOrbit;
        }
        serviceAreaAngularRadiusDeg = serviceAreaRadius;
    }

    private Material? cachedFactionMat;
    private Faction? cachedFaction;

    public override bool VisibleInBackground => true;

    public override Color ExpandingIconColor => Faction != null ? Faction.Color : Color.white;

    public override Material Material =>
        OrbitalVisibilityUtility.ResolveOrbitalMaterial(this, ref cachedFactionMat, ref cachedFaction) ?? base.Material;

    public override void SetFaction(Faction newFaction) =>
        OrbitalVisibilityUtility.SetFactionSafe(this, newFaction, base.SetFaction, ref cachedFactionMat, ref cachedFaction);

    public PlanetTile CurrentSubSatelliteTile => OrbitalFlightModel.CalculateSubSatelliteTile(orbit, Current.Game?.tickManager?.TicksGame ?? 0, (PlanetTile)Tile);

    public Vector3 CurrentOrbitalPosition => OrbitalFlightModel.CalculateOrbitalPosition(
        orbit,
        Current.Game?.tickManager?.TicksGame ?? 0,
        altitudeRenderOffset: 25f);

    public override Vector3 DrawPos => CurrentOrbitalPosition;

    public override void SpawnSetup()
    {
        base.SpawnSetup();
        OrbitalManager.Instance?.RegisterStation(this);

        if (string.IsNullOrEmpty(stationCallsign) || stationCallsign == "Orbital Hub" || stationCallsign == "Logistics Hub")
        {
            stationCallsign = LabelCap.NullOrEmpty() ? "Hub-" + ID : LabelCap;
        }

        // Align tile to current sub-station coordinates
        SetTileSafe(CurrentSubSatelliteTile);
    }

    public override void PostRemove()
    {
        base.PostRemove();
        OrbitalManager.Instance?.DeregisterStation(this);
    }

    protected override void Tick()
    {
        base.Tick();

        int currentTick = Current.Game?.tickManager?.TicksGame ?? 0;
        if (currentTick % 250 == 0)
        {
            UpdateSubSatelliteTile(currentTick);
            CheckConstellationServiceZones();
            TickReplenishmentQueue();
        }
    }

    public void TickForTesting(int currentTick)
    {
        UpdateSubSatelliteTile(currentTick);
        CheckConstellationServiceZones();
        TickReplenishmentQueue();
    }

    private void UpdateSubSatelliteTile(int currentTick)
    {
        PlanetTile newTile = OrbitalFlightModel.CalculateSubSatelliteTile(orbit, currentTick, (PlanetTile)Tile);
        if (newTile.Valid && newTile != Tile)
        {
            SetTileSafe(newTile);
        }
    }

    private void SetTileSafe(PlanetTile newTile)
    {
        try
        {
            if (Find.World?.worldObjects != null && newTile.Tile != null)
            {
                Tile = newTile;
                return;
            }
        }
        catch (Exception)
        {
            // Fallback for layers or world states lacking ground tile definitions (e.g. FastTileFinder NRE)
        }

        WorldObjectAccessUtility.SetTileRaw(this, newTile);
    }

    internal void ReconcileChildSatellites(HashSet<int> validSatelliteIds, int? attachedTradeSatId = null)
    {
        for (int i = childSatelliteIds.Count - 1; i >= 0; i--)
        {
            if (!validSatelliteIds.Contains(childSatelliteIds[i]))
            {
                childSatelliteIds.RemoveAt(i);
            }
        }

        if (attachedTradeSatId.HasValue &&
            validSatelliteIds.Contains(attachedTradeSatId.Value) &&
            !childSatelliteIds.Contains(attachedTradeSatId.Value))
        {
            childSatelliteIds.Add(attachedTradeSatId.Value);
        }

        if (childSatelliteIds.Count > 0)
        {
            replenishmentCountdownTicks = -1;
        }
    }

    public void RegisterChildSatellite(WorldObject_Satellite satellite)
    {
        if (satellite != null && !childSatelliteIds.Contains(satellite.ID))
        {
            childSatelliteIds.Add(satellite.ID);
        }
    }

    public void UnregisterChildSatellite(int satelliteId)
    {
        childSatelliteIds.Remove(satelliteId);
    }

    public bool IsSatelliteInServiceArea(WorldObject_Satellite satellite)
    {
        if (satellite == null || satellite.Orbit == null || orbit == null)
        {
            return false;
        }

        int currentTick = Current.Game?.tickManager?.TicksGame ?? 0;
        Vector3 stationDir = OrbitalFlightModel.CalculateSubSatelliteDirection(orbit, currentTick);
        Vector3 satDir = OrbitalFlightModel.CalculateSubSatelliteDirection(satellite.Orbit, currentTick);

        float angularDistance = OrbitalFlightModel.AngularDistanceDeg(stationDir, satDir);
        return angularDistance <= serviceAreaAngularRadiusDeg;
    }

    private void CheckConstellationServiceZones()
    {
        if (childSatelliteIds.Count == 0 || Find.World?.worldObjects == null)
        {
            return;
        }

        for (int i = childSatelliteIds.Count - 1; i >= 0; i--)
        {
            int satId = childSatelliteIds[i];
            WorldObject_Satellite? sat = OrbitalManager.Instance?.GetSatelliteById(satId);
            if (sat == null)
            {
                // Lost or destroyed satellite; queue replenishment if not already queued
                childSatelliteIds.RemoveAt(i);
                QueueReplenishment(SatelliteRole.Trade);
                continue;
            }

            bool inZone = IsSatelliteInServiceArea(sat);
            if (inZone && !sat.InServiceArea)
            {
                sat.OnEnterServiceArea(this);
            }
            else if (!inZone && sat.InServiceArea)
            {
                sat.OnLeaveServiceArea(this);
            }
        }
    }

    public void QueueReplenishment(SatelliteRole role, int delayTicks = 180000)
    {
        if (replenishmentCountdownTicks < 0)
        {
            pendingReplenishmentRole = role;
            replenishmentCountdownTicks = delayTicks;
        }
    }

    private void TickReplenishmentQueue()
    {
        if (replenishmentCountdownTicks > 0)
        {
            replenishmentCountdownTicks -= 250;
            if (replenishmentCountdownTicks <= 0)
            {
                replenishmentCountdownTicks = -1;
                DeployReplacementSatellite(pendingReplenishmentRole);
            }
        }
    }

    private void DeployReplacementSatellite(SatelliteRole role)
    {
        OrbitalManager.Instance?.SpawnSatelliteForStation(this, role);
    }

    public void OnEnterServiceArea(WorldObject station)
    {
        // Hub self-reference
    }

    public void OnLeaveServiceArea(WorldObject station)
    {
        // Hub self-reference
    }

    public string GetStationInspectString()
    {
        StringBuilder sb = new();
        string alt = orbit.AltitudeKm.ToString("F0");
        string horizon = serviceAreaAngularRadiusDeg.ToString("F0");
        sb.AppendLine(ProjectVigilance.Core.Localization.VigilanceLoc.Tr("PV_Inspect_Station_Altitude", $"Orbital Altitude: {alt} km", alt));
        sb.AppendLine(ProjectVigilance.Core.Localization.VigilanceLoc.Tr("PV_Inspect_Station_ServiceHorizon", $"Service Horizon: {horizon}° angular sector", horizon));

        int activeChildren = 0;
        int inZoneCount = 0;
        if (Find.World?.worldObjects != null)
        {
            for (int i = 0; i < childSatelliteIds.Count; i++)
            {
                if (Find.World.worldObjects.AllWorldObjects.Find(wo => wo.ID == childSatelliteIds[i]) is WorldObject_Satellite sat)
                {
                    activeChildren++;
                    if (sat.InServiceArea) inZoneCount++;
                }
            }
        }

        sb.Append(ProjectVigilance.Core.Localization.VigilanceLoc.Tr(
            "PV_Inspect_Station_SatellitesInService",
            $"Satellite Constellation: {activeChildren} active ({inZoneCount} in service corridor)",
            activeChildren,
            inZoneCount));

        if (replenishmentCountdownTicks > 0)
        {
            string daysRemaining = ((float)replenishmentCountdownTicks / OrbitalFlightModel.TicksPerRimWorldDay).ToString("F1");
            sb.AppendLine();
            sb.Append(ProjectVigilance.Core.Localization.VigilanceLoc.Tr(
                "PV_Inspect_Station_Replenishing",
                $"Fabricating Replacement: {pendingReplenishmentRole} ({daysRemaining} days)",
                pendingReplenishmentRole.ToString(),
                daysRemaining));
        }

        return sb.ToString().TrimEnd();
    }

    public override string GetInspectString()
    {
        StringBuilder sb = new();
        string baseString = base.GetInspectString();
        if (!string.IsNullOrEmpty(baseString))
        {
            sb.AppendLine(baseString);
        }
        sb.Append(GetStationInspectString());
        return sb.ToString().TrimEnd();
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Deep.Look(ref orbit, "orbit");
        Scribe_Values.Look(ref stationCallsign, "stationCallsign", "Logistics Hub");
        Scribe_Values.Look(ref serviceAreaAngularRadiusDeg, "serviceAreaAngularRadiusDeg", 35f);
        Scribe_Collections.Look(ref childSatelliteIds, "childSatelliteIds", LookMode.Value);
        Scribe_Values.Look(ref replenishmentCountdownTicks, "replenishmentCountdownTicks", -1);
        Scribe_Values.Look(ref pendingReplenishmentRole, "pendingReplenishmentRole", SatelliteRole.Trade);

        childSatelliteIds ??= new List<int>();
        orbit ??= new OrbitalParameters();
    }
}

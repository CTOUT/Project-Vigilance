using System.Collections.Generic;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Orbital;

/// <summary>
/// Persistent orbital operational centre and logistics hub for orbital factions (e.g. TradersGuild).
/// Manages satellite constellations, maintenance cycles, and automated satellite replenishment.
/// </summary>
public class WorldObject_OrbitalStation : WorldObject, IOrbitalEntity
{
    private OrbitalParameters orbit = new(
        altitudeKm: 35786f,
        periodTicks: 60000,
        inclinationDeg: 12f,
        ascendingNodeDeg: 45f,
        precessionRateDegPerDay: -0.2f,
        meanAnomalyEpochDeg: 90f,
        orbitalDriftDegPerDay: 0.05f,
        epochTick: 0);

    private string stationCallsign = "Orbital Hub";
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

    private Material? cachedFactionMat;
    private Faction? cachedFaction;

    public override bool VisibleInBackground => true;

    public override Color ExpandingIconColor => Faction != null ? Faction.Color : Color.white;

    public override Material Material
    {
        get
        {
            if (cachedFactionMat == null || cachedFaction != Faction)
            {
                cachedFaction = Faction;
                Color color = Faction != null ? Faction.Color : Color.white;
                if (def?.texture != null)
                {
                    cachedFactionMat = MaterialPool.MatFrom(
                        def.texture,
                        ShaderDatabase.WorldOverlayTransparentLit,
                        color,
                        WorldMaterials.WorldObjectRenderQueue);
                }
            }
            return cachedFactionMat ?? base.Material;
        }
    }

    public override void SetFaction(Faction newFaction)
    {
        base.SetFaction(newFaction);
        cachedFactionMat = null;
        cachedFaction = null;
    }

    public PlanetTile CurrentSubSatelliteTile => OrbitalFlightModel.CalculateSubSatelliteTile(orbit, Current.Game?.tickManager?.TicksGame ?? 0);

    public Vector3 CurrentOrbitalPosition => OrbitalFlightModel.CalculateOrbitalPosition(
        orbit,
        Current.Game?.tickManager?.TicksGame ?? 0,
        altitudeRenderOffset: 25f);

    public override Vector3 DrawPos => CurrentOrbitalPosition;

    public override void SpawnSetup()
    {
        base.SpawnSetup();
        if (string.IsNullOrEmpty(stationCallsign) || stationCallsign == "Orbital Hub")
        {
            stationCallsign = LabelCap.NullOrEmpty() ? "Station-" + ID : LabelCap;
        }

        // Align tile to current sub-station coordinates
        Tile = CurrentSubSatelliteTile;
        OrbitalManager.Instance?.RegisterStation(this);
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
        PlanetTile newTile = OrbitalFlightModel.CalculateSubSatelliteTile(orbit, currentTick);
        if (newTile.Valid && newTile != Tile)
        {
            SetTileSafe(newTile);
        }
    }

    private void SetTileSafe(PlanetTile newTile)
    {
        if (Find.World?.worldObjects != null)
        {
            Tile = newTile;
        }
        else
        {
            HarmonyLib.Traverse.Create(this).Field("tile").SetValue(newTile);
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
            if (Find.World.worldObjects.AllWorldObjects.Find(wo => wo.ID == satId) is not WorldObject_Satellite sat)
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
        sb.Append("Orbital Altitude: ").Append(orbit.AltitudeKm.ToString("F0")).AppendLine(" km");
        sb.Append("Service Horizon: ").Append(serviceAreaAngularRadiusDeg.ToString("F0")).AppendLine("° angular sector");

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

        sb.Append("Satellite Constellation: ").Append(activeChildren).Append(" active (")
          .Append(inZoneCount).Append(" in service corridor)");

        if (replenishmentCountdownTicks > 0)
        {
            float daysRemaining = (float)replenishmentCountdownTicks / OrbitalFlightModel.TicksPerRimWorldDay;
            sb.AppendLine();
            sb.Append("Fabricating Replacement: ").Append(pendingReplenishmentRole).Append(" (")
              .Append(daysRemaining.ToString("F1")).Append(" days)");
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
        Scribe_Values.Look(ref stationCallsign, "stationCallsign", "Orbital Hub");
        Scribe_Values.Look(ref serviceAreaAngularRadiusDeg, "serviceAreaAngularRadiusDeg", 35f);
        Scribe_Collections.Look(ref childSatelliteIds, "childSatelliteIds", LookMode.Value);
        Scribe_Values.Look(ref replenishmentCountdownTicks, "replenishmentCountdownTicks", -1);
        Scribe_Values.Look(ref pendingReplenishmentRole, "pendingReplenishmentRole", SatelliteRole.Trade);

        childSatelliteIds ??= new List<int>();
        orbit ??= new OrbitalParameters();
    }
}

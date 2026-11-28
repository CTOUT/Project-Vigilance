using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using ProjectVigilance.Geoscape.World;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// WorldComponent managing planetary early-warning detection sweeps, sensory coverage, and fog of war.
/// Evaluates contacts at a realistic pulse interval (TickRare: 250 ticks) with zero allocation.
/// </summary>
public partial class SensoryManager : WorldComponent
{
    public const int SweepIntervalTicks = 250; // ~4.16 seconds (realistic radar rotation / visual sweep rate)

    /// <summary>
    /// Invoked whenever a previously undetected contact enters sensor coverage.
    /// </summary>
    public static event Action<ISensorProvider, IDetectableTarget, IntelligenceTier>? OnContactDetected;

    /// <summary>
    /// Invoked whenever an existing contact's intelligence tier is classified or upgraded.
    /// </summary>
    public static event Action<IDetectableTarget, IntelligenceTier>? OnContactClassified;

    /// <summary>
    /// Invoked whenever a tracked contact moves outside all sensor envelopes and becomes undetected.
    /// </summary>
    public static event Action<IDetectableTarget>? OnContactLost;

    /// <summary>
    /// Registers a third-party or dynamic sensor provider into the planetary sensory grid.
    /// </summary>
    public static void RegisterDynamicSensor(ISensorProvider sensor) => Instance?.RegisterSensor(sensor);

    /// <summary>
    /// Unregisters a third-party or dynamic sensor provider from the planetary sensory grid.
    /// </summary>
    public static void UnregisterDynamicSensor(ISensorProvider sensor) => Instance?.DeregisterSensor(sensor);

    /// <summary>
    /// Registers a third-party or dynamic detectable target for planetary tracking sweeps.
    /// </summary>
    public static void RegisterDynamicTarget(IDetectableTarget target) => Instance?.RegisterTarget(target);

    /// <summary>
    /// Unregisters a third-party or dynamic detectable target from planetary tracking sweeps.
    /// </summary>
    public static void UnregisterDynamicTarget(IDetectableTarget target) => Instance?.DeregisterTarget(target);

    private static readonly Predicate<IDetectableTarget> StaleTargetPredicate = static t => t is WorldObject wo && (wo.Destroyed || (Find.World != null && !wo.Spawned));

    /// <summary>
    /// Optional override for world distance calculation in test / benchmark scenarios where WorldGrid is uninitialized.
    /// </summary>
    public static Func<PlanetTile, PlanetTile, int, int>? DistanceCalculationOverride
    {
        get => ProjectVigilance.Core.Modules.VigilanceModuleManager.DistanceCalculationOverrideHook;
        set => ProjectVigilance.Core.Modules.VigilanceModuleManager.DistanceCalculationOverrideHook = value;
    }

    [ThreadStatic]
    private static Func<PlanetTile, bool>? isExploredOverride;

    /// <summary>
    /// Optional override for tile exploration status in test / benchmark scenarios where WorldGrid is uninitialized.
    /// </summary>
    public static Func<PlanetTile, bool>? IsExploredOverride
    {
        get => isExploredOverride;
        set => isExploredOverride = value;
    }

    [ThreadStatic]
    private static Func<PlanetTile, bool>? isIdentifiedOverride;

    /// <summary>
    /// Optional override for tile identification status in test / benchmark scenarios where WorldGrid is uninitialized.
    /// </summary>
    public static Func<PlanetTile, bool>? IsIdentifiedOverride
    {
        get => isIdentifiedOverride;
        set => isIdentifiedOverride = value;
    }

    [ThreadStatic]
    private static bool suppressUnknownMasking;

    /// <summary>
    /// Temporarily suppresses unknown-site label/description masking when internal systems
    /// (such as discovery letters or logging) need the underlying WorldObject's true label.
    /// </summary>
    public static bool SuppressUnknownMasking
    {
        get => suppressUnknownMasking;
        set => suppressUnknownMasking = value;
    }

    private const int MaxVisibilityCacheEntries = 4096;

    private readonly struct CachedMaskEntry
    {
        public readonly int TileId;
        public readonly int ObjectId;
        public readonly bool Result;

        public CachedMaskEntry(int tileId, int objectId, bool result)
        {
            TileId = tileId;
            ObjectId = objectId;
            Result = result;
        }
    }

    private readonly List<ISensorProvider> customSensors = new(64);
    private readonly Dictionary<int, ScoutHorizonSensor> colonySensors = new(32);
    private readonly Dictionary<int, ScoutHorizonSensor> caravanSensors = new(32);
    private readonly HashSet<IDetectableTarget> activeTargets = new(128);
    private readonly Dictionary<(int tileId, bool isSonar), int> stationaryExploredRadius = new(64);
    private readonly Dictionary<(int tileId, bool isSonar), int> stationaryIdentifiedRadius = new(64);
    private readonly Dictionary<int, PlanetTile> lastCaravanTiles = new(32);
    private readonly Dictionary<ISensorProvider, (PlanetTile tile, float radius, int idRadius, int sigintPenRadius, bool penetratesMasking)> lastActiveSensorStates = new(128);
    private readonly List<ISensorProvider> activeSensorsCache = new(128);
    private readonly HashSet<int> currentActiveVisionTileIds = new(256);
    private readonly HashSet<int> currentActiveIdentifiedTileIds = new(256);
    private readonly Dictionary<int, SensorDomainFlags> currentActiveTileDomains = new(256);
    private readonly HashSet<int> stationaryVisionTileIds = new(256);
    private readonly HashSet<int> stationaryIdentifiedTileIds = new(128);
    private readonly Dictionary<int, SensorDomainFlags> stationaryTileDomains = new(256);
    private readonly HashSet<int> tmpActiveVisionTileIds = new(256);
    private readonly HashSet<int> tmpActiveIdentifiedTileIds = new(256);
    private readonly Dictionary<int, SensorDomainFlags> tmpActiveTileDomains = new(256);
    private readonly HashSet<int> tmpSonarCoverageTileIds = new(128);
    private readonly HashSet<int> tmpShadowCandidateTileIds = new(64);
    private readonly HashSet<int> tmpPenetratedMaskedTileIds = new(64);
    private readonly HashSet<int> newActiveVisionTileIds = new(256);
    private readonly List<PlanetTile> tmpNeighbors = new(6);
    private readonly Queue<(PlanetTile tile, int step)> bfsQueue = new(128);
    private readonly HashSet<int> bfsVisitedTileIds = new(128);
    private readonly Dictionary<int, List<int>> unidentifiedObjectIdsByTile = new();
    private readonly Dictionary<int, int> unidentifiedObjectTileById = new();
    private readonly List<List<int>> pooledIdLists = new();
    private readonly HashSet<int> tmpNewlyIdentifiedTileIds = new();
    private HashSet<int> unidentifiedObjectIds = new();
    private bool unidentifiedTileIndexDirty;
    private bool stationaryCacheValid;

    private static readonly Dictionary<WorldObject, CachedMaskEntry> shouldMaskCache = new(ReferenceEqualityComparer.Instance);
    private static int shouldMaskCacheRevision = -1;
    private static VisibilityMode shouldMaskCacheMode = (VisibilityMode)(-1);
    private static SensoryManager? shouldMaskCacheInstance;

    internal static void ClearStaticCaches()
    {
        shouldMaskCache.Clear();
        shouldMaskCacheRevision = -1;
        shouldMaskCacheMode = (VisibilityMode)(-1);
        shouldMaskCacheInstance = null;
    }

    internal void InvalidateStationaryVisionCache() => stationaryCacheValid = false;

    internal void MarkUnidentifiedTileIndexDirty() => unidentifiedTileIndexDirty = true;

    private byte[]? exploredBytes;
    private byte[]? identifiedBytes;
    private int nextContactTrackingId = 1;
    private bool hasCompletedInitialTickRefresh;

    /// <summary>
    /// Monotonically incremented whenever tile exploration, identification, active vision,
    /// or target intelligence state changes, allowing open UI search caches to invalidate cleanly.
    /// </summary>
    public int SweepRevision { get; private set; }

    [ThreadStatic]
    private static SensoryManager? testInstance;
    private static SensoryManager? cachedInstance;
    public static SensoryManager? Instance
    {
        get
        {
            if (testInstance != null) return testInstance;
            RimWorld.Planet.World? currentWorld = Find.World;
            if (currentWorld == null) return null;
            if (cachedInstance == null || cachedInstance.world != currentWorld)
            {
                cachedInstance = currentWorld.GetComponent<SensoryManager>();
            }
            return cachedInstance;
        }
        set => testInstance = value;
    }

    /// <summary>
    /// Scans the explored tiles bitset for any recorded exploration data.
    /// Used during legacy migration to detect whether an unversioned save was created under Classic Haze of War.
    /// </summary>
    public bool HasAnyExploredTiles
    {
        get
        {
            if (exploredBytes == null)
            {
                return false;
            }

            for (int i = 0; i < exploredBytes.Length; i++)
            {
                if (exploredBytes[i] != 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public SensoryManager(RimWorld.Planet.World world) : base(world)
    {
        ExternalFogCompatibility.ResetAndRescan();
    }

    public override void FinalizeInit(bool fromLoad)
    {
        base.FinalizeInit(fromLoad);
        ClearStaticCaches();
        CaravanExpeditionUtility.ClearAllPendingDirectives();
        Patches.WorldObject_Visibility_Patch.ClearVisibilityCaches();
        ExternalFogCompatibility.ResetAndRescan();
        hasCompletedInitialTickRefresh = false;

        // Eagerly initialize explored bitset and execute sensory sweep
        // so player colonies and caravans are revealed before the globe renders.
        featureCenterTileCache.Clear();
        featureExploredCache.Clear();
        featureExploredCacheRevision = -1;
        featureExploredCacheMode = (VisibilityMode)(-1);
        featureCenterTilesIndexed = false;
        featureCenterTilesSet.Clear();
        stationaryCacheValid = false;
        if (fromLoad)
        {
            unidentifiedTileIndexDirty = true;
        }
        else
        {
            ClearUnidentifiedTileIndex();
            unidentifiedTileIndexDirty = false;
        }
        EnsureExploredArray();
        EnsureFeatureCenterTilesIndexed();
        PerformSensorySweep();

        if (Find.World?.grid?.Surface != null)
        {
            Patches.PlanetLayer_InitializeLayer_Patch.EnsureLayersAttached(Find.World.grid.Surface);
            WorldDrawLayer_HazeOfWar.InvalidateHierarchySync();
            WorldDrawLayer_HazeOfWar.RequestLayerRefresh(WorldDrawLayer_ShroudShell.Instance);
            WorldDrawLayer_HazeOfWar.RequestLayerRefresh(WorldDrawLayer_HazeOfWar.Instance);
            WorldDrawLayer_HazeOfWar.RequestLayerRefresh(WorldDrawLayer_RadarCoverage.Instance);
        }
    }

    public override void WorldComponentTick()
    {
        base.WorldComponentTick();

        if (!hasCompletedInitialTickRefresh && Find.TickManager.TicksGame <= 1)
        {
            hasCompletedInitialTickRefresh = true;
            RefreshImmediateVision();
        }

        // Responsive movement check: if an active player caravan steps onto a new hex,
        // instantly reveal fog and update active vision without waiting for the 250-tick sweep
        if (GeoscapeRules.VisibilityMode == VisibilityMode.FogOfWar && Find.TickManager.TicksGame % 5 == 0)
        {
            CheckPlayerCaravanMovement();
        }

        if (Find.TickManager.TicksGame % SweepIntervalTicks == 0)
        {
            PerformSensorySweep();
        }
    }

    private void CheckPlayerCaravanMovement()
    {
        var caravans = Find.World?.worldObjects?.Caravans;
        if (caravans == null || caravans.Count == 0) return;

        bool anyMoved = false;
        for (int i = 0; i < caravans.Count; i++)
        {
            Caravan c = caravans[i];
            if (c != null && c.Spawned && c.Faction?.IsPlayer == true)
            {
                int id = c.ID;
                if (!lastCaravanTiles.TryGetValue(id, out PlanetTile lastTile) || lastTile != c.Tile)
                {
                    lastCaravanTiles[id] = c.Tile;
                    if (!caravanSensors.TryGetValue(id, out var sensor))
                    {
                        sensor = new ScoutHorizonSensor(c, isColony: false);
                        caravanSensors[id] = sensor;
                    }

                    float radius = GeoscapeRules.CaravanScoutRadius;
                    int idRadius = 1 + SensoryTerrainUtility.GetIdentificationVantageBonus(SensoryTerrainUtility.GetVantageBonus(c.Tile));
                    if (sensor.IsSensorActive)
                    {
                        radius = sensor.DetectionRadiusInTiles;
                        idRadius = sensor.IdentificationRadiusInTiles;
                    }
                    EnsureExploredArray(c.Tile.tileId + 1);
                    MarkSurroundingTilesExplored(c.Tile, radius);
                    MarkSurroundingTilesIdentified(c.Tile, idRadius);
                    anyMoved = true;
                    OptimisticPathingUtility.NotifyCaravanVisionUpdated(c);
                }
            }
        }

        if (anyMoved)
        {
            UpdateHazeActiveVision(mobileOnly: true);
            WorldDrawLayer_RadarCoverage.Instance?.SetDirty();
        }
    }

    public void RegisterTarget(IDetectableTarget target)
    {
        if (target != null)
        {
            activeTargets.Add(target);
        }
    }

    public void DeregisterTarget(IDetectableTarget target)
    {
        if (target != null)
        {
            activeTargets.Remove(target);
        }
    }

    public HashSet<IDetectableTarget> ActiveTargets => activeTargets;

    public int NextContactTrackingId => nextContactTrackingId;

    /// <summary>
    /// Allocates a monotonic sequential tracking ID (e.g. Contact-1, Contact-2)
    /// representing an X-COM style persistent target designation that remains for the incursion's lifetime.
    /// Delegated to ContactRegistry for central indexing.
    /// </summary>
    public int AllocateContactTrackingId()
    {
        if (ContactRegistry.Instance != null)
        {
            return ContactRegistry.Instance.AllocateTrackingId();
        }
        return nextContactTrackingId++;
    }

    public void RegisterSensor(ISensorProvider sensor)
    {
        if (sensor != null && !customSensors.Contains(sensor))
        {
            // Security & gameplay isolation: only player-owned sensors or unowned player utilities may join SensoryManager
            if (sensor.Source != null && sensor.Source.Faction != null && !sensor.Source.Faction.IsPlayer)
            {
                return;
            }

            customSensors.Add(sensor);
            InvalidateStationaryVisionCache();
            WorldDrawLayer_RadarCoverage.Instance?.SetDirty();
        }
    }

    public void DeregisterSensor(ISensorProvider sensor)
    {
        if (sensor != null && customSensors.Remove(sensor))
        {
            lastActiveSensorStates.Remove(sensor);
            InvalidateStationaryVisionCache();
            WorldDrawLayer_RadarCoverage.Instance?.SetDirty();
        }
    }

    internal float GetMaxGroundRadarRangeForMap(Map map)
    {
        if (map == null)
        {
            return 0f;
        }

        float maxRange = 0f;
        for (int i = 0; i < customSensors.Count; i++)
        {
            if (customSensors[i] is CompRadar radar &&
                radar.parent != null &&
                !radar.parent.Destroyed &&
                radar.parent.Map == map &&
                radar.IsSensorActive &&
                radar.IsTechnological &&
                !radar.IsOceanicSonar)
            {
                float range = radar.DetectionRadiusInTiles;
                if (range > maxRange)
                {
                    maxRange = range;
                }
            }
        }

        return maxRange;
    }

    public IReadOnlyList<ISensorProvider> GetActiveSensors()
    {
        activeSensorsCache.Clear();

        // 1. Natural Colony & Camp Scout Horizons (Settlements, Camps, and Outposts with active maps)
        if (Find.Maps != null)
        {
            var maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                Map map = maps[i];
                Faction? playerFaction = (Find.World != null && Find.FactionManager != null) ? Faction.OfPlayerSilentFail : null;
                if (map.Parent != null && map.Parent.Tile.Valid &&
                    (map.IsPlayerHome || (playerFaction != null && map.Parent.Faction == playerFaction) || (map.mapPawns != null && map.mapPawns.AnyFreeColonistSpawned)))
                {
                    int parentId = map.Parent.ID;
                    if (!colonySensors.TryGetValue(parentId, out var sensor))
                    {
                        sensor = new ScoutHorizonSensor(map.Parent, isColony: map.IsPlayerHome);
                        colonySensors[parentId] = sensor;
                    }
                    if (sensor.IsSensorActive)
                    {
                        activeSensorsCache.Add(sensor);
                    }
                }
            }
        }

        // 2. Natural Caravan Lookouts (Player caravans)
        var caravans = Find.World?.worldObjects?.Caravans;
        if (caravans != null)
        {
            for (int i = 0; i < caravans.Count; i++)
            {
                Caravan caravan = caravans[i];
                if (caravan.Faction?.IsPlayer == true && caravan.Tile.Valid && caravan.Spawned && !caravan.Destroyed)
                {
                    int caravanId = caravan.ID;
                    if (!caravanSensors.TryGetValue(caravanId, out var sensor))
                    {
                        sensor = new ScoutHorizonSensor(caravan, isColony: false);
                        caravanSensors[caravanId] = sensor;
                    }
                    if (sensor.IsSensorActive)
                    {
                        activeSensorsCache.Add(sensor);
                    }
                }
            }
        }

        // 3. Registered Custom Sensors (Radar installations, listening posts, etc.)
        if (Find.World?.worldObjects?.AllWorldObjects != null)
        {
            var all = Find.World.worldObjects.AllWorldObjects;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] is WorldObject_ListeningPost post && (Find.World == null || post.Spawned) && !post.Destroyed && !customSensors.Contains(post))
                {
                    if (post.Faction == null || post.Faction.IsPlayer)
                    {
                        customSensors.Add(post);
                    }
                }
            }
        }

        for (int i = 0; i < customSensors.Count; i++)
        {
            var s = customSensors[i];
            if (s.IsSensorActive && (s.Source == null || s.Source.Faction == null || s.Source.Faction.IsPlayer))
            {
                activeSensorsCache.Add(s);
            }
        }

        return activeSensorsCache;
    }

}

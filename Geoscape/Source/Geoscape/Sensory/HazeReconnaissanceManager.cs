using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using ProjectVigilance.Geoscape.World;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Serializable record tracking a dynamic world site concealed in the unmonitored Haze of War.
/// Holds the site ID, tile, and deferred announcement letter until uncovered.
/// </summary>
public class ConcealedSiteRecord : IExposable
{
    public int worldObjectId;
    public PlanetTile tile;
    public string siteLabel = string.Empty;
    public Letter? suppressedLetter;
    public int concealedAtTick;

    public ConcealedSiteRecord() { }

    public ConcealedSiteRecord(WorldObject wo, Letter? letter = null)
    {
        worldObjectId = wo.ID;
        tile = wo.Tile;
        siteLabel = HazeReconnaissanceManager.GetSafeSiteLabel(wo);
        suppressedLetter = letter;
        concealedAtTick = (Current.Game != null && Current.ProgramState == ProgramState.Playing)
            ? (Find.TickManager?.TicksGame ?? 0)
            : 0;
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref worldObjectId, "worldObjectId");
        Scribe_Values.Look(ref tile, "tile");
        Scribe_Values.Look(ref siteLabel, "siteLabel", string.Empty);
        Scribe_Deep.Look(ref suppressedLetter, "suppressedLetter");
        Scribe_Values.Look(ref concealedAtTick, "concealedAtTick");
    }
}

/// <summary>
/// WorldComponent managing dynamic camp Haze concealment, suppressed global announcement letters,
/// and quest timeout suspension.
/// Dynamic world sites spawned in unmonitored Haze tiles have their announcements suppressed
/// and their WorldObjectComp_Timeout clocks paused until uncovered by radar sweeps, caravans, or rumours.
/// </summary>
public class HazeReconnaissanceManager : WorldComponent
{
    private Dictionary<int, ConcealedSiteRecord> concealedSites = new();
    private readonly List<WorldObject> tmpUncoveredObjects = new();
    private readonly List<int> tmpStaleIds = new();
    private bool isDeliveringSuppressedLetter;

    [ThreadStatic]
    private static HazeReconnaissanceManager? testInstance;
    public static HazeReconnaissanceManager? Instance
    {
        get => testInstance ?? Find.World?.GetComponent<HazeReconnaissanceManager>();
        set => testInstance = value;
    }

    [ThreadStatic]
    private static Func<int, WorldObject?>? worldObjectLookupOverride;
    public static Func<int, WorldObject?>? WorldObjectLookupOverride
    {
        get => worldObjectLookupOverride;
        set => worldObjectLookupOverride = value;
    }

    public IReadOnlyDictionary<int, ConcealedSiteRecord> ConcealedSites => concealedSites;

    public HazeReconnaissanceManager(RimWorld.Planet.World world) : base(world)
    {
    }

    public bool IsSiteConcealed(WorldObject? wo)
    {
        if (wo == null) return false;
        return concealedSites.ContainsKey(wo.ID);
    }

    public bool IsTileConcealed(PlanetTile tile)
    {
        if (!tile.Valid) return false;
        return concealedSites.Values.Any(r => r.tile == tile);
    }

    public static string GetSafeSiteLabel(WorldObject? wo)
    {
        if (wo == null) return "Site";
        bool prevSuppress = SensoryManager.SuppressUnknownMasking;
        SensoryManager.SuppressUnknownMasking = true;
        try
        {
            return wo.LabelCap ?? wo.def?.label ?? "Site";
        }
        catch
        {
            return wo.def?.label ?? "Site";
        }
        finally
        {
            SensoryManager.SuppressUnknownMasking = prevSuppress;
        }
    }

    public static bool IsDynamicSite(WorldObject wo)
    {
        if (wo == null) return false;
        if (wo is Settlement || wo is WorldObject_ListeningPost || wo is WorldObject_Incursion || wo is Caravan)
        {
            return false;
        }
        if (wo is Site || wo is WorldObject_EncounterAftermath) return true;
        if (wo.GetComponent<TimeoutComp>() != null) return true;
        if (wo.def != null && (wo.def.defName.Contains("Site") ||
                               wo.def.defName.Contains("Camp") ||
                               wo.def.defName.Contains("Outpost") ||
                               wo.def.defName.Contains("WorkSite") ||
                               wo.def.defName.Contains("Aftermath")))
        {
            return true;
        }
        return false;
    }

    public void Notify_WorldObjectSpawned(WorldObject wo)
    {
        if (Current.ProgramState != ProgramState.Playing || wo == null || !wo.Spawned || !wo.Tile.Valid || wo.Faction?.IsPlayer == true)
        {
            return;
        }

        if (wo is WorldObject_Incursion || wo is WorldObject_ListeningPost || wo is Caravan)
        {
            return;
        }

        if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.ClassicHazeOfWar)
        {
            return;
        }

        if (SensoryManager.Instance != null && !SensoryManager.Instance.HasActiveIdentification(wo.Tile))
        {
            SensoryManager.Instance.MarkWorldObjectUnidentified(wo);
            if (IsDynamicSite(wo))
            {
                RegisterConcealedSite(wo);
            }
        }
    }

    public void RegisterConcealedSite(WorldObject wo, Letter? letter = null)
    {
        if (wo == null) return;

        if (!concealedSites.ContainsKey(wo.ID))
        {
            concealedSites[wo.ID] = new ConcealedSiteRecord(wo, letter);
            Log.Message($"[Project Vigilance] Concealed dynamic site '{GetSafeSiteLabel(wo)}' at tile {wo.Tile} in unmonitored Haze. Suspending timeout clock.");
        }
        else if (letter != null && concealedSites[wo.ID].suppressedLetter == null)
        {
            concealedSites[wo.ID].suppressedLetter = letter;
        }
    }

    public bool TrySuppressLetter(Letter letter)
    {
        if (isDeliveringSuppressedLetter)
        {
            return false;
        }

        if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.ClassicHazeOfWar)
        {
            return false;
        }

        SensoryManager? sensory = SensoryManager.Instance;
        if (sensory == null)
        {
            return false;
        }

        // 1. Direct LookTargets inspection
        if (letter.lookTargets != null && letter.lookTargets.targets != null)
        {
            for (int i = 0; i < letter.lookTargets.targets.Count; i++)
            {
                GlobalTargetInfo target = letter.lookTargets.targets[i];
                if (target.WorldObject != null)
                {
                    WorldObject wo = target.WorldObject;
                    if (wo.Faction?.IsPlayer == true || wo is WorldObject_Incursion)
                    {
                        continue;
                    }

                    if (concealedSites.TryGetValue(wo.ID, out var rec))
                    {
                        if (rec.suppressedLetter == null)
                        {
                            rec.suppressedLetter = letter;
                        }
                        Log.Message($"[Project Vigilance] Suppressed announcement letter '{letter.Label}' for concealed site '{GetSafeSiteLabel(wo)}'.");
                        return true;
                    }

                    if (!sensory.HasActiveIdentification(wo.Tile) && IsDynamicSite(wo))
                    {
                        sensory.MarkWorldObjectUnidentified(wo);
                        RegisterConcealedSite(wo, letter);
                        Log.Message($"[Project Vigilance] Suppressed announcement letter '{letter.Label}' and concealed dynamic site '{GetSafeSiteLabel(wo)}'.");
                        return true;
                    }
                }
                else if (target.Tile.Valid && !sensory.HasActiveIdentification(target.Tile))
                {
                    WorldObject? siteAtTile = Find.WorldObjects?.AllWorldObjects.FirstOrDefault(o => o.Tile == target.Tile && IsDynamicSite(o) && o.Faction?.IsPlayer != true);
                    if (siteAtTile != null)
                    {
                        sensory.MarkWorldObjectUnidentified(siteAtTile);
                        RegisterConcealedSite(siteAtTile, letter);
                        Log.Message($"[Project Vigilance] Suppressed announcement letter '{letter.Label}' for site at unidentified tile {target.Tile}.");
                        return true;
                    }
                }
            }
        }

        // 2. Quest LookTargets inspection
        if (letter is ChoiceLetter choiceLetter && choiceLetter.quest != null)
        {
            Quest quest = choiceLetter.quest;
            if (quest.QuestLookTargets != null)
            {
                foreach (GlobalTargetInfo target in quest.QuestLookTargets)
                {
                    if (target.WorldObject != null)
                    {
                        WorldObject wo = target.WorldObject;
                        if (wo.Faction?.IsPlayer != true && !sensory.HasActiveIdentification(wo.Tile) && IsDynamicSite(wo))
                        {
                            sensory.MarkWorldObjectUnidentified(wo);
                            RegisterConcealedSite(wo, letter);
                            Log.Message($"[Project Vigilance] Suppressed quest announcement letter '{letter.Label}' for quest target '{GetSafeSiteLabel(wo)}'.");
                            return true;
                        }
                    }
                    else if (target.Tile.Valid && !sensory.HasActiveIdentification(target.Tile))
                    {
                        WorldObject? siteAtTile = Find.WorldObjects?.AllWorldObjects.FirstOrDefault(o => o.Tile == target.Tile && IsDynamicSite(o) && o.Faction?.IsPlayer != true);
                        if (siteAtTile != null)
                        {
                            sensory.MarkWorldObjectUnidentified(siteAtTile);
                            RegisterConcealedSite(siteAtTile, letter);
                            Log.Message($"[Project Vigilance] Suppressed quest announcement letter '{letter.Label}' for site at unidentified tile {target.Tile}.");
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    public void Notify_WorldObjectRemoved(WorldObject wo)
    {
        if (wo == null) return;
        concealedSites.Remove(wo.ID);
    }

    public bool UncoverSite(WorldObject wo, bool suppressDiscoveryLetter = false)
    {
        if (wo == null) return false;

        SensoryManager.Instance?.MarkWorldObjectIdentified(wo);

        if (concealedSites.TryGetValue(wo.ID, out var record))
        {
            concealedSites.Remove(wo.ID);
            string safeLabel = GetSafeSiteLabel(wo);
            Log.Message($"[Project Vigilance] Uncovered dynamic site '{safeLabel}' at tile {wo.Tile}. Resuming timeout clock and dispatching discovery letter.");

            isDeliveringSuppressedLetter = true;
            try
            {
                if (record.suppressedLetter != null && Find.LetterStack != null)
                {
                    Find.LetterStack.ReceiveLetter(record.suppressedLetter);
                }
                else if (!suppressDiscoveryLetter)
                {
                    DispatchDiscoveryLetter(wo);
                }
            }
            finally
            {
                isDeliveringSuppressedLetter = false;
            }

            return true;
        }

        return false;
    }

    public void DispatchDiscoveryLetter(WorldObject wo)
    {
        if (Current.ProgramState != ProgramState.Playing || Find.LetterStack == null)
        {
            return;
        }

        string safeLabel = GetSafeSiteLabel(wo);
        TaggedString title = $"Site Discovered: {safeLabel}";
        TaggedString body = $"Our reconnaissance has uncovered an active {safeLabel} at hex {wo.Tile.tileId}.\n\nIt is now charted on the planetary map.";
        Find.LetterStack.ReceiveLetter(title, body, LetterDefOf.PositiveEvent, new GlobalTargetInfo(wo));
    }

    public static WorldObject? FindWorldObjectById(int id)
    {
        if (worldObjectLookupOverride != null)
        {
            return worldObjectLookupOverride(id);
        }

        var allObjects = Find.World?.worldObjects?.AllWorldObjects;
        if (allObjects == null) return null;

        for (int i = 0; i < allObjects.Count; i++)
        {
            WorldObject obj = allObjects[i];
            if (obj != null && obj.ID == id)
            {
                return obj;
            }
        }

        return null;
    }

    public void CheckUncoveredSites()
    {
        SensoryManager? sensory = SensoryManager.Instance;
        if (sensory == null || concealedSites.Count == 0)
        {
            return;
        }

        bool hasWorldObjects = Find.World?.worldObjects != null;
        if (worldObjectLookupOverride == null && !hasWorldObjects)
        {
            return;
        }

        tmpUncoveredObjects.Clear();
        tmpStaleIds.Clear();

        foreach (var kvp in concealedSites)
        {
            WorldObject? wo = FindWorldObjectById(kvp.Key);
            bool isSpawned = hasWorldObjects
                ? wo?.Spawned == true
                : (wo != null && (HarmonyLib.Traverse.Create(wo).Field("spawned").GetValue<bool?>() ?? true));

            if (wo == null || wo.Destroyed || !isSpawned)
            {
                tmpStaleIds.Add(kvp.Key);
                continue;
            }

            if (sensory.IsWorldObjectIdentified(wo))
            {
                tmpUncoveredObjects.Add(wo);
            }
        }

        for (int i = 0; i < tmpStaleIds.Count; i++)
        {
            concealedSites.Remove(tmpStaleIds[i]);
        }

        for (int i = 0; i < tmpUncoveredObjects.Count; i++)
        {
            UncoverSite(tmpUncoveredObjects[i]);
        }
    }

    public override void WorldComponentTick()
    {
        base.WorldComponentTick();

        if (Current.Game != null && Find.TickManager != null && Find.TickManager.TicksGame % SensoryManager.SweepIntervalTicks == 0)
        {
            CheckUncoveredSites();
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Collections.Look(ref concealedSites, "concealedSites", LookMode.Value, LookMode.Deep);
        concealedSites ??= new Dictionary<int, ConcealedSiteRecord>();
    }
}

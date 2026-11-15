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
    public int questId = -1;

    public ConcealedSiteRecord() { }

    public ConcealedSiteRecord(WorldObject wo, Letter? letter = null, int questId = -1)
    {
        worldObjectId = wo.ID;
        tile = wo.Tile;
        siteLabel = HazeReconnaissanceManager.GetSafeSiteLabel(wo);
        suppressedLetter = letter;
        this.questId = questId;
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
        Scribe_Values.Look(ref questId, "questId", -1);
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
    private static HazeReconnaissanceManager? cachedInstance;
    public static HazeReconnaissanceManager? Instance
    {
        get
        {
            if (testInstance != null) return testInstance;
            RimWorld.Planet.World? currentWorld = Find.World;
            if (currentWorld == null) return null;
            if (cachedInstance == null || cachedInstance.world != currentWorld)
            {
                cachedInstance = currentWorld.GetComponent<HazeReconnaissanceManager>();
            }
            return cachedInstance;
        }
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
    public bool HasConcealedSitesRecorded => concealedSites.Count > 0;

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

        if (GeoscapeRules.VisibilityMode != VisibilityMode.ClassicHazeOfWar)
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

        Quest? associatedQuest = FindAssociatedQuest(wo, letter);
        int qId = associatedQuest?.id ?? -1;

        if (!concealedSites.ContainsKey(wo.ID))
        {
            concealedSites[wo.ID] = new ConcealedSiteRecord(wo, letter, qId);
            Log.Message($"[Project Vigilance] Concealed dynamic site '{GetSafeSiteLabel(wo)}' at tile {wo.Tile} in unmonitored Haze. Suspending timeout clock.");
        }
        else
        {
            if (letter != null && concealedSites[wo.ID].suppressedLetter == null)
            {
                concealedSites[wo.ID].suppressedLetter = letter;
            }
            if (qId >= 0 && concealedSites[wo.ID].questId < 0)
            {
                concealedSites[wo.ID].questId = qId;
            }
        }

        if (associatedQuest != null)
        {
            associatedQuest.hiddenInUI = true;
            Log.Message($"[Project Vigilance] Concealed quest '{associatedQuest.name}' in Quests UI until site '{GetSafeSiteLabel(wo)}' is uncovered.");
        }
    }

    public bool TrySuppressLetter(Letter letter)
    {
        if (isDeliveringSuppressedLetter)
        {
            return false;
        }

        if (GeoscapeRules.VisibilityMode != VisibilityMode.ClassicHazeOfWar)
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

            if (record.questId >= 0 && Current.ProgramState == ProgramState.Playing && Current.Game?.questManager != null)
            {
                Quest? quest = Find.QuestManager.QuestsListForReading?.FirstOrDefault(q => q.id == record.questId);
                if (quest != null)
                {
                    quest.hiddenInUI = false;
                    Log.Message($"[Project Vigilance] Revealed quest '{quest.name}' in Quests UI for uncovered site '{safeLabel}'.");
                }
            }

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

    /// <summary>
    /// Uncovers all currently concealed sites when escalating out of Classic Haze of War.
    /// Resumes their timeout clocks and dispatches any deferred announcement letters.
    /// </summary>
    public void ReleaseAllConcealedSites()
    {
        if (concealedSites.Count == 0) return;

        List<int> ids = concealedSites.Keys.ToList();
        for (int i = 0; i < ids.Count; i++)
        {
            WorldObject? wo = FindWorldObjectById(ids[i]);
            if (wo != null && !wo.Destroyed)
            {
                UncoverSite(wo);
            }
            else
            {
                concealedSites.Remove(ids[i]);
            }
        }
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

    public static Quest? FindAssociatedQuest(WorldObject wo, Letter? letter = null)
    {
        if (letter is ChoiceLetter choiceLetter && choiceLetter.quest != null)
        {
            return choiceLetter.quest;
        }

        if (Current.ProgramState != ProgramState.Playing || Current.Game?.questManager == null) return null;

        var quests = Find.QuestManager?.QuestsListForReading;
        if (quests == null) return null;

        for (int i = 0; i < quests.Count; i++)
        {
            Quest q = quests[i];
            if (q == null) continue;

            if (q.QuestLookTargets != null)
            {
                foreach (var target in q.QuestLookTargets)
                {
                    if (target.WorldObject == wo || (target.Tile.Valid && target.Tile == wo.Tile))
                    {
                        return q;
                    }
                }
            }

            if (q.PartsListForReading != null)
            {
                for (int j = 0; j < q.PartsListForReading.Count; j++)
                {
                    if (q.PartsListForReading[j] is QuestPart_WorldObjectTimeout woTimeout && woTimeout.worldObject == wo)
                    {
                        return q;
                    }
                }
            }
        }

        return null;
    }

    public WorldObject? GetConcealedSiteForQuest(Quest quest)
    {
        if (quest == null || concealedSites.Count == 0) return null;

        foreach (var rec in concealedSites.Values)
        {
            if (rec.questId == quest.id)
            {
                return FindWorldObjectById(rec.worldObjectId);
            }
        }

        if (quest.QuestLookTargets != null)
        {
            foreach (var target in quest.QuestLookTargets)
            {
                if (target.WorldObject != null && IsSiteConcealed(target.WorldObject))
                {
                    return target.WorldObject;
                }
            }
        }

        if (quest.PartsListForReading != null)
        {
            for (int i = 0; i < quest.PartsListForReading.Count; i++)
            {
                if (quest.PartsListForReading[i] is QuestPart_WorldObjectTimeout woTimeout &&
                    woTimeout.worldObject != null &&
                    IsSiteConcealed(woTimeout.worldObject))
                {
                    return woTimeout.worldObject;
                }
            }
        }

        return null;
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
                : (wo != null && WorldObjectAccessUtility.GetSpawnedRaw(wo));

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

    private bool loadedFromSave;

    /// <summary>
    /// Indicates whether this component was deserialized from save XML.
    /// </summary>
    public bool LoadedFromSave
    {
        get => loadedFromSave;
        internal set => loadedFromSave = value;
    }

    public override void ExposeData()
    {
        base.ExposeData();

        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            loadedFromSave = true;
        }

        Scribe_Collections.Look(ref concealedSites, "concealedSites", LookMode.Value, LookMode.Deep);
        concealedSites ??= new Dictionary<int, ConcealedSiteRecord>();
    }
}

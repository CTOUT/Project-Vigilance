using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Replaces vanilla RimWorld's hardcoded 22:00-06:00 player caravan rest lock with
/// trait-aware (Night Owl) and Schedule-tab-aware (<see cref="Pawn_TimetableTracker"/>) rest evaluation:
/// <list type="bullet">
/// <item><description><b>Nocturnal / Night Owl Squads:</b> Colonists with the <c>NightOwl</c> trait on default or <c>Anything</c> schedules rest during daytime (11:00-18:00) and march through the night (18:00-11:00).</description></item>
/// <item><description><b>Mixed Squads:</b> Follows the majority circadian shift of conscious colonists in the caravan (breaking exact 50/50 ties in favour of standard 22:00-06:00 night rest so the caravan only camps once per 24-hour cycle).</description></item>
/// <item><description><b>Schedule-Tab Override:</b> Explicit <c>Sleep</c> or <c>Work</c>/<c>Joy</c>/<c>Meditate</c> timetable assignments override default circadian windows, allowing players to customize caravan sleep shifts or force-march through rest hours.</description></item>
/// </list>
/// </summary>
[HarmonyPatch(typeof(Caravan), nameof(Caravan.NightResting), MethodType.Getter)]
public static class Caravan_NightResting_Patch
{
    private const int StandardRestStartHour = 22;
    private const int StandardRestEndHour = 6;
    private const int NightOwlRestStartHour = 11;
    private const int NightOwlRestEndHour = 18;
    private const float WellRestedThreshold = 0.90f;

    private static TraitDef? cachedNightOwlTrait;
    private static bool nightOwlLookupDone;

    [HarmonyPostfix]
    public static void Postfix(Caravan __instance, ref bool __result)
    {
        if (__instance == null || !__instance.Spawned || __instance.Faction != Faction.OfPlayer)
        {
            return;
        }

        if (!__instance.Tile.Valid)
        {
            return;
        }

        // Preserve vanilla's "final push" exemption when almost at destination
        if (__instance.pather is { Moving: true } pather &&
            pather.nextTile == pather.Destination &&
            Caravan_PathFollower.IsValidFinalPushDestination(pather.Destination) &&
            Mathf.CeilToInt(pather.nextTileCostLeft / 1f) <= 10000)
        {
            __result = false;
            return;
        }

        int currentHour = GenLocalDate.HourInteger(__instance.Tile);
        if (TryEvaluateCaravanRestAtHour(__instance, currentHour, out bool shouldRest))
        {
            __result = shouldRest;
        }
    }

    public static bool TryEvaluateCaravanRestAtHour(Caravan caravan, int hour, out bool shouldRest)
    {
        shouldRest = false;
        List<Pawn>? pawns = caravan.PawnsListForReading;
        if (pawns == null || pawns.Count == 0)
        {
            return false;
        }

        hour = ((hour % 24) + 24) % 24;

        // Prefer conscious colonists; fall back to non-dead colonists if all are downed
        bool hasConsciousColonist = false;
        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn p = pawns[i];
            if (p != null && p.IsColonist && !p.Dead && !p.Downed)
            {
                hasConsciousColonist = true;
                break;
            }
        }

        int restingCount = 0;
        int activeCount = 0;
        int explicitActiveOverrideCount = 0;
        int evaluatedCount = 0;

        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn p = pawns[i];
            if (p == null || !p.IsColonist || p.Dead)
            {
                continue;
            }

            if (hasConsciousColonist && p.Downed)
            {
                continue;
            }

            evaluatedCount++;
            if (DoesColonistWantCaravanRest(p, hour, out bool isExplicitActiveOverride))
            {
                restingCount++;
            }
            else
            {
                activeCount++;
                if (isExplicitActiveOverride)
                {
                    explicitActiveOverrideCount++;
                }
            }
        }

        if (evaluatedCount == 0)
        {
            return false;
        }

        if (restingCount > activeCount)
        {
            shouldRest = true;
            return true;
        }

        if (activeCount > restingCount)
        {
            shouldRest = false;
            return true;
        }

        // Exact 50/50 tie in a mixed squad:
        // If half the squad is explicitly scheduled to Work/Recreation/Meditate, keep marching;
        // otherwise follow standard 22:00-06:00 night hours so the caravan only camps once per day.
        if (explicitActiveOverrideCount >= restingCount)
        {
            shouldRest = false;
        }
        else
        {
            shouldRest = IsStandardNightRestHour(hour);
        }

        return true;
    }

    /// <summary>
    /// Evaluates whether any conscious colonist in the caravan is currently awake on watch
    /// at the specified local hour, and whether any awake colonist possesses the NightOwl trait.
    /// In a mixed squad, even when the caravan halts for the majority shift's rest window,
    /// the opposite shift remains awake on camp watch.
    /// </summary>
    public static void GetCaravanAwakeWatchState(
        Caravan caravan,
        int hour,
        out bool anyAwakeColonist,
        out bool anyAwakeNightOwl)
    {
        anyAwakeColonist = false;
        anyAwakeNightOwl = false;

        List<Pawn>? pawns = caravan?.PawnsListForReading;
        if (pawns == null || pawns.Count == 0)
        {
            return;
        }

        hour = ((hour % 24) + 24) % 24;

        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn p = pawns[i];
            if (p == null || !p.IsColonist || p.Dead || p.Downed)
            {
                continue;
            }

            bool wantsRest = DoesColonistWantCaravanRest(p, hour, out _);
            if (!wantsRest)
            {
                anyAwakeColonist = true;
                if (HasNightOwlTrait(p))
                {
                    anyAwakeNightOwl = true;
                    return;
                }
            }
        }
    }

    private static bool DoesColonistWantCaravanRest(Pawn pawn, int hour, out bool isExplicitActiveOverride)
    {
        isExplicitActiveOverride = false;
        Pawn_TimetableTracker? timetable = pawn.timetable;
        TimeAssignmentDef assignment = timetable?.GetAssignment(hour) ?? TimeAssignmentDefOf.Anything;

        // Explicit Work, Joy, or Meditate assignment always keeps the pawn active (enables force-march)
        if (assignment == TimeAssignmentDefOf.Work ||
            assignment == TimeAssignmentDefOf.Joy ||
            assignment == TimeAssignmentDefOf.Meditate)
        {
            isExplicitActiveOverride = true;
            return false;
        }

        bool isNightOwl = HasNightOwlTrait(pawn);
        bool hasAnySleep = HasAnySleepAssignment(timetable);
        bool isVanillaDefault = IsVanillaDefaultTimetable(timetable);

        if (isNightOwl)
        {
            // RimWorld initializes all new colonists with Sleep on 22:00-06:00 even if they have NightOwl,
            // and players frequently set Night Owls to all-Anything. In both cases, use the Night Owl
            // daytime rest window (11:00-18:00) and march through the night (18:00-11:00).
            if (isVanillaDefault || !hasAnySleep)
            {
                if (IsNightOwlDayRestHour(hour))
                {
                    return !IsAlreadyFullyRested(pawn);
                }

                return false;
            }

            // Player customized this Night Owl's Sleep schedule explicitly
            return assignment == TimeAssignmentDefOf.Sleep;
        }

        // Non-Night-Owl colonist:
        if (assignment == TimeAssignmentDefOf.Sleep)
        {
            return true;
        }

        // Assignment is Anything:
        // If the colonist has Sleep hours defined elsewhere in their 24h schedule, Anything is active time.
        if (hasAnySleep)
        {
            return false;
        }

        // All-Anything schedule for a standard colonist: rest during standard night hours (22:00-06:00)
        if (IsStandardNightRestHour(hour))
        {
            return !IsAlreadyFullyRested(pawn);
        }

        return false;
    }

    private static bool IsAlreadyFullyRested(Pawn pawn)
    {
        float restPct = pawn.needs?.rest?.CurLevelPercentage ?? 0.5f;
        return restPct > WellRestedThreshold;
    }

    private static bool IsStandardNightRestHour(int hour)
    {
        return hour < StandardRestEndHour || hour >= StandardRestStartHour;
    }

    private static bool IsNightOwlDayRestHour(int hour)
    {
        return hour >= NightOwlRestStartHour && hour < NightOwlRestEndHour;
    }

    public static bool HasNightOwlTrait(Pawn pawn)
    {
        if (pawn.story?.traits == null)
        {
            return false;
        }

        if (!nightOwlLookupDone)
        {
            cachedNightOwlTrait = DefDatabase<TraitDef>.GetNamedSilentFail("NightOwl");
            nightOwlLookupDone = true;
        }

        return cachedNightOwlTrait != null && pawn.story.traits.HasTrait(cachedNightOwlTrait);
    }

    private static bool HasAnySleepAssignment(Pawn_TimetableTracker? timetable)
    {
        if (timetable?.times == null)
        {
            return false;
        }

        int count = Mathf.Min(24, timetable.times.Count);
        for (int h = 0; h < count; h++)
        {
            if (timetable.times[h] == TimeAssignmentDefOf.Sleep)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsVanillaDefaultTimetable(Pawn_TimetableTracker? timetable)
    {
        if (timetable?.times == null || timetable.times.Count < 24)
        {
            return true;
        }

        for (int h = 0; h < 24; h++)
        {
            bool defaultIsSleep = h < StandardRestEndHour || h >= StandardRestStartHour;
            TimeAssignmentDef expected = defaultIsSleep
                ? TimeAssignmentDefOf.Sleep
                : TimeAssignmentDefOf.Anything;

            if (timetable.times[h] != expected)
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Ensures that when a player caravan camps outside vanilla's 22:00-06:00 window
/// (such as a Night Owl squad resting from 11:00-18:00 or a custom schedule shift),
/// the caravan inspection panel and rest estimators compute accurate remaining rest ticks
/// rather than displaying 0 seconds.
/// </summary>
[HarmonyPatch(typeof(CaravanNightRestUtility), nameof(CaravanNightRestUtility.LeftRestTicksAt), typeof(PlanetTile), typeof(long))]
public static class CaravanNightRestUtility_LeftRestTicksAt_Patch
{
    [HarmonyPostfix]
    public static void Postfix(PlanetTile tile, long ticksAbs, ref int __result)
    {
        if (!tile.Valid || Find.WorldObjects == null)
        {
            return;
        }

        List<Caravan>? caravans = Find.WorldObjects.Caravans;
        if (caravans == null || caravans.Count == 0)
        {
            return;
        }

        Caravan? matchingCaravan = null;
        for (int i = 0; i < caravans.Count; i++)
        {
            Caravan c = caravans[i];
            if (c != null && c.Spawned && c.Faction == Faction.OfPlayer && c.Tile == tile)
            {
                matchingCaravan = c;
                break;
            }
        }

        if (matchingCaravan == null)
        {
            return;
        }

        float longitude = Find.WorldGrid.LongLatOf(tile).x;
        float hourFloat = GenDate.HourFloat(ticksAbs, longitude);
        int currentHour = Mathf.FloorToInt(hourFloat);
        currentHour = ((currentHour % 24) + 24) % 24;

        if (!Caravan_NightResting_Patch.TryEvaluateCaravanRestAtHour(matchingCaravan, currentHour, out bool currentlyResting) ||
            !currentlyResting)
        {
            return;
        }

        // Scan forward up to 24 hours to find when this caravan's rest block ends
        int hoursUntilWake = 0;
        for (int offset = 1; offset <= 24; offset++)
        {
            int checkHour = (currentHour + offset) % 24;
            if (!Caravan_NightResting_Patch.TryEvaluateCaravanRestAtHour(matchingCaravan, checkHour, out bool stillResting) ||
                !stillResting)
            {
                hoursUntilWake = offset;
                break;
            }
        }

        if (hoursUntilWake <= 0)
        {
            return;
        }

        float fractionalHourElapsed = Mathf.Clamp01(hourFloat - Mathf.Floor(hourFloat));
        float remainingHours = Mathf.Max(0.05f, hoursUntilWake - fractionalHourElapsed);
        __result = Mathf.CeilToInt(remainingHours * GenDate.TicksPerHour);
    }
}

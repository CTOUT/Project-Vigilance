using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Geoscape.World;

/// <summary>
/// Centralised pawn-lifecycle guards that prevent Geoscape-tracked world pawns from being
/// redressed or prematurely garbage-collected and strip unresolvable pawn references before saving.
/// </summary>
public static class WorldPawnIntegrityUtility
{
    private static readonly FieldInfo? PawnsWithDirectRelationsWithMeField =
        AccessTools.Field(typeof(Pawn_RelationsTracker), "pawnsWithDirectRelationsWithMe");

    private static readonly FieldInfo? RelationshipRecordReferencesField =
        AccessTools.Field(typeof(RelationshipRecord), "references");

    private static WorldPawns? SafeWorldPawns => Find.World?.worldPawns;
    private static WorldObjectsHolder? SafeWorldObjects => Find.World?.worldObjects;
    private static List<Map>? SafeMaps => Find.Maps;

    internal static bool ListContainsPawn(List<Pawn>? pawns, Pawn target)
    {
        if (pawns == null)
        {
            return false;
        }

        for (int i = 0; i < pawns.Count; i++)
        {
            if (pawns[i] == target)
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsDeadSafe(Pawn? pawn) => pawn != null && pawn.health != null && pawn.Dead;

    internal static bool IsSpawnedOrHeldSafe(Pawn pawn)
    {
        if (SafeMaps != null && pawn.SpawnedOrAnyParentSpawned)
        {
            return true;
        }

        return pawn.ParentHolder != null;
    }

    private static bool IsInIncursion(WorldObject_Incursion? inc, Pawn pawn)
    {
        if (inc == null)
        {
            return false;
        }

        return inc.TraderPawn == pawn ||
               ListContainsPawn(inc.CommittedPawns, pawn) ||
               ListContainsPawn(inc.RetreatingPawns, pawn) ||
               ListContainsPawn(inc.KidnappedColonists, pawn);
    }

    /// <summary>
    /// Returns true if <paramref name="pawn"/> is currently owned or tracked by any active Geoscape
    /// incursion, retreat batch, aftermath site, or tactical overland ambush component.
    /// </summary>
    public static bool IsTrackedByAnyGeoscapeSystem(
        Pawn? pawn,
        WorldObject_Incursion? excludingIncursion = null,
        WorldObject_EncounterAftermath? excludingAftermath = null)
    {
        if (pawn == null)
        {
            return false;
        }

        var worldObjectsHolder = SafeWorldObjects;
        if (excludingIncursion == null &&
            excludingAftermath == null &&
            worldObjectsHolder != null &&
            IncursionTrackingHelper.IsTrackedByAnyIncursion(pawn))
        {
            return true;
        }

        var worldObjects = worldObjectsHolder?.AllWorldObjects;
        if (worldObjects != null)
        {
            for (int i = 0; i < worldObjects.Count; i++)
            {
                WorldObject obj = worldObjects[i];
                if (obj is WorldObject_Incursion inc)
                {
                    if (inc != excludingIncursion && inc.Spawned && IsInIncursion(inc, pawn))
                    {
                        return true;
                    }
                }
                else if (obj is WorldObject_EncounterAftermath aftermath)
                {
                    if (aftermath != excludingAftermath && aftermath.Spawned &&
                        ListContainsPawn(aftermath.WoundedSurvivors, pawn))
                    {
                        return true;
                    }
                }
            }
        }

        var executingIncursion = IncursionArrivalManager.CurrentExecutingIncursion;
        if (executingIncursion != null &&
            executingIncursion != excludingIncursion &&
            IsInIncursion(executingIncursion, pawn))
        {
            return true;
        }

        if (IncursionRetreatManager.Instance != null &&
            IncursionRetreatManager.Instance.ContainsTrackedPawn(pawn))
        {
            return true;
        }

        var maps = SafeMaps;
        if (maps != null)
        {
            for (int m = 0; m < maps.Count; m++)
            {
                var comp = maps[m].GetComponent<MapComponent_OverlandAmbush>();
                if (comp != null && comp.ContainsTrackedPawn(pawn))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Registers an unspawned live pawn in <see cref="Find.WorldPawns"/> with
    /// <see cref="PawnDiscardDecideMode.KeepForever"/> and pins it against garbage collection.
    /// </summary>
    public static void EnsurePawnInWorldPawnsIfUnspawned(Pawn? p, bool isCaptiveColonist = false)
    {
        if (p == null || p.Discarded || p.Destroyed || IsDeadSafe(p))
        {
            return;
        }

        if (SafeMaps != null && p.SpawnedOrAnyParentSpawned)
        {
            return;
        }

        if (p.ParentHolder != null && p.ParentHolder is not Caravan)
        {
            return;
        }

        var worldPawns = SafeWorldPawns;
        if (worldPawns != null)
        {
            if (!worldPawns.Contains(p))
            {
                worldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
            }

            worldPawns.ForcefullyKeptPawns?.Add(p);
        }

        if (isCaptiveColonist && p.Faction != Faction.OfPlayer)
        {
            p.SetFaction(Faction.OfPlayer);
        }
    }

    /// <summary>
    /// Returns true if a reference to <paramref name="other"/> cannot resolve cleanly after saving.
    /// </summary>
    public static bool IsDanglingPawnReference(
        Pawn? other,
        WorldPawns? worldPawns = null,
        Func<Pawn, bool>? isInWorldPawnsOverride = null,
        Func<Pawn, bool>? isHeldOrSpawnedOverride = null)
    {
        if (other == null || other.Discarded)
        {
            return true;
        }

        WorldPawns? resolvedWorldPawns = worldPawns ?? (isInWorldPawnsOverride == null ? SafeWorldPawns : null);
        bool inWorldPawns = isInWorldPawnsOverride != null
            ? isInWorldPawnsOverride(other)
            : resolvedWorldPawns != null && resolvedWorldPawns.Contains(other);

        if (inWorldPawns)
        {
            return false;
        }

        bool heldOrSpawned = isHeldOrSpawnedOverride != null
            ? isHeldOrSpawnedOverride(other)
            : IsSpawnedOrHeldSafe(other);

        if (other.Destroyed)
        {
            return !heldOrSpawned;
        }

        // Preserve isolated unspawned test pawns when no WorldPawns instance or override exists.
        if (resolvedWorldPawns == null && isInWorldPawnsOverride == null)
        {
            return false;
        }

        return !heldOrSpawned;
    }

    /// <summary>
    /// Removes null, discarded, optionally dead/destroyed, and unserializable pawn references
    /// from <paramref name="pawns"/> prior to save serialisation.
    /// </summary>
    public static void PruneInvalidPawnReferences(
        List<Pawn>? pawns,
        bool removeDeadOrDestroyed = true,
        WorldPawns? worldPawns = null,
        Func<Pawn, bool>? isInWorldPawnsOverride = null,
        Func<Pawn, bool>? isHeldOrSpawnedOverride = null,
        Func<Pawn, bool>? isDeadOverride = null)
    {
        if (pawns == null)
        {
            return;
        }

        for (int i = pawns.Count - 1; i >= 0; i--)
        {
            Pawn? p = pawns[i];
            if (p == null || p.Discarded)
            {
                pawns.RemoveAt(i);
                continue;
            }

            if (removeDeadOrDestroyed)
            {
                bool isDead = isDeadOverride != null ? isDeadOverride(p) : IsDeadSafe(p);
                if (isDead || p.Destroyed)
                {
                    pawns.RemoveAt(i);
                    continue;
                }
            }

            if (IsDanglingPawnReference(p, worldPawns, isInWorldPawnsOverride, isHeldOrSpawnedOverride))
            {
                pawns.RemoveAt(i);
            }
        }
    }

    private static HashSet<Pawn>? GetPawnsWithDirectRelationsWithMe(Pawn_RelationsTracker? tracker) =>
        tracker != null ? PawnsWithDirectRelationsWithMeField?.GetValue(tracker) as HashSet<Pawn> : null;

    private static bool HasDirectRelation(List<DirectPawnRelation>? relations, PawnRelationDef def, Pawn otherPawn)
    {
        if (relations == null)
        {
            return false;
        }

        for (int i = 0; i < relations.Count; i++)
        {
            DirectPawnRelation? r = relations[i];
            if (r != null && r.def == def && r.otherPawn == otherPawn)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAnyDirectRelationWith(List<DirectPawnRelation>? relations, Pawn target)
    {
        if (relations == null)
        {
            return false;
        }

        for (int i = 0; i < relations.Count; i++)
        {
            if (relations[i]?.otherPawn == target)
            {
                return true;
            }
        }

        return false;
    }

    private static void RemoveDirectRelationsWith(List<DirectPawnRelation>? relations, Pawn target)
    {
        if (relations == null)
        {
            return;
        }

        for (int i = relations.Count - 1; i >= 0; i--)
        {
            if (relations[i]?.otherPawn == target)
            {
                relations.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Strips dangling direct, incoming, and virtual social relations on <paramref name="pawn"/> whose target
    /// pawns are discarded or absent from maps, containers, and <see cref="WorldPawns"/>, keeping reciprocal
    /// trackers consistent so <see cref="Pawn_RelationsTracker.ClearAllRelations"/> cannot dereference missing halves.
    /// </summary>
    public static void SanitizeDanglingRelations(
        Pawn? pawn,
        WorldPawns? worldPawns = null,
        Func<Pawn, bool>? isInWorldPawnsOverride = null,
        Func<Pawn, bool>? isHeldOrSpawnedOverride = null)
    {
        var tracker = pawn?.relations;
        if (tracker == null || pawn == null)
        {
            return;
        }

        var directRelations = tracker.DirectRelations;
        var incomingSet = GetPawnsWithDirectRelationsWithMe(tracker);
        if (directRelations != null)
        {
            for (int r = directRelations.Count - 1; r >= 0; r--)
            {
                DirectPawnRelation? rel = directRelations[r];
                Pawn? other = rel?.otherPawn;
                bool isDanglingOther = IsDanglingPawnReference(other, worldPawns, isInWorldPawnsOverride, isHeldOrSpawnedOverride);
                bool isBrokenReflexive = !isDanglingOther &&
                                         rel != null &&
                                         other != null &&
                                         rel.def != null &&
                                         rel.def.reflexive &&
                                         other.relations?.DirectRelations != null &&
                                         !HasDirectRelation(other.relations.DirectRelations, rel.def, pawn);

                if (rel == null || isDanglingOther || isBrokenReflexive)
                {
                    directRelations.RemoveAt(r);
                    if (other != null)
                    {
                        if (other.relations != null)
                        {
                            if (isDanglingOther)
                            {
                                RemoveDirectRelationsWith(other.relations.DirectRelations, pawn);
                            }

                            if (!HasAnyDirectRelationWith(directRelations, other))
                            {
                                GetPawnsWithDirectRelationsWithMe(other.relations)?.Remove(pawn);
                            }
                        }

                        if (isDanglingOther || !HasAnyDirectRelationWith(other.relations?.DirectRelations, pawn))
                        {
                            incomingSet?.Remove(other);
                        }
                    }
                }
            }
        }

        if (incomingSet != null && incomingSet.Count > 0)
        {
            var staleIncoming = new List<Pawn>();
            bool hasNullIncoming = false;
            foreach (Pawn? other in incomingSet)
            {
                if (other == null)
                {
                    hasNullIncoming = true;
                    continue;
                }

                if (IsDanglingPawnReference(other, worldPawns, isInWorldPawnsOverride, isHeldOrSpawnedOverride) ||
                    !HasAnyDirectRelationWith(other.relations?.DirectRelations, pawn))
                {
                    staleIncoming.Add(other);
                }
            }

            if (hasNullIncoming)
            {
                incomingSet.Remove(null!);
            }

            for (int i = 0; i < staleIncoming.Count; i++)
            {
                Pawn staleOther = staleIncoming[i];
                incomingSet.Remove(staleOther);
                if (staleOther.relations != null)
                {
                    RemoveDirectRelationsWith(staleOther.relations.DirectRelations, pawn);
                    if (!HasAnyDirectRelationWith(directRelations, staleOther))
                    {
                        GetPawnsWithDirectRelationsWithMe(staleOther.relations)?.Remove(pawn);
                    }
                }
            }
        }

        var virtualRelations = tracker.VirtualRelations;
        if (virtualRelations != null)
        {
            for (int v = virtualRelations.Count - 1; v >= 0; v--)
            {
                if (virtualRelations[v]?.record == null)
                {
                    virtualRelations.RemoveAt(v);
                }
            }
        }

        if (tracker.relativeInvolvedInRescueQuest != null &&
            IsDanglingPawnReference(tracker.relativeInvolvedInRescueQuest, worldPawns, isInWorldPawnsOverride, isHeldOrSpawnedOverride))
        {
            tracker.relativeInvolvedInRescueQuest = null;
        }
    }

    /// <summary>
    /// Removes dangling or null pawn references from <see cref="RelationshipRecord"/> entries and prunes
    /// unused records so discarded pawns cannot linger as unresolved XML load references.
    /// </summary>
    public static void SanitizeRelationshipRecords(
        RelationshipRecords? relationshipRecords,
        WorldPawns? worldPawns = null,
        Func<Pawn, bool>? isInWorldPawnsOverride = null,
        Func<Pawn, bool>? isHeldOrSpawnedOverride = null)
    {
        if (relationshipRecords == null)
        {
            return;
        }

        var recordsDict = relationshipRecords.Records;
        if (recordsDict != null)
        {
            var toRemove = new List<Pawn>();
            foreach (RelationshipRecord? record in recordsDict.Values)
            {
                if (record == null)
                {
                    continue;
                }

                var rawRefs = RelationshipRecordReferencesField?.GetValue(record) as HashSet<Pawn>;
                rawRefs?.Remove(null!);

                if (record.References != null && record.References.Count > 0)
                {
                    toRemove.Clear();
                    foreach (Pawn? p in record.References)
                    {
                        if (p != null && IsDanglingPawnReference(p, worldPawns, isInWorldPawnsOverride, isHeldOrSpawnedOverride))
                        {
                            toRemove.Add(p);
                        }
                    }

                    for (int i = 0; i < toRemove.Count; i++)
                    {
                        Pawn danglingPawn = toRemove[i];
                        var vrList = danglingPawn.relations?.VirtualRelations;
                        if (vrList != null)
                        {
                            for (int v = vrList.Count - 1; v >= 0; v--)
                            {
                                if (vrList[v]?.record == record)
                                {
                                    vrList.RemoveAt(v);
                                }
                            }
                        }

                        record.RemoveReference(danglingPawn);
                    }
                }
            }
        }

        relationshipRecords.CleanupUnusedRecords();
    }

    /// <summary>
    /// Synchronises Geoscape-tracked world pawns and scrubs dangling relations across all saved
    /// world and map pawns immediately before <see cref="WorldPawns.ExposeData"/> writes XML or after loading.
    /// </summary>
    public static void PrepareWorldPawnsForSave(WorldPawns? worldPawns)
    {
        if (worldPawns == null)
        {
            return;
        }

        SyncTrackedGeoscapePawnsToWorldPawns();

        var alive = worldPawns.AllPawnsAlive;
        if (alive != null)
        {
            for (int i = 0; i < alive.Count; i++)
            {
                SanitizeDanglingRelations(alive[i], worldPawns);
            }
        }

        var dead = worldPawns.AllPawnsDead;
        if (dead != null)
        {
            foreach (Pawn deadPawn in dead)
            {
                SanitizeDanglingRelations(deadPawn, worldPawns);
            }
        }

        var maps = SafeMaps;
        if (maps != null)
        {
            for (int m = 0; m < maps.Count; m++)
            {
                var mapPawns = maps[m]?.mapPawns?.AllPawns;
                if (mapPawns == null)
                {
                    continue;
                }

                for (int p = 0; p < mapPawns.Count; p++)
                {
                    SanitizeDanglingRelations(mapPawns[p], worldPawns);
                }
            }
        }

        if (Current.Game != null)
        {
            SanitizeRelationshipRecords(Find.RelationshipRecords, worldPawns);
        }
    }

    private static void SyncTrackedGeoscapePawnsToWorldPawns()
    {
        var worldObjects = SafeWorldObjects?.AllWorldObjects;
        if (worldObjects != null)
        {
            for (int i = 0; i < worldObjects.Count; i++)
            {
                WorldObject obj = worldObjects[i];
                if (obj is WorldObject_Incursion inc)
                {
                    EnsureListInWorldPawns(inc.CommittedPawns, isCaptiveColonist: false);
                    EnsureListInWorldPawns(inc.RetreatingPawns, isCaptiveColonist: false);
                    EnsureListInWorldPawns(inc.KidnappedColonists, isCaptiveColonist: true);
                    EnsurePawnInWorldPawnsIfUnspawned(inc.TraderPawn);
                }
                else if (obj is WorldObject_EncounterAftermath aftermath)
                {
                    EnsureListInWorldPawns(aftermath.WoundedSurvivors, isCaptiveColonist: false);
                }
            }
        }

        IncursionRetreatManager.Instance?.ForEachTrackedPawn(static (p, isCaptive) =>
        {
            EnsurePawnInWorldPawnsIfUnspawned(p, isCaptive);
        });

        var maps = SafeMaps;
        if (maps != null)
        {
            for (int m = 0; m < maps.Count; m++)
            {
                maps[m].GetComponent<MapComponent_OverlandAmbush>()?.EnsureDespawnedTrackedPawnsInWorld();
            }
        }
    }

    internal static void EnsureListInWorldPawns(List<Pawn>? pawns, bool isCaptiveColonist)
    {
        if (pawns == null)
        {
            return;
        }

        for (int i = 0; i < pawns.Count; i++)
        {
            EnsurePawnInWorldPawnsIfUnspawned(pawns[i], isCaptiveColonist);
        }
    }
}

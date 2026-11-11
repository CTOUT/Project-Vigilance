using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape.Patches;
using ProjectVigilance.Geoscape.World;
using RimWorld;
using Verse;

namespace Geoscape.Tests;

[TestClass]
public sealed class WorldPawnIntegrityTests
{
    private static readonly FieldInfo MapIndexOrStateField =
        typeof(Thing).GetField("mapIndexOrState", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly FieldInfo ActiveBatchesField =
        typeof(IncursionRetreatManager).GetField("activeBatches", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly FieldInfo TrackedEnemiesField =
        typeof(MapComponent_OverlandAmbush).GetField("trackedEnemies", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static Pawn CreateUnspawnedPawn(sbyte state = -1)
    {
        var pawn = (Pawn)RuntimeHelpers.GetUninitializedObject(typeof(Pawn));
        MapIndexOrStateField.SetValue(pawn, state);
        pawn.relations = new Pawn_RelationsTracker(pawn);
        return pawn;
    }

    [TestMethod]
    public void SanitizeDanglingRelations_RemovesUnresolvableTargetsAndKeepsValidOnes()
    {
        Pawn subject = CreateUnspawnedPawn();
        Pawn discarded = CreateUnspawnedPawn(state: -3);
        Pawn unheldDestroyed = CreateUnspawnedPawn(state: -2);
        Pawn heldDestroyed = CreateUnspawnedPawn(state: -2);
        Pawn orphaned = CreateUnspawnedPawn(state: -1);
        Pawn keptWorldPawn = CreateUnspawnedPawn(state: -1);

        var relations = subject.relations.DirectRelations;
        relations.Add(new DirectPawnRelation(null, null, 0));
        relations.Add(new DirectPawnRelation(null, discarded, 0));
        relations.Add(new DirectPawnRelation(null, unheldDestroyed, 0));
        relations.Add(new DirectPawnRelation(null, heldDestroyed, 0));
        relations.Add(new DirectPawnRelation(null, orphaned, 0));
        relations.Add(new DirectPawnRelation(null, keptWorldPawn, 0));
        subject.relations.relativeInvolvedInRescueQuest = discarded;

        WorldPawnIntegrityUtility.SanitizeDanglingRelations(
            subject,
            isInWorldPawnsOverride: p => p == keptWorldPawn,
            isHeldOrSpawnedOverride: p => p == heldDestroyed);

        Assert.AreEqual(2, subject.relations.DirectRelations.Count, "Only held and WorldPawns-contained targets may remain.");
        Assert.AreSame(heldDestroyed, subject.relations.DirectRelations[0].otherPawn);
        Assert.AreSame(keptWorldPawn, subject.relations.DirectRelations[1].otherPawn);
        Assert.IsNull(subject.relations.relativeInvolvedInRescueQuest, "Dangling quest relative must be cleared.");
    }

    [TestMethod]
    public void PruneInvalidPawnReferences_RespectsDeadDestroyedDiscardedAndOrphanedStates()
    {
        Pawn liveInWorld = CreateUnspawnedPawn(state: -1);
        Pawn deadInWorld = CreateUnspawnedPawn(state: -1);
        Pawn destroyed = CreateUnspawnedPawn(state: -2);
        Pawn discarded = CreateUnspawnedPawn(state: -3);
        Pawn orphaned = CreateUnspawnedPawn(state: -1);
        Pawn heldOnMap = CreateUnspawnedPawn(state: -1);

        var incursionList = new List<Pawn> { null!, liveInWorld, deadInWorld, destroyed, discarded, orphaned, heldOnMap };
        WorldPawnIntegrityUtility.PruneInvalidPawnReferences(
            incursionList,
            removeDeadOrDestroyed: true,
            isInWorldPawnsOverride: p => p == liveInWorld || p == deadInWorld,
            isHeldOrSpawnedOverride: p => p == heldOnMap,
            isDeadOverride: p => p == deadInWorld);

        CollectionAssert.AreEqual(new List<Pawn> { liveInWorld, heldOnMap }, incursionList);

        var ambushList = new List<Pawn> { null!, liveInWorld, deadInWorld, destroyed, discarded, orphaned, heldOnMap };
        WorldPawnIntegrityUtility.PruneInvalidPawnReferences(
            ambushList,
            removeDeadOrDestroyed: false,
            isInWorldPawnsOverride: p => p == liveInWorld || p == deadInWorld,
            isHeldOrSpawnedOverride: p => p == heldOnMap,
            isDeadOverride: p => p == deadInWorld);

        CollectionAssert.AreEqual(new List<Pawn> { liveInWorld, deadInWorld, heldOnMap }, ambushList);
    }

    [TestMethod]
    public void MapComponentOverlandAmbush_ContainsTrackedPawn_DetectsTrackedEnemies()
    {
        Pawn tracked = CreateUnspawnedPawn();
        Pawn untracked = CreateUnspawnedPawn();

        var ambush = (MapComponent_OverlandAmbush)RuntimeHelpers.GetUninitializedObject(typeof(MapComponent_OverlandAmbush));
        TrackedEnemiesField.SetValue(ambush, new List<Pawn> { tracked });

        Assert.IsTrue(ambush.ContainsTrackedPawn(tracked));
        Assert.IsFalse(ambush.ContainsTrackedPawn(untracked));
        Assert.IsFalse(ambush.ContainsTrackedPawn(null));
    }

    [TestMethod]
    [DoNotParallelize]
    public void RedressAndGcPatches_ProtectTrackedGeoscapePawnsAndIgnoreUntrackedPawns()
    {
        var previousInstance = IncursionRetreatManager.Instance;
        Pawn tracked = CreateUnspawnedPawn();
        Pawn untracked = CreateUnspawnedPawn();

        try
        {
            var manager = new IncursionRetreatManager(null!);
            var batches = (List<IncursionRetreatManager.RetreatBatch>)ActiveBatchesField.GetValue(manager)!;
            batches.Add(new IncursionRetreatManager.RetreatBatch
            {
                fleeingPawns = new List<Pawn> { tracked }
            });

            bool redressTracked = true;
            PawnGenerator_IsValidCandidateToRedress_Patch.Postfix(tracked, ref redressTracked);
            Assert.IsFalse(redressTracked, "Tracked Geoscape pawn must never be selected for redressing.");

            bool redressUntracked = true;
            PawnGenerator_IsValidCandidateToRedress_Patch.Postfix(untracked, ref redressUntracked);
            Assert.IsTrue(redressUntracked, "Untracked pawn must remain eligible for vanilla redressing.");

            string? gcReasonTracked = null;
            WorldPawnGC_GetCriticalPawnReason_Patch.Postfix(tracked, ref gcReasonTracked);
            Assert.AreEqual("ProjectVigilance_Tracked", gcReasonTracked, "Tracked pawn must receive a critical GC reason.");

            string? existingReason = "Colonist";
            WorldPawnGC_GetCriticalPawnReason_Patch.Postfix(tracked, ref existingReason);
            Assert.AreEqual("Colonist", existingReason, "Existing vanilla critical reason must be preserved.");

            string? gcReasonUntracked = null;
            WorldPawnGC_GetCriticalPawnReason_Patch.Postfix(untracked, ref gcReasonUntracked);
            Assert.IsNull(gcReasonUntracked, "Untracked pawn must remain null for vanilla GC reason.");
        }
        finally
        {
            var instanceProp = typeof(IncursionRetreatManager).GetProperty(nameof(IncursionRetreatManager.Instance))!;
            instanceProp.SetValue(null, previousInstance);
        }
    }
}

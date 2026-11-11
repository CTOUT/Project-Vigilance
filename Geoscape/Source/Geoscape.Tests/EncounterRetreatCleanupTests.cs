using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape.World;

namespace Geoscape.Tests;

[TestClass]
[DoNotParallelize]
public sealed class EncounterRetreatCleanupTests
{
    [TestMethod]
    public void IsOverlandEncounterState_DefaultEmptyComponent_ReturnsFalse()
    {
        bool result = MapComponent_OverlandAmbush.IsOverlandEncounterState(
            isActive: false,
            ambushSprung: false,
            hasSourceIncursion: false,
            engagementCount: 0,
            guestCohortCount: 0,
            hasTrackedEnemyForce: false,
            capturedColonistCount: 0);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsOverlandEncounterState_AnyActiveTrackingSignal_ReturnsTrue()
    {
        Assert.IsTrue(MapComponent_OverlandAmbush.IsOverlandEncounterState(
            isActive: true, ambushSprung: false, hasSourceIncursion: false, engagementCount: 0, guestCohortCount: 0, hasTrackedEnemyForce: false, capturedColonistCount: 0));
        Assert.IsTrue(MapComponent_OverlandAmbush.IsOverlandEncounterState(
            isActive: false, ambushSprung: true, hasSourceIncursion: false, engagementCount: 0, guestCohortCount: 0, hasTrackedEnemyForce: false, capturedColonistCount: 0));
        Assert.IsTrue(MapComponent_OverlandAmbush.IsOverlandEncounterState(
            isActive: false, ambushSprung: false, hasSourceIncursion: true, engagementCount: 0, guestCohortCount: 0, hasTrackedEnemyForce: false, capturedColonistCount: 0));
        Assert.IsTrue(MapComponent_OverlandAmbush.IsOverlandEncounterState(
            isActive: false, ambushSprung: false, hasSourceIncursion: false, engagementCount: 1, guestCohortCount: 0, hasTrackedEnemyForce: false, capturedColonistCount: 0));
        Assert.IsTrue(MapComponent_OverlandAmbush.IsOverlandEncounterState(
            isActive: false, ambushSprung: false, hasSourceIncursion: false, engagementCount: 0, guestCohortCount: 1, hasTrackedEnemyForce: false, capturedColonistCount: 0));
        Assert.IsTrue(MapComponent_OverlandAmbush.IsOverlandEncounterState(
            isActive: false, ambushSprung: false, hasSourceIncursion: false, engagementCount: 0, guestCohortCount: 0, hasTrackedEnemyForce: true, capturedColonistCount: 0));
        Assert.IsTrue(MapComponent_OverlandAmbush.IsOverlandEncounterState(
            isActive: false, ambushSprung: false, hasSourceIncursion: false, engagementCount: 0, guestCohortCount: 0, hasTrackedEnemyForce: false, capturedColonistCount: 1));
    }

    [TestMethod]
    public void IsOverlandEncounterMapState_SettlementWithoutEncounterState_ReturnsFalse()
    {
        bool result = IncursionTacticalRetreatUtility.IsOverlandEncounterMapState(
            isPlayerHome: false,
            isPocketMap: false,
            isTemporaryBattlefieldParent: false,
            hasAmbushEncounterState: false);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsOverlandEncounterMapState_PlayerHomeOrPocketMap_AlwaysReturnsFalse()
    {
        Assert.IsFalse(IncursionTacticalRetreatUtility.IsOverlandEncounterMapState(
            isPlayerHome: true,
            isPocketMap: false,
            isTemporaryBattlefieldParent: true,
            hasAmbushEncounterState: true));

        Assert.IsFalse(IncursionTacticalRetreatUtility.IsOverlandEncounterMapState(
            isPlayerHome: false,
            isPocketMap: true,
            isTemporaryBattlefieldParent: true,
            hasAmbushEncounterState: true));
    }

    [TestMethod]
    public void IsOverlandEncounterMapState_CaravansBattlefieldOrActiveAmbush_ReturnsTrue()
    {
        Assert.IsTrue(IncursionTacticalRetreatUtility.IsOverlandEncounterMapState(
            isPlayerHome: false,
            isPocketMap: false,
            isTemporaryBattlefieldParent: true,
            hasAmbushEncounterState: false));

        Assert.IsTrue(IncursionTacticalRetreatUtility.IsOverlandEncounterMapState(
            isPlayerHome: false,
            isPocketMap: false,
            isTemporaryBattlefieldParent: false,
            hasAmbushEncounterState: true));
    }

    [TestMethod]
    public void ClassifyMapCleanupAction_PersistentSettlementOrSite_NeverDestroysParent()
    {
        var action = IncursionTacticalRetreatUtility.ClassifyMapCleanupAction(
            isPlayerHome: false,
            isPocketMap: false,
            hasParent: true,
            parentDestroyed: false,
            isTemporaryBattlefieldParent: false);

        Assert.AreEqual(
            IncursionTacticalRetreatUtility.EncounterMapCleanupAction.CheckRemovePersistentParent,
            action);
    }

    [TestMethod]
    public void ClassifyMapCleanupAction_TemporaryBattlefield_DestroysTemporaryParent()
    {
        var action = IncursionTacticalRetreatUtility.ClassifyMapCleanupAction(
            isPlayerHome: false,
            isPocketMap: false,
            hasParent: true,
            parentDestroyed: false,
            isTemporaryBattlefieldParent: true);

        Assert.AreEqual(
            IncursionTacticalRetreatUtility.EncounterMapCleanupAction.DestroyTemporaryParent,
            action);
    }

    [TestMethod]
    public void ClassifyMapCleanupAction_PlayerHomePocketOrDestroyedParent_ReturnsNone()
    {
        Assert.AreEqual(
            IncursionTacticalRetreatUtility.EncounterMapCleanupAction.None,
            IncursionTacticalRetreatUtility.ClassifyMapCleanupAction(
                isPlayerHome: true,
                isPocketMap: false,
                hasParent: true,
                parentDestroyed: false,
                isTemporaryBattlefieldParent: true));

        Assert.AreEqual(
            IncursionTacticalRetreatUtility.EncounterMapCleanupAction.None,
            IncursionTacticalRetreatUtility.ClassifyMapCleanupAction(
                isPlayerHome: false,
                isPocketMap: true,
                hasParent: true,
                parentDestroyed: false,
                isTemporaryBattlefieldParent: true));

        Assert.AreEqual(
            IncursionTacticalRetreatUtility.EncounterMapCleanupAction.None,
            IncursionTacticalRetreatUtility.ClassifyMapCleanupAction(
                isPlayerHome: false,
                isPocketMap: false,
                hasParent: false,
                parentDestroyed: false,
                isTemporaryBattlefieldParent: true));

        Assert.AreEqual(
            IncursionTacticalRetreatUtility.EncounterMapCleanupAction.None,
            IncursionTacticalRetreatUtility.ClassifyMapCleanupAction(
                isPlayerHome: false,
                isPocketMap: false,
                hasParent: true,
                parentDestroyed: true,
                isTemporaryBattlefieldParent: true));
    }

    [TestMethod]
    public void ExitMapAndRetreatPredicates_OrdinaryNpcSettlement_DoesNotInterceptOrHijack()
    {
        Assert.IsFalse(IncursionTacticalRetreatUtility.ShouldInterceptPlayerExitMap(
            isPlayerHome: false,
            isOverlandEncounterMap: false,
            allowedToJoinOrCreateCaravan: true,
            isCaravanMember: false,
            isTeleporting: false,
            isInsideTransporter: false,
            hasHomeRetreatCondition: false));

        Assert.IsFalse(IncursionTacticalRetreatUtility.ShouldTrackNonPlayerExitMap(
            isPlayerHome: false,
            isOverlandEncounterMap: false,
            isTrackedIncursionPawn: false,
            isHostile: true));

        Assert.IsFalse(IncursionTacticalRetreatUtility.ShouldAllowRetreatExitGrid(
            isPlayerHome: false,
            isOverlandEncounterMap: false,
            hasHomeRetreatCondition: false));

        Assert.IsFalse(IncursionTacticalRetreatUtility.ShouldShowEncounterRetreatGizmo(
            isPlayerHome: false,
            isOverlandEncounterMap: false));
    }

    [TestMethod]
    public void ShouldInterceptPlayerExitMap_IgnoresCaravanReformPodsTeleportAndPeacefulHomeLeave()
    {
        // Dialog_FormCaravan / Reform Caravan passes allowedToJoinOrCreateCaravan = false after MakeCaravan.
        Assert.IsFalse(IncursionTacticalRetreatUtility.ShouldInterceptPlayerExitMap(
            isPlayerHome: true,
            isOverlandEncounterMap: false,
            allowedToJoinOrCreateCaravan: false,
            isCaravanMember: true,
            isTeleporting: false,
            isInsideTransporter: false,
            hasHomeRetreatCondition: true));

        Assert.IsFalse(IncursionTacticalRetreatUtility.ShouldInterceptPlayerExitMap(
            isPlayerHome: false,
            isOverlandEncounterMap: true,
            allowedToJoinOrCreateCaravan: false,
            isCaravanMember: false,
            isTeleporting: false,
            isInsideTransporter: false,
            hasHomeRetreatCondition: false));

        // Transport pod / shuttle / teleport exits must pass through to RimWorld cleanly.
        Assert.IsFalse(IncursionTacticalRetreatUtility.ShouldInterceptPlayerExitMap(
            isPlayerHome: false,
            isOverlandEncounterMap: true,
            allowedToJoinOrCreateCaravan: true,
            isCaravanMember: false,
            isTeleporting: true,
            isInsideTransporter: false,
            hasHomeRetreatCondition: false));

        Assert.IsFalse(IncursionTacticalRetreatUtility.ShouldInterceptPlayerExitMap(
            isPlayerHome: false,
            isOverlandEncounterMap: true,
            allowedToJoinOrCreateCaravan: true,
            isCaravanMember: false,
            isTeleporting: false,
            isInsideTransporter: true,
            hasHomeRetreatCondition: false));

        // Peaceful home departure (for example a quitting colonist) without threat or retreat caravan passes through.
        Assert.IsFalse(IncursionTacticalRetreatUtility.ShouldInterceptPlayerExitMap(
            isPlayerHome: true,
            isOverlandEncounterMap: false,
            allowedToJoinOrCreateCaravan: true,
            isCaravanMember: false,
            isTeleporting: false,
            isInsideTransporter: false,
            hasHomeRetreatCondition: false));
    }

    [TestMethod]
    public void ExitMapAndRetreatPredicates_PlayerHomeAndOverlandEncounters_PreserveExpectedBehaviour()
    {
        Assert.IsTrue(IncursionTacticalRetreatUtility.ShouldInterceptPlayerExitMap(
            isPlayerHome: true,
            isOverlandEncounterMap: false,
            allowedToJoinOrCreateCaravan: true,
            isCaravanMember: false,
            isTeleporting: false,
            isInsideTransporter: false,
            hasHomeRetreatCondition: true));

        Assert.IsTrue(IncursionTacticalRetreatUtility.ShouldInterceptPlayerExitMap(
            isPlayerHome: false,
            isOverlandEncounterMap: true,
            allowedToJoinOrCreateCaravan: true,
            isCaravanMember: false,
            isTeleporting: false,
            isInsideTransporter: false,
            hasHomeRetreatCondition: false));

        Assert.IsTrue(IncursionTacticalRetreatUtility.ShouldTrackNonPlayerExitMap(
            isPlayerHome: true,
            isOverlandEncounterMap: false,
            isTrackedIncursionPawn: false,
            isHostile: true));

        Assert.IsFalse(IncursionTacticalRetreatUtility.ShouldTrackNonPlayerExitMap(
            isPlayerHome: true,
            isOverlandEncounterMap: false,
            isTrackedIncursionPawn: false,
            isHostile: false));

        Assert.IsTrue(IncursionTacticalRetreatUtility.ShouldTrackNonPlayerExitMap(
            isPlayerHome: false,
            isOverlandEncounterMap: true,
            isTrackedIncursionPawn: true,
            isHostile: false));

        Assert.IsTrue(IncursionTacticalRetreatUtility.ShouldAllowRetreatExitGrid(
            isPlayerHome: true,
            isOverlandEncounterMap: false,
            hasHomeRetreatCondition: true));

        Assert.IsFalse(IncursionTacticalRetreatUtility.ShouldAllowRetreatExitGrid(
            isPlayerHome: true,
            isOverlandEncounterMap: false,
            hasHomeRetreatCondition: false));

        Assert.IsTrue(IncursionTacticalRetreatUtility.ShouldShowEncounterRetreatGizmo(
            isPlayerHome: true,
            isOverlandEncounterMap: false));
    }

    [TestMethod]
    public void ShouldShowReformCaravanUi_SuppressesSettlementPocketHomeAndPassiveTransit()
    {
        // Cleared CaravansBattlefield shows Reform Caravan UI.
        Assert.IsTrue(IncursionTacticalRetreatUtility.ShouldShowReformCaravanUi(
            isPlayerHome: false,
            isPocketMap: false,
            isSettlementParent: false,
            isTemporaryBattlefieldParent: true,
            hasActiveThreats: false,
            hasCombatOrAmbushState: false));

        // Combat engagement on a temporary site shows Reform Caravan UI once threats clear.
        Assert.IsTrue(IncursionTacticalRetreatUtility.ShouldShowReformCaravanUi(
            isPlayerHome: false,
            isPocketMap: false,
            isSettlementParent: false,
            isTemporaryBattlefieldParent: false,
            hasActiveThreats: false,
            hasCombatOrAmbushState: true));

        // NPC Settlement must never show overland Reform Caravan UI even after a transit pass-by.
        Assert.IsFalse(IncursionTacticalRetreatUtility.ShouldShowReformCaravanUi(
            isPlayerHome: false,
            isPocketMap: false,
            isSettlementParent: true,
            isTemporaryBattlefieldParent: false,
            hasActiveThreats: false,
            hasCombatOrAmbushState: true));

        // Persistent Site with only peaceful transit (no combat/ambush) must not show Sector Clear UI.
        Assert.IsFalse(IncursionTacticalRetreatUtility.ShouldShowReformCaravanUi(
            isPlayerHome: false,
            isPocketMap: false,
            isSettlementParent: false,
            isTemporaryBattlefieldParent: false,
            hasActiveThreats: false,
            hasCombatOrAmbushState: false));

        // Player home, pocket map, or active threats never show Reform Caravan UI.
        Assert.IsFalse(IncursionTacticalRetreatUtility.ShouldShowReformCaravanUi(
            isPlayerHome: true,
            isPocketMap: false,
            isSettlementParent: false,
            isTemporaryBattlefieldParent: false,
            hasActiveThreats: false,
            hasCombatOrAmbushState: true));

        Assert.IsFalse(IncursionTacticalRetreatUtility.ShouldShowReformCaravanUi(
            isPlayerHome: false,
            isPocketMap: true,
            isSettlementParent: false,
            isTemporaryBattlefieldParent: false,
            hasActiveThreats: false,
            hasCombatOrAmbushState: true));

        Assert.IsFalse(IncursionTacticalRetreatUtility.ShouldShowReformCaravanUi(
            isPlayerHome: false,
            isPocketMap: false,
            isSettlementParent: false,
            isTemporaryBattlefieldParent: true,
            hasActiveThreats: true,
            hasCombatOrAmbushState: true));
    }

    [TestMethod]
    public void IsCombatOrAmbushState_ReflectsAmbushOrNonTransitEngagement()
    {
        Assert.IsFalse(MapComponent_OverlandAmbush.IsCombatOrAmbushState(
            ambushSprung: false,
            hasNonTransitOrCombatEngagement: false));

        Assert.IsTrue(MapComponent_OverlandAmbush.IsCombatOrAmbushState(
            ambushSprung: true,
            hasNonTransitOrCombatEngagement: false));

        Assert.IsTrue(MapComponent_OverlandAmbush.IsCombatOrAmbushState(
            ambushSprung: false,
            hasNonTransitOrCombatEngagement: true));
    }

    [TestMethod]
    public void ShouldAbductDownedColonistsOnExit_OnlyWhenBattlefieldOrActiveIncursion()
    {
        Assert.IsFalse(IncursionTacticalRetreatUtility.ShouldAbductDownedColonistsOnExit(
            isTemporaryBattlefieldParent: false,
            hasActiveIncursions: false));

        Assert.IsTrue(IncursionTacticalRetreatUtility.ShouldAbductDownedColonistsOnExit(
            isTemporaryBattlefieldParent: true,
            hasActiveIncursions: false));

        Assert.IsTrue(IncursionTacticalRetreatUtility.ShouldAbductDownedColonistsOnExit(
            isTemporaryBattlefieldParent: false,
            hasActiveIncursions: true));
    }

    [TestMethod]
    public void NullSafeHelpers_DoNotThrow()
    {
        Assert.IsFalse(IncursionTacticalRetreatUtility.IsOverlandEncounterMap(null));
        IncursionTacticalRetreatUtility.CleanUpEncounterMap(null!);
    }
}

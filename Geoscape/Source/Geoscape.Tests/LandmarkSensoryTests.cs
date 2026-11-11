using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using ProjectVigilance.Geoscape;
using ProjectVigilance.Geoscape.Patches;
using ProjectVigilance.Geoscape.Sensory;

namespace Geoscape.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LandmarkSensoryTests
{
    private static void EnsureEnvironmentInitialised()
    {
        var prefsField = typeof(Prefs).GetField("data", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        if (prefsField != null && prefsField.GetValue(null) == null)
        {
            var prefsDataType = typeof(Prefs).Assembly.GetType("Verse.PrefsData");
            if (prefsDataType != null)
            {
                prefsField.SetValue(null, System.Activator.CreateInstance(prefsDataType));
            }
        }
        if (LanguageDatabase.activeLanguage == null)
        {
            LanguageDatabase.activeLanguage = new LoadedLanguage("English");
        }
    }

    [TestMethod]
    public void IsManMadeLandmarkDef_DistinguishesStructuresFromGeography()
    {
        // Man-made and anomalous structures must return true
        string[] manMade = new[]
        {
            "Ruins",
            "FrozenRuins",
            "AncientLaunchSite",
            "AncientGarrison",
            "AncientWarehouse",
            "AncientChemfuelRefinery",
            "AncientInfestedSettlement",
            "AbandonedColonyTribal",
            "AbandonedColonyOutlander",
            "AncientSmokeVent",
            "AncientToxVent",
            "AncientHeatVent",
            "AncientQuarry",
            "Harbor",
            "TerraformingScar",
            "InsectMegahive",
            "Mechhive"
        };

        foreach (string defName in manMade)
        {
            Assert.IsTrue(
                LandmarkSensoryUtility.IsManMadeLandmarkDef(defName),
                $"Expected '{defName}' to be identified as a man-made landmark.");
        }

        // Mundane geographical and weather features must return false
        string[] geography = new[]
        {
            "Lake",
            "Pond",
            "Oasis",
            "DryLake",
            "HotSprings",
            "Archipelago",
            "Bay",
            "Fjord",
            "Iceberg",
            "Valley",
            "Cavern",
            "Chasm",
            "Plateau",
            "Cliffs",
            "Dunes"
        };

        foreach (string defName in geography)
        {
            Assert.IsFalse(
                LandmarkSensoryUtility.IsManMadeLandmarkDef(defName),
                $"Expected '{defName}' to NOT be classified as a man-made landmark.");
        }
    }

    [TestMethod]
    public void IsAnomalousStructure_IdentifiesKnownDefNames()
    {
        EnsureEnvironmentInitialised();

        WorldObjectDef monolithDef = new() { defName = "FallenMonolith" };
        WorldObjectDef ancientComplexDef = new() { defName = "AncientComplex" };
        WorldObjectDef ruinsDef = new() { defName = "AncientRuins" };
        WorldObjectDef insectHiveDef = new() { defName = "InsectMegahive" };
        WorldObjectDef mechhiveDef = new() { defName = "Mechhive" };
        WorldObjectDef normalSettlementDef = new() { defName = "Settlement" };

        WorldObject monolithObj = new() { def = monolithDef };
        WorldObject complexObj = new() { def = ancientComplexDef };
        WorldObject ruinsObj = new() { def = ruinsDef };
        WorldObject insectHiveObj = new() { def = insectHiveDef };
        WorldObject mechhiveObj = new() { def = mechhiveDef };
        WorldObject normalObj = new() { def = normalSettlementDef };

        Assert.IsTrue(LandmarkSensoryUtility.IsAnomalousStructure(monolithObj));
        Assert.IsTrue(LandmarkSensoryUtility.IsAnomalousStructure(complexObj));
        Assert.IsTrue(LandmarkSensoryUtility.IsAnomalousStructure(ruinsObj));
        Assert.IsTrue(LandmarkSensoryUtility.IsAnomalousStructure(insectHiveObj));
        Assert.IsTrue(LandmarkSensoryUtility.IsAnomalousStructure(mechhiveObj));
        Assert.IsFalse(LandmarkSensoryUtility.IsAnomalousStructure(normalObj));
        Assert.IsFalse(LandmarkSensoryUtility.IsAnomalousStructure(null));
    }

    [TestMethod]
    public void IsAnomalousStructure_IdentifiesInsectHiveSiteParts()
    {
        EnsureEnvironmentInitialised();

        Site siteWithInfestation = new();
        SitePartDef infestedDef = new() { defName = "OpportunitySite_AncientInfestedSettlement" };
        siteWithInfestation.parts = new System.Collections.Generic.List<SitePart>
        {
            new SitePart(siteWithInfestation, infestedDef, null)
        };

        Assert.IsTrue(LandmarkSensoryUtility.IsAnomalousStructure(siteWithInfestation));
    }

    [TestMethod]
    public void ProceduralExclamationMark_RasterisesWithCorrectGeometry()
    {
        Texture2D tex = LandmarkSensoryUtility.CreateProceduralExclamationMarkTexture();
        Assert.IsNotNull(tex);
        Assert.AreEqual(64, tex.width);
        Assert.AreEqual(64, tex.height);

        // Center stem pixel (31, 40) should be solid white
        Color stemColor = tex.GetPixel(31, 40);
        Assert.AreEqual(1f, stemColor.r, 0.05f);
        Assert.AreEqual(1f, stemColor.g, 0.05f);
        Assert.AreEqual(1f, stemColor.b, 0.05f);
        Assert.IsTrue(stemColor.a > 0.8f);

        // Center dot pixel (31, 11) should be solid white
        Color dotColor = tex.GetPixel(31, 11);
        Assert.AreEqual(1f, dotColor.r, 0.05f);
        Assert.AreEqual(1f, dotColor.g, 0.05f);
        Assert.AreEqual(1f, dotColor.b, 0.05f);
        Assert.IsTrue(dotColor.a > 0.8f);

        // Corner pixel (0, 0) should be transparent
        Color cornerColor = tex.GetPixel(0, 0);
        Assert.AreEqual(0f, cornerColor.a, 0.01f);
    }

    [TestMethod]
    public void SensoryColours_TacticalCyanDistinctFromAmber()
    {
        Color cyan = LandmarkSensoryUtility.TacticalCyan;
        Color amber = WorldObject_Visibility_Patch.UnknownSiteIconColor;

        // Tactical cyan is predominantly cyan/blue (high blue, low red)
        Assert.IsTrue(cyan.b > 0.9f);
        Assert.IsTrue(cyan.r < 0.5f);

        // Amber threat indicator is predominantly warm/red (high red, low blue)
        Assert.IsTrue(amber.r > 0.9f);
        Assert.IsTrue(amber.b < 0.3f);
    }

    [TestMethod]
    public void Landmarks_DrawStandard_SuppressesUnidentifiedManMadeLandmarks()
    {
        VisibilityMode originalMode = GeoscapeMod.Settings.visibilityMode;
        SensoryManager? originalInstance = SensoryManager.Instance;
        Func<PlanetTile, bool>? originalExplored = SensoryManager.IsExploredOverride;
        Func<PlanetTile, bool>? originalIdentified = SensoryManager.IsIdentifiedOverride;

        try
        {
            GeoscapeMod.Settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;

            var def = new RimWorld.LandmarkDef { defName = "Ruins" };
            var lm = new RimWorld.Landmark { def = def };
            var tile = (PlanetTile)100;

            // With SensoryManager.Instance == null, Prefix returns true
            SensoryManager.Instance = null;
            bool allow = WorldObject_Visibility_Patch.Landmarks_DrawStandard_Patch.Prefix(null, lm, tile);
            Assert.IsTrue(allow);

            // Explored but unidentified man-made landmark suppresses vanilla random-rotation draw
            // (and draws upright cyan '!' when WorldDrawLayer_Landmarks instance is attached)
            SensoryManager.Instance = new SensoryManager(null!);
            SensoryManager.IsExploredOverride = _ => true;
            SensoryManager.IsIdentifiedOverride = _ => false;
            allow = WorldObject_Visibility_Patch.Landmarks_DrawStandard_Patch.Prefix(null, lm, tile);
            Assert.IsFalse(allow);

            // Once identified, vanilla landmark drawing resumes
            SensoryManager.IsIdentifiedOverride = _ => true;
            allow = WorldObject_Visibility_Patch.Landmarks_DrawStandard_Patch.Prefix(null, lm, tile);
            Assert.IsTrue(allow);

            // Natural landmark remains visible when explored even before identification
            var lakeDef = new RimWorld.LandmarkDef { defName = "Lake" };
            var lakeLm = new RimWorld.Landmark { def = lakeDef };
            SensoryManager.IsIdentifiedOverride = _ => false;
            allow = WorldObject_Visibility_Patch.Landmarks_DrawStandard_Patch.Prefix(null, lakeLm, tile);
            Assert.IsTrue(allow);

            // When VisibilityMode is Omniscient, Prefix always returns true regardless
            GeoscapeMod.Settings.visibilityMode = VisibilityMode.Omniscient;
            allow = WorldObject_Visibility_Patch.Landmarks_DrawStandard_Patch.Prefix(null, lm, tile);
            Assert.IsTrue(allow);
        }
        finally
        {
            GeoscapeMod.Settings.visibilityMode = originalMode;
            SensoryManager.Instance = originalInstance;
            SensoryManager.IsExploredOverride = originalExplored;
            SensoryManager.IsIdentifiedOverride = originalIdentified;
        }
    }

    [TestMethod]
    public void LandmarksToShow_KeepsExploredLandmarksAndFiltersUnexplored()
    {
        VisibilityMode originalMode = GeoscapeMod.Settings.visibilityMode;
        SensoryManager? originalInstance = SensoryManager.Instance;
        Func<PlanetTile, bool>? originalExplored = SensoryManager.IsExploredOverride;
        Func<PlanetTile, bool>? originalIdentified = SensoryManager.IsIdentifiedOverride;

        try
        {
            GeoscapeMod.Settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;
            SensoryManager.Instance = new SensoryManager(null!);

            PlanetTile exploredUnidentifiedTile = new(101);
            PlanetTile unexploredTile = new(102);

            SensoryManager.IsExploredOverride = t => t == exploredUnidentifiedTile;
            SensoryManager.IsIdentifiedOverride = _ => false;

            var shown = new System.Collections.Generic.List<PlanetTile>
            {
                exploredUnidentifiedTile,
                unexploredTile
            };

            WorldObject_Visibility_Patch.LandmarksToShow_Patch.Postfix(ref shown);

            Assert.AreEqual(1, shown.Count);
            Assert.AreEqual(exploredUnidentifiedTile, shown[0]);
        }
        finally
        {
            GeoscapeMod.Settings.visibilityMode = originalMode;
            SensoryManager.Instance = originalInstance;
            SensoryManager.IsExploredOverride = originalExplored;
            SensoryManager.IsIdentifiedOverride = originalIdentified;
        }
    }

    private sealed class TestRadarCoverageLayer : WorldDrawLayer_RadarCoverage
    {
        public System.Collections.Generic.List<Verse.LayerSubMesh> ExposedSubMeshes => subMeshes;
    }

    [TestMethod]
    public void RenderQueues_SeparateDimmingHazeWashOutlinesAndWorldObjectsInOrder()
    {
        System.Collections.Generic.Dictionary<int, UnityEngine.Material> materialsByQueue = new()
        {
            [WorldDrawLayer_RadarCoverage.DimmingRenderQueue] =
                (UnityEngine.Material)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(UnityEngine.Material)),
            [WorldDrawLayer_HazeOfWar.HazeRenderQueue] =
                (UnityEngine.Material)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(UnityEngine.Material)),
            [WorldDrawLayer_RadarCoverage.ShadowWashRenderQueue] =
                (UnityEngine.Material)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(UnityEngine.Material)),
            [WorldDrawLayer_RadarCoverage.OutlineRenderQueue] =
                (UnityEngine.Material)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(UnityEngine.Material))
        };

        int[] orderedQueues =
        {
            WorldDrawLayer_RadarCoverage.DimmingRenderQueue,
            WorldDrawLayer_HazeOfWar.HazeRenderQueue,
            WorldDrawLayer_RadarCoverage.ShadowWashRenderQueue,
            WorldDrawLayer_RadarCoverage.OutlineRenderQueue,
            WorldMaterials.WorldObjectRenderQueue
        };
        for (int i = 1; i < orderedQueues.Length; i++)
        {
            Assert.IsTrue(
                orderedQueues[i - 1] < orderedQueues[i],
                $"Expected queue[{i - 1}]={orderedQueues[i - 1]} < queue[{i}]={orderedQueues[i]}.");
        }

        System.Collections.Generic.List<Verse.LayerSubMesh> subMeshes = new();
        System.Collections.Generic.List<int> requestedQueues = new();
        System.Func<int, UnityEngine.Material?> queueOverride = q =>
        {
            requestedQueues.Add(q);
            return materialsByQueue[q];
        };

        int index = 0;
        Verse.LayerSubMesh dimMesh = WorldDrawLayer_HazeOfWar.GetOrCreateVertexColorSubMesh(
            subMeshes, 38000, 7, WorldDrawLayer_RadarCoverage.DimmingRenderQueue, null, queueOverride, ref index);
        Verse.LayerSubMesh hazeMesh = WorldDrawLayer_HazeOfWar.GetOrCreateVertexColorSubMesh(
            subMeshes, 38000, 7, WorldDrawLayer_HazeOfWar.HazeRenderQueue, null, queueOverride, ref index);
        Verse.LayerSubMesh washMesh = WorldDrawLayer_HazeOfWar.GetOrCreateVertexColorSubMesh(
            subMeshes, 38000, 7, WorldDrawLayer_RadarCoverage.ShadowWashRenderQueue, null, queueOverride, ref index);
        Verse.LayerSubMesh outlineMesh = WorldDrawLayer_HazeOfWar.GetOrCreateVertexColorSubMesh(
            subMeshes, 38000, 4, WorldDrawLayer_RadarCoverage.OutlineRenderQueue, null, queueOverride, ref index);

        Assert.AreEqual(4, subMeshes.Count);
        Assert.AreNotSame(dimMesh, hazeMesh);
        Assert.AreNotSame(hazeMesh, washMesh);
        Assert.AreNotSame(washMesh, outlineMesh);
        CollectionAssert.AreEqual(
            new[]
            {
                WorldDrawLayer_RadarCoverage.DimmingRenderQueue,
                WorldDrawLayer_HazeOfWar.HazeRenderQueue,
                WorldDrawLayer_RadarCoverage.ShadowWashRenderQueue,
                WorldDrawLayer_RadarCoverage.OutlineRenderQueue
            },
            requestedQueues);
    }

    [TestMethod]
    public void RadarCoverage_ShadowWashAndHatching_UseSeparateRenderQueueSubMeshesAndReuseAcrossTiles()
    {
        System.Func<UnityEngine.Material?>? originalMaterialOverride = WorldDrawLayer_RadarCoverage.SubMeshMaterialOverride;
        System.Func<int, UnityEngine.Material?>? originalQueueOverride = WorldDrawLayer_RadarCoverage.RenderQueueMaterialOverride;
        System.Action<PlanetTile, System.Collections.Generic.List<UnityEngine.Vector3>>? originalVertsOverride =
            WorldDrawLayer_RadarCoverage.TileVerticesOverride;
        System.Func<PlanetTile, UnityEngine.Vector3>? originalCenterOverride = WorldDrawLayer_RadarCoverage.TileCenterOverride;

        try
        {
            UnityEngine.Material washMat =
                (UnityEngine.Material)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(UnityEngine.Material));
            UnityEngine.Material outlineMat =
                (UnityEngine.Material)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(UnityEngine.Material));

            System.Collections.Generic.List<int> requestedQueues = new();
            WorldDrawLayer_RadarCoverage.SubMeshMaterialOverride = null;
            WorldDrawLayer_RadarCoverage.RenderQueueMaterialOverride = q =>
            {
                requestedQueues.Add(q);
                return q == WorldDrawLayer_RadarCoverage.ShadowWashRenderQueue ? washMat : outlineMat;
            };
            WorldDrawLayer_RadarCoverage.TileCenterOverride = _ => new UnityEngine.Vector3(0f, 0f, 100f);
            WorldDrawLayer_RadarCoverage.TileVerticesOverride = (_, verts) =>
            {
                verts.Add(new UnityEngine.Vector3(1f, 1f, 100f));
                verts.Add(new UnityEngine.Vector3(-1f, 1f, 100f));
                verts.Add(new UnityEngine.Vector3(-1f, -1f, 100f));
                verts.Add(new UnityEngine.Vector3(1f, -1f, 100f));
            };

            TestRadarCoverageLayer layer = new();
            int subMeshIndex = 0;
            UnityEngine.Color32 hatchColor = new(255, 255, 255, 160);
            UnityEngine.Color32 washColor = new(0, 0, 0, 80);

            layer.RenderShadowCellHatching(null!, new PlanetTile(1), hatchColor, washColor, ref subMeshIndex);
            layer.RenderShadowCellHatching(null!, new PlanetTile(2), hatchColor, washColor, ref subMeshIndex);

            Assert.AreEqual(2, layer.ExposedSubMeshes.Count);
            Assert.AreSame(washMat, layer.ExposedSubMeshes[0].material);
            Assert.AreSame(outlineMat, layer.ExposedSubMeshes[1].material);
            CollectionAssert.Contains(requestedQueues, WorldDrawLayer_RadarCoverage.ShadowWashRenderQueue);
            CollectionAssert.Contains(requestedQueues, WorldDrawLayer_RadarCoverage.OutlineRenderQueue);
        }
        finally
        {
            WorldDrawLayer_RadarCoverage.SubMeshMaterialOverride = originalMaterialOverride;
            WorldDrawLayer_RadarCoverage.RenderQueueMaterialOverride = originalQueueOverride;
            WorldDrawLayer_RadarCoverage.TileVerticesOverride = originalVertsOverride;
            WorldDrawLayer_RadarCoverage.TileCenterOverride = originalCenterOverride;
        }
    }

    [TestMethod]
    public void UnknownLandmark_PrintRotation_AlignsUpAxisWithNorth()
    {
        float rotation = WorldObject_ListeningPost.UprightPrintRotationAngle;
        Assert.AreEqual(90f, rotation);

        WorldRendererUtility.GetTangentsToPlanet(
            new UnityEngine.Vector3(0f, 0f, 100f),
            out UnityEngine.Vector3 defaultFirstAxis,
            out UnityEngine.Vector3 defaultSecondAxis,
            0f);

        Assert.IsTrue(defaultSecondAxis.x > 0.99f, $"Expected rot=0 +second axis to point East (+X), got {defaultSecondAxis}");
        Assert.IsTrue(defaultFirstAxis.y > 0.99f, $"Expected rot=0 +first axis to point North (+Y), got {defaultFirstAxis}");

        WorldRendererUtility.GetTangentsToPlanet(
            new UnityEngine.Vector3(0f, 0f, 100f),
            out UnityEngine.Vector3 firstAxis,
            out UnityEngine.Vector3 secondAxis,
            rotation);

        Assert.IsTrue(secondAxis.y > 0.99f, $"Expected rot=90 +second axis to point North (+Y), got {secondAxis}");
        Assert.IsTrue(firstAxis.x < -0.99f, $"Expected rot=90 +first axis to point West (-X), got {firstAxis}");
    }

    private sealed class TestRefreshTrackingLayer : WorldDrawLayerBase
    {
        public int RegenerateCalls { get; private set; }

        public void ClearDirtyForTest()
        {
            dirty = false;
        }

        public override System.Collections.IEnumerable Regenerate()
        {
            RegenerateCalls++;
            yield break;
        }
    }

    [TestMethod]
    public void RequestLayerRefresh_DefersOffMainThreadAndRegeneratesOnMainThread()
    {
        System.Func<bool>? originalMainThreadOverride = WorldDrawLayer_HazeOfWar.IsInMainThreadOverride;
        try
        {
            TestRefreshTrackingLayer layer = new();
            layer.ClearDirtyForTest();

            WorldDrawLayer_HazeOfWar.IsInMainThreadOverride = () => false;
            WorldDrawLayer_HazeOfWar.RequestLayerRefresh(layer);
            Assert.AreEqual(0, layer.RegenerateCalls, "Off-main-thread callers must never execute RegenerateNow.");
            Assert.IsTrue(layer.Dirty, "Off-main-thread callers must mark the layer dirty for main-thread render.");

            WorldDrawLayer_HazeOfWar.IsInMainThreadOverride = () => true;
            WorldDrawLayer_HazeOfWar.RequestLayerRefresh(layer);
            Assert.AreEqual(1, layer.RegenerateCalls, "Main-thread callers must regenerate immediately.");
            Assert.IsFalse(layer.Dirty, "RegenerateNow must clear the dirty flag.");

            // Null-safe no-op
            WorldDrawLayer_HazeOfWar.RequestLayerRefresh(null);
        }
        finally
        {
            WorldDrawLayer_HazeOfWar.IsInMainThreadOverride = originalMainThreadOverride;
        }
    }

    [TestMethod]
    public void OverlayMaterials_HasStartupAttributeAndCoversAllOverlayQueues()
    {
        Type cacheType = typeof(GeoscapeOverlayMaterials);
        Assert.IsTrue(
            cacheType.IsDefined(typeof(StaticConstructorOnStartup), inherit: false),
            "GeoscapeOverlayMaterials must be marked [StaticConstructorOnStartup] for main-thread initialisation.");

        CollectionAssert.AreEquivalent(
            new[]
            {
                WorldDrawLayer_RadarCoverage.DimmingRenderQueue,
                WorldDrawLayer_HazeOfWar.HazeRenderQueue,
                WorldDrawLayer_RadarCoverage.ShadowWashRenderQueue,
                WorldDrawLayer_RadarCoverage.OutlineRenderQueue
            },
            WorldDrawLayer_HazeOfWar.OverlayRenderQueues);

        System.Reflection.FieldInfo[] matArrayFields = Array.FindAll(
            cacheType.GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public),
            f => f.FieldType == typeof(Material) || f.FieldType == typeof(Material[]));
        Assert.AreEqual(1, matArrayFields.Length, "Expected prewarmed Material[] holder on GeoscapeOverlayMaterials.");
    }
}

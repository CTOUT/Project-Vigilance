using System;
using System.Xml;
using ProjectVigilance.Core.Modules;
using Verse;

namespace ProjectVigilance.Core.Migration;

/// <summary>
/// Intercepts orphaned XML Defs and C# class names in existing savegames when split modules
/// (such as Orbital, Aquatics, or Thermal) are not installed, redirecting them into safe stubs
/// so RimWorld loads cleanly without red Scribe console errors.
/// </summary>
public class BackCompatibilityConverter_VigilanceModular : BackCompatibilityConverter
{
    public const string MissingModuleWorldObjectDefName = "Vigilance_MissingModuleWorldObject";

    public override bool AppliesToVersion(int majorVer, int minorVer) => true;

    public override void PreLoadSavegame(string loadingVersion)
    {
        VigilanceMigrationTracker.ClearPendingInterceptions();
    }

    public override string BackCompatibleDefName(Type defType, string defName, bool forDefInjections, XmlNode node)
    {
        if (string.IsNullOrEmpty(defName))
        {
            return null!;
        }

        // Redirect Orbital WorldObjectDefs when ProjectVigilance.Orbital is inactive
        if (!VigilanceModuleManager.IsModuleActive(VigilanceModuleId.Orbital))
        {
            if (defName == "Vigilance_OrbitalStation" ||
                defName.StartsWith("Vigilance_Satellite_", StringComparison.Ordinal))
            {
                VigilanceMigrationTracker.RecordInterceptedEntity(VigilanceModuleId.Orbital, defName);
                return MissingModuleWorldObjectDefName;
            }
        }

        // Redirect Aquatics WorldObjectDef and record missing Sonar ThingDef when ProjectVigilance.Aquatics is inactive
        if (!VigilanceModuleManager.IsModuleActive(VigilanceModuleId.Aquatics))
        {
            if (defName == "Vigilance_ListeningPost_Sonar")
            {
                VigilanceMigrationTracker.RecordInterceptedEntity(VigilanceModuleId.Aquatics, defName);
                return MissingModuleWorldObjectDefName;
            }

            if (defName == "Geoscape_SonarArray" && defType == typeof(ThingDef))
            {
                VigilanceMigrationTracker.RecordInterceptedEntity(VigilanceModuleId.Aquatics, defName);
            }
        }

        return null!;
    }

    public override Type GetBackCompatibleType(Type baseType, string providedClassName, XmlNode node)
    {
        if (string.IsNullOrEmpty(providedClassName))
        {
            return null!;
        }

        // Redirect Orbital classes when ProjectVigilance.Orbital is inactive
        if (!VigilanceModuleManager.IsModuleActive(VigilanceModuleId.Orbital))
        {
            if (providedClassName == "ProjectVigilance.Geoscape.Orbital.OrbitalManager")
            {
                VigilanceMigrationTracker.RecordInterceptedEntity(VigilanceModuleId.Orbital, "OrbitalManager");
                return typeof(DeprecatedWorldComponentStub);
            }

            if (providedClassName == "ProjectVigilance.Geoscape.Orbital.WorldObject_OrbitalStation" ||
                providedClassName == "ProjectVigilance.Geoscape.Orbital.WorldObject_Satellite" ||
                providedClassName == "ProjectVigilance.Geoscape.Orbital.Satellite_Trade")
            {
                string defLabel = node?["def"]?.InnerText ?? providedClassName;
                VigilanceMigrationTracker.RecordInterceptedEntity(VigilanceModuleId.Orbital, defLabel);
                return typeof(MissingModuleWorldObjectStub);
            }
        }

        // Redirect Sonar Listening Posts to stub when ProjectVigilance.Aquatics is inactive
        if (!VigilanceModuleManager.IsModuleActive(VigilanceModuleId.Aquatics))
        {
            if (providedClassName == "ProjectVigilance.Geoscape.Sensory.WorldObject_ListeningPost" &&
                string.Equals(node?["def"]?.InnerText, "Vigilance_ListeningPost_Sonar", StringComparison.Ordinal))
            {
                VigilanceMigrationTracker.RecordInterceptedEntity(VigilanceModuleId.Aquatics, "Vigilance_ListeningPost_Sonar");
                return typeof(MissingModuleWorldObjectStub);
            }
        }

        return null!;
    }

    public override void PostCouldntLoadDef(string defName)
    {
        if (defName == "Geoscape_SonarArray" && !VigilanceModuleManager.IsModuleActive(VigilanceModuleId.Aquatics))
        {
            VigilanceMigrationTracker.RecordInterceptedEntity(VigilanceModuleId.Aquatics, defName);
        }
    }

    public override int GetBackCompatibleBodyPartIndex(BodyDef body, int index) => index;

    public override void PostExposeData(object obj)
    {
    }
}

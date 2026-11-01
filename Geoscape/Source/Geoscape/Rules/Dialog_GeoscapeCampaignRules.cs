using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ProjectVigilance.Geoscape.Rules;

/// <summary>
/// World-generation configuration modal allowing players to choose campaign rules and presets
/// before generating the planetary world.
/// </summary>
public sealed class Dialog_GeoscapeCampaignRules : Window
{
    private readonly GeoscapeRuleSet draft;
    private Vector2 scrollPosition = Vector2.zero;

    public override Vector2 InitialSize => new(650f, 720f);

    public Dialog_GeoscapeCampaignRules(GeoscapeRuleSet draft)
    {
        this.draft = draft;
        forcePause = true;
        doCloseX = true;
        doCloseButton = false;
        closeOnClickedOutside = false;
        absorbInputAroundWindow = true;
    }

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 36f), "Geoscape Campaign Rules");
        Text.Font = GameFont.Small;

        Rect descRect = new(inRect.x, inRect.y + 38f, inRect.width, 42f);
        Widgets.Label(descRect, "Configure strategic rules for this new world. Visibility can only be raised later; all other simulation rules lock permanently once the campaign begins.");

        // Profile Presets Bar
        Rect presetBarRect = new(inRect.x, descRect.yMax + 4f, inRect.width, 30f);
        float btnWidth = (presetBarRect.width - 12f) / 3f;

        CampaignProfile currentProf = draft.DetectCurrentProfile();

        if (Widgets.ButtonText(new Rect(presetBarRect.x, presetBarRect.y, btnWidth, 28f), "Classic Haze (Rec.)"))
        {
            draft.ApplyProfile(CampaignProfile.ClassicHaze);
            SoundDefOf.Click?.PlayOneShotOnCamera();
        }

        if (Widgets.ButtonText(new Rect(presetBarRect.x + btnWidth + 6f, presetBarRect.y, btnWidth, 28f), "Planetary Surveillance"))
        {
            draft.ApplyProfile(CampaignProfile.PlanetarySurveillance);
            SoundDefOf.Click?.PlayOneShotOnCamera();
        }

        if (Widgets.ButtonText(new Rect(presetBarRect.x + (btnWidth + 6f) * 2f, presetBarRect.y, btnWidth, 28f), "Hardcore Vigilance"))
        {
            draft.ApplyProfile(CampaignProfile.HardcoreVigilance);
            SoundDefOf.Click?.PlayOneShotOnCamera();
        }

        Widgets.DrawLineHorizontal(inRect.x, presetBarRect.yMax + 8f, inRect.width);

        // Scrollable Rules View
        float contentTop = presetBarRect.yMax + 14f;
        float bottomBarHeight = 44f;
        Rect outRect = new(inRect.x, contentTop, inRect.width, inRect.height - contentTop - bottomBarHeight);
        Rect viewRect = new(0f, 0f, outRect.width - 20f, 680f);

        Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
        Listing_Standard listing = new();
        listing.Begin(viewRect);

        RuleEditContext ctx = new(
            CampaignActive: false,
            ReviewWindowOpen: false,
            UnsafeOverrideEnabled: false);

        CampaignRulesPanel.DrawAllSections(listing, draft, null, ctx, showLockPreview: true);

        listing.End();
        Widgets.EndScrollView();

        // Bottom Action Buttons
        Rect bottomRect = new(inRect.x, inRect.yMax - 36f, inRect.width, 32f);
        float actionBtnWidth = 180f;

        if (Widgets.ButtonText(new Rect(bottomRect.x, bottomRect.y, actionBtnWidth, 32f), "Restore My Defaults"))
        {
            draft.CopyFromDefaults(GeoscapeMod.Settings);
            SoundDefOf.ClickReject?.PlayOneShotOnCamera();
            Messages.Message("Draft restored to your saved new-campaign defaults.", MessageTypeDefOf.NeutralEvent, false);
        }

        if (Widgets.ButtonText(new Rect(bottomRect.x + actionBtnWidth + 10f, bottomRect.y, actionBtnWidth, 32f), "Save As My Defaults"))
        {
            draft.WriteToDefaults(GeoscapeMod.Settings);
            GeoscapeMod.Settings.Write();
            SoundDefOf.Click?.PlayOneShotOnCamera();
            Messages.Message("Current draft saved as your global new-campaign defaults.", MessageTypeDefOf.PositiveEvent, false);
        }

        if (Widgets.ButtonText(new Rect(bottomRect.xMax - 110f, bottomRect.y, 110f, 32f), "Close"))
        {
            Close();
        }
    }
}

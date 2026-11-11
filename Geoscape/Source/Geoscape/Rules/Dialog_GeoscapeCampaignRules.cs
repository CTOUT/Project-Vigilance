using System;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ProjectVigilance.Geoscape.Rules;

/// <summary>
/// Anomaly-style modal configuration window allowing players to customise fine-grained campaign rules,
/// lookout horizons, and storyteller incursion mechanics before world generation.
/// </summary>
public sealed class Dialog_GeoscapeCampaignRules : Window
{
    private readonly GeoscapeRuleSet draft;
    private readonly GeoscapeCampaignRules? campaign;
    private readonly Action? onClose;
    private Vector2 scrollPosition = Vector2.zero;
    private float lastViewHeight = 720f;

    public override Vector2 InitialSize => new(700f, 760f);

    public Dialog_GeoscapeCampaignRules(
        GeoscapeRuleSet draft,
        GeoscapeCampaignRules? campaign = null,
        Action? onClose = null)
    {
        this.draft = draft;
        this.campaign = campaign;
        this.onClose = onClose;
        forcePause = true;
        doCloseX = true;
        doCloseButton = false;
        closeOnClickedOutside = false;
        absorbInputAroundWindow = true;
    }

    public override void PreClose()
    {
        base.PreClose();
        CampaignWizardPageUtility.SyncPageLinkage(draft);
        onClose?.Invoke();
    }

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), "Customise Campaign Rules");
        Text.Font = GameFont.Small;

        Rect descRect = new(inRect.x, inRect.y + 36f, inRect.width, 38f);
        GUI.color = new Color(0.85f, 0.85f, 0.85f);
        Widgets.Label(descRect, "Fine-tune planetary visibility, lookout horizons, and incursion tactics for this world. Physical simulation rules lock upon world generation to ensure strategic integrity.");
        GUI.color = Color.white;

        // Profile Presets Bar
        CampaignProfile currentProf = draft.DetectCurrentProfile();
        Rect presetBarRect = new(inRect.x, descRect.yMax + 4f, inRect.width, 30f);
        float btnWidth = (presetBarRect.width - 18f) / 4f;

        DrawPresetButton(new Rect(presetBarRect.x, presetBarRect.y, btnWidth, 28f), CampaignProfile.FogOfWar, "Fog of War", currentProf);
        DrawPresetButton(new Rect(presetBarRect.x + btnWidth + 6f, presetBarRect.y, btnWidth, 28f), CampaignProfile.PlanetarySurveillance, "Planetary Surv.", currentProf);
        DrawPresetButton(new Rect(presetBarRect.x + (btnWidth + 6f) * 2f, presetBarRect.y, btnWidth, 28f), CampaignProfile.HardcoreVigilance, "Hardcore", currentProf);
        DrawPresetButton(new Rect(presetBarRect.x + (btnWidth + 6f) * 3f, presetBarRect.y, btnWidth, 28f), CampaignProfile.Omniscient, "Omniscient", currentProf);

        // Active Profile Status
        Rect statusRect = new(inRect.x, presetBarRect.yMax + 6f, inRect.width, 22f);
        Text.Font = GameFont.Tiny;
        GUI.color = currentProf == CampaignProfile.Custom ? new Color(1.0f, 0.85f, 0.35f) : new Color(0.7f, 0.9f, 1.0f);
        Widgets.Label(statusRect, $"Active Profile: {currentProf.GetDisplayName()}");
        GUI.color = Color.white;
        Text.Font = GameFont.Small;

        Widgets.DrawLineHorizontal(inRect.x, statusRect.yMax + 6f, inRect.width);

        // Scrollable Rules View
        float contentTop = statusRect.yMax + 12f;
        float bottomBarHeight = 44f;
        Rect outRect = new(inRect.x, contentTop, inRect.width, inRect.height - contentTop - bottomBarHeight);
        Rect viewRect = new(0f, 0f, outRect.width - 16f, Mathf.Max(lastViewHeight, outRect.height));

        Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
        Listing_Standard listing = new() { maxOneColumn = true };
        listing.Begin(viewRect);

        RuleEditContext ctx = campaign != null
            ? campaign.CreateEditContext()
            : new RuleEditContext(CampaignActive: false, ReviewWindowOpen: false, UnsafeOverrideEnabled: false);

        bool showLockPreview = campaign == null || Current.ProgramState != ProgramState.Playing;
        CampaignRulesPanel.DrawAllSections(listing, draft, campaign, ctx, showLockPreview: showLockPreview);

        lastViewHeight = listing.CurHeight + 20f;
        listing.End();
        Widgets.EndScrollView();

        // Bottom Action Buttons
        Rect bottomRect = new(inRect.x, inRect.yMax - 36f, inRect.width, 32f);
        float actionBtnWidth = 175f;

        if (campaign == null)
        {
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
        }

        if (Widgets.ButtonText(new Rect(bottomRect.xMax - 110f, bottomRect.y, 110f, 32f), "Close"))
        {
            Close();
        }
    }

    private void DrawPresetButton(Rect rect, CampaignProfile profile, string label, CampaignProfile current)
    {
        if (Widgets.ButtonText(rect, label))
        {
            if (campaign == null)
            {
                draft.ApplyProfile(profile);
            }
            else
            {
                campaign.ApplyAdoptionProfile(profile, "Modal Preset Selection");
            }
            SoundDefOf.Click?.PlayOneShotOnCamera();
        }

        if (current == profile)
        {
            Widgets.DrawHighlightSelected(rect);
            GUI.color = new Color(0.2f, 0.8f, 0.4f, 0.8f);
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;
        }
    }
}

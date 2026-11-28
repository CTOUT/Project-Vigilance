using System.Linq;
using ProjectVigilance.Core.Localization;
using ProjectVigilance.Core.UI;
using ProjectVigilance.Geoscape.Rules;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ProjectVigilance.Geoscape;

public partial class GeoscapeSettings
{
    // =========================================================================
    // Tab 5: About Suite & Diagnostics
    // =========================================================================

    private void DrawAboutDiagnosticsTab(Listing_Standard listing)
    {
        VigilanceUiWidgets.DrawSectionHeader(listing, VigilanceLoc.Tr("PV_Diag_ArchTitle", "Project Vigilance — Modular Architecture"));

        string desc = VigilanceLoc.Tr(
            "PV_Diag_ArchDesc",
            "Decoupled RimWorld 1.7 framework suite. Modules operate independently and interconnect automatically when active.");
        float descHeight = Mathf.Max(46f, Text.CalcHeight(desc, Mathf.Max(120f, listing.ColumnWidth - 16f)) + 16f);
        Rect descRect = listing.GetRect(descHeight);
        Widgets.DrawBoxSolid(descRect, new Color(0.12f, 0.15f, 0.20f, 0.6f));
        Widgets.DrawBox(descRect);
        Widgets.Label(descRect.ContractedBy(8f), desc);

        listing.Gap(6f);

        VigilanceUiWidgets.DrawSectionHeader(listing, VigilanceLoc.Tr("PV_Diag_ModulesHeader", "Installed & Available Modules"));

        var modules = ProjectVigilance.Core.Modules.VigilanceModuleManager.Modules;
        bool isEmbeddedFallback = ProjectVigilance.Core.Modules.VigilanceModuleManager.IsCoreEmbeddedFallback;
        float rowHeight = 24f;
        float modInnerWidth = Mathf.Max(120f, listing.ColumnWidth - 16f);

        string embeddedNote = isEmbeddedFallback
            ? VigilanceLoc.Tr(
                "PV_Diag_EmbeddedCoreNote",
                "Running Geoscape with embedded Core fallback. Subscribe to standalone Core Framework to enable optional modules (Orbital, Aquatics, Thermal).")
            : string.Empty;
        float embeddedNoteHeight = 0f;
        if (isEmbeddedFallback)
        {
            using (new VigilanceUiWidgets.ImguiStateScope())
            {
                Text.Font = GameFont.Tiny;
                embeddedNoteHeight = Mathf.Max(22f, Text.CalcHeight(embeddedNote, Mathf.Max(80f, modInnerWidth - 8f)) + 4f);
            }
        }

        float tableHeight = (modules.Count * rowHeight) + 16f + embeddedNoteHeight;

        Rect modTableRect = listing.GetRect(tableHeight);
        Widgets.DrawBoxSolid(modTableRect, new Color(0.08f, 0.11f, 0.14f, 0.8f));
        Widgets.DrawBox(modTableRect);
        Rect modContentRect = modTableRect.ContractedBy(8f);

        string activeLabel = VigilanceLoc.Tr("PV_Diag_Status_Active", "Active");
        string embeddedLabel = VigilanceLoc.Tr("PV_Diag_Status_Embedded", "Embedded Fallback");
        string notInstalledLabel = VigilanceLoc.Tr("PV_Diag_Status_NotInstalled", "Not Installed");

        float curY = modContentRect.y;
        for (int i = 0; i < modules.Count; i++)
        {
            var mod = modules[i];
            bool isActive = ProjectVigilance.Core.Modules.VigilanceModuleManager.IsModuleActive(mod.Id);
            bool isCoreEmbedded = mod.Id == ProjectVigilance.Core.Modules.VigilanceModuleId.Core && !isActive && isEmbeddedFallback;

            Rect rowRect = new Rect(modContentRect.x, curY, modContentRect.width, rowHeight);
            if ((i & 1) == 1)
            {
                Widgets.DrawBoxSolid(rowRect, new Color(1f, 1f, 1f, 0.03f));
            }

            float leftWidth = rowRect.width * 0.68f;
            float rightWidth = rowRect.width - leftWidth;

            string versionInfo = string.Empty;
            if (isActive)
            {
                versionInfo = mod.Id == ProjectVigilance.Core.Modules.VigilanceModuleId.Geoscape
                    ? $" ({GeoscapeMod.FormatBuildDetails()})"
                    : $" (v{BuildInfo.Version}.{BuildInfo.BuildNumber})";
            }

            Widgets.Label(new Rect(rowRect.x + 4f, rowRect.y + 2f, leftWidth - 4f, rowHeight - 2f), $"• {mod.DisplayName}{versionInfo}");

            using (new VigilanceUiWidgets.ImguiStateScope())
            {
                Text.Anchor = TextAnchor.MiddleRight;
                var (statusText, statusColor) = VigilanceUiWidgets.GetModuleStatusBadge(
                    isActive,
                    isCoreEmbedded,
                    activeLabel,
                    embeddedLabel,
                    notInstalledLabel);
                GUI.color = statusColor;
                Widgets.Label(new Rect(rowRect.x + leftWidth, rowRect.y, rightWidth - 6f, rowHeight), statusText);
            }

            string tip = VigilanceLoc.Tr("PV_Diag_ModTooltip", $"{mod.Summary}\nPackage ID: {mod.PackageId}", mod.Summary, mod.PackageId);
            TooltipHandler.TipRegion(rowRect, tip);

            curY += rowHeight;
        }

        if (isEmbeddedFallback)
        {
            using (new VigilanceUiWidgets.ImguiStateScope())
            {
                Text.Font = GameFont.Tiny;
                GUI.color = VigilanceUiWidgets.StatusWarningColor;
                Widgets.Label(new Rect(modContentRect.x + 4f, curY + 2f, modContentRect.width - 8f, embeddedNoteHeight), embeddedNote);
            }
        }

        listing.Gap(6f);

        float leftColW = (listing.ColumnWidth - 16f) * 0.56f;
        float rightColW = (listing.ColumnWidth - 16f) - leftColW;
        Rect bottomRect = listing.GetRect(diagnosticsBottomHeight);
        Rect govRect = new Rect(bottomRect.x, bottomRect.y, leftColW, bottomRect.height);
        Rect cfgRect = new Rect(bottomRect.x + leftColW + 16f, bottomRect.y, rightColW, bottomRect.height);

        GeoscapeCampaignRules? campaign = GeoscapeCampaignRules.Active;

        Listing_Standard govCol = new() { maxOneColumn = true };
        govCol.Begin(govRect);
        {
            VigilanceUiWidgets.DrawSectionHeader(govCol, VigilanceLoc.Tr("PV_Diag_GovernanceHeader", "Active Campaign Governance"));

            if (campaign != null)
            {
                string statusText = $"• Active Profile: {campaign.Rules.DetectCurrentProfile().GetDisplayName()}\n" +
                                    $"• Schema Version: v{campaign.SchemaVersion}\n" +
                                    $"• Campaign Integrity: {campaign.IntegrityStatusLabel}\n" +
                                    $"• Review Window: {(campaign.ReviewWindowOpen ? "Open (Day 1 Grace Period)" : "Locked")}\n" +
                                    $"• Ledger Records: {campaign.Ledger.Count} entries";
                govCol.Label(statusText);

                if (campaign.HasExternalModifications)
                {
                    RuleChangeRecord? lastMod = campaign.Ledger.LastOrDefault(r => r.kind == RuleChangeKind.ExternalModification);
                    if (lastMod != null)
                    {
                        govCol.Gap(2f);
                        Rect warnRect = govCol.GetRect(38f);
                        Widgets.DrawBoxSolid(warnRect, new Color(0.32f, 0.08f, 0.08f, 0.78f));
                        Widgets.DrawBox(warnRect);
                        using (new VigilanceUiWidgets.ImguiStateScope())
                        {
                            GUI.color = VigilanceUiWidgets.AlertTextColor;
                            string safeNote = SanitizeSaveText(lastMod.note, 256);
                            Widgets.Label(warnRect.ContractedBy(4f), $"[Save Integrity Warning] {safeNote}");
                        }
                    }
                }

                if (campaign.ReviewWindowOpen && govCol.ButtonText("Confirm & Lock In Campaign Rules Now"))
                {
                    campaign.ConfirmAndLock();
                    SoundDefOf.Click?.PlayOneShotOnCamera();
                    Messages.Message("Campaign rules locked. Mutability restrictions now active.", MessageTypeDefOf.PositiveEvent, historical: false);
                }

                if (Prefs.DevMode)
                {
                    govCol.Gap(4f);
                    bool req = UnsafeRuleOverride.Requested;
                    bool prev = req;
                    govCol.CheckboxLabeled(
                        "Allow Unsafe Rule Transitions (Session Only)",
                        ref req,
                        "Developer Mode override. Bypasses all mutability constraints, allowing downgrades and editing of immutable rules. All changes are marked [Unsafe] in the audit ledger.");
                    if (req != prev)
                    {
                        if (!req)
                        {
                            UnsafeRuleOverride.SetRequested(false);
                        }
                        else
                        {
                            string warnText = "WARNING: Enabling unsafe rule transitions allows bypassing all campaign mutability constraints (including irreversible downgrades and immutable rule overrides).\n\n" +
                                              "Modifying locked rules mid-campaign can desynchronise active incursion intelligence, corrupt fog-of-war exploration state, or compromise campaign integrity.\n\n" +
                                              "This override lasts for this game session only, but any rule changes made while active are permanently signed into the campaign audit ledger.\n\n" +
                                              "Do you wish to enable Developer Mode unsafe rule transitions?";
                            Dialog_MessageBox confirmBox = Dialog_MessageBox.CreateConfirmation(
                                warnText,
                                () => UnsafeRuleOverride.SetRequested(true),
                                destructive: true,
                                title: "Enable Unsafe Rule Transitions");
                            Find.WindowStack?.Add(confirmBox);
                        }
                    }
                }
            }
            else
            {
                using (new VigilanceUiWidgets.ImguiStateScope())
                {
                    GUI.color = SubNoteColor;
                    string noSave = VigilanceLoc.Tr(
                        "PV_Diag_GovernanceNoSave",
                        "No active campaign loaded. Defaults apply to newly started colonies.");
                    govCol.Label(noSave);
                }
            }
        }
        govCol.End();

        Listing_Standard cfgCol = new() { maxOneColumn = true };
        cfgCol.Begin(cfgRect);
        {
            VigilanceUiWidgets.DrawSectionHeader(cfgCol, VigilanceLoc.Tr("PV_Diag_ConfigHeader", "Configuration Management"));

            using (new VigilanceUiWidgets.ImguiStateScope())
            {
                GUI.color = SubNoteColor;
                string cfgDesc = VigilanceLoc.Tr(
                    "PV_Diag_ConfigDesc",
                    "Restore all Geoscape defaults or reset new-campaign rules without altering an active save.");
                cfgCol.Label(cfgDesc);
            }
            cfgCol.Gap(6f);

            string resetBtnLabel = campaign != null
                ? VigilanceLoc.Tr("PV_Diag_ResetCampaignDefaults", "Reset New Campaign Defaults")
                : VigilanceLoc.Tr("PV_Diag_ResetDefaults", "Reset All Geoscape Settings");

            if (cfgCol.ButtonText(resetBtnLabel))
            {
                ResetToDefaults();
                SoundDefOf.ClickReject?.PlayOneShotOnCamera();
                string msg = campaign != null
                    ? "Project Vigilance new campaign defaults reset. Active save rules remain unaffected."
                    : "Project Vigilance settings reset to default values.";
                Messages.Message(msg, MessageTypeDefOf.NeutralEvent, historical: false);
            }
        }
        cfgCol.End();

        diagnosticsBottomHeight = Mathf.Max(govCol.CurHeight, cfgCol.CurHeight) + 2f;
    }
}

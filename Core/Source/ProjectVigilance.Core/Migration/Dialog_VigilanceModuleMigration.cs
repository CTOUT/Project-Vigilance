using System.Collections.Generic;
using System.Linq;
using ProjectVigilance.Core.Localization;
using ProjectVigilance.Core.Modules;
using ProjectVigilance.Core.UI;
using UnityEngine;
using Verse;
using Verse.Sound;
using RimWorld;

namespace ProjectVigilance.Core.Migration;

/// <summary>
/// Interactive migration modal shown when loading a pre-split save or a save missing
/// previously active Project Vigilance modules. Offers Steam Workshop links or one-click
/// clean retirement of orphaned objects without red Scribe errors.
/// </summary>
public sealed class Dialog_VigilanceModuleMigration : Window
{
    private readonly VigilanceMigrationTracker tracker;

    public override Vector2 InitialSize => new(740f, 520f);

    public Dialog_VigilanceModuleMigration(VigilanceMigrationTracker tracker)
    {
        this.tracker = tracker;
        forcePause = true;
        doCloseX = false;
        doCloseButton = false;
        closeOnClickedOutside = false;
        absorbInputAroundWindow = true;
    }

    public override void DoWindowContents(Rect inRect)
    {
        string title = VigilanceLoc.Tr("PV_Migration_Title", "Project Vigilance — Modular Suite Migration");
        using (new VigilanceUiWidgets.ImguiStateScope())
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), title);
        }

        string intro = VigilanceLoc.Tr(
            "PV_Migration_Intro",
            "Project Vigilance has been separated into independent modular packages. Review module availability for this save:");
        Rect introRect = new(inRect.x, inRect.y + 36f, inRect.width, 42f);
        Widgets.Label(introRect, intro);

        float curY = introRect.yMax + 8f;
        var modules = VigilanceModuleManager.Modules;
        var intercepted = tracker.InterceptedEntitiesByModule;
        var missingSet = tracker.MissingModules;

        string activeLabel = VigilanceLoc.Tr("PV_Migration_ModuleActive", "INSTALLED");
        string embeddedLabel = VigilanceLoc.Tr("PV_Migration_ModuleEmbedded", "EMBEDDED FALLBACK");
        string missingLabel = VigilanceLoc.Tr("PV_Migration_ModuleMissing", "NOT INSTALLED");
        string workshopLabel = VigilanceLoc.Tr("PV_Migration_BtnWorkshop", "Steam Workshop");

        float badgeWidth;
        float btnWidth;
        using (new VigilanceUiWidgets.ImguiStateScope())
        {
            Text.Font = GameFont.Tiny;
            badgeWidth = Mathf.Max(
                115f,
                Text.CalcSize(activeLabel).x + 14f,
                Text.CalcSize(embeddedLabel).x + 14f,
                Text.CalcSize(missingLabel).x + 14f);
            Text.Font = GameFont.Small;
            btnWidth = Mathf.Max(135f, Text.CalcSize(workshopLabel).x + 20f);
        }

        float rowHeight = 58f;
        for (int i = 0; i < modules.Count; i++)
        {
            VigilanceModuleDescriptor mod = modules[i];
            Rect cardRect = new(inRect.x, curY, inRect.width, rowHeight);
            Widgets.DrawMenuSection(cardRect);
            Rect inner = cardRect.ContractedBy(6f);

            bool isActive = VigilanceModuleManager.IsModuleActive(mod.Id);
            bool isFlaggedMissing = missingSet.Contains(mod.Id);

            // Title & Status badge
            float infoWidth = inner.width - badgeWidth - btnWidth - 16f;

            using (new VigilanceUiWidgets.ImguiStateScope())
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(inner.x, inner.y, infoWidth, 22f), mod.DisplayName);

                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.8f, 0.8f, 0.8f);
                string subText = mod.Summary;
                if (!isActive && intercepted.TryGetValue(mod.Id, out List<string>? entities) && entities.Count > 0)
                {
                    string summaryCounts = string.Join(", ", entities.GroupBy(e => e).Select(g => $"{g.Count()}x {SanitizeDisplayText(g.Key, 64)}"));
                    if (summaryCounts.Length > 256)
                    {
                        summaryCounts = summaryCounts.Substring(0, 253) + "...";
                    }
                    subText = VigilanceLoc.Tr("PV_Migration_DetectedEntities", $"Detected in save: {summaryCounts}", summaryCounts);
                    GUI.color = VigilanceUiWidgets.StatusWarningColor;
                }
                Widgets.Label(new Rect(inner.x, inner.y + 22f, infoWidth, 24f), subText);

                // Status Badge
                bool isEmbeddedCore = mod.Id == VigilanceModuleId.Core && !isActive && VigilanceModuleManager.IsCoreEmbeddedFallback;
                Rect badgeRect = new(inner.x + infoWidth + 6f, inner.y + 10f, badgeWidth, 26f);
                var (statusText, statusColor) = VigilanceUiWidgets.GetModuleStatusBadge(
                    isActive,
                    isEmbeddedCore || isFlaggedMissing,
                    activeLabel,
                    embeddedLabel,
                    missingLabel);
                GUI.color = statusColor;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(badgeRect, statusText);
            }

            // Workshop Button
            Rect workshopRect = new(inner.xMax - btnWidth, inner.y + 9f, btnWidth, 28f);
            if (!isActive && !string.IsNullOrEmpty(mod.WorkshopUrl) && Widgets.ButtonText(workshopRect, workshopLabel))
            {
                Application.OpenURL(mod.WorkshopUrl);
            }

            curY += rowHeight + 6f;
        }

        // Bottom Buttons
        float bottomY = inRect.yMax - 36f;
        float exitWidth = 210f;
        float retireWidth = inRect.width - exitWidth - 12f;

        string exitLabel = VigilanceLoc.Tr("PV_Migration_BtnExitToMenu", "Exit to Main Menu");
        if (Widgets.ButtonText(new Rect(inRect.x, bottomY, exitWidth, 34f), exitLabel))
        {
            SoundDefOf.Click?.PlayOneShotOnCamera();
            Close();
            GenScene.GoToMainMenu();
        }

        string retireLabel = VigilanceLoc.Tr("PV_Migration_BtnRetireAndContinue", "Cleanly Retire Missing Objects & Continue");
        if (Widgets.ButtonText(new Rect(inRect.x + exitWidth + 12f, bottomY, retireWidth, 34f), retireLabel))
        {
            int retiredCount = tracker.RetireMissingModuleObjects();
            SoundDefOf.Click?.PlayOneShotOnCamera();
            string msg = VigilanceLoc.Tr(
                "PV_Migration_RetiredMessage",
                $"Project Vigilance: Cleanly retired {retiredCount} missing-module world object(s).",
                retiredCount);
            Messages.Message(msg, MessageTypeDefOf.NeutralEvent, historical: false);
            Close();
        }
    }

    internal static string SanitizeDisplayText(string? raw, int maxLength = 256)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return string.Empty;
        }

        var sb = new System.Text.StringBuilder(Math.Min(raw.Length, maxLength));
        bool inTag = false;
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (c == '<')
            {
                inTag = true;
                continue;
            }
            if (c == '>')
            {
                inTag = false;
                continue;
            }
            if (!inTag)
            {
                sb.Append(c);
                if (sb.Length >= maxLength)
                {
                    break;
                }
            }
        }

        string cleaned = sb.ToString();
        if (cleaned.Length >= maxLength && raw.Length > maxLength && maxLength > 3)
        {
            return cleaned.Substring(0, maxLength - 3) + "...";
        }
        return cleaned;
    }
}

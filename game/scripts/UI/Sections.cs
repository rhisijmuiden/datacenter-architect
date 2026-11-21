using System;
using DatacenterArchitect.Core;
using Godot;

namespace DatacenterArchitect.UI;

/// <summary>The screens that can open in the management drawer.</summary>
public enum PanelKind { Overview, Company, Staff, Reputation, Growth, Power, Network, Progress, Building }

/// <summary>One tab in a management section: which screen it opens and at which of that screen's own tabs.</summary>
public record SectionTab(string Key, string Icon, PanelKind Kind, int Tab);

/// <summary>One button on the management rail (left), with its tabs.</summary>
public record Section(string Key, string ShortKey, string Icon, Color Accent, Key Hotkey, SectionTab[] Tabs);

/// <summary>
/// The menu structure of the management rail (GUI v2). Building lives in the build bar along the bottom,
/// managing lives here: seven sections, F1 to F7, each with its own tabs on the left of the drawer.
/// </summary>
public static class Sections
{
    public const int Overview = 0, Company = 1, Customers = 2, Infra = 3, Staff = 4, Reputation = 5, Growth = 6, Progress = 7;

    public static readonly Section[] All =
    {
        new("nav_overview", "nav_overview_short", "dashboard", Brand.Ink, Key.F1, new SectionTab[]
        {
            new("ov_tab_dashboard", "dashboard", PanelKind.Overview, OverviewPanel.TabDashboard),
            new("ov_tab_goals", "check", PanelKind.Overview, OverviewPanel.TabGoals),
            new("ov_tab_messages", "alert", PanelKind.Overview, OverviewPanel.TabMessages),
        }),
        new("nav_company", "nav_company_short", "org", Brand.Amber, Key.F2, new SectionTab[]
        {
            new("co_tab_org", "org", PanelKind.Company, CompanyPanel.TabOrg),
            new("gr_tab_people", "key", PanelKind.Growth, GrowthPanel.TabPeople),
            new("co_tab_finance", "money", PanelKind.Company, CompanyPanel.TabFinance),
            new("gr_tab_risks", "shield", PanelKind.Growth, GrowthPanel.TabRisks),
            new("co_tab_brand", "palette", PanelKind.Company, CompanyPanel.TabBrand),
        }),
        new("nav_customers", "nav_customers_short", "cat_customers", Brand.Ok, Key.F3, new SectionTab[]
        {
            new("co_tab_sales", "cat_customers", PanelKind.Company, CompanyPanel.TabSales),
            new("co_tab_contracts", "contract", PanelKind.Company, CompanyPanel.TabContracts),
            new("co_tab_tenders", "tender", PanelKind.Company, CompanyPanel.TabTenders),
            new("gr_tab_market", "chart", PanelKind.Growth, GrowthPanel.TabMarket),
        }),
        new("nav_infra", "nav_infra_short", "bolt", Brand.Power, Key.F4, new SectionTab[]
        {
            new("nav_tab_power", "bolt", PanelKind.Power, PowerPanel.TabOverview),
            new("nav_tab_power_tree", "msb", PanelKind.Power, PowerPanel.TabTree),
            new("nav_tab_network", "cat_network", PanelKind.Network, NetworkPanel.TabOverview),
            new("nav_tab_nettree", "tree", PanelKind.Network, NetworkPanel.TabTree),
            new("nav_tab_own_energy", "leaf", PanelKind.Power, PowerPanel.TabEnergy),
            new("gr_tab_energy", "chart", PanelKind.Growth, GrowthPanel.TabEnergy),
            new("nav_tab_bms", "cat_halls", PanelKind.Building, 0),
        }),
        new("nav_staff", "nav_staff_short", "crew", Brand.Cooling, Key.F5, new SectionTab[]
        {
            new("st_tab_team", "crew", PanelKind.Staff, StaffPanel.TabTeam),
            new("st_tab_policy", "roster", PanelKind.Staff, StaffPanel.TabPolicy),
            new("st_tab_maintenance", "wrench", PanelKind.Staff, StaffPanel.TabMaintenance),
            new("st_tab_incidents", "alert", PanelKind.Staff, StaffPanel.TabIncidents),
        }),
        new("nav_reputation", "nav_reputation_short", "star", new Color("b79cff"), Key.F6, new SectionTab[]
        {
            new("rp_tab_reputation", "star", PanelKind.Reputation, ReputationPanel.TabReputation),
            new("rp_tab_security", "shield", PanelKind.Reputation, ReputationPanel.TabSecurity),
            new("rp_tab_certs", "certificate", PanelKind.Reputation, ReputationPanel.TabCerts),
            new("rp_tab_research", "flask", PanelKind.Reputation, ReputationPanel.TabResearch),
            new("rp_tab_proposals", "bulb", PanelKind.Reputation, ReputationPanel.TabProposals),
        }),
        new("nav_growth", "nav_growth_short", "globe", Brand.Fiber, Key.F7, new SectionTab[]
        {
            new("gr_tab_locations", "globe", PanelKind.Growth, GrowthPanel.TabLocations),
            new("gr_tab_offices", "office", PanelKind.Growth, GrowthPanel.TabOffices),
            new("gr_tab_hosting", "cloud", PanelKind.Growth, GrowthPanel.TabHosting),
        }),
        new("nav_progress", "nav_progress_short", "trophy", new Color("f2c94c"), Key.F8, new SectionTab[]
        {
            new("pg_tab_stats", "stats", PanelKind.Progress, ProgressPanel.TabStats),
            new("pg_tab_scenario", "medal", PanelKind.Progress, ProgressPanel.TabScenario),
            new("pg_tab_achievements", "trophy", PanelKind.Progress, ProgressPanel.TabAchievements),
            new("pg_tab_timelapse", "film", PanelKind.Progress, ProgressPanel.TabTimelapse),
            new("pg_tab_awards", "trophy", PanelKind.Progress, ProgressPanel.TabAwards),
        }),
    };

    /// <summary>The section and tab that show this screen at this tab; the first match wins, else the first tab of that screen.</summary>
    public static (int section, int tab) Find(PanelKind kind, int tab)
    {
        (int, int)? first = null;
        for (var s = 0; s < All.Length; s++)
            for (var t = 0; t < All[s].Tabs.Length; t++)
            {
                var e = All[s].Tabs[t];
                if (e.Kind != kind) continue;
                if (e.Tab == tab) return (s, t);
                first ??= (s, t);
            }
        return first ?? (0, 0);
    }

    /// <summary>The section for a function key, or -1.</summary>
    public static int ForKey(Key k) => Array.FindIndex(All, s => s.Hotkey == k);
}

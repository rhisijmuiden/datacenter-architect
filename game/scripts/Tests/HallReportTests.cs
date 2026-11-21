using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;

namespace DatacenterArchitect.Tests;

/// <summary>Infra sketch board 6: the data hall card with power, network and cooling against the racks' needs, redundancy and upgrades.</summary>
public static partial class TestRunner
{
    static void HallReportTests()
    {
        var (s, hall, _, _) = PowerSite();
        LiveRaw(s, ObjKind.Switchgear, 24, 4);
        LiveRaw(s, ObjKind.Ups, 29, 4);
        var racks = Enumerable.Range(0, 10).Select(i => LiveRaw(s, ObjKind.Rack, 6 + i, 8)).ToList();
        var r = HallReport.Build(s, hall);
        Check(r.Racks == 10 && System.Math.Abs(r.NeedKw - 60) < 1e-6, $"the card counts the racks and their kW ({r.Racks}, {r.NeedKw})");
        Check(r.PowerLevel == HallLevel.Short && r.Upgrades[0].Kind == HallUpgradeKind.Pdu && r.Upgrades[0].Priority == 0, "no PDU: power is short and a PDU is the first upgrade");
        Check(r.CoolLevel == HallLevel.Short && r.NetLevel == HallLevel.Short && !r.AllOnline, "no CRAH and no switch: cooling and network are short, the hall is not online");
        Check(r.Upgrades.Any(u => u.Kind == HallUpgradeKind.Busbar) && r.Upgrades.Any(u => u.Kind == HallUpgradeKind.Containment), "busbar and containment are offered");

        // one PDU 75 (A): every rack fed, single side
        var a = Live(s, ObjKind.Pdu75, 22, 20);
        r = HallReport.Build(s, hall);
        Check(r.PowerLevel == HallLevel.N && r.A.Units == 1 && r.B.Units == 0 && r.A.UsedOutlets == 10, $"one PDU: power N on side A ({r.PowerLevel})");
        var pdu = r.Upgrades.First(u => u.Kind == HallUpgradeKind.Pdu);
        Check(pdu.Obj == ObjKind.Pdu75 && pdu.Count == 1 && pdu.Priority > 0, "for A+B the card offers one more PDU 75");
        Check(r.Headroom == 0 && r.HeadroomBy != "power", $"no more racks fit: the hall has no network ports or cooling yet ({r.Headroom}, {r.HeadroomBy})");

        // a second PDU 75 goes on side B: A+B on a shared switchboard
        Live(s, ObjKind.Pdu75, 22, 18);
        r = HallReport.Build(s, hall);
        Check(r.PowerLevel == HallLevel.AB && !r.SeparateMsb && r.Upgrades.All(u => u.Kind != HallUpgradeKind.Pdu), $"two PDUs: A+B, no PDU upgrade left ({r.PowerLevel})");
        Check(r.Features().Contains("hall_feat_ab"), "A+B shows as a feature");

        // 14 racks are too many for two PDU 75s (24 outlets is enough, but each side has 12 for A+B)
        for (var i = 0; i < 4; i++) racks.Add(LiveRaw(s, ObjKind.Rack, 6 + i, 11));
        r = HallReport.Build(s, hall);
        Check(r.PowerLevel == HallLevel.Partial, $"14 racks on 2 × 12 outlets: partly A+B ({r.PowerLevel})");

        // cooling: CRAHs and heat rejection outside
        LiveRaw(s, ObjKind.Chiller, 40, 30);
        var crah = LiveRaw(s, ObjKind.Crah, 20, 6);
        r = HallReport.Build(s, hall);
        Check(r.Crahs == 1 && r.CoolLevel == HallLevel.N && r.CrahsForNPlus1(s) == 1, $"one CRAH 120 for 84 kW: N, one more for N+1 ({r.CoolLevel})");
        LiveRaw(s, ObjKind.Crah, 20, 9);
        r = HallReport.Build(s, hall);
        Check(r.CoolLevel >= HallLevel.NPlus1 && r.Upgrades.All(u => u.Kind != HallUpgradeKind.Crah), $"two CRAH 120: N+1 or better ({r.CoolLevel})");
        crah.Broken = true; s.Touch(false);
        r = HallReport.Build(s, hall);
        Check(r.BrokenCrahs == 1 && r.CoolLevel == HallLevel.N, "a broken CRAH does not count");
        crah.Broken = false; s.Touch(false);

        // without heat rejection the CRAHs cannot get rid of the heat
        s.Objects.RemoveAll(o => o.Kind == ObjKind.Chiller); s.Touch(false);
        r = HallReport.Build(s, hall);
        Check(r.RejectLimited && r.CoolLevel == HallLevel.Short, "no chiller: cooling is limited by heat rejection outside");

        // the busbar upgrade: both sides get a run
        s.Money += 10_000_000;
        Check(r.Upgrades.First(u => u.Kind == HallUpgradeKind.Busbar).Blocked == null, "the busbar can be installed");
        Check(s.UpgradeHall(hall, true) == null, "busbar upgrade starts");
        r = HallReport.Build(s, hall);
        Check(r.Upgrades.First(u => u.Kind == HallUpgradeKind.Busbar).Blocked == "msg_upgrade_busy", "while it is being installed the busbar shows as busy");
        hall.Upgrade = 0; hall.Busbar = true; s.Touch(false);
        r = HallReport.Build(s, hall);
        Check(r.A.Busbar && r.B.Busbar && r.A.Unlimited && r.PowerLevel == HallLevel.AB && r.Upgrades.All(u => u.Kind != HallUpgradeKind.Busbar), $"with the busbar every rack is A+B ({r.PowerLevel})");
        Check(r.Features().Contains("hall_feat_busbar"), "the busbar shows as a feature");

        // PDU sizing and texts
        Check(HallReport.PduFor(250, 30) == (ObjKind.Pdu300, 1) && HallReport.PduFor(60, 10) == (ObjKind.Pdu75, 1), "the card picks the PDU that covers the gap with the fewest units");
        foreach (var u in r.Upgrades) { var (t, sub) = UI.HallCard.UpgradeText(s, r, u); Check(!string.IsNullOrEmpty(t) && !t.StartsWith("hall_") && !sub.StartsWith("hall_"), $"upgrade text {u.Kind}"); }
        Check(r.Sig() == HallReport.Build(s, hall).Sig(), "the signature is stable while nothing changes");
        var card = UI.HallCard.Build(s, r, _ => { }, () => { }, () => { }, () => { });
        Check(card != null && card.GetChildCount() > 3, "the card builds");
        card.Free();
        UI.HallCard.Tab = UI.HallCard.TabUpgrades;
        card = UI.HallCard.Build(s, r, _ => { }, () => { }, () => { }, () => { });
        Check(card.GetChildCount() > 3, "the upgrades tab builds");
        card.Free();
        UI.HallCard.Tab = UI.HallCard.TabCapacity;
    }
}

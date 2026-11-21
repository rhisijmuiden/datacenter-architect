using System;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;

namespace DatacenterArchitect.Tests;

/// <summary>Own energy (2026-11-21): hourly balance, battery policy, the green label with nomination and grace, green customers.</summary>
public static partial class TestRunner
{
    static EnergyDayRecord Fake(int day, double use, double own) => new() { Day = day, Use = use, Direct = own, Solar = own };

    static void OwnEnergyTests()
    {
        var s = Quiet();
        var day = (int)s.Day + 1;
        var use = s.Infra.FacilityKw;
        Check(use > 0, $"the demo site uses power ({use:0} kW)");

        // ---- no own generation: everything from the grid, same bill as before ----
        var rec = s.SimulateEnergyDay(day);
        var price = s.EnergyPrice() * (1 - s.EnergyDiscount);
        Check(Math.Abs(rec.Use - use * 24) < 1e-6 && Math.Abs(rec.Grid - rec.Use) < 1e-6 && rec.Own == 0 && rec.OwnShare == 0, "without solar and wind all power comes from the grid");
        Check(Math.Abs(rec.GridEur - rec.Use * price) < 1e-3 && rec.SoldEur == 0, "the grid bill is use × price, like before");
        Check(s.EnergyHours.Count == 24, "the day is kept hour by hour");

        // ---- solar and wind feed the site first ----
        for (var i = 0; i < 6; i++) LiveRaw(s, ObjKind.SolarPanel, 2 + 2 * i, 2);
        for (var i = 0; i < 3; i++) LiveRaw(s, ObjKind.WindTurbine, 2 + 3 * i, 40);
        rec = s.SimulateEnergyDay(day + 1);
        Check(rec.Own > 0 && rec.Wind > 0, "own generation is counted");
        Check(Math.Abs(rec.Direct + rec.Discharged + rec.Grid - rec.Use) < 1e-6, "use = own direct + battery + grid");
        Check(Math.Abs(rec.Direct + rec.Charged + rec.Sold + rec.Curtailed - rec.Own) < 1e-6, "own power = direct + charged + sold + switched off");
        Check(s.EnergyHours[0].Solar == 0 && s.EnergyHours[12].Solar > 0, "solar at noon, none at midnight");
        Check(s.EnergyHours.All(h => h.Direct <= h.Use + 1e-9 && h.Direct <= h.Own + 1e-9), "own use comes first, never more than the use");

        // ---- big surplus: battery first, then the grid ----
        for (var i = 0; i < 40; i++) LiveRaw(s, ObjKind.WindTurbine, 2 + (i % 10) * 3, 44 + i / 10 * 3);
        LiveRaw(s, ObjKind.BatteryContainer, 30, 2);
        s.BatteryUse = BatteryMode.Own; s.BatterySoc = 0; s.SellSurplus = true;
        rec = s.SimulateEnergyDay(day + 2);
        Check(rec.Charged > 0 && rec.Sold > 0 && s.BatterySoc > 0, $"surplus charges the battery and the rest is sold ({rec.Charged:0} / {rec.Sold:0} kWh)");
        Check(rec.SoldEur > 0 && s.BatteryProfit() == 0, "selling earns money; storing own power does not trade");
        s.SellSurplus = false;
        rec = s.SimulateEnergyDay(day + 3);
        Check(rec.Sold == 0 && rec.Curtailed > 0, "selling off: the surplus is switched off");
        s.SellSurplus = true;

        // ---- trading keeps the battery off your own surplus ----
        s.BatteryUse = BatteryMode.Trade;
        rec = s.SimulateEnergyDay(day + 4);
        Check(rec.Charged == 0 && rec.Discharged == 0 && s.BatteryProfit() > 0, "trade mode: the battery works the market, not your surplus");
        s.BatteryUse = BatteryMode.Smart;
        Check(s.BatteryProfit() >= 0, "smart mode trades with what is left");

        // ---- reserve: own use never empties the battery below it ----
        s.Objects.RemoveAll(o => o.Kind is ObjKind.SolarPanel or ObjKind.WindTurbine); s.Touch(false);
        s.BatteryUse = BatteryMode.Own; s.BatteryReserve = 0.3; s.BatterySoc = s.BatteryKwh;
        rec = s.SimulateEnergyDay(day + 5);
        Check(rec.Discharged > 0 && s.BatterySoc >= 0.3 * s.BatteryKwh - 1e-6, "the battery helps at night but keeps the outage reserve");

        // ---- 30-day share, nomination, Green Gold ----
        var g = Quiet();
        Unlock(g);
        var u = g.Infra.FacilityKw * 24;
        for (var i = 0; i < 29; i++) g.EnergyLog.Add(Fake(i + 1, u, u * 0.6));
        g.GreenDay(29);
        Check(g.GreenNominatedDay < 0, "no nomination before 30 days");
        g.EnergyLog.Add(Fake(30, u, u * 0.6));
        g.GreenDay(30);
        Check(Math.Abs(g.OwnShare30 - 0.6) < 1e-9 && g.GreenNominatedDay == 30 && g.GoldNominatedDay < 0, "30 days at 60%: nominated for green, not for gold");
        Check(g.Notes.Any(n => n.key == "msg_green_nominated"), "the nomination is announced");
        var checks = g.CertChecks(CertKind.Green);
        Check(checks.Any(r => r.Key == "cert_req_own_energy" && r.Status == ReqStatus.Ok) && checks.Any(r => r.Key == "cert_req_nominated" && r.Status == ReqStatus.Ok), "the audit sees the own share and the nomination");
        Check(g.CertChecks(CertKind.GreenGold).Any(r => r.Key == "cert_req_own_energy" && r.Status == ReqStatus.Fail), "Green Gold needs 80%");
        Check(!g.GreenLabel, "a nomination alone is not the label");

        // ---- label held: grace, suspension and recovery ----
        g.Certs.Add(new Certificate { Kind = CertKind.Green, Valid = true, IssuedDay = g.Day, ExpiresDay = g.Day + 1000, CheckDay = g.Day + 365 });
        Check(g.GreenLabel, "with the certificate you hold the label");
        for (var i = 0; i < 30; i++) g.EnergyLog.Add(Fake(31 + i, u, u * 0.3));
        g.GreenDay(61);
        Check(g.GreenBelowDays == 1 && g.GreenLabel && g.Notes.Any(n => n.key == "msg_green_below"), "under the threshold: a warning, the label stays");
        for (var i = 0; i < GameState.GreenGraceDays - 1; i++) g.GreenDay(62 + i);
        Check(!g.GreenLabel && g.Cert(CertKind.Green).Suspended && g.Cert(CertKind.Green).Valid, "after 60 days the label is suspended, not withdrawn");
        for (var i = 0; i < 30; i++) g.EnergyLog.Add(Fake(200 + i, u, u * 0.7));
        g.GreenDay(230);
        Check(g.GreenLabel && !g.Cert(CertKind.Green).Suspended && g.GreenBelowDays == 0, "back above the threshold: the label returns without a new audit");

        // ---- customers that want green ----
        var rng = new Random(7);
        var lead = g.MakeLead(0, rng, CustomerType.Smb); lead.Green = true; lead.GreenGold = false;
        Check(g.Check(lead).Any(r => r.Key == "req_green" && r.Status == ReqStatus.Ok), "green leads sign with the label");
        g.Cert(CertKind.Green).Suspended = true;
        Check(g.Check(lead).Any(r => r.Key == "req_green" && r.Status == ReqStatus.Fail), "a suspended label does not count");
        g.Cert(CertKind.Green).Suspended = false;
        g.Supplier = Catalog.Suppliers.First(x => x.Green).Id;
        g.Certs.Clear();
        Check(g.Check(lead).Any(r => r.Key == "req_green" && r.Status == ReqStatus.Fail), "a green supplier contract alone is not enough any more");
        Check(g.EsgLines().First(l => l.Key == "esg_renewable").Points == 30, "a green supplier still gives the ESG points");
        var none = Enumerable.Range(0, 300).Select(_ => g.MakeLead(0, rng)).Count(d => d.Type == CustomerType.GreenCloud);
        Check(none == 0, "sustainable clouds only ask once you hold the label");
        g.Certs.Add(new Certificate { Kind = CertKind.Green, Valid = true, ExpiresDay = g.Day + 1000, CheckDay = g.Day + 365 });
        g.Reputation = 60;
        var some = Enumerable.Range(0, 300).Select(_ => g.MakeLead(0, rng)).Where(d => d.Type == CustomerType.GreenCloud).ToList();
        Check(some.Count > 0 && some.All(d => d.Green), $"with the label sustainable clouds come, and they want green ({some.Count})");
        var gov = Enumerable.Range(0, 8).Select(_ => g.MakeLead(0, rng, CustomerType.Government)).ToList();
        Check(gov.Any(d => d.GreenGold) && gov.Any(d => !d.GreenGold) && gov.Where(d => d.GreenGold).All(d => d.Green), "some ministries want Green Gold");
        var goldLead = gov.First(d => d.GreenGold);
        Check(g.Check(goldLead).Any(r => r.Key == "req_green_gold" && r.Status == ReqStatus.Fail), "Green Gold leads need the gold label");

        // ---- old saves and the save file ----
        var o = Quiet();
        o.BatteryTrading = true; o.SaveFormat = 13; o.Notes.Clear();
        LiveRaw(o, ObjKind.BatteryContainer, 30, 2);
        o.Migrate();
        Check(o.BatteryUse == BatteryMode.Trade && o.SaveFormat == GameState.CurrentFormat && o.Notes.Any(n => n.key == "msg_own_energy_new"), "old saves keep trading and hear about own energy");
        s.BatteryUse = BatteryMode.Smart; s.BatteryReserve = 0.35; s.SellSurplus = false; s.GreenNominatedDay = 12;
        SaveSystem.Save(s, "unit-test-own-energy");
        var l = SaveSystem.Load("unit-test-own-energy");
        Check(l != null && l.BatteryUse == BatteryMode.Smart && Math.Abs(l.BatteryReserve - 0.35) < 1e-9 && !l.SellSurplus && l.GreenNominatedDay == 12
              && l.EnergyLog.Count == s.EnergyLog.Count && l.EnergyHours.Count == 24 && Math.Abs(l.BatterySoc - s.BatterySoc) < 1e-6, "save roundtrip own energy");

        // ---- a full day in the game books the energy ----
        var a = Quiet();
        var before = a.EnergyLog.Count;
        a.Advance(24);
        Check(a.EnergyLog.Count == before + 1 && a.LastEnergy.Day == (int)a.Day, "every game day adds one energy record");
    }
}

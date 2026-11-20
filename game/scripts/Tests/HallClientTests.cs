using System;
using System.Linq;
using DatacenterArchitect.Sim;

namespace DatacenterArchitect.Tests;

/// <summary>Playtest: per data hall the player picks which customer types are hosted there.</summary>
public static partial class TestRunner
{
    static void HallClientTests()
    {
        var (s, hall, _, _) = OrgSite();
        for (var i = 0; i < 6; i++) Live(s, ObjKind.Rack, 6 + i, 6);
        var rng = new Random(3);
        Deal Lead(CustomerType t)
        {
            var d = s.MakeLead(0, rng, t);
            d.Product = Product.Rack; d.Racks = 2; d.Amp32 = d.AB = d.TwoN = d.Green = false; d.Carriers = 1;
            return d;
        }
        var bank = Lead(CustomerType.Hosting);
        var crypto = Lead(CustomerType.Crypto);
        Check(hall.Clients.Count == 0 && GameState.HallTakes(hall, CustomerType.Crypto), "a new hall welcomes every customer type");
        Check(s.FreeRacksFor(bank, out _).Count == 6 && s.FreeRacksFor(crypto, out _).Count == 6, "all free racks are for sale to anyone");
        s.SetHallClients(hall, new[] { CustomerType.Hosting, CustomerType.WebHosting });
        Check(hall.Clients.Count == 2 && GameState.HallTakes(hall, CustomerType.Hosting) && !GameState.HallTakes(hall, CustomerType.Crypto), "the hall is kept for hosting companies");
        Check(s.FreeRacksFor(bank, out _).Count == 6, "hosting companies still get racks there");
        Check(s.FreeRacksFor(crypto, out _).Count == 0, "crypto miners do not");
        Check(s.Check(crypto).Any(r => r.Key == "req_hall_clients" && r.Status == ReqStatus.Fail) && !s.Meets(crypto), "the deal shows that no hall hosts this type");
        Check(!s.Check(bank).Any(r => r.Key == "req_hall_clients"), "a welcome type has no such line");
        s.Deals.Add(bank);
        Check(s.Sign(bank) == null && bank.RackIds.Count == 2, "a hosting company signs for racks in its hall");
        s.SetHallClients(hall, new[] { CustomerType.Crypto });
        Check(bank.State == DealState.Active && bank.RackIds.Count == 2, "changing the hall later does not evict the tenants already there");
        Check(s.FreeRacksFor(crypto, out _).Count == 4, "the rest of the hall now goes to crypto");
        s.SetHallClients(hall, Enum.GetValues<CustomerType>());
        Check(hall.Clients.Count == 0, "ticking every type means everyone");
        // per U follows the same rule
        s.SetZone(hall, HallZone.PerU);
        s.SetHallClients(hall, new[] { CustomerType.Smb });
        Check(s.FreeUnits(CustomerType.Startup) == 0 && s.FreeUnits(CustomerType.Smb) > 0, "per-U units only for the chosen types");
        var back = System.Text.Json.JsonSerializer.Deserialize<GameState>(System.Text.Json.JsonSerializer.Serialize(s));
        Check(back.Rooms.First(r => r.Id == hall.Id).Clients.SequenceEqual(new[] { CustomerType.Smb }), "the choice survives a save");
    }
}

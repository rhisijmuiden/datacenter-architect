using System;
using System.Linq;
using DatacenterArchitect.Sim;

namespace DatacenterArchitect.Tests;

/// <summary>Infra sketch board 7: hall codes and names, placing contracts in a hall, whole-hall tenants, sign and build.</summary>
public static partial class TestRunner
{
    static void HallNameTests()
    {
        var (s, hall, _, _) = OrgSite();
        var b = s.AddBuilding(BuildingKind.Datacenter, 4, 30, 30, 12, 1); b.Stage = Stage.Done;
        var hallB = s.AddRoom(RoomType.DataHall, 4, 30, 12, 12, 0); hallB.Stage = Stage.Done;
        var hallC = s.AddRoom(RoomType.DataHall, 16, 30, 12, 12, 0);
        Check(s.HallCode(hall) == "DH-01" && s.HallCode(hallB) == "DH-02" && s.HallCode(hallC) == "DH-03", "halls are numbered in the order they were drawn");
        Check(s.HallTitle(hallB) == "DH-02", "without a name the title is the code");
        Check(s.RenameHall(hallB, "  Private hall north\n ") == null && hallB.Name == "Private hall north" && s.HallTitle(hallB) == "DH-02 · Private hall north", "a hall gets a name");
        s.RenameHall(hallC, new string('x', 80));
        Check(hallC.Name.Length == GameState.MaxHallName, "long names are cut");
        Check(GameState.HallBand(hall) != GameState.HallBand(hallB), "halls get their own colour band");
        s.SetHallBand(hallB, 4);
        Check(GameState.HallBand(hallB) == GameState.HallBands[4], "the band can be picked");

        for (var i = 0; i < 6; i++) Live(s, ObjKind.Rack, 6 + i, 32);
        var rng = new Random(5);
        Deal Lead(CustomerType t, Product p, int racks)
        {
            var d = s.MakeLead(0, rng, t);
            d.Product = p; d.Racks = racks; d.Amp32 = d.AB = d.TwoN = d.Green = d.Security = false; d.Carriers = 1; d.Gbps = 0;
            s.Deals.Add(d);
            return d;
        }
        var cloud = Lead(CustomerType.Cloud, Product.Hall, 4);
        Check(s.FreeRacksFor(cloud, out _).Count == 0 && !s.Meets(cloud), "no hall is sold as a private hall yet");
        var choices = s.HallChoices(cloud);
        Check(choices.Count == 3 && choices.First(c => c.Hall == hallB).Problem == null, "the place list offers DH-02, which fits");
        Check(choices.First(c => c.Hall == hall).Free == 0 && choices.First(c => c.Hall == hallC).Problem == "place_building", "and says why the others do not");
        Check(s.PlaceDeal(cloud, hallB.Id) == null && cloud.HallIds.SequenceEqual(new[] { hallB.Id }), "the contract is placed in DH-02");
        Check(s.Check(cloud).Any(r => r.Key == "req_place" && r.Have == "DH-02" && r.Status == ReqStatus.Ok) && s.Meets(cloud), "the picked hall meets the request");
        Check(s.Sign(cloud) == null && cloud.RackIds.Count == 4, "signed in the picked hall");
        Check(hallB.Zone == HallZone.PrivateHall, "the hall is now sold as a private hall (set from the contract)");
        Check(s.HallTenant(hallB) == cloud && s.DealHalls(cloud).SequenceEqual(new[] { hallB }), "the customer rents the whole hall");

        var racks = Lead(CustomerType.Hosting, Product.Rack, 1);
        Check(s.PlaceDeal(racks, hallB.Id) == "msg_place_taken", "another customer cannot be placed in a rented hall");
        Check(s.FreeRacksFor(racks, out _).Count == 0, "nor gets its free racks automatically");

        // a customer asks for an own hall that is not built yet
        var bank = Lead(CustomerType.Cloud, Product.Hall, 4); // a bank would also want certificates
        Check(s.CanSign(bank) != null && s.CanSignAndBuild(bank) == "msg_pick_hall", "sign and build needs a picked hall");
        Check(s.PlaceDeal(bank, hallC.Id) == null && s.CanSignAndBuild(bank) == null && s.SignAndBuild(bank) == null, "sign now, build DH-03 for the bank");
        Check(bank.State == DealState.Building && bank.DeadlineDay > s.Day && s.HallTenant(hallC) == bank && hallC.Zone == HallZone.PrivateHall, "DH-03 is reserved for the bank");
        var other = Lead(CustomerType.Cloud, Product.Hall, 4);
        Check(s.HallChoices(other).First(c => c.Hall == hallC).Problem == "place_taken", "a reserved hall is not offered to others");
        hallC.Stage = Stage.Done;
        for (var i = 0; i < 5; i++) Live(s, ObjKind.Rack, 18 + i, 32);
        s.OrgDay(1);
        Check(bank.State == DealState.Active && bank.RackIds.Count == 4 && s.DealHalls(bank).SequenceEqual(new[] { hallC }), "the bank moves in once its hall is ready");

        // a customer bigger than one hall is spread over two halls (Reinder: a contract must never get stuck on hall size)
        var b2 = s.AddBuilding(BuildingKind.Datacenter, 4, 46, 26, 12, 1); b2.Stage = Stage.Done;
        var hallD = s.AddRoom(RoomType.DataHall, 4, 46, 12, 12, 0); hallD.Stage = Stage.Done;
        var hallE = s.AddRoom(RoomType.DataHall, 16, 46, 12, 12, 0); hallE.Stage = Stage.Done;
        for (var i = 0; i < 3; i++) { Live(s, ObjKind.Rack, 6 + i, 48); Live(s, ObjKind.Rack, 18 + i, 48); }
        var big = Lead(CustomerType.Cloud, Product.Hall, 5);
        Check(s.PlaceDeal(big, hallD.Id) == null && !s.Meets(big) && s.PickedFree(big) == 3, "one hall of 3 racks is too small for 5");
        Check(s.PlaceDeal(big, hallE.Id) == null && big.HallIds.Count == 2 && s.PickedFree(big) == 6, "a second hall is added: 6 racks free together");
        Check(s.Meets(big) && s.Sign(big) == null && big.RackIds.Count == 5, "the customer signs for 5 racks over two halls");
        Check(s.HallTenant(hallD) == big && s.HallTenant(hallE) == big && hallD.Zone == HallZone.PrivateHall && hallE.Zone == HallZone.PrivateHall, "both halls are its own, the spare rack is room to grow");
        Check(s.DealHalls(big).Count == 2, "the contract lists both halls");
        var cage = Lead(CustomerType.Cloud, Product.Cage, 2);
        s.PlaceDeal(cage, hall.Id);
        Check(s.PlaceDeal(cage, hallD.Id) == "msg_place_taken" && cage.HallIds.SequenceEqual(new[] { hall.Id }), "a cage stays in one hall, and not in a rented one");

        // saves and older games
        var back = System.Text.Json.JsonSerializer.Deserialize<GameState>(System.Text.Json.JsonSerializer.Serialize(s));
        var bb = back.Rooms.First(r => r.Id == hallB.Id);
        Check(bb.Name == "Private hall north" && bb.HallNo == hallB.HallNo && bb.Band == 4 && back.DealById(cloud.Id).HallIds.SequenceEqual(new[] { hallB.Id }), "names, bands and placements survive a save");
        s.Rooms.Remove(hallB);
        Check(s.HallCode(hallC) == "DH-03", "codes stay when another hall goes");
        var d4 = s.AddRoom(RoomType.DataHall, 4, 30, 12, 12, 0);
        Check(s.HallCode(d4) == "DH-06", "a new hall gets the next number");
        foreach (var r in back.Rooms) r.HallNo = 0;
        back.Migrate();
        Check(back.HallCode(back.Rooms.First(r => r.Id == hall.Id)) == "DH-01" && back.HallCode(back.Rooms.First(r => r.Id == hallC.Id)) == "DH-03", "older games number their halls in drawing order");
    }
}

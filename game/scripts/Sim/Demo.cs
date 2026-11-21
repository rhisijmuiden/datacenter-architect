using System.Linq;

namespace DatacenterArchitect.Sim;

/// <summary>An example campus shown behind the title screen and used for screenshots and tests.</summary>
public static class Demo
{
    public static GameState Build()
    {
        var o = new NewGameOptions { Company = "Polder Datacenters", Creative = true, CreativeOptions = new CreativeOptions { UnlimitedMoney = true, AllUnlocks = true } };
        var s = GameState.New(o);
        s.Builders = 6; s.Fitters = 3; s.Engineers = 2;

        // driveway from the public road, apron in front of the dock, access road to the yard, parking
        s.PaintSurface(28, 34, 3, 30, Surface.Asphalt, out _);
        s.PaintSurface(31, 50, 15, 2, Surface.Asphalt, out _);
        s.PaintSurface(46, 44, 8, 8, Surface.Yard, out _);
        s.PaintSurface(33, 40, 10, 8, Surface.Parking, out _);
        s.PaintSurface(31, 42, 2, 4, Surface.Asphalt, out _); // the car park's entrance from the driveway
        s.PaintSurface(14, 34, 14, 1, Surface.Footpath, out _);
        s.PaintSurface(31, 36, 2, 1, Surface.Footpath, out _);
        s.PaintSurface(38, 28, 2, 12, Surface.Footpath, out _);
        s.PaintSurface(5, 58, 23, 1, Surface.BikePath, out _);

        // tall steel fence with a sliding gate on the driveway, a barrier and the guard booth
        s.PlaceFence(3, 6, 54, 55, FenceType.Palisade, out _);
        s.AddObject(ObjKind.SlidingGate, 28, 60, 0, 0);
        s.AddObject(ObjKind.Barrier, 28, 56, 0, 0);
        s.AddObject(ObjKind.GuardBooth, 31, 56, 0, 0);
        s.AddObject(ObjKind.Intercom, 27, 62, 0, 0);
        // two fiber pits on different carrier pits in the street: two separate routes
        var pit = s.AddObject(ObjKind.FiberPit, 24, 62, 0, 0);
        s.AddObject(ObjKind.FiberPit, 50, 62, 0, 0);
        pit.Level = 2;

        // the datacenter: a closed concrete box with a hall, a corridor and the loading dock
        var dc = s.AddBuilding(BuildingKind.Datacenter, 8, 14, 26, 20, 1);
        var hall = s.AddRoom(RoomType.DataHall, 8, 14, 18, 20, 0);
        s.AddRoom(RoomType.Corridor, 26, 18, 8, 4, 0);
        s.AddRoom(RoomType.Corridor, 26, 22, 4, 4, 0);
        // the meet-me room: patch frames (ODFs) where the carriers come in, and two core routers
        s.AddRoom(RoomType.MeetMe, 30, 22, 4, 4, 0);
        s.AddDoor(DoorType.Double, 27, 22, 1, 0);
        s.AddDoor(DoorType.Badge, 31, 22, 1, 0);
        s.AddObject(ObjKind.Odf, 30, 25, 0, 0);
        s.AddObject(ObjKind.Odf, 31, 25, 0, 0);
        s.AddObject(ObjKind.CoreRouter, 33, 22, 3, 0);
        s.AddObject(ObjKind.CoreRouter, 33, 24, 3, 0);
        // the distribution switch: every hall switch hangs on it, it takes one port on the core
        s.AddObject(ObjKind.DistSwitch, 32, 25, 0, 0);
        // a carrier rack per carrier and internet exchange
        s.AddObject(ObjKind.CarrierRack, 30, 23, 0, 0);
        s.AddObject(ObjKind.CarrierRack, 32, 23, 0, 0);
        s.AddObject(ObjKind.CarrierRack, 32, 24, 0, 0);
        // technical rooms: main switchgear with the ATS, and the UPS room with its batteries
        s.AddRoom(RoomType.Switchgear, 26, 14, 4, 4, 0);
        s.AddRoom(RoomType.UpsRoom, 30, 14, 4, 4, 0);
        s.AddDoor(DoorType.Fire, 27, 18, 1, 0);
        s.AddDoor(DoorType.Double, 31, 18, 1, 0);
        s.AddObject(ObjKind.Switchgear, 26, 14, 0, 0);
        s.AddObject(ObjKind.Switchgear, 28, 14, 0, 0);
        s.AddObject(ObjKind.Ups, 30, 14, 0, 0);
        s.AddObject(ObjKind.Ups, 32, 14, 0, 0);
        for (var x = 30; x < 34; x++) s.AddObject(ObjKind.Battery, x, 16, 0, 0);
        s.AddRoom(RoomType.LoadingDock, 26, 26, 8, 8, 0);
        s.AddDoor(DoorType.RollUp, 28, 34, 1, 0);
        s.AddDoor(DoorType.Double, 28, 26, 1, 0);
        s.AddDoor(DoorType.Double, 26, 18, 0, 0);
        s.AddDoor(DoorType.Badge, 14, 34, 1, 0);
        // rows face each other across a cold aisle, backs face each other across a hot aisle; every rack is reachable front and back
        foreach (var (z, rot) in new[] { (15, 0), (17, 2), (20, 0), (22, 2), (25, 0), (27, 2), (30, 0) })
            for (var x = 12; x < 25; x++) s.AddObject(ObjKind.Rack, x, z, rot, 0);
        for (var z = 15; z < 32; z += 4) s.AddObject(ObjKind.Crah, 9, z, 0, 0);
        // floor PDUs at the end of the rows; the hall also has an overhead busbar
        foreach (var z in new[] { 15, 22, 27 }) s.AddObject(ObjKind.Pdu, 25, z, 0, 0);
        hall.Busbar = true;
        // hall switches at the start of the rows: a Spine 400 and two Edge 48s, so every rack has an A and a B port
        s.AddObject(ObjKind.Spine, 11, 16, 1, 0);
        foreach (var z in new[] { 23, 28 }) s.AddObject(ObjKind.EdgeSwitch48, 11, z, 1, 0);
        hall.Containment = Containment.HotCold;

        s.AddObject(ObjKind.CrashCart, 24, 17, 0, 0);

        // office in brick with two floors, stairs and a passenger lift, reception, a customer lounge and the director's office
        var office = s.AddBuilding(BuildingKind.Office, 38, 12, 16, 14, 2, Facade.Brick);
        s.AddRoom(RoomType.Reception, 38, 20, 8, 6, 0);
        s.AddRoom(RoomType.Office, 38, 12, 8, 8, 0);
        s.AddRoom(RoomType.Lounge, 46, 12, 8, 8, 0);
        s.AddRoom(RoomType.Executive, 46, 20, 8, 6, 0);
        s.AddDoor(DoorType.Double, 40, 26, 1, 0);
        s.AddDoor(DoorType.Single, 41, 20, 1, 0);
        s.AddDoor(DoorType.Single, 46, 22, 0, 0);
        s.AddDoor(DoorType.Single, 46, 16, 0, 0);
        s.AddObject(ObjKind.Desk, 48, 22, 2, 0);
        s.AddObject(ObjKind.Chair, 49, 21, 0, 0);
        s.AddObject(ObjKind.Plant, 52, 24, 0, 0);
        s.AddObject(ObjKind.Cabinet, 52, 20, 0, 0);
        s.AddObject(ObjKind.Sofa, 48, 18, 2, 0);
        s.AddObject(ObjKind.Coffee, 52, 13, 0, 0);
        s.AddObject(ObjKind.Vending, 53, 15, 0, 0);
        s.AddObject(ObjKind.Terminal, 47, 13, 0, 0);
        for (var i = 0; i < 3; i++) { s.AddObject(ObjKind.Desk, 39 + i * 2 % 6, 13 + i * 2, 0, 0); s.AddObject(ObjKind.Chair, 39 + i * 2 % 6, 14 + i * 2, 2, 0); }
        s.AddObject(ObjKind.MeetingTable, 39, 22, 0, 0);
        // a passenger lift in the reception and stairs in the director's office take people upstairs
        s.AddObject(ObjKind.Stairs, 50, 21, 0, 0);
        s.AddObject(ObjKind.PassengerLift, 42, 20, 0, 0);
        // network operations centre upstairs, with a video wall
        s.AddRoom(RoomType.Noc, 38, 12, 8, 6, 1);
        s.AddDoor(DoorType.Badge, 41, 18, 1, 1);
        s.AddObject(ObjKind.NocWall, 40, 12, 0, 1);
        s.AddObject(ObjKind.Desk, 39, 15, 2, 1);
        s.AddObject(ObjKind.Desk, 42, 15, 2, 1);
        s.AddObject(ObjKind.Chair, 40, 16, 2, 1);
        s.AddObject(ObjKind.Chair, 43, 16, 2, 1);

        // R&D lab upstairs next to the NOC, with two lab benches
        s.AddRoom(RoomType.Lab, 46, 12, 8, 8, 1);
        s.AddDoor(DoorType.Badge, 46, 14, 0, 1);
        s.AddObject(ObjKind.LabBench, 48, 13, 0, 1);
        s.AddObject(ObjKind.LabBench, 48, 16, 0, 1);
        // cameras: on poles along the driveway, and domes in the hall
        s.AddObject(ObjKind.CameraPole, 31, 54, 0, 0);
        s.AddObject(ObjKind.CameraPole, 26, 36, 0, 0);
        s.AddObject(ObjKind.CameraDome, 10, 20, 0, 0);
        s.AddObject(ObjKind.CameraDome, 10, 29, 0, 0);

        // generators and landscaping
        for (var i = 0; i < 4; i++) s.AddObject(ObjKind.Generator, 9 + i * 4, 40, 0, 0);
        s.AddObject(ObjKind.UndergroundTank, 10, 43, 0, 0);
        s.AddObject(ObjKind.UndergroundTank, 16, 43, 0, 0);
        foreach (var x in new[] { 6, 10, 14, 18, 22, 46, 50, 54 }) s.AddObject(ObjKind.Tree, x, 8, 0, 0);
        foreach (var z in new[] { 12, 18, 24, 30, 46, 52 }) s.AddObject(ObjKind.Tree, 5, z, 0, 0);
        foreach (var z in new[] { 38, 44, 50, 54 }) s.AddObject(ObjKind.LampPost, 27, z, 0, 0);
        foreach (var x in new[] { 34, 38, 42 }) s.AddObject(ObjKind.Shrub, x, 38, 0, 0);
        s.AddObject(ObjKind.Bench, 35, 36, 0, 0);
        s.AddObject(ObjKind.BikeRack, 14, 36, 0, 0);

        foreach (var b in s.Buildings) b.Stage = Stage.Done;
        // chillers on the datacenter roof
        foreach (var x in new[] { 10, 14, 18 }) s.AddObject(ObjKind.Chiller, x, 16, 0, GameState.RoofFloor);
        foreach (var r in s.Rooms) r.Stage = Stage.Done;
        foreach (var x in s.Objects) x.Status = ObjStatus.Live;
        foreach (var r in s.Rooms) r.Temp = 23;
        // a warehouse going up, and a row of racks still on its way
        var wh = s.AddBuilding(BuildingKind.Warehouse, 40, 30, 12, 8, 1);
        wh.Stage = Stage.Walls; wh.Material = 18; wh.Work = 1.4;
        for (var x = 12; x < 25; x++) s.AddObject(ObjKind.Rack, x, 32, 2, 0);
        // carriers, the internet exchange and peering with a rival
        s.Contracts.Add(new CarrierContract { Carrier = 1, PortGbps = 100, Commit = 10 });
        s.Contracts.Add(new CarrierContract { Carrier = 3, PortGbps = 10, Commit = 5 });
        s.Contracts.Add(new CarrierContract { Carrier = 4, PortGbps = 10, Ports = 2, Commit = 0 });
        s.Peers.Add(new PeeringDeal { Rival = 0, State = PeerState.Active, PortGbps = 10, Granted = 5 });
        s.Peers.Add(new PeeringDeal { Rival = 1, State = PeerState.Requested, PortGbps = 10, Granted = 2 });
        s.CrossConnects = 14; s.XcPending = 2;
        // the organisation, customers on contract, open requests and a tender
        foreach (var e in new[] { Exec.Ceo, Exec.Cfo, Exec.Hr, Exec.Sales, Exec.Legal, Exec.Operations, Exec.Facility, Exec.Network })
            s.Execs.Add(new ExecHire { Role = e, Training = e is Exec.Facility or Exec.Sales ? 1 : 0 });
        s.SalesReps = 3; s.Reputation = 46; s.Seed = 7;
        var rng = new System.Random(5);
        var racks = s.Objects.Where(o => o.Kind == ObjKind.Rack && o.Status == ObjStatus.Live).Select(o => o.Id).ToList();
        var taken = 0;
        foreach (var (type, n) in new[] { (CustomerType.Hosting, 8), (CustomerType.Streaming, 12), (CustomerType.Municipality, 2), (CustomerType.Smb, 3) })
        {
            if (taken + n > racks.Count) break;
            var d = s.MakeLead(0, rng, type);
            d.Product = Product.Rack; d.Racks = n; d.Monthly = Org.ListPrice(d);
            d.RackIds = racks.Skip(taken).Take(n).ToList(); taken += n;
            d.State = DealState.Active; d.StartDay = 2; d.EndDay = 2 + d.Months * 30; d.Satisfaction = 70 + rng.Next(30);
            s.Deals.Add(d);
        }
        for (var i = 0; i < 3; i++) s.Deals.Add(s.MakeLead(12, rng));
        s.NewTender(12, rng); s.NextTenderDay = 40;
        s.MaterialStock = 40;
        s.Hours = 24 * 12 + 9;
        s.Touch();
        _ = dc; _ = hall; _ = office;
        return s;
    }
}

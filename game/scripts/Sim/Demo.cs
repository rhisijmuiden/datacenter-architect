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
        s.AddObject(ObjKind.FiberPit, 24, 62, 0, 0);

        // the datacenter: a closed concrete box with a hall, a corridor and the loading dock
        var dc = s.AddBuilding(BuildingKind.Datacenter, 8, 14, 26, 20, 1);
        var hall = s.AddRoom(RoomType.DataHall, 8, 14, 18, 20, 0);
        s.AddRoom(RoomType.Corridor, 26, 14, 8, 12, 0);
        s.AddRoom(RoomType.LoadingDock, 26, 26, 8, 8, 0);
        s.AddDoor(DoorType.RollUp, 28, 34, 1, 0);
        s.AddDoor(DoorType.Double, 28, 26, 1, 0);
        s.AddDoor(DoorType.Double, 26, 18, 0, 0);
        s.AddDoor(DoorType.Badge, 14, 34, 1, 0);
        // rows face each other across a cold aisle, backs face each other across a hot aisle
        foreach (var (z, rot) in new[] { (16, 0), (18, 2), (21, 0), (23, 2), (26, 0), (28, 2), (31, 0) })
            for (var x = 12; x < 25; x++) s.AddObject(ObjKind.Rack, x, z, rot, 0);
        for (var z = 15; z < 32; z += 4) s.AddObject(ObjKind.Crah, 9, z, 0, 0);
        s.AddObject(ObjKind.CrashCart, 24, 17, 0, 0);

        // office in brick with two floors, a lift, reception, a customer lounge and the director's office
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

        // generators and landscaping
        for (var i = 0; i < 4; i++) s.AddObject(ObjKind.Generator, 9 + i * 4, 40, 0, 0);
        foreach (var x in new[] { 6, 10, 14, 18, 22, 46, 50, 54 }) s.AddObject(ObjKind.Tree, x, 8, 0, 0);
        foreach (var z in new[] { 12, 18, 24, 30, 46, 52 }) s.AddObject(ObjKind.Tree, 5, z, 0, 0);
        foreach (var z in new[] { 38, 44, 50, 54 }) s.AddObject(ObjKind.LampPost, 27, z, 0, 0);
        foreach (var x in new[] { 34, 38, 42 }) s.AddObject(ObjKind.Shrub, x, 38, 0, 0);
        s.AddObject(ObjKind.Bench, 35, 36, 0, 0);
        s.AddObject(ObjKind.BikeRack, 14, 36, 0, 0);

        foreach (var b in s.Buildings) b.Stage = Stage.Done;
        foreach (var r in s.Rooms) r.Stage = Stage.Done;
        foreach (var x in s.Objects) x.Status = ObjStatus.Live;
        // a warehouse going up, and a row of racks still on its way
        var wh = s.AddBuilding(BuildingKind.Warehouse, 40, 30, 12, 8, 1);
        wh.Stage = Stage.Walls; wh.Material = 18; wh.Work = 1.4;
        for (var x = 12; x < 25; x++) s.AddObject(ObjKind.Rack, x, 33 - 1, 2, 0);
        s.MaterialStock = 40;
        s.Hours = 24 * 12 + 9;
        s.Touch();
        _ = dc; _ = hall; _ = office;
        return s;
    }
}

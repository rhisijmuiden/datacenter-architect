using System.Collections.Generic;

namespace DatacenterArchitect.Sim;

/// <summary>Ground cover painted on the site. Trucks can drive on asphalt, parking and the materials yard.</summary>
public enum Surface : byte { Grass, Asphalt, Parking, Footpath, BikePath, Yard }

public enum FenceType : byte { None, Mesh, Palisade, Welded, Wall, Electric }

public enum BuildingKind { Datacenter, Office, Warehouse }

/// <summary>Outer wall finish. Offices choose brick, concrete look or glass; a datacenter is a closed concrete box.</summary>
public enum Facade { Concrete, Brick, Glass, Cladding }

/// <summary>Construction stage that is being worked on. Done means finished.</summary>
public enum Stage { Foundation, Walls, Roof, Done }

public enum RoomType { DataHall, MeetMe, UpsRoom, Office, Executive, Meeting, Canteen, Lounge, Reception, Security, LoadingDock, Storage, Corridor }

public enum DoorType { Single, Double, Fire, Badge, RollUp }

public enum ObjKind
{
    Rack, Crah, Generator, FiberPit, Lift,
    Desk, Chair, Plant, Cabinet, MeetingTable,
    Vending, Microwave, Coffee, Sofa, Terminal, CrashCart,
    Tree, Shrub, LampPost, Bench, BikeRack,
    GuardBooth, Intercom, Barrier, SlidingGate, PedGate,
}

/// <summary>Delivery chain for hall equipment: truck to the dock, fitter places it, engineer connects power and then fiber.</summary>
public enum ObjStatus { Ordered, Delivered, Placed, Powered, Live }

/// <summary>Where an object may stand.</summary>
public enum Where { Outside, Room, Hall, Building, Fence, Paved }

public record ObjDef(ObjKind Kind, string NameKey, int W, int D, long Cost, Where Where, bool Blocks,
    double PowerKw = 0, double CoolingKw = 0, double GenKw = 0, bool Chain = false, bool NeedsFiber = false);

public record SurfaceDef(Surface Surface, string NameKey, long Cost, bool Trucks);
public record FenceDef(FenceType Type, string NameKey, long Cost, float Height, int Security, bool NeedsResearch);
public record BuildingDef(BuildingKind Kind, string NameKey, long CostPerCellPerFloor);
public record RoomDef(RoomType Type, string NameKey, string Color);
public record DoorDef(DoorType Type, string NameKey, int Width, long Cost, bool Wide);

public static class Catalog
{
    public const int MaxFloors = 4;
    public const int MaxRow = 24;

    public static readonly Dictionary<ObjKind, ObjDef> Objects = new()
    {
        [ObjKind.Rack] = new(ObjKind.Rack, "obj_rack", 1, 1, 4200, Where.Hall, true, PowerKw: 6, Chain: true, NeedsFiber: true),
        [ObjKind.Crah] = new(ObjKind.Crah, "obj_crah", 1, 2, 38000, Where.Hall, true, PowerKw: 4, CoolingKw: 120, Chain: true),
        [ObjKind.Generator] = new(ObjKind.Generator, "obj_generator", 3, 2, 410000, Where.Outside, true, GenKw: 1000),
        [ObjKind.FiberPit] = new(ObjKind.FiberPit, "obj_fiberpit", 1, 1, 12000, Where.Outside, false),
        [ObjKind.Lift] = new(ObjKind.Lift, "obj_lift", 2, 2, 85000, Where.Building, false),
        [ObjKind.Desk] = new(ObjKind.Desk, "obj_desk", 2, 1, 900, Where.Room, true),
        [ObjKind.Chair] = new(ObjKind.Chair, "obj_chair", 1, 1, 250, Where.Room, false),
        [ObjKind.Plant] = new(ObjKind.Plant, "obj_plant", 1, 1, 80, Where.Room, true),
        [ObjKind.Cabinet] = new(ObjKind.Cabinet, "obj_cabinet", 1, 1, 400, Where.Room, true),
        [ObjKind.MeetingTable] = new(ObjKind.MeetingTable, "obj_meeting_table", 3, 2, 2200, Where.Room, true),
        [ObjKind.Vending] = new(ObjKind.Vending, "obj_vending", 1, 1, 3800, Where.Room, true, PowerKw: 0.4),
        [ObjKind.Microwave] = new(ObjKind.Microwave, "obj_microwave", 1, 1, 650, Where.Room, true),
        [ObjKind.Coffee] = new(ObjKind.Coffee, "obj_coffee", 1, 1, 2900, Where.Room, true, PowerKw: 0.3),
        [ObjKind.Sofa] = new(ObjKind.Sofa, "obj_sofa", 2, 1, 1600, Where.Room, true),
        [ObjKind.Terminal] = new(ObjKind.Terminal, "obj_terminal", 1, 1, 2400, Where.Room, true, PowerKw: 0.2),
        [ObjKind.CrashCart] = new(ObjKind.CrashCart, "obj_crashcart", 1, 1, 1900, Where.Hall, false),
        [ObjKind.Tree] = new(ObjKind.Tree, "obj_tree", 1, 1, 350, Where.Outside, true),
        [ObjKind.Shrub] = new(ObjKind.Shrub, "obj_shrub", 1, 1, 60, Where.Outside, true),
        [ObjKind.LampPost] = new(ObjKind.LampPost, "obj_lamp", 1, 1, 900, Where.Outside, false),
        [ObjKind.Bench] = new(ObjKind.Bench, "obj_bench", 2, 1, 450, Where.Outside, true),
        [ObjKind.BikeRack] = new(ObjKind.BikeRack, "obj_bikerack", 2, 1, 600, Where.Outside, true),
        [ObjKind.GuardBooth] = new(ObjKind.GuardBooth, "obj_guardbooth", 2, 2, 28000, Where.Outside, true),
        [ObjKind.Intercom] = new(ObjKind.Intercom, "obj_intercom", 1, 1, 3500, Where.Outside, false),
        [ObjKind.Barrier] = new(ObjKind.Barrier, "obj_barrier", 3, 1, 6500, Where.Paved, false),
        [ObjKind.SlidingGate] = new(ObjKind.SlidingGate, "obj_sliding_gate", 3, 1, 18000, Where.Fence, false),
        [ObjKind.PedGate] = new(ObjKind.PedGate, "obj_ped_gate", 1, 1, 2500, Where.Fence, false),
    };

    public static readonly Dictionary<Surface, SurfaceDef> Surfaces = new()
    {
        [Surface.Grass] = new(Surface.Grass, "surf_grass", 5, false),
        [Surface.Asphalt] = new(Surface.Asphalt, "surf_asphalt", 60, true),
        [Surface.Parking] = new(Surface.Parking, "surf_parking", 70, true),
        [Surface.Footpath] = new(Surface.Footpath, "surf_footpath", 35, false),
        [Surface.BikePath] = new(Surface.BikePath, "surf_bikepath", 40, false),
        [Surface.Yard] = new(Surface.Yard, "surf_yard", 25, true),
    };

    public static readonly Dictionary<FenceType, FenceDef> Fences = new()
    {
        [FenceType.Mesh] = new(FenceType.Mesh, "fence_mesh", 80, 1.8f, 1, false),
        [FenceType.Welded] = new(FenceType.Welded, "fence_welded", 140, 2.4f, 2, false),
        [FenceType.Palisade] = new(FenceType.Palisade, "fence_palisade", 180, 2.8f, 3, false),
        [FenceType.Wall] = new(FenceType.Wall, "fence_wall", 260, 2.6f, 3, false),
        [FenceType.Electric] = new(FenceType.Electric, "fence_electric", 420, 3.0f, 4, true),
    };

    public static readonly Dictionary<BuildingKind, BuildingDef> Buildings = new()
    {
        [BuildingKind.Datacenter] = new(BuildingKind.Datacenter, "bld_datacenter", 1400),
        [BuildingKind.Office] = new(BuildingKind.Office, "bld_office", 1100),
        [BuildingKind.Warehouse] = new(BuildingKind.Warehouse, "bld_warehouse", 700),
    };

    public static readonly Dictionary<RoomType, RoomDef> Rooms = new()
    {
        [RoomType.DataHall] = new(RoomType.DataHall, "room_datahall", "#9AA5AE"),
        [RoomType.MeetMe] = new(RoomType.MeetMe, "room_meetme", "#C99A7A"),
        [RoomType.UpsRoom] = new(RoomType.UpsRoom, "room_ups", "#C9B27A"),
        [RoomType.Office] = new(RoomType.Office, "room_office", "#B7C4A3"),
        [RoomType.Executive] = new(RoomType.Executive, "room_executive", "#A88F73"),
        [RoomType.Meeting] = new(RoomType.Meeting, "room_meeting", "#A3B6C4"),
        [RoomType.Canteen] = new(RoomType.Canteen, "room_canteen", "#D2B48C"),
        [RoomType.Lounge] = new(RoomType.Lounge, "room_lounge", "#C7B79E"),
        [RoomType.Reception] = new(RoomType.Reception, "room_reception", "#C8C2B6"),
        [RoomType.Security] = new(RoomType.Security, "room_security", "#8E9BB5"),
        [RoomType.LoadingDock] = new(RoomType.LoadingDock, "room_dock", "#B0A89A"),
        [RoomType.Storage] = new(RoomType.Storage, "room_storage", "#ABA397"),
        [RoomType.Corridor] = new(RoomType.Corridor, "room_corridor", "#BFC5C9"),
    };

    public static readonly Dictionary<DoorType, DoorDef> Doors = new()
    {
        [DoorType.Single] = new(DoorType.Single, "door_single", 1, 600, false),
        [DoorType.Double] = new(DoorType.Double, "door_double", 2, 1400, true),
        [DoorType.Fire] = new(DoorType.Fire, "door_fire", 1, 1800, false),
        [DoorType.Badge] = new(DoorType.Badge, "door_badge", 1, 3200, false),
        [DoorType.RollUp] = new(DoorType.RollUp, "door_rollup", 3, 5200, true),
    };

    public static readonly Dictionary<Role, long> WagePerDay = new()
    {
        [Role.Builder] = 280, [Role.Fitter] = 260, [Role.Engineer] = 340,
    };

    public static long RoomCost(int w, int d) => (2 * (w + d)) * 300L + w * d * 50L;
}

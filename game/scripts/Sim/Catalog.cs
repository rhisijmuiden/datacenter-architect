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

public enum RoomType { DataHall, MeetMe, UpsRoom, Office, Executive, Meeting, Canteen, Lounge, Reception, Security, LoadingDock, Storage, Corridor, Transformer, Switchgear, Battery, Noc, Lab, Soc, Warehouse }

public enum DoorType { Single, Double, Fire, Badge, RollUp, Mantrap }

public enum ObjKind
{
    Rack, Crah, Generator, FiberPit, Lift,
    Desk, Chair, Plant, Cabinet, MeetingTable,
    Vending, Microwave, Coffee, Sofa, Terminal, CrashCart,
    Tree, Shrub, LampPost, Bench, BikeRack,
    GuardBooth, Intercom, Barrier, SlidingGate, PedGate,
    // power and cooling (step 4)
    Transformer, Switchgear, Ups, Battery, Pdu, DieselTank, UndergroundTank, Chiller, CoolingTower,
    SlidingGateLarge,
    // network (step 5)
    Odf, CoreRouter, Spine, NocWall,
    // security and research (step 8)
    CameraPole, CameraDome, SecurityWall, GlassLodge, LabBench, Bollard,
    // growth (step 9): own energy, dry coolers, the warehouse and the SOC
    SolarPanel, WindTurbine, BatteryContainer, DryCooler, PalletRack, Workbench, PalletJack, Shredder, SocDesk,
    // playtest: people move between floors by stairs or a passenger lift
    PassengerLift, Stairs,
    // outdoor seating (3D models round)
    ParkBench, PicnicTable, TerraceSet,
    // playtest: commuting by bike or electric car
    BikeShed, EvCharger,
    // power distribution (infra sketch board 2): small and large floor PDUs next to the PDU 150
    Pdu75, Pdu300,
    // network tree: edge switches in the halls and the distribution switch in the MMR.
    // The values leave room for Pdu75 and Pdu300 of the power tree, so saves keep their kinds whichever lands first.
    EdgeSwitch24 = EvCharger + 3, EdgeSwitch48, DistSwitch,
}

/// <summary>Delivery chain for hall equipment: truck to the dock, fitter places it, engineer connects power and then fiber.</summary>
public enum ObjStatus { Ordered, Delivered, Placed, Powered, Live }

/// <summary>Where an object may stand.</summary>
public enum Where { Outside, Room, Hall, Building, Fence, Paved, OutsideOrRoof, Underground }

/// <summary>Links of the power chain, from the grid operator to the racks.</summary>
public enum PowerLink { Grid, Transformers, Switchgear, Ups, Distribution }

/// <summary>The A or B side of the power distribution. Racks fed from both sides keep running when one side fails.</summary>
public enum FeedSide : byte { A, B }

/// <summary>Hall air management, upgraded step by step: more effective cooling and a lower PUE.</summary>
public enum Containment { Open, HotCold, ColdCorridor }

public record ObjDef(ObjKind Kind, string NameKey, int W, int D, long Cost, Where Where, bool Blocks,
    double PowerKw = 0, double CoolingKw = 0, double GenKw = 0, bool Chain = false, bool NeedsFiber = false,
    double CapacityKw = 0, double StorageKwh = 0, double FuelLitres = 0, double RejectKw = 0,
    RoomType[] Rooms = null, int MaxLevel = 1, int Ports = 0, double Gbps = 0,
    int Outlets = 0, int Outputs = 0, int FeederAmps = 0);

/// <summary>Grid connection levels offered by the grid operator. Level 0 is the compact station every site starts with.</summary>
public record GridLevelDef(int Level, double Mva, long Cost, int LeadDays, string NameKey);

/// <summary>Fictional energy suppliers. Variable contracts follow the market price.</summary>
public record SupplierDef(int Id, string Name, double PricePerKwh, bool Green, bool Variable, int Years, string DescKey);

/// <summary>A fictional carrier selling IP transit (or the internet exchange) at one of the street pits.</summary>
public record CarrierDef(int Id, string Name, int Pit, double PricePerMbit, bool Ix, string DescKey);
public record PortDef(int Gbps, long MonthlyFee, long SetupFee);
/// <summary>Rival datacenter: the peak traffic (Gbit/s) you need before it accepts your proposal, the share of your traffic the peering carries,
/// setup cost, monthly money (+ they rent a rack in your MMR, - you pay them because they are bigger), and the game day it asks to peer.</summary>
public record RivalDef(int Id, string Name, double MinGbps, double Share, long SetupCost, long Monthly, int RequestDay);

public record ContainmentDef(Containment Level, string NameKey, double CoolingBonus, double Pue, long CostPerM2, double Hours);

public record SurfaceDef(Surface Surface, string NameKey, long Cost, bool Trucks);
public record FenceDef(FenceType Type, string NameKey, long Cost, float Height, int Security, bool NeedsResearch);
public record BuildingDef(BuildingKind Kind, string NameKey, long CostPerCellPerFloor);
public record RoomDef(RoomType Type, string NameKey, string Color);
public record DoorDef(DoorType Type, string NameKey, int Width, long Cost, bool Wide);

public static class Catalog
{
    /// <summary>Gates in a fence that vehicles drive through.</summary>
    public static bool IsVehicleGate(ObjKind k) => k is ObjKind.SlidingGate or ObjKind.SlidingGateLarge;
    /// <summary>Any gate in a fence, for vehicles or people.</summary>
    /// <summary>Things that link all floors of a building: the freight lift, the passenger lift and stairs.</summary>
    public static bool IsShaft(ObjKind k) => k is ObjKind.Lift or ObjKind.PassengerLift or ObjKind.Stairs;

    /// <summary>Whether people moving this way may use a floor link: carried loads need the freight lift.</summary>
    public static bool ShaftAllows(ObjKind k, NavMode m) => k == ObjKind.Lift || (m == NavMode.Walk && IsShaft(k));

    /// <summary>Floor PDUs of every size.</summary>
    public static bool IsPdu(ObjKind k) => k is ObjKind.Pdu or ObjKind.Pdu75 or ObjKind.Pdu300;
    /// <summary>Power devices that belong to the A or the B side: main switchboards, UPS units and PDUs.</summary>
    public static bool HasFeed(ObjKind k) => k is ObjKind.Switchgear or ObjKind.Ups || IsPdu(k);
    /// <summary>The PDU sizes in the build menu, small to large.</summary>
    public static readonly ObjKind[] PduKinds = { ObjKind.Pdu75, ObjKind.Pdu, ObjKind.Pdu300 };

    public static bool IsGate(ObjKind k) => IsVehicleGate(k) || k == ObjKind.PedGate;
    /// <summary>Things on the road that a vehicle waits for until they are open.</summary>
    public static bool IsVehicleStop(ObjKind k) => IsVehicleGate(k) || k == ObjKind.Barrier;

    public const int MaxFloors = 4;
    public const int MaxRow = 24;

    public static readonly Dictionary<ObjKind, ObjDef> Objects = new()
    {
        [ObjKind.Rack] = new(ObjKind.Rack, "obj_rack", 1, 1, 4200, Where.Hall, true, PowerKw: 6, Chain: true, NeedsFiber: true),
        [ObjKind.Crah] = new(ObjKind.Crah, "obj_crah", 1, 2, 38000, Where.Hall, true, PowerKw: 4, CoolingKw: 120, Chain: true, MaxLevel: 3),
        [ObjKind.Generator] = new(ObjKind.Generator, "obj_generator", 3, 2, 410000, Where.Outside, true, GenKw: 1000, FuelLitres: 2000),
        [ObjKind.FiberPit] = new(ObjKind.FiberPit, "obj_fiberpit", 1, 1, 25000, Where.Outside, false),
        [ObjKind.Lift] = new(ObjKind.Lift, "obj_lift", 2, 2, 85000, Where.Building, false),
        [ObjKind.PassengerLift] = new(ObjKind.PassengerLift, "obj_passenger_lift", 2, 2, 60000, Where.Building, false),
        [ObjKind.Stairs] = new(ObjKind.Stairs, "obj_stairs", 2, 5, 18000, Where.Building, false),
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
        [ObjKind.ParkBench] = new(ObjKind.ParkBench, "obj_park_bench", 2, 1, 650, Where.Outside, true),
        [ObjKind.PicnicTable] = new(ObjKind.PicnicTable, "obj_picnic_table", 2, 2, 900, Where.Outside, true),
        [ObjKind.TerraceSet] = new(ObjKind.TerraceSet, "obj_terrace_set", 2, 2, 1800, Where.OutsideOrRoof, true),
        [ObjKind.BikeShed] = new(ObjKind.BikeShed, "obj_bikeshed", 4, 2, 9500, Where.Outside, true, PowerKw: 4),
        [ObjKind.EvCharger] = new(ObjKind.EvCharger, "obj_evcharger", 1, 1, 4800, Where.Outside, true, PowerKw: 22),
        [ObjKind.GuardBooth] = new(ObjKind.GuardBooth, "obj_guardbooth", 2, 2, 28000, Where.Outside, true),
        [ObjKind.Intercom] = new(ObjKind.Intercom, "obj_intercom", 1, 1, 3500, Where.Outside, false),
        [ObjKind.Barrier] = new(ObjKind.Barrier, "obj_barrier", 3, 1, 6500, Where.Paved, false),
        [ObjKind.SlidingGate] = new(ObjKind.SlidingGate, "obj_sliding_gate", 3, 1, 18000, Where.Fence, false),
        [ObjKind.SlidingGateLarge] = new(ObjKind.SlidingGateLarge, "obj_sliding_gate_large", 6, 1, 32000, Where.Fence, false),
        [ObjKind.PedGate] = new(ObjKind.PedGate, "obj_ped_gate", 1, 1, 2500, Where.Fence, false),
        // power chain: transformer room, switchgear room, UPS and battery rooms, PDUs in the hall
        [ObjKind.Transformer] = new(ObjKind.Transformer, "obj_transformer", 2, 2, 220000, Where.Room, true, CapacityKw: 2500, Chain: true, Rooms: new[] { RoomType.Transformer }),
        [ObjKind.Switchgear] = new(ObjKind.Switchgear, "obj_switchgear", 2, 1, 120000, Where.Room, true, CapacityKw: 4000, Chain: true, Rooms: new[] { RoomType.Switchgear }, Outputs: 8),
        [ObjKind.Ups] = new(ObjKind.Ups, "obj_ups", 2, 1, 160000, Where.Room, true, CapacityKw: 500, Chain: true, Rooms: new[] { RoomType.UpsRoom }, Outputs: 6),
        [ObjKind.Battery] = new(ObjKind.Battery, "obj_battery", 1, 1, 30000, Where.Room, true, StorageKwh: 40, Chain: true, Rooms: new[] { RoomType.Battery, RoomType.UpsRoom }),
        // a floor PDU feeds only racks in its own hall: one outlet per rack, up to its kW (each rack counted at full load)
        [ObjKind.Pdu75] = new(ObjKind.Pdu75, "obj_pdu75", 1, 1, 11000, Where.Hall, true, CapacityKw: 75, Chain: true, Outlets: 12, FeederAmps: 125),
        [ObjKind.Pdu] = new(ObjKind.Pdu, "obj_pdu", 1, 1, 18000, Where.Hall, true, CapacityKw: 150, Chain: true, Outlets: 24, FeederAmps: 250),
        [ObjKind.Pdu300] = new(ObjKind.Pdu300, "obj_pdu300", 1, 1, 31000, Where.Hall, true, CapacityKw: 300, Chain: true, Outlets: 42, FeederAmps: 500),
        [ObjKind.DieselTank] = new(ObjKind.DieselTank, "obj_diesel_tank", 3, 1, 60000, Where.Outside, true, FuelLitres: 20000),
        [ObjKind.UndergroundTank] = new(ObjKind.UndergroundTank, "obj_underground_tank", 3, 2, 180000, Where.Underground, false, FuelLitres: 50000),
        // heat rejection outside or on the roof
        [ObjKind.Chiller] = new(ObjKind.Chiller, "obj_chiller", 3, 2, 260000, Where.OutsideOrRoof, true, PowerKw: 0, RejectKw: 600, MaxLevel: 3),
        [ObjKind.CoolingTower] = new(ObjKind.CoolingTower, "obj_cooling_tower", 2, 2, 340000, Where.OutsideOrRoof, true, RejectKw: 1000, MaxLevel: 3),
        // network: patch frames, core routers and distribution switches in the meet-me room, edge (or spine) switches in the halls,
        // the NOC video wall. A hall switch serves only the racks of its own hall: Ports = racks, Gbps = its uplink.
        [ObjKind.Odf] = new(ObjKind.Odf, "obj_odf", 1, 1, 9500, Where.Room, true, Chain: true, Ports: 48, Rooms: new[] { RoomType.MeetMe }),
        [ObjKind.CoreRouter] = new(ObjKind.CoreRouter, "obj_core_router", 1, 1, 180000, Where.Room, true, PowerKw: 3, Chain: true, Ports: 32, Gbps: 400, Rooms: new[] { RoomType.MeetMe }),
        [ObjKind.DistSwitch] = new(ObjKind.DistSwitch, "obj_dist_switch", 1, 1, 120000, Where.Room, true, PowerKw: 2.5, Chain: true, Ports: 64, Gbps: 800, Rooms: new[] { RoomType.MeetMe }),
        [ObjKind.EdgeSwitch24] = new(ObjKind.EdgeSwitch24, "obj_edge24", 1, 1, 28000, Where.Hall, true, PowerKw: 0.6, Chain: true, Ports: 24, Gbps: 200),
        [ObjKind.EdgeSwitch48] = new(ObjKind.EdgeSwitch48, "obj_edge48", 1, 1, 45000, Where.Hall, true, PowerKw: 1, Chain: true, Ports: 48, Gbps: 400),
        [ObjKind.Spine] = new(ObjKind.Spine, "obj_spine", 1, 1, 65000, Where.Hall, true, PowerKw: 2, Chain: true, Ports: 96, Gbps: 3200),
        [ObjKind.NocWall] = new(ObjKind.NocWall, "obj_noc_wall", 3, 1, 48000, Where.Room, true, PowerKw: 1.5, Rooms: new[] { RoomType.Noc }),
        // security: cameras on poles outside and in the halls, the video wall and glass lodge in the security room, bollards against vehicles
        [ObjKind.CameraPole] = new(ObjKind.CameraPole, "obj_camera_pole", 1, 1, 6500, Where.Outside, false, PowerKw: 0.05),
        [ObjKind.CameraDome] = new(ObjKind.CameraDome, "obj_camera_dome", 1, 1, 1800, Where.Room, false, PowerKw: 0.02, Rooms: new[] { RoomType.DataHall, RoomType.MeetMe, RoomType.Corridor }),
        [ObjKind.SecurityWall] = new(ObjKind.SecurityWall, "obj_security_wall", 3, 1, 42000, Where.Room, true, PowerKw: 1.2, Rooms: new[] { RoomType.Security }),
        [ObjKind.GlassLodge] = new(ObjKind.GlassLodge, "obj_glass_lodge", 3, 2, 36000, Where.Room, true, Rooms: new[] { RoomType.Reception, RoomType.Security }),
        [ObjKind.Bollard] = new(ObjKind.Bollard, "obj_bollard", 1, 1, 1200, Where.Outside, true),
        // research: a lab bench in the R&D lab, one researcher each
        [ObjKind.LabBench] = new(ObjKind.LabBench, "obj_lab_bench", 2, 1, 14000, Where.Room, true, PowerKw: 0.5, Rooms: new[] { RoomType.Lab }),
        // step 9: solar panels on a roof or field, a small wind turbine, a battery container that trades on the energy market,
        // dry coolers that reject heat without water, the warehouse (pallet racks, workbenches, pallet jacks, a disk shredder) and SOC desks
        [ObjKind.SolarPanel] = new(ObjKind.SolarPanel, "obj_solar", 2, 2, 26000, Where.OutsideOrRoof, false),
        [ObjKind.WindTurbine] = new(ObjKind.WindTurbine, "obj_wind", 2, 2, 380000, Where.Outside, true),
        [ObjKind.BatteryContainer] = new(ObjKind.BatteryContainer, "obj_battery_container", 3, 1, 420000, Where.Outside, true),
        [ObjKind.DryCooler] = new(ObjKind.DryCooler, "obj_dry_cooler", 2, 1, 150000, Where.OutsideOrRoof, true, RejectKw: 450, MaxLevel: 3),
        [ObjKind.PalletRack] = new(ObjKind.PalletRack, "obj_pallet_rack", 3, 1, 3200, Where.Room, true, Rooms: new[] { RoomType.Warehouse, RoomType.Storage }),
        [ObjKind.Workbench] = new(ObjKind.Workbench, "obj_workbench", 2, 1, 2800, Where.Room, true, PowerKw: 0.3, Rooms: new[] { RoomType.Warehouse, RoomType.Storage }),
        [ObjKind.PalletJack] = new(ObjKind.PalletJack, "obj_pallet_jack", 1, 1, 650, Where.Room, false, Rooms: new[] { RoomType.Warehouse, RoomType.Storage, RoomType.LoadingDock }),
        [ObjKind.Shredder] = new(ObjKind.Shredder, "obj_shredder", 1, 1, 18000, Where.Room, true, PowerKw: 1, Rooms: new[] { RoomType.Warehouse, RoomType.Storage }),
        [ObjKind.SocDesk] = new(ObjKind.SocDesk, "obj_soc_desk", 2, 1, 9000, Where.Room, true, PowerKw: 0.4, Rooms: new[] { RoomType.Soc }),
    };

    /// <summary>Capacity multiplier per upgrade level for CRAH units, chillers and cooling towers.</summary>
    public static double LevelFactor(int level) => level switch { >= 3 => 1.7, 2 => 1.35, _ => 1.0 };
    public static long UpgradeCost(ObjDef d, int level) => d.Cost * level / 2;

    public const double GridTransformerKw = 2000;   // the compact station of level 0 has its own transformer
    public const double FuelPerKwh = 0.27;          // litres of diesel per kWh at the generator
    public const double RackKw = 6;
    /// <summary>Rack power products sold to customers: 16 A and 32 A feeds at 230 V.</summary>
    public const double Amp16Kw = 3.7, Amp32Kw = 7.4;

    public static readonly GridLevelDef[] GridLevels =
    {
        new(0, 2, 0, 0, "grid_level_0"),
        new(1, 10, 1_200_000, 30, "grid_level_1"),
        new(2, 20, 2_800_000, 60, "grid_level_2"),
        new(3, 50, 7_500_000, 120, "grid_level_3"),
    };

    public static readonly SupplierDef[] Suppliers =
    {
        new(0, "Polder Energie", 0.18, false, false, 3, "supplier_fixed"),
        new(1, "Delta Stroom", 0.16, false, true, 1, "supplier_variable"),
        new(2, "Noordwind Groen", 0.21, true, false, 5, "supplier_green"),
        new(3, "Kustlicht", 0.19, true, true, 1, "supplier_green_variable"),
    };

    public static readonly Dictionary<Containment, ContainmentDef> Containments = new()
    {
        [Containment.Open] = new(Containment.Open, "hall_open", 0, 1.65, 0, 0),
        [Containment.HotCold] = new(Containment.HotCold, "hall_hotcold", 0.12, 1.50, 120, 24),
        [Containment.ColdCorridor] = new(Containment.ColdCorridor, "hall_cold_corridor", 0.35, 1.30, 380, 72),
    };

    /// <summary>Overhead busbar in a hall: two runs (A and B), each with this capacity and a tap-off box for every rack.</summary>
    public const int BusbarAmps = 630;
    public const double BusbarKw = 400;
    public const long BusbarCost = 140_000;
    public const double BusbarHours = 24;

    // ---------------- network ----------------
    /// <summary>Old carrier pits in the pavement (x positions), only kept so old land shifts still add up; ducts now start at your own cable entries.</summary>
    public static readonly int[] StreetPits = { 8, 30, 52 };
    /// <summary>Street pits sit in the pavement across the public road (plot edge + this many metres), never on the carriageway.</summary>
    public const float StreetPitOffset = 7.8f;
    public static string PitName(int pit) => ((char)('A' + pit)).ToString();
    /// <summary>Your own main fiber duct comes as a 4-, 7- or 12-way duct: that many subducts, one per carrier, IX port or dark fiber.</summary>
    public static readonly int[] DuctWays = { 4, 7, 12 };
    public static long DuctCostPerM(int ways) => ways >= 12 ? 180 : ways >= 7 ? 130 : 90;
    public const int FibersPerCable = 144;
    /// <summary>Port sizes in Gbit/s, with the monthly port fee and the one-off setup fee.</summary>
    public static readonly PortDef[] Ports =
    {
        new(1, 120, 1500), new(10, 450, 4000), new(100, 1600, 12000), new(400, 4200, 30000),
    };
    /// <summary>Commit steps in Gbit/s.</summary>
    public static readonly double[] Commits = { 0.5, 1, 2, 5, 10, 20, 50, 100, 200, 400 };
    public const int NewPortDays = 20, PortUpgradeDays = 10;
    /// <summary>Burst above the commit is billed at the 95th percentile at this multiple of the price; a NOC brings it down.</summary>
    public const double BurstFactor = 1.5, BurstFactorNoc = 1.2;
    /// <summary>Average traffic of a running rack in Gbit/s; it follows the time of day.</summary>
    public const double RackGbps = 0.25;
    /// <summary>Switches in a data hall that racks plug into: Edge 24, Edge 48 and the big Spine 400.</summary>
    public static bool IsHallSwitch(ObjKind k) => k is ObjKind.EdgeSwitch24 or ObjKind.EdgeSwitch48 or ObjKind.Spine;
    /// <summary>Uplinks of a hall switch to the distribution switch: 2 × 100G, 4 × 100G or 8 × 400G.</summary>
    public static int Uplinks(ObjKind k) => k switch { ObjKind.EdgeSwitch24 => 2, ObjKind.EdgeSwitch48 => 4, ObjKind.Spine => 8, ObjKind.DistSwitch => 2, _ => 0 };

    public static readonly CarrierDef[] Carriers =
    {
        new(0, "Lowlands Transit", 0, 0.32, false, "carrier_tier1"),
        new(1, "NordFiber", 1, 0.26, false, "carrier_budget"),
        new(2, "Kanaal Net", 0, 0.29, false, "carrier_regional"),
        new(3, "Eurolink", 2, 0.38, false, "carrier_premium"),
        new(4, "Polder-IX", 2, 0, true, "carrier_ix"),
    };

    /// <summary>Rival datacenters in the region that want to peer (only with AI competitors switched on).</summary>
    public static readonly RivalDef[] Rivals =
    {
        new(0, "Zuidas Colo", 2, 0.08, 45_000, 900, 5),
        new(1, "Rivierhaven DC", 8, 0.10, 60_000, -1500, 20),
        new(2, "Noordpoort Data", 5, 0.07, 40_000, 600, 40),
    };
    public const int PeeringDays = 10;

    /// <summary>Cross-connects: one-off fee and monthly fee; customers order them as more networks sit in the MMR.</summary>
    public const long XcSetupFee = 250, XcMonthlyFee = 60;
    public const double XcHours = 0.4;

    /// <summary>Price per Mbit with a volume discount for bigger commits.</summary>
    public static double PricePerMbit(CarrierDef c, double commitGbps) => c.PricePerMbit * (commitGbps >= 100 ? 0.7 : commitGbps >= 10 ? 0.85 : 1);
    public static PortDef Port(int gbps) => System.Array.Find(Ports, p => p.Gbps == gbps) ?? Ports[0];

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
        [RoomType.Transformer] = new(RoomType.Transformer, "room_transformer", "#C9A66B"),
        [RoomType.Switchgear] = new(RoomType.Switchgear, "room_switchgear", "#BFA77E"),
        [RoomType.Battery] = new(RoomType.Battery, "room_battery", "#B9B08A"),
        [RoomType.Noc] = new(RoomType.Noc, "room_noc", "#7F8FA8"),
        [RoomType.Lab] = new(RoomType.Lab, "room_lab", "#9FC2B8"),
        [RoomType.Soc] = new(RoomType.Soc, "room_soc", "#7C8CB0"),
        [RoomType.Warehouse] = new(RoomType.Warehouse, "room_warehouse", "#B7A98F"),
    };

    public static readonly Dictionary<DoorType, DoorDef> Doors = new()
    {
        [DoorType.Single] = new(DoorType.Single, "door_single", 1, 600, false),
        [DoorType.Double] = new(DoorType.Double, "door_double", 2, 1400, true),
        [DoorType.Fire] = new(DoorType.Fire, "door_fire", 1, 1800, false),
        [DoorType.Badge] = new(DoorType.Badge, "door_badge", 1, 3200, false),
        [DoorType.RollUp] = new(DoorType.RollUp, "door_rollup", 3, 5200, true),
        [DoorType.Mantrap] = new(DoorType.Mantrap, "door_mantrap", 1, 24000, false),
    };

    public static readonly Dictionary<Role, long> WagePerDay = new()
    {
        [Role.Builder] = 280, [Role.Fitter] = 260, [Role.Engineer] = 340,
    };

    public static long RoomCost(int w, int d) => (2 * (w + d)) * 300L + w * d * 50L;
}

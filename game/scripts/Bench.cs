using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect;

/// <summary>
/// Performance benchmark: godot --path game -- --bench 5000 [--bench-seconds 10] [--bench-out file.json].
/// Builds one very large data hall full of live racks, runs the game at normal speed with the roof off
/// and reports frames per second and how long the simulation and the 3D world take per frame.
/// </summary>
public static class Bench
{
    public static GameState Build(int racks)
    {
        const int rowLen = 60;
        var rows = (racks + rowLen - 1) / rowLen;
        // two rows back to back (hot aisle side), then a cold aisle: three cells per pair
        var depth = (rows + 1) / 2 * 3 + 2;
        var o = new NewGameOptions { Company = "Bench", Creative = true, CreativeOptions = new CreativeOptions { UnlimitedMoney = true, AllUnlocks = true, InstantBuild = true } };
        var s = GameState.New(o);
        s.PlotSize = Math.Max(Math.Max(64, rowLen + 24), depth + 24);
        s.EnsureGrids();
        var w = rowLen + 6;
        s.AddBuilding(BuildingKind.Datacenter, 4, 4, w, depth, 1);
        var hall = s.AddRoom(RoomType.DataHall, 4, 4, w, depth, 0);
        var placed = 0;
        for (var r = 0; r < rows && placed < racks; r++)
        {
            var z = 5 + r / 2 * 3 + r % 2;
            var rot = r % 2 == 0 ? 2 : 0;
            for (var x = 7; x < 7 + rowLen && placed < racks; x++)
                if (s.AddObject(ObjKind.Rack, x, z, rot, 0) != null) placed++;
        }
        for (var z = 6; z < 4 + depth; z += 4) s.AddObject(ObjKind.Crah, 4, z, 1, 0);
        if (hall != null) { hall.Containment = Containment.HotCold; hall.Busbar = true; hall.Stage = Stage.Done; hall.Temp = 23; }
        foreach (var b in s.Buildings) b.Stage = Stage.Done;
        foreach (var x in s.Objects) x.Status = ObjStatus.Live;
        // play on as a normal game would: nothing is finished instantly any more
        o.CreativeOptions.InstantBuild = false;
        s.Touch();
        return s;
    }

    public sealed class Sample
    {
        public readonly List<double> Frame = new(), Sim = new(), World = new(), Cpu = new();
        ulong _last;
        public void Add(double frame, double sim, double world)
        {
            var now = Time.GetTicksUsec();
            if (_last != 0) { Frame.Add((now - _last) / 1e6); Cpu.Add(Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000); }
            _last = now; Sim.Add(sim); World.Add(world);
        }
        static double P(List<double> l, double p) { if (l.Count == 0) return 0; var o = l.OrderBy(v => v).ToList(); return o[Math.Min(o.Count - 1, (int)(o.Count * p))]; }
        public string Report(string name) =>
            $"\"{name}\": {{\"frames\": {Frame.Count}, \"fps_avg\": {F(Frame.Count / Math.Max(1e-6, Frame.Sum()))}, \"frame_ms_p50\": {F(P(Frame, 0.5) * 1000)}, \"frame_ms_p95\": {F(P(Frame, 0.95) * 1000)}, " +
            $"\"process_ms_avg\": {F(Cpu.DefaultIfEmpty().Average())}, \"process_ms_p95\": {F(P(Cpu, 0.95))}, \"sim_ms_avg\": {F(Sim.DefaultIfEmpty().Average())}, \"sim_ms_p95\": {F(P(Sim, 0.95))}, \"world_ms_avg\": {F(World.DefaultIfEmpty().Average())}, \"world_ms_p95\": {F(P(World, 0.95))}}}";
        static string F(double v) => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static double Ms(Stopwatch sw) => sw.Elapsed.TotalMilliseconds;
}

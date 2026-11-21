using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;

namespace DatacenterArchitect.UI;

/// <summary>Texts of an <see cref="Outage"/>: the cause line and the fix button.</summary>
public static class OutageUi
{
    // arguments that are text keys themselves (the bottleneck link) are translated too
    static object[] Args(object[] args) => args.Select(a => a is string k && k.StartsWith("link_") ? Loc.T(k) : a).ToArray();

    public static string Text(Outage o) => Loc.T(o.Key, Args(o.Args));
    public static string FixText(Outage o) => Loc.T(o.FixKey, Args(o.FixArgs));
}

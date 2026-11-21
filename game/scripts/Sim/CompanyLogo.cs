using System;
using System.Linq;
using System.Text.Json.Serialization;

namespace DatacenterArchitect.Sim;

/// <summary>
/// Step 11 round 3: the player's own company logo. A shape in a main colour, a border and initials in a second colour.
/// It is shown on the facades, the delivery trucks, the shirts and ties of your own staff and the rack doors (each can be switched off).
/// </summary>
public class CompanyLogo
{
    public const int Shapes = 6;
    /// <summary>0 circle, 1 rounded square, 2 hexagon, 3 shield, 4 diamond, 5 ring.</summary>
    public int Shape { get; set; } = 1;
    public string Primary { get; set; } = "#f2a33a";
    public string Secondary { get; set; } = "#111d2b";
    /// <summary>Up to three letters; empty = taken from the company name.</summary>
    public string Initials { get; set; } = "";
    public bool OnBuildings { get; set; } = true;
    public bool OnVehicles { get; set; } = true;
    public bool OnWorkwear { get; set; } = true;
    public bool OnRacks { get; set; } = true;

    /// <summary>The colours the logo editor offers (brand-friendly, readable on dark and light).</summary>
    public static readonly string[] Palette =
    {
        "#f2a33a", "#38c6f4", "#ff7a1a", "#3fbf7f", "#e2493b", "#8a6cf0", "#f2c94c", "#2f5d9a",
        "#111d2b", "#f4f1ea", "#5a6570", "#c2185b",
    };

    /// <summary>The first letters of up to two words of a name ("Polder Datacenters" → "PD").</summary>
    public static string InitialsOf(string name)
    {
        var words = (name ?? "").Split(new[] { ' ', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => char.IsLetterOrDigit(w[0])).ToArray();
        if (words.Length == 0) return "DC";
        if (words.Length == 1) return words[0].Length >= 2 ? words[0][..2].ToUpperInvariant() : words[0].ToUpperInvariant();
        return (words[0][..1] + words[1][..1]).ToUpperInvariant();
    }

    /// <summary>The letters on the logo: the chosen initials, or those of the company name.</summary>
    public string Letters(string company) => string.IsNullOrWhiteSpace(Initials) ? InitialsOf(company) : Initials.Trim().ToUpperInvariant()[..Math.Min(3, Initials.Trim().Length)];

    /// <summary>A starting logo picked from the company name, so two companies rarely look the same.</summary>
    public static CompanyLogo For(string company)
    {
        var h = 0;
        foreach (var ch in company ?? "") h = h * 31 + ch;
        h = Math.Abs(h);
        var light = new[] { 0, 1, 2, 3, 4, 5, 6, 11 };
        return new CompanyLogo { Shape = h % Shapes, Primary = Palette[light[h / 7 % light.Length]], Secondary = h / 3 % 2 == 0 ? "#111d2b" : "#f4f1ea" };
    }

    /// <summary>A customer's logo, the same every time for the same customer.</summary>
    public static CompanyLogo ForCustomer(Deal d) => For((d.NameKey ?? "") + d.Name + (int)d.Type);

    /// <summary>Changes on this number tell the world to redraw the logo.</summary>
    [JsonIgnore] public int Version => HashCode.Combine(Shape, Primary, Secondary, Initials, OnBuildings, OnVehicles, OnWorkwear, OnRacks);
}

public partial class GameState
{
    CompanyLogo _logo;
    /// <summary>The company logo (round 3). Older saves get one made from the company name.</summary>
    public CompanyLogo Logo { get => _logo ??= CompanyLogo.For(Options?.Company); set => _logo = value; }

    /// <summary>Renames the company (logo initials follow when they were not set by hand).</summary>
    public string Rename(string name)
    {
        name = (name ?? "").Trim();
        if (name.Length < 2) return "msg_name_short";
        if (name.Length > 40) name = name[..40];
        Options.Company = name;
        Touch(false);
        return null;
    }
}

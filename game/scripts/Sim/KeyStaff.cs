using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace DatacenterArchitect.Sim;

/// <summary>Named key people with a lasting bonus (step 9). The headhunter finds them; rivals try to poach them.</summary>
public enum KeyRole { Architect, StarSales, EnergyTrader, SocLead, Sustainability, CountryManager, HostingLead }

public class KeyPerson
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public KeyRole Role { get; set; }
    public long Salary { get; set; }
    public double Loyalty { get; set; } = 70;
    public double HiredDay { get; set; }
    /// <summary>A candidate the headhunter found (not yet hired), and the day the candidate takes another job.</summary>
    public bool Candidate { get; set; }
    public double ExpiresDay { get; set; }
    /// <summary>A rival made an offer: its name and the day the person decides.</summary>
    public string PoachedBy { get; set; }
    public double PoachDay { get; set; }
}

public static class KeyStaff
{
    public static readonly Dictionary<KeyRole, long> Salary = new()
    {
        [KeyRole.Architect] = 11000, [KeyRole.StarSales] = 12000, [KeyRole.EnergyTrader] = 10000, [KeyRole.SocLead] = 10500,
        [KeyRole.Sustainability] = 9000, [KeyRole.CountryManager] = 13000, [KeyRole.HostingLead] = 11500,
    };
    public static string Key(KeyRole r) => "key_" + r.ToString().ToLowerInvariant();
    public const int FeeMonths = 3;
}

public partial class GameState
{
    public List<KeyPerson> KeyPeople { get; set; } = new();
    public double NextHeadhunterDay { get; set; } = 30;

    public bool Has(KeyRole r) => KeyPeople.Any(k => !k.Candidate && k.Role == r);
    [JsonIgnore] public long KeySalaries => KeyPeople.Where(k => !k.Candidate).Sum(k => k.Salary);

    public string CanHireKey(KeyPerson k)
    {
        if (k == null || !k.Candidate || !KeyPeople.Contains(k)) return "msg_nothing_changed";
        if (Has(k.Role)) return "msg_key_have";
        if (!CanAfford(k.Salary * KeyStaff.FeeMonths)) return "msg_no_money";
        return null;
    }

    public string HireKey(KeyPerson k)
    {
        var why = CanHireKey(k);
        if (why != null) return why;
        if (!Creative.UnlimitedMoney) { var fee = k.Salary * KeyStaff.FeeMonths; Money -= fee; Book("cost_salaries", -fee); }
        k.Candidate = false; k.HiredDay = Day; k.Loyalty = 70;
        Notes.Add(("msg_key_hired", k.Name));
        AddNews("news_key_hired", $"{k.Name}¦{Options.Company}", 3);
        Touch(false);
        return null;
    }

    /// <summary>Answer a rival's offer: match it (20% more salary, more loyal) or let the person go.</summary>
    public string AnswerPoach(KeyPerson k, bool match)
    {
        if (k == null || k.PoachedBy == null) return "msg_nothing_changed";
        if (match)
        {
            k.Salary = (long)Math.Round(k.Salary * 1.2 / 50) * 50; k.Loyalty = Math.Min(100, k.Loyalty + 30); k.PoachedBy = null;
            Notes.Add(("msg_key_stays", k.Name));
        }
        else Leave(k);
        Touch(false);
        return null;
    }

    void Leave(KeyPerson k)
    {
        var to = k.PoachedBy;
        KeyPeople.Remove(k);
        Notes.Add(("msg_key_left", k.Name));
        AddNews(to != null ? "news_key_poached" : "news_key_left", $"{k.Name}¦{to ?? Options.Company}", 2);
    }

    public void DismissKey(KeyPerson k) { if (k != null && KeyPeople.Remove(k)) Touch(false); }

    void KeyDay(int day, Random rng)
    {
        KeyPeople.RemoveAll(k => k.Candidate && day >= k.ExpiresDay);
        // the headhunter (with HR) brings a candidate for a role you do not have
        if (Has(Exec.Hr) && day >= NextHeadhunterDay && !KeyPeople.Any(k => k.Candidate))
        {
            NextHeadhunterDay = day + 35 + rng.Next(30);
            var open = Enum.GetValues<KeyRole>().Where(r => !Has(r)).ToList();
            if (open.Count > 0)
            {
                var role = open[rng.Next(open.Count)];
                var k = new KeyPerson
                {
                    Id = NextId++, Role = role, Candidate = true, ExpiresDay = day + 20,
                    Name = Staffing.First[rng.Next(Staffing.First.Length)] + " " + Staffing.Last[rng.Next(Staffing.Last.Length)],
                    Salary = (long)Math.Round(KeyStaff.Salary[role] * (0.9 + rng.NextDouble() * 0.2) / 50) * 50,
                };
                KeyPeople.Add(k);
                Notes.Add(("msg_key_candidate", $"{k.Name}¦{KeyStaff.Key(role)}"));
            }
        }
        foreach (var k in KeyPeople.Where(k => !k.Candidate).ToList())
        {
            // loyalty follows pay and how the company does
            var target = 55 + 15 * PayPolicy + (Reputation - 50) / 5 + (Staff.Count == 0 ? 0 : (AverageSatisfaction - 60) / 4);
            k.Loyalty = Math.Clamp(k.Loyalty + (target - k.Loyalty) * 0.01, 0, 100);
            if (k.PoachedBy != null)
            {
                if (day >= k.PoachDay) { if (k.Loyalty < 55) Leave(k); else { k.PoachedBy = null; Notes.Add(("msg_key_stays", k.Name)); } }
                continue;
            }
            if (Options.AiCompetitors && Rivals.Any() && k.Loyalty < 75 && rng.NextDouble() < 1 / 260.0 * (1 + Aggression))
            {
                var rival = Rivals.ElementAt(rng.Next(Rivals.Count()));
                k.PoachedBy = rival.Name; k.PoachDay = day + 10;
                Notes.Add(("msg_key_poach", $"{k.Name}¦{rival.Name}"));
            }
        }
    }
}

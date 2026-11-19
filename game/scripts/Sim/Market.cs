using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace DatacenterArchitect.Sim;

/// <summary>A rival datacenter company in the market (step 9). It grows, sets prices, bids on tenders and may be put up for sale.</summary>
public class Competitor
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Built IT capacity and the part of it that is sold, in MW.</summary>
    public double CapacityMw { get; set; }
    public double SoldMw { get; set; }
    /// <summary>Price level against the market (0.85 cheap .. 1.2 premium) and quality (0..100).</summary>
    public double Price { get; set; } = 1;
    public double Quality { get; set; } = 60;
    /// <summary>Cash in millions of euros: they need it to build.</summary>
    public double Cash { get; set; } = 20;
    public int Sites { get; set; } = 1;
    /// <summary>Bought by you, or gone bankrupt.</summary>
    public bool Gone { get; set; }
    [JsonIgnore] public double Utilisation => CapacityMw <= 0 ? 0 : SoldMw / CapacityMw;
}

/// <summary>A headline on the market news ticker.</summary>
public class NewsItem
{
    public double Day { get; set; }
    public string Key { get; set; } = "";
    public string Arg { get; set; }
    /// <summary>0 = market, 1 = energy, 2 = rivals, 3 = your company, 4 = technology, 5 = disasters and attacks.</summary>
    public int Kind { get; set; }
}

/// <summary>A datacenter that is for sale: an independent colo or a rival in trouble.</summary>
public class SaleOffer
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Country { get; set; } = "nl";
    public string City { get; set; } = "";
    public int Racks { get; set; }
    public int Occupied { get; set; }
    public long Price { get; set; }
    public double ExpiresDay { get; set; }
    /// <summary>The rival that is for sale, -1 for an independent datacenter.</summary>
    public int Competitor { get; set; } = -1;
}

public static class Market
{
    public static readonly string[] RivalNames = { "Zuidas Colo", "Rivierhaven DC", "Noordpoort Data", "Kustlijn Data", "Heuvelrug Compute" };
    static readonly string[] IndieNames = { "Datahaven", "Polderbox", "Kanaalcolo", "Molenhost", "Dijkdata", "Brugpunt DC", "Veenserver", "Lindencolo" };
    /// <summary>Number of shares a company is divided into when it goes public.</summary>
    public const double Shares = 10_000_000;
    public const int MaxNews = 40;
    /// <summary>Market demand in the home region at the start, in MW of IT load, and its growth per month.</summary>
    public const double StartDemandMw = 40, DemandGrowth = 0.012;

    public static string IndieName(Random rng) => IndieNames[rng.Next(IndieNames.Length)];
}

/// <summary>Rivals and market share, tenders against named rivals, the news ticker, acquisitions and the stock market (step 9).</summary>
public partial class GameState
{
    public List<Competitor> Competitors { get; set; } = new();
    public List<NewsItem> News { get; set; } = new();
    public List<SaleOffer> Offers { get; set; } = new();
    public double DemandMw { get; set; } = Market.StartDemandMw;
    /// <summary>Listed on the stock exchange: share of the company in public hands, the share price and its history (one point per month).</summary>
    public bool Listed { get; set; }
    public double PublicShare { get; set; }
    public double SharePrice { get; set; }
    public double ListedDay { get; set; }
    public List<double> PriceHistory { get; set; } = new();
    public double NextOfferDay { get; set; } = 45;

    Random GrowthRng(int day, int salt) => new(unchecked(Seed * 48271 + day * 7919 + salt * 104729));

    [JsonIgnore] public IEnumerable<Competitor> Rivals => Competitors.Where(c => !c.Gone);

    /// <summary>Rivals are made once, at the start of a game with AI competitors (1 to 5, chosen at new game).</summary>
    public void EnsureCompetitors()
    {
        if (!Options.AiCompetitors || Competitors.Count > 0) return;
        var rng = new Random(unchecked(Seed * 31 + 17));
        var n = Math.Clamp(Options.Competitors, 1, 5);
        for (var i = 0; i < n; i++)
        {
            var cap = 6 + rng.Next(10);
            Competitors.Add(new Competitor
            {
                Id = i, Name = Market.RivalNames[i], CapacityMw = cap, SoldMw = cap * (0.55 + rng.NextDouble() * 0.25),
                Price = 0.9 + rng.NextDouble() * 0.2, Quality = 45 + rng.Next(30), Cash = 10 + rng.Next(30), Sites = 1 + rng.Next(2),
            });
        }
    }

    /// <summary>IT capacity you sell, in MW: colocation contracts, the hosting company and other locations.</summary>
    [JsonIgnore]
    public double MySoldMw => Deals.Where(d => d.State == DealState.Active).Sum(d => d.Kw) / 1000 + HostingKw / 1000 + Sites.Where(x => x.Kind == SiteKind.Datacenter).Sum(x => x.Occupied * RemoteRackKw / 1000);

    /// <summary>Your share of the sold capacity in the market (0..1). Without rivals it is your share of the demand.</summary>
    [JsonIgnore]
    public double MarketShare
    {
        get
        {
            var mine = MySoldMw;
            var others = Rivals.Sum(c => c.SoldMw);
            if (!Options.AiCompetitors) return Math.Min(1, mine / Math.Max(1, DemandMw));
            return mine + others <= 0 ? 0 : mine / (mine + others);
        }
    }

    /// <summary>Aggression of the rivals: 0 calm, 1 normal, 2 aggressive.</summary>
    [JsonIgnore] public int Aggression => Math.Clamp(Options.Aggression, 0, 2);

    public void AddNews(string key, string arg = null, int kind = 0)
    {
        if (!Options.News) return;
        News.Insert(0, new NewsItem { Day = Day, Key = key, Arg = arg, Kind = kind });
        if (News.Count > Market.MaxNews) News.RemoveRange(Market.MaxNews, News.Count - Market.MaxNews);
    }

    /// <summary>The best score among the rivals that bid on a tender. Draws three numbers so the old balance stays the same.</summary>
    public double RivalBid(Random rng, out string winner)
    {
        winner = null;
        if (!Options.AiCompetitors) return 0.45;
        var list = Rivals.ToList();
        var best = 0.0;
        for (var i = 0; i < 3; i++)
        {
            var r = 0.3 + rng.NextDouble() * 0.45;
            if (list.Count == 0) continue;
            var c = list[i % list.Count];
            r += (c.Quality - 60) / 400 + (1 - c.Price) * 0.3 + 0.03 * (Aggression - 1);
            if (r > best) { best = r; winner = c.Name; }
        }
        if (list.Count == 0) best = 0.45;
        return best;
    }

    // ---------------- the market moves, once a day ----------------
    void MarketDay(int day)
    {
        var rng = GrowthRng(day, 1);
        DemandMw *= 1 + Market.DemandGrowth / 30;
        if (Options.AiCompetitors)
        {
            var share = MarketShare;
            foreach (var c in Rivals.ToList())
            {
                // they sell into the demand: better quality and lower prices sell faster
                var pull = 0.002 * (c.Quality / 60) * (1.1 - c.Price) * 10 * (DemandMw / Market.StartDemandMw);
                c.SoldMw = Math.Min(c.CapacityMw, c.SoldMw + pull * (0.5 + rng.NextDouble()));
                // churn: customers move, more when you are better than them
                c.SoldMw = Math.Max(0, c.SoldMw - c.SoldMw * 0.0008 * (1 + (Reputation - c.Quality) / 100));
                c.Cash += (c.SoldMw * 0.11 - c.CapacityMw * 0.06) / 30;
                // building: when they fill up and have money
                if (c.Utilisation > 0.78 && c.Cash > 8 && rng.NextDouble() < 0.02 * (1 + Aggression))
                {
                    var add = 4 + rng.Next(8 + 6 * Aggression);
                    c.CapacityMw += add; c.Cash -= add * 1.1; c.Sites += rng.NextDouble() < 0.35 ? 1 : 0;
                    AddNews("news_rival_expands", $"{c.Name}¦{add}", 2);
                }
                // a price war when you take too much of their market
                if (Aggression >= 1 && share > 0.3 && rng.NextDouble() < 0.004 * Aggression)
                {
                    c.Price = Math.Max(0.82, c.Price - 0.05);
                    AddNews("news_rival_price_cut", c.Name, 2);
                }
                else if (c.Price < 1 && rng.NextDouble() < 0.01) c.Price = Math.Min(1.15, c.Price + 0.02);
                if (rng.NextDouble() < 0.004) { c.Quality = Math.Clamp(c.Quality + rng.Next(-4, 7), 30, 95); }
                // trouble: low cash and an empty building put a rival up for sale
                if (c.Cash < 0 && c.Utilisation < 0.6 && !Offers.Any(o => o.Competitor == c.Id) && rng.NextDouble() < 0.05)
                {
                    var racks = (int)Math.Round(c.CapacityMw * 1000 / RemoteRackKw / 4) * 4;
                    Offers.Add(new SaleOffer
                    {
                        Id = NextId++, Name = c.Name, Country = "nl", City = Org.City(rng), Racks = Math.Max(24, racks),
                        Occupied = (int)Math.Round(Math.Max(24, racks) * c.Utilisation), Competitor = c.Id,
                        Price = (long)Math.Round(c.CapacityMw * 3_200_000 + c.SoldMw * 2_000_000), ExpiresDay = day + 45,
                    });
                    AddNews("news_rival_for_sale", c.Name, 2);
                    Notes.Add(("msg_rival_for_sale", c.Name));
                }
                if (c.Cash < -25) { c.Gone = true; AddNews("news_rival_bankrupt", c.Name, 2); Peers.RemoveAll(p => p.Rival == c.Id); }
            }
        }
        // independent datacenters come up for sale now and then (the CFO hears about them)
        if (Has(Exec.Cfo) && day >= NextOfferDay)
        {
            NextOfferDay = day + 60 + rng.Next(60);
            var country = OpenCountries.Count > 0 && rng.NextDouble() < 0.5 ? OpenCountries[rng.Next(OpenCountries.Count)] : "nl";
            var racks = 24 + 24 * rng.Next(5);
            var def = Countries.Get(country);
            var occupied = (int)Math.Round(racks * (0.35 + rng.NextDouble() * 0.5));
            Offers.Add(new SaleOffer
            {
                Id = NextId++, Name = Market.IndieName(rng), Country = country, City = def.Cities[rng.Next(def.Cities.Length)], Racks = racks, Occupied = occupied,
                Price = (long)Math.Round((racks * 26_000 + occupied * 15_000) * def.Cost / 10000) * 10000, ExpiresDay = day + 40,
            });
            Notes.Add(("msg_offer_new", Offers[^1].Name));
        }
        Offers.RemoveAll(o => day >= o.ExpiresDay || (o.Competitor >= 0 && Competitors.FirstOrDefault(c => c.Id == o.Competitor)?.Gone != false));
        // news about the wider world
        if (rng.NextDouble() < 0.06) AddNews("news_tech_" + rng.Next(8), null, 4);
        if (rng.NextDouble() < 0.03) AddNews("news_market_" + rng.Next(6), null, 0);
        if (Listed) ListedDay2(day, rng);
    }

    // ---------------- acquisitions ----------------
    public string CanBuyOffer(SaleOffer o)
    {
        if (o == null || !Offers.Contains(o)) return "msg_nothing_changed";
        if (!Has(Exec.Cfo)) return "msg_needs_cfo";
        if (o.Country != "nl" && !OpenCountries.Contains(o.Country)) return "msg_needs_country";
        if (!CanAfford(o.Price)) return "msg_no_money";
        return null;
    }

    /// <summary>Buy a datacenter: it becomes one of your locations, with its customers. A rival leaves the market.</summary>
    public string BuyOffer(SaleOffer o)
    {
        var why = CanBuyOffer(o);
        if (why != null) return why;
        if (!Creative.UnlimitedMoney) { Money -= o.Price; Book("cost_acquisition", -o.Price); }
        Offers.Remove(o);
        var size = Math.Clamp(o.Racks <= 48 ? 0 : o.Racks <= 192 ? 1 : o.Racks <= 768 ? 2 : 3, 0, 3);
        Sites.Add(new RemoteSite
        {
            Id = NextId++, Name = o.Name, Country = o.Country, City = o.City, Kind = SiteKind.Datacenter, Size = size, Racks = o.Racks, Occupied = o.Occupied,
            Built = true, Acquired = true, OpenedDay = Day,
        });
        if (o.Competitor >= 0 && Competitors.FirstOrDefault(c => c.Id == o.Competitor) is { } c)
        {
            c.Gone = true;
            Peers.RemoveAll(p => p.Rival == c.Id);
        }
        Reputation = Math.Min(100, Reputation + 2);
        AddNews("news_you_acquire", o.Name, 3);
        Notes.Add(("msg_acquired", o.Name));
        Touch(false);
        return null;
    }

    // ---------------- stock market ----------------
    /// <summary>Revenue of the last twelve months from the books.</summary>
    [JsonIgnore] public long AnnualRevenue => Books.Where(b => b.Month < MonthIndex).TakeLast(12).Sum(b => b.Lines.Where(kv => kv.Key.StartsWith("inc_")).Sum(kv => kv.Value));
    [JsonIgnore] public long AnnualProfit => Books.Where(b => b.Month < MonthIndex).TakeLast(12).Sum(b => b.Lines.Where(kv => kv.Key.StartsWith("inc_") || kv.Key.StartsWith("cost_")).Sum(kv => kv.Value));

    /// <summary>What the company is worth: a multiple of revenue that rises with reputation, ESG, market share and profit.</summary>
    [JsonIgnore]
    public long MarketValue
    {
        get
        {
            var rev = Math.Max(0, AnnualRevenue);
            var margin = rev > 0 ? Math.Clamp(AnnualProfit / (double)rev, -0.5, 0.6) : 0;
            var multiple = 2.2 + Reputation / 50 + Esg / 60 + MarketShare * 2 + margin * 3;
            return Math.Max(Valuation, (long)Math.Round(rev * Math.Max(0.8, multiple) - Debt));
        }
    }

    public const long IpoMinRevenue = 6_000_000;

    public string CanIpo()
    {
        if (Listed) return "msg_nothing_changed";
        if (!Has(Exec.Cfo) || Training(Exec.Cfo) < 1) return "msg_ipo_needs_cfo";
        if (!Has(Exec.Legal)) return "msg_needs_legal";
        if (AnnualRevenue < IpoMinRevenue) return "msg_ipo_revenue";
        if (MarketValue <= 0) return "msg_ipo_value";
        if (InvestorShare + 0.2 > 0.75) return "msg_ipo_shares";
        return null;
    }

    /// <summary>Go public: sell a share of the company on the stock exchange. The money comes in at once; shareholders want a dividend.</summary>
    public string Ipo(double share)
    {
        var why = CanIpo();
        if (why != null) return why;
        share = Math.Clamp(share, 0.1, 0.4);
        var value = MarketValue;
        var cash = (long)Math.Round(value * share * 0.95);
        Money += cash; Book("fin_ipo", cash);
        Listed = true; PublicShare = share; ListedDay = Day;
        SharePrice = Math.Round(value / Market.Shares * 100) / 100;
        PriceHistory.Clear(); PriceHistory.Add(SharePrice);
        Reputation = Math.Min(100, Reputation + 5);
        AddNews("news_you_ipo", Options.Company, 3);
        Notes.Add(("msg_ipo_done", Ui(cash)));
        Touch(false);
        return null;
    }

    static string Ui(long v) => v.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Share price follows the valuation with some noise; every quarter the results come out.</summary>
    void ListedDay2(int day, Random rng)
    {
        var fair = Math.Max(0.5, MarketValue / Market.Shares);
        SharePrice = Math.Max(0.1, SharePrice + (fair - SharePrice) * 0.03 + SharePrice * (rng.NextDouble() - 0.5) * 0.02);
        if (day % 30 == 0) { PriceHistory.Add(Math.Round(SharePrice * 100) / 100); if (PriceHistory.Count > 36) PriceHistory.RemoveAt(0); }
        if (day % 90 == 0 && PriceHistory.Count >= 4)
        {
            var before = PriceHistory[^4];
            AddNews(SharePrice >= before ? "news_you_results_good" : "news_you_results_bad", Options.Company, 3);
            if (SharePrice < before * 0.8) Reputation = Math.Max(0, Reputation - 1);
        }
    }

    /// <summary>Shareholders on the stock exchange get 40% of their share of the profit as a dividend.</summary>
    void PublicDividend(long profit)
    {
        if (!Listed || PublicShare <= 0 || profit <= 0) return;
        var div = (long)Math.Round(profit * PublicShare * 0.4);
        Money -= div; Book("fin_dividend", -div);
    }
}

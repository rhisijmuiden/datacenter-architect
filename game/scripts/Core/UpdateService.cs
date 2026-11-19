using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using HttpClient = System.Net.Http.HttpClient;
using FileAccess = Godot.FileAccess;

namespace DatacenterArchitect.Core;

public record ReleaseInfo(SemVer Version, string Tag, bool Prerelease, string PageUrl, string InstallerUrl, string InstallerName, long InstallerSize, string Notes);

public enum UpdateVerdict { UpToDate, NewerAvailable, ChannelSwitch, NoReleases, Error }

public record UpdateCheck(UpdateVerdict Verdict, ReleaseInfo Release, string Error);

/// <summary>
/// Checks GitHub Releases of the game repository. Stable = newest normal release;
/// test = newest release including pre-releases (tags like v0.2.0-test.1).
/// Updating downloads the Windows installer and runs it silently.
/// </summary>
public static class UpdateService
{
    static readonly HttpClient Http = CreateClient();

    static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("DatacenterArchitect-Updater");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    public static List<ReleaseInfo> ParseReleases(string json)
    {
        var list = new List<ReleaseInfo>();
        using var doc = JsonDocument.Parse(json);
        foreach (var r in doc.RootElement.EnumerateArray())
        {
            if (r.TryGetProperty("draft", out var draft) && draft.GetBoolean()) continue;
            var tag = r.GetProperty("tag_name").GetString();
            if (!SemVer.TryParse(tag, out var v)) continue;
            string url = null, name = null; long size = 0;
            if (r.TryGetProperty("assets", out var assets))
                foreach (var a in assets.EnumerateArray())
                {
                    var n = a.GetProperty("name").GetString() ?? "";
                    if (!n.EndsWith("Setup.exe", StringComparison.OrdinalIgnoreCase)) continue;
                    name = n; url = a.GetProperty("browser_download_url").GetString(); size = a.GetProperty("size").GetInt64();
                }
            var pre = r.TryGetProperty("prerelease", out var p) && p.GetBoolean();
            var notes = r.TryGetProperty("body", out var b) && b.ValueKind == JsonValueKind.String ? b.GetString() : "";
            list.Add(new ReleaseInfo(v, tag, pre, r.GetProperty("html_url").GetString(), url, name, size, notes));
        }
        return list;
    }

    /// <summary>Pick the release for a channel and decide what to offer.</summary>
    public static UpdateCheck Decide(List<ReleaseInfo> releases, string channel, SemVer current, string buildChannel)
    {
        var pool = releases.Where(r => r.InstallerUrl != null && (channel == "test" || !r.Prerelease)).OrderByDescending(r => r.Version).ToList();
        if (pool.Count == 0) return new UpdateCheck(UpdateVerdict.NoReleases, null, null);
        var best = pool[0];
        var cmp = best.Version.CompareTo(current);
        if (cmp > 0) return new UpdateCheck(UpdateVerdict.NewerAvailable, best, null);
        // Switching from test to stable may mean installing an older stable build.
        if (channel == "stable" && buildChannel == "test" && cmp != 0) return new UpdateCheck(UpdateVerdict.ChannelSwitch, best, null);
        return new UpdateCheck(UpdateVerdict.UpToDate, best, null);
    }

    public static async Task<UpdateCheck> CheckAsync(string channel)
    {
        try
        {
            var json = await Http.GetStringAsync($"https://api.github.com/repos/{GameVersion.Repository}/releases?per_page=30");
            return Decide(ParseReleases(json), channel, GameVersion.Current, GameVersion.BuildChannel);
        }
        catch (Exception e)
        {
            return new UpdateCheck(UpdateVerdict.Error, null, e.Message);
        }
    }

    /// <summary>Download the installer to user://updates and report progress 0..1.</summary>
    public static async Task<string> DownloadAsync(ReleaseInfo release, Action<double> progress)
    {
        var dir = ProjectSettings.GlobalizePath("user://updates");
        Directory.CreateDirectory(dir);
        var path = System.IO.Path.Combine(dir, release.InstallerName);
        using var resp = await Http.GetAsync(release.InstallerUrl, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength ?? release.InstallerSize;
        await using var src = await resp.Content.ReadAsStreamAsync();
        await using var dst = File.Create(path);
        var buffer = new byte[81920]; long done = 0; int n;
        while ((n = await src.ReadAsync(buffer)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, n));
            done += n;
            if (total > 0) progress?.Invoke((double)done / total);
        }
        return path;
    }

    /// <summary>Start the installer silently; it closes the game, updates and starts it again.</summary>
    public static bool RunInstaller(string path)
    {
        if (OS.GetName() != "Windows") { OS.ShellOpen(System.IO.Path.GetDirectoryName(path)); return false; }
        var pid = OS.CreateProcess(path, new[] { "/SILENT", "/SP-", "/NOCANCEL", "/CLOSEAPPLICATIONS", "/RESTARTAPPLICATIONS" });
        return pid > 0;
    }
}

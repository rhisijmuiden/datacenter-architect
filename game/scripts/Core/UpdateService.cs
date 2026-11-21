using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using HttpClient = System.Net.Http.HttpClient;

namespace DatacenterArchitect.Core;

public record ReleaseInfo(SemVer Version, string Tag, bool Prerelease, string PageUrl, string InstallerUrl, string InstallerName, long InstallerSize, string Notes,
    string Sha256 = null, string ChecksumUrl = null);

public enum UpdateVerdict { UpToDate, NewerAvailable, ChannelSwitch, NoReleases, Error }

public record UpdateCheck(UpdateVerdict Verdict, ReleaseInfo Release, string Error);

/// <summary>
/// Checks GitHub Releases of the game repository. Stable = newest normal release;
/// test = newest release including pre-releases (tags like v0.2.0-test.1).
/// Updating downloads the package for this platform: on Windows the installer, run silently; on macOS a zip of the
/// app bundle that a small script swaps in after the game has closed (<see cref="RunInstaller"/>).
/// Security: only HTTPS links into this repository's GitHub releases are used, the installer name must look like
/// ours, and the download must match the SHA-256 that GitHub reports for the asset (or the release's .sha256 file)
/// and its listed size before it is started. Anything else is refused.
/// </summary>
public enum UpdatePlatform { Windows, MacOS }

public static class UpdateService
{
    /// <summary>Installers are far smaller; a bigger download is refused.</summary>
    public const long MaxInstallerBytes = 2L << 30;
    static readonly Regex WindowsPattern = new(@"^DatacenterArchitect-[0-9A-Za-z.\-]+-Setup\.exe$", RegexOptions.CultureInvariant);
    static readonly Regex MacPattern = new(@"^DatacenterArchitect-[0-9A-Za-z.\-]+-macOS\.zip$", RegexOptions.CultureInvariant);
    static readonly Regex Sha256Pattern = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant);
    static readonly HttpClient Http = CreateClient();

    static HttpClient CreateClient()
    {
        // redirects stay on (GitHub sends downloads to its asset host); .NET never follows HTTPS to HTTP
        var c = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, MaxAutomaticRedirections = 5 }) { Timeout = TimeSpan.FromSeconds(30) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("DatacenterArchitect-Updater");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    /// <summary>True for an HTTPS link to this game's repository on github.com (release pages and downloads).</summary>
    public static bool IsTrustedUrl(string url, string kind = "releases")
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps || !u.IsDefaultPort) return false;
        if (!string.Equals(u.Host, "github.com", StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrEmpty(u.UserInfo)) return false;
        return u.AbsolutePath.StartsWith($"/{GameVersion.Repository}/{kind}/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The platform this build runs on; anything that is not macOS uses the Windows installer.</summary>
    public static UpdatePlatform Platform => OS.GetName() == "macOS" ? UpdatePlatform.MacOS : UpdatePlatform.Windows;

    /// <summary>A package file name we would have published ourselves for that platform: no folders, no odd characters.</summary>
    public static bool IsInstallerName(string name, UpdatePlatform platform) =>
        name != null && (platform == UpdatePlatform.MacOS ? MacPattern : WindowsPattern).IsMatch(name) && Path.GetFileName(name) == name;

    /// <summary>A package name of ours for any platform.</summary>
    public static bool IsInstallerName(string name) => IsInstallerName(name, UpdatePlatform.Windows) || IsInstallerName(name, UpdatePlatform.MacOS);

    static string Digest(JsonElement asset)
    {
        if (!asset.TryGetProperty("digest", out var d) || d.ValueKind != JsonValueKind.String) return null;
        var text = d.GetString() ?? "";
        if (!text.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) return null;
        var hex = text[7..].Trim().ToLowerInvariant();
        return Sha256Pattern.IsMatch(hex) ? hex : null;
    }

    static string Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>Releases with the package for <paramref name="platform"/> (default: this one); a release without it gets no installer link.</summary>
    public static List<ReleaseInfo> ParseReleases(string json, UpdatePlatform? platform = null)
    {
        var target = platform ?? Platform;
        var list = new List<ReleaseInfo>();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return list;
        foreach (var r in doc.RootElement.EnumerateArray())
        {
            if (r.ValueKind != JsonValueKind.Object) continue;
            if (r.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True) continue;
            var tag = Str(r, "tag_name");
            if (!SemVer.TryParse(tag, out var v)) continue;
            string url = null, name = null, sha = null, sumUrl = null; long size = 0;
            if (r.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in assets.EnumerateArray())
                {
                    var n = Str(a, "name") ?? "";
                    var link = Str(a, "browser_download_url");
                    if (!IsInstallerName(n, target) || !IsTrustedUrl(link, "releases/download")) continue;
                    name = n; url = link; sha = Digest(a);
                    size = a.TryGetProperty("size", out var sz) && sz.ValueKind == JsonValueKind.Number ? sz.GetInt64() : 0;
                }
                if (name != null)
                    foreach (var a in assets.EnumerateArray())
                        if (Str(a, "name") == name + ".sha256" && IsTrustedUrl(Str(a, "browser_download_url"), "releases/download"))
                            sumUrl = Str(a, "browser_download_url");
            }
            var pre = r.TryGetProperty("prerelease", out var p) && p.ValueKind == JsonValueKind.True;
            var page = Str(r, "html_url");
            list.Add(new ReleaseInfo(v, tag, pre, IsTrustedUrl(page) ? page : null, url, name, size, Str(r, "body") ?? "", sha, sumUrl));
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

    /// <summary>
    /// Download the installer to user://updates and report progress 0..1. The file is written under a temporary name,
    /// checked against the expected size and SHA-256, and only then renamed; a mismatch deletes it and throws.
    /// </summary>
    public static async Task<string> DownloadAsync(ReleaseInfo release, Action<double> progress)
    {
        if (release == null || !IsInstallerName(release.InstallerName, Platform) || !IsTrustedUrl(release.InstallerUrl, "releases/download"))
            throw new InvalidOperationException("untrusted update link");
        var expected = release.Sha256 ?? await ChecksumAsync(release);
        if (expected == null) throw new InvalidOperationException("the release has no SHA-256 checksum");
        var dir = ProjectSettings.GlobalizePath("user://updates");
        Directory.CreateDirectory(dir);
        CleanUp(dir);
        var path = Path.Combine(dir, release.InstallerName);
        var part = path + ".part";
        try
        {
            using var resp = await Http.GetAsync(release.InstallerUrl, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();
            if (resp.RequestMessage?.RequestUri?.Scheme != Uri.UriSchemeHttps) throw new InvalidOperationException("download left HTTPS");
            var total = resp.Content.Headers.ContentLength ?? release.InstallerSize;
            if (total > MaxInstallerBytes || release.InstallerSize > MaxInstallerBytes) throw new InvalidOperationException("download too large");
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long done = 0;
            await using (var src = await resp.Content.ReadAsStreamAsync())
            await using (var dst = File.Create(part))
            {
                var buffer = new byte[81920]; int n;
                while ((n = await src.ReadAsync(buffer)) > 0)
                {
                    done += n;
                    if (done > MaxInstallerBytes) throw new InvalidOperationException("download too large");
                    sha.AppendData(buffer, 0, n);
                    await dst.WriteAsync(buffer.AsMemory(0, n));
                    if (total > 0) progress?.Invoke(Math.Min(1, (double)done / total));
                }
            }
            if (release.InstallerSize > 0 && done != release.InstallerSize) throw new InvalidOperationException("download incomplete");
            var actual = Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant();
            if (actual != expected) throw new InvalidOperationException("checksum mismatch: the download is not the published installer");
            File.Move(part, path, true);
            return path;
        }
        catch
        {
            try { if (File.Exists(part)) File.Delete(part); } catch (IOException) { }
            throw;
        }
    }

    /// <summary>The installer's SHA-256 from the release's "<installer>.sha256" file (sha256sum format), or null.</summary>
    static async Task<string> ChecksumAsync(ReleaseInfo release)
    {
        if (!IsTrustedUrl(release.ChecksumUrl, "releases/download")) return null;
        using var resp = await Http.GetAsync(release.ChecksumUrl, HttpCompletionOption.ResponseHeadersRead);
        if (!resp.IsSuccessStatusCode || (resp.Content.Headers.ContentLength ?? 0) > 4096) return null;
        return ParseChecksum(await resp.Content.ReadAsStringAsync(), release.InstallerName);
    }

    /// <summary>Reads "hash  name" or a bare hash; the name, when present, must be the installer's.</summary>
    public static string ParseChecksum(string text, string installer)
    {
        foreach (var line in (text ?? "").Split('\n'))
        {
            var parts = line.Trim().Split((char[])null, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;
            var hex = parts[0].ToLowerInvariant();
            if (!Sha256Pattern.IsMatch(hex)) continue;
            if (parts.Length == 1 || parts[1].TrimStart('*') == installer) return hex;
        }
        return null;
    }

    /// <summary>Old installers and broken partial downloads are removed before a new one comes in.</summary>
    static void CleanUp(string dir)
    {
        foreach (var f in Directory.EnumerateFiles(dir))
        {
            var name = Path.GetFileName(f);
            if (!name.EndsWith(".part") && !IsInstallerName(name)) continue;
            try { File.Delete(f); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// Install the downloaded package. Windows: start the installer silently; it closes the game, updates and starts it
    /// again. macOS: start a script that waits for the game to quit, swaps the app bundle and opens the new one. False
    /// means the player has to install by hand (see <see cref="ManualKey"/>); the download folder is then opened.
    /// </summary>
    public static bool RunInstaller(string path)
    {
        var dir = ProjectSettings.GlobalizePath("user://updates");
        var name = Path.GetFileName(path);
        if (!IsInstallerName(name, Platform) || Path.GetFullPath(Path.GetDirectoryName(path) ?? "") != Path.GetFullPath(dir) || !File.Exists(path)) return false;
        var ok = OS.GetName() switch
        {
            "Windows" => OS.CreateProcess(path, new[] { "/SILENT", "/SP-", "/NOCANCEL", "/CLOSEAPPLICATIONS", "/RESTARTAPPLICATIONS" }) > 0,
            "macOS" => SwapMacBundle(path, dir),
            _ => false,
        };
        if (!ok) OS.ShellOpen(dir);
        return ok;
    }

    /// <summary>Text key for "install it yourself", per platform.</summary>
    public static string ManualKey => Platform == UpdatePlatform.MacOS ? "info_manual_mac" : "info_manual";

    /// <summary>
    /// The .app bundle that holds a macOS executable (".../Name.app/Contents/MacOS/Name"), or null when the game does
    /// not run from a bundle. A bundle that macOS runs from a random read-only copy (App Translocation, when the app was
    /// opened straight from Downloads) also gives null: it cannot be replaced.
    /// </summary>
    public static string MacBundle(string executable)
    {
        if (string.IsNullOrEmpty(executable)) return null;
        var macos = Path.GetDirectoryName(executable);
        var contents = Path.GetDirectoryName(macos ?? "");
        var app = Path.GetDirectoryName(contents ?? "");
        if (Path.GetFileName(macos) != "MacOS" || Path.GetFileName(contents) != "Contents" || app == null || !app.EndsWith(".app", StringComparison.Ordinal)) return null;
        if (app.Contains("/AppTranslocation/", StringComparison.Ordinal)) return null;
        return app;
    }

    static bool SwapMacBundle(string zip, string dir)
    {
        var app = MacBundle(OS.GetExecutablePath());
        if (app == null || !CanWrite(Path.GetDirectoryName(app))) return false;
        var script = Path.Combine(dir, "apply-update.sh");
        File.WriteAllText(script, MacScript);
        // the paths go in as arguments, never into the script text
        return OS.CreateProcess("/bin/sh", new[] { script, OS.GetProcessId().ToString(), zip, app }) > 0;
    }

    static bool CanWrite(string folder)
    {
        try
        {
            var probe = Path.Combine(folder, $".dca-write-test-{OS.GetProcessId()}");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch (Exception) { return false; }
    }

    /// <summary>Waits for the game (pid $1) to quit, unpacks the zip ($2) next to the bundle ($3), swaps it in and opens it.
    /// Any failure puts the old bundle back and opens that one. Output goes to update.log in the download folder.</summary>
    const string MacScript = """
    #!/bin/sh
    pid="$1"; zip="$2"; app="$3"
    exec >>"$(dirname "$zip")/update.log" 2>&1
    echo "update $(date): $zip -> $app"
    i=0
    while kill -0 "$pid" 2>/dev/null; do
      i=$((i + 1)); if [ "$i" -gt 600 ]; then echo "game did not quit"; exit 1; fi
      sleep 0.2
    done
    stage="$(dirname "$app")/.DatacenterArchitect-update-$$"
    fail() { echo "failed: $1"; if [ ! -d "$app" ] && [ -d "$stage/old.app" ]; then mv "$stage/old.app" "$app"; fi; rm -rf "$stage"; open "$app"; exit 1; }
    rm -rf "$stage"; mkdir "$stage" || fail "no staging folder"
    /usr/bin/ditto -x -k "$zip" "$stage/new" || fail "unzip"
    new="$(find "$stage/new" -maxdepth 1 -name '*.app' -type d | head -n 1)"
    [ -n "$new" ] && [ -d "$new/Contents/MacOS" ] || fail "no app in the package"
    mv "$app" "$stage/old.app" || fail "cannot move the old app"
    mv "$new" "$app" || fail "cannot move the new app"
    /usr/bin/xattr -dr com.apple.quarantine "$app" 2>/dev/null
    rm -rf "$stage" "$zip"
    echo "done"
    open "$app"
    """;
}

using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using FieldGuide.Core;

namespace FieldGuide.Adapters;

/// <summary>
/// The downloaded copy of the site, in BepInEx/FieldGuide/site with its build in version.txt. Downloads run on a
/// background task; the session reads <see cref="State"/> each frame. A download is checked against the SHA-256 the
/// site publishes before it replaces the copy in place, so a broken download never leaves a broken copy.
/// </summary>
internal sealed class OfflineCopy
{
    private const string InfoUrl = GuideView.OnlineBase + "/offline/version.json";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(30) };

    private readonly string _folder;
    private readonly Action<string> _log;
    private readonly object _gate = new();
    private OfflineState _state;
    private int _isDownloading;
    private int _hasCheckedSite;

    public OfflineCopy(string folder, Action<string> log)
    {
        _folder = folder;
        _log = log;
        _state = ReadDisk();
    }

    public string SiteFolder => Path.Combine(_folder, "site");

    public OfflineState State
    {
        get
        {
            lock (_gate) return _state;
        }
    }

    private string VersionFile => Path.Combine(_folder, "version.txt");

    /// <summary>Asks the site once per game start what its offline copy weighs and whether ours is older.</summary>
    public void CheckSite()
    {
        if (Interlocked.Exchange(ref _hasCheckedSite, 1) == 1) return;

        Task.Run(async () =>
        {
            try
            {
                var site = await FetchInfo();

                lock (_gate) _state = _state.WithSiteBundle(site);
            }
            catch (Exception ex)
            {
                _log("could not read the site's offline version, the download button shows no size: " + ex.Message);
            }
        });
    }

    public void Download()
    {
        if (Interlocked.Exchange(ref _isDownloading, 1) == 1) return;

        var hadCopy = State.HasCopy;

        Set(new OfflineState.Downloading(0, hadCopy));
        Task.Run(async () =>
        {
            try
            {
                var version = await DownloadAndInstall();

                Set(new OfflineState.Ready(version, 0));
                _log("offline copy " + version + " installed");
            }
            catch (Exception ex)
            {
                Set(new OfflineState.Failed(ex.Message, hadCopy));
                _log("offline download failed: " + ex);
            }
            finally
            {
                Interlocked.Exchange(ref _isDownloading, 0);
            }
        });
    }

    private OfflineState ReadDisk()
    {
        try
        {
            if (File.Exists(VersionFile) && File.Exists(Path.Combine(SiteFolder, "index.html"))) return new OfflineState.Ready(File.ReadAllText(VersionFile).Trim(), 0);
        }
        catch (Exception ex)
        {
            _log("could not read the offline copy's version, treating it as missing: " + ex.Message);
        }

        return new OfflineState.Missing(0);
    }

    private void Set(OfflineState state)
    {
        lock (_gate) _state = state;
    }

    private async Task<BundleInfo> FetchInfo()
    {
        var json = await Http.GetStringAsync(InfoUrl + "?t=" + DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));

        return BundleInfo.TryParse(json, out var info) ? info : throw new InvalidDataException("the site's version.json could not be read");
    }

    private async Task<string> DownloadAndInstall()
    {
        var info = await FetchInfo();
        var zip = Path.Combine(_folder, "download.zip");
        var fresh = Path.Combine(_folder, "site-new");
        var old = Path.Combine(_folder, "site-old");

        Directory.CreateDirectory(_folder);
        await Fetch(Offline.BundleAddress(GuideView.OnlineBase, info), zip, info.Bytes);

        if (!string.Equals(Sha256(zip), info.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("the download does not match the site's checksum");

        DeleteFolder(fresh);
        ZipFile.ExtractToDirectory(zip, fresh);
        File.Delete(zip);
        DeleteFolder(old);

        if (Directory.Exists(SiteFolder)) Directory.Move(SiteFolder, old);

        Directory.Move(fresh, SiteFolder);
        File.WriteAllText(VersionFile, info.Version);
        TryDeleteFolder(old);

        return info.Version;
    }

    private async Task Fetch(string url, string path, long expectedBytes)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? expectedBytes;
        var buffer = new byte[1 << 16];
        var done = 0L;
        var shownPercent = 0;
        int read;

        await using var source = await response.Content.ReadAsStreamAsync();
        await using var target = File.Create(path);

        while ((read = await source.ReadAsync(buffer)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read));
            done += read;

            var percent = Offline.Percent(done, total);

            if (percent == shownPercent) continue;

            shownPercent = percent;
            Set(new OfflineState.Downloading(percent, State.HasCopy));
        }
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();

        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    private static void DeleteFolder(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }

    // The local server may still be sending a file from the old copy; what is left is cleared before the next download.
    private void TryDeleteFolder(string path)
    {
        try
        {
            DeleteFolder(path);
        }
        catch (Exception ex)
        {
            _log("the previous offline copy could not be removed yet: " + ex.Message);
        }
    }
}

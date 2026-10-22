using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FieldGuide.Core;

/// <summary>Which copy of the site the window shows. Auto uses the downloaded copy once there is one.</summary>
public enum SiteSource
{
    Auto,
    Online,
    Offline,
}

/// <summary>The site's offline/version.json: which build the zip next to it holds, its size and its SHA-256.</summary>
public readonly record struct BundleInfo(string Version, long Bytes, string Sha256)
{
    private static readonly Regex VersionField = new("\"version\"\\s*:\\s*\"([^\"]+)\"");
    private static readonly Regex BytesField = new("\"bytes\"\\s*:\\s*(\\d+)");
    private static readonly Regex ShaField = new("\"sha256\"\\s*:\\s*\"([0-9a-f]{64})\"");

    public static bool TryParse(string? json, out BundleInfo info)
    {
        info = default;

        if (json == null) return false;

        var version = VersionField.Match(json);
        var bytes = BytesField.Match(json);
        var sha = ShaField.Match(json);

        if (!version.Success || !bytes.Success || !sha.Success) return false;
        if (!long.TryParse(bytes.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var size)) return false;

        info = new BundleInfo(version.Groups[1].Value, size, sha.Groups[1].Value);

        return true;
    }
}

/// <summary>Where the offline copy stands. Exactly one of these at a time.</summary>
public abstract record OfflineState
{
    private OfflineState()
    {
    }

    /// <summary>No copy on disk. Bytes is the download size once the site has said it, 0 before.</summary>
    public sealed record Missing(long Bytes) : OfflineState;

    public sealed record Downloading(int Percent) : OfflineState;

    /// <summary>A copy is on disk. NewerBytes is the size of a newer one on the site, 0 when there is none.</summary>
    public sealed record Ready(string Version, long NewerBytes) : OfflineState;

    /// <summary>The last download failed; the copy before it, if any, is still in place.</summary>
    public sealed record Failed(string Reason, bool KeptCopy) : OfflineState;

    public bool HasCopy => this is Ready || this is Failed { KeptCopy: true };

    /// <summary>After the site's version.json is read: a copy older than the site's build offers an update.</summary>
    public OfflineState WithSiteBundle(BundleInfo site) => this switch
    {
        Missing => new Missing(site.Bytes),
        Ready ready => ready with { NewerBytes = ready.Version == site.Version ? 0 : site.Bytes },
        _ => this,
    };
}

public static class Offline
{
    /// <summary>
    /// The address the window loads pages from: the local copy's server, or the live site. Null when the player chose
    /// Offline and there is no copy yet, so the window asks for the download instead of going online.
    /// </summary>
    public static string? BaseFor(SiteSource source, string? localBase, string onlineBase) => source switch
    {
        SiteSource.Online => onlineBase,
        SiteSource.Offline => localBase,
        _ => localBase ?? onlineBase,
    };

    /// <summary>"96 MB": what the download button says it will cost.</summary>
    /// <summary>
    /// The bundle is asked for by its checksum. Cloudflare keeps a zip for hours, so a plain address can hand back the
    /// copy from an earlier build of the site, which then fails the check against the current version.json.
    /// </summary>
    public static string BundleAddress(string siteBase, BundleInfo bundle) => siteBase + "/offline/field-guide-site.zip?v=" + bundle.Sha256;

    public static string Megabytes(long bytes) => Math.Max(1, (long)Math.Round(bytes / 1_000_000.0)).ToString(CultureInfo.InvariantCulture) + " MB";

    /// <summary>The download as a whole percent, never 100 until it is done.</summary>
    public static int Percent(long done, long total) => total <= 0 ? 0 : (int)Math.Clamp(done * 100 / total, 0, 99);
}

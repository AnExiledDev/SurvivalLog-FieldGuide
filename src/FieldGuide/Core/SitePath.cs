using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FieldGuide.Core;

/// <summary>What a request to the local copy of the site resolves to.</summary>
public abstract record SiteFile
{
    private SiteFile()
    {
    }

    /// <summary>A file under the site folder, as a relative path with forward slashes.</summary>
    public sealed record Found(string RelativePath) : SiteFile;

    public sealed record NotFound : SiteFile;

    /// <summary>A path that tries to leave the site folder, or one that cannot be a file name.</summary>
    public sealed record Refused : SiteFile;
}

/// <summary>
/// Maps a request path to a file of the downloaded site, the way the live server does: "/en/items/x/" and "/en/items/x"
/// are that page's index.html. Nothing outside the site folder is ever reachable.
/// </summary>
public static class SitePath
{
    private static readonly char[] BadCharacters = { '\\', ':', '\0', '*', '?', '"', '<', '>', '|' };

    public static SiteFile Resolve(string target, Func<string, bool> isFile)
    {
        var path = target.Split('?', '#')[0];

        if (!path.StartsWith("/", StringComparison.Ordinal)) return new SiteFile.Refused();

        var segments = Uri.UnescapeDataString(path).Split('/').Skip(1).ToList();

        if (segments.Any(IsUnsafe)) return new SiteFile.Refused();

        var wantsFolder = segments.Count == 0 || segments[^1] == "";
        var parts = segments.Where(s => s != "").ToList();

        foreach (var candidate in Candidates(parts, wantsFolder))
        {
            if (isFile(candidate)) return new SiteFile.Found(candidate);
        }

        return new SiteFile.NotFound();
    }

    private static IEnumerable<string> Candidates(List<string> parts, bool wantsFolder)
    {
        var joined = string.Join("/", parts);
        var index = joined == "" ? "index.html" : joined + "/index.html";

        if (wantsFolder)
        {
            yield return index;

            yield break;
        }

        yield return joined;

        if (Path.GetExtension(parts[^1]) == "") yield return index;
    }

    private static bool IsUnsafe(string segment) => segment is "." or ".." || segment.IndexOfAny(BadCharacters) >= 0;
}

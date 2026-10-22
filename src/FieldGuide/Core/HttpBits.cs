using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace FieldGuide.Core;

/// <summary>The first line of a request to the local server: "GET /en/ HTTP/1.1".</summary>
public readonly record struct RequestLine(string Method, string Target)
{
    public bool IsHead => Method == "HEAD";

    public bool IsReadable => Method is "GET" or "HEAD";

    public static bool TryParse(string? line, out RequestLine request)
    {
        request = default;

        var parts = line?.Split(' ');

        if (parts is not { Length: 3 } || !parts[2].StartsWith("HTTP/", StringComparison.Ordinal) || parts[1] == "") return false;

        request = new RequestLine(parts[0], parts[1]);

        return true;
    }
}

public static class HttpBits
{
    private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".js"] = "text/javascript; charset=utf-8",
        [".mjs"] = "text/javascript; charset=utf-8",
        [".json"] = "application/json; charset=utf-8",
        [".xml"] = "application/xml; charset=utf-8",
        [".txt"] = "text/plain; charset=utf-8",
        [".svg"] = "image/svg+xml",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp",
        [".avif"] = "image/avif",
        [".gif"] = "image/gif",
        [".ico"] = "image/x-icon",
        [".woff"] = "font/woff",
        [".woff2"] = "font/woff2",
        // Search loads its index as WebAssembly, which the browser only compiles when served with this type.
        [".wasm"] = "application/wasm",
    };

    public static string ContentType(string path) => Types.TryGetValue(Path.GetExtension(path), out var type) ? type : "application/octet-stream";

    public static string Head(int status, string contentType, long length)
    {
        var reason = status switch
        {
            200 => "OK",
            400 => "Bad Request",
            403 => "Forbidden",
            404 => "Not Found",
            405 => "Method Not Allowed",
            _ => "Error",
        };

        return string.Create(CultureInfo.InvariantCulture,
            $"HTTP/1.1 {status} {reason}\r\nContent-Type: {contentType}\r\nContent-Length: {length}\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n");
    }
}

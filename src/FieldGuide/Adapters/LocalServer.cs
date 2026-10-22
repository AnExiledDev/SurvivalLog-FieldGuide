using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using FieldGuide.Core;

namespace FieldGuide.Adapters;

/// <summary>
/// Serves the offline copy to the game's web view on 127.0.0.1, on a port the system picks, so the site's pages, search
/// and links work as they do online. Only this machine can reach it, and only files inside the site folder are served.
/// </summary>
internal sealed class LocalServer
{
    private const int MaxHeaderLines = 100;

    private readonly string _root;
    private readonly Action<string> _log;
    private TcpListener? _listener;

    public LocalServer(string root, Action<string> log)
    {
        _root = Path.GetFullPath(root);
        _log = log;
    }

    /// <summary>"http://127.0.0.1:port" once started, null before.</summary>
    public string? Base { get; private set; }

    public void Start()
    {
        if (_listener != null) return;

        var listener = new TcpListener(IPAddress.Loopback, 0);

        listener.Start();
        _listener = listener;
        Base = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
        new Thread(() => Accept(listener)) { IsBackground = true, Name = "Field Guide server" }.Start();
        _log("offline copy served at " + Base);
    }

    private void Accept(TcpListener listener)
    {
        while (true)
        {
            TcpClient client;

            try
            {
                client = listener.AcceptTcpClient();
            }
            catch (Exception ex)
            {
                _log("the offline server stopped: " + ex.Message);

                return;
            }

            ThreadPool.QueueUserWorkItem(_ => ServeSafely(client));
        }
    }

    private void ServeSafely(TcpClient client)
    {
        try
        {
            using (client) Serve(client.GetStream());
        }
        catch (Exception)
        {
            // A browser that drops a connection mid-file is normal; the page asks again if it still wants it.
        }
    }

    private void Serve(NetworkStream stream)
    {
        stream.ReadTimeout = 5000;

        using var reader = new StreamReader(stream, Encoding.ASCII, false, 4096, leaveOpen: true);
        var line = reader.ReadLine();

        for (var i = 0; i < MaxHeaderLines && !string.IsNullOrEmpty(reader.ReadLine()); i++)
        {
        }

        if (!RequestLine.TryParse(line, out var request))
        {
            Reply(stream, 400, false);

            return;
        }

        if (!request.IsReadable)
        {
            Reply(stream, 405, request.IsHead);

            return;
        }

        switch (SitePath.Resolve(request.Target, IsSiteFile))
        {
            case SiteFile.Found found:
                SendFile(stream, 200, FullPath(found.RelativePath), request.IsHead);
                break;
            case SiteFile.NotFound when IsSiteFile("404.html"):
                SendFile(stream, 404, FullPath("404.html"), request.IsHead);
                break;
            case SiteFile.NotFound:
                Reply(stream, 404, request.IsHead);
                break;
            case SiteFile.Refused:
                Reply(stream, 403, request.IsHead);
                break;
        }
    }

    private bool IsSiteFile(string relativePath)
    {
        var path = FullPath(relativePath);

        return path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && File.Exists(path);
    }

    private string FullPath(string relativePath) => Path.GetFullPath(Path.Combine(_root, relativePath));

    private static void SendFile(Stream stream, int status, string path, bool isHead)
    {
        using var file = File.OpenRead(path);

        Write(stream, HttpBits.Head(status, HttpBits.ContentType(path), file.Length));

        if (!isHead) file.CopyTo(stream);
    }

    private static void Reply(Stream stream, int status, bool isHead)
    {
        var body = Encoding.ASCII.GetBytes(status + "\n");

        Write(stream, HttpBits.Head(status, "text/plain; charset=utf-8", body.Length));

        if (!isHead) stream.Write(body, 0, body.Length);
    }

    private static void Write(Stream stream, string head)
    {
        var bytes = Encoding.ASCII.GetBytes(head);

        stream.Write(bytes, 0, bytes.Length);
    }
}

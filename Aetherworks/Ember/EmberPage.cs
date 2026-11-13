using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Aetherworks.Ember;

internal sealed class EmberPage : IDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stop = new();
    private readonly byte[] page;
    public string Url { get; }

    public EmberPage(string directory)
    {
        page = File.ReadAllBytes(Path.Combine(directory, "ember.html"));
        listener.Start();
        Url = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/";
        _ = Accept(stop.Token);
    }

    private async Task Accept(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
                _ = Serve(await listener.AcceptTcpClientAsync(token).ConfigureAwait(false), token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (SocketException) when (token.IsCancellationRequested) { }
    }

    private async Task Serve(TcpClient client, CancellationToken token)
    {
        using (client)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            try
            {
                var stream = client.GetStream();
                var bytes = new byte[8192];
                int count = 0;
                while (count < bytes.Length)
                {
                    int read = await stream.ReadAsync(bytes.AsMemory(count), timeout.Token).ConfigureAwait(false);
                    if (read == 0) return;
                    count += read;
                    if (bytes.AsSpan(0, count).IndexOf("\r\n\r\n"u8) >= 0) break;
                }
                string[] headers = Encoding.ASCII.GetString(bytes, 0, count).Split("\r\n");
                if (!headers.Any(h => string.Equals(h, $"Host: {new Uri(Url).Authority}", StringComparison.OrdinalIgnoreCase))) return;
                var request = headers[0].Split(' ');
                if (request.Length != 3 || request[0] != "GET") return;
                string path = request[1];
                bool valid = path == "/" || path == "/?HOST_PORT=ws://127.0.0.1:10501/ws&mode=stats";
                string status = valid ? "200 OK" : "404 Not Found";
                string location = "";
                if (path.StartsWith("/img/", StringComparison.Ordinal) || path == "/logs/CHANGELOG.md" || path == "/favicon.ico")
                {
                    status = "302 Found";
                    location = $"Location: https://proxy.iinact.com/overlay/ember_overlay{path}\r\n";
                }
                var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\n{location}Content-Type: text/html; charset=utf-8\r\nContent-Length: {(valid ? page.Length : 0)}\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(header, timeout.Token).ConfigureAwait(false);
                if (valid) await stream.WriteAsync(page, timeout.Token).ConfigureAwait(false);
            }
            catch (IOException) { }
            catch (OperationCanceledException) { }
        }
    }

    public void Dispose()
    {
        stop.Cancel();
        listener.Stop();
        stop.Dispose();
    }
}

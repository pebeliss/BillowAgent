using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BillowAgent;

public class BrowserServer : IDisposable
{
    private readonly string _prefix; // e.g., http://localhost:57451/ws/
    private readonly HttpListener _listener = new();
    private readonly Storage _storage;
    private CancellationTokenSource? _cts;

    public BrowserServer(string prefix, Storage storage)
    { _prefix = prefix; _storage = storage; }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _listener.Prefixes.Add(_prefix);
        _listener.Start();
        _ = AcceptLoop(_cts.Token);
    }

    private async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var ctx = await _listener.GetContextAsync();
            if (ctx.Request.IsWebSocketRequest)
            {
                var wsCtx = await ctx.AcceptWebSocketAsync(null);
                _ = HandleSocket(wsCtx.WebSocket, ct);
            }
            else
            {
                ctx.Response.StatusCode = 400; ctx.Response.Close();
            }
        }
    }


    private async Task HandleSocket(WebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[4096];
        while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            if (result.MessageType == WebSocketMessageType.Close) break;
            var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
            try
            {
                var evt = JsonSerializer.Deserialize<BrowserEventDto>(json);
                if (evt != null && evt.type == "tab")
                {
                    _storage.RecordBrowserEvent(DateTime.UtcNow, evt.url!, evt.title!);
                }
            }
            catch { /* ignore */ }
        }
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
    }


    public void Dispose()
    {
        _cts?.Cancel();
        if (_listener.IsListening) _listener.Stop();
        _listener.Close();
    }


    private record BrowserEventDto(string type, string? url, string? title);
}
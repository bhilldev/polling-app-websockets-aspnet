using System.Net.WebSockets;
using System.Text;
using System.Collections.Concurrent;

namespace PollingAppWebSockets.Services;

public class PollWebSocketHandler
{
    private readonly ConcurrentBag<WebSocket> _sockets = new();
    private readonly ConcurrentDictionary<string, int> _choiceCounts = new();
    private readonly ConcurrentDictionary<WebSocket, string> _clientVotes = new();

    public async Task HandleAsync(HttpContext context, CancellationToken shutdownToken)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        _sockets.Add(socket);

        var buffer = new byte[1024];

        while (socket.State == WebSocketState.Open && !shutdownToken.IsCancellationRequested)
        {
            var result = await socket.ReceiveAsync(buffer, CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
                break;

            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

            if (message.StartsWith("choice:"))
            {
                var newChoice = message.Split(':')[1];

                if (_clientVotes.TryGetValue(socket, out var oldChoice))
                {
                    if (oldChoice != newChoice)
                    {
                        _choiceCounts.AddOrUpdate(oldChoice, 0, (_, count) => Math.Max(count - 1, 0));
                        _clientVotes[socket] = newChoice;
                        _choiceCounts.AddOrUpdate(newChoice, 1, (_, count) => count + 1);
                    }
                }
                else
                {
                    _clientVotes[socket] = newChoice;
                    _choiceCounts.AddOrUpdate(newChoice, 1, (_, count) => count + 1);
                }

                var payload = $"total:{_choiceCounts.Values.Sum()}|"
                            + string.Join(",", _choiceCounts.Select(kvp => $"{kvp.Key}:{kvp.Value}"));

                var bytes = Encoding.UTF8.GetBytes(payload);

                foreach (var ws in _sockets.Where(s => s.State == WebSocketState.Open))
                {
                    await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
        }

        if (_clientVotes.TryRemove(socket, out var removedChoice))
        {
            _choiceCounts.AddOrUpdate(removedChoice, 0, (_, count) => Math.Max(count - 1, 0));
        }
    }

    public void CloseAll()
    {
        foreach (var ws in _sockets.Where(s => s.State == WebSocketState.Open))
        {
            ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server shutting down", CancellationToken.None).Wait();
        }
    }
}


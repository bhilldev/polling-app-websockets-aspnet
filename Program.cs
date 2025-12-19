using System.Net.WebSockets;
using System.Text;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

/* ----------------------------
   Static files (HTML/CSS/JS)
-----------------------------*/
app.UseDefaultFiles();
app.UseStaticFiles();

/* ----------------------------
   WebSockets
-----------------------------*/
app.UseWebSockets();

// Track connected clients
var sockets = new ConcurrentBag<WebSocket>();

// Track poll counts
var choiceCounts = new ConcurrentDictionary<string, int>();

// Track each client’s current vote
var clientVotes = new ConcurrentDictionary<WebSocket, string>();

// Token that triggers when app is stopping
var shutdownToken = app.Lifetime.ApplicationStopping;

// Ensure all WebSockets close on shutdown
shutdownToken.Register(() =>
{
    foreach (var ws in sockets.Where(s => s.State == WebSocketState.Open))
    {
        ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server shutting down", CancellationToken.None).Wait();
    }
});

app.Map("/poll", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    sockets.Add(socket);

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

            if (clientVotes.TryGetValue(socket, out var oldChoice))
            {
                if (oldChoice != newChoice)
                {
                    choiceCounts.AddOrUpdate(oldChoice, 0, (_, count) => Math.Max(count - 1, 0));
                    clientVotes[socket] = newChoice;
                    choiceCounts.AddOrUpdate(newChoice, 1, (_, count) => count + 1);
                }
            }
            else
            {
                clientVotes[socket] = newChoice;
                choiceCounts.AddOrUpdate(newChoice, 1, (_, count) => count + 1);
            }

            // Build response payload: "total:5|1:2,2:1,3:2"
            var payload = $"total:{choiceCounts.Values.Sum()}|"
                        + string.Join(",", choiceCounts.Select(kvp => $"{kvp.Key}:{kvp.Value}"));

            var bytes = Encoding.UTF8.GetBytes(payload);

            foreach (var ws in sockets.Where(s => s.State == WebSocketState.Open))
            {
                await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }

    // Clean up on disconnect
    if (clientVotes.TryRemove(socket, out var removedChoice))
    {
        choiceCounts.AddOrUpdate(removedChoice, 0, (_, count) => Math.Max(count - 1, 0));
    }
});
app.Run();


using System.Net.WebSockets;
using System.Text;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

/* ----------------------------
   Static files (HTML/CSS/JS)
-----------------------------*/
app.UseDefaultFiles();   // loads index.html automatically
app.UseStaticFiles();

/* ----------------------------
   WebSockets
-----------------------------*/
app.UseWebSockets();

// Track connected clients
var sockets = new ConcurrentBag<WebSocket>();

// Track poll counts
var choiceCounts = new ConcurrentDictionary<string, int>();

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

    while (socket.State == WebSocketState.Open)
    {
        var result = await socket.ReceiveAsync(buffer, CancellationToken.None);

        if (result.MessageType == WebSocketMessageType.Close)
            break;

        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

        // Expected message: "choice:1"
        if (message.StartsWith("choice:"))
        {
            var choice = message.Split(':')[1];

            // Increment counter safely
            choiceCounts.AddOrUpdate(choice, 1, (_, count) => count + 1);

            // Build response payload: "1:3,2:5,3:2"
            var payload = string.Join(",",
                choiceCounts.Select(kvp => $"{kvp.Key}:{kvp.Value}")
            );

            var bytes = Encoding.UTF8.GetBytes(payload);

            // Broadcast to all connected clients
            foreach (var ws in sockets.Where(s => s.State == WebSocketState.Open))
            {
                await ws.SendAsync(
                    bytes,
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );
            }
        }
    }
});

app.Run();

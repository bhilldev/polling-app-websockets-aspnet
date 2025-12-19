using PollingAppWebSockets.Services;
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseWebSockets();

var handler = new PollWebSocketHandler();
var shutdownToken = app.Lifetime.ApplicationStopping;

shutdownToken.Register(() => handler.CloseAll());

app.Map("/poll", async context =>
{
    await handler.HandleAsync(context, shutdownToken);
});

app.Run();


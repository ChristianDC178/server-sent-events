using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

// Add CORS to allow requests from React client
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy",
        builder => builder
            .WithOrigins("http://localhost:3000") // React app URL
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

var app = builder.Build();
app.UseCors("CorsPolicy");

// Store connected clients
var clients = new ConcurrentDictionary<string, StreamWriter>();

// Wrap the Task.Run call in an async method and await it to fix CS4014
Task.Run(async () =>
{
    while (true)
    {
        if (clients.Count > 0)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var message = $"Server time: {timestamp}";

            foreach (var client in clients.ToArray())
            {
                try
                {
                    await client.Value.WriteAsync($"data: {message}\n\n");
                    await client.Value.FlushAsync();
                }
                catch
                {
                    // Remove client if we can't write to it
                    clients.TryRemove(client.Key, out _);
                }
            }
        }

        await Task.Delay(5000); // Wait 5 seconds
    }
});

// Call the async method and await it
//await StartAutomaticMessageGenerator();

app.MapGet("/sse", async (HttpContext context) =>
{
    var clientId = Guid.NewGuid().ToString();

    if (context.Response.Headers != null)
    {
        context.Response.Headers.Add("Content-Type", "text/event-stream");
        context.Response.Headers.Add("Cache-Control", "no-cache");
        context.Response.Headers.Add("Connection", "keep-alive");
    }

    // Get stream writer for the response
    var response = context.Response;
    var writer = new StreamWriter(response.Body);

    // Register client
    clients[clientId] = writer;

    try
    {
        // Send initial message
        await writer.WriteAsync($"data: Connected with ID {clientId}\n\n");
        await writer.FlushAsync();

        // Keep connection open until client disconnects
        var tcs = new TaskCompletionSource<bool>();
        context.RequestAborted.Register(() => tcs.TrySetResult(true));
        await tcs.Task;
    }
    finally
    {
        //clients.TryRemove(clientId, out _);
    }
});

// Endpoint to send messages to all clients
app.MapPost("/broadcast", async (HttpContext context) =>
{
    var request = await new StreamReader(context.Request.Body).ReadToEndAsync();
    var message = request ?? "No message provided";

    // Send message to all connected clients
    foreach (var client in clients)
    {
        try
        {
            await client.Value.WriteAsync($"data: {message}\n\n");
            await client.Value.FlushAsync();
        }
        catch
        {
            // Remove client if we can't write to it
            clients.TryRemove(client.Key, out _);
        }
    }

    return Results.Ok($"Message broadcast to {clients.Count} clients");
});

app.Run("http://localhost:5059");
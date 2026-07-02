using System.Text.Json;
using cobblersBackend.Hubs;
using cobblersBackend.Services;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddScoped<ExecutorService>();
builder.Services.AddScoped<IExecuteResultClassifier,JavaExecuteResultClassifier>();

builder.Services.AddHttpClient<IPistonClient, PistonClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Piston:BaseUrl"] ?? "http://localhost:2000/");
});
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    });

// Live rooms (teacher↔student) — in-memory store + SignalR hub. The store is a
// singleton so every request/connection shares the same room state.
builder.Services.AddSingleton<SessionStore>();
builder.Services.AddSignalR();

builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();
app.MapControllers();
app.MapHub<SessionHub>("/hub");

// run
app.Run();

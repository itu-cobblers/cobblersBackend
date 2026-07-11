using System.Text.Json;
using cobblersBackend.Data;
using cobblersBackend.Hubs;
using cobblersBackend.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Add services to the container.
builder.Services.AddScoped<ExecutorService>();
builder.Services.AddScoped<IExecuteResultClassifier,JavaExecuteResultClassifier>();
// Stateless rule evaluator for Task.GradingJson; the no-arg construction means
// no custom (slug-keyed) checks are registered — none are needed today.
builder.Services.AddSingleton<ITaskGrader>(_ => new TaskGrader());
builder.Services.AddScoped<ITaskSetService, TaskSetService>();

builder.Services.AddHttpClient<IPistonClient, PistonClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Piston:BaseUrl"] ?? "http://localhost:2000/");
});
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddOpenApi();
}
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    });

// Live rooms (teacher↔student) — in-memory store + SignalR hub. The store is a
// singleton so every request/connection shares the same room state.
builder.Services.AddSingleton<SessionStore>();
builder.Services.AddSignalR();

builder.Services.AddDbContext<CobblersDbContext>(OptionsBuilder =>
{
    OptionsBuilder.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();
app.MapHub<SessionHub>("/hub");

// run
app.Run();

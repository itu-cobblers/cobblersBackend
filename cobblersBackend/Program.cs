using System.Text.Json;
using cobblersBackend.Data;
using cobblersBackend.Hubs;
using cobblersBackend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                       ?? throw new InvalidOperationException("ConnectionStrings__DefaultConnection not set");

// Add services to the container.
builder.Services.AddScoped<ExecutorService>();
builder.Services.AddScoped<IExecuteResultClassifier,JavaExecuteResultClassifier>();
// Stateless rule evaluator for Assignment.GradingJson; the no-arg construction
// means no custom (slug-keyed) checks are registered — none are needed today.
builder.Services.AddSingleton<IAssignmentGrader>(_ => new AssignmentGrader());
builder.Services.AddScoped<IAssignmentSetService, AssignmentSetService>();

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

builder.Services.AddDbContext<CobblersDbContext>(options =>
{
    options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();

    // SQL logging for debugging — Development only; too noisy for production.
    if (builder.Environment.IsDevelopment())
    {
        options.LogTo(Console.WriteLine, LogLevel.Information)
               .EnableSensitiveDataLogging();
    }
});
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();

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

public partial class Program { }


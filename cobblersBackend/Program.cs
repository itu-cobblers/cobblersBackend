using cobblersBackend.Hubs;
using cobblersBackend.Services;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddScoped<IExecutorService,ExecutorService>();
builder.Services.AddHttpClient<IPistonClient, PistonClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Piston:BaseUrl"] ?? "http://localhost:2000/");
});

// for existing REST api.
builder.Services.AddControllers();

// for websocket connection with SignalR 
builder.Services.AddSignalR();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();
app.MapControllers();

//configure websocket.
app.MapHub<ExecutorHub>("/hubs/executor");

// run
app.Run();

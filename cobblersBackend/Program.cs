using cobblersBackend.Services;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHttpClient<IPistonClient, PistonClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Piston:BaseUrl"] ?? "http://localhost:2000/");
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();
app.MapControllers();

// run
app.Run();

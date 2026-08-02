using cobblersBackend.Models;
using cobblersBackend.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace cobblersBackend.Tests.Infrastructure;

/// <summary>
/// Boots the real application — real routing, real model binding, real JSON
/// options, real DI — against the Testcontainers Postgres, with Piston stubbed
/// out. This is the only layer that can prove a route exists, that a service's
/// <c>null</c> becomes a 404, and that the JSON on the wire matches CONTRACT.md;
/// the service tests deliberately stop one level below all three.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public ApiFactory(string connectionString) => _connectionString = connectionString;

    /// <summary>What the stubbed Piston answers. Swap per test if a case needs different output.</summary>
    public PistonExecuteResponse PistonResponse { get; set; } =
        new() { Run = new PistonStage("hi\n", "", "hi\n", 0, null) };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Program throws at startup without this, and it's what points the app
        // at the same container the fixture seeds through.
        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);

        // Not Development: keeps OpenAPI and EF's sensitive-data SQL logging off.
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Registered via AddHttpClient in Program, so remove the typed-client
            // registration wholesale rather than trying to re-point its address.
            services.RemoveAll<IPistonClient>();
            services.AddSingleton<IPistonClient>(new StubPistonClient(this));
        });
    }

    private sealed class StubPistonClient : IPistonClient
    {
        private readonly ApiFactory _factory;
        public StubPistonClient(ApiFactory factory) => _factory = factory;

        public Task<PistonExecuteResponse> ExecuteAsync(string language, IReadOnlyList<PistonFile> files) =>
            Task.FromResult(_factory.PistonResponse);
    }
}

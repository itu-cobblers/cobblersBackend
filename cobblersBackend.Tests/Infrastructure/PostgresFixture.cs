using cobblersBackend.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace cobblersBackend.Tests.Infrastructure;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = 
        new PostgreSqlBuilder("postgres:18-alpine") // mirrors the droplet (18.4)
        .Build();

        private Respawner _respawner = null!;

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        // start db
        await _container.StartAsync();

        // migration
        await using CobblersDbContext ctx = CreateContext();
        await ctx.Database.MigrateAsync();

        // respawn logic
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            TablesToIgnore = ["__EFMigrationsHistory"] // don't destroy migration history
        });
        
    } 
    public async Task DisposeAsync() => await _container.DisposeAsync();

    public CobblersDbContext CreateContext() =>
        new (new DbContextOptionsBuilder<CobblersDbContext>()
             .UseNpgsql(ConnectionString)
             .UseSnakeCaseNamingConvention()
             .Options);

    public async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await _respawner.ResetAsync(conn);
        
    } 
}
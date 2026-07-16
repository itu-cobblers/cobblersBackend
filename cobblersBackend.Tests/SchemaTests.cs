using cobblersBackend.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;


namespace cobblersBackend.Tests;

[Collection("db")]
public sealed class SchemaTests : IAsyncLifetime 
{
    private readonly PostgresFixture _fixture;

    public SchemaTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync() => await _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Migrations_ApplyToEmptyDatabase()
    {
        await using var ctx = _fixture.CreateContext();
        // "None pending" = every defined migration applied — unlike an exact
        // count, this never goes stale when a new migration is added.
        Assert.Empty(await ctx.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task Model_HasNoPendingChanges()
    {
        await using var ctx = _fixture.CreateContext();
        Assert.False(ctx.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task CanInsertAndReadStudent()
    {
        await using (var write = _fixture.CreateContext())
        {
            write.Student.Add(new Data.Entities.Student { Id = "s-1", DisplayName = "Maria"});
            await write.SaveChangesAsync();
        }

        await using var read = _fixture.CreateContext();
        var student = await read.Student.SingleAsync(s => s.Id == "s-1");
        Assert.Equal("Maria", student.DisplayName);
    }
}
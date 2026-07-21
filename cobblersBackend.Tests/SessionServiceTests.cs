using cobblersBackend.Tests.Infrastructure;

namespace cobblersBackend.Tests;

[Collection("db")]
public sealed class SessionServiceTest : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    public SessionServiceTest(PostgresFixture fixture) => _fixture = fixture;
    public async Task InitializeAsync() => await _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    
}
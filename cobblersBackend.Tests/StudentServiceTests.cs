using cobblersBackend.Services;
using cobblersBackend.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace cobblersBackend.Tests;

[Collection("db")]
public class StudentServiceTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public StudentServiceTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync() => await _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    
    [Fact]
    public async Task UpsertStudentAsync_NewStudent_Inserts()
    {
        // Given
        await using (var setup = _fixture.CreateContext())
        {
            var service = new StudentService(setup);
            await service.UpsertStudentAsync("student-1", "Maria");
        }
        // When
        await using var read = _fixture.CreateContext();
        // Then
        Assert.Equal("Maria", (await read.Student.SingleAsync()).DisplayName);
    }

    [Fact]
    public async Task UpsertStudentAsync_ExistingStudentSameName_IsIdempotent()
    {
        // Given
        await using (var setup = _fixture.CreateContext())
        {
            var service = new StudentService(setup);
            await service.UpsertStudentAsync("student-1", "Maria");
            await service.UpsertStudentAsync("student-1", "Maria"); // second attempt
        }
        // When
        await using var read = _fixture.CreateContext();
        // Then
        Assert.Equal(1, await read.Student.CountAsync());
        Assert.Equal("Maria", (await read.Student.SingleAsync()).DisplayName);
    }
    [Fact]
    public async Task UpsertStudentAsync_ExistingStudentNewDisplayName_UpdatesDisplayName()
    {
        // Given
        await using (var setup = _fixture.CreateContext())
        {
            var service = new StudentService(setup);
            await service.UpsertStudentAsync("student-1", "Maria");
            await service.UpsertStudentAsync("student-1", "Marianne");
        }
        // When
        await using var read = _fixture.CreateContext();
        // Then
        Assert.Equal("Marianne", (await read.Student.SingleAsync()).DisplayName);

    }
}
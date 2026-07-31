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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpsertStudentAsync_BlankDisplayName_IsStoredAsIs(string displayName)
    {
        // Given
        await using (var setup = _fixture.CreateContext())
        {
            await new StudentService(setup).UpsertStudentAsync("student-1", displayName);
        }

        // When
        await using var read = _fixture.CreateContext();

        // Then — CHARACTERIZATION. Neither the service nor the DB rejects a blank
        // name, so a student who clears the field lands on the teacher's roster as
        // an empty row. The frontend gates this today (the entry form requires a
        // name); if that gate ever moves server-side, this is the test to flip.
        Assert.Equal(displayName, (await read.Student.SingleAsync()).DisplayName);
    }

    [Fact]
    public async Task UpsertStudentAsync_TwoStudents_DoNotOverwriteEachOther()
    {
        // Given — studentId is a client-generated anon id; two browsers, two rows.
        await using (var setup = _fixture.CreateContext())
        {
            var service = new StudentService(setup);
            await service.UpsertStudentAsync("student-1", "Maria");
            await service.UpsertStudentAsync("student-2", "Maria");   // same name, different id
        }

        // When
        await using var read = _fixture.CreateContext();

        // Then — the upsert keys on id alone; a duplicate display name is fine.
        Assert.Equal(2, await read.Student.CountAsync());
    }

    [Fact]
    public async Task UpsertStudentAsync_RenameThenRenameBack_LandsOnTheLatest()
    {
        // Given — "latest name wins" has to survive a round trip, not just one edit.
        await using (var setup = _fixture.CreateContext())
        {
            var service = new StudentService(setup);
            await service.UpsertStudentAsync("student-1", "Maria");
            await service.UpsertStudentAsync("student-1", "Marianne");
            await service.UpsertStudentAsync("student-1", "Maria");
        }

        // When
        await using var read = _fixture.CreateContext();

        // Then
        Assert.Equal("Maria", (await read.Student.SingleAsync()).DisplayName);
    }
}
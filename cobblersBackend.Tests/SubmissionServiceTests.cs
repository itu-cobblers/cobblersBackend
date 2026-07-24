using cobblersBackend.DTOs;
using cobblersBackend.Data.Entities;
using cobblersBackend.Services;
using cobblersBackend.Tests.Infrastructure;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace cobblersBackend.Tests;

[Collection("db")]
public sealed class SubmissionServiceTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    public SubmissionServiceTests(PostgresFixture fixture) => _fixture = fixture;
    public async Task InitializeAsync() => await _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SubmitAsync_PassingCode_SetPassedTrue()
    {
        // Given
        int assignmentId;
        await using (var setup = _fixture.CreateContext())
        {
            setup.Student.Add(TestData.MakeStudent("student-1"));
            var assignment = TestData.MakeAssignment(AssignmentKind.Code);
            assignment.GradingJson = """{"target":"stdout","op":"containsLine","value":"hi"}""";
            setup.Assignment.Add(assignment);
            await setup.SaveChangesAsync();
            assignmentId = assignment.Id;
        }

        var executor = new FakeExecutorService(new ExecuteResponseDto(ExecuteStatus.SUCCESS, "hi\n", ""));

        // When
        await using var ctx = _fixture.CreateContext();
        var service = new SubmissionService(ctx, executor, new AssignmentGrader());
        var request = new SubmissionRequestDto("student-1", null, JsonSerializer.SerializeToElement("ignored"));
        var result = await service.SubmitAsync(assignmentId, request);

        // Then
        Assert.True(result!.Passed);
        await using var read = _fixture.CreateContext();
        Assert.True((await read.Submission.SingleAsync()).Passed);
    }

    [Fact]
    public async Task SubmitAsync_FailingCode_SetPassedFalse()
    {
        // Given
        int assignmentId;
        await using (var setup = _fixture.CreateContext())
        {
            setup.Student.Add(TestData.MakeStudent("student-1"));
            var assignment = TestData.MakeAssignment(AssignmentKind.Code);
            assignment.GradingJson = """{"target":"stdout","op":"containsLine","value":"goodbye"}""";
            setup.Assignment.Add(assignment);
            await setup.SaveChangesAsync();
            assignmentId = assignment.Id;
        }

        var executor = new FakeExecutorService(new ExecuteResponseDto(ExecuteStatus.SUCCESS, "hi\n", ""));

        // When
        await using var ctx = _fixture.CreateContext();
        var service = new SubmissionService(ctx, executor, new AssignmentGrader());
        var request = new SubmissionRequestDto("student-1", null, JsonSerializer.SerializeToElement("ignored"));
        var result = await service.SubmitAsync(assignmentId, request);

        // Then
        Assert.False(result!.Passed);
        await using var read = _fixture.CreateContext();
        Assert.False((await read.Submission.SingleAsync()).Passed);
    }

    [Fact]
    public async Task SubmitAsync_GradingNull_PassedPersistsNull()
    {
        // Given
        int assignmentId;
        await using (var setup = _fixture.CreateContext())
        {
            setup.Student.Add(TestData.MakeStudent("student-1"));
            var assignment = TestData.MakeAssignment(AssignmentKind.Code);
            assignment.GradingJson = null;
            setup.Assignment.Add(assignment);
            await setup.SaveChangesAsync();
            assignmentId = assignment.Id;
        }

        var executor = new FakeExecutorService(new ExecuteResponseDto(ExecuteStatus.SUCCESS, "hi\n", ""));

        // When
        await using var ctx = _fixture.CreateContext();
        var service = new SubmissionService(ctx, executor, new AssignmentGrader());
        var request = new SubmissionRequestDto("student-1", null, JsonSerializer.SerializeToElement("ignored"));
        var result = await service.SubmitAsync(assignmentId, request);

        // Then
        Assert.Null(result!.Passed);
        await using var read = _fixture.CreateContext();
        Assert.Null((await read.Submission.SingleAsync()).Passed);
    }

    [Theory]
    [InlineData(AssignmentKind.Predict)]
    [InlineData(AssignmentKind.Project)]
    public async Task SubmitAsync_NonCodeKind_ResultAndPassedStayNull(AssignmentKind kind)
    {
        // Given
        int assignmentId;
        await using (var setup = _fixture.CreateContext())
        {
            setup.Student.Add(TestData.MakeStudent("student-1"));
            var assignment = TestData.MakeAssignment(kind);
            assignment.GradingJson = """{"target":"stdout","op":"containsLine","value":"hi"}""";
            setup.Assignment.Add(assignment);
            await setup.SaveChangesAsync();
            assignmentId = assignment.Id;
        }

        var executor = new FakeExecutorService(new ExecuteResponseDto(ExecuteStatus.SUCCESS, "hi\n", ""));

        // When
        await using var ctx = _fixture.CreateContext();
        var service = new SubmissionService(ctx, executor, new AssignmentGrader());
        var request = new SubmissionRequestDto("student-1", null, JsonSerializer.SerializeToElement("ignored"));
        var result = await service.SubmitAsync(assignmentId, request);

        // Then
        Assert.Null(result!.Passed);
        await using var read = _fixture.CreateContext();
        var submission = await read.Submission.SingleAsync();
        Assert.Null(submission.Passed);
        Assert.Null(submission.ResultJson);
    }

    
}
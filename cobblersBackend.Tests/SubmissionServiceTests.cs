using cobblersBackend.DTOs;
using cobblersBackend.Data;
using cobblersBackend.Data.Entities;
using cobblersBackend.Hubs;
using cobblersBackend.Services;
using cobblersBackend.Tests.Infrastructure;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace cobblersBackend.Tests;

/// <summary>
/// The write path: <c>SubmitAsync</c> — validation, session resolution, and the
/// per-kind grading dispatch (STORIES.md S2). Reading submissions back lives in
/// SubmissionHistoryTests (student) and SessionSubmissionsTests (teacher).
/// </summary>
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
        var service = TestServices.Submissions(ctx, executor);
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
        var service = TestServices.Submissions(ctx, executor);
        var request = new SubmissionRequestDto("student-1", null, JsonSerializer.SerializeToElement("ignored"));
        var result = await service.SubmitAsync(assignmentId, request);

        // Then
        Assert.False(result!.Passed);
        await using var read = _fixture.CreateContext();
        Assert.False((await read.Submission.SingleAsync()).Passed);
    }

    [Fact]
    public async Task SubmitAsync_FailingCodeWithMessage_ReturnsAndPersistsFeedback()
    {
        // Given
        int assignmentId;
        await using (var setup = _fixture.CreateContext())
        {
            setup.Student.Add(TestData.MakeStudent("student-1"));
            var assignment = TestData.MakeAssignment(AssignmentKind.Code);
            assignment.GradingJson = """{"target":"stdout","op":"containsLine","value":"goodbye","message":"Print goodbye, not hi."}""";
            setup.Assignment.Add(assignment);
            await setup.SaveChangesAsync();
            assignmentId = assignment.Id;
        }

        var executor = new FakeExecutorService(new ExecuteResponseDto(ExecuteStatus.SUCCESS, "hi\n", ""));

        // When
        await using var ctx = _fixture.CreateContext();
        var service = TestServices.Submissions(ctx, executor);
        var request = new SubmissionRequestDto("student-1", null, JsonSerializer.SerializeToElement("ignored"));
        var result = await service.SubmitAsync(assignmentId, request);

        // Then
        Assert.False(result!.Passed);
        Assert.Equal(new[] { "Print goodbye, not hi." }, result.Feedback);
        await using var read = _fixture.CreateContext();
        var submission = await read.Submission.SingleAsync();
        Assert.NotNull(submission.FeedbackJson);
        Assert.Contains("Print goodbye, not hi.", submission.FeedbackJson);
    }

    [Fact]
    public async Task SubmitAsync_PassingCode_FeedbackStaysNull()
    {
        // Given
        int assignmentId;
        await using (var setup = _fixture.CreateContext())
        {
            setup.Student.Add(TestData.MakeStudent("student-1"));
            var assignment = TestData.MakeAssignment(AssignmentKind.Code);
            assignment.GradingJson = """{"target":"stdout","op":"containsLine","value":"hi","message":"should never surface"}""";
            setup.Assignment.Add(assignment);
            await setup.SaveChangesAsync();
            assignmentId = assignment.Id;
        }

        var executor = new FakeExecutorService(new ExecuteResponseDto(ExecuteStatus.SUCCESS, "hi\n", ""));

        // When
        await using var ctx = _fixture.CreateContext();
        var service = TestServices.Submissions(ctx, executor);
        var request = new SubmissionRequestDto("student-1", null, JsonSerializer.SerializeToElement("ignored"));
        var result = await service.SubmitAsync(assignmentId, request);

        // Then
        Assert.True(result!.Passed);
        Assert.Null(result.Feedback);
        await using var read = _fixture.CreateContext();
        Assert.Null((await read.Submission.SingleAsync()).FeedbackJson);
    }

    [Fact]
    public async Task SubmitAsync_CodeKind_GradingNull_PassedPersistsNull()
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
        var service = TestServices.Submissions(ctx, executor);
        var request = new SubmissionRequestDto("student-1", null, JsonSerializer.SerializeToElement("ignored"));
        var result = await service.SubmitAsync(assignmentId, request);

        // Then
        Assert.Null(result!.Passed);
        await using var read = _fixture.CreateContext();
        Assert.Null((await read.Submission.SingleAsync()).Passed);
    }

    [Fact]
    public async Task SubmitAsync_PassingPredict_SetPassedTrue()
    {
        // Given
        int assignmentId;
        await using (var setup = _fixture.CreateContext())
        {
            setup.Student.Add(TestData.MakeStudent("student-1"));
            var assignment = TestData.MakeAssignment(AssignmentKind.Predict);
            assignment.GradingJson = """{"predict":{"compare":"normalized","expectedOutput":"42"}}""";
            setup.Assignment.Add(assignment);
            await setup.SaveChangesAsync();
            assignmentId = assignment.Id;
        }
        var executor = new FakeExecutorService(new ExecuteResponseDto(ExecuteStatus.SUCCESS, "ignored", ""));
        
        // When
        await using var ctx = _fixture.CreateContext();
        var service = TestServices.Submissions(ctx, executor);
        var request = new SubmissionRequestDto("student-1", null, JsonSerializer.SerializeToElement("42"));
        var result = await service.SubmitAsync(assignmentId, request);

        // Then
        Assert.True(result!.Passed);
        await using var read = _fixture.CreateContext();
        var submission = await read.Submission.SingleAsync();
        Assert.True(submission.Passed);
        Assert.Null(submission.ResultJson);
    }

    [Fact]
    public async Task SubmitAsync_FailingPredict_SetPassedFalse()
    {
        // Given
        int assignmentId;
        await using (var setup = _fixture.CreateContext())
        {
            setup.Student.Add(TestData.MakeStudent("student-1"));
            var assignment = TestData.MakeAssignment(AssignmentKind.Predict);
            assignment.GradingJson = """{"predict":{"compare":"normalized","expectedOutput":"99"}}""";
            setup.Assignment.Add(assignment);
            await setup.SaveChangesAsync();
            assignmentId = assignment.Id;
        }
        var executor = new FakeExecutorService(new ExecuteResponseDto(ExecuteStatus.SUCCESS, "ignored", ""));
        
        // When
        await using var ctx = _fixture.CreateContext();
        var service = TestServices.Submissions(ctx, executor);
        var request = new SubmissionRequestDto("student-1", null, JsonSerializer.SerializeToElement("42"));
        var result = await service.SubmitAsync(assignmentId, request);

        // Then
        Assert.False(result!.Passed);
        await using var read = _fixture.CreateContext();
        var submission = await read.Submission.SingleAsync();
        Assert.False(submission.Passed);
        Assert.Null(submission.ResultJson);
    }

    [Fact]
    public async Task SubmitAsync_PredictKind_GradingNull_PassedPersistsNull()
    {
        // Given
        int assignmentId;
        await using (var setup = _fixture.CreateContext())
        {
            setup.Student.Add(TestData.MakeStudent("student-1"));
            var assignment = TestData.MakeAssignment(AssignmentKind.Predict);
            assignment.GradingJson = null;
            setup.Assignment.Add(assignment);
            await setup.SaveChangesAsync();
            assignmentId = assignment.Id;
        }
        var executor = new FakeExecutorService(new ExecuteResponseDto(ExecuteStatus.SUCCESS, "ignored", ""));
        
        // When
        await using var ctx = _fixture.CreateContext();
        var service = TestServices.Submissions(ctx, executor);
        var request = new SubmissionRequestDto("student-1", null, JsonSerializer.SerializeToElement("ignored"));
        var result = await service.SubmitAsync(assignmentId, request);

        // Then
        Assert.Null(result!.Passed);
        await using var read = _fixture.CreateContext();
        var submission = await read.Submission.SingleAsync();
        Assert.Null(submission.Passed);
        Assert.Null(submission.ResultJson);
        }

    
    [Fact]
    public async Task SubmitAsync_ProjectKind_ResultAndPassedStayNull()
    {
        // Given
        int assignmentId;
        await using (var setup = _fixture.CreateContext())
        {
            setup.Student.Add(TestData.MakeStudent("student-1"));
            var assignment = TestData.MakeAssignment(AssignmentKind.Project);
            assignment.GradingJson = """{"target":"stdout","op":"containsLine","value":"hi"}""";
            setup.Assignment.Add(assignment);
            await setup.SaveChangesAsync();
            assignmentId = assignment.Id;
        }

        var executor = new FakeExecutorService(new ExecuteResponseDto(ExecuteStatus.SUCCESS, "hi\n", ""));

        // When
        await using var ctx = _fixture.CreateContext();
        var service = TestServices.Submissions(ctx, executor);
        var request = new SubmissionRequestDto("student-1", null, JsonSerializer.SerializeToElement("ignored"));
        var result = await service.SubmitAsync(assignmentId, request);

        // Then
        Assert.Null(result!.Passed);
        await using var read = _fixture.CreateContext();
        var submission = await read.Submission.SingleAsync();
        Assert.Null(submission.Passed);
        Assert.Null(submission.ResultJson);
    }

    
    [Fact]
    public async Task SubmitAsync_UnknownAssignmentId_ReturnsNull()
    {
        // Given 
        await using (var setup = _fixture.CreateContext())
        {
            setup.Student.Add(TestData.MakeStudent("student-1"));
            var assignment = TestData.MakeAssignment(AssignmentKind.Code);
            assignment.GradingJson = """{"target":"stdout","op":"containsLine","value":"ignored"}""";
            setup.Assignment.Add(assignment);
            await setup.SaveChangesAsync();

        }

        var executor = new FakeExecutorService(new ExecuteResponseDto(ExecuteStatus.SUCCESS, "ignored", ""));

        // When
        await using var ctx = _fixture.CreateContext();
        var service = TestServices.Submissions(ctx, executor);
        var request = new SubmissionRequestDto("student-1", null, JsonSerializer.SerializeToElement("ignored"));
        var result = await service.SubmitAsync(9999, request);

        // Then
        Assert.Null(result);
        
    }

    [Fact]
    public async Task SubmitAsync_UnknownStudentId_Throws()
    {
        // Given 
        int assignmentId;
        await using (var setup = _fixture.CreateContext())
        {
            setup.Student.Add(TestData.MakeStudent("student-1"));
            var assignment = TestData.MakeAssignment(AssignmentKind.Code);
            assignment.GradingJson = """{"target":"stdout","op":"containsLine","value":"ignored"}""";
            setup.Assignment.Add(assignment);
            await setup.SaveChangesAsync();
            assignmentId = assignment.Id;

        }

        var executor = new FakeExecutorService(new ExecuteResponseDto(ExecuteStatus.SUCCESS, "ignored", ""));

        // When
        await using var ctx = _fixture.CreateContext();
        var service = TestServices.Submissions(ctx, executor);
        var request = new SubmissionRequestDto("student-2", null, JsonSerializer.SerializeToElement("ignored"));

        // Then
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SubmitAsync(assignmentId, request));
        
    }

    
    [Fact]
    public async Task SubmitAsync_UnknownSessionCode_Throws()
    {
        // Given 
        int assignmentId;
        await using (var setup = _fixture.CreateContext())
        {
            setup.Student.Add(TestData.MakeStudent("student-1"));
            var assignment = TestData.MakeAssignment(AssignmentKind.Code);
            assignment.GradingJson = """{"target":"stdout","op":"containsLine","value":"ignored"}""";
            setup.Assignment.Add(assignment);
            var assignmentSet = TestData.MakeAssignmentSet();
            setup.AssignmentSet.Add(assignmentSet);
            var session = TestData.MakeSession(assignmentSet.AssignmentSetId);
            setup.Session.Add(session);
            await setup.SaveChangesAsync();
            assignmentId = assignment.Id;

        }

        var executor = new FakeExecutorService(new ExecuteResponseDto(ExecuteStatus.SUCCESS, "ignored", ""));

        // When
        await using var ctx = _fixture.CreateContext();
        var service = TestServices.Submissions(ctx, executor);
        var request = new SubmissionRequestDto("student-1", "ZZZZ", JsonSerializer.SerializeToElement("ignored"));

        // Then
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SubmitAsync(assignmentId, request));
        
    }

    [Fact]
    public async Task SubmitAsync_ValidSessionId_ResolvesToSessionGuid()
    {
        // Given 
        string sessionCode;
        string sessionId;
        int assignmentId;
        await using (var setup = _fixture.CreateContext())
        {
            setup.Student.Add(TestData.MakeStudent("student-1"));
            var assignment = TestData.MakeAssignment(AssignmentKind.Code);
            assignment.GradingJson = """{"target":"stdout","op":"containsLine","value":"ignored"}""";
            setup.Assignment.Add(assignment);
            var assignmentSet = TestData.MakeAssignmentSet();
            setup.AssignmentSet.Add(assignmentSet);
            var session = TestData.MakeSession(assignmentSet.AssignmentSetId);
            setup.Session.Add(session);
            await setup.SaveChangesAsync();
            sessionCode = session.Code;
            sessionId = session.SessionId;
            assignmentId = assignment.Id;

        }

        var executor = new FakeExecutorService(new ExecuteResponseDto(ExecuteStatus.SUCCESS, "ignored", ""));

        // When
        await using var ctx = _fixture.CreateContext();
        var service = TestServices.Submissions(ctx, executor);
        var request = new SubmissionRequestDto("student-1", sessionCode, JsonSerializer.SerializeToElement("ignored"));
        await service.SubmitAsync(assignmentId,request);

        // Then
        await using var read = _fixture.CreateContext();
        var submission = await read.Submission.SingleAsync();
        Assert.Equal(sessionId, submission.SessionId);
        Assert.NotEqual(sessionCode, submission.SessionId);

    }

    // ── Ended rooms: the session lookup closes, the history stays open ────────

    [Fact]
    public async Task SubmitAsync_EndedSessionCode_CurrentlySucceeds()
    {
        // Given — a room the teacher already ended.
        string code;
        int assignmentId;
        await using (var setup = _fixture.CreateContext())
        {
            var assignmentSet = TestData.MakeAssignmentSet();
            setup.AssignmentSet.Add(assignmentSet);
            await setup.SaveChangesAsync();
            var session = TestData.MakeSession(assignmentSet.AssignmentSetId, status: SessionStatus.Ended);
            setup.Session.Add(session);
            var assignment = TestData.MakeAssignment(AssignmentKind.Predict);
            setup.Assignment.Add(assignment);
            setup.Student.Add(TestData.MakeStudent("student-1"));
            await setup.SaveChangesAsync();
            code = session.Code;
            assignmentId = assignment.Id;
        }

        // When
        await using var ctx = _fixture.CreateContext();
        var request = new SubmissionRequestDto("student-1", code, JsonSerializer.SerializeToElement("42"));
        var result = await TestServices.Submissions(ctx).SubmitAsync(assignmentId, request);

        // Then — CHARACTERIZATION, not an endorsement. SubmitAsync resolves the
        // room without the `Status == Active` filter every read path applies, so
        // a student whose tab is still open can submit into an ended room. Pinned
        // so the behaviour can't drift silently; flip to Assert.ThrowsAsync if the
        // team decides late submissions should be rejected.
        Assert.NotNull(result);
    }
}

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
/// The student-facing reads (STORIES.md S5): <c>GetHistoryAsync</c> — their own thin
/// attempt list — and <c>GetSubmissionAsync</c>, the shared full-detail replay both
/// the student's My Progress panel and the teacher's Col 4 use.
///
/// These are the two reads that deliberately do <b>not</b> filter on session status:
/// an ended room stops resolving via session lookup, but its rows stay reachable here.
/// </summary>
[Collection("db")]
public sealed class SubmissionHistoryTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    public SubmissionHistoryTests(PostgresFixture fixture) => _fixture = fixture;
    public async Task InitializeAsync() => await _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetHistoryAsync_ReturnsNewestFirst()
    {
        // Given
        string studentId = "student-1";
        List<int> AssignmentIdList = [];
        await using (var write = _fixture.CreateContext())
        {
            write.Student.Add(TestData.MakeStudent(studentId));
            var assignment1 = TestData.MakeAssignment(AssignmentKind.Code);
            assignment1.GradingJson = """{"target":"stdout","op":"containsLine","value":"ignored"}""";
            write.Assignment.Add(assignment1);
            var assignment2 = TestData.MakeAssignment(AssignmentKind.Code);
            assignment2.GradingJson = """{"target":"stdout","op":"containsLine","value":"ignored"}""";
            write.Assignment.Add(assignment2);
            var assignmentSet = TestData.MakeAssignmentSet();
            write.AssignmentSet.Add(assignmentSet);
            await write.SaveChangesAsync();
            AssignmentIdList.Add(assignment1.Id);
            AssignmentIdList.Add(assignment2.Id); // insert order.

        }

        var expectedOrder = new List<int> { AssignmentIdList[1], AssignmentIdList[1] }; // newest first check
        var executor = new FakeExecutorService(new ExecuteResponseDto(ExecuteStatus.SUCCESS, "ignored", ""));

        // When
        await using var ctx = _fixture.CreateContext();
        var service = TestServices.Submissions(ctx, executor);
        var request1 = new SubmissionRequestDto(studentId, null, JsonSerializer.SerializeToElement("x"));
        await service.SubmitAsync(expectedOrder[0], request1);
        await service.SubmitAsync(expectedOrder[1], request1);

        var result = await service.GetHistoryAsync(studentId);

        // Then
        var actualOrder = result.Select(dto => dto.AssignmentId).ToList();
        Assert.Equal(expectedOrder, actualOrder);
    }

    [Fact]
    public async Task GetHistoryAsync_SoloSubmission_SessionIdIsNull()
    {
        // Given
        string studentId = "student-1";
        int assignmentId;
        await using (var write = _fixture.CreateContext())
        {
            write.Student.Add(TestData.MakeStudent(studentId));
            var assignment = TestData.MakeAssignment(AssignmentKind.Code);
            assignment.GradingJson = """{"target":"stdout","op":"containsLine","value":"ignored"}""";
            write.Assignment.Add(assignment);
            await write.SaveChangesAsync();
            assignmentId = assignment.Id;
        }

        var executor = new FakeExecutorService(new ExecuteResponseDto(ExecuteStatus.SUCCESS, "ignored", ""));

        // When
        await using var ctx = _fixture.CreateContext();
        var service = TestServices.Submissions(ctx, executor);
        var request1 = new SubmissionRequestDto(studentId, null, JsonSerializer.SerializeToElement("x"));
        await service.SubmitAsync(assignmentId, request1);

        var result = await service.GetHistoryAsync(studentId);

        // Then
        var only = Assert.Single(result);
        Assert.Null(only.SessionId);
    }

    [Fact] 
    public async Task GetHistoryAsync_ValidSessionCode_SessionIdIsCode()
    {
        // Given 
        string sessionCode;
        string sessionId;
        int assignmentId;
        string studentId = "student-1";
        await using (var setup = _fixture.CreateContext())
        {
            setup.Student.Add(TestData.MakeStudent(studentId));
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
        var request = new SubmissionRequestDto(studentId, sessionCode, JsonSerializer.SerializeToElement("ignored"));
        await service.SubmitAsync(assignmentId, request);


        // Then
        var result = await service.GetHistoryAsync(studentId);
        var only = Assert.Single(result);
        Assert.NotEqual(sessionId, only.SessionId);
        Assert.Equal(sessionCode, only.SessionId);
    }

    [Fact]
    public async Task GetHistoryAsync_UnknownStudentId_ReturnsEmptyList()
    {
        // Given
        string studentId = "student-1";
        int assignmentId;
        await using (var write = _fixture.CreateContext())
        {
            write.Student.Add(TestData.MakeStudent(studentId));
            var assignment = TestData.MakeAssignment(AssignmentKind.Code);
            assignment.GradingJson = """{"target":"stdout","op":"containsLine","value":"ignored"}""";
            write.Assignment.Add(assignment);
            await write.SaveChangesAsync();
            assignmentId = assignment.Id;
        }

        var executor = new FakeExecutorService(new ExecuteResponseDto(ExecuteStatus.SUCCESS, "ignored", ""));

        // When
        await using var ctx = _fixture.CreateContext();
        var service = TestServices.Submissions(ctx, executor);
        var request1 = new SubmissionRequestDto(studentId, null, JsonSerializer.SerializeToElement("x"));
        await service.SubmitAsync(assignmentId, request1);

        var result = await service.GetHistoryAsync("unknown-student");

        // Then
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSubmissionAsync_CodeKind_RoundTripsContentResultAndPassed()
    {
        // Given
        string studentId = "student-1";
        int assignmentId;
        await using (var write = _fixture.CreateContext())
        {
            write.Student.Add(TestData.MakeStudent(studentId));
            var assignment = TestData.MakeAssignment(AssignmentKind.Code);
            assignment.GradingJson = """{"target":"stdout","op":"containsLine","value":"hello"}""";
            write.Assignment.Add(assignment);
            await write.SaveChangesAsync();
            assignmentId = assignment.Id;
        }

        var executor = new FakeExecutorService(new ExecuteResponseDto(ExecuteStatus.SUCCESS, "hello", ""));

        // When
        await using var ctx = _fixture.CreateContext();
        var service = TestServices.Submissions(ctx, executor);
        JsonElement content = JsonSerializer.SerializeToElement("""{"code": "class Main {}"}""");
        var request1 = new SubmissionRequestDto(studentId, null, content);
        var response = await service.SubmitAsync(assignmentId, request1);

        var result = await service.GetSubmissionAsync(response!.SubId);

        // Then
        
        Assert.NotNull(result);
        Assert.Equal(content.GetString(), result.Content.GetString());
        Assert.NotNull(result.Result);
        Assert.True(result.Passed);
    }

    [Fact]
    public async Task GetSubmissionAsync_FailingCodeWithMessage_RoundTripsFeedback()
    {
        // Given
        string studentId = "student-1";
        int assignmentId;
        await using (var write = _fixture.CreateContext())
        {
            write.Student.Add(TestData.MakeStudent(studentId));
            var assignment = TestData.MakeAssignment(AssignmentKind.Code);
            assignment.GradingJson = """{"target":"stdout","op":"containsLine","value":"hello","message":"Print hello, not goodbye."}""";
            write.Assignment.Add(assignment);
            await write.SaveChangesAsync();
            assignmentId = assignment.Id;
        }

        var executor = new FakeExecutorService(new ExecuteResponseDto(ExecuteStatus.SUCCESS, "goodbye", ""));

        // When
        await using var ctx = _fixture.CreateContext();
        var service = TestServices.Submissions(ctx, executor);
        JsonElement content = JsonSerializer.SerializeToElement("""{"code": "class Main {}"}""");
        var request1 = new SubmissionRequestDto(studentId, null, content);
        var response = await service.SubmitAsync(assignmentId, request1);

        var result = await service.GetSubmissionAsync(response!.SubId);

        // Then
        Assert.NotNull(result);
        Assert.False(result.Passed);
        Assert.Equal(new[] { "Print hello, not goodbye." }, result.Feedback);
    }

    [Fact]
    public async Task GetSubmissionAsync_PredictKind_RoundTripsContent_ResultIsNull()
    {
        // Given
        string studentId = "student-1";
        int assignmentId;
        await using (var write = _fixture.CreateContext())
        {
            write.Student.Add(TestData.MakeStudent(studentId));
            var assignment = TestData.MakeAssignment(AssignmentKind.Predict);
            assignment.GradingJson = """{"predict":{"compare":"normalized","expectedOutput":"42"}}""";
            write.Assignment.Add(assignment);
            await write.SaveChangesAsync();
            assignmentId = assignment.Id;
        }

        var executor = new FakeExecutorService(new ExecuteResponseDto(ExecuteStatus.SUCCESS, "Ignored", ""));

        // When
        await using var ctx = _fixture.CreateContext();
        var service = TestServices.Submissions(ctx, executor);
        JsonElement content = JsonSerializer.SerializeToElement("42");
        var request1 = new SubmissionRequestDto(studentId, null, content);
        var response = await service.SubmitAsync(assignmentId, request1);

        var result = await service.GetSubmissionAsync(response!.SubId);

        // Then
        
        Assert.NotNull(result);
        Assert.Equal(content.GetString(), result.Content.GetString());
        Assert.Null(result.Result);
        Assert.True(result.Passed);
    }

    [Fact]
    public async Task GetSubmissionAsync_ValidSessionCode_SessionIdIsCode()
    {
        // Given
        string studentId = "student-1";
        int assignmentId;
        string sessionId;
        string sessionCode;
        await using (var write = _fixture.CreateContext())
        {
            write.Student.Add(TestData.MakeStudent(studentId));
            var assignment = TestData.MakeAssignment(AssignmentKind.Code);
            assignment.GradingJson = """{"target":"stdout","op":"containsLine","value":"hello"}""";
            write.Assignment.Add(assignment);
            var assignmentSet = TestData.MakeAssignmentSet();
            write.AssignmentSet.Add(assignmentSet);
            var session = TestData.MakeSession(assignmentSet.AssignmentSetId);
            write.Session.Add(session);
            await write.SaveChangesAsync();
            assignmentId = assignment.Id;
            sessionId = session.SessionId;
            sessionCode = session.Code;
        }

        var executor = new FakeExecutorService(new ExecuteResponseDto(ExecuteStatus.SUCCESS, "hello", ""));

        // When
        await using var ctx = _fixture.CreateContext();
        var service = TestServices.Submissions(ctx, executor);
        JsonElement content = JsonSerializer.SerializeToElement("""{"code": "class Main {}"}""");
        var request1 = new SubmissionRequestDto(studentId, sessionCode, content);
        var response = await service.SubmitAsync(assignmentId, request1);

        var result = await service.GetSubmissionAsync(response!.SubId);

        // Then
        
        Assert.NotNull(result);
        Assert.Equal(content.GetString(), result.Content.GetString());

        Assert.NotEqual(sessionId, result.SessionId);
        Assert.Equal(sessionCode, result.SessionId);
    }

    [Fact]
    public async Task GetSubmissionAsync_UnknownSubId_ReturnsNull()
    {
        // Given
        string studentId = "student-1";
        int assignmentId;
        await using (var write = _fixture.CreateContext())
        {
            write.Student.Add(TestData.MakeStudent(studentId));
            var assignment = TestData.MakeAssignment(AssignmentKind.Code);
            assignment.GradingJson = """{"target":"stdout","op":"containsLine","value":"hello"}""";
            write.Assignment.Add(assignment);
            await write.SaveChangesAsync();
            assignmentId = assignment.Id;
        }

        var executor = new FakeExecutorService(new ExecuteResponseDto(ExecuteStatus.SUCCESS, "hello", ""));

        // When
        await using var ctx = _fixture.CreateContext();
        var service = TestServices.Submissions(ctx, executor);
        JsonElement content = JsonSerializer.SerializeToElement("""{"code": "class Main {}"}""");
        var request1 = new SubmissionRequestDto(studentId, null, content);
        var response = await service.SubmitAsync(assignmentId, request1);
        var unknownSubId = new Guid();
        var result = await service.GetSubmissionAsync(unknownSubId);

        // Then

        Assert.Null(result);
    }

    [Fact]
    public async Task GetHistoryAsync_EndedSession_StillReturnsTheAttempt()
    {
        // Given — attempts made in a room that has since ended.
        var seed = await RoomSeeder.WithAttemptsAsync(_fixture, SessionStatus.Ended);

        // When
        await using var ctx = _fixture.CreateContext();
        var rows = await TestServices.Submissions(ctx).GetHistoryAsync("student-maria");

        // Then — SCHEMA.md: ended rooms stop resolving via session lookup, but
        // their rows stay reachable through the student/history endpoints.
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(seed.Code, r.SessionId));
    }

    [Fact]
    public async Task GetSubmissionAsync_EndedSession_StillReturnsTheDetail()
    {
        // Given
        var seed = await RoomSeeder.WithAttemptsAsync(_fixture, SessionStatus.Ended);

        // When
        await using var ctx = _fixture.CreateContext();
        var detail = await TestServices.Submissions(ctx).GetSubmissionAsync(seed.Newest);

        // Then — Col 4's replay must keep working after the class is over.
        Assert.NotNull(detail);
        Assert.Equal(seed.Code, detail.SessionId);
    }
}

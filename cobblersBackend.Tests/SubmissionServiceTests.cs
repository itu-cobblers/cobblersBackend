using cobblersBackend.DTOs;
using cobblersBackend.Data;
using cobblersBackend.Data.Entities;
using cobblersBackend.Hubs;
using cobblersBackend.Services;
using cobblersBackend.Tests.Infrastructure;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

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
        var service = NewService(ctx, executor);
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
        var service = NewService(ctx, executor);
        var request = new SubmissionRequestDto("student-1", null, JsonSerializer.SerializeToElement("ignored"));
        var result = await service.SubmitAsync(assignmentId, request);

        // Then
        Assert.False(result!.Passed);
        await using var read = _fixture.CreateContext();
        Assert.False((await read.Submission.SingleAsync()).Passed);
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
        var service = NewService(ctx, executor);
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
        var service = NewService(ctx, executor);
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
        var service = NewService(ctx, executor);
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
        var service = NewService(ctx, executor);
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
        var service = NewService(ctx, executor);
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
    public async Task GetSolutionAsync_ReturnsStringSolution()
    {
        int assignmentId;
        await using (var setup = _fixture.CreateContext())
        {
            var assignment = TestData.MakeAssignment(AssignmentKind.Code);
            assignment.SampleSolutionJson = JsonSerializer.Serialize("public class Main {}");
            setup.Assignment.Add(assignment);
            await setup.SaveChangesAsync();
            assignmentId = assignment.Id;
        }

        await using var ctx = _fixture.CreateContext();
        var service = NewService(ctx);
        var result = await service.GetSolutionAsync(assignmentId);

        Assert.NotNull(result);
        Assert.Equal(JsonValueKind.String, result!.Solution!.Value.ValueKind);
        Assert.Equal("public class Main {}", result.Solution.Value.GetString());
    }

    [Fact]
    public async Task GetSolutionAsync_ReturnsFileListSolution()
    {
        int assignmentId;
        await using (var setup = _fixture.CreateContext())
        {
            var assignment = TestData.MakeAssignment(AssignmentKind.Code);
            assignment.SampleSolutionJson = """[{"name":"Main.java","content":"class Main {}"}]""";
            setup.Assignment.Add(assignment);
            await setup.SaveChangesAsync();
            assignmentId = assignment.Id;
        }

        await using var ctx = _fixture.CreateContext();
        var service = NewService(ctx);
        var result = await service.GetSolutionAsync(assignmentId);

        Assert.NotNull(result);
        Assert.Equal(JsonValueKind.Array, result!.Solution!.Value.ValueKind);
    }

    [Fact]
    public async Task GetSolutionAsync_NoSampleSolution_ReturnsNullSolution()
    {
        int assignmentId;
        await using (var setup = _fixture.CreateContext())
        {
            var assignment = TestData.MakeAssignment(AssignmentKind.Code);
            setup.Assignment.Add(assignment);
            await setup.SaveChangesAsync();
            assignmentId = assignment.Id;
        }

        await using var ctx = _fixture.CreateContext();
        var service = NewService(ctx);
        var result = await service.GetSolutionAsync(assignmentId);

        Assert.NotNull(result);
        Assert.Null(result!.Solution);
    }

    [Fact]
    public async Task GetSolutionAsync_UnknownAssignment_ReturnsNull()
    {
        await using var ctx = _fixture.CreateContext();
        var service = NewService(ctx);
        var result = await service.GetSolutionAsync(999_999);

        Assert.Null(result);
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
        var service = NewService(ctx, executor);
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
        var service = NewService(ctx, executor);
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
        var service = NewService(ctx, executor);
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
        var service = NewService(ctx, executor);
        var request = new SubmissionRequestDto("student-1", sessionCode, JsonSerializer.SerializeToElement("ignored"));
        var result = service.SubmitAsync(assignmentId,request);

        // Then
        await using var read = _fixture.CreateContext();
        var submission = await read.Submission.SingleAsync();
        Assert.Equal(sessionId, submission.SessionId);
        Assert.NotEqual(sessionCode, submission.SessionId);

    }

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
        var service = NewService(ctx, executor);
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
        var service = NewService(ctx, executor);
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
        var service = NewService(ctx, executor);
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
        var service = NewService(ctx, executor);
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
        var service = NewService(ctx, executor);
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
        var service = NewService(ctx, executor);
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
        var service = NewService(ctx, executor);
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
        var service = NewService(ctx, executor);
        JsonElement content = JsonSerializer.SerializeToElement("""{"code": "class Main {}"}""");
        var request1 = new SubmissionRequestDto(studentId, null, content);
        var response = await service.SubmitAsync(assignmentId, request1);
        var unknownSubId = new Guid();
        var result = await service.GetSubmissionAsync(unknownSubId);

        // Then

        Assert.Null(result);
    }

    // ── GetSessionSubmissionsAsync — the teacher dashboard's hydration read ──
    // CONTRACT.md "GET /api/sessions/{code}/submissions": every attempt in the
    // room — all assignments, all students, every try — flat and newest-first.

    /// <summary>
    /// Seeds a room with two students and two assignments and returns the ids the
    /// room-wide tests assert against. `SubmittedAt` is written explicitly here:
    /// the column is DB-owned (<c>DEFAULT now()</c>), so three rows inserted in
    /// one SaveChanges would share a timestamp and make ordering assertions flaky.
    /// </summary>
    private async Task<RoomSeed> SeedRoomWithAttemptsAsync(SessionStatus status = SessionStatus.Active)
    {
        await using var setup = _fixture.CreateContext();

        var assignmentSet = TestData.MakeAssignmentSet();
        setup.AssignmentSet.Add(assignmentSet);
        await setup.SaveChangesAsync();

        var session = TestData.MakeSession(assignmentSet.AssignmentSetId, status: status);
        setup.Session.Add(session);

        var assignmentA = TestData.MakeAssignment(AssignmentKind.Code);
        var assignmentB = TestData.MakeAssignment(AssignmentKind.Predict);
        setup.Assignment.AddRange(assignmentA, assignmentB);
        setup.Student.Add(TestData.MakeStudent("student-maria", "Maria"));
        setup.Student.Add(TestData.MakeStudent("student-jonas", "Jonas"));
        await setup.SaveChangesAsync();

        var oldest = TestData.MakeSubmission("student-maria", assignmentA.Id, session.SessionId);
        var middle = TestData.MakeSubmission("student-jonas", assignmentA.Id, session.SessionId);
        var newest = TestData.MakeSubmission("student-maria", assignmentB.Id, session.SessionId);
        oldest.SubmittedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        middle.SubmittedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        newest.SubmittedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        oldest.Passed = false;
        middle.Passed = true;
        // newest.Passed stays null — an ungraded kind.
        setup.Submission.AddRange(oldest, middle, newest);
        await setup.SaveChangesAsync();

        return new RoomSeed(session.Code, assignmentA.Id, assignmentB.Id,
                            oldest.SubId, middle.SubId, newest.SubId);
    }

    private sealed record RoomSeed(
        string Code, int AssignmentA, int AssignmentB, Guid Oldest, Guid Middle, Guid Newest);

    /// <summary>
    /// Captures the SubmissionRecorded broadcasts every `NewService` in this class emits.
    /// xUnit builds a fresh test-class instance per test, so this is per-test state.
    /// </summary>
    private readonly RecordingHubContext<SessionHub> _hub = new();

    private SubmissionService NewService(CobblersDbContext ctx, IExecutorService? executor = null) =>
        new(ctx,
            executor ?? new FakeExecutorService(new ExecuteResponseDto(ExecuteStatus.SUCCESS, "", "")),
            new AssignmentGrader(),
            _hub.Object,
            NullLogger<SubmissionService>.Instance);

    [Fact]
    public async Task GetSessionSubmissionsAsync_UnknownCode_ReturnsNull()
    {
        // Given
        await using var ctx = _fixture.CreateContext();

        // When
        var rows = await NewService(ctx).GetSessionSubmissionsAsync("ZZZZZZ");

        // Then — null is the 404 signal; an empty list would mean "no attempts yet".
        Assert.Null(rows);
    }

    [Fact]
    public async Task GetSessionSubmissionsAsync_EndedSession_ReturnsNull()
    {
        // Given — the same room, but the teacher has ended it.
        var seed = await SeedRoomWithAttemptsAsync(SessionStatus.Ended);

        // When
        await using var ctx = _fixture.CreateContext();
        var rows = await NewService(ctx).GetSessionSubmissionsAsync(seed.Code);

        // Then — CONTRACT.md: an ended room reads as not-found on session lookup,
        // even though its rows are untouched (see the history tests below).
        Assert.Null(rows);
    }

    [Fact]
    public async Task GetSessionSubmissionsAsync_ActiveRoomWithNoAttempts_ReturnsEmptyList()
    {
        // Given — a real room nobody has submitted in yet.
        string code;
        await using (var setup = _fixture.CreateContext())
        {
            var assignmentSet = TestData.MakeAssignmentSet();
            setup.AssignmentSet.Add(assignmentSet);
            await setup.SaveChangesAsync();
            var session = TestData.MakeSession(assignmentSet.AssignmentSetId);
            setup.Session.Add(session);
            await setup.SaveChangesAsync();
            code = session.Code;
        }

        // When
        await using var ctx = _fixture.CreateContext();
        var rows = await NewService(ctx).GetSessionSubmissionsAsync(code);

        // Then — empty, NOT null. "200 []" and "404" are different answers.
        Assert.NotNull(rows);
        Assert.Empty(rows);
    }

    [Fact]
    public async Task GetSessionSubmissionsAsync_ReturnsEveryStudentAndAssignmentInOneFlatList()
    {
        // Given
        var seed = await SeedRoomWithAttemptsAsync();

        // When
        await using var ctx = _fixture.CreateContext();
        var rows = await NewService(ctx).GetSessionSubmissionsAsync(seed.Code);

        // Then — no per-assignment or per-student scoping: the whole room, flat.
        Assert.NotNull(rows);
        Assert.Equal(3, rows.Count);
        Assert.Equal(["student-jonas", "student-maria"],
                     rows.Select(r => r.StudentId).Distinct().OrderBy(id => id).ToArray());
        Assert.Equal([seed.AssignmentA, seed.AssignmentB],
                     rows.Select(r => r.AssignmentId).Distinct().OrderBy(id => id).ToArray());
    }

    [Fact]
    public async Task GetSessionSubmissionsAsync_ReturnsNewestFirst()
    {
        // Given
        var seed = await SeedRoomWithAttemptsAsync();

        // When
        await using var ctx = _fixture.CreateContext();
        var rows = await NewService(ctx).GetSessionSubmissionsAsync(seed.Code);

        // Then — sorted by submittedAt desc only, not pre-grouped by anything.
        Assert.NotNull(rows);
        Assert.Equal([seed.Newest, seed.Middle, seed.Oldest], rows.Select(r => r.SubId).ToArray());
    }

    [Fact]
    public async Task GetSessionSubmissionsAsync_CarriesPassedIncludingNull()
    {
        // Given — middle passed, oldest failed, newest was never graded.
        var seed = await SeedRoomWithAttemptsAsync();

        // When
        await using var ctx = _fixture.CreateContext();
        var rows = await NewService(ctx).GetSessionSubmissionsAsync(seed.Code);

        // Then — `passed: null` survives as null; the frontend must not read it as failed.
        Assert.NotNull(rows);
        Assert.True(rows.Single(r => r.SubId == seed.Middle).Passed);
        Assert.False(rows.Single(r => r.SubId == seed.Oldest).Passed);
        Assert.Null(rows.Single(r => r.SubId == seed.Newest).Passed);
    }

    [Fact]
    public async Task GetSessionSubmissionsAsync_ExcludesSoloSubmissions()
    {
        // Given — the same student also practices solo (sessionId null).
        var seed = await SeedRoomWithAttemptsAsync();
        await using (var write = _fixture.CreateContext())
        {
            write.Submission.Add(TestData.MakeSubmission("student-maria", seed.AssignmentA, null));
            await write.SaveChangesAsync();
        }

        // When
        await using var ctx = _fixture.CreateContext();
        var rows = await NewService(ctx).GetSessionSubmissionsAsync(seed.Code);

        // Then — solo work belongs to no room's dashboard.
        Assert.NotNull(rows);
        Assert.Equal(3, rows.Count);
    }

    [Fact]
    public async Task GetSessionSubmissionsAsync_ExcludesOtherRoomsAttempts()
    {
        // Given — a second, unrelated room with its own attempt.
        var seed = await SeedRoomWithAttemptsAsync();
        Guid straySubId;
        await using (var write = _fixture.CreateContext())
        {
            var otherSet = TestData.MakeAssignmentSet();
            write.AssignmentSet.Add(otherSet);
            await write.SaveChangesAsync();
            var otherSession = TestData.MakeSession(otherSet.AssignmentSetId);
            write.Session.Add(otherSession);
            await write.SaveChangesAsync();
            var stray = TestData.MakeSubmission("student-maria", seed.AssignmentA, otherSession.SessionId);
            write.Submission.Add(stray);
            await write.SaveChangesAsync();
            straySubId = stray.SubId;
        }

        // When
        await using var ctx = _fixture.CreateContext();
        var rows = await NewService(ctx).GetSessionSubmissionsAsync(seed.Code);

        // Then
        Assert.NotNull(rows);
        Assert.DoesNotContain(rows, r => r.SubId == straySubId);
        Assert.Equal(3, rows.Count);
    }

    [Fact]
    public async Task GetSessionSubmissionsAsync_NormalizesCase()
    {
        // Given — codes are stored uppercase; a caller may send anything.
        var seed = await SeedRoomWithAttemptsAsync();

        // When
        await using var ctx = _fixture.CreateContext();
        var rows = await NewService(ctx).GetSessionSubmissionsAsync($"  {seed.Code.ToLowerInvariant()} ");

        // Then
        Assert.NotNull(rows);
        Assert.Equal(3, rows.Count);
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
        var result = await NewService(ctx).SubmitAsync(assignmentId, request);

        // Then — CHARACTERIZATION, not an endorsement. SubmitAsync resolves the
        // room without the `Status == Active` filter every read path applies, so
        // a student whose tab is still open can submit into an ended room. Pinned
        // so the behaviour can't drift silently; flip to Assert.ThrowsAsync if the
        // team decides late submissions should be rejected.
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetHistoryAsync_EndedSession_StillReturnsTheAttempt()
    {
        // Given — attempts made in a room that has since ended.
        var seed = await SeedRoomWithAttemptsAsync(SessionStatus.Ended);

        // When
        await using var ctx = _fixture.CreateContext();
        var rows = await NewService(ctx).GetHistoryAsync("student-maria");

        // Then — SCHEMA.md: ended rooms stop resolving via session lookup, but
        // their rows stay reachable through the student/history endpoints.
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(seed.Code, r.SessionId));
    }

    [Fact]
    public async Task GetSubmissionAsync_EndedSession_StillReturnsTheDetail()
    {
        // Given
        var seed = await SeedRoomWithAttemptsAsync(SessionStatus.Ended);

        // When
        await using var ctx = _fixture.CreateContext();
        var detail = await NewService(ctx).GetSubmissionAsync(seed.Newest);

        // Then — Col 4's replay must keep working after the class is over.
        Assert.NotNull(detail);
        Assert.Equal(seed.Code, detail.SessionId);
    }

    // ── SubmissionRecorded — the live delta for the teacher dashboard ─────────
    // CONTRACT.md "Live progress broadcasts": one thin attempt row, pushed to the
    // room's observers so the dashboard patches instead of re-polling.

    /// <summary>A live room, one student, and one assignment of the given kind.</summary>
    private async Task<(string Code, int AssignmentId)> SeedSubmittableRoomAsync(
        AssignmentKind kind = AssignmentKind.Predict, string? grading = null)
    {
        await using var setup = _fixture.CreateContext();
        var assignmentSet = TestData.MakeAssignmentSet();
        setup.AssignmentSet.Add(assignmentSet);
        await setup.SaveChangesAsync();

        var session = TestData.MakeSession(assignmentSet.AssignmentSetId);
        setup.Session.Add(session);
        var assignment = TestData.MakeAssignment(kind);
        // Default the grading rules per kind, so an explicit `grading: null` really does mean
        // "this assignment has no grader" rather than silently inheriting the predict doc.
        assignment.GradingJson = grading ?? (kind == AssignmentKind.Predict
            ? """{"predict":{"compare":"normalized","expectedOutput":"42"}}"""
            : null);
        setup.Assignment.Add(assignment);
        setup.Student.Add(TestData.MakeStudent("student-maria", "Maria"));
        await setup.SaveChangesAsync();

        return (session.Code, assignment.Id);
    }

    [Fact]
    public async Task SubmitAsync_RoomSubmission_BroadcastsSubmissionRecordedToThatRoom()
    {
        // Given
        var seed = await SeedSubmittableRoomAsync();

        // When
        await using var ctx = _fixture.CreateContext();
        var request = new SubmissionRequestDto("student-maria", seed.Code, JsonSerializer.SerializeToElement("42"));
        await NewService(ctx).SubmitAsync(seed.AssignmentId, request);

        // Then — the group is the room code, so only that room's observers get it.
        var broadcast = _hub.Single();
        Assert.Equal(seed.Code, broadcast.Group);
        Assert.Equal("SubmissionRecorded", broadcast.Method);
    }

    [Fact]
    public async Task SubmitAsync_BroadcastPayloadIsOneSubmissionsRow()
    {
        // Given
        var seed = await SeedSubmittableRoomAsync();

        // When
        await using var ctx = _fixture.CreateContext();
        var service = NewService(ctx);
        var request = new SubmissionRequestDto("student-maria", seed.Code, JsonSerializer.SerializeToElement("42"));
        var response = await service.SubmitAsync(seed.AssignmentId, request);

        // Then — same DTO the hydration endpoint returns, so the frontend can prepend it
        // to the hydrated list with no special-casing. Assert it field-for-field against
        // what GET /api/sessions/{code}/submissions actually serves for the same attempt.
        var row = Assert.IsType<SessionSubmissionDto>(_hub.Single().Args[0]);
        var hydrated = await service.GetSessionSubmissionsAsync(seed.Code);
        Assert.Equal(Assert.Single(hydrated!), row);

        // …and it's the submission the caller was just told about.
        Assert.Equal(response!.SubId, row.SubId);
        Assert.Equal("student-maria", row.StudentId);
        Assert.Equal(seed.AssignmentId, row.AssignmentId);
        Assert.True(row.Passed);
        Assert.NotEqual(default, row.SubmittedAt);
    }

    [Fact]
    public async Task SubmitAsync_SoloSubmission_BroadcastsNothing()
    {
        // Given — same assignment, but no sessionId on the request.
        var seed = await SeedSubmittableRoomAsync();

        // When
        await using var ctx = _fixture.CreateContext();
        var request = new SubmissionRequestDto("student-maria", null, JsonSerializer.SerializeToElement("42"));
        await NewService(ctx).SubmitAsync(seed.AssignmentId, request);

        // Then — solo practice belongs to no room; there is nobody to tell.
        Assert.Empty(_hub.Sent);
    }

    [Fact]
    public async Task SubmitAsync_ProjectKind_BroadcastsNothing()
    {
        // Given
        var seed = await SeedSubmittableRoomAsync(AssignmentKind.Project);

        // When
        await using var ctx = _fixture.CreateContext();
        var request = new SubmissionRequestDto("student-maria", seed.Code, JsonSerializer.SerializeToElement("{}"));
        await NewService(ctx).SubmitAsync(seed.AssignmentId, request);

        // Then — mini-projects are VS-Code-only and never appear in the room list, so a
        // broadcast would put a row on the dashboard that a re-hydrate then removes.
        Assert.Empty(_hub.Sent);
    }

    [Fact]
    public async Task SubmitAsync_FailedAttempt_StillBroadcasts()
    {
        // Given — a wrong answer is still progress the teacher wants to see.
        var seed = await SeedSubmittableRoomAsync();

        // When
        await using var ctx = _fixture.CreateContext();
        var request = new SubmissionRequestDto("student-maria", seed.Code, JsonSerializer.SerializeToElement("99"));
        await NewService(ctx).SubmitAsync(seed.AssignmentId, request);

        // Then — only `project` is excluded, not failures.
        var row = Assert.IsType<SessionSubmissionDto>(_hub.Single().Args[0]);
        Assert.False(row.Passed);
    }

    [Fact]
    public async Task SubmitAsync_UngradedCodeKind_StillBroadcastsWithNullPassed()
    {
        // Given — a code assignment with no grading rules: passed stays null.
        var seed = await SeedSubmittableRoomAsync(AssignmentKind.Code);

        // When
        await using var ctx = _fixture.CreateContext();
        var request = new SubmissionRequestDto("student-maria", seed.Code,
                                               JsonSerializer.SerializeToElement("class Main {}"));
        await NewService(ctx).SubmitAsync(seed.AssignmentId, request);

        // Then — `passed: null` reaches the frontend as null, which derives to neither
        // passed nor failed (same rule as the hydrated list).
        var row = Assert.IsType<SessionSubmissionDto>(_hub.Single().Args[0]);
        Assert.Null(row.Passed);
    }

    [Fact]
    public async Task SubmitAsync_LowercaseSessionCode_BroadcastsToTheNormalizedGroup()
    {
        // Given
        var seed = await SeedSubmittableRoomAsync();

        // When — the client sends the code however the student typed it.
        await using var ctx = _fixture.CreateContext();
        var request = new SubmissionRequestDto("student-maria", seed.Code.ToLowerInvariant(),
                                               JsonSerializer.SerializeToElement("42"));
        await NewService(ctx).SubmitAsync(seed.AssignmentId, request);

        // Then — SignalR group names are case-sensitive, so a raw code here would broadcast
        // into a group nobody is subscribed to and the dashboard would silently never update.
        Assert.Equal(seed.Code, _hub.Single().Group);
    }

    [Fact]
    public async Task SubmitAsync_BroadcastThrows_SubmissionStillSucceedsAndPersists()
    {
        // Given — the hub is down / the send faults.
        var seed = await SeedSubmittableRoomAsync();
        _hub.ThrowOnSend = new InvalidOperationException("hub is gone");

        // When
        await using var ctx = _fixture.CreateContext();
        var request = new SubmissionRequestDto("student-maria", seed.Code, JsonSerializer.SerializeToElement("42"));
        var response = await NewService(ctx).SubmitAsync(seed.AssignmentId, request);

        // Then — best-effort by design: the row is already committed, so failing the request
        // would tell a student their work was lost and invite a duplicate resubmit. The
        // teacher's dashboard catches up on its next hydrate.
        Assert.NotNull(response);
        await using var read = _fixture.CreateContext();
        Assert.Equal(response.SubId, (await read.Submission.SingleAsync()).SubId);
    }

    [Fact]
    public async Task SubmitAsync_UnknownAssignment_BroadcastsNothing()
    {
        // Given
        await SeedSubmittableRoomAsync();

        // When
        await using var ctx = _fixture.CreateContext();
        var request = new SubmissionRequestDto("student-maria", null, JsonSerializer.SerializeToElement("42"));
        var response = await NewService(ctx).SubmitAsync(999999, request);

        // Then — nothing was recorded, so nothing is announced.
        Assert.Null(response);
        Assert.Empty(_hub.Sent);
    }
}
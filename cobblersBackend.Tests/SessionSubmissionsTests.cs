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
/// The teacher dashboard's submission data (STORIES.md S10), both halves:
/// <c>GetSessionSubmissionsAsync</c> (the REST hydration read) and the
/// <c>SubmissionRecorded</c> broadcast (the live delta on top). They live together
/// because the broadcast payload <i>is</i> one row of the hydration read — a test
/// here asserts them equal so the two can't drift apart.
/// </summary>
[Collection("db")]
public sealed class SessionSubmissionsTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    /// <summary>Captures the SubmissionRecorded broadcasts this class's services emit.</summary>
    private readonly RecordingHubContext<SessionHub> _hub = new();

    public SessionSubmissionsTests(PostgresFixture fixture) => _fixture = fixture;
    public async Task InitializeAsync() => await _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetSessionSubmissionsAsync_UnknownCode_ReturnsNull()
    {
        // Given
        await using var ctx = _fixture.CreateContext();

        // When
        var rows = await TestServices.Submissions(ctx, hub: _hub.Object).GetSessionSubmissionsAsync("ZZZZZZ");

        // Then — null is the 404 signal; an empty list would mean "no attempts yet".
        Assert.Null(rows);
    }

    [Fact]
    public async Task GetSessionSubmissionsAsync_EndedSession_ReturnsNull()
    {
        // Given — the same room, but the teacher has ended it.
        var seed = await RoomSeeder.WithAttemptsAsync(_fixture, SessionStatus.Ended);

        // When
        await using var ctx = _fixture.CreateContext();
        var rows = await TestServices.Submissions(ctx, hub: _hub.Object).GetSessionSubmissionsAsync(seed.Code);

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
        var rows = await TestServices.Submissions(ctx, hub: _hub.Object).GetSessionSubmissionsAsync(code);

        // Then — empty, NOT null. "200 []" and "404" are different answers.
        Assert.NotNull(rows);
        Assert.Empty(rows);
    }

    [Fact]
    public async Task GetSessionSubmissionsAsync_ReturnsEveryStudentAndAssignmentInOneFlatList()
    {
        // Given
        var seed = await RoomSeeder.WithAttemptsAsync(_fixture);

        // When
        await using var ctx = _fixture.CreateContext();
        var rows = await TestServices.Submissions(ctx, hub: _hub.Object).GetSessionSubmissionsAsync(seed.Code);

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
        var seed = await RoomSeeder.WithAttemptsAsync(_fixture);

        // When
        await using var ctx = _fixture.CreateContext();
        var rows = await TestServices.Submissions(ctx, hub: _hub.Object).GetSessionSubmissionsAsync(seed.Code);

        // Then — sorted by submittedAt desc only, not pre-grouped by anything.
        Assert.NotNull(rows);
        Assert.Equal([seed.Newest, seed.Middle, seed.Oldest], rows.Select(r => r.SubId).ToArray());
    }

    [Fact]
    public async Task GetSessionSubmissionsAsync_DerivesStatusFromPassedIncludingNull()
    {
        // Given — middle passed, oldest failed, newest was never graded.
        var seed = await RoomSeeder.WithAttemptsAsync(_fixture);

        // When
        await using var ctx = _fixture.CreateContext();
        var rows = await TestServices.Submissions(ctx, hub: _hub.Object).GetSessionSubmissionsAsync(seed.Code);

        // Then — an ungraded (null) attempt reads as "passed", same as the frontend's
        // existing `passed !== false` convention for a result with no execution error.
        Assert.NotNull(rows);
        Assert.Equal("passed", rows.Single(r => r.SubId == seed.Middle).Status);
        Assert.Equal("tried", rows.Single(r => r.SubId == seed.Oldest).Status);
        Assert.Equal("passed", rows.Single(r => r.SubId == seed.Newest).Status);
    }

    [Fact]
    public async Task GetSessionSubmissionsAsync_ExcludesSoloSubmissions()
    {
        // Given — the same student also practices solo (sessionId null).
        var seed = await RoomSeeder.WithAttemptsAsync(_fixture);
        await using (var write = _fixture.CreateContext())
        {
            write.Submission.Add(TestData.MakeSubmission("student-maria", seed.AssignmentA, null));
            await write.SaveChangesAsync();
        }

        // When
        await using var ctx = _fixture.CreateContext();
        var rows = await TestServices.Submissions(ctx, hub: _hub.Object).GetSessionSubmissionsAsync(seed.Code);

        // Then — solo work belongs to no room's dashboard.
        Assert.NotNull(rows);
        Assert.Equal(3, rows.Count);
    }

    [Fact]
    public async Task GetSessionSubmissionsAsync_ExcludesOtherRoomsAttempts()
    {
        // Given — a second, unrelated room with its own attempt.
        var seed = await RoomSeeder.WithAttemptsAsync(_fixture);
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
        var rows = await TestServices.Submissions(ctx, hub: _hub.Object).GetSessionSubmissionsAsync(seed.Code);

        // Then
        Assert.NotNull(rows);
        Assert.DoesNotContain(rows, r => r.SubId == straySubId);
        Assert.Equal(3, rows.Count);
    }

    [Fact]
    public async Task GetSessionSubmissionsAsync_NormalizesCase()
    {
        // Given — codes are stored uppercase; a caller may send anything.
        var seed = await RoomSeeder.WithAttemptsAsync(_fixture);

        // When
        await using var ctx = _fixture.CreateContext();
        var rows = await TestServices.Submissions(ctx, hub: _hub.Object).GetSessionSubmissionsAsync($"  {seed.Code.ToLowerInvariant()} ");

        // Then
        Assert.NotNull(rows);
        Assert.Equal(3, rows.Count);
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
        await TestServices.Submissions(ctx, hub: _hub.Object).SubmitAsync(seed.AssignmentId, request);

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
        var service = TestServices.Submissions(ctx, hub: _hub.Object);
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
        Assert.Equal("passed", row.Status);
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
        await TestServices.Submissions(ctx, hub: _hub.Object).SubmitAsync(seed.AssignmentId, request);

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
        await TestServices.Submissions(ctx, hub: _hub.Object).SubmitAsync(seed.AssignmentId, request);

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
        await TestServices.Submissions(ctx, hub: _hub.Object).SubmitAsync(seed.AssignmentId, request);

        // Then — only `project` is excluded, not failures.
        var row = Assert.IsType<SessionSubmissionDto>(_hub.Single().Args[0]);
        Assert.Equal("tried", row.Status);
    }

    [Fact]
    public async Task SubmitAsync_UngradedCodeKindThatRanFine_StillBroadcastsAsPassed()
    {
        // Given — a code assignment with no grading rules: passed stays null, but the
        // default fake executor reports a clean run, so there's no error to surface.
        var seed = await SeedSubmittableRoomAsync(AssignmentKind.Code);

        // When
        await using var ctx = _fixture.CreateContext();
        var request = new SubmissionRequestDto("student-maria", seed.Code,
                                               JsonSerializer.SerializeToElement("class Main {}"));
        await TestServices.Submissions(ctx, hub: _hub.Object).SubmitAsync(seed.AssignmentId, request);

        // Then — `passed: null` derives to "passed" (same rule as the hydrated list),
        // matching how the frontend already treated an unresolved null before this DTO existed.
        var row = Assert.IsType<SessionSubmissionDto>(_hub.Single().Args[0]);
        Assert.Equal("passed", row.Status);
    }

    [Fact]
    public async Task SubmitAsync_UngradedCodeKindThatFailsToCompile_BroadcastsAsError()
    {
        // Given — a code assignment with no grading rules, but the code doesn't compile.
        // Before this DTO carried a status, `passed: null` here would have misread as
        // "passed" — this is the case the `status` field exists to fix.
        var seed = await SeedSubmittableRoomAsync(AssignmentKind.Code);
        var executor = new FakeExecutorService(new ExecuteResponseDto(ExecuteStatus.COMPILE_ERROR, "", "error: ';' expected"));

        // When
        await using var ctx = _fixture.CreateContext();
        var request = new SubmissionRequestDto("student-maria", seed.Code,
                                               JsonSerializer.SerializeToElement("class Main {"));
        await TestServices.Submissions(ctx, executor, _hub.Object).SubmitAsync(seed.AssignmentId, request);

        // Then
        var row = Assert.IsType<SessionSubmissionDto>(_hub.Single().Args[0]);
        Assert.Equal("error", row.Status);
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
        await TestServices.Submissions(ctx, hub: _hub.Object).SubmitAsync(seed.AssignmentId, request);

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
        var response = await TestServices.Submissions(ctx, hub: _hub.Object).SubmitAsync(seed.AssignmentId, request);

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
        var response = await TestServices.Submissions(ctx, hub: _hub.Object).SubmitAsync(999999, request);

        // Then — nothing was recorded, so nothing is announced.
        Assert.Null(response);
        Assert.Empty(_hub.Sent);
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using cobblersBackend.Data.Entities;
using cobblersBackend.Models;
using cobblersBackend.Services;
using cobblersBackend.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace cobblersBackend.Tests;

/// <summary>
/// End-to-end over HTTP: routes, status codes and wire JSON. These are the
/// assertions the service tests structurally cannot make — a service returning
/// <c>null</c> proves nothing about whether the controller turns it into a 404,
/// which is exactly how the /attendance endpoint shipped answering 200 on an
/// unknown room. Runs the real app via <see cref="ApiFactory"/>.
/// </summary>
[Collection("db")]
public sealed class ApiEndpointTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ApiEndpointTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _factory = new ApiFactory(fixture.ConnectionString);
        _client = _factory.CreateClient();
    }

    public async Task InitializeAsync() => await _fixture.ResetAsync();

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

    /// <summary>An assignment set with one predict assignment in it, plus a live room.</summary>
    private async Task<(string Code, string SetId, int AssignmentId)> SeedRoomAsync(
        SessionStatus status = SessionStatus.Active)
    {
        await using var ctx = _fixture.CreateContext();
        var set = TestData.MakeAssignmentSet();
        ctx.AssignmentSet.Add(set);
        await ctx.SaveChangesAsync();

        var assignment = TestData.MakeAssignment(AssignmentKind.Predict);
        assignment.GradingJson = """{"predict":{"compare":"normalized","expectedOutput":"42"}}""";
        ctx.Assignment.Add(assignment);
        await ctx.SaveChangesAsync();

        ctx.AssignmentSetAssignment.Add(
            TestData.MakeAssignmentSetAssignment(set.AssignmentSetId, assignment.Id, 0));
        var session = TestData.MakeSession(set.AssignmentSetId, status: status);
        ctx.Session.Add(session);
        await ctx.SaveChangesAsync();

        return (session.Code, set.AssignmentSetId, assignment.Id);
    }

    // ── Assignment sets ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetAssignmentSets_EmptyDatabase_Is200WithEmptyArray()
    {
        var response = await _client.GetAsync("/api/assignmentsets");

        // The frontend's first call on load — an empty seed must not 500.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JsonValueKind.Array, (await ReadJson(response)).ValueKind);
    }

    [Fact]
    public async Task GetAssignments_UnknownSet_Is404()
    {
        var response = await _client.GetAsync("/api/assignmentsets/no-such-set/assignments");

        // This is the exact failure the workshop hit when the seed hadn't run:
        // the solo set id resolving to 404 is what the "Could not load the
        // assignments" toast is made of.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAssignments_KnownSet_Is200AndCamelCase()
    {
        var seed = await SeedRoomAsync();

        var response = await _client.GetAsync($"/api/assignmentsets/{seed.SetId}/assignments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var row = (await ReadJson(response)).EnumerateArray().Single();

        // Program sets a snake_case naming policy globally and every DTO the
        // frontend reads overrides it with [JsonPropertyName]. A field added
        // without the attribute would silently ship as snake_case, so pin the
        // camelCase keys CONTRACT.md promises.
        Assert.Equal(seed.AssignmentId, row.GetProperty("id").GetInt32());
        Assert.Equal("predict", row.GetProperty("kind").GetString());
        Assert.True(row.TryGetProperty("title", out _));
        Assert.True(row.TryGetProperty("description", out _));
        Assert.True(row.TryGetProperty("content", out _));

        // Never served to students: grading rules and the reference answer.
        Assert.False(row.TryGetProperty("gradingJson", out _));
        Assert.False(row.TryGetProperty("grading_json", out _));
        Assert.False(row.TryGetProperty("sampleSolutionJson", out _));
        Assert.False(row.TryGetProperty("slug", out _));

        // Optional fields are omitted entirely rather than sent as null.
        Assert.False(row.TryGetProperty("hint", out _));
        Assert.False(row.TryGetProperty("lesson", out _));
    }

    // ── Sessions ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateSession_BlankAssignmentSetId_Is400()
    {
        var response = await _client.PostAsync("/api/sessions", Json("""{"assignmentSetId":""}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateSession_UnknownAssignmentSet_Is400NotA500()
    {
        var response = await _client.PostAsync("/api/sessions", Json("""{"assignmentSetId":"nope"}"""));

        // The service throws InvalidOperationException; the controller has to
        // catch it, or a teacher typo becomes a 500.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateSession_ThenGet_RoundTripsTheCode()
    {
        var seed = await SeedRoomAsync();

        var created = await _client.PostAsync("/api/sessions", Json($$"""{"assignmentSetId":"{{seed.SetId}}"}"""));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var code = (await ReadJson(created)).GetProperty("code").GetString();

        var fetched = await _client.GetAsync($"/api/sessions/{code}");

        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal(seed.SetId, (await ReadJson(fetched)).GetProperty("assignmentSetId").GetString());
    }

    [Fact]
    public async Task GetSession_LowercaseCode_Resolves()
    {
        var seed = await SeedRoomAsync();

        var response = await _client.GetAsync($"/api/sessions/{seed.Code.ToLowerInvariant()}");

        // Students type the code off a slide; the controller must normalize.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSession_UnknownCode_Is404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/sessions/ZZZZZZ")).StatusCode);
    }

    [Fact]
    public async Task GetTodayLatest_NoRooms_Is404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/sessions/today-latest")).StatusCode);
    }

    [Fact]
    public async Task GetTodayLatest_IsNotShadowedByTheCodeRoute()
    {
        var seed = await SeedRoomAsync();

        var response = await _client.GetAsync("/api/sessions/today-latest");

        // "today-latest" and "{code}" are both GET /api/sessions/<one segment>.
        // A literal segment must win over the parameter, or this silently
        // becomes a lookup for a room literally called TODAY-LATEST.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(seed.Code, (await ReadJson(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task StartTimer_Is200WithEndsAt_AndUnknownRoomIs404()
    {
        var seed = await SeedRoomAsync();

        var ok = await _client.PostAsync($"/api/sessions/{seed.Code}/timer", Json("""{"durationMinutes":5}"""));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace((await ReadJson(ok)).GetProperty("endsAt").GetString()));

        var missing = await _client.PostAsync("/api/sessions/ZZZZZZ/timer", Json("""{"durationMinutes":5}"""));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task EndSession_Is204_ThenEverySessionLookupIs404()
    {
        var seed = await SeedRoomAsync();

        Assert.Equal(HttpStatusCode.NoContent,
                     (await _client.PostAsync($"/api/sessions/{seed.Code}/end", null)).StatusCode);

        // The whole "ended ⇒ not found" rule, asserted in one place at the layer
        // the frontend actually sees.
        Assert.Equal(HttpStatusCode.NotFound,
                     (await _client.PostAsync($"/api/sessions/{seed.Code}/end", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/sessions/{seed.Code}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/sessions/{seed.Code}/attendance")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/sessions/{seed.Code}/submissions")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
                     (await _client.PostAsync($"/api/sessions/{seed.Code}/timer", Json("""{"durationMinutes":5}"""))).StatusCode);
    }

    // ── Teacher dashboard hydration ──────────────────────────────────────────

    [Fact]
    public async Task GetAttendance_UnknownRoom_Is404NotAnEmptyArray()
    {
        var response = await _client.GetAsync("/api/sessions/ZZZZZZ/attendance");

        // THE regression test. The endpoint shipped answering 200 [] here
        // because the service's non-nullable return made the controller's
        // `rows is null` branch unreachable.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAttendance_LiveRoomNobodyJoined_Is200WithEmptyArray()
    {
        var seed = await SeedRoomAsync();

        var response = await _client.GetAsync($"/api/sessions/{seed.Code}/attendance");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await ReadJson(response)).EnumerateArray());
    }

    [Fact]
    public async Task GetAttendance_ReturnsStudentIdAndDisplayName()
    {
        var seed = await SeedRoomAsync();
        await using (var ctx = _fixture.CreateContext())
        {
            ctx.Student.Add(TestData.MakeStudent("student-maria", "Maria"));
            await ctx.SaveChangesAsync();
            await new AttendanceService(ctx).RecordAttendanceAsync(seed.Code, "student-maria");
        }

        var response = await _client.GetAsync($"/api/sessions/{seed.Code}/attendance");

        // CONTRACT.md: `[{ studentId, displayName }]`, camelCase. The dashboard
        // joins Col 3's rows against this for names.
        var row = (await ReadJson(response)).EnumerateArray().Single();
        Assert.Equal("student-maria", row.GetProperty("studentId").GetString());
        Assert.Equal("Maria", row.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task GetSessionSubmissions_UnknownRoom_Is404()
    {
        Assert.Equal(HttpStatusCode.NotFound,
                     (await _client.GetAsync("/api/sessions/ZZZZZZ/submissions")).StatusCode);
    }

    [Fact]
    public async Task GetSessionSubmissions_LiveRoomNoAttempts_Is200WithEmptyArray()
    {
        var seed = await SeedRoomAsync();

        var response = await _client.GetAsync($"/api/sessions/{seed.Code}/submissions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await ReadJson(response)).EnumerateArray());
    }

    [Fact]
    public async Task GetSessionSubmissions_RowShapeMatchesTheContract()
    {
        var seed = await SeedRoomAsync();
        await _client.PutAsync("/api/students/student-maria", Json("""{"displayName":"Maria"}"""));
        await _client.PostAsync($"/api/assignments/{seed.AssignmentId}/submissions",
            Json($$"""{"studentId":"student-maria","sessionId":"{{seed.Code}}","content":"42"}"""));

        var response = await _client.GetAsync($"/api/sessions/{seed.Code}/submissions");

        var row = (await ReadJson(response)).EnumerateArray().Single();
        // The post-contract-change shape: assignmentId in, displayName out.
        Assert.Equal(seed.AssignmentId, row.GetProperty("assignmentId").GetInt32());
        Assert.Equal("student-maria", row.GetProperty("studentId").GetString());
        Assert.True(row.GetProperty("passed").GetBoolean());
        Assert.False(row.TryGetProperty("displayName", out _));
        Assert.True(row.TryGetProperty("subId", out _));
        Assert.True(row.TryGetProperty("submittedAt", out _));
    }

    // ── Students ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertStudent_Is204_AndHistoryStartsEmpty()
    {
        var upsert = await _client.PutAsync("/api/students/student-1", Json("""{"displayName":"Maria"}"""));
        Assert.Equal(HttpStatusCode.NoContent, upsert.StatusCode);

        var history = await _client.GetAsync("/api/students/student-1/submissions");
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        Assert.Empty((await ReadJson(history)).EnumerateArray());
    }

    [Fact]
    public async Task GetStudentHistory_UnknownStudent_Is200WithEmptyArray()
    {
        var response = await _client.GetAsync("/api/students/nobody/submissions");

        // Deliberately not a 404: "you haven't submitted anything" is a valid,
        // non-exceptional state for the My Progress panel.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await ReadJson(response)).EnumerateArray());
    }

    // ── Submissions ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_UnknownAssignment_Is404()
    {
        await _client.PutAsync("/api/students/student-1", Json("""{"displayName":"Maria"}"""));

        var response = await _client.PostAsync("/api/assignments/999999/submissions",
            Json("""{"studentId":"student-1","content":"42"}"""));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Submit_UnknownStudent_Is400NotA500()
    {
        var seed = await SeedRoomAsync();

        var response = await _client.PostAsync($"/api/assignments/{seed.AssignmentId}/submissions",
            Json("""{"studentId":"nobody","content":"42"}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Submit_SoloThenDetail_RoundTripsWithNullSessionId()
    {
        var seed = await SeedRoomAsync();
        await _client.PutAsync("/api/students/student-1", Json("""{"displayName":"Maria"}"""));

        var submitted = await _client.PostAsync($"/api/assignments/{seed.AssignmentId}/submissions",
            Json("""{"studentId":"student-1","content":"42"}"""));
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        var subId = (await ReadJson(submitted)).GetProperty("subId").GetString();

        var detail = await _client.GetAsync($"/api/submissions/{subId}");

        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var body = await ReadJson(detail);
        // Solo practice has no room; the wire must carry a real null, not "".
        Assert.Equal(JsonValueKind.Null, body.GetProperty("sessionId").ValueKind);
        Assert.Equal(seed.AssignmentId, body.GetProperty("assignmentId").GetInt32());
    }

    [Fact]
    public async Task GetSubmission_UnknownGuid_Is404()
    {
        Assert.Equal(HttpStatusCode.NotFound,
                     (await _client.GetAsync($"/api/submissions/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task GetSubmission_MalformedGuid_Is404FromTheRouteConstraint()
    {
        // {subId:guid} rejects it before the action runs — a 400 here would mean
        // the constraint was dropped and the action is parsing ids by hand.
        Assert.Equal(HttpStatusCode.NotFound,
                     (await _client.GetAsync("/api/submissions/not-a-guid")).StatusCode);
    }

    // ── Solution ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSolution_UnknownAssignment_Is404()
    {
        Assert.Equal(HttpStatusCode.NotFound,
                     (await _client.GetAsync("/api/assignments/999999/solution")).StatusCode);
    }

    [Fact]
    public async Task GetSolution_NoStoredSolution_Is200WithNull()
    {
        var seed = await SeedRoomAsync();

        var response = await _client.GetAsync($"/api/assignments/{seed.AssignmentId}/solution");

        // "No reference answer authored" is a 200 with `solution: null`, not a
        // 404 — 404 means the assignment itself doesn't exist.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await ReadJson(response)).GetProperty("solution").ValueKind);
    }

    [Fact]
    public async Task GetSolution_IsNotGatedOnHavingSubmitted()
    {
        var seed = await SeedRoomAsync();
        await using (var ctx = _fixture.CreateContext())
        {
            var assignment = await ctx.Assignment.SingleAsync(a => a.Id == seed.AssignmentId);
            assignment.SampleSolutionJson = "\"public class Main {}\"";
            await ctx.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/assignments/{seed.AssignmentId}/solution");

        // The reveal gate lives in the frontend by decision (CONTRACT.md
        // "Solution"); the backend hands it over to anyone who asks. If someone
        // later adds a server-side ?studentId gate, this fails first.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("public class Main {}", (await ReadJson(response)).GetProperty("solution").GetString());
    }

    // ── Execute ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_SingleFile_Is200WithLowercaseStatus()
    {
        var response = await _client.PostAsync("/api/execute",
            Json("""{"code":"public class Main {}"}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJson(response);
        // The enum serializes via JsonStringEnumMemberName — lowercase on the
        // wire, per CONTRACT.md, even though the C# member is SUCCESS.
        Assert.Equal("success", body.GetProperty("status").GetString());
        Assert.Equal("hi\n", body.GetProperty("stdout").GetString());
    }

    [Fact]
    public async Task Execute_NonZeroExit_ClassifiesAsRuntimeError()
    {
        _factory.PistonResponse = new PistonExecuteResponse
        {
            Run = new PistonStage("", "Exception in thread \"main\"", "", 1, null)
        };

        var response = await _client.PostAsync("/api/execute", Json("""{"code":"public class Main {}"}"""));

        Assert.Equal("runtime_error", (await ReadJson(response)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Execute_EmptyCode_Is400()
    {
        Assert.Equal(HttpStatusCode.BadRequest,
                     (await _client.PostAsync("/api/execute", Json("""{"code":""}"""))).StatusCode);
    }

    [Fact]
    public async Task Execute_FilesPlusEntryClass_Is200()
    {
        var response = await _client.PostAsync("/api/execute", Json("""
            {"files":[{"name":"Main.java","content":"public class Main {}"},
                      {"name":"Person.java","content":"public class Person {}"}],
             "entryClass":"Main"}
            """));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("success", (await ReadJson(response)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Execute_FilesWithoutEntryClass_Is400()
    {
        var response = await _client.PostAsync("/api/execute", Json("""
            {"files":[{"name":"Main.java","content":"public class Main {}"}]}
            """));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("entryClass", await response.Content.ReadAsStringAsync());
    }

    // ── Metrics ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task MetricsEndpoint_IsExposedForPrometheus()
    {
        var response = await _client.GetAsync("/metrics");

        // Prometheus scrapes this; the Grafana dashboards are built on it.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("cobblers_", await response.Content.ReadAsStringAsync());
    }
}
